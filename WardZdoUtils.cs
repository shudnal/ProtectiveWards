using HarmonyLib;
using ProtectiveWards.Compatibility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardZdoUtils
    {
        internal const string WardPrefabName = "guard_stone";
        internal static readonly int s_wardPrefabHash = WardPrefabName.GetStableHashCode();
        private static readonly HashSet<ZDO> s_wardObjects = new();
        private static bool s_wardObjectsInitialized;
        private static bool s_wardDefaultRadiusCached;
        private static float s_wardDefaultRadius = 32f;

        internal static bool IsWardPrefab(GameObject gameObject) => gameObject != null && Utils.GetPrefabName(gameObject) == WardPrefabName;

        internal static bool IsWard(ZDO zdo) => zdo != null && zdo.GetPrefab() == s_wardPrefabHash;

        internal static IEnumerable<ZDO> GetAllWards()
        {
            if (ShouldTrackServerWards())
            {
                EnsureWardObjectsInitialized();
                PruneWardObjects();

                foreach (ZDO zdo in s_wardObjects)
                    yield return zdo;

                yield break;
            }

            HashSet<ZDOID> visited = new();
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                ZDO zdo = area?.m_nview?.IsValid() == true ? area.m_nview.GetZDO() : null;
                if (!IsWard(zdo) || !visited.Add(zdo.m_uid))
                    continue;

                yield return zdo;
            }
        }

        internal static int CountWardsByCreator(long creatorID)
        {
            if (creatorID == 0L)
                return 0;

            int count = 0;
            foreach (ZDO zdo in GetAllWards())
            {
                if (zdo.IsCreator(creatorID))
                    count++;
            }

            return count;
        }

        internal static PrivateArea FindLoadedWard(ZDOID zdoID)
        {
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || area.m_nview == null || !area.m_nview.IsValid())
                    continue;

                ZDO zdo = area.m_nview.GetZDO();
                if (zdo != null && zdo.m_uid.Equals(zdoID))
                    return area;
            }

            return null;
        }

        internal static ZDO GetWard(ZDOID zdoID)
        {
            ZDO zdo = ZDOMan.instance?.GetZDO(zdoID);
            return IsWard(zdo) ? zdo : null;
        }

        internal static bool TryGetWard(ZDOID zdoID, out ZDO zdo)
        {
            zdo = GetWard(zdoID);
            return zdo != null;
        }

        internal static List<KeyValuePair<long, string>> GetPermittedPlayers(ZDO zdo)
        {
            List<KeyValuePair<long, string>> permitted = new();
            if (!IsWard(zdo))
                return permitted;

            int count = Math.Max(zdo.GetInt(ZDOVars.s_permitted, 0), 0);
            for (int i = 0; i < count; i++)
            {
                long playerID = zdo.GetLong("pu_id" + i, 0L);
                if (playerID == 0L)
                    continue;

                permitted.Add(new KeyValuePair<long, string>(playerID, zdo.GetString("pu_name" + i, "")));
            }

            return permitted;
        }

        internal static bool IsExplicitlyPermitted(ZDO zdo, long playerID)
        {
            return playerID != 0L && GetPermittedPlayers(zdo).Any(player => player.Key == playerID);
        }

        internal static bool AddPermitted(ZDO zdo, long playerID, string playerName)
        {
            if (!IsWard(zdo) || playerID == 0L)
                return false;

            List<KeyValuePair<long, string>> permitted = GetPermittedPlayers(zdo);
            if (permitted.Any(player => player.Key == playerID))
                return false;

            permitted.Add(new KeyValuePair<long, string>(playerID, playerName ?? ""));
            SetPermittedPlayers(zdo, permitted);
            return true;
        }

        internal static bool RemovePermitted(ZDO zdo, long playerID, out string playerName)
        {
            playerName = "";
            if (!IsWard(zdo) || playerID == 0L)
                return false;

            List<KeyValuePair<long, string>> permitted = GetPermittedPlayers(zdo);
            KeyValuePair<long, string> target = permitted.FirstOrDefault(player => player.Key == playerID);
            if (target.Key == 0L)
                return false;

            playerName = target.Value ?? "";
            permitted.RemoveAll(player => player.Key == playerID);
            SetPermittedPlayers(zdo, permitted);
            return true;
        }

        internal static void SetPermittedPlayers(ZDO zdo, List<KeyValuePair<long, string>> permitted)
        {
            if (!IsWard(zdo))
                return;

            permitted ??= new List<KeyValuePair<long, string>>();
            zdo.Set(ZDOVars.s_permitted, permitted.Count);
            for (int i = 0; i < permitted.Count; i++)
            {
                zdo.Set("pu_id" + i, permitted[i].Key);
                zdo.Set("pu_name" + i, permitted[i].Value ?? "");
            }
        }

        internal static bool IsPermitted(ZDO zdo, long playerID)
        {
            if (zdo == null || playerID == 0L)
                return false;

            if (HasWardManagementAccess(zdo, playerID))
                return true;

            if (IsExplicitlyPermitted(zdo, playerID))
                return true;

            return GuildsCompat.HasWardGuildAccess(zdo, playerID);
        }

        internal static bool HasDirectAccessToWardZdo(ZDO zdo, long playerID)
        {
            if (zdo == null)
                return true;

            if (playerID == 0L)
                return false;

            if (!IsWard(zdo))
                return false;

            if (zdo.IsCreator(playerID))
                return true;

            return IsPermitted(zdo, playerID);
        }

        internal static bool UseCustomWardRange(ZDO zdo)
        {
            if (!ArePerWardSettingsEnabled())
                return setWardRange.Value;

            if (zdo == null)
                return false;

            bool fallback = wardSettingsUseDefaultsForAllWards.Value && setWardRange.Value;
            if (HasZdoFloat(zdo, s_range))
                fallback = true;

            return zdo.GetBool(s_customRange, fallback);
        }

        internal static float GetConfiguredWardRange(ZDO zdo)
        {
            if (!ArePerWardSettingsEnabled())
                return wardRange.Value;

            return zdo != null ? zdo.GetFloat(s_range, wardSettingsUseDefaultsForAllWards.Value ? wardRange.Value : GetWardDefaultRadius()) : wardRange.Value;
        }

        internal static float GetWardDefaultRadius()
        {
            if (s_wardDefaultRadiusCached)
                return s_wardDefaultRadius;

            if (ZNetScene.instance != null)
            {
                PrivateArea prefabWard = ZNetScene.instance?.GetPrefab(WardPrefabName)?.GetComponent<PrivateArea>();
                if (prefabWard != null)
                    s_wardDefaultRadius = prefabWard.m_radius;
            }

            s_wardDefaultRadiusCached = true;
            return s_wardDefaultRadius;
        }

        internal static float GetWardRadius(ZDO zdo)
        {
            if (zdo == null)
                return GetWardDefaultRadius();

            return UseCustomWardRange(zdo) ? GetConfiguredWardRange(zdo) : GetWardDefaultRadius();
        }

        internal static bool AreWardZdosOverlapping(ZDO protectedWard, ZDO candidateWard)
        {
            if (!IsWard(protectedWard) || !IsWard(candidateWard))
                return false;

            float protectedRadius = GetWardRadius(protectedWard);
            float candidateRadius = GetWardRadius(candidateWard);
            return GetConfiguredWardDistance(protectedWard.GetPosition(), candidateWard.GetPosition()) <= protectedRadius + candidateRadius;
        }

        internal static bool CanShareConnectedWardAccess(ZDO protectedWard, ZDO candidateWard, WardConnectedAccessMode mode)
        {
            if (mode == WardConnectedAccessMode.Off)
                return false;

            if (!IsWard(protectedWard) || !IsWard(candidateWard))
                return false;

            if (protectedWard == candidateWard || protectedWard.m_uid.Equals(candidateWard.m_uid))
                return true;

            switch (mode)
            {
                case WardConnectedAccessMode.SameCreatorOnly:
                    long protectedCreator = protectedWard.GetCreatorId();
                    long candidateCreator = candidateWard.GetCreatorId();
                    return protectedCreator != 0L && protectedCreator == candidateCreator;

                case WardConnectedAccessMode.MutualTrust:
                    protectedCreator = protectedWard.GetCreatorId();
                    candidateCreator = candidateWard.GetCreatorId();
                    return protectedCreator != 0L
                           && candidateCreator != 0L
                           && HasDirectAccessToWardZdo(protectedWard, candidateCreator)
                           && HasDirectAccessToWardZdo(candidateWard, protectedCreator);

                case WardConnectedAccessMode.AnyConnected:
                    return true;

                default:
                    return false;
            }
        }

        internal static IEnumerable<ZDO> ConnectedAccessWardZdos(ZDO rootWard, WardConnectedAccessMode mode, Func<ZDO, bool> isActiveCandidate)
        {
            if (!IsWard(rootWard))
                yield break;

            HashSet<ZDOID> visited = new();
            List<ZDO> queue = new();
            int queueIndex = 0;

            visited.Add(rootWard.m_uid);
            queue.Add(rootWard);

            while (queueIndex < queue.Count)
            {
                ZDO current = queue[queueIndex++];
                yield return current;

                if (mode == WardConnectedAccessMode.Off)
                    continue;

                foreach (ZDO candidate in GetAllWards())
                {
                    if (candidate == null || visited.Contains(candidate.m_uid))
                        continue;

                    if (isActiveCandidate != null && !isActiveCandidate(candidate))
                        continue;

                    if (!AreWardZdosOverlapping(current, candidate))
                        continue;

                    // Connected sharing rules are checked against the protected/root ward,
                    // matching the loaded PrivateArea connected-access logic.
                    if (!CanShareConnectedWardAccess(rootWard, candidate, mode))
                        continue;

                    visited.Add(candidate.m_uid);
                    queue.Add(candidate);
                }
            }
        }

        internal static bool HasAccessToWardOrConnectedWardZdo(ZDO rootWard, long playerID, WardConnectedAccessMode mode, Func<ZDO, bool> isActiveCandidate)
        {
            if (HasDirectAccessToWardZdo(rootWard, playerID))
                return true;

            if (mode == WardConnectedAccessMode.Off)
                return false;

            foreach (ZDO candidate in ConnectedAccessWardZdos(rootWard, mode, isActiveCandidate))
            {
                if (candidate == null || candidate.m_uid.Equals(rootWard.m_uid))
                    continue;

                if (HasDirectAccessToWardZdo(candidate, playerID))
                    return true;
            }

            return false;
        }

        private static bool ShouldTrackServerWards() => ZNet.instance != null && ZNet.instance.IsServer();

        private static void AddIfWard(ZDO zdo)
        {
            if (ShouldTrackServerWards() && IsWard(zdo))
                s_wardObjects.Add(zdo);
        }

        private static void RemoveIfWard(ZDO zdo)
        {
            if (zdo != null && IsWard(zdo))
                s_wardObjects.Remove(zdo);
        }

        private static void PruneWardObjects() => s_wardObjects.RemoveWhere(zdo => zdo == null || !IsWard(zdo));

        private static void EnsureWardObjectsInitialized()
        {
            if (!ShouldTrackServerWards())
                return;

            ZDOMan zdoMan = ZDOMan.instance;
            if (zdoMan == null)
                return;

            if (!s_wardObjectsInitialized)
            {
                RebuildWardObjects(zdoMan);
                return;
            }

            if (s_wardObjects.Count == 0 && zdoMan.m_objectsByID.Count > 0)
                RebuildWardObjects(zdoMan);
        }

        private static void RebuildWardObjects(ZDOMan zdoMan)
        {
            s_wardObjects.Clear();

            if (!ShouldTrackServerWards() || zdoMan == null)
            {
                s_wardObjectsInitialized = false;
                return;
            }

            s_wardObjectsInitialized = true;

            foreach (KeyValuePair<ZDOID, ZDO> pair in zdoMan.m_objectsByID)
                AddIfWard(pair.Value);
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.Load))]
        private static class ZDOMan_Load_WardListInit
        {
            private static void Postfix(ZDOMan __instance) => RebuildWardObjects(__instance);
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_WardListInit
        {
            private static void Postfix() => EnsureWardObjectsInitialized();
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.CreateNewZDO), new Type[3] { typeof(ZDOID), typeof(Vector3), typeof(int) })]
        private static class ZDOMan_CreateNewZDO_WardListAddNew
        {
            private static void Postfix(int prefabHashIn, ZDO __result)
            {
                if (!ShouldTrackServerWards())
                    return;

                if (prefabHashIn != 0 && prefabHashIn != s_wardPrefabHash)
                    return;

                AddIfWard(__result);
            }
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO))]
        private static class ZDOMan_HandleDestroyedZDO_WardListRemove
        {
            private static void Prefix(ZDOMan __instance, ZDOID uid)
            {
                if (__instance == null)
                    return;

                RemoveIfWard(__instance.GetZDO(uid));
            }
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
        private static class ZDO_Deserialize_WardListAdd
        {
            private static void Postfix(ZDO __instance) => AddIfWard(__instance);
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_WardListClear
        {
            private static void Postfix()
            {
                s_wardObjects.Clear();
                s_wardObjectsInitialized = false;
            }
        }
    }
}
