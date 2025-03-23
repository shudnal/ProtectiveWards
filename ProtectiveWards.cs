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

namespace ProtectiveWards
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class ProtectiveWards : BaseUnityPlugin
    {
        public const string pluginID = "shudnal.ProtectiveWards";
        public const string pluginName = "Protective Wards";
        public const string pluginVersion = "1.2.6";

        private static Harmony _harmony;

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> configLocked;

        public static ConfigEntry<bool> disableFlash;
        public static ConfigEntry<bool> showAreaMarker;
        public static ConfigEntry<bool> loggingEnabled;
        public static ConfigEntry<int> refreshingTime;
        public static ConfigEntry<bool> showOfferingsInHover;
        public static ConfigEntry<float> showOfferingsInHoverAfterSeconds;
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

        public static ConfigEntry<int> offeringTaxiPriceHaldorUndiscovered;
        public static ConfigEntry<int> offeringTaxiPriceHaldorDiscovered;
        public static ConfigEntry<string> offeringTaxiPriceHildirItem;
        public static ConfigEntry<string> offeringTaxiPriceBogWitchItem;
        public static ConfigEntry<int> offeringTaxiPriceBogWitchAmount;
        public static ConfigEntry<int> offeringTaxiSecondsToFlyBack;

        public static ConfigEntry<bool> wardPassiveRepair;
        public static ConfigEntry<bool> wardPassiveRepairNonPlayer;
        public static ConfigEntry<bool> wardPassiveRepairRequireStation;
        public static ConfigEntry<int> autoCloseDoorsTime;

        public static ConfigEntry<string> wardPrefabNameToChangeRange;
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
        public static ConfigEntry<float> wardBubbleGlossiness;
        public static ConfigEntry<float> wardBubbleMetallic;
        public static ConfigEntry<float> wardBubbleNormalScale;
        public static ConfigEntry<float> wardBubbleDepthFade;

        public static ConfigEntry<bool> wardAreaMarkerPatch;
        public static ConfigEntry<float> wardAreaMarkerSpeed;
        public static ConfigEntry<Color> wardAreaMarkerStartColor;
        public static ConfigEntry<Color> wardAreaMarkerEndColor;
        public static ConfigEntry<float> wardAreaMarkerLength;
        public static ConfigEntry<float> wardAreaMarkerWidth;
        public static ConfigEntry<float> wardAreaMarkerAmount;

        public static ConfigEntry<bool> wardEmissionColorEnabled;
        public static ConfigEntry<Color> wardEmissionColor;
        public static ConfigEntry<float> wardEmissionColorMultiplier;
        public static ConfigEntry<bool> wardLightColorEnabled;

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

        public static readonly int s_customRange = "ward_useRange".GetStableHashCode();
        public static readonly int s_range = "ward_range".GetStableHashCode();

        public static readonly int s_customColor = "ward_useColor".GetStableHashCode();
        public static readonly int s_color = "ward_color".GetStableHashCode();

        public static readonly int s_circleEnabled = "circle_enabled".GetStableHashCode();
        public static readonly int s_circleStartColor = "circle_startColor".GetStableHashCode();
        public static readonly int s_circleEndColor = "circle_endColor".GetStableHashCode();
        public static readonly int s_circleSpeed = "circle_speed".GetStableHashCode();
        public static readonly int s_circleLength = "circle_length".GetStableHashCode();
        public static readonly int s_circleWidth = "circle_width".GetStableHashCode();
        public static readonly int s_circleAmount = "circle_amount".GetStableHashCode();

        public static readonly int s_bubbleEnabled = "bubble_enabled".GetStableHashCode();
        public static readonly int s_bubbleColor = "bubble_color".GetStableHashCode();
        public static readonly int s_bubbleColorAlpha = "bubble_color_alpha".GetStableHashCode();
        public static readonly int s_bubbleWaveVel = "bubble_wave".GetStableHashCode();
        public static readonly int s_bubbleRefractionIntensity = "bubble_refraction".GetStableHashCode();
        public static readonly int s_bubbleGlossiness = "bubble_glossiness".GetStableHashCode();
        public static readonly int s_bubbleMetallic = "bubble_metallic".GetStableHashCode();
        public static readonly int s_bubbleNormalScale = "bubble_normalscale".GetStableHashCode();
        public static readonly int s_bubbleDepthFade = "bubble_depthfade".GetStableHashCode();

        private static readonly MaterialPropertyBlock s_matBlock = new MaterialPropertyBlock();

        private static float offeringsTimer = 0f;

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

            LocalizationManager.Localizer.Load();
        }

        private void FixedUpdate()
        {
            if (offeringsTimer > 0f)
                offeringsTimer -= Time.fixedDeltaTime;
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
            showOfferingsInHoverAfterSeconds = config("Misc", "Show offerings in hover after seconds", defaultValue: 10f, "Show offerings list after set amount of seconds. [Not Synced with Server]", false);
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

            offeringTaxi = config("Offerings", "10 - Fly back and forth to distant point by different items offering", defaultValue: true, "Offer boss trophy to travel to Sacrificial Stones (initial spawn point). Boss trophy will NOT be consumed." +
                                                                                                                                   "\nOffer coins to travel to Haldor (x2000 if you didn't find him yet. x500 otherwise). Coins will be consumed." +
                                                                                                                                   "\nOffer Hildir's chest to travel to Hildir for free. Chest will NOT be consumed. Totem WILL be consumed." +
                                                                                                                                   "\nOffer Fuling totem to travel to Hildir. Totem WILL be consumed.");

            offeringTaxiPriceHaldorUndiscovered = config("Offerings - Taxi", "Price to undiscovered Haldor", defaultValue: 2000, "Coins amount that must be paid to discover and travel to nearest Haldor.");
            offeringTaxiPriceHaldorDiscovered = config("Offerings - Taxi", "Price to discovered Haldor", defaultValue: 500, "Coins amount that must be paid to travel to already discovered Haldor.");
            offeringTaxiPriceHildirItem = config("Offerings - Taxi", "Item to travel to Hildir", defaultValue: "$item_goblintotem", "An item that must be paid to travel to Hildir.");
            offeringTaxiPriceBogWitchItem = config("Offerings - Taxi", "Item to travel to Bog Witch", defaultValue: "$item_pukeberries", "An item that must be paid to travel to Bog Witch.");
            offeringTaxiPriceBogWitchAmount = config("Offerings - Taxi", "Item to travel to Bog Witch amount", defaultValue: 20, "An amount of items that must be paid to travel to Bog Witch.");
            offeringTaxiSecondsToFlyBack = config("Offerings - Taxi", "Seconds to fly back", defaultValue: 120, "An amount of seconds you have to make business with trader before Valkyrie will bring you back.");

            wardPassiveRepair = config("Passive", "Activatable passive repair", defaultValue: true, "Interact with a ward to start passive repair process of all pieces in all connected areas" +
                                                                                                      "\nWard will repair one piece every 10 seconds until all pieces are healthy. Then the process will stop.");
            wardPassiveRepairNonPlayer = config("Passive", "Passive repair non player structures", defaultValue: false, "If enabled - ward will repair structures from locations not initially placed by players. Like ruins, dverger outposts, infested mines and so on.");
            wardPassiveRepairRequireStation = config("Passive", "Passive repair requires crafting station", defaultValue: false, "If enabled - piece can be repaired only if there is corresponding crafting station near the ward.");

            autoCloseDoorsTime = config("Passive", "Auto close doors after", defaultValue: 0, "Automatically close doors after a specified number of seconds. 0 to disable. 5 recommended");


            wardPrefabNameToChangeRange = config("Range", "Ward prefab names to control range", defaultValue: "guard_stone", "Prefab name of ward to control range in case.");
            setWardRange = config("Range", "Change Ward range", defaultValue: false, "Change ward range.");
            wardRange = config("Range", "Ward range", defaultValue: 10f, "Ward range. Toggle ward protection for changes to take effect");
            supressSpawnInRange = config("Range", "Supress spawn in ward area", defaultValue: true, "Vanilla behavior is true. Set false if you want creatures and raids spawn in ward radius. Toggle ward protection for changes to take effect");
            permitEveryone = config("Range", "Grant permittance to everyone", defaultValue: false, "Grant permittance to every player. There still will be permittance list on ward but it won't take any effect.");


            wardBubbleShow = config("Ward Bubble", "Show bubble", defaultValue: false, "Show ward bubble like trader's one [Not Synced with Server]", false);
            wardBubbleColor = config("Ward Bubble", "Bubble color", defaultValue: Color.black, "Bubble color. Toggle ward protection to change color [Not Synced with Server]", false);
            wardBubbleRefractionIntensity = config("Ward Bubble", "Refraction intensity", defaultValue: 0.005f, "Intensity of light refraction caused by bubble. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleWaveIntensity = config("Ward Bubble", "Wave intensity", defaultValue: 40f, "Bubble light distortion speed. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleGlossiness = config("Ward Bubble", "Glossiness", defaultValue: 0f, "Bubble glossiness. 1 to soap bubble effect. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleMetallic = config("Ward Bubble", "Metallic", defaultValue: 1f, "Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleNormalScale = config("Ward Bubble", "Normal scale", defaultValue: 15f, "Density of distortion effect. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleDepthFade = config("Ward Bubble", "Depth fade", defaultValue: 1f, "Toggle ward protection for changes to take effect [Not Synced with Server]", false);

            wardDemisterEnabled = config("Ward Demister", "Enable demister", defaultValue: false, "Ward will push out the mist");


            wardAreaMarkerPatch = config("Ward Circle", "Patch circle", defaultValue: false, "Change area marker circle projector parameters. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardAreaMarkerSpeed = config("Ward Circle", "Speed", defaultValue: 0.1f, "Speed of lines movement. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardAreaMarkerStartColor = config("Ward Circle", "Color start", defaultValue: new Color(0.8f, 0.8f, 0.8f), "Starting color. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardAreaMarkerEndColor = config("Ward Circle", "Color end", defaultValue: Color.clear, "End color (if set color of lines will change gradually). Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardAreaMarkerLength = config("Ward Circle", "Length multiplier", defaultValue: 1.0f, "Change area marker circle projector parameters. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardAreaMarkerWidth = config("Ward Circle", "Width multiplier", defaultValue: 1.0f, "Change area marker circle projector parameters. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardAreaMarkerAmount = config("Ward Circle", "Lines amount multiplier", defaultValue: 1.0f, "Change area marker circle projector parameters. Toggle ward protection for changes to take effect [Not Synced with Server]", false);


            boarsHensProtection = config("Ward protects", "Boars and hens from damage", true, "Set whether an active Ward will protect nearby boars and hens from taken damage (players excluded)");
            wardRainProtection = config("Ward protects", "Structures from rain damage", true, "Set whether an active Ward will protect nearby structures from rain and water damage");
            wardShipProtection = config("Ward protects", "Ship from damage", ShipDamageType.WaterDamage, "Set whether an active Ward will protect nearby ships from damage (waves and upsidedown for water damage option or any structural damage)");
            wardPlantProtection = config("Ward protects", "Plants from any damage", true, "Set whether an active Ward will protect nearby plants from taking damage");
            fireplaceProtection = config("Ward protects", "Fireplace from step damage", true, "Set whether an active Ward will protect nearby fire sources from taking damage from stepping on them");
            wardTrapProtection = config("Ward protects", "Players from their traps", true, "Set whether an active Ward will protect players from stepping on traps");
            sittingRaidProtection = config("Ward protects", "Players from raids when sitting on something near the fire (not floor)", true, "Set whether an active Ward will protect nearby players from raids when sitting next to an active fire"
                                                                                                                                           + "\nDo you want to go AFK in your base? Find a warm chair, bench, stool, throne whatever to sit on and go"
                                                                                                                                           + "\nIf the fire does not burn - you are vulnerable");


            wardEmissionColorEnabled = config("Ward Color", "Change emission color", defaultValue: false, "Change ward emission color. World restart required to apply changes. [Not Synced with Server]", false);
            wardEmissionColor = config("Ward Color", "Color", defaultValue: new Color(0.967f, 0.508f, 0.092f), "Ward color. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardEmissionColorMultiplier = config("Ward Color", "Color multiplier", defaultValue: 2f, "Ward color multiplier. Makes color more bright and intense. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardLightColorEnabled = config("Ward Color", "Change flare and light accordingly", defaultValue: true, "Flare and emitted light color will fit ward color. Toggle ward protection for changes to take effect [Not Synced with Server]", false);


            wardPlantProtectionList = config("Ward protects", "Plants from list", "$item_carrot, $item_turnip, $item_onion, $item_carrotseeds, $item_turnipseeds, $item_onionseeds, $item_jotunpuffs, $item_magecap", "List of plants to be protected from damage");
            boarsHensProtectionGroupList = config("Ward protects", "Boars and hens from list", "boar, chicken", "List of tamed groups to be protected from damage");

            wardPlantProtectionList.SettingChanged += (sender, args) => FillWardProtectionLists();
            boarsHensProtectionGroupList.SettingChanged += (sender, args) => FillWardProtectionLists();

            FillWardProtectionLists();
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

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

        public static bool InsideEnabledPlayersArea(Vector3 point, bool checkCache = false) => InsideEnabledPlayersArea(point, out _, checkCache);

        public static bool InsideEnabledPlayersArea(Vector3 point, out PrivateArea area, bool checkCache = false)
        {
            area = null;
            if (checkCache)
            {
                UpdateCache();

                if (areaCache.TryGetValue(point, out area))
                {
                    if (area && area.isActiveAndEnabled && area.IsEnabled())
                        return true;

                    areaCache.Remove(point);
                }
            }

            foreach (PrivateArea allArea in PrivateArea.m_allAreas)
                if (allArea.IsEnabled() && allArea.m_ownerFaction == Character.Faction.Players && allArea.IsInside(point, 0f))
                {
                    area = allArea;

                    if (checkCache)
                        areaCache.Add(point, area);

                    return true;
                }

            if (checkCache)
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

        private static void SetWardRange(PrivateArea __instance, float range)
        {
            float newRadius = Math.Max(range, 0);

            __instance.m_radius = newRadius;
            
            __instance.m_areaMarker.m_radius = newRadius;
            __instance.m_areaMarker.m_nrOfSegments = (int)(80 * (wardAreaMarkerPatch.Value ? wardAreaMarkerAmount.Value : 1f) * (newRadius / 32f));

            ApplyRangeEffect(__instance, EffectArea.Type.PlayerBase, newRadius);
        }

        private static void SetWardPlayerBase(PrivateArea __instance, float range)
        {
            Transform playerBase = __instance.transform.Find("PlayerBase");
            if (playerBase != null)
                playerBase.localScale = supressSpawnInRange.Value ? Vector3.one : Vector3.one * Math.Min(1f, 10f / Math.Max(range, 1f));
        }

        private static void FillWardProtectionLists()
        {
            _wardPlantProtectionList = new HashSet<string>(wardPlantProtectionList.Value.Split(',').Select(p => p.Trim().ToLower()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList());
            _boarsHensProtectionGroupList = new HashSet<string>(boarsHensProtectionGroupList.Value.Split(',').Select(p => p.Trim().ToLower()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList());
        }

        private static bool IsCraftingStationNear(Piece piece, Vector3 position)
        {
            return !wardPassiveRepairRequireStation.Value 
                || piece.m_craftingStation == null 
                || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench) 
                || CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, position);
        }

        private static bool CanBeRepaired(Piece piece, Vector3 position)
        {
            return (piece.IsPlacedByPlayer() ? IsCraftingStationNear(piece, position) : wardPassiveRepairNonPlayer.Value)
                 && piece.TryGetComponent(out WearNTear WNT) && WNT.GetHealthPercentage() < 1.0f;
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

                ConnectedAreas(ward).Do(area => Piece.GetAllPiecesInRadius(area.transform.position, area.m_radius, pieces));

                HashSet<Piece> piecesToRepair = pieces.Where(piece => CanBeRepaired(piece, ward.transform.position)).ToHashSet();

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

        public static IEnumerable<PrivateArea> ConnectedAreas(PrivateArea ward)
        {
            return PrivateArea.m_allAreas.Where(area => area.IsEnabled() && area.m_radius + ward.m_radius < Vector3.Distance(area.transform.position, ward.transform.position));
        }

        [HarmonyPatch(typeof(CircleProjector), nameof(CircleProjector.CreateSegments))]
        public static class CircleProjector_CreateSegments_InitState
        {
            public static void Prefix(CircleProjector __instance, ref bool __state)
            {
                if (!modEnabled.Value)
                    return;

                if (!wardAreaMarkerPatch.Value)
                    return;

                if (__instance.transform.root.GetComponent<PrivateArea>() == null)
                    return;

                __instance.m_nrOfSegments = (int)(80 * (wardAreaMarkerPatch.Value ? wardAreaMarkerAmount.Value : 1f) * (__instance.m_radius / 32f));

                __state = (!__instance.m_sliceLines && __instance.m_segments.Count == __instance.m_nrOfSegments) || (__instance.m_sliceLines && __instance.m_calcStart == __instance.m_start && __instance.m_calcTurns == __instance.m_turns);
            }

            public static void Postfix(CircleProjector __instance, bool __state)
            {
                if (__state)
                    return;

                ZNetView m_nview = __instance.GetComponentInParent<ZNetView>();
                if (!m_nview || !m_nview.IsValid())
                    return;

                ZDO zdo = m_nview.GetZDO();
                if (zdo == null)
                    return;

                InitCircleProjectorState(__instance, m_nview);
            }
        }

        private static void InitEmissionColor(PrivateArea ward)
        {
            if (!wardEmissionColorEnabled.Value || !ward.m_model)
                return;

            ZDO zdo = ward.m_nview.GetZDO();
            if (!zdo.GetBool(s_customColor))
                return;

            int materialIndex = -1;
            for (int i = 0; i < ward.m_model.sharedMaterials.Length; i++)
            {
                if (ward.m_model.sharedMaterials[i].name.StartsWith("Guardstone_OdenGlow_mat"))
                {
                    materialIndex = i;
                    break;
                }
            }

            if (materialIndex == -1)
                return;

            if (!zdo.GetVec3(s_color, out Vector3 vector))
                return;

            Color color = new Color(vector.x, vector.y, vector.z);

            ward.m_model.GetPropertyBlock(s_matBlock, materialIndex);
            s_matBlock.SetColor("_EmissionColor", color);
            ward.m_model.SetPropertyBlock(s_matBlock, materialIndex);

            if (wardLightColorEnabled.Value && ward.m_enabledEffect)
            {
                float multiplier = wardEmissionColorMultiplier.Value == 0f ? 1f : wardEmissionColorMultiplier.Value;

                foreach (ParticleSystem ps in ward.m_enabledEffect.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.startColor = new Color(color.r / multiplier, color.g / multiplier, color.b / multiplier, main.startColor.color.a);
                }

                Light light = ward.m_enabledEffect.GetComponentInChildren<Light>();
                if (light)
                    light.color = Color.Lerp(new Color(0.99f, 0.87f, 0.76f), new Color(color.r / multiplier, color.g / multiplier, color.b / multiplier), 0.5f);
            }
        }

        public static void InitCircleProjectorState(CircleProjector marker, ZNetView nview)
        {
            if (!wardAreaMarkerPatch.Value)
                return;

            ZDO zdo = nview.GetZDO();
            if (!zdo.GetBool(s_circleEnabled))
                return;

            string start = zdo.GetString(s_circleStartColor, "");
            Color startColor = wardAreaMarkerStartColor.Value;
            if (!start.IsNullOrWhiteSpace() && ColorUtility.TryParseHtmlString(start, out Color color))
                startColor = color;

            string end = zdo.GetString(s_circleEndColor, "");
            Color endColor = wardAreaMarkerEndColor.Value;
            if (!end.IsNullOrWhiteSpace() && ColorUtility.TryParseHtmlString(end, out Color color1))
                endColor = color1;

            var gradient = new Gradient();
            if (endColor != Color.clear)
            {
                gradient.SetKeys(new GradientColorKey[4]
                                    {
                                        new GradientColorKey(startColor, 0.0f),
                                        new GradientColorKey(endColor, 0.45f),
                                        new GradientColorKey(endColor, 0.55f),
                                        new GradientColorKey(startColor, 1.0f)
                                    }, 
                                 Array.Empty<GradientAlphaKey>());
            }

            marker.m_speed = zdo.GetFloat(s_circleSpeed, wardAreaMarkerSpeed.Value);

            for (int i = 0; i < marker.m_segments.Count; i++)
            {
                GameObject segment = marker.m_segments[i];
                
                segment.transform.localScale = new Vector3(marker.m_prefab.transform.localScale.x * zdo.GetFloat(s_circleWidth, wardAreaMarkerWidth.Value), marker.m_prefab.transform.localScale.y, marker.m_prefab.transform.localScale.z * zdo.GetFloat(s_circleLength, wardAreaMarkerLength.Value));

                Renderer renderer = segment.GetComponent<MeshRenderer>();
                renderer.GetPropertyBlock(s_matBlock);
                s_matBlock.SetColor("_Color", endColor == Color.clear ? startColor : gradient.Evaluate((float)i / marker.m_segments.Count));
                renderer.SetPropertyBlock(s_matBlock);
            }
        }

        public static void InitBubbleState(PrivateArea ward, GameObject bubble, ZNetView m_nview)
        {
            if (bubble == null)
                return;

            if (!m_nview.IsValid())
            {
                bubble.SetActive(false);
                return;
            }

            ZDO zdo = m_nview.GetZDO();

            bubble.SetActive(zdo.GetBool(s_bubbleEnabled, wardBubbleShow.Value) && ward.IsEnabled());

            bubble.transform.localScale = Vector3.one * ward.m_radius * 2f;

            Transform noMonsterArea = bubble.transform.Find("NoMonsterArea");
            if (noMonsterArea != null)
                Destroy(noMonsterArea.gameObject);

            MeshRenderer renderer = bubble.GetComponent<MeshRenderer>();

            Vector3 vecColor = zdo.GetVec3(s_bubbleColor, new Vector3(wardBubbleColor.Value.r, wardBubbleColor.Value.g, wardBubbleColor.Value.b));
            Color bubbleColor = new Color(vecColor.x, vecColor.y, vecColor.z, zdo.GetFloat(s_bubbleColorAlpha, 0f));

            renderer.GetPropertyBlock(s_matBlock);

            s_matBlock.SetColor("_Color", bubbleColor);
            s_matBlock.SetFloat("_RefractionIntensity", zdo.GetFloat(s_bubbleRefractionIntensity, wardBubbleRefractionIntensity.Value));
            s_matBlock.SetFloat("_WaveVel", zdo.GetFloat(s_bubbleWaveVel, wardBubbleWaveIntensity.Value));
            s_matBlock.SetFloat("_Glossiness", zdo.GetFloat(s_bubbleGlossiness, wardBubbleGlossiness.Value));
            s_matBlock.SetFloat("_Metallic", zdo.GetFloat(s_bubbleMetallic, wardBubbleMetallic.Value));
            s_matBlock.SetFloat("_NormalScale", zdo.GetFloat(s_bubbleNormalScale, wardBubbleNormalScale.Value));
            s_matBlock.SetFloat("_DepthFade", zdo.GetFloat(s_bubbleDepthFade, wardBubbleDepthFade.Value));

            renderer.SetPropertyBlock(s_matBlock);
        }

        public static void InitDemisterState(PrivateArea ward, GameObject demister, ZNetView m_nview)
        {
            if (demister == null)
                return;

            if (m_nview == null && !m_nview.IsValid())
            {
                demister.SetActive(false);
                return;
            }

            demister.SetActive(wardDemisterEnabled.Value && ward.IsEnabled());
            demister.GetComponent<ParticleSystemForceField>().endRange = ward.m_radius;
        }

        [HarmonyPatch(typeof(Door), nameof(Door.Interact))]
        public static class Door_SetState_AutoClose
        {
            public static void Postfix(Door __instance, ZNetView ___m_nview, bool __result)
            {
                if (!modEnabled.Value)
                    return;

                if (autoCloseDoorsTime.Value == 0)
                    return;

                if (!___m_nview.IsValid())
                    return;

                if (!__result)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, out PrivateArea ward, checkCache: true))
                    return;

                if (!doorsToClose.TryGetValue(ward, out List<Door> doors))
                    doors = new List<Door>();

                int state = ___m_nview.GetZDO().GetInt(ZDOVars.s_state);

                if (state == 0)
                    doors.Remove(__instance);
                else if (!doors.Contains(__instance))
                    doors.Add(__instance);

                if (doors.Count == 0)
                {
                    wardIsClosing.Remove(ward);
                    doorsToClose.Remove(ward);
                    return;
                }

                if (state == 0)
                    return;

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
                if (!modEnabled.Value)
                    return;

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
            public static void Prefix(PrivateArea __instance, StringBuilder text)
            {
                if (!modEnabled.Value)
                    return;

                if (!__instance.HaveLocalAccess())
                    return;

                bool wardEnabled = __instance.IsEnabled();
                if (!wardEnabled && !__instance.m_piece.IsCreator())
                    return;

                if (!wardEnabled)
                {
                    if (!ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive())
                        text.Append($"\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $pw_ward_apply_settings");
                    else
                        text.Append($"\n[<color=yellow><b>$KEY_JoyAltKeys + $KEY_Use</b></color>] $pw_ward_apply_settings");
                    return;
                }

                List<string> status = new List<string>();

                if (wardIsRepairing.TryGetValue(__instance, out int piecesToRepair))
                {
                    status.Add($"$hud_repair {piecesToRepair}");
                }
                else if (wardPassiveRepair.Value)
                {
                    if (!ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive())
                        text.Append($"\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $pw_ward_start_repair");
                    else
                        text.Append($"\n[<color=yellow><b>$KEY_JoyAltKeys + $KEY_Use</b></color>] $pw_ward_start_repair");
                }

                if (wardIsHealing.TryGetValue(__instance, out int secondsLeft))
                    status.Add($"$item_food_regen {TimeSpan.FromSeconds(secondsLeft).ToString(@"m\:ss")}");

                if (status.Count > 0)
                {
                    text.Append("\n$guardianstone_hook_power_activate: ");
                    text.Append(String.Join(", ", status.ToArray()));
                }

                if (offeringsTimer < showOfferingsInHoverAfterSeconds.Value + 0.5f)
                    offeringsTimer += Time.fixedDeltaTime * 2f;

                if (showOfferingsInHover.Value && offeringsTimer > showOfferingsInHoverAfterSeconds.Value)
                {
                    List<string> offeringsList = new List<string>();

                    if (offeringActiveRepair.Value && (Player.m_localPlayer.IsMaterialKnown("$item_surtlingcore") || Player.m_localPlayer.NoCostCheat()))
                        offeringsList.Add("$item_surtlingcore - $pw_ward_offering_surtlingcore_description");
                    if (offeringAugmenting.Value && (Player.m_localPlayer.IsMaterialKnown("$item_blackcore") || Player.m_localPlayer.NoCostCheat()))
                        offeringsList.Add("$item_blackcore - $pw_ward_offering_blackcore_description");
                    if (offeringFood.Value)
                        offeringsList.Add("$item_food - $pw_ward_offering_food_description");
                    if (offeringMead.Value)
                        offeringsList.Add("$se_mead_name - $pw_ward_offering_mead_description");
                    if (offeringThundertone.Value && (Player.m_localPlayer.IsMaterialKnown("$item_thunderstone") || Player.m_localPlayer.NoCostCheat()))
                        offeringsList.Add("$item_thunderstone - $pw_ward_offering_thunderstone_description");
                    if (offeringTrophy.Value)
                    {
                        offeringsList.Add("$inventory_trophies - $pw_ward_offering_trophies_description");
                        offeringsList.Add("$pw_ward_offering_bosstrophies - $pw_ward_offering_bosstrophies_description");
                    }
                    if (offeringYmirRemains.Value && (Player.m_localPlayer.IsMaterialKnown("$item_ymirremains") || Player.m_localPlayer.NoCostCheat()))
                        offeringsList.Add("$item_ymirremains - $pw_ward_offering_ymirremains_description");
                    if (offeringEitr.Value && (Player.m_localPlayer.IsMaterialKnown("$item_eitr") || Player.m_localPlayer.NoCostCheat()))
                        offeringsList.Add("$item_eitr - $pw_ward_offering_eitr_description");
                    if (offeringDragonEgg.Value && (Player.m_localPlayer.IsMaterialKnown("$item_dragonegg") && ZoneSystem.instance.GetGlobalKey(GlobalKeys.defeated_dragon) || Player.m_localPlayer.NoCostCheat()))
                        offeringsList.Add("$item_dragonegg - $pw_ward_offering_dragonegg_description");
                    if (offeringTaxi.Value)
                    {
                        if (Player.m_localPlayer.IsMaterialKnown("$item_coins") || Player.m_localPlayer.NoCostCheat())
                        {
                            ZoneSystem.instance.tempIconList.Clear();
                            ZoneSystem.instance.GetLocationIcons(ZoneSystem.instance.tempIconList);
                            int price = ZoneSystem.instance.tempIconList.Any(icon => icon.Value == "Vendor_BlackForest") ? offeringTaxiPriceHaldorDiscovered.Value : offeringTaxiPriceHaldorUndiscovered.Value;
                            offeringsList.Add($"$item_coins: {price} - $pw_ward_offering_coins_description");
                        }

                        if (!offeringTaxiPriceHildirItem.Value.IsNullOrWhiteSpace() && (Player.m_localPlayer.IsMaterialKnown(offeringTaxiPriceHildirItem.Value) || Player.m_localPlayer.NoCostCheat()))
                            offeringsList.Add($"{offeringTaxiPriceHildirItem.Value} - $pw_ward_offering_hildiritem_description");
                        if (Player.m_localPlayer.IsMaterialKnown("$item_chest_hildir1") || Player.m_localPlayer.IsMaterialKnown("$item_chest_hildir2") || Player.m_localPlayer.IsMaterialKnown("$item_chest_hildir3") || Player.m_localPlayer.NoCostCheat())
                            offeringsList.Add("$pw_ward_offering_hildirchest - $pw_ward_offering_hildirchest_description");

                        if (!offeringTaxiPriceBogWitchItem.Value.IsNullOrWhiteSpace() && (Player.m_localPlayer.IsMaterialKnown(offeringTaxiPriceBogWitchItem.Value) || Player.m_localPlayer.NoCostCheat()))
                            offeringsList.Add($"{offeringTaxiPriceBogWitchItem.Value} {(offeringTaxiPriceBogWitchAmount.Value > 0 ? $"x{offeringTaxiPriceBogWitchAmount.Value}" : "")} - $pw_ward_offering_bogwitchitem_description");
                    }

                    if (offeringsList.Count > 0)
                    {
                        text.Append("\n[<color=yellow><b>1-8</b></color>] $piece_offerbowl_offeritem:\n");
                        text.Append(String.Join("\n", offeringsList));
                        text.Append('\n');
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
            private static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, bool alt, Character.Faction ___m_ownerFaction, ref bool __result)
            {
                if (!modEnabled.Value)
                    return true;

                if (hold)
                    return true;

                if (!alt)
                    return true;

                if (___m_ownerFaction != 0)
                    return true;

                if (!__instance.HaveLocalAccess())
                    return true;

                areaCache.Clear();

                if (__instance.IsEnabled() && wardPassiveRepair.Value)
                {
                    __result = true;

                    if (wardIsRepairing.ContainsKey(__instance))
                        return false;

                    LogInfo($"Passive repairing begins");
                    instance.StartCoroutine(PassiveRepairEffect(__instance, human as Player));
                    return false;
                }
                else if (!__instance.IsEnabled() && __instance.m_piece.IsCreator())
                {
                    ZDO zdo = __instance.m_nview.GetZDO();
                    if (zdo == null)
                        return false;

                    __result = true;

                    zdo.Set(s_bubbleEnabled, wardBubbleShow.Value);
                    if (wardBubbleShow.Value)
                    {
                        zdo.Set(s_bubbleRefractionIntensity, wardBubbleRefractionIntensity.Value);
                        zdo.Set(s_bubbleWaveVel, wardBubbleWaveIntensity.Value);
                        zdo.Set(s_bubbleColor, new Vector3(wardBubbleColor.Value.r, wardBubbleColor.Value.g, wardBubbleColor.Value.b));
                        zdo.Set(s_bubbleColorAlpha, wardBubbleColor.Value.a);

                        zdo.Set(s_bubbleGlossiness, wardBubbleGlossiness.Value);
                        zdo.Set(s_bubbleMetallic, wardBubbleMetallic.Value);
                        zdo.Set(s_bubbleNormalScale, wardBubbleNormalScale.Value);
                        zdo.Set(s_bubbleDepthFade, wardBubbleDepthFade.Value);
                    }

                    zdo.Set(s_customRange, setWardRange.Value);
                    if (setWardRange.Value)
                        zdo.Set(s_range, wardRange.Value);

                    zdo.Set(s_customColor, wardEmissionColorEnabled.Value);
                    if (wardEmissionColorEnabled.Value)
                        zdo.Set(s_color, new Vector3(wardEmissionColor.Value.r * wardEmissionColorMultiplier.Value, wardEmissionColor.Value.g * wardEmissionColorMultiplier.Value, wardEmissionColor.Value.b * wardEmissionColorMultiplier.Value));

                    zdo.Set(s_circleEnabled, wardAreaMarkerPatch.Value);
                    if (wardAreaMarkerPatch.Value)
                    {
                        zdo.Set(s_circleStartColor, ColorUtility.ToHtmlStringRGBA(wardAreaMarkerStartColor.Value));
                        zdo.Set(s_circleEndColor, ColorUtility.ToHtmlStringRGBA(wardAreaMarkerEndColor.Value));
                        zdo.Set(s_circleSpeed, wardAreaMarkerSpeed.Value);
                        zdo.Set(s_circleLength, wardAreaMarkerLength.Value);
                        zdo.Set(s_circleWidth, wardAreaMarkerWidth.Value);
                        zdo.Set(s_circleAmount, wardAreaMarkerAmount.Value);
                    }

                    __instance.m_addPermittedEffect.Create(__instance.transform.position, __instance.transform.rotation);

                    LogInfo($"Ward settings applied for {zdo}");

                    return false;
                }

                return true;
            }
        }

        private static bool IsWardToSetRange(PrivateArea ward)
        {
            if (!setWardRange.Value)
                return false;

            return wardPrefabNameToChangeRange.Value.Split(',').Any(name => name.Trim() == Utils.GetPrefabName(ward.name));
        }

        private static void PatchRange(PrivateArea ward)
        {
            if (ward.m_nview == null || !ward.m_nview.IsValid())
                return;

            ZDO zdo = ward.m_nview.GetZDO();

            if (!zdo.GetBool(s_customRange))
                return;

            float range = ward.m_nview.GetZDO().GetFloat(s_range, wardRange.Value);
            if (ward.m_radius != range && IsWardToSetRange(ward))
            {
                SetWardRange(ward, range);
                SetWardPlayerBase(ward, range);
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Awake))]
        public static class PrivateArea_Awake_SetWardRange
        {
            private static void Postfix(PrivateArea __instance, ZNetView ___m_nview)
            {
                if (!modEnabled.Value)
                    return;

                if (___m_nview == null || !___m_nview.IsValid())
                    return;

                PatchRange(__instance);

                if (showAreaMarker.Value)
                    __instance.m_areaMarker.gameObject.SetActive(value: true);

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

                InitEmissionColor(__instance);
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
                if (haldor == null || !haldor.m_prefab.IsValid)
                    return;

                haldor.m_prefab.Load();
                forceField = Instantiate(haldor.m_prefab.Asset.transform.Find(forceFieldName)?.gameObject);
                forceField.name = "ProtectiveWards_bubble";

                MeshRenderer fieldRenderer = forceField.GetComponent<MeshRenderer>();
                fieldRenderer.sharedMaterial = new Material(fieldRenderer.sharedMaterial);
                fieldRenderer.sharedMaterial.renderQueue++;

                haldor.m_prefab.Release();
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        public static class ZoneSystem_OnDestroy_DestroyWardBubble
        {
            private static void Postfix(ZoneSystem __instance)
            {
                UnityEngine.Object.Destroy(forceField);
                lightningAOE = null;
                preLightning = null;
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
                if (!modEnabled.Value)
                    return;

                if (___m_nview == null || !___m_nview.IsValid())
                    return;

                PatchRange(__instance);

                InitEmissionColor(__instance);

                InitBubbleState(__instance, __instance.transform.Find(forceFieldName)?.gameObject, ___m_nview);

                InitDemisterState(__instance, __instance.transform.Find(forceFieldDemisterName)?.gameObject, ___m_nview);

                InitCircleProjectorState(__instance.m_areaMarker, ___m_nview);
            }
        }
        
    }
}
