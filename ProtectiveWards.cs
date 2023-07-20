using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ServerSync;

namespace ProtectiveWards
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class ProtectiveWards : BaseUnityPlugin
    {
        const string pluginID = "shudnal.ProtectiveWards";
        const string pluginName = "Protective Wards";
        const string pluginVersion = "1.0.2";
        public static ManualLogSource logger;

        private Harmony _harmony;

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> configLocked;

        private static ConfigEntry<bool> disableFlash;
        private static ConfigEntry<bool> showAreaMarker;

        private static ConfigEntry<bool> setWardRange;
        private static ConfigEntry<float> wardRange;
        
        private static ConfigEntry<float> playerDamageDealtMultiplier;
        private static ConfigEntry<float> playerDamageTakenMultiplier;
        private static ConfigEntry<float> tamedDamageTakenMultiplier; 
        private static ConfigEntry<float> structureDamageTakenMultiplier;
        private static ConfigEntry<float> fallDamageTakenMultiplier;

        private static ConfigEntry<bool> boarsHensProtection;
        private static ConfigEntry<bool> wardRainProtection;
        private static ConfigEntry<bool> wardShipProtection;
        private static ConfigEntry<bool> wardPlantProtection;
        private static ConfigEntry<bool> fireplaceProtection;
        private static ConfigEntry<bool> sittingRaidProtection;

        internal static ProtectiveWards instance;

        private void Awake()
        {
            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), pluginID);

            instance = this;

            logger = Logger;

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);
        }

        private void OnDestroy()
        {
            Config.Save();
            _harmony?.UnpatchSelf();
        }

        private void ConfigInit()
        {
            config("General", "NexusID", 2450, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", defaultValue: true, "Enable the mod. Every option requires being in the zone of the active Ward.");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");

            disableFlash = config("Misc", "Disable flash", defaultValue: false, "Disable flash on hit [Not Synced with Server]", false);
            showAreaMarker = config("Misc", "Always show radius", defaultValue: false, "Always show ward radius. Hover the ward for changes to take effect. [Not Synced with Server]", false);

            playerDamageDealtMultiplier = config("Modifiers", "Creatures damage taken multiplier", defaultValue: 1.0f, "Basically it means damage dealt by any creatures (players and tames included) to any creatures (players and tames excluded)");
            playerDamageTakenMultiplier = config("Modifiers", "Player damage taken multiplier", defaultValue: 1.0f, "Damage taken by players from creatures");
            fallDamageTakenMultiplier = config("Modifiers", "Player fall damage taken multiplier", defaultValue: 1.0f, "Player fall damage taken");
            structureDamageTakenMultiplier = config("Modifiers", "Structure damage taken multiplier", defaultValue: 1.0f, "Structures (and ships) damage taken");
            tamedDamageTakenMultiplier = config("Modifiers", "Tamed damage taken multiplier", defaultValue: 1.0f, "Damage taken by tamed from creatures (players included)");

            setWardRange = config("Range", "Change Ward range", defaultValue: false, "Change ward range.");
            wardRange = config("Range", "Ward range", defaultValue: 10f, "Ward range. Toggle ward protection for changes to take effect");

            boarsHensProtection = config("Ward protects", "Boars and hens from damage", false, "Set whether an active Ward will protect nearby boars and hens from taken damage (players excluded)");
            wardRainProtection = config("Ward protects", "Structures from rain damage", false, "Set whether an active Ward will protect nearby structures from rain and water damage");
            wardShipProtection = config("Ward protects", "Ship from water damage", false, "Set whether an active Ward will protect nearby ships from water damage (waves and upsidedown)");
            wardPlantProtection = config("Ward protects", "Plants from any damage", false, "Set whether an active Ward will protect nearby plants from taking damage");
            fireplaceProtection = config("Ward protects", "Fireplace from step damage", false, "Set whether an active Ward will protect nearby fire sources from taking damage from stepping on them");
            sittingRaidProtection = config("Ward protects", "Players from raids when sitting on something near the fire (not floor)", false, "Set whether an active Ward will protect nearby players from raids when sitting next to an active fire"
                                                                                                                                           + "\nDo you want to go AFK in your base? Find a warm chair, bench, stool, throne whatever to sit on and go"
                                                                                                                                           + "\nIf the fire does not burn - you are vulnerable");
        }

        private void ConfigUpdate()
        {
            Config.Reload();
            ConfigInit();
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        public static bool InsideEnabledPlayersArea(Vector3 point)
        {
            return InsideEnabledPlayersArea(point, out PrivateArea _);
        }

        public static bool InsideEnabledPlayersArea(Vector3 point, out PrivateArea area)
        {
            area = null;
            foreach (PrivateArea allArea in PrivateArea.m_allAreas)
            {
                if (allArea.IsEnabled() && allArea.m_ownerFaction == Character.Faction.Players && allArea.IsInside(point, 0f))
                {
                    area = allArea;
                    return true;
                }
            }
            return false;
        }

        private static void ApplyRangeEffect(Component parent, EffectArea.Type includedTypes, float newRadius)
        {
            if (parent == null)
                return;

            EffectArea componentInChildren = parent.GetComponentInChildren<EffectArea>();
            if ((componentInChildren == null) || (componentInChildren.m_type != includedTypes))
                return;

            SphereCollider component = componentInChildren.GetComponent<SphereCollider>();
            if (component == null)
                return;

            component.radius = newRadius;
        }

        private static void SetWardRange(ref PrivateArea __instance)
        {
            float newRadius = Math.Max(wardRange.Value, 0);

            __instance.m_radius = newRadius;
            __instance.m_areaMarker.m_radius = newRadius;
            ApplyRangeEffect(__instance, EffectArea.Type.PlayerBase, newRadius);
        }

        private static void ModifyHitDamage(ref HitData hit, float value)
        {
            hit.m_damage.Modify(Math.Max(value, 0));
            return;
        }

        [HarmonyPatch(typeof(RandEventSystem), nameof(RandEventSystem.GetPossibleRandomEvents))]
        public static class RandEventSystem_GetPossibleRandomEvents_SittingRaidProtection
        {
            public static void Postfix(ref List<KeyValuePair<RandomEvent, Vector3>> __result)
            {
                if (!modEnabled.Value) return;

                if (!sittingRaidProtection.Value) return;

                if (!ZNet.instance.IsServer()) return;

                List<Vector3> protectedPositions = new List<Vector3>();

                Player.GetAllPlayers().ForEach(player =>
                {
                    if (InsideEnabledPlayersArea(player.transform.position) && player.IsSitting() && player.m_attached && player.m_seman.HaveStatusEffect(Player.s_statusEffectCampFire))
                        protectedPositions.Add(player.transform.position);
                });

                for (int i = __result.Count - 1; i >= 0; i--)
                {
                    foreach (Vector3 pos in protectedPositions)
                    {
                        if (Vector3.Distance(pos, __result[i].Value) < 1f)
                            __result.RemoveAt(i);
                    }
                }
            }
        }

        // RETURN TO THIS AFTER PUBLIC HILDIR UPDATE
        /*[HarmonyPatch(typeof(RandEventSystem), nameof(RandEventSystem.GetValidEventPoints))]
        public static class RandEventSystem_GetValidEventPoints_SittingRaidProtection
        {
            public static void Prefix(ref List<Player> characters, ref List<Player> __state)
            {
                if (!modEnabled.Value) return;

                if (!sittingRaidProtection.Value) return;

                if (!ZNet.instance.IsServer()) return;

                __state = characters;

                List<Player> nonprotectedPlayers = new List<Player>();

                foreach (Player character in characters)
                {
                    if (InsideEnabledPlayersArea(character.transform.position) && character.IsSitting() && character.m_attached && character.m_seman.HaveStatusEffect(Player.s_statusEffectCampFire))
                    {
                        logger.LogInfo($"{character.GetPlayerName()} is in raid protected state.");
                    }
                    else
                    {
                        nonprotectedPlayers.Add(character);
                    }
                }
                
                characters = nonprotectedPlayers;
            }
            public static void Postfix(ref List<Player> characters, List<Player> __state)
            {
                if (!modEnabled.Value) return;

                if (!sittingRaidProtection.Value) return;

                if (!ZNet.instance.IsServer()) return;

                if (__state.Count != characters.Count)
                {
                    characters = __state;
                }
            }
        }*/

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.HideMarker))]
        public static class PrivateArea_HideMarker_showAreaMarker
        {
            public static bool Prefix()
            {
                if (!modEnabled.Value) return true;

                return !showAreaMarker.Value;
            }
        }

        [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyFallDamage))] 
        public static class SEMan_ModifyFallDamage_ConfigUpdate
        {
            private static void Postfix(Character ___m_character, ZNetView ___m_nview, ref float damage)
            {
                if (!modEnabled.Value) return;

                if (___m_nview == null || !InsideEnabledPlayersArea(___m_character.transform.position))
                    return;

                damage *= Math.Max(fallDamageTakenMultiplier.Value, 0);
            }
            
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
        public static class PrivateArea_Interract_ConfigUpdate
        {
            private static void Prefix()
            {
                instance.ConfigUpdate();
            }

            private static void Postfix(ref PrivateArea __instance)
            {
                if (!modEnabled.Value) return;

                if (setWardRange.Value && __instance.m_radius != wardRange.Value)
                {
                    SetWardRange(ref __instance);
                }
            }

        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Awake))]
        public static class PrivateArea_Awake_SetWardRange
        {
            private static void Prefix(ref PrivateArea __instance)
            {
                if (!modEnabled.Value) return;

                if (setWardRange.Value)
                {
                    SetWardRange(ref __instance);
                }
            }

            private static void Postfix(ref PrivateArea __instance)
            {
                if (!modEnabled.Value) return;

                if (setWardRange.Value)
                {
                    SetWardRange(ref __instance);
                }

                if (showAreaMarker.Value)
                {
                    __instance.m_areaMarker.gameObject.SetActive(value: true);
                }
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.RPC_FlashShield))]
        public static class PrivateArea_RPC_FlashShield_StopFlashShield
        {
            private static bool Prefix(PrivateArea __instance)
            {
                if (!modEnabled.Value) return true;
                
                if (__instance.m_ownerFaction == Character.Faction.Players)
                    return !disableFlash.Value;

                return true;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        public static class Character_Damage_PlayerDamageMultiplier
        {
            private static void Prefix(Character __instance, ref HitData hit, ZNetView ___m_nview)
            {
                if (!modEnabled.Value) return;

                if (___m_nview == null || !InsideEnabledPlayersArea(__instance.transform.position, out PrivateArea area))
                    return;

                if (__instance.IsPlayer())
                {
                    ModifyHitDamage(ref hit, playerDamageTakenMultiplier.Value);
                }
                else if (__instance.IsTamed())
                {
                    ModifyHitDamage(ref hit, tamedDamageTakenMultiplier.Value);
                    if (boarsHensProtection.Value && (__instance.m_group == "boar" || __instance.m_group == "chicken"))
                    {
                        if (!(hit.GetAttacker() != null && hit.GetAttacker().IsPlayer()))
                        {
                            ModifyHitDamage(ref hit, 0f);
                            area.FlashShield(false);
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
                if (!modEnabled.Value) return;

                if (___m_nview == null || !InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                if (fireplaceProtection.Value && hit.m_hitType == HitData.HitType.Self && __instance.GetComponent<Fireplace>() != null)
                {
                    ModifyHitDamage(ref hit, 0);
                }
                else
                {
                    ModifyHitDamage(ref hit, structureDamageTakenMultiplier.Value);
                }
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        public static class Destructible_RPC_Damage_PlantProtection
        {
            private static void Prefix(Destructible __instance, ZNetView ___m_nview, bool ___m_destroyed, ref HitData hit)
            {

                if (!modEnabled.Value) return;

                if (!wardPlantProtection.Value) return;

                if (!___m_nview.IsValid() || !___m_nview.IsOwner() || ___m_destroyed)
                {
                    return;
                }

                if (__instance.GetDestructibleType() != DestructibleType.Default || __instance.m_health != 1)
                    return;

                if (__instance.GetComponent<Plant>() != null && InsideEnabledPlayersArea(__instance.transform.position, out PrivateArea area))
                {
                    ModifyHitDamage(ref hit, 0f);
                    area.FlashShield(false);
                }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
        public static class WearNTear_UpdateWear_RainProtection
        {
            private static void Prefix(WearNTear __instance, ZNetView ___m_nview, ref bool ___m_noRoofWear, ref bool __state)
            {
                if (!modEnabled.Value) return;

                if (!wardRainProtection.Value) return;

                if (___m_nview == null || !___m_nview.IsValid() || !InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                __state = ___m_noRoofWear;

                ___m_noRoofWear = false;
            }
            
            private static void Postfix(ref bool ___m_noRoofWear, bool __state)
            {              
                if (!modEnabled.Value) return;

                if (!wardRainProtection.Value) return;

                if (__state != true) return;

                ___m_noRoofWear = __state;
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateUpsideDmg))]
        public static class Ship_UpdateUpsideDmg_PreventShipDamage
        {
            private static bool Prefix(Ship __instance)
            {
                if (!modEnabled.Value) return true;
                if (!wardShipProtection.Value) return true;

                return !InsideEnabledPlayersArea(__instance.transform.position);
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateWaterForce))]
        public static class Ship_UpdateWaterForce_PreventShipDamage
        {
            private static void Prefix(Ship __instance, ref float ___m_waterImpactDamage, ref float __state)
            {
                if (!modEnabled.Value) return;
                if (!wardShipProtection.Value) return;

                if (!InsideEnabledPlayersArea(__instance.transform.position)) return;

                __state = ___m_waterImpactDamage;

                ___m_waterImpactDamage = 0f;
            }

            private static void Postfix(Ship __instance, ref float ___m_waterImpactDamage, float __state)
            {
                if (!modEnabled.Value) return;
                if (!wardShipProtection.Value) return;

                if (!InsideEnabledPlayersArea(__instance.transform.position)) return;

                ___m_waterImpactDamage = __state;
            }
        }

    }
}
