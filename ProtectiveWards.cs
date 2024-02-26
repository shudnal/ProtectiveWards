using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ServerSync;
using static UnityEngine.ParticleSystem;

namespace ProtectiveWards
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class ProtectiveWards : BaseUnityPlugin
    {
        const string pluginID = "shudnal.ProtectiveWards";
        const string pluginName = "Protective Wards";
        const string pluginVersion = "1.1.12";

        private Harmony _harmony;

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> configLocked;

        public static ConfigEntry<bool> disableFlash;
        public static ConfigEntry<bool> showAreaMarker;
        public static ConfigEntry<bool> loggingEnabled;
        public static ConfigEntry<int> refreshingTime;
        public static ConfigEntry<bool> showOfferingsInHover;
        public static ConfigEntry<float> maxTaxiSpeed;

        public static ConfigEntry<bool> offeringActiveRepair;
        public static ConfigEntry<bool> offeringAugmenting;
        public static ConfigEntry<bool> offeringFood;
        public static ConfigEntry<bool> offeringMead;
        public static ConfigEntry<bool> offeringThundertone;
        public static ConfigEntry<bool> offeringTrophy;
        public static ConfigEntry<bool> offeringYmirRemains;
        public static ConfigEntry<bool> offeringEitr;
        public static ConfigEntry<bool> offeringDragonEgg;
        public static ConfigEntry<bool> offeringTaxi;

        public static ConfigEntry<bool> wardPassiveRepair;
        public static ConfigEntry<int> autoCloseDoorsTime;

        public static ConfigEntry<bool> setWardRange;
        public static ConfigEntry<float> wardRange;
        public static ConfigEntry<bool> supressSpawnInRange;
        public static ConfigEntry<bool> permitEveryone;

        public static ConfigEntry<float> playerDamageDealtMultiplier;
        public static ConfigEntry<float> playerDamageTakenMultiplier;
        public static ConfigEntry<float> tamedDamageTakenMultiplier;
        public static ConfigEntry<float> structureDamageTakenMultiplier;
        public static ConfigEntry<float> fallDamageTakenMultiplier;
        public static ConfigEntry<float> turretFireRateMultiplier;

        public static ConfigEntry<float> foodDrainMultiplier;
        public static ConfigEntry<float> staminaDrainMultiplier;
        public static ConfigEntry<float> skillsDrainMultiplier;
        public static ConfigEntry<float> fireplaceDrainMultiplier;
        public static ConfigEntry<float> hammerDurabilityDrainMultiplier;

        public static ConfigEntry<float> smeltingSpeedMultiplier;
        public static ConfigEntry<float> cookingSpeedMultiplier;
        public static ConfigEntry<float> fermentingSpeedMultiplier;
        public static ConfigEntry<float> sapCollectingSpeedMultiplier;

        public static ConfigEntry<bool> wardBubbleShow;
        public static ConfigEntry<Color> wardBubbleColor;
        public static ConfigEntry<float> wardBubbleRefractionIntensity;
        public static ConfigEntry<float> wardBubbleWaveIntensity;
        
        public static ConfigEntry<bool> wardDemisterEnabled;

        public static ConfigEntry<bool> boarsHensProtection;
        public static ConfigEntry<bool> wardRainProtection;
        public static ConfigEntry<ShipDamageType> wardShipProtection;
        public static ConfigEntry<bool> wardPlantProtection;
        public static ConfigEntry<bool> fireplaceProtection;
        public static ConfigEntry<bool> sittingRaidProtection;
        public static ConfigEntry<bool> wardTrapProtection;
        public static ConfigEntry<string> wardPlantProtectionList;
        public static ConfigEntry<string> boarsHensProtectionGroupList;

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
        
        internal static GameObject forceField;
        internal const string forceFieldName = "ForceField";
        internal static GameObject forceFieldDemister;
        internal const string forceFieldDemisterName = "Particle System Force Field";

        internal static bool taxiReturnBack = false;
        internal static Vector3 taxiPlayerPositionToReturn;
        internal static Vector3 taxiTargetPosition;
        internal static bool canTravel = true;
        internal static Player isTravelingPlayer;
        internal static bool playerDropped = false;
        internal static bool castSlowFall;

        internal static HashSet<string> _wardPlantProtectionList;
        internal static HashSet<string> _boarsHensProtectionGroupList;

        public static readonly int s_bubbleEnabled = "bubble_enabled".GetStableHashCode();
        public static readonly int s_bubbleColor = "bubble_color".GetStableHashCode();
        public static readonly int s_bubbleColorAlpha = "bubble_color_alpha".GetStableHashCode();
        public static readonly int s_bubbleWaveVel = "bubble_wave".GetStableHashCode();
        public static readonly int s_bubbleRefractionIntensity = "bubble_refraction".GetStableHashCode();

        private static readonly MaterialPropertyBlock s_matBlock = new MaterialPropertyBlock();

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


            wardBubbleShow = config("Ward Bubble", "Show bubble", defaultValue: false, "Show ward bubble like trader's one [Not Synced with Server]", false);
            wardBubbleColor = config("Ward Bubble", "Bubble color", defaultValue: Color.black, "Bubble color. Toggle ward protection to change color [Not Synced with Server]", false);
            wardBubbleRefractionIntensity = config("Ward Bubble", "Refraction intensity", defaultValue: 0.005f, "Intensity of light refraction caused by bubble. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleWaveIntensity = config("Ward Bubble", "Wave intensity", defaultValue: 40f, "Bubble light distortion speed. Toggle ward protection for changes to take effect [Not Synced with Server]", false);

            wardDemisterEnabled = config("Ward Demister", "Enable demister", defaultValue: false, "Ward will push out the mist");

            boarsHensProtection = config("Ward protects", "Boars and hens from damage", true, "Set whether an active Ward will protect nearby boars and hens from taken damage (players excluded)");
            wardRainProtection = config("Ward protects", "Structures from rain damage", true, "Set whether an active Ward will protect nearby structures from rain and water damage");
            wardShipProtection = config("Ward protects", "Ship from damage", ShipDamageType.WaterDamage, "Set whether an active Ward will protect nearby ships from damage (waves and upsidedown for water damage option or any structural damage)");
            wardPlantProtection = config("Ward protects", "Plants from any damage", true, "Set whether an active Ward will protect nearby plants from taking damage");
            fireplaceProtection = config("Ward protects", "Fireplace from step damage", true, "Set whether an active Ward will protect nearby fire sources from taking damage from stepping on them");
            wardTrapProtection = config("Ward protects", "Players from their traps", true, "Set whether an active Ward will protect players from stepping on traps");
            sittingRaidProtection = config("Ward protects", "Players from raids when sitting on something near the fire (not floor)", true, "Set whether an active Ward will protect nearby players from raids when sitting next to an active fire"
                                                                                                                                           + "\nDo you want to go AFK in your base? Find a warm chair, bench, stool, throne whatever to sit on and go"
                                                                                                                                           + "\nIf the fire does not burn - you are vulnerable");

            wardPlantProtectionList = config("Ward protects", "Plants from list", "$item_carrot, $item_turnip, $item_onion, $item_carrotseeds, $item_turnipseeds, $item_onionseeds, $item_jotunpuffs, $item_magecap", "List of plants to be protected from damage");
            boarsHensProtectionGroupList = config("Ward protects", "Boars and hens from list", "boar, chicken", "List of tamed groups to be protected from damage");

            wardPlantProtectionList.SettingChanged += (sender, args) => FillWardProtectionLists();
            boarsHensProtectionGroupList.SettingChanged += (sender, args) => FillWardProtectionLists();

            FillWardProtectionLists();
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

        private static void SetWardRange(PrivateArea __instance)
        {
            float newRadius = Math.Max(wardRange.Value, 0);

            __instance.m_radius = newRadius;
            __instance.m_areaMarker.m_radius = newRadius;
            ApplyRangeEffect(__instance, EffectArea.Type.PlayerBase, newRadius);
        }

        private static void SetWardPlayerBase(PrivateArea __instance)
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

        private static void FillWardProtectionLists()
        {
            _wardPlantProtectionList = new HashSet<string>(wardPlantProtectionList.Value.Split(',').Select(p => p.Trim().ToLower()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList());
            _boarsHensProtectionGroupList = new HashSet<string>(boarsHensProtectionGroupList.Value.Split(',').Select(p => p.Trim().ToLower()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList());
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

                        initiator?.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$piece_repair"));

                        break;
                    }
                }

                yield return new WaitForSecondsRealtime(10);
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

        public static List<PrivateArea> ConnectedAreas(PrivateArea ward)
        {
            List<PrivateArea> areas = ward.GetConnectedAreas();
            areas.Add(ward);
            return areas.Where(area => area.IsEnabled()).Distinct().ToList();
        }

        public static void InitBubbleState(PrivateArea ward, GameObject bubble, ZNetView m_nview)
        {
            if (bubble == null)
                return;

            ZDO zdo = m_nview.GetZDO();
            if (zdo == null)
            {
                bubble.SetActive(false);
                return;
            }

            bubble.SetActive(zdo.GetBool(s_bubbleEnabled, wardBubbleShow.Value) && ward.IsEnabled());

            bubble.transform.localScale = Vector3.one * wardRange.Value * 2f;

            Transform noMonsterArea = bubble.transform.Find("NoMonsterArea");
            if (noMonsterArea != null)
                Destroy(noMonsterArea.gameObject);

            MeshRenderer renderer = bubble.GetComponent<MeshRenderer>();

            Vector3 vecColor = zdo.GetVec3(s_bubbleColor, new Vector3(wardBubbleColor.Value.r, wardBubbleColor.Value.g, wardBubbleColor.Value.b));
            Color bubbleColor = new Color(vecColor.x, vecColor.y, vecColor.z, zdo.GetFloat(s_bubbleColorAlpha, 0f));

            s_matBlock.Clear();

            s_matBlock.SetColor("_Color", bubbleColor);
            s_matBlock.SetFloat("_RefractionIntensity", zdo.GetFloat(s_bubbleRefractionIntensity, wardBubbleRefractionIntensity.Value));
            s_matBlock.SetFloat("_WaveVel", zdo.GetFloat(s_bubbleWaveVel, wardBubbleWaveIntensity.Value));

            renderer.SetPropertyBlock(s_matBlock);
        }

        public static void InitDemisterState(PrivateArea ward, GameObject demister, ZNetView m_nview)
        {
            if (demister == null)
                return;

            ZDO zdo = m_nview.GetZDO();
            if (zdo == null)
            {
                demister.SetActive(false);
                return;
            }

            demister.SetActive(wardDemisterEnabled.Value && ward.IsEnabled());
            demister.GetComponent<ParticleSystemForceField>().endRange = wardRange.Value;
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

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.HideMarker))]
        public static class PrivateArea_HideMarker_ShowAreaMarker
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
            public static bool Prefix(ref bool __result)
            {
                if (!modEnabled.Value)
                    return true;

                if (!permitEveryone.Value)
                    return true;

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
        public static class PrivateArea_Interact_PassiveEffectWardRepair
        {
            private static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, bool alt, Character.Faction ___m_ownerFaction)
            {
                if (!modEnabled.Value)
                    return true;

                if (!alt)
                    areaCache.Clear();

                if (!wardPassiveRepair.Value)
                    return true;

                if (!alt)
                    return true;

                if (hold)
                    return true;

                if (___m_ownerFaction != 0)
                    return true;

                if (!__instance.IsEnabled())
                    return true;

                if (wardIsRepairing.ContainsKey(__instance))
                    return true;

                LogInfo($"Passive repairing begins");
                instance.StartCoroutine(PassiveRepairEffect(__instance, human as Player));

                return false;
            }

            private static void Postfix(PrivateArea __instance)
            {
                if (!modEnabled.Value) return;

                if (setWardRange.Value) 
                {
                    if (__instance.m_radius != wardRange.Value) 
                        SetWardRange(__instance);

                    SetWardPlayerBase(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Awake))]
        public static class PrivateArea_Awake_SetWardRange
        {
            private static void Prefix(PrivateArea __instance)
            {
                if (!modEnabled.Value)
                    return;

                if (setWardRange.Value)
                {
                    SetWardRange(__instance);

                    SetWardPlayerBase(__instance);
                }
            }

            private static void Postfix(PrivateArea __instance, ZNetView ___m_nview)
            {
                if (!modEnabled.Value)
                    return;

                if (setWardRange.Value)
                {
                    SetWardRange(__instance);
                    SetWardPlayerBase(__instance);
                }

                if (showAreaMarker.Value)
                    __instance.m_areaMarker.gameObject.SetActive(value: true);

                if (___m_nview == null || !___m_nview.IsValid())
                    return;

                if (forceField != null)
                {
                    GameObject bubble = Instantiate(forceField, __instance.transform);
                    bubble.name = forceFieldName;

                    InitBubbleState(__instance, bubble, ___m_nview);
                }

                if (forceFieldDemister != null)
                {
                    GameObject demister = Instantiate(forceFieldDemister, __instance.transform);
                    demister.name = forceFieldDemisterName;

                    InitDemisterState(__instance, demister, ___m_nview);
                }
                
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.RPC_FlashShield))]
        public static class PrivateArea_RPC_FlashShield_StopFlashShield
        {
            private static bool Prefix(PrivateArea __instance)
            {
                if (!modEnabled.Value)
                    return true;

                if (__instance.m_ownerFaction == Character.Faction.Players)
                    return !disableFlash.Value;

                return true;
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        public static class ZoneSystem_Start_InitWardBubble
        {
            private static void Postfix(ZoneSystem __instance)
            {
                ZoneSystem.ZoneLocation haldor = __instance.m_locations.Find(loc => loc.m_prefabName == "Vendor_BlackForest");
                if (haldor == null)
                    return;

                forceField = haldor.m_prefab.transform.Find(forceFieldName)?.gameObject;
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        internal static class ZNetScene_Awake_DemisterForceField
        {
            private static void Postfix(ZNetScene __instance)
            {
                forceFieldDemister = __instance.GetPrefab("Demister")?.transform.Find(forceFieldDemisterName)?.gameObject;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.RPC_ToggleEnabled))]
        public static class PrivateArea_RPC_ToggleEnabled_InitWardBubble
        {
            private static void Postfix(PrivateArea __instance, ZNetView ___m_nview, Piece ___m_piece)
            {
                ZDO zdo = ___m_nview.GetZDO();
                if (zdo == null)
                    return;

                if (___m_piece.IsCreator())
                {
                    zdo.Set(s_bubbleEnabled, wardBubbleShow.Value);
                    zdo.Set(s_bubbleRefractionIntensity, wardBubbleRefractionIntensity.Value);
                    zdo.Set(s_bubbleWaveVel, wardBubbleWaveIntensity.Value);
                    zdo.Set(s_bubbleColor, new Vector3(wardBubbleColor.Value.r, wardBubbleColor.Value.g, wardBubbleColor.Value.b));
                    zdo.Set(s_bubbleColorAlpha, wardBubbleColor.Value.a);
                }

                InitBubbleState(__instance, __instance.transform.Find(forceFieldName)?.gameObject, ___m_nview);

                InitDemisterState(__instance, __instance.transform.Find(forceFieldDemisterName)?.gameObject, ___m_nview);
            }
        }
        
    }
}
