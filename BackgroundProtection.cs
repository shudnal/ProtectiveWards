using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class BackgroundProtection
    {
        private static readonly Dictionary<PrivateArea, CachedBool> s_qualifiedBaseCache = new();
        private const float QualifiedBaseCacheSeconds = 10f;

        private struct CachedBool
        {
            public float Time;
            public bool Value;
        }

        internal static bool IsBackgroundProtectionActiveAt(Vector3 point, out PrivateArea ward)
        {
            ward = null;

            if (!TryFindBackgroundWard(point, point, out ward))
                return false;

            if (!IsQualifiedProtectedBase(ward))
                return false;

            return !HasEffectiveAccessPresence(ward, point);
        }

        internal static bool TryFindBackgroundWard(Vector3 sourcePoint, Vector3 targetPoint, out PrivateArea ward)
        {
            ward = null;

            if (!TryResolveWardCheckPoint(sourcePoint, out sourcePoint) || !TryResolveWardCheckPoint(targetPoint, out targetPoint))
                return false;

            WardConnectedAccessMode mode = wardBackgroundConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardBackgroundConnectedAccessMode.Value;

            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (!IsActivePlayerWard(area) || !IsInsideWardXZ(area, sourcePoint))
                    continue;

                if (!ConnectedAccessAreas(area, mode).AnySafe(candidate => IsInsideWardXZ(candidate, targetPoint)))
                    continue;

                ward = area;
                return true;
            }

            return false;
        }

        internal static bool HasEffectiveAccessPresence(PrivateArea ward, Vector3 point)
        {
            if (ward == null)
                return false;

            WardConnectedAccessMode mode = wardBackgroundConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardBackgroundConnectedAccessMode.Value;
            float radius = Mathf.Max(wardBackgroundPresenceRadius.Value, 0f);

            if (!TryResolveWardCheckPoint(point, out Vector3 resolvedPoint))
                return false;

            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null)
                    continue;

                if (!HasAccessToWardOrConnectedWard(ward, player, mode))
                    continue;

                if (!TryResolveWardCheckPoint(player.transform.position, out Vector3 resolvedPlayerPoint))
                    continue;

                switch (wardBackgroundPresenceMode.Value)
                {
                    case WardBackgroundPresenceMode.PermittedOnline:
                        return true;
                    case WardBackgroundPresenceMode.PermittedInsideConnectedArea:
                        if (ConnectedAccessAreas(ward, mode).AnySafe(area => IsInsideWardXZ(area, resolvedPlayerPoint)))
                            return true;
                        break;
                    case WardBackgroundPresenceMode.PermittedNearProtectedArea:
                    default:
                        if (Utils.DistanceXZ(resolvedPlayerPoint, resolvedPoint) <= radius)
                            return true;
                        break;
                }
            }

            return false;
        }

        internal static bool IsQualifiedProtectedBase(PrivateArea ward)
        {
            if (ward == null)
                return false;

            int minimumPieces = Math.Max(wardBackgroundProtectedBaseMinPieces.Value, 0);
            if (minimumPieces == 0)
                return true;

            if (s_qualifiedBaseCache.TryGetValue(ward, out CachedBool cached) && Time.time - cached.Time < QualifiedBaseCacheSeconds)
                return cached.Value;

            bool result = CountPlayerBuiltPiecesInNetwork(ward, minimumPieces) >= minimumPieces;
            s_qualifiedBaseCache[ward] = new CachedBool { Time = Time.time, Value = result };
            return result;
        }

        private static int CountPlayerBuiltPiecesInNetwork(PrivateArea ward, int stopAt)
        {
            WardConnectedAccessMode mode = wardBackgroundConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardBackgroundConnectedAccessMode.Value;
            HashSet<Piece> pieces = new();
            List<Piece> buffer = new();

            foreach (PrivateArea area in ConnectedAccessAreas(ward, mode))
            {
                buffer.Clear();
                Piece.GetAllPiecesInRadius(area.transform.position, area.m_radius, buffer);
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

        internal static bool ShouldSuppressWearNTearDamage(WearNTear wearNTear, HitData hit)
        {
            if (wearNTear == null || hit == null)
                return false;

            Piece piece = wearNTear.m_piece ?? wearNTear.GetComponent<Piece>();
            bool isPlayerBuiltPiece = piece != null && piece.IsPlacedByPlayer();
            bool isShip = wearNTear.GetComponent<Ship>() != null || wearNTear.GetComponentInParent<Ship>() != null;
            bool isCart = wearNTear.GetComponent<Vagon>() != null || wearNTear.GetComponentInParent<Vagon>() != null;

            if (!isPlayerBuiltPiece && !isShip && !isCart)
                return false;

            if (!TryFindBackgroundWard(wearNTear.transform.position, wearNTear.transform.position, out PrivateArea ward))
                return false;

            if (!IsQualifiedProtectedBase(ward))
                return false;

            Character attacker = hit.GetAttacker();

            if (wardBackgroundStructureProtection.Value == WardBackgroundStructureProtectionMode.BlockNonPermittedPlayerDamage
                && isPlayerBuiltPiece
                && attacker != null
                && attacker.IsPlayer()
                && !HasAccessToWardOrConnectedWard(ward, attacker as Player, wardBackgroundConnectedAccessMode.Value))
                return true;

            if (HasEffectiveAccessPresence(ward, wearNTear.transform.position))
                return false;

            if (wardBackgroundProtectBoats.Value && isShip)
                return true;

            if (wardBackgroundProtectCarts.Value && isCart)
                return true;

            if (isPlayerBuiltPiece)
            {
                if (wardBackgroundStructureProtection.Value == WardBackgroundStructureProtectionMode.BlockAllDamageWhenNoPermittedNearby)
                    return true;

                if (wardBackgroundProtectFire.Value && IsFireDamage(hit))
                    return true;
            }

            return false;
        }

        internal static bool ShouldSuppressTameDamageToStructure(WearNTear wearNTear, HitData hit)
        {
            if (!wardBackgroundTamesPreventDamageToStructures.Value)
                return false;

            if (wearNTear == null || hit == null || !hit.HaveAttacker())
                return false;

            Piece piece = wearNTear.m_piece ?? wearNTear.GetComponent<Piece>();
            if (piece == null || !piece.IsPlacedByPlayer())
                return false;

            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker.IsPlayer() || !attacker.IsTamed())
                return false;

            if (!TryFindBackgroundWard(attacker.transform.position, wearNTear.transform.position, out PrivateArea ward))
                return false;

            if (!IsQualifiedProtectedBase(ward))
                return false;

            return !HasEffectiveAccessPresence(ward, attacker.transform.position)
                   && !HasEffectiveAccessPresence(ward, wearNTear.transform.position);
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
            if (!wardBackgroundPreventBuildingAndDemolishing.Value)
                return false;

            if (player == null)
                return false;

            long playerID = player.GetPlayerID();
            if (HasWardAdminAccess(playerID))
                return false;

            if (!TryFindBackgroundWard(point, point, out PrivateArea ward))
                return false;

            if (!IsQualifiedProtectedBase(ward))
                return false;

            if (HasAccessToWardOrConnectedWard(ward, player, wardBackgroundConnectedAccessMode.Value))
                return false;

            return !HasEffectiveAccessPresence(ward, point);
        }

        private static bool IsFireDamage(HitData hit) => hit != null && (hit.m_damage.m_fire > 0f || hit.m_hitType == HitData.HitType.Burning);

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
                if (ShouldSuppressWearNTearDamage(__instance, hit) || ShouldSuppressTameDamageToStructure(__instance, hit))
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
