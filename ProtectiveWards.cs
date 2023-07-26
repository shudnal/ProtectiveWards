﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
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
        const string pluginVersion = "1.1.0";
        public static ManualLogSource logger;

        private Harmony _harmony;

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> configLocked;
        private static ConfigEntry<int> refreshingTime;

        private static ConfigEntry<bool> disableFlash;
        private static ConfigEntry<bool> showAreaMarker;

        private static ConfigEntry<bool> offeringActiveRepair;
        private static ConfigEntry<bool> offeringAugmenting;
        private static ConfigEntry<bool> offeringFood;
        private static ConfigEntry<bool> offeringMead;
        private static ConfigEntry<bool> offeringThundertone;
        private static ConfigEntry<bool> offeringTrophy;
        private static ConfigEntry<bool> wardPassiveRepair;

        private static ConfigEntry<bool> setWardRange;
        private static ConfigEntry<float> wardRange;

        private static ConfigEntry<float> playerDamageDealtMultiplier;
        private static ConfigEntry<float> playerDamageTakenMultiplier;
        private static ConfigEntry<float> tamedDamageTakenMultiplier;
        private static ConfigEntry<float> structureDamageTakenMultiplier;
        private static ConfigEntry<float> fallDamageTakenMultiplier;
        private static ConfigEntry<float> turretFireRateMultiplier;

        private static ConfigEntry<float> foodDrainMultiplier;
        private static ConfigEntry<float> staminaDrainMultiplier;
        private static ConfigEntry<float> skillsDrainMultiplier;
        private static ConfigEntry<float> fireplaceDrainMultiplier;
        private static ConfigEntry<float> hammerDurabilityDrainMultiplier;

        private static ConfigEntry<float> smeltingSpeedMultiplier;
        private static ConfigEntry<float> cookingSpeedMultiplier;
        private static ConfigEntry<float> fermentingSpeedMultiplier;
        private static ConfigEntry<float> sapCollectingSpeedMultiplier;

        private static ConfigEntry<bool> boarsHensProtection;
        private static ConfigEntry<bool> wardRainProtection;
        private static ConfigEntry<ShipDamageType> wardShipProtection;
        private static ConfigEntry<bool> wardPlantProtection;
        private static ConfigEntry<bool> fireplaceProtection;
        private static ConfigEntry<bool> sittingRaidProtection;
        private static ConfigEntry<bool> wardTrapProtection;

        internal static ProtectiveWards instance;
        internal static long startTimeCached;
        internal static Dictionary<Vector3, PrivateArea> areaCache = new Dictionary<Vector3, PrivateArea>();
        internal static Dictionary<PrivateArea, int> wardIsRepairing = new Dictionary<PrivateArea, int>();
        internal static Dictionary<PrivateArea, DateTime> wardIsHealing = new Dictionary<PrivateArea, DateTime>();

        internal static GameObject lightningAOE;
        internal static EffectList preLightning;
        internal static List<Turret.TrophyTarget> trophyTargets;

        public enum ShipDamageType
        {
            Off,
            WaterDamage,
            AnyButPlayerDamage,
            AnyDamage
        }

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
            refreshingTime = config("Misc", "Ward protected status check time", defaultValue: 30, "Set how many seconds the \"inside the protected zone\" status is reset. [Not Synced with Server]" +
                                                                                                    "\nSetting more seconds can be helpful for fps for base with many objects and static untoggled wards. " +
                                                                                                    "\nDoesn't affect moving objects.", false);

            playerDamageDealtMultiplier = config("Modifiers damage", "Creatures damage taken multiplier", defaultValue: 1.0f, "Basically it means damage dealt by any creatures (players and tames included) to any creatures (players and tames excluded)");
            playerDamageTakenMultiplier = config("Modifiers damage", "Player damage taken multiplier", defaultValue: 1.0f, "Damage taken by players from creatures");
            fallDamageTakenMultiplier = config("Modifiers damage", "Player fall damage taken multiplier", defaultValue: 1.0f, "Player fall damage taken");
            structureDamageTakenMultiplier = config("Modifiers damage", "Structure damage taken multiplier", defaultValue: 1.0f, "Structures (and ships) damage taken");
            tamedDamageTakenMultiplier = config("Modifiers damage", "Tamed damage taken multiplier", defaultValue: 1.0f, "Damage taken by tamed from creatures (players included)");
            turretFireRateMultiplier = config("Modifiers damage", "Turret fire rate multiplier", defaultValue: 1.0f, "Basically time between shots");

            foodDrainMultiplier = config("Modifiers drain", "Food drain multiplier", defaultValue: 1.0f, "Speed of food drain, more - faster, less - slower");
            staminaDrainMultiplier = config("Modifiers drain", "Stamina drain multiplier", defaultValue: 1.0f, "Multiplier of stamina needed for actions");
            skillsDrainMultiplier = config("Modifiers drain", "Skills drain on death multiplier", defaultValue: 1.0f, "Multiplier of how much skill points you lose on death. Accumulated scale will be lost anyway");
            fireplaceDrainMultiplier = config("Modifiers drain", "Fireplace fuel drain multiplier", defaultValue: 1.0f, "Speed of fuel spent for fireplaces (including bathtub, torches and braziers)");
            hammerDurabilityDrainMultiplier = config("Modifiers drain", "Hammer durability drain multiplier", defaultValue: 1.0f, "Multiplier of how much hammer's durability will be spent on building and repairing");

            smeltingSpeedMultiplier = config("Modifiers speed", "Smelting speed multiplier", defaultValue: 1.0f, "Speed of smelting for all smelting stations (excluding bathtub)");
            cookingSpeedMultiplier = config("Modifiers speed", "Cooking speed multiplier", defaultValue: 1.0f, "Speed of cooking for all cooking stations (that also means faster burn)");
            fermentingSpeedMultiplier = config("Modifiers speed", "Fermenting speed multiplier", defaultValue: 1.0f, "Speed of fermenting");
            sapCollectingSpeedMultiplier = config("Modifiers speed", "Sap collecting speed multiplier", defaultValue: 1.0f, "Speed of sap collecting");

            offeringActiveRepair = config("Offerings", "1 - Repair all pieces by surtling core offering", defaultValue: false, "Offer surtling core to ward to instantly repair all pieces in all connected areas" +
                                                                                                                               "\nCore will NOT be wasted if there is no piece to repair");
            offeringAugmenting = config("Offerings", "2 - Augment all pieces by black core offering", defaultValue: false, "Offer black core to ward to double the health of every structural piece in all connected areas" +
                                                                                                                            "\nCore will NOT be wasted if there is no piece to repair");
            offeringFood = config("Offerings", "3 - Heal all allies for 3 min by food offering", defaultValue: false, "Offer food to ward to activate healing for 3 minutes in all connected areas. Better food means better heal."+
                                                                                                                       "\nYou can offer one food to one ward until 3 minutes are gone. But nothing stops you from offering food to several wards.");
            offeringMead = config("Offerings", "4 - Share mead effect to all players by mead offering", defaultValue: false, "Offer mead to ward to share the effect to all players in all connected areas. " +
                                                                                                                              "\nMead will NOT be wasted if no one can have effect.");
            offeringThundertone = config("Offerings", "5 - Call the wrath of the Thor upon your enemies by thunderstone offering", defaultValue: false, "Offer thunderstone to ward to call the Thor's wrath upon your enemies in all connected areas" +
                                                                                                                                                        "\nThunderstone will be wasted even if no one gets hurt");
            offeringTrophy = config("Offerings", "6 - Kill all enemies of the same type by trophy offering", defaultValue: false, "Offer trophy to ward to kill all enemies with type of the offered trophy in all connected areas" +
                                                                                                                                   "\nTrophy will NOT be wasted if no one gets hurt");

            wardPassiveRepair = config("Passive", "Activatable passive repair", defaultValue: false, "Interact with a ward to start passive repair process of all pieces in all connected areas" +
                                                                                                      "\nWard will repair one piece every 10 seconds until all pieces are healthy. Then the process will stop.");

            setWardRange = config("Range", "Change Ward range", defaultValue: false, "Change ward range.");
            wardRange = config("Range", "Ward range", defaultValue: 10f, "Ward range. Toggle ward protection for changes to take effect");

            boarsHensProtection = config("Ward protects", "Boars and hens from damage", false, "Set whether an active Ward will protect nearby boars and hens from taken damage (players excluded)");
            wardRainProtection = config("Ward protects", "Structures from rain damage", false, "Set whether an active Ward will protect nearby structures from rain and water damage");
            wardShipProtection = config("Ward protects", "Ship from damage", ShipDamageType.WaterDamage, "Set whether an active Ward will protect nearby ships from damage (waves and upsidedown for water damage option or any structural damage)");
            wardPlantProtection = config("Ward protects", "Plants from any damage", false, "Set whether an active Ward will protect nearby plants from taking damage");
            fireplaceProtection = config("Ward protects", "Fireplace from step damage", false, "Set whether an active Ward will protect nearby fire sources from taking damage from stepping on them");
            wardTrapProtection = config("Ward protects", "Players from their traps", false, "Set whether an active Ward will protect players from stepping on traps");
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

        internal static void UpdateCache()
        {
            DateTime time = ZNet.instance.GetTime();

            if (startTimeCached == 0)
                startTimeCached = time.Ticks;

            if ((time - new DateTime(startTimeCached)).TotalSeconds > refreshingTime.Value)
            {
                areaCache.Clear();
                startTimeCached = time.Ticks;
            }
        }

        public static bool InsideEnabledPlayersArea(Vector3 point, out PrivateArea area)
        {
            UpdateCache();

            if (areaCache.TryGetValue(point, out area))
            {
                return area != null;
            }

            foreach (PrivateArea allArea in PrivateArea.m_allAreas)
            {
                if (allArea.IsEnabled() && allArea.m_ownerFaction == Character.Faction.Players && allArea.IsInside(point, 0f))
                {
                    area = allArea;
                    areaCache.Add(point, area);
                    return true;
                }
            }

            areaCache.Add(point, area);
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

        public static IEnumerator PassiveRepairEffect(PrivateArea ward, Player initiator)
        {
            while (true)
            {
                if (ward == null)
                    yield break;

                if (!ZNetScene.instance)
                    yield break;

                List<Piece> pieces = new List<Piece>();

                ward.GetConnectedAreas().ForEach(area => Piece.GetAllPiecesInRadius(area.transform.position, area.m_radius, pieces));

                List<Piece> piecesToRepair = pieces.Distinct().ToList().Where(piece => piece.IsPlacedByPlayer() && piece.TryGetComponent<WearNTear>(out WearNTear WNT) && WNT.GetHealthPercentage() < 1.0f).ToList();

                if (piecesToRepair.Count == 0)
                {
                    logger.LogInfo($"Passive repairing stopped");
                    wardIsRepairing.Remove(ward);

                    if (initiator != null)
                    {
                        string str = Localization.instance.Localize("$msg_doesnotneedrepair");
                        initiator.Message(MessageHud.MessageType.TopLeft, char.ToUpper(str[0]) + str.Substring(1));
                    }

                    yield break;
                }

                wardIsRepairing[ward] = piecesToRepair.Count;
                foreach (Piece piece in piecesToRepair)
                {
                    if (piece.TryGetComponent<WearNTear>(out WearNTear WNT) && WNT.Repair())
                    {
                        piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation);

                        wardIsRepairing.TryGetValue(ward, out int toRepair);
                        wardIsRepairing[ward] = Math.Max(toRepair - 1, 0);

                        if (initiator != null)
                            initiator.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$piece_repair"));

                        break;
                    }
                }

                yield return new WaitForSecondsRealtime(10);
            }
        }

        public static IEnumerator PassiveHealingEffect(PrivateArea ward, float amount, int seconds)
        {
            while (true)
            {
                if (ward == null)
                    yield break;

                if (!wardIsHealing.TryGetValue(ward, out DateTime endDate))
                    yield break;

                if (endDate < DateTime.Now)
                {
                    wardIsHealing.Remove(ward);
                    logger.LogInfo($"Passive healing stopped");
                    yield break;
                }

                List<Player> players = new List<Player>();
                ward.GetConnectedAreas().ForEach(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

                foreach (Player player in players.Distinct().ToList())
                {
                    player.Heal(amount * seconds);
                }

                List<Character> characters = new List<Character>();
                ward.GetConnectedAreas().ForEach(area => Character.GetCharactersInRange(area.transform.position, area.m_radius, characters));

                foreach (Character character in characters.Distinct().ToList())
                {
                    if (character.IsTamed())
                        character.Heal(amount * seconds);
                }

                yield return new WaitForSecondsRealtime(seconds);
            }
        }

        public static IEnumerator LightningStrikeEffect(PrivateArea ward)
        {
            if (ward == null)
                yield break;

            List<Player> players = new List<Player>();
            ward.GetConnectedAreas().ForEach(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            foreach (Player player in players.Distinct().ToList())
            {
                preLightning.Create(player.transform.position, player.transform.rotation);
            }

            yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 7f));

            List<Character> characters = new List<Character>();
            ward.GetConnectedAreas().ForEach(area => Character.GetCharactersInRange(area.transform.position, area.m_radius, characters));

            foreach (Character character in characters.Distinct().ToList())
            {
                if (character.IsMonsterFaction(Time.time))
                {
                    UnityEngine.Object.Instantiate(lightningAOE, character.transform.position, character.transform.rotation);
                }
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.OnDestroy))]
        public static class PrivateArea_OnDestroy_ClearStatus
        {
            public static void Prefix(PrivateArea __instance)
            {
                if (!modEnabled.Value) return;

                wardIsHealing.Remove(__instance);
                wardIsRepairing.Remove(__instance);
            }
        }
        
        [HarmonyPatch(typeof(RandEventSystem), nameof(RandEventSystem.GetValidEventPoints))]
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
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.HideMarker))]
        public static class PrivateArea_HideMarker_showAreaMarker
        {
            public static bool Prefix()
            {
                if (!modEnabled.Value) return true;

                return !showAreaMarker.Value;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.AddUserList))]
        public static class PrivateArea_AddUserList_WardAltActionCaption
        {
            public static void Postfix(PrivateArea __instance, ref StringBuilder text)
            {
                if (!modEnabled.Value)
                    return;

                if (!__instance.IsEnabled())
                    return;

                string[] lines = text.ToString().Split(new char[] { '\n' }, StringSplitOptions.None);

                int index = 0;
                foreach (string line in lines)
                {
                    index += line.Length;
                    if (line.Contains("$KEY_Use"))
                        break;
                    index++;
                }

                List<string> status = new List<string>();

                if (wardIsRepairing.TryGetValue(__instance, out int piecesToRepair))
                {
                    status.Add($"$hud_repair {piecesToRepair}");
                }
                else if (index < text.Length && wardPassiveRepair.Value)
                {
                    string actionCaption = $"$menu_start {Localization.instance.Localize("$piece_repair").ToLower()}";

                    if (!ZInput.IsAlternative1Functionality() || !ZInput.IsGamepadActive())
                        text.Insert(index, $"\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] {actionCaption}");
                    else
                        text.Insert(index, $"\n[<color=yellow><b>$KEY_JoyAltKeys + $KEY_Use</b></color>] {actionCaption}");
                }

                if (wardIsHealing.TryGetValue(__instance, out DateTime endDate))
                    status.Add($"$item_food_regen {endDate.Subtract(DateTime.Now).ToString(@"m\:ss")}");

                if (status.Count > 0)
                {
                    text.Append("\n$guardianstone_hook_power_activate: ");
                    text.Append(String.Join(", ", status.ToArray()));
                }

                List<string> offeringsList = new List<string>();

                if (offeringActiveRepair.Value && Player.m_localPlayer.IsMaterialKnown("$item_surtlingcore"))
                    offeringsList.Add("$item_surtlingcore");
                if (offeringAugmenting.Value && Player.m_localPlayer.IsMaterialKnown("$item_blackcore"))
                    offeringsList.Add("$item_blackcore");
                if (offeringFood.Value)
                    offeringsList.Add("$item_food");
                if (offeringMead.Value)
                    offeringsList.Add("$se_mead_name");
                if (offeringThundertone.Value && Player.m_localPlayer.IsMaterialKnown("$item_thunderstone"))
                    offeringsList.Add("$item_thunderstone");
                if (offeringTrophy.Value)
                    offeringsList.Add("$inventory_trophies");

                if (offeringsList.Count > 0)
                {
                    text.Append("\n\n$piece_offerbowl_offeritem: ");
                    text.Append(String.Join(", ", offeringsList.ToArray()));
                }
                
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
        public static class PrivateArea_Interact_PassiveEffectWardRange
        {
            private static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, bool alt, Character.Faction ___m_ownerFaction)
            {
                instance.ConfigUpdate();

                if (!modEnabled.Value) return true;

                if (!wardPassiveRepair.Value) return true;

                if (!alt) return true;

                if (hold) return true;

                if (___m_ownerFaction != 0) return true;

                if (!__instance.IsEnabled()) return true;

                if (wardIsRepairing.ContainsKey(__instance)) return true;

                logger.LogInfo($"Passive repairing begins");
                instance.StartCoroutine(PassiveRepairEffect(__instance, human as Player));

                return false;
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

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.UseItem))]
        public static class PrivateArea_UseItem_Offerings
        {
            private static void Postfix(PrivateArea __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
            {
                if (!__instance.IsEnabled()) return;

                Player player = user as Player;

                if (!player)
                {
                    logger.LogInfo("UseItem user not a player");
                    return;
                }

                bool repair = (item.m_shared.m_name == "$item_surtlingcore" && offeringActiveRepair.Value) || (item.m_shared.m_name == "$item_blackcore" && offeringAugmenting.Value);
                bool augment = item.m_shared.m_name == "$item_blackcore" && offeringAugmenting.Value;

                bool consumable = (offeringFood.Value || offeringMead.Value) && (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable);

                bool thunderstrike = offeringThundertone.Value && item.m_shared.m_name == "$item_thunderstone";

                bool trophy = offeringTrophy.Value && (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy);

                if (!repair && !consumable && !thunderstrike && !trophy) return;

                if (repair)
                    RepairNearestStructures(augment, __instance, player, item);

                if (consumable)
                    ApplyConsumableEffectToNearestPlayers(__instance, item, player);

                if (thunderstrike)
                    ApplyThunderstrikeOnNearbyEnemies(__instance, item, player);

                if (trophy)
                    ApplyTrophyEffectOnNearbyEnemies(__instance, item, player);

                __result = true;
            }
        }

        private static void ApplyTrophyEffectOnNearbyEnemies(PrivateArea ward, ItemDrop.ItemData item, Player initiator)
        {
            trophyTargets = Resources.FindObjectsOfTypeAll<Turret>().FirstOrDefault().m_configTargets;

            bool killed = false;

            foreach (Turret.TrophyTarget configTarget in trophyTargets)
            {
                if (!(item.m_shared.m_name == configTarget.m_item.m_itemData.m_shared.m_name))
                {
                    continue;
                }

                List<Character> characters = new List<Character>();
                ward.GetConnectedAreas().ForEach(area => Character.GetCharactersInRange(area.transform.position, area.m_radius, characters));

                foreach (Character character in characters.Distinct().ToList())
                {
                    if (character.IsMonsterFaction(Time.time))
                    {
                        foreach (Character onlyTarget in configTarget.m_targets)
                        {
                            if (character.m_name == onlyTarget.m_name)
                            {
                                character.SetHealth(0f);
                                killed = true;
                            }
                        }
                    }
                }
            }

            if (killed)
            {
                initiator.GetInventory().RemoveOneItem(item);
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_offerdone"));
            }
            else
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_offerwrong"));
            }
        }

        private static void ApplyThunderstrikeOnNearbyEnemies(PrivateArea ward, ItemDrop.ItemData item, Player initiator)
        {
            Incinerator incinerator = Resources.FindObjectsOfTypeAll<Incinerator>().FirstOrDefault();

            lightningAOE = incinerator.m_lightingAOEs;
            preLightning = incinerator.m_leverEffects;

            instance.StartCoroutine(LightningStrikeEffect(ward));
            
            initiator.GetInventory().RemoveOneItem(item);
            initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$piece_incinerator_conversion"));
        }

        private static void ApplyConsumableEffectToNearestPlayers(PrivateArea ward, ItemDrop.ItemData item, Player initiator)
        {
            if (item.m_shared.m_food > 0f)
            {
                if (wardIsHealing.ContainsKey(ward))
                {
                    initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_offerwrong"));
                    return;
                }

                wardIsHealing.Add(ward, DateTime.Now.AddMinutes(3));

                instance.StartCoroutine(PassiveHealingEffect(ward, amount:item.m_shared.m_foodRegen, seconds:2));
                logger.LogInfo("Passive healing begins");

                initiator.GetInventory().RemoveOneItem(item);
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_consumed"));
                
                return;
            }

            if (!(bool)item.m_shared.m_consumeStatusEffect) return;

            logger.LogInfo("Consumable effect offered");

            List<Player> players = new List<Player>();

            ward.GetConnectedAreas().ForEach(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            bool applied = false;

            foreach (Player player in players.Distinct().ToList())
            {
                if (!player.CanConsumeItem(item)) continue;

                _ = item.m_shared.m_consumeStatusEffect;
                player.m_seman.AddStatusEffect(item.m_shared.m_consumeStatusEffect, resetTime: true);
                applied = true;
            }

            if (!applied)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantoffer"));
                return;
            }

            initiator.GetInventory().RemoveOneItem(item);
            initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_offerdone"));
        }

        private static void RepairNearestStructures(bool augment, PrivateArea ward, Player initiator, ItemDrop.ItemData item)
        {
            int repaired = 0;
            int augmented = 0;

            List<Piece> pieces = new List<Piece>();

            ward.GetConnectedAreas().ForEach(area => Piece.GetAllPiecesInRadius(area.transform.position, area.m_radius, pieces));

            foreach (Piece piece in pieces)
            {
                if (!piece.IsPlacedByPlayer()) continue;

                WearNTear component = piece.GetComponent<WearNTear>();
                if (!(bool)component) continue;

                if (component.Repair())
                {
                    repaired++;
                    piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation);
                }

                if (augment && component.m_nview != null && component.m_nview.IsValid() && component.m_nview.IsOwner())
                {
                    if (component.m_nview.GetZDO().GetFloat(ZDOVars.s_health, component.m_health) < component.m_health * 2f)
                    {
                        component.m_nview.GetZDO().Set(ZDOVars.s_health, component.m_health * 2f);
                        component.m_nview.InvokeRPC(ZNetView.Everybody, "WNTHealthChanged", component.m_health * 2f);
                        augmented++;
                    };
                }
            }

            if (repaired + augmented > 0)
                initiator.GetInventory().RemoveOneItem(item);

            if (augmented > 0)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_offerdone"));
                if (repaired > 0)
                    initiator.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_repaired", new string[1] { repaired.ToString() }));
            }
            else if (repaired > 0)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_repaired", new string[1] { repaired.ToString() }));
            }
            else if (augment)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantoffer"));
            }
            else
            {
                string str = Localization.instance.Localize("$msg_doesnotneedrepair");
                initiator.Message(MessageHud.MessageType.Center, char.ToUpper(str[0]) + str.Substring(1));
            }
        }
        
        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        public static class Character_Damage_DamageMultipliers
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
                            if (hit.GetTotalDamage() != hit.m_damage.m_fire)
                                area.FlashShield(false);

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

        [HarmonyPatch(typeof(Character), nameof(Character.AddFireDamage))]
        public static class Character_AddFireDamage_IndirectFireDamageProtection
        {
            private static void Prefix(Character __instance, ref float damage)
            {
                if (!modEnabled.Value) return;

                if (boarsHensProtection.Value && __instance.IsTamed() && (__instance.m_group == "boar" || __instance.m_group == "chicken") && InsideEnabledPlayersArea(__instance.transform.position)) 
                {
                    damage = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.UpdateSmoke))]
        public static class Character_UpdateSmoke_IndirectSmokeDamageProtection
        {
            private static void Prefix(Character __instance, ref float dt)
            {
                if (!modEnabled.Value) return;

                if (boarsHensProtection.Value && __instance.IsTamed() && (__instance.m_group == "boar" || __instance.m_group == "chicken") && InsideEnabledPlayersArea(__instance.transform.position))
                {
                    dt = 0f;
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

                if (hit.GetAttacker() == null) return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, out PrivateArea area))
                    return;

                if (__instance.GetComponent<Plant>() != null)
                {
                    ModifyHitDamage(ref hit, 0f);
                    area.FlashShield(false);
                }
                else if (__instance.TryGetComponent<Pickable>(out Pickable pickable))
                {
                    ItemDrop.ItemData.SharedData m_shared = pickable.m_itemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared;

                    if (m_shared.m_name == "$item_carrot" || m_shared.m_name == "$item_turnip" || m_shared.m_name == "$item_onion" || 
                        m_shared.m_name == "$item_carrotseeds" || m_shared.m_name == "$item_turnipseeds" || m_shared.m_name == "$item_onionseeds" || 
                        m_shared.m_name == "$item_jotunpuffs" || m_shared.m_name == "$item_magecap")
                    {
                        ModifyHitDamage(ref hit, 0f);
                        area.FlashShield(false);
                    }
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
                if (wardShipProtection.Value == ShipDamageType.Off) return true;

                return !InsideEnabledPlayersArea(__instance.transform.position);
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateWaterForce))]
        public static class Ship_UpdateWaterForce_PreventShipDamage
        {
            private static void Prefix(Ship __instance, ref float ___m_waterImpactDamage, ref float __state)
            {
                if (!modEnabled.Value) return;
                if (wardShipProtection.Value == ShipDamageType.Off) return;

                if (!InsideEnabledPlayersArea(__instance.transform.position)) return;

                __state = ___m_waterImpactDamage;

                ___m_waterImpactDamage = 0f;
            }

            private static void Postfix(Ship __instance, ref float ___m_waterImpactDamage, float __state)
            {
                if (!modEnabled.Value) return;
                if (wardShipProtection.Value == ShipDamageType.Off) return;

                if (!InsideEnabledPlayersArea(__instance.transform.position)) return;

                ___m_waterImpactDamage = __state;
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

                if (!InsideEnabledPlayersArea(__instance.transform.position))
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

                if (!InsideEnabledPlayersArea(__instance.transform.position))
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

                if (!InsideEnabledPlayersArea(__instance.transform.position))
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

                if (!InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                __state = dt;

                dt *= Math.Max(0f, fireplaceDrainMultiplier.Value);
                if (cookingSpeedMultiplier.Value > 0f)
                    dt /= cookingSpeedMultiplier.Value;
            }

            private static void Postfix(Smelter __instance, ref float dt, float __state)
            {
                if (!modEnabled.Value)
                    return;

                if ((fireplaceDrainMultiplier.Value == 1.0f) && (cookingSpeedMultiplier.Value == 1.0f))
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position))
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

                if (!InsideEnabledPlayersArea(__instance.transform.position))
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

                if (!InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                __result *= Math.Max(0f, sapCollectingSpeedMultiplier.Value);
            }
        }

        [HarmonyPatch(typeof(Trap), nameof(Trap.OnTriggerEnter))]
        static class Trap_OnTriggerEnter_TrapProtection
        {
            private static bool Prefix(Collider collider)
            {
                if (!modEnabled.Value)
                    return true;

                if (!wardTrapProtection.Value)
                    return true;

                Player player = collider.GetComponentInParent<Player>();
                if (player == null)
                    return true;

                if (!InsideEnabledPlayersArea(player.transform.position))
                    return true;

                return false;
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

                if (!InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                __state = ___m_attackCooldown;

                ___m_attackCooldown *= Math.Max(0.0f, turretFireRateMultiplier.Value);
            }

            private static void Postfix(Turret __instance, ref float ___m_attackCooldown, float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (__state > 0f)
                    ___m_attackCooldown = __state;
            }
        }

    }
}
