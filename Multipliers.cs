using HarmonyLib;
using System;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal class Multipliers
    {
        private static void ModifyHitDamage(ref HitData hit, float value)
        {
            hit.m_damage.Modify(Math.Max(value, 0));
        }

        [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyFallDamage))]
        public static class SEMan_ModifyFallDamage_FallDamageMultiplier
        {
            private static void Postfix(Character ___m_character, ZNetView ___m_nview, ref float damage)
            {
                if (!modEnabled.Value)
                    return;

                if (___m_nview == null || !InsideEnabledPlayersArea(___m_character.transform.position))
                    return;

                damage *= Math.Max(fallDamageTakenMultiplier.Value, 0);
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        public static class Character_Damage_DamageMultipliers
        {
            private static void Prefix(Character __instance, ref HitData hit, ZNetView ___m_nview)
            {
                if (!modEnabled.Value)
                    return;

                if (___m_nview == null || !InsideEnabledPlayersArea(__instance.transform.position, out PrivateArea ward))
                    return;

                if (hit.HaveAttacker() && hit.GetAttacker().IsBoss())
                    return;

                if (__instance.IsPlayer())
                {
                    ModifyHitDamage(ref hit, playerDamageTakenMultiplier.Value);
                }
                else if (__instance.IsTamed())
                {
                    ModifyHitDamage(ref hit, tamedDamageTakenMultiplier.Value);
                    if (boarsHensProtection.Value && _boarsHensProtectionGroupList.Contains(__instance.m_group.ToLower()))
                    {
                        if (!(hit.HaveAttacker() && hit.GetAttacker().IsPlayer()))
                        {
                            if (hit.GetTotalDamage() != hit.m_damage.m_fire)
                                ward.FlashShield(false);

                            ModifyHitDamage(ref hit, 0f);
                        }
                    }
                }
                else
                {
                    ModifyHitDamage(ref hit, playerDamageDealtMultiplier.Value);
                }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
        public static class WearNTear_Damage_DamageTakenMultiplier
        {
            private static void Prefix(WearNTear __instance, ref HitData hit, ZNetView ___m_nview)
            {
                if (!modEnabled.Value)
                    return;

                if (___m_nview == null || !InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                if (fireplaceProtection.Value && hit.m_hitType == HitData.HitType.Self && __instance.GetComponent<Fireplace>() != null)
                {
                    ModifyHitDamage(ref hit, 0f);
                }
                else if (wardShipProtection.Value == ShipDamageType.AnyButPlayerDamage && hit.m_hitType != HitData.HitType.PlayerHit && __instance.GetComponent<Ship>() != null)
                {
                    ModifyHitDamage(ref hit, 0f);
                }
                else if (wardShipProtection.Value == ShipDamageType.AnyDamage && __instance.GetComponent<Ship>() != null)
                {
                    ModifyHitDamage(ref hit, 0f);
                }
                else if (__instance.GetComponent<Piece>() != null)
                {
                    if (hit.HaveAttacker() && hit.GetAttacker().IsBoss())
                        return;

                    ModifyHitDamage(ref hit, structureDamageTakenMultiplier.Value);
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdateFood))]
        public static class Player_UpdateFood_FoodDrainMultiplier
        {
            private static void Prefix(Player __instance, float dt, bool forceUpdate)
            {
                if (!modEnabled.Value)
                    return;

                if (foodDrainMultiplier.Value == 1.0f)
                    return;

                if (__instance == null)
                    return;

                if (!(dt + __instance.m_foodUpdateTimer >= 1f || forceUpdate))
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                foreach (Player.Food food in __instance.m_foods)
                    food.m_time += 1f - Math.Max(0f, foodDrainMultiplier.Value);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
        public static class Player_UseStamina_StaminaDrainMultiplier
        {
            private static void Prefix(Player __instance, ref float v)
            {
                if (!modEnabled.Value)
                    return;

                if (staminaDrainMultiplier.Value == 1.0f)
                    return;

                if (__instance == null)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                v *= Math.Max(0f, staminaDrainMultiplier.Value);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacement))]
        public static class Player_UseStamina_HammerDurabilityDrainMultiplier
        {
            private static void Prefix(Player __instance, ref float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (hammerDurabilityDrainMultiplier.Value == 1.0f)
                    return;

                if (__instance == null)
                    return;

                if (!__instance.InPlaceMode())
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                ItemDrop.ItemData rightItem = __instance.GetRightItem();

                __state = rightItem.m_shared.m_useDurabilityDrain;
                rightItem.m_shared.m_useDurabilityDrain *= Math.Max(0f, hammerDurabilityDrainMultiplier.Value);
            }

            private static void Postfix(Player __instance, float __state)
            {
                if (__state == 0f)
                    return;

                ItemDrop.ItemData rightItem = __instance.GetRightItem();

                rightItem.m_shared.m_useDurabilityDrain = __state;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Repair))]
        public static class Player_Repair_HammerDurabilityDrainMultiplier
        {
            private static void Prefix(Player __instance, ItemDrop.ItemData toolItem, ref float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (hammerDurabilityDrainMultiplier.Value == 1.0f)
                    return;

                if (__instance == null)
                    return;

                if (!__instance.InPlaceMode())
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                __state = toolItem.m_shared.m_useDurabilityDrain;
                toolItem.m_shared.m_useDurabilityDrain *= Math.Max(0f, hammerDurabilityDrainMultiplier.Value);
            }

            private static void Postfix(Player __instance, ItemDrop.ItemData toolItem, float __state)
            {
                if (__state == 0f)
                    return;

                toolItem.m_shared.m_useDurabilityDrain = __state;
            }
        }

        [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
        static class Skills_OnDeath_SkillDrainMultiplier
        {
            static void Prefix(ref float factor, Player ___m_player)
            {
                if (!modEnabled.Value)
                    return;

                if (___m_player == null)
                    return;

                if (!InsideEnabledPlayersArea(___m_player.transform.position))
                    return;

                factor *= Math.Max(0f, skillsDrainMultiplier.Value);
            }
        }

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetTimeSinceLastUpdate))]
        static class Fireplace_GetTimeSinceLastUpdate_FireplaceDrainMultiplier
        {
            private static void Postfix(Fireplace __instance, ref double __result)
            {
                if (!modEnabled.Value)
                    return;

                if (fireplaceDrainMultiplier.Value == 1.0f)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                __result *= (double)Math.Max(0f, fireplaceDrainMultiplier.Value);
            }
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.GetDeltaTime))]
        static class Smelter_GetDeltaTime_FireplaceDrainMultiplier_SmeltingSpeedMultiplier
        {
            private static void Postfix(Smelter __instance, ref double __result)
            {
                if (!modEnabled.Value)
                    return;

                float multiplier = (__instance.m_name == "$piece_bathtub") ? fireplaceDrainMultiplier.Value : smeltingSpeedMultiplier.Value;

                if (multiplier == 1.0f)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                __result *= (double)Math.Max(0f, multiplier);
            }
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.GetDeltaTime))]
        static class CookingStation_GetDeltaTime_CookingSpeedMultiplier
        {
            private static void Postfix(Smelter __instance, ref float __result)
            {
                if (!modEnabled.Value)
                    return;

                if (cookingSpeedMultiplier.Value == 1.0f)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                __result *= Math.Max(0f, cookingSpeedMultiplier.Value);
            }
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.UpdateFuel))]
        static class CookingStation_UpdateFuel_FireplaceDrainMultiplier
        {
            private static void Prefix(Smelter __instance, ref float dt, ref float __state)
            {
                if (!modEnabled.Value)
                    return;

                if ((fireplaceDrainMultiplier.Value == 1.0f) && (cookingSpeedMultiplier.Value == 1.0f))
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                __state = dt;

                dt *= Math.Max(0f, fireplaceDrainMultiplier.Value);
                if (cookingSpeedMultiplier.Value > 0f)
                    dt /= cookingSpeedMultiplier.Value;
            }

            private static void Postfix(Smelter __instance, ref float dt, float __state)
            {
                if (__state == 0f)
                    return;

                dt = __state;
            }
        }

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetFermentationTime))]
        static class Fermenter_GetFermentationTime_FermentingSpeedMultiplier
        {
            private static void Postfix(Fermenter __instance, ref double __result)
            {
                if (!modEnabled.Value)
                    return;

                if (fermentingSpeedMultiplier.Value == 1.0f)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                __result *= Math.Max(0f, fermentingSpeedMultiplier.Value);
            }
        }

        [HarmonyPatch(typeof(SapCollector), nameof(SapCollector.GetTimeSinceLastUpdate))]
        static class SapCollector_GetTimeSinceLastUpdate_SapCollectingSpeedMultiplier
        {
            private static void Postfix(SapCollector __instance, ref float __result)
            {
                if (!modEnabled.Value)
                    return;

                if (sapCollectingSpeedMultiplier.Value == 1.0f)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                __result *= Math.Max(0f, sapCollectingSpeedMultiplier.Value);
            }
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.IsCoolingDown))]
        static class Turret_IsCoolingDown_turretFireRateMultiplier
        {
            private static void Prefix(Turret __instance, ref float ___m_attackCooldown, ref float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (turretFireRateMultiplier.Value == 1.0f)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                __state = ___m_attackCooldown;

                ___m_attackCooldown *= Math.Max(0.0f, turretFireRateMultiplier.Value);
            }

            private static void Postfix(ref float ___m_attackCooldown, float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (__state > 0f)
                    ___m_attackCooldown = __state;
            }
        }

    }
}
