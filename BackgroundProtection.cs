using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class BackgroundProtection
    {
        private static readonly Dictionary<ZDOID, CachedBool> s_qualifiedBaseCache = new();
        private const float QualifiedBaseCacheSeconds = 10f;
        private static readonly Func<ZDO, bool> activeWardPredicate = IsActiveBackgroundWard;

        private struct CachedBool
        {
            public float Time;
            public bool Value;
        }

        internal static void ResetCache() => s_qualifiedBaseCache.Clear();

        internal static bool IsBackgroundProtectionActiveAt(Vector3 point, out ZDO ward)
        {
            foreach (ZDO candidate in GetBackgroundWards(point, point))
            {
                if (!IsQualifiedProtectedBase(candidate) || HasEffectiveAccessPresence(candidate, point))
                    continue;
                ward = candidate;
                return true;
            }
            ward = null;
            return false;
        }

        private static IEnumerable<ZDO> GetBackgroundWards(Vector3 sourcePoint, Vector3 targetPoint)
        {
            bool samePoint = sourcePoint.x == targetPoint.x && sourcePoint.y == targetPoint.y && sourcePoint.z == targetPoint.z;
            if (!TryResolveWardCheckPoint(sourcePoint, out sourcePoint))
                yield break;
            if (samePoint)
                targetPoint = sourcePoint;
            else if (!TryResolveWardCheckPoint(targetPoint, out targetPoint))
                yield break;

            WardConnectedAccessMode mode = wardBackgroundConnectedAccessMode?.Value ?? WardConnectedAccessMode.Off;
            foreach (ZDO area in WardZdoUtils.GetAllWards())
            {
                if (!IsActiveBackgroundWard(area) || !IsInsideWardXZ(area, sourcePoint))
                    continue;

                if (samePoint || IsInsideWardXZ(area, targetPoint))
                {
                    yield return area;
                    continue;
                }
                if (mode == WardConnectedAccessMode.Off)
                    continue;

                foreach (ZDO candidate in WardZdoUtils.ConnectedAccessWardZdos(area, mode, activeWardPredicate))
                {
                    if (!IsInsideWardXZ(candidate, targetPoint))
                        continue;
                    yield return area;
                    break;
                }
            }
        }

        internal static bool HasEffectiveAccessPresence(ZDO ward, Vector3 point)
        {
            if (!WardZdoUtils.IsWard(ward))
                return false;

            WardConnectedAccessMode mode = wardBackgroundConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardBackgroundConnectedAccessMode.Value;
            if (wardBackgroundPresenceMode.Value == WardBackgroundPresenceMode.PermittedOnline)
            {
                foreach (WardPlayers.OnlinePlayer player in WardPlayers.GetOnlinePlayers())
                    if (ward.HasConnectedWardAccess(player.PlayerID, mode, activeWardPredicate))
                        return true;
                return false;
            }

            float radius = Mathf.Max(wardBackgroundPresenceRadius.Value, 0f);

            if (!TryResolveWardCheckPoint(point, out Vector3 resolvedPoint))
                return false;

            WardBackgroundPresenceMode presenceMode = wardBackgroundPresenceMode.Value;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null || !TryResolveWardCheckPoint(player.transform.position, out Vector3 resolvedPlayerPoint))
                    continue;

                // Reject distant players before resolving connected permissions. Access itself remains live.
                if (presenceMode != WardBackgroundPresenceMode.PermittedOnline
                    && presenceMode != WardBackgroundPresenceMode.PermittedInsideConnectedArea)
                {
                    float dx = resolvedPlayerPoint.x - resolvedPoint.x;
                    float dz = resolvedPlayerPoint.z - resolvedPoint.z;
                    if (!(dx * dx + dz * dz <= radius * radius))
                        continue;
                }

                if (!ward.HasConnectedWardAccess(player.GetPlayerID(), mode, activeWardPredicate))
                    continue;

                if (presenceMode != WardBackgroundPresenceMode.PermittedInsideConnectedArea)
                    return true;

                if (IsInsideWardXZ(ward, resolvedPlayerPoint))
                    return true;
                if (mode == WardConnectedAccessMode.Off)
                    continue;
                foreach (ZDO area in WardZdoUtils.ConnectedAccessWardZdos(ward, mode, activeWardPredicate))
                    if (IsInsideWardXZ(area, resolvedPlayerPoint))
                        return true;
            }

            return false;
        }

        internal static bool IsQualifiedProtectedBase(ZDO ward)
        {
            if (!WardZdoUtils.IsWard(ward))
                return false;

            int minimumPieces = Math.Max(wardBackgroundProtectedBaseMinPieces.Value, 0);
            if (minimumPieces == 0)
                return true;

            if (s_qualifiedBaseCache.TryGetValue(ward.m_uid, out CachedBool cached) && Time.time - cached.Time < QualifiedBaseCacheSeconds)
                return cached.Value;

            bool result = CountPlayerBuiltPiecesInNetwork(ward, minimumPieces) >= minimumPieces;
            s_qualifiedBaseCache[ward.m_uid] = new CachedBool { Time = Time.time, Value = result };
            return result;
        }

        private static int CountPlayerBuiltPiecesInNetwork(ZDO ward, int stopAt)
        {
            WardConnectedAccessMode mode = wardBackgroundConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardBackgroundConnectedAccessMode.Value;
            HashSet<Piece> pieces = new();
            List<Piece> buffer = new();

            foreach (ZDO area in WardZdoUtils.ConnectedAccessWardZdos(ward, mode, activeWardPredicate))
            {
                buffer.Clear();
                Piece.GetAllPiecesInRadius(area.GetPosition(), area.GetWardRadius(), buffer);
                foreach (Piece piece in buffer)
                {
                    if (piece == null || !piece.IsPlacedByPlayer())
                        continue;

                    pieces.Add(piece);
                    if (pieces.Count >= stopAt)
                        return pieces.Count;
                }
            }

            return pieces.Count;
        }

        private static bool IsActiveBackgroundWard(ZDO zdo)
        {
            return WardExpiration.IsWardActive(zdo);
        }

        internal static bool ShouldSuppressWearNTearDamage(WearNTear wearNTear, HitData hit, bool isShip, bool isCart)
        {
            if (wearNTear == null || hit == null)
                return false;

            Piece piece = wearNTear.m_piece ?? wearNTear.GetComponent<Piece>();
            bool isPlayerBuiltPiece = piece != null && piece.IsPlacedByPlayer();
            if (!isPlayerBuiltPiece && !isShip && !isCart)
                return false;

            Player attacker = hit.GetAttacker() as Player;
            Vector3 point = wearNTear.transform.position;
            // Each covering root contributes independently. A protecting root wins; load order never grants access.
            foreach (ZDO ward in GetBackgroundWards(point, point))
            {
                if (!IsQualifiedProtectedBase(ward))
                    continue;

                if (wardBackgroundStructureProtection.Value == WardBackgroundStructureProtectionMode.BlockNonPermittedPlayerDamage
                    && isPlayerBuiltPiece && attacker != null
                    && !ward.HasConnectedWardAccess(attacker.GetPlayerID(), wardBackgroundConnectedAccessMode.Value, activeWardPredicate))
                    return true;

                if (HasEffectiveAccessPresence(ward, point))
                    continue;

                if ((wardBackgroundProtectBoats.Value && isShip) || (wardBackgroundProtectCarts.Value && isCart))
                    return true;

                if (isPlayerBuiltPiece
                    && (wardBackgroundStructureProtection.Value == WardBackgroundStructureProtectionMode.BlockAllDamageWhenNoPermittedNearby
                        || (wardBackgroundProtectFire.Value && IsFireDamage(hit))))
                    return true;
            }
            return false;
        }

        internal static bool ShouldSuppressTameDamageToStructure(WearNTear wearNTear, HitData hit)
        {
            if (!wardBackgroundTamesPreventDamageToStructures.Value || wearNTear == null || hit == null || !hit.HaveAttacker())
                return false;

            Piece piece = wearNTear.m_piece ?? wearNTear.GetComponent<Piece>();
            if (piece == null || !piece.IsPlacedByPlayer())
                return false;

            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker.IsPlayer() || !attacker.IsTamed())
                return false;

            foreach (ZDO ward in GetBackgroundWards(attacker.transform.position, wearNTear.transform.position))
                if (IsQualifiedProtectedBase(ward)
                    && !HasEffectiveAccessPresence(ward, attacker.transform.position)
                    && !HasEffectiveAccessPresence(ward, wearNTear.transform.position))
                    return true;
            return false;
        }

        internal static bool ShouldSuppressTameCharacterDamage(Character character)
        {
            if (!wardBackgroundProtectTames.Value)
                return false;

            if (character == null || !character.IsTamed())
                return false;

            return IsBackgroundProtectionActiveAt(character.transform.position, out _);
        }

        internal static bool ShouldPacifyTame(BaseAI ai)
        {
            if (wardBackgroundTamePacify.Value == WardBackgroundTamePacifyMode.Off)
                return false;

            if (ai == null || ai.m_character == null || !ai.m_character.IsTamed())
                return false;

            return IsBackgroundProtectionActiveAt(ai.transform.position, out _);
        }

        internal static void PacifyMonster(MonsterAI ai)
        {
            if (ai == null)
                return;

            ai.SetAlerted(false);
            ai.m_targetCreature = null;
            ai.m_targetStatic = null;
            ai.m_timeSinceAttacking = 0f;
            ai.m_timeSinceSensedTargetCreature = 99999f;
            ai.SetTargetInfo(ZDOID.None);
        }

        internal static void PacifyAnimal(AnimalAI ai)
        {
            if (ai == null)
                return;

            ai.SetAlerted(false);
            ai.m_target = null;
            ai.SetTargetInfo(ZDOID.None);
        }

        internal static bool IsBuildingRestricted(Player player, Vector3 point)
        {
            if (!wardBackgroundPreventBuildingAndDemolishing.Value || player == null)
                return false;

            long playerID = player.GetPlayerID();
            if (HasWardAdminAccess(playerID))
                return false;

            foreach (ZDO ward in GetBackgroundWards(point, point))
                if (IsQualifiedProtectedBase(ward)
                    && !ward.HasConnectedWardAccess(playerID, wardBackgroundConnectedAccessMode.Value, activeWardPredicate)
                    && !HasEffectiveAccessPresence(ward, point))
                    return true;
            return false;
        }

        private static bool IsFireDamage(HitData hit) => hit != null && (hit.m_damage.m_fire > 0f || hit.m_hitType == HitData.HitType.Burning);

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_ResetBackgroundProtectionCache
        {
            private static void Postfix() => ResetCache();
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ResetBackgroundProtectionCache
        {
            private static void Postfix() => ResetCache();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static class Player_TryPlacePiece_PreventBuildingWithoutPermittedPlayersNearby
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Player __instance, Piece piece, ref bool __result)
            {
                if (__instance == null || piece == null || __instance.m_placementGhost == null)
                    return true;

                if (!IsBuildingRestricted(__instance, __instance.m_placementGhost.transform.position))
                    return true;

                __instance.Message(MessageHud.MessageType.Center, "$msg_privatezone");

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.CheckCanRemovePiece))]
        private static class Player_CheckCanRemovePiece_PreventDemolishingWithoutPermittedPlayersNearby
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Player __instance, Piece piece, ref bool __result)
            {
                if (__instance == null || piece == null)
                    return true;

                if (piece.IsCreator())
                    return true;

                if (!IsBuildingRestricted(__instance, piece.transform.position))
                    return true;

                __instance.Message(MessageHud.MessageType.Center, "$msg_privatezone");

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
        private static class WearNTear_Damage_BackgroundProtection
        {
            private static void Prefix(WearNTear __instance, HitData hit)
            {
                if (__instance == null || hit == null)
                    return;

                bool isShip = __instance.GetComponentInParent<Ship>() != null;
                bool isCart = __instance.GetComponentInParent<Vagon>() != null;

                if (vehiclesNeverProtected.Value && (isShip || isCart))
                    return;

                if (ShouldSuppressWearNTearDamage(__instance, hit, isShip, isCart) || ShouldSuppressTameDamageToStructure(__instance, hit))
                    hit.m_damage.Modify(0f);
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        private static class Character_Damage_BackgroundTameProtection
        {
            private static void Prefix(Character __instance, HitData hit)
            {
                if (ShouldSuppressTameCharacterDamage(__instance))
                    hit.m_damage.Modify(0f);
            }
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindEnemy))]
        private static class BaseAI_FindEnemy_PacifyTames
        {
            private static bool Prefix(BaseAI __instance, ref Character __result)
            {
                if (!ShouldPacifyTame(__instance))
                    return true;

                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindRandomStaticTarget))]
        private static class BaseAI_FindRandomStaticTarget_PacifyTames
        {
            private static bool Prefix(BaseAI __instance, ref StaticTarget __result)
            {
                if (!ShouldPacifyTame(__instance))
                    return true;

                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindClosestStaticPriorityTarget))]
        private static class BaseAI_FindClosestStaticPriorityTarget_PacifyTames
        {
            private static bool Prefix(BaseAI __instance, ref StaticTarget __result)
            {
                if (!ShouldPacifyTame(__instance))
                    return true;

                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
        private static class MonsterAI_UpdateAI_PacifyTames
        {
            private static void Prefix(MonsterAI __instance)
            {
                if (ShouldPacifyTame(__instance))
                    PacifyMonster(__instance);
            }
        }

        [HarmonyPatch(typeof(AnimalAI), nameof(AnimalAI.UpdateAI))]
        private static class AnimalAI_UpdateAI_PacifyTames
        {
            private static void Prefix(AnimalAI __instance)
            {
                if (ShouldPacifyTame(__instance))
                    PacifyAnimal(__instance);
            }
        }
    }

    internal static class EnumerableExtensions
    {
        public static bool AnySafe<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null)
                return false;

            foreach (T item in source)
                if (predicate(item))
                    return true;

            return false;
        }
    }
}
