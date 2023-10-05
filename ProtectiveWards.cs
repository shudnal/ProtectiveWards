using System;
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
        const string pluginVersion = "1.1.9";

        private Harmony _harmony;

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> configLocked;

        private static ConfigEntry<bool> disableFlash;
        private static ConfigEntry<bool> showAreaMarker;
        private static ConfigEntry<bool> loggingEnabled;
        private static ConfigEntry<int> refreshingTime;
        private static ConfigEntry<bool> showOfferingsInHover;
        private static ConfigEntry<float> maxTaxiSpeed;

        private static ConfigEntry<bool> offeringActiveRepair;
        private static ConfigEntry<bool> offeringAugmenting;
        private static ConfigEntry<bool> offeringFood;
        private static ConfigEntry<bool> offeringMead;
        private static ConfigEntry<bool> offeringThundertone;
        private static ConfigEntry<bool> offeringTrophy;
        private static ConfigEntry<bool> offeringYmirRemains;
        private static ConfigEntry<bool> offeringEitr;
        private static ConfigEntry<bool> offeringDragonEgg;
        private static ConfigEntry<bool> offeringTaxi;

        private static ConfigEntry<bool> wardPassiveRepair;
        private static ConfigEntry<int> autoCloseDoorsTime;

        private static ConfigEntry<bool> setWardRange;
        private static ConfigEntry<float> wardRange;
        private static ConfigEntry<bool> supressSpawnInRange;
        private static ConfigEntry<bool> permitEveryone;

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
        internal static Dictionary<PrivateArea, int> wardIsHealing = new Dictionary<PrivateArea, int>();
        internal static Dictionary<PrivateArea, int> wardIsClosing = new Dictionary<PrivateArea, int>();
        internal static Dictionary<PrivateArea, List<Door>> doorsToClose = new Dictionary<PrivateArea, List<Door>>();

        internal static GameObject lightningAOE;
        internal static EffectList preLightning;
        internal static List<Turret.TrophyTarget> trophyTargets;
        internal static int baseValueProtected = 999;

        internal static bool taxiReturnBack = false;
        internal static Vector3 taxiPlayerPositionToReturn;
        internal static Vector3 taxiTargetPosition;
        internal static bool canTravel = true;
        internal static Player isTravelingPlayer;
        internal static bool playerDropped = false;
        internal static bool castSlowFall;

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

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);
        }

        private void OnDestroy()
        {
            Config.Save();
            _harmony?.UnpatchSelf();
        }
        
        public static void LogInfo(object data)
        {
            if (loggingEnabled.Value)
                instance.Logger.LogInfo(data);
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
            loggingEnabled = config("Misc", "Enable logging", defaultValue: false, "Enable logging for ward events. [Not Synced with Server]", false);
            showOfferingsInHover = config("Misc", "Show offerings in hover", defaultValue: true, "Show offerings list in hover text. [Not Synced with Server]", false);
            maxTaxiSpeed = config("Misc", "Maximum taxi speed", defaultValue: 30f, "Reduce maximum taxi speed if it is laggy. [Not Synced with Server]", false);


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

            
            offeringActiveRepair = config("Offerings", "1 - Repair all pieces by surtling core offering", defaultValue: true, "Offer surtling core to ward to instantly repair all pieces in all connected areas" +
                                                                                                                               "\nCore will NOT be wasted if there is no piece to repair");
            offeringAugmenting = config("Offerings", "2 - Augment all pieces by black core offering", defaultValue: true, "Offer black core to ward to double the health of every structural piece in all connected areas" +
                                                                                                                            "\nCore will NOT be wasted if there is no piece to repair");
            offeringFood = config("Offerings", "3 - Heal all allies for 3 min by food offering", defaultValue: true, "Offer food to ward to activate healing for 3 minutes in all connected areas. Better food means better heal."+
                                                                                                                       "\nYou can offer one food to one ward until 3 minutes are gone. But nothing stops you from offering food to several wards.");
            offeringMead = config("Offerings", "4 - Share mead effect to all players by mead offering", defaultValue: true, "Offer mead to ward to share the effect to all players in all connected areas. " +
                                                                                                                              "\nMead will NOT be wasted if no one can have effect.");
            offeringThundertone = config("Offerings", "5 - Call the wrath of the Thor upon your enemies by thunderstone offering", defaultValue: true, "Offer thunderstone to ward to call the Thor's wrath upon your enemies in all connected areas" +
                                                                                                                                                        "\nThunderstone WILL be wasted even if no one gets hurt");
            offeringTrophy = config("Offerings", "6 - Kill all enemies of the same type by trophy offering", defaultValue: true, "Offer trophy to ward to kill all enemies with type of the offered trophy in all connected areas" +
                                                                                                                                   "\nTrophy will NOT be wasted if no one gets hurt");
            offeringYmirRemains = config("Offerings", "7 - Grow all plants by Ymir flesh offering", defaultValue: true, "Offer Ymir flesh to instantly grow every plant in all connected areas" +
                                                                                                                                   "\nYmir flesh will NOT be wasted if there is no plant to grow");
            offeringEitr = config("Offerings", "8 - Grow all plants regardless the requirements by Eitr x5 offering", defaultValue: true, "Offer 5 Eitr to instantly grow every plant regardless the requirements in all connected areas" +
                                                                                                                                   "\nEitr will NOT be wasted if there is no plant to grow");
            offeringDragonEgg = config("Offerings", "9 - Activate Moder power by dragon egg offering", defaultValue: true, "Offer dragon egg to activate Moder power on all players in all connected areas.");

            offeringTaxi = config("Offerings", "10 - Fly back and forth to distant point by different items offering", defaultValue: true, "Offer boss trophy to travel to start temple (initial spawn point). Boss trophy will NOT be consumed." +
                                                                                                                                   "\nOffer coins to travel to Haldor (x2000 if you didn't find him yet. x500 otherwise). Coins will be consumed." +
                                                                                                                                   "\nOffer Hildir's chest to travel to Hildir for free. Chest will NOT be consumed. Totem WILL be consumed." +
                                                                                                                                   "\nOffer Fuling totem to travel to Hildir. Totem WILL be consumed.");


            wardPassiveRepair = config("Passive", "Activatable passive repair", defaultValue: true, "Interact with a ward to start passive repair process of all pieces in all connected areas" +
                                                                                                      "\nWard will repair one piece every 10 seconds until all pieces are healthy. Then the process will stop.");
            autoCloseDoorsTime = config("Passive", "Auto close doors after", defaultValue: 0, "Automatically close doors after a specified number of seconds. 0 to disable. 5 recommended");

                        
            setWardRange = config("Range", "Change Ward range", defaultValue: false, "Change ward range.");
            wardRange = config("Range", "Ward range", defaultValue: 10f, "Ward range. Toggle ward protection for changes to take effect");
            supressSpawnInRange = config("Range", "Supress spawn in ward area", defaultValue: true, "Vanilla behavior is true. Set false if you want creatures and raids spawn in ward radius. Toggle ward protection for changes to take effect");
            permitEveryone = config("Range", "Grant permittance to everyone", defaultValue: false, "Grant permittance to every player. There still will be permittance list on ward but it won't take any effect.");


            boarsHensProtection = config("Ward protects", "Boars and hens from damage", true, "Set whether an active Ward will protect nearby boars and hens from taken damage (players excluded)");
            wardRainProtection = config("Ward protects", "Structures from rain damage", true, "Set whether an active Ward will protect nearby structures from rain and water damage");
            wardShipProtection = config("Ward protects", "Ship from damage", ShipDamageType.WaterDamage, "Set whether an active Ward will protect nearby ships from damage (waves and upsidedown for water damage option or any structural damage)");
            wardPlantProtection = config("Ward protects", "Plants from any damage", true, "Set whether an active Ward will protect nearby plants from taking damage");
            fireplaceProtection = config("Ward protects", "Fireplace from step damage", true, "Set whether an active Ward will protect nearby fire sources from taking damage from stepping on them");
            wardTrapProtection = config("Ward protects", "Players from their traps", true, "Set whether an active Ward will protect players from stepping on traps");
            sittingRaidProtection = config("Ward protects", "Players from raids when sitting on something near the fire (not floor)", true, "Set whether an active Ward will protect nearby players from raids when sitting next to an active fire"
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

        private static void SetWardPlayerBase(ref PrivateArea __instance)
        {
            Transform playerBase = __instance.transform.Find("PlayerBase");
            if (playerBase != null)
            {
                float scale = Math.Min(1f, 10f / Math.Max(wardRange.Value, 1f));
                if (supressSpawnInRange.Value)
                    scale = 1f;

                playerBase.localScale = new Vector3(scale, scale, scale);
            }
        }

        private static void ModifyHitDamage(ref HitData hit, float value)
        {
            hit.m_damage.Modify(Math.Max(value, 0));
        }

        private static List<PrivateArea> ConnectedAreas(PrivateArea ward)
        {
            List<PrivateArea> areas = ward.GetConnectedAreas();
            areas.Add(ward);
            return areas.Where(area => area.IsEnabled()).Distinct().ToList();
        }

        public static IEnumerator PassiveRepairEffect(PrivateArea ward, Player initiator)
        {
            while (true)
            {
                if (ward == null)
                    yield break;

                if (!ZNetScene.instance)
                    yield break;

                if (Game.IsPaused())
                    yield return new WaitForSeconds(2.0f);

                List<Piece> pieces = new List<Piece>();

                ConnectedAreas(ward).ForEach(area => Piece.GetAllPiecesInRadius(area.transform.position, area.m_radius, pieces));

                List<Piece> piecesToRepair = pieces.Distinct().ToList().Where(piece => piece.IsPlacedByPlayer() && piece.TryGetComponent<WearNTear>(out WearNTear WNT) && WNT.GetHealthPercentage() < 1.0f).ToList();

                if (piecesToRepair.Count == 0)
                {
                    LogInfo($"Passive repairing stopped");
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

                if (Game.IsPaused())
                    yield return new WaitForSeconds(2.0f);

                if (!wardIsHealing.TryGetValue(ward, out int secondsLeft))
                    yield break;

                if (secondsLeft <= 0)
                {
                    wardIsHealing.Remove(ward);
                    LogInfo($"Passive healing stopped");
                    yield break;
                }

                wardIsHealing[ward] -= seconds;

                List<Player> players = new List<Player>();
                List<Character> characters = new List<Character>();

                ConnectedAreas(ward).ForEach(area => 
                {
                    Player.GetPlayersInRange(area.transform.position, area.m_radius, players);
                    Character.GetCharactersInRange(area.transform.position, area.m_radius, characters);
                });

                foreach (Player player in players.Distinct().ToList())
                {
                    player.Heal(amount * seconds);
                }

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
            ConnectedAreas(ward).ForEach(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            foreach (Player player in players.Distinct().ToList())
            {
                preLightning.Create(player.transform.position, player.transform.rotation);
            }

            LogInfo("Thor is preparing his strike");

            yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 7f));

            if (Game.IsPaused())
                yield return new WaitForSeconds(1.0f);

            List<Character> characters = new List<Character>();
            ConnectedAreas(ward).ForEach(area => Character.GetCharactersInRange(area.transform.position, area.m_radius, characters));

            foreach (Character character in characters.Distinct().ToList())
            {
                if (character.IsMonsterFaction(Time.time))
                {
                    UnityEngine.Object.Instantiate(lightningAOE, character.transform.position, character.transform.rotation);
                }
            }
        }

        public static IEnumerator AutoClosingDoors(PrivateArea ward)
        {
            while (true)
            {
                if (ward == null)
                    yield break;

                if (Game.IsPaused())
                    yield return new WaitForSeconds(2.0f);

                if (!wardIsClosing.TryGetValue(ward, out int secondsToClose))
                    yield break;

                if (!doorsToClose.TryGetValue(ward, out List<Door> doors))
                    yield break;

                if (secondsToClose <= 0)
                {
                    if (doors.Count > 0)
                    {
                        LogInfo($"Closed {doors.Count} doors");
                        doors.ForEach(door =>
                        {
                            if (door.m_nview.IsValid())
                            {
                                door.m_nview.GetZDO().Set(ZDOVars.s_state, 0);
                                door.UpdateState();
                            }
                        });
                    }

                    wardIsClosing.Remove(ward);
                    doorsToClose.Remove(ward);
                    LogInfo($"Doors closing stopped");
                    yield break;
                }

                wardIsClosing[ward] -= 1;

                yield return new WaitForSeconds(1);
            }
        }

        public static IEnumerator InstantGrowthEffect(PrivateArea ward, List<Plant> plants)
        {
            if (ward == null)
                yield break;

            LogInfo("Instant growth started");

            yield return new WaitForSeconds(1);

            foreach (Plant plant in plants.Distinct().ToList())
            {
                if (!plant.m_nview.IsOwner()) continue;

                if (plant.m_status != 0)
                {
                    plant.UpdateHealth(0);
                }
                
                plant.Grow();
                yield return new WaitForSeconds(0.25f);
            }

            LogInfo("Instant growth ended");
        }

        public static IEnumerator ReturnPlayerToPosition(Player player, Vector3 position, int seconds)
        {
            if (player == null)
                yield break;

            canTravel = false;

            LogInfo("Timer of player returnal started");

            for (int i = seconds; i >= 0; i--)
            {
                player.Message((i > 15) ? MessageHud.MessageType.TopLeft : MessageHud.MessageType.Center, Localization.instance.Localize("$button_return") + " " + TimeSpan.FromSeconds(i).ToString(@"m\:ss"));
                yield return new WaitForSeconds(1);
            }

            LogInfo("Timer of player returnal ended");

            player.StartCoroutine(TaxiToPosition(player, position));
        }

        private static void GetPlantsInRange(Vector3 point, float radius, List<Plant> plants, bool growableOnly)
        {
            List <SlowUpdate> allPlants = SlowUpdate.GetAllInstaces();

            float num = radius * radius;
            foreach (SlowUpdate su_plant in allPlants)
            {
                if (!su_plant.TryGetComponent<Plant>(out Plant plant)) continue;

                if (Utils.DistanceSqr(su_plant.transform.position, point) < num && plant.m_nview.IsOwner() && (!growableOnly || plant.m_status == 0))
                {
                    plants.Add(plant);
                }
            }
        }

        [HarmonyPatch(typeof(Door), nameof(Door.Interact))]
        public static class Door_SetState_AutoClose
        {
            public static void Postfix(Door __instance, ZNetView ___m_nview, bool __result)
            {
                
                if (!modEnabled.Value) return;

                if (autoCloseDoorsTime.Value == 0) return;

                if (!___m_nview.IsValid()) return;

                if (!__result) return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, out PrivateArea ward)) return;

                if (!doorsToClose.TryGetValue(ward, out List<Door> doors))
                    doors = new List<Door>();

                int state = ___m_nview.GetZDO().GetInt(ZDOVars.s_state);

                if (state == 0)
                {
                    doors.Remove(__instance);
                }
                else if (!doors.Contains(__instance))
                {
                    doors.Add(__instance);
                }

                if (doors.Count == 0)
                {
                    wardIsClosing.Remove(ward);
                    doorsToClose.Remove(ward);
                    return;
                }

                if (state == 0) return;

                doorsToClose[ward] = doors;

                LogInfo(doors.Count);

                if (wardIsClosing.ContainsKey(ward))
                {
                    wardIsClosing[ward] = Math.Max(autoCloseDoorsTime.Value, 2);
                    LogInfo($"Doors closing reset to {wardIsClosing[ward]} seconds");
                }
                else
                {
                    wardIsClosing[ward] = Math.Max(autoCloseDoorsTime.Value, 2);
                    ward.StartCoroutine(AutoClosingDoors(ward));
                    LogInfo($"Doors closing started");
                }

            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.OnDestroy))]
        public static class PrivateArea_OnDestroy_ClearStatus
        {
            public static void Prefix(PrivateArea __instance)
            {
                if (!modEnabled.Value) return;

                areaCache.Clear();

                wardIsHealing.Remove(__instance);
                wardIsRepairing.Remove(__instance);
                wardIsClosing.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdateBaseValue))]
        public static class Player_UpdateBaseValue_SittingRaidProtection
        {
            public static void Postfix(Player __instance, float ___m_baseValueUpdateTimer, ref int ___m_baseValue, ZNetView ___m_nview)
            {
                if (!modEnabled.Value || !sittingRaidProtection.Value)
                    return; 
                
                if ((___m_baseValueUpdateTimer == 0f) && (___m_baseValue >= 3))
                {
                    if (!__instance.IsSitting() || !__instance.m_attached || !__instance.m_seman.HaveStatusEffect(Player.s_statusEffectCampFire) || !InsideEnabledPlayersArea(__instance.transform.position))
                        return;

                    ___m_baseValue = baseValueProtected;

                    ZNet.instance.m_serverSyncedPlayerData["baseValue"] = ___m_baseValue.ToString();
                    ___m_nview.GetZDO().Set(ZDOVars.s_baseValue, ___m_baseValue);
                }
            }
        }

        [HarmonyPatch(typeof(RandEventSystem), nameof(RandEventSystem.CheckBase))]
        public static class RandEventSystem_CheckBase_SittingRaidProtection
        {
            public static void Postfix(RandEventSystem.PlayerEventData player, ref bool __result)
            {
                if (!modEnabled.Value || !sittingRaidProtection.Value || __result == false)
                    return;

                if (player.baseValue != baseValueProtected)
                    return;

                LogInfo($"Player at {player.position.x} {player.position.z} is in raid protected state.");
                __result = false;
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

                    if (!ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive())
                        text.Insert(index, $"\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] {actionCaption}");
                    else
                        text.Insert(index, $"\n[<color=yellow><b>$KEY_JoyAltKeys + $KEY_Use</b></color>] {actionCaption}");
                }

                if (wardIsHealing.TryGetValue(__instance, out int secondsLeft))
                    status.Add($"$item_food_regen {TimeSpan.FromSeconds(secondsLeft).ToString(@"m\:ss")}");

                if (status.Count > 0)
                {
                    text.Append("\n$guardianstone_hook_power_activate: ");
                    text.Append(String.Join(", ", status.ToArray()));
                }

                if (showOfferingsInHover.Value)
                {
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
                    if (offeringYmirRemains.Value && Player.m_localPlayer.IsMaterialKnown("$item_ymirremains"))
                        offeringsList.Add("$item_ymirremains");
                    if (offeringEitr.Value && Player.m_localPlayer.IsMaterialKnown("$item_eitr"))
                        offeringsList.Add("$item_eitr");
                    if (offeringDragonEgg.Value && Player.m_localPlayer.IsMaterialKnown("$item_dragonegg") && ZoneSystem.instance.GetGlobalKey(GlobalKeys.defeated_dragon))
                        offeringsList.Add("$item_dragonegg");
                    if (offeringTaxi.Value)
                    {
                        if (Player.m_localPlayer.IsMaterialKnown("$item_coins"))
                            offeringsList.Add("$item_coins");
                        if (Player.m_localPlayer.IsMaterialKnown("$item_goblintotem"))
                            offeringsList.Add("$item_goblintotem");
                        if (Player.m_localPlayer.IsMaterialKnown("$item_chest_hildir1"))
                            offeringsList.Add("$piece_chestwood");
                        else if (Player.m_localPlayer.IsMaterialKnown("$item_chest_hildir2"))
                            offeringsList.Add("$piece_chestwood");
                        else if (Player.m_localPlayer.IsMaterialKnown("$item_chest_hildir3"))
                            offeringsList.Add("$piece_chestwood");
                    }

                    if (offeringsList.Count > 0)
                    {
                        text.Append("\n\n$piece_offerbowl_offeritem: ");
                        text.Append(String.Join(", ", offeringsList.ToArray()));
                    }
                }
                
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.IsPermitted))]
        public static class PrivateArea_AddUserList_PermittanceToEveryone
        {
            public static bool Prefix(PrivateArea __instance, ref bool __result)
            {
                if (!modEnabled.Value)
                    return true;

                if (!permitEveryone.Value)
                    return true;

                __result = true;
                return false;
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

                if (!alt) areaCache.Clear();

                if (!wardPassiveRepair.Value) return true;

                if (!alt) return true;

                if (hold) return true;

                if (___m_ownerFaction != 0) return true;

                if (!__instance.IsEnabled()) return true;

                if (wardIsRepairing.ContainsKey(__instance)) return true;

                LogInfo($"Passive repairing begins");
                instance.StartCoroutine(PassiveRepairEffect(__instance, human as Player));

                return false;
            }

            private static void Postfix(ref PrivateArea __instance)
            {
                if (!modEnabled.Value) return;

                if (setWardRange.Value) 
                {
                    if (__instance.m_radius != wardRange.Value) 
                        SetWardRange(ref __instance);

                    SetWardPlayerBase(ref __instance);
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

                    SetWardPlayerBase(ref __instance);
                }
            }

            private static void Postfix(ref PrivateArea __instance)
            {
                if (!modEnabled.Value) return;

                if (setWardRange.Value)
                {
                    SetWardRange(ref __instance);

                    SetWardPlayerBase(ref __instance);
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
                    LogInfo("UseItem user not a player");
                    return;
                }

                bool augment = item.m_shared.m_name == "$item_blackcore" && offeringAugmenting.Value;
                bool repair = augment || (item.m_shared.m_name == "$item_surtlingcore" && offeringActiveRepair.Value);

                bool consumable = (offeringFood.Value || offeringMead.Value) && (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable);

                bool thunderstrike = offeringThundertone.Value && item.m_shared.m_name == "$item_thunderstone";

                bool trophy = offeringTrophy.Value && (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy);

                bool growAll = offeringEitr.Value && item.m_shared.m_name == "$item_eitr";
                bool growth = (offeringYmirRemains.Value && item.m_shared.m_name == "$item_ymirremains") || growAll;

                bool moderPower = item.m_shared.m_name == "$item_dragonegg" && ZoneSystem.instance.GetGlobalKey(GlobalKeys.defeated_dragon);

                bool taxi = offeringTaxi.Value && (item.m_shared.m_name == "$item_coins" ||
                                                   item.m_shared.m_name == "$item_trophy_eikthyr" ||
                                                   item.m_shared.m_name == "$item_trophy_elder" ||
                                                   item.m_shared.m_name == "$item_trophy_bonemass" ||
                                                   item.m_shared.m_name == "$item_trophy_dragonqueen" ||
                                                   item.m_shared.m_name == "$item_trophy_goblinking" ||
                                                   item.m_shared.m_name == "$item_trophy_seekerqueen" ||
                                                   item.m_shared.m_name == "$item_goblintotem" ||
                                                   item.m_shared.m_name == "$item_chest_hildir1" ||
                                                   item.m_shared.m_name == "$item_chest_hildir2" ||
                                                   item.m_shared.m_name == "$item_chest_hildir3");

                if (!repair && !consumable && !thunderstrike && !trophy && !growth && !moderPower && !taxi) return;

                if (repair)
                    RepairNearestStructures(augment, __instance, player, item);

                if (consumable)
                    ApplyConsumableEffectToNearestPlayers(__instance, item, player);

                if (thunderstrike)
                    ApplyThunderstrikeOnNearbyEnemies(__instance, item, player);

                if (trophy)
                    ApplyTrophyEffectOnNearbyEnemies(__instance, item, player);

                if (growth)
                    ApplyInstantGrowthEffectOnNearbyPlants(__instance, item, player, !growAll);

                if (moderPower)
                    ApplyModerPowerEffectToNearbyPlayers(__instance, item, player);

                if (taxi)
                    TaxiToLocation(item, player);

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
                ConnectedAreas(ward).ForEach(area => Character.GetCharactersInRange(area.transform.position, area.m_radius, characters));

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

                wardIsHealing.Add(ward, 180);

                instance.StartCoroutine(PassiveHealingEffect(ward, amount:item.m_shared.m_foodRegen / 2, seconds:1));
                LogInfo("Passive healing begins");

                initiator.GetInventory().RemoveOneItem(item);
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_consumed"));
                
                return;
            }

            if (!(bool)item.m_shared.m_consumeStatusEffect) return;

            LogInfo("Consumable effect offered");

            List<Player> players = new List<Player>();

            ConnectedAreas(ward).ForEach(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

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

            ConnectedAreas(ward).ForEach(area => Piece.GetAllPiecesInRadius(area.transform.position, area.m_radius, pieces));

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

        private static void ApplyInstantGrowthEffectOnNearbyPlants(PrivateArea ward, ItemDrop.ItemData item, Player initiator, bool growableOnly = true)
        {

            List<Plant> plants = new List<Plant>();

            ConnectedAreas(ward).ForEach(area => GetPlantsInRange(area.transform.position, area.m_radius, plants, growableOnly));

            if (plants.Count == 0)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantoffer"));
                return;
            }

            Inventory inventory = initiator.GetInventory();
            if (item.m_shared.m_name == "$item_eitr" && inventory.CountItems("$item_eitr") < 5)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_incompleteoffering"));
                return;
            }

            ward.StartCoroutine(InstantGrowthEffect(ward, plants));

            if (item.m_shared.m_name == "$item_eitr")
            {
                initiator.GetInventory().RemoveItem("$item_eitr", 5);
                LogInfo($"Offered {item.m_shared.m_name} x5");
            }
            else
            {
                initiator.GetInventory().RemoveOneItem(item);
                LogInfo($"Offered {item.m_shared.m_name}");
            }
            
            initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_offerdone"));

        }

        private static void ApplyModerPowerEffectToNearbyPlayers(PrivateArea ward, ItemDrop.ItemData item, Player initiator)
        {
            LogInfo("Dragon egg offered");

            List<Player> players = new List<Player>();

            ConnectedAreas(ward).ForEach(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            foreach (Player player in players.Distinct().ToList())
            {
                int moderPowerHash = "GP_Moder".GetStableHashCode();
                StatusEffect moderSE = ObjectDB.instance.GetStatusEffect(moderPowerHash);
                player.GetSEMan().AddStatusEffect(moderSE.NameHash(), resetTime: true);
            }

            initiator.GetInventory().RemoveOneItem(item);
        }

        private static void TaxiToLocation(ItemDrop.ItemData item, Player initiator)
        {
            if (initiator.IsEncumbered())
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$se_encumbered_start"));
                return;
            }

            if (!IsTeleportable(initiator))
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_noteleport"));
                return;
            }

            if (!canTravel)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantoffer"));
                return;
            }

            ZoneSystem.LocationInstance location;
            List<ZoneSystem.LocationInstance> locations = new List<ZoneSystem.LocationInstance>();

            bool targetingClosest = false;

            if (item.m_shared.m_name == "$item_coins")
            {
                if (!ZoneSystem.instance.FindLocations("Vendor_BlackForest", ref locations))
                    return;

                if (locations.Count == 1)
                {
                    location = locations[0];
                    LogInfo("Found 1 location Vendor_BlackForest");
                }
                else
                {
                    ZoneSystem.instance.FindClosestLocation("Vendor_BlackForest", initiator.transform.position, out location);
                    targetingClosest = true;
                    LogInfo("Targeting closest location Vendor_BlackForest");
                }
                    

            } else if (item.m_shared.m_name == "$item_trophy_eikthyr" ||
                       item.m_shared.m_name == "$item_trophy_elder" ||
                       item.m_shared.m_name == "$item_trophy_bonemass" ||
                       item.m_shared.m_name == "$item_trophy_dragonqueen" ||
                       item.m_shared.m_name == "$item_trophy_goblinking" ||
                       item.m_shared.m_name == "$item_trophy_seekerqueen")
            {
                if (!ZoneSystem.instance.FindClosestLocation("StartTemple", initiator.transform.position, out location))
                    return;
            }
            else if (item.m_shared.m_name == "$item_goblintotem" ||
                       item.m_shared.m_name == "$item_chest_hildir1" ||
                       item.m_shared.m_name == "$item_chest_hildir2" ||
                       item.m_shared.m_name == "$item_chest_hildir3")
            {
                if (!ZoneSystem.instance.FindLocations("Hildir_camp", ref locations))
                    return;

                if (locations.Count == 1)
                {
                    location = locations[0];
                    LogInfo("Found 1 location Hildir_camp");
                }
                else
                {
                    ZoneSystem.instance.FindClosestLocation("Hildir_camp", initiator.transform.position, out location);
                    targetingClosest = true;
                    LogInfo("Targeting closest location Hildir_camp");
                }
            } else
                return;

            if (Utils.DistanceXZ(initiator.transform.position, location.m_position) < 300f)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_wontwork"));
                return;
            }

            bool consumeItem = item.m_shared.m_name == "$item_goblintotem" || item.m_shared.m_name == "$item_coins";

            if (consumeItem)
            {
                if (item.m_shared.m_name == "$item_goblintotem")
                    initiator.GetInventory().RemoveOneItem(item);
                else if(item.m_shared.m_name == "$item_coins")
                {
                    int stack = targetingClosest ? 2000 : 500;
                    if (initiator.GetInventory().CountItems("$item_coins") < stack)
                    {
                        initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_incompleteoffering"));
                        return;
                    }
                    initiator.GetInventory().RemoveItem("$item_coins", stack);
                }
            }
            
            initiator.StartCoroutine(TaxiToPosition(initiator, location.m_position, returnBack: true, waitSeconds:10));
        }

        public static bool IsTeleportable(Player player)
        {
            if (player.IsTeleportable())
            {
                return true;
            }

            foreach (ItemDrop.ItemData item in player.GetInventory().m_inventory)
            {
                if (item.m_shared.m_name == "$item_chest_hildir1" ||
                    item.m_shared.m_name == "$item_chest_hildir2" ||
                    item.m_shared.m_name == "$item_chest_hildir3")
                    continue;

                if (!item.m_shared.m_teleportable)
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerator TaxiToPosition(Player player, Vector3 position, bool returnBack = false, int waitSeconds = 0)
        {
            canTravel = false;
            isTravelingPlayer = player;

            if (waitSeconds > 0)
            {
                for (int i = waitSeconds; i > 0; i--)
                {
                    player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$npc_dvergrmage_random_goodbye5") + " " + TimeSpan.FromSeconds(i).ToString(@"m\:ss"));
                    yield return new WaitForSeconds(1); 
                }
            }

            DateTime flightInitiated = DateTime.Now;

            if (Valkyrie.m_instance == null)
            {
                bool playerShouldExit = player.IsAttachedToShip() || player.IsAttached() || player.IsDead() || player.IsRiding() || player.IsSleeping() || player.IsTeleporting()
                                              || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();

                while (playerShouldExit || player.IsEncumbered() || !player.IsTeleportable())
                {
                    string timeSpent = (DateTime.Now - flightInitiated).ToString(@"m\:ss");
                    if (playerShouldExit)
                        player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$location_exit") + " " + timeSpent);
                    else
                        player.Message(MessageHud.MessageType.Center, Localization.instance.Localize(player.IsEncumbered() ? "$se_encumbered_start" : "$msg_noteleport") + " " + timeSpent);

                    yield return new WaitForSeconds(1);

                    playerShouldExit = player.IsAttachedToShip() || player.IsAttached() || player.IsDead() || player.IsRiding() || player.IsSleeping() || player.IsTeleporting()
                                              || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();
                }

                player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$npc_dvergrmage_random_goodbye5"));

                taxiTargetPosition = position;
                taxiReturnBack = returnBack;
                taxiPlayerPositionToReturn = player.transform.position;
                playerDropped = false;

                GameObject prefab = ZNetScene.instance.GetPrefab("Valkyrie");
                Instantiate(prefab, player.transform.position, Quaternion.identity);
            }
            
            canTravel = true;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetIntro))]
        public static class Player_SetIntro_Taxi
        {
            static void Postfix(Player __instance)
            {
                if (!modEnabled.Value) return;

                if (__instance.InIntro()) return;

                if (taxiReturnBack && canTravel)
                {
                    isTravelingPlayer = __instance;
                    __instance.StartCoroutine(ReturnPlayerToPosition(__instance, taxiPlayerPositionToReturn, 120));
                }
                else
                {
                    isTravelingPlayer = null;
                }

            }
        }

        [HarmonyPatch(typeof(Valkyrie), nameof(Valkyrie.Awake))]
        public static class Valkyrie_Awake_Taxi
        {
            private static bool Prefix(Valkyrie __instance)
            {
                if (!modEnabled.Value) return true;

                if (!offeringTaxi.Value) return true;

                if (isTravelingPlayer == null)
                    return true;

                if (isTravelingPlayer.m_firstSpawn) return true;

                Valkyrie.m_instance = __instance;
                __instance.m_nview = __instance.GetComponent<ZNetView>();
                __instance.m_animator = __instance.GetComponentInChildren<Animator>();
                if (!__instance.m_nview.IsOwner())
                {
                    __instance.enabled = false;
                    return false;
                }

                __instance.m_startPause = 2f;
                __instance.m_startAltitude = 30f;
                __instance.m_textDuration = 0f;
                __instance.m_descentAltitude = 150f;
                __instance.m_attachOffset = new Vector3(-0.1f, 1.5f, 0.1f);

                __instance.m_targetPoint = taxiTargetPosition + new Vector3(0f, __instance.m_dropHeight, 0f);

                Vector3 position = isTravelingPlayer.transform.position;
                position.y += __instance.m_startAltitude;

                float flyDistance = Vector3.Distance(__instance.m_targetPoint, position);

                __instance.m_startDistance = flyDistance;

                __instance.m_startDescentDistance = Math.Min(200f, flyDistance / 5);

                __instance.m_speed = Math.Max(Math.Min(flyDistance / 90f, Math.Min(30f, maxTaxiSpeed.Value)), 10f);  // 30 max 10 min inbetween depends on distance target time 90 sec

                if (__instance.m_speed <= 15)
                    EnvMan.instance.m_introEnvironment = EnvMan.instance.m_currentEnv.m_name;
                else
                    EnvMan.instance.m_introEnvironment = "ThunderStorm";

                isTravelingPlayer.m_intro = true;

                __instance.transform.position = position;

                float landDistance = Utils.DistanceXZ(__instance.m_targetPoint, __instance.transform.position);
                float descentPathPart = Math.Max(__instance.m_descentAltitude / landDistance, Math.Min(__instance.m_descentAltitude * 2 / landDistance, 0.2f));
                __instance.m_descentStart = Vector3.Lerp(__instance.m_targetPoint, __instance.transform.position, descentPathPart);
                __instance.m_descentStart.y = __instance.m_descentAltitude;
                Vector3 a2 = __instance.m_targetPoint - __instance.m_descentStart;
                a2.y = 0f;
                a2.Normalize();
                __instance.m_flyAwayPoint = __instance.m_targetPoint + a2 * __instance.m_startDescentDistance;
                __instance.m_flyAwayPoint.y += 100f;
                __instance.SyncPlayer(doNetworkSync: true);

                LogInfo("Setting up valkyrie " + __instance.transform.position.ToString() + "   " + ZNet.instance.GetReferencePosition().ToString());

                return false;
            }
        }

        [HarmonyPatch(typeof(Valkyrie), nameof(Valkyrie.LateUpdate))]
        public static class Valkyrie_LateUpdate_Taxi
        {
            private static void Prefix(Valkyrie __instance)
            {
                if (!modEnabled.Value) return;

                if (!offeringTaxi.Value) return;

                if (ZInput.GetButton("Use") && ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyUse") && ZInput.GetButton("JoyAltPlace"))
                {
                    __instance.DropPlayer();
                }
            }
        }

        [HarmonyPatch(typeof(Valkyrie), nameof(Valkyrie.DropPlayer))]
        public static class Valkyrie_DropPlayer_Taxi
        {
            private static void Postfix(Valkyrie __instance)
            {
                if (!modEnabled.Value) return;

                if (!offeringTaxi.Value) return;

                playerDropped = true;
                if (!Player.m_localPlayer.m_seman.HaveStatusEffect("SlowFall"))
                {
                    castSlowFall = true;
                    Player.m_localPlayer.m_seman.AddStatusEffect("SlowFall".GetStableHashCode());
                    LogInfo("Cast slow fall");
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        public static class Player_Update_Taxi
        {
            private static void Postfix(Player __instance)
            {
                if (!modEnabled.Value)
                    return;

                if (!offeringTaxi.Value) return;

                if (Player.m_localPlayer != __instance)
                    return;

                if (playerDropped && castSlowFall && __instance.IsOnGround())
                {
                    castSlowFall = false;
                    playerDropped = false;
                    if (__instance.m_seman.HaveStatusEffect("SlowFall"))
                        __instance.m_seman.RemoveStatusEffect("SlowFall".GetStableHashCode(), true);
                    LogInfo("Remove slow fall");
                }
            }
        }

        [HarmonyPatch(typeof(Valkyrie), nameof(Valkyrie.OnDestroy))]
        public static class Valkyrie_OnDestroy_Taxi
        {
            private static void Postfix()
            {
                if (!modEnabled.Value) return;
                canTravel = true;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        public static class Character_Damage_DamageMultipliers
        {
            private static void Prefix(Character __instance, ref HitData hit, ZNetView ___m_nview)
            {
                if (!modEnabled.Value) return;

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
                    if (boarsHensProtection.Value && (__instance.m_group == "boar" || __instance.m_group == "chicken"))
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
                else if (__instance.GetComponent<Piece>() != null)
                {
                    if (hit.HaveAttacker() && hit.GetAttacker().IsBoss())
                        return;

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

                if (!hit.HaveAttacker()) return;

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
