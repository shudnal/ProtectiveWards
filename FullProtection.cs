using HarmonyLib;
using System;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal class FullProtection
    {
        [HarmonyPatch(typeof(Character), nameof(Character.AddFireDamage))]
        public static class Character_AddFireDamage_IndirectFireDamageProtection
        {
            private static void Prefix(Character __instance, ref float damage)
            {
                if (!modEnabled.Value)
                    return;

                if (boarsHensProtection.Value && __instance.IsTamed() && _boarsHensProtectionGroupList.Contains(__instance.m_group.ToLower()) && InsideEnabledPlayersArea(__instance.transform.position))
                    damage = 0f;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.UpdateSmoke))]
        public static class Character_UpdateSmoke_IndirectSmokeDamageProtection
        {
            private static void Prefix(Character __instance, ref float dt)
            {
                if (!modEnabled.Value)
                    return;

                if (boarsHensProtection.Value && __instance.IsTamed() && _boarsHensProtectionGroupList.Contains(__instance.m_group.ToLower()) && InsideEnabledPlayersArea(__instance.transform.position))
                    dt = 0f;
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
        public static class WearNTear_UpdateWear_RainProtection
        {
            private static void Prefix(WearNTear __instance, ref bool ___m_noRoofWear, ref bool __state)
            {
                if (!modEnabled.Value)
                    return;

                if (!wardRainProtection.Value)
                    return;

                if (!___m_noRoofWear)
                    return;

                if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                __state = ___m_noRoofWear;

                ___m_noRoofWear = false;
            }

            private static void Postfix(ref bool ___m_noRoofWear, bool __state)
            {
                if (!modEnabled.Value)
                    return;

                if (!wardRainProtection.Value)
                    return;

                if (__state != true)
                    return;

                ___m_noRoofWear = __state;
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateUpsideDmg))]
        public static class Ship_UpdateUpsideDmg_PreventShipDamage
        {
            private static bool Prefix(Ship __instance)
            {
                if (!modEnabled.Value)
                    return true;

                if (wardShipProtection.Value == ShipDamageType.Off)
                    return true;

                return !InsideEnabledPlayersArea(__instance.transform.position);
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateWaterForce))]
        public static class Ship_UpdateWaterForce_PreventShipDamage
        {
            private static void Prefix(Ship __instance, ref float ___m_waterImpactDamage, ref float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (wardShipProtection.Value == ShipDamageType.Off)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                __state = ___m_waterImpactDamage;

                ___m_waterImpactDamage = 0f;
            }

            private static void Postfix(Ship __instance, ref float ___m_waterImpactDamage, float __state)
            {
                if (__state == 0f)
                    return;

                ___m_waterImpactDamage = __state;
            }
        }

        [HarmonyPatch(typeof(Trap), nameof(Trap.OnTriggerEnter))]
        static class Trap_OnTriggerEnter_TrapProtection
        {
            private static bool Prefix(Trap __instance, Collider collider)
            {
                if (!modEnabled.Value)
                    return true;

                if (!wardTrapProtection.Value)
                    return true;

                if (collider.GetComponentInParent<Player>() == null)
                    return true;

                return !InsideEnabledPlayersArea(__instance.transform.position, checkCache: true);
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        public static class Destructible_Damage_PlantProtection
        {
            private static void ModifyHitDamage(HitData hit, float value)
            {
                hit.m_damage.Modify(Math.Max(value, 0));
            }

            private static void Prefix(Destructible __instance, bool ___m_destroyed, HitData hit)
            {
                if (!modEnabled.Value)
                    return;

                if (!wardPlantProtection.Value)
                    return;

                if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || __instance.m_destroyed)
                    return;

                if (__instance.GetDestructibleType() != DestructibleType.Default || __instance.m_health != 1)
                    return;

                if (hit == null || !hit.HaveAttacker())
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, out PrivateArea ward, checkCache: true))
                    return;

                if (__instance.GetComponent<Plant>() != null)
                {
                    ModifyHitDamage(hit, 0f);
                    ward.FlashShield(false);
                }
                else if (__instance.TryGetComponent(out Pickable pickable))
                {
                    ItemDrop.ItemData.SharedData m_shared = pickable.m_itemPrefab?.GetComponent<ItemDrop>()?.m_itemData.m_shared;

                    if (m_shared != null && _wardPlantProtectionList.Contains(m_shared.m_name.ToLower()))
                    {
                        ModifyHitDamage(hit, 0f);
                        ward.FlashShield(false);
                    }
                }
            }
        }
    }
}
