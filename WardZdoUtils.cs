using System;
using System.Collections.Generic;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardZdoUtils
    {
        internal const string GuardStonePrefabName = "guard_stone";
        internal static readonly int s_guardStonePrefabHash = GuardStonePrefabName.GetStableHashCode();

        internal static bool IsGuardStonePrefab(GameObject gameObject)
        {
            return gameObject != null && Utils.GetPrefabName(gameObject) == GuardStonePrefabName;
        }

        internal static bool IsGuardStoneZdo(ZDO zdo)
        {
            return zdo != null && zdo.GetPrefab() == s_guardStonePrefabHash;
        }

        internal static IEnumerable<ZDO> GetAllGuardStoneZdos()
        {
            if (ZDOMan.instance == null)
                yield break;

            foreach (KeyValuePair<ZDOID, ZDO> pair in ZDOMan.instance.m_objectsByID)
            {
                ZDO zdo = pair.Value;
                if (IsGuardStoneZdo(zdo))
                    yield return zdo;
            }
        }

        internal static int CountGuardStonesByCreator(long creatorID)
        {
            if (creatorID == 0L)
                return 0;

            int count = 0;
            foreach (ZDO zdo in GetAllGuardStoneZdos())
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

            if (!IsGuardStoneZdo(zdo))
                return false;

            if (GetCreatorID(zdo) == playerID)
                return true;

            return IsPermitted(zdo, playerID);
        }

        internal static float GetGuardStoneRadius(ZDO zdo)
        {
            if (zdo == null)
                return 10f;

            bool useCustomRange = HasZdoBool(zdo, s_customRange)
                ? zdo.GetBool(s_customRange, setWardRange.Value)
                : HasZdoFloat(zdo, s_range) || GetWardBoolSetting(zdo, s_customRange, setWardRange.Value);

            return useCustomRange ? GetWardFloatSetting(zdo, s_range, wardRange.Value) : 10f;
        }

        internal static bool AreGuardStoneZdosOverlapping(ZDO protectedWard, ZDO candidateWard)
        {
            if (!IsGuardStoneZdo(protectedWard) || !IsGuardStoneZdo(candidateWard))
                return false;

            float protectedRadius = GetGuardStoneRadius(protectedWard);
            float candidateRadius = GetGuardStoneRadius(candidateWard);
            return Utils.DistanceXZ(protectedWard.GetPosition(), candidateWard.GetPosition()) <= protectedRadius + candidateRadius;
        }

        internal static bool CanShareConnectedAccess(ZDO protectedWard, ZDO candidateWard, WardConnectedAccessMode mode)
        {
            if (mode == WardConnectedAccessMode.Off)
                return false;

            if (!IsGuardStoneZdo(protectedWard) || !IsGuardStoneZdo(candidateWard))
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
            if (!IsGuardStoneZdo(rootWard))
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

                foreach (ZDO candidate in GetAllGuardStoneZdos())
                {
                    if (candidate == null || visited.Contains(candidate.m_uid))
                        continue;

                    if (isActiveCandidate != null && !isActiveCandidate(candidate))
                        continue;

                    if (!AreGuardStoneZdosOverlapping(current, candidate))
                        continue;

                    // Connected sharing rules are checked against the protected/root ward,
                    // matching the loaded PrivateArea connected-access logic.
                    if (!CanShareConnectedAccess(rootWard, candidate, mode))
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
