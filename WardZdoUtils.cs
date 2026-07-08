using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardZdoUtils
    {
        internal const string WardPrefabName = "guard_stone";
        internal static readonly int s_wardPrefabHash = WardPrefabName.GetStableHashCode();
        private static readonly HashSet<ZDO> s_wardObjects = new();
        private static bool s_wardDefaultRadiusCached;
        private static float s_wardDefaultRadius = 32f;

        internal static bool IsWardPrefab(GameObject gameObject) => gameObject != null && Utils.GetPrefabName(gameObject) == WardPrefabName;

        internal static bool IsWard(ZDO zdo) => zdo != null && zdo.GetPrefab() == s_wardPrefabHash;

        internal static IEnumerable<ZDO> GetAllWards()
        {
            if (!ShouldTrackServerWards())
                yield break;

            PruneWardObjects();

            foreach (ZDO zdo in s_wardObjects)
                yield return zdo;
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

        internal static bool IsPermitted(ZDO zdo, long playerID)
        {
            if (zdo == null || playerID == 0L)
                return false;

            if (HasWardAdminAccess(playerID))
                return true;

            int count = Math.Max(zdo.GetInt(ZDOVars.s_permitted, 0), 0);
            for (int i = 0; i < count; i++)
            {
                if (zdo.GetLong("pu_id" + i, 0L) == playerID)
                    return true;
            }

            return false;
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
            if (zdo == null)
                return false;

            bool fallback = wardSettingsUseDefaultsForAllWards.Value && setWardRange.Value;
            if (HasZdoFloat(zdo, s_range))
                fallback = true;

            return zdo.GetBool(s_customRange, fallback);
        }

        internal static float GetConfiguredWardRange(ZDO zdo) => zdo != null ? zdo.GetFloat(s_range, wardSettingsUseDefaultsForAllWards.Value ? wardRange.Value : GetWardDefaultRadius()) : wardRange.Value;

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
            return Utils.DistanceXZ(protectedWard.GetPosition(), candidateWard.GetPosition()) <= protectedRadius + candidateRadius;
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

        private static void RebuildWardObjects(ZDOMan zdoMan)
        {
            s_wardObjects.Clear();

            if (!ShouldTrackServerWards() || zdoMan == null)
                return;

            foreach (KeyValuePair<ZDOID, ZDO> pair in zdoMan.m_objectsByID)
                AddIfWard(pair.Value);
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.Load))]
        private static class ZDOMan_Load_WardListInit
        {
            private static void Postfix(ZDOMan __instance) => RebuildWardObjects(__instance);
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
            private static void Postfix() => s_wardObjects.Clear();
        }
    }
}
