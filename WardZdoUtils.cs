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
        private static bool s_wardDefaultRadiusCached;
        private static float s_wardDefaultRadius = 32f;

        internal static bool IsWardPrefab(GameObject gameObject)
        {
            return gameObject != null && Utils.GetPrefabName(gameObject) == WardPrefabName;
        }

        internal static bool IsWardZdo(ZDO zdo)
        {
            return zdo != null && zdo.GetPrefab() == s_wardPrefabHash;
        }

        internal static IEnumerable<ZDO> GetAllWardZdos()
        {
            if (ZDOMan.instance == null)
                yield break;

            foreach (KeyValuePair<ZDOID, ZDO> pair in ZDOMan.instance.m_objectsByID)
            {
                ZDO zdo = pair.Value;
                if (IsWardZdo(zdo))
                    yield return zdo;
            }
        }

        internal static int CountWardsByCreator(long creatorID)
        {
            if (creatorID == 0L)
                return 0;

            int count = 0;
            foreach (ZDO zdo in GetAllWardZdos())
            {
                if (GetCreatorID(zdo) == creatorID)
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

        internal static long GetCreatorID(ZDO zdo)
        {
            return zdo != null ? zdo.GetLong(ZDOVars.s_creator, 0L) : 0L;
        }

        internal static bool IsPermitted(ZDO zdo, long playerID)
        {
            if (zdo == null || playerID == 0L)
                return false;

            if (permitEveryone != null && permitEveryone.Value)
                return true;

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

            if (!IsWardZdo(zdo))
                return false;

            if (GetCreatorID(zdo) == playerID)
                return true;

            return IsPermitted(zdo, playerID);
        }

        internal static bool UseCustomWardRange(ZDO zdo)
        {
            if (zdo == null)
                return false;

            bool fallback = wardSettingsUseDefaultsForAllWards.Value ? setWardRange.Value : false;
            if (HasZdoFloat(zdo, s_range))
                fallback = true;

            return zdo.GetBool(s_customRange, fallback);
        }

        internal static float GetConfiguredWardRange(ZDO zdo)
        {
            return zdo != null ? zdo.GetFloat(s_range, wardSettingsUseDefaultsForAllWards.Value ? wardRange.Value : GetWardDefaultRadius()) : wardRange.Value;
        }

        internal static float GetWardDefaultRadius()
        {
            if (s_wardDefaultRadiusCached)
                return s_wardDefaultRadius;

            if (ZNetScene.instance != null)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(WardPrefabName);
                PrivateArea prefabWard = prefab != null ? prefab.GetComponent<PrivateArea>() : null;
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
            if (!IsWardZdo(protectedWard) || !IsWardZdo(candidateWard))
                return false;

            float protectedRadius = GetWardRadius(protectedWard);
            float candidateRadius = GetWardRadius(candidateWard);
            return Utils.DistanceXZ(protectedWard.GetPosition(), candidateWard.GetPosition()) <= protectedRadius + candidateRadius;
        }

        internal static bool CanShareConnectedWardAccess(ZDO protectedWard, ZDO candidateWard, WardConnectedAccessMode mode)
        {
            if (mode == WardConnectedAccessMode.Off)
                return false;

            if (!IsWardZdo(protectedWard) || !IsWardZdo(candidateWard))
                return false;

            if (protectedWard == candidateWard || protectedWard.m_uid.Equals(candidateWard.m_uid))
                return true;

            switch (mode)
            {
                case WardConnectedAccessMode.SameCreatorOnly:
                    long protectedCreator = GetCreatorID(protectedWard);
                    long candidateCreator = GetCreatorID(candidateWard);
                    return protectedCreator != 0L && protectedCreator == candidateCreator;

                case WardConnectedAccessMode.MutualTrust:
                    protectedCreator = GetCreatorID(protectedWard);
                    candidateCreator = GetCreatorID(candidateWard);
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
            if (!IsWardZdo(rootWard))
                yield break;

            HashSet<ZDOID> visited = new HashSet<ZDOID>();
            List<ZDO> queue = new List<ZDO>();
            int queueIndex = 0;

            visited.Add(rootWard.m_uid);
            queue.Add(rootWard);

            while (queueIndex < queue.Count)
            {
                ZDO current = queue[queueIndex++];
                yield return current;

                if (mode == WardConnectedAccessMode.Off)
                    continue;

                foreach (ZDO candidate in GetAllWardZdos())
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
    }
}
