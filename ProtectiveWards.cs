using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ProtectiveWards
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, BepInDependency.DependencyFlags.HardDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class ProtectiveWards : BaseUnityPlugin
    {
        public const string pluginID = "shudnal.ProtectiveWards";
        public const string pluginName = "Protective Wards";
        public const string pluginVersion = "2.0.2";

        private static Harmony _harmony;

        public static ConfigEntry<bool> disableFlash;
        public static ConfigEntry<bool> showAreaMarker;
        public static ConfigEntry<bool> loggingEnabled;
        public static ConfigEntry<int> refreshingTime;
        public static ConfigEntry<bool> showOfferingsInHover;
        public static ConfigEntry<float> showOfferingsInHoverAfterSeconds;
        public static ConfigEntry<float> maxTaxiSpeed;
        public static ConfigEntry<bool> addLightMovement;

        public static ConfigEntry<bool> wardSettingsUseDefaultsForAllWards;
        public static ConfigEntry<bool> wardSettingsRequireCreator;
        public static ConfigEntry<bool> wardSettingsAllowAdminEdit;

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
        public static ConfigEntry<bool> offeringProtectFromNonPermitted;

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
        public static ConfigEntry<string> autoCloseDoorsIgnorePrefabs;

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
        public static ConfigEntry<bool> wardAccessProtectChests;
        public static ConfigEntry<bool> wardAccessProtectDoors;
        public static ConfigEntry<bool> wardAccessProtectPlants;
        public static ConfigEntry<bool> wardAccessProtectBoats;
        public static ConfigEntry<bool> wardAccessProtectTames;
        public static ConfigEntry<bool> wardAccessProtectProductionStations;
        public static ConfigEntry<bool> wardAccessProtectItemStands;
        public static ConfigEntry<bool> wardAccessProtectCarts;
        public static ConfigEntry<WardPortalAccessMode> wardAccessProtectPortals;
        public static ConfigEntry<bool> wardAccessProtectFood;
        public static ConfigEntry<WardItemPickupMode> wardAccessProtectItemPickupMode;
        public static ConfigEntry<bool> wardAccessProtectMapTables;
        public static ConfigEntry<bool> wardAccessProtectFireplaces;
        public static ConfigEntry<bool> wardAccessProtectShieldGenerators;
        public static ConfigEntry<bool> wardAccessProtectIncinerators;
        public static ConfigEntry<bool> wardAccessProtectTurrets;
        public static ConfigEntry<bool> wardAccessProtectCraftingStations;
        public static ConfigEntry<bool> wardAccessProtectBeds;
        public static ConfigEntry<bool> wardAccessProtectCatapults;
        public static ConfigEntry<bool> wardAccessProtectArcheryTargets;
        public static ConfigEntry<bool> wardAccessProtectBarbers;
        public static ConfigEntry<bool> wardAccessProtectInactiveWards;
        public static ConfigEntry<WardConnectedAccessMode> wardAccessConnectedAccessMode;
        public static ConfigEntry<bool> wardAccessProtectInteractables;
        public static ConfigEntry<bool> wardBackgroundTamesPreventDamageToStructures;
        public static ConfigEntry<float> wardBackgroundPresenceRadius;
        public static ConfigEntry<WardBackgroundPresenceMode> wardBackgroundPresenceMode;
        public static ConfigEntry<WardConnectedAccessMode> wardBackgroundConnectedAccessMode;
        public static ConfigEntry<int> wardBackgroundProtectedBaseMinPieces;
        public static ConfigEntry<WardBackgroundStructureProtectionMode> wardBackgroundStructureProtection;
        public static ConfigEntry<bool> wardBackgroundProtectFire;
        public static ConfigEntry<bool> wardBackgroundProtectTames;
        public static ConfigEntry<bool> wardBackgroundProtectBoats;
        public static ConfigEntry<bool> wardBackgroundProtectCarts;
        public static ConfigEntry<WardBackgroundTamePacifyMode> wardBackgroundTamePacify;
        public static ConfigEntry<bool> wardBackgroundPreventBuildingAndDemolishing;

        public static ConfigEntry<WardAdminAccessMode> wardAdminAccess;
        public static ConfigEntry<bool> wardExternalControlCommandsEnabled;
        public static ConfigEntry<float> wardExternalControlCommandRange;
        public static ConfigEntry<int> wardBuildLimitPerPlayer;

        public static ConfigEntry<int> wardExpirationMinutes;
        public static ConfigEntry<WardExpirationRefreshMode> wardExpirationRefreshMode;
        public static ConfigEntry<WardConnectedAccessMode> wardExpirationConnectedAccessMode;
        public static ConfigEntry<WardExpirationReactivationMode> wardExpirationReactivationMode;
        public static ConfigEntry<bool> wardExpirationAdminHover;

        internal static ProtectiveWards instance;
        internal static long startTimeCached;
        internal static Dictionary<Vector3, PrivateArea> areaCache = new();
        internal static Dictionary<PrivateArea, int> wardIsRepairing = new();
        internal static Dictionary<PrivateArea, int> wardIsHealing = new();
        internal static Dictionary<PrivateArea, int> wardIsClosing = new();
        internal static Dictionary<PrivateArea, List<Door>> doorsToClose = new();

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
        public static readonly int s_colorMultiplier = "ward_colorMultiplier".GetStableHashCode();

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
        public static readonly int s_lastSaddleUser = "pw_last_saddle_user".GetStableHashCode();
        public static readonly int s_lastVehicleController = "pw_last_vehicle_controller".GetStableHashCode();

        private static readonly MaterialPropertyBlock s_matBlock = new();
        private static readonly Dictionary<PrivateArea, float> s_wardDefaultRanges = new();
        private static readonly Dictionary<PrivateArea, WardEmissionDefaults> s_wardEmissionDefaults = new();

        private sealed class WardEmissionDefaults
        {
            public ParticleSystem[] ParticleSystems;
            public ParticleSystem.MinMaxGradient[] ParticleStartColors;
            public Light Light;
            public Color LightColor;
            public LightFlicker Flicker;
            public bool HadFlicker;
            public bool FlickerEnabled;
        }

        private static float offeringsTimer = 0f;

        public enum ShipDamageType
        {
            Off,
            WaterDamage,
            AnyButPlayerDamage,
            AnyDamage
        }

        public enum WardConnectedAccessMode
        {
            Off,
            SameCreatorOnly,
            MutualTrust,
            AnyConnected
        }

        public enum WardPortalAccessMode
        {
            AllowAll,
            AllowTeleportOnly,
            BlockAll
        }

        public enum WardItemPickupMode
        {
            AllowAll,
            AllowNonPlayerDropped,
            BlockAll
        }

        public enum WardBackgroundPresenceMode
        {
            PermittedNearProtectedArea,
            PermittedInsideConnectedArea,
            PermittedOnline
        }

        public enum WardBackgroundStructureProtectionMode
        {
            Off,
            BlockNonPermittedPlayerDamage,
            BlockAllDamageWhenNoPermittedNearby
        }

        public enum WardBackgroundTamePacifyMode
        {
            Off,
            WhenNoPermittedNearby
        }

        public enum WardAdminAccessMode
        {
            Off,
            Admins,
            AdminsInGodMode
        }

        public enum WardExpirationRefreshMode
        {
            DirectPermitted,
            EffectiveAccess
        }

        public enum WardExpirationReactivationMode
        {
            ManualInteraction,
            AutomaticOnLogin
        }

        private void Awake()
        {
            instance = this;

            ConfigInit();

            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), pluginID);

            StartCoroutine(LocalizationManager.Localizer.Load());
        }

        private void Start()
        {
            FullProtection.PatchLoadedInteractables(_harmony);
        }

        private void FixedUpdate()
        {
            if (offeringsTimer > 0f)
                offeringsTimer -= Time.fixedDeltaTime;

            WardExpiration.Update();
        }

        private void OnDestroy()
        {
            Config.Save();
            _harmony?.UnpatchSelf();
            FullProtection.ResetDynamicPatchState();
        }
        
        public static void LogInfo(object data)
        {
            if (loggingEnabled.Value)
                instance.Logger.LogInfo(data);
        }

        private void ConfigInit()
        {
            config("General", "NexusID", 2450, "Nexus mod ID for updates", false);

            wardSettingsUseDefaultsForAllWards = config("Ward settings", "Use default values for wards without custom settings", defaultValue: true, "If enabled, wards without per-ward ZDO overrides use the default values from this config. If disabled, only values explicitly saved on a ward are applied.");
            wardSettingsRequireCreator = config("Ward settings", "Only creator can edit ward settings", defaultValue: true, "If enabled, only the ward creator can open and apply the per-ward settings window. If disabled, any player with ward access can edit these settings.");
            wardSettingsAllowAdminEdit = config("Ward settings", "Admins can edit ward settings", defaultValue: true, "If enabled, players allowed by Ward admin access can open and apply the per-ward settings window regardless of ward creator/access checks.");

            
            disableFlash = config("Misc", "Disable flash", defaultValue: false, "Disable flash on hit [Not Synced with Server]", false);
            showAreaMarker = config("Misc", "Always show radius", defaultValue: false, "Always show ward radius. Hover the ward for changes to take effect. [Not Synced with Server]", false);
            refreshingTime = config("Misc", "Ward protected status check time", defaultValue: 30, "Set how many seconds the \"inside the protected zone\" status is reset. [Not Synced with Server]" +
                                                                                                    "\nSetting more seconds can be helpful for fps for base with many objects and static untoggled wards. " +
                                                                                                    "\nDoesn't affect moving objects.", false);
            loggingEnabled = config("Misc", "Enable logging", defaultValue: false, "Enable logging for ward events. [Not Synced with Server]", false);
            showOfferingsInHover = config("Misc", "Show offerings in hover", defaultValue: true, "Show offerings list in hover text. [Not Synced with Server]", false);
            showOfferingsInHoverAfterSeconds = config("Misc", "Show offerings in hover after seconds", defaultValue: 10f, "Show offerings list after set amount of seconds. [Not Synced with Server]", false);
            maxTaxiSpeed = config("Misc", "Maximum taxi speed", defaultValue: 30f, "Reduce maximum taxi speed if it is laggy. [Not Synced with Server]", false);
            addLightMovement = config("Misc", "Add movement to light emitted by ward", defaultValue: true, "Adds little lavalamp effect on light emitted by ward. Applied only if ward emission color was changed. Reactivate ward after config change. [Not Synced with Server]", false);


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
                                                                                                                                   "\nOffer Hildir's chest to travel to Hildir for free. Chest will NOT be consumed." +
                                                                                                                                   "\nOffer the configured Hildir item to travel to Hildir. Item WILL be consumed." +
                                                                                                                                   "\nOffer the configured Bog Witch item to travel to Bog Witch. Item WILL be consumed.");
            offeringProtectFromNonPermitted = config("Offerings", "Protect from non-permitted players", defaultValue: false, "Set whether active Ward offerings are allowed only for players with direct or connected ward access. Disabled by default so visitors can make offerings to active wards.");

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
            autoCloseDoorsIgnorePrefabs = config("Passive", "Auto close doors ignore prefabs", defaultValue: "", "Comma-separated list of prefab names which should not be auto closed.");

            setWardRange = config("Range", "Change Ward range", defaultValue: false, "Default value for whether wards without per-ward range override should use a custom range. Each disabled ward can be configured separately from its settings window.");
            wardRange = config("Range", "Ward range", defaultValue: 10f, "Default ward range used for wards without per-ward range override. Each disabled ward can be configured separately from its settings window. Toggle ward protection for changes to take effect");
            supressSpawnInRange = config("Range", "Supress spawn in ward area", defaultValue: true, "Vanilla behavior is true. Set false if you want creatures and raids spawn in ward radius. Toggle ward protection for changes to take effect");
            permitEveryone = config("Ward admin", "Permit everyone", defaultValue: false, "Bypasses ward access checks completely. When enabled, every player is treated as having ward admin access, regardless of the Ward admin access mode. Permitted lists are still stored on wards but do not restrict access.");


            wardBubbleShow = config("Ward Bubble", "Show bubble", defaultValue: false, "Default value for wards without per-ward bubble override. Each disabled ward can be configured separately from its settings window. Show ward bubble like trader's one [Not Synced with Server]", false);
            wardBubbleColor = config("Ward Bubble", "Bubble color", defaultValue: Color.black, "Default bubble color for wards without per-ward bubble color override. Each disabled ward can be configured separately from its settings window. Toggle ward protection to change color [Not Synced with Server]", false);
            wardBubbleRefractionIntensity = config("Ward Bubble", "Refraction intensity", defaultValue: 0.005f, "Intensity of light refraction caused by bubble. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleWaveIntensity = config("Ward Bubble", "Wave intensity", defaultValue: 40f, "Bubble light distortion speed. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleGlossiness = config("Ward Bubble", "Glossiness", defaultValue: 0f, "Bubble glossiness. 1 to soap bubble effect. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleMetallic = config("Ward Bubble", "Metallic", defaultValue: 1f, "Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleNormalScale = config("Ward Bubble", "Normal scale", defaultValue: 15f, "Density of distortion effect. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardBubbleDepthFade = config("Ward Bubble", "Depth fade", defaultValue: 1f, "Toggle ward protection for changes to take effect [Not Synced with Server]", false);

            wardDemisterEnabled = config("Ward Demister", "Enable demister", defaultValue: false, "Ward will push out the mist");


            wardAreaMarkerPatch = config("Ward Circle", "Patch circle", defaultValue: false, "Default value for whether wards without per-ward circle override should use custom area marker parameters. Each disabled ward can be configured separately from its settings window. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
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


            wardEmissionColorEnabled = config("Ward Color", "Change emission color", defaultValue: false, "Default value for whether wards without per-ward color override should use a custom emission color. Each disabled ward can be configured separately from its settings window. World restart required to apply changes. [Not Synced with Server]", false);
            wardEmissionColor = config("Ward Color", "Color", defaultValue: new Color(0.967f, 0.508f, 0.092f), "Default ward emission color for wards without per-ward color override. Each disabled ward can be configured separately from its settings window. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardEmissionColorMultiplier = config("Ward Color", "Color multiplier", defaultValue: 2f, "Default ward emission color multiplier for wards without per-ward color multiplier override. Each disabled ward can be configured separately from its settings window. Makes color more bright and intense. Toggle ward protection for changes to take effect [Not Synced with Server]", false);
            wardLightColorEnabled = config("Ward Color", "Change flare and light accordingly", defaultValue: true, "Flare and emitted light color will fit ward color. Toggle ward protection for changes to take effect [Not Synced with Server]", false);


            wardPlantProtectionList = config("Ward protects", "Plants from list", "$item_carrot, $item_turnip, $item_onion, $item_carrotseeds, $item_turnipseeds, $item_onionseeds, $item_jotunpuffs, $item_magecap", "List of plants to be protected from damage");
            boarsHensProtectionGroupList = config("Ward protects", "Boars and hens from list", "boar, chicken", "List of tamed groups to be protected from damage");
            wardAccessProtectChests = config("Ward access from non-permitted players", "Chests", true, "Set whether an active Ward blocks non-permitted players from opening nearby chests and containers");
            wardAccessProtectDoors = config("Ward access from non-permitted players", "Doors", true, "Set whether an active Ward blocks non-permitted players from opening nearby doors");
            wardAccessProtectPlants = config("Ward access from non-permitted players", "Plant picking", true, "Set whether an active Ward blocks non-permitted players from picking nearby plants and pickables");
            wardAccessProtectBoats = config("Ward access from non-permitted players", "Boat mounting", true, "Set whether an active Ward blocks non-permitted players from mounting or controlling nearby boats");
            wardAccessProtectTames = config("Ward access from non-permitted players", "Tame mounting", true, "Set whether an active Ward blocks non-permitted players from mounting nearby tamed creatures");
            wardAccessProtectProductionStations = config("Ward access from non-permitted players", "Production stations", true, "Set whether an active Ward blocks non-permitted players from using nearby production stations such as smelters, kilns, ovens, cooking stations, fermenters, windmills, beehives and sap collectors.");
            wardAccessProtectItemStands = config("Ward access from non-permitted players", "Item stands", true, "Set whether an active Ward blocks non-permitted players from taking, placing, or changing items and equipment on nearby item stands and armor stands.");
            wardAccessProtectCarts = config("Ward access from non-permitted players", "Carts", true, "Set whether an active Ward blocks non-permitted players from dragging nearby carts and wagons.");
            wardAccessProtectPortals = config("Ward access from non-permitted players", "Portal access mode", WardPortalAccessMode.AllowTeleportOnly, "Controls how an active Ward protects nearby portals from non-permitted players."
                                                                                                                                     + "\nAllowAll: non-permitted players can use and rename portals as usual."
                                                                                                                                     + "\nAllowTeleportOnly: non-permitted players can teleport through portals but cannot rename/change portal tags."
                                                                                                                                     + "\nBlockAll: non-permitted players cannot teleport through or rename nearby portals.");
            wardAccessProtectFood = config("Ward access from non-permitted players", "Food and feasts", true, "Set whether an active Ward blocks non-permitted players from eating nearby feasts and placed consumable item pieces.");
            wardAccessProtectItemPickupMode = config("Ward access from non-permitted players", "Item pickup mode", WardItemPickupMode.AllowNonPlayerDropped, "Controls whether an active Ward blocks non-permitted players from picking up nearby non-consumable item drops."
                                                                                                                                              + "\nAllowAll: non-permitted players can pick up all non-consumable item drops."
                                                                                                                                              + "\nAllowNonPlayerDropped: non-permitted players can pick up normal loot/world drops, but not items dropped by players."
                                                                                                                                              + "\nBlockAll: non-permitted players cannot pick up any non-consumable item drops inside protected ward areas.");
            wardAccessProtectMapTables = config("Ward access from non-permitted players", "Map tables", true, "Set whether an active Ward blocks non-permitted players from reading from or writing to nearby map tables.");
            wardAccessProtectFireplaces = config("Ward access from non-permitted players", "Fireplaces", false, "Set whether an active Ward blocks non-permitted players from interacting with nearby fireplaces. Disabled by default so visitors can add fuel to fires.");
            wardAccessProtectShieldGenerators = config("Ward access from non-permitted players", "Shield generator fuel", true, "Set whether an active Ward blocks non-permitted players from adding fuel to nearby shield generators.");
            wardAccessProtectIncinerators = config("Ward access from non-permitted players", "Incinerator lever", true, "Set whether an active Ward blocks non-permitted players from pulling nearby obliterator/incinerator levers.");
            wardAccessProtectTurrets = config("Ward access from non-permitted players", "Turrets", true, "Set whether an active Ward blocks non-permitted players from interacting with nearby turrets, including changing targets or adding ammo.");
            wardAccessProtectCraftingStations = config("Ward access from non-permitted players", "Crafting stations", true, "Set whether an active Ward blocks non-permitted players from using or discovering nearby crafting stations.");
            wardAccessProtectBeds = config("Ward access from non-permitted players", "Beds", true, "Set whether an active Ward blocks non-permitted players from sleeping in nearby beds, even if another mod allows it.");
            wardAccessProtectCatapults = config("Ward access from non-permitted players", "Catapults", true, "Set whether an active Ward blocks non-permitted players from using nearby catapults.");
            wardAccessProtectArcheryTargets = config("Ward access from non-permitted players", "Archery target", true, "Set whether an active Ward blocks non-permitted players from interacting with nearby archery targets.");
            wardAccessProtectBarbers = config("Ward access from non-permitted players", "Barber station", true, "Set whether an active Ward blocks non-permitted players from using nearby barber stations.");
            wardAccessProtectInactiveWards = config("Ward access from non-permitted players", "Inactive ward inside another ward", true, "Set whether an active Ward blocks non-permitted players from interacting with nearby inactive wards inside its protected area.");
            wardAccessConnectedAccessMode = config("Ward access from non-permitted players", "Connected ward access mode", WardConnectedAccessMode.Off, "Controls whether overlapping active player wards can share access for protected interactions."
                                                                                                                                         + "\nOff: only direct access to the ward covering the object is accepted."
                                                                                                                                         + "\nSameCreatorOnly: access may be shared only between overlapping wards created by the same player."
                                                                                                                                         + "\nMutualTrust: access may be shared only between overlapping wards whose creators are mutually trusted/permitted by the protected/root ward, not by transitive chain trust."
                                                                                                                                         + "\nAnyConnected: access to any overlapping ward can grant access to the whole connected group. Intended for single-party servers where all players share one ward network.");
            wardAccessProtectInteractables = config("Ward access from non-permitted players", "Generic interactables", false, "Set whether an active Ward blocks non-permitted players from using generic nearby interactable objects. This is a broad compatibility layer for vanilla and modded interactables. Ownership-sensitive objects and special cases are excluded or handled separately.");
            wardBackgroundTamesPreventDamageToStructures = config("Ward without permitted players nearby", "Tames prevent damage to structures", true, "Set whether tamed creatures inside a protected ward network prevent their own damage to player-built structures when no permitted/effective-access player is nearby.");
            wardBackgroundPresenceRadius = config("Ward without permitted players nearby", "Permitted player presence radius", 64f, "Horizontal radius used to detect a permitted/effective-access player for background protection checks.");
            wardBackgroundPresenceMode = config("Ward without permitted players nearby", "Permitted player presence mode", WardBackgroundPresenceMode.PermittedNearProtectedArea, "Controls how player presence disables background protection."
                                                                                                                                                                                + "\nPermittedNearProtectedArea: a permitted/effective player must be near the protected object."
                                                                                                                                                                                + "\nPermittedInsideConnectedArea: a permitted/effective player anywhere inside the connected ward network disables background protection."
                                                                                                                                                                                + "\nPermittedOnline: any permitted/effective online player disables background protection.");
            wardBackgroundConnectedAccessMode = config("Ward without permitted players nearby", "Connected ward access mode", WardConnectedAccessMode.Off, "Controls which overlapping wards are treated as one protected network for background protection.");
            wardBackgroundProtectedBaseMinPieces = config("Ward without permitted players nearby", "Protected base minimum player pieces", 80, "Minimum number of player-built pieces inside the connected ward network required for broad background protection. 0 disables this qualification check.");
            wardBackgroundStructureProtection = config("Ward without permitted players nearby", "Background protection mode", WardBackgroundStructureProtectionMode.BlockNonPermittedPlayerDamage, "Off: no broad structure background protection."
                                                                                                                                                                               + "\nBlockNonPermittedPlayerDamage: blocks direct damage from non-permitted players."
                                                                                                                                                                               + "\nBlockAllDamageWhenNoPermittedNearby: blocks all damage to player-built structures while no permitted/effective player is nearby.");
            wardBackgroundProtectFire = config("Ward without permitted players nearby", "Prevent structure fire damage", true, "Blocks fire/burning damage to player-built structures in a protected ward network while no permitted/effective player is nearby.");
            wardBackgroundProtectTames = config("Ward without permitted players nearby", "Protect tames", true, "Blocks damage to tamed creatures inside a protected ward network while no permitted/effective player is nearby.");
            wardBackgroundProtectBoats = config("Ward without permitted players nearby", "Protect boats", true, "Blocks damage to boats inside a protected ward network while no permitted/effective player is nearby.");
            wardBackgroundProtectCarts = config("Ward without permitted players nearby", "Protect carts", true, "Blocks damage to carts/wagons inside a protected ward network while no permitted/effective player is nearby.");
            wardBackgroundTamePacify = config("Ward without permitted players nearby", "Tame pacify", WardBackgroundTamePacifyMode.WhenNoPermittedNearby, "When enabled, tamed creatures inside a protected ward network drop creature/static targets and do not acquire new combat targets while no permitted/effective player is nearby.");
            wardBackgroundPreventBuildingAndDemolishing = config("Ward without permitted players nearby", "Prevent building and demolishing", true, "Blocks non-permitted players from placing new pieces or demolishing other players' pieces inside a protected ward network while no permitted/effective player is nearby. Players can always demolish their own pieces.");

            wardAdminAccess = config("Ward admin", "Ward admin access", WardAdminAccessMode.AdminsInGodMode, "Controls global admin bypass behavior used by ward settings, permit command bypass, admin-only expiration commands, and admin-only expiration hover details. Ignored when Permit everyone is enabled."
                                                                                                    + "\nOff: admins do not bypass ward access checks."
                                                                                                    + "\nAdmins: server admins and host bypass ward access checks."
                                                                                                    + "\nAdminsInGodMode: server admins and host bypass ward access checks only while god mode is enabled.");
            wardExternalControlCommandsEnabled = config("Ward admin", "Enable external ward control commands", true, "Enables external ward management console commands and aliases for changing nearby wards, including permitted-list, enabled-state, and expiration-state commands.");
            wardExternalControlCommandRange = config("Ward admin", "External ward control command range", 5f, "Maximum horizontal distance from the player to the ward used by external ward management commands.");
            wardBuildLimitPerPlayer = config("Ward admin", "Ward build limit per player", 0, "Maximum number of wards each player can have in the world. 0 disables the limit. Existing wards are never removed; if the limit is exceeded after building a new ward, only the newly built ward is destroyed.");

            wardExpirationMinutes = config("Ward expiration", "Expiration minutes", 0, "0 disables inactive ward expiration. This is a multiplayer/server-side abandonment mechanic and is ignored in singleplayer. It is also skipped when Ward admin / Permit everyone is enabled. When greater than 0, the server periodically checks tracked ward ZDOs while player character ZDOs are online. A ward expires after this many real-time minutes without nearby activity from a player who can refresh it. Old wards are initialized with the current server time, so enabling this option does not expire existing wards immediately.");
            wardExpirationRefreshMode = config("Ward expiration", "Expiration refresh mode", WardExpirationRefreshMode.EffectiveAccess, "Controls which nearby players can refresh the inactive ward timer and reactivate expired wards. The player must be within the ward's current radius. DirectPermitted accepts the ward creator, directly permitted players, and admin/global bypass. EffectiveAccess also accepts access through overlapping connected wards according to Expiration connected access mode.");
            wardExpirationConnectedAccessMode = config("Ward expiration", "Expiration connected access mode", WardConnectedAccessMode.Off, "Controls connected ward access only for expiration refresh and reactivation when Expiration refresh mode is EffectiveAccess. Off requires direct access to this ward. SameCreatorOnly, MutualTrust, and AnyConnected can let an overlapping active connected ward keep this ward alive or reactivate it. Expired wards are not treated as active connected access sources for this check.");
            wardExpirationReactivationMode = config("Ward expiration", "Expiration reactivation mode", WardExpirationReactivationMode.ManualInteraction, "Controls how expired wards become active again. ManualInteraction keeps expired wards inactive until a player with refresh access interacts with the ward. AutomaticOnLogin reactivates an expired ward when a player with refresh access is nearby during a server check, or when an expired loaded ward wakes up near such a player. Reactivation also refreshes the last-active timestamp.");
            wardExpirationAdminHover = config("Ward expiration", "Show expiration admin hover details", false, "Shows additional expiration debug details in ward hover text for players allowed by Ward admin access. The extra lines are intended for server administration and include raw Unix timestamps and the last player recorded as refreshing or reactivating the ward.", false);

            wardExpirationMinutes.SettingChanged += (sender, args) => WardExpiration.ResetNextCheckTime();

            wardPlantProtectionList.SettingChanged += (sender, args) => FillWardProtectionLists();
            boarsHensProtectionGroupList.SettingChanged += (sender, args) => FillWardProtectionLists();

            FillWardProtectionLists();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            return Config.Bind(group, name, defaultValue, WithJotunnSync(description, synchronizedSetting));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        private static ConfigDescription WithJotunnSync(ConfigDescription description, bool synchronizedSetting)
        {
            if (!synchronizedSetting)
                return description;

            List<object> tags = description.Tags?.ToList() ?? new List<object>();
            tags.Add(new ConfigurationManagerAttributes { IsAdminOnly = true });

            return new ConfigDescription(description.Description, description.AcceptableValues, tags.ToArray());
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

        public static bool IsActivePlayerWard(PrivateArea ward) => ward != null && ward.IsEnabled() && ward.m_ownerFaction == Character.Faction.Players;

        private static bool AreWardsOverlapping(PrivateArea ward, PrivateArea candidate) => ward != null && candidate != null && ward.m_radius + candidate.m_radius >= Utils.DistanceXZ(ward.transform.position, candidate.transform.position);

        public static bool IsInsideWardXZ(PrivateArea ward, Vector3 point, float radius = 0f) => ward != null && Utils.DistanceXZ(ward.transform.position, point) <= ward.m_radius + radius;

        public static bool HasDirectAccessToWard(PrivateArea ward, Player player)
        {
            if (ward == null || player == null)
                return true;

            return HasDirectAccessToWard(ward, player.GetPlayerID());
        }

        public static bool HasDirectAccessToWard(PrivateArea ward, long playerID)
        {
            if (ward == null)
                return true;

            if (playerID == 0L)
                return false;

            if (ward.m_ownerFaction != Character.Faction.Players)
                return false;

            if (ward.m_piece != null && ward.m_piece.GetCreator() == playerID)
                return true;

            return ward.IsPermitted(playerID);
        }

        public static bool HasAccessToWard(PrivateArea ward, Player player) => HasDirectAccessToWard(ward, player);

        private static long GetCreatorId(PrivateArea ward) => ward?.m_piece != null ? ward.m_piece.GetCreator() : 0L;

        public static bool CanShareConnectedAccess(PrivateArea protectedWard, PrivateArea candidateWard, WardConnectedAccessMode mode)
        {
            if (mode == WardConnectedAccessMode.Off)
                return false;

            if (!IsActivePlayerWard(protectedWard) || !IsActivePlayerWard(candidateWard))
                return false;

            if (protectedWard == candidateWard)
                return true;

            switch (mode)
            {
                case WardConnectedAccessMode.SameCreatorOnly:
                    long protectedCreator = GetCreatorId(protectedWard);
                    long candidateCreator = GetCreatorId(candidateWard);
                    return protectedCreator != 0L && protectedCreator == candidateCreator;
                case WardConnectedAccessMode.MutualTrust:
                    protectedCreator = GetCreatorId(protectedWard);
                    candidateCreator = GetCreatorId(candidateWard);
                    return protectedCreator != 0L
                           && candidateCreator != 0L
                           && HasDirectAccessToWard(protectedWard, candidateCreator)
                           && HasDirectAccessToWard(candidateWard, protectedCreator);
                case WardConnectedAccessMode.AnyConnected:
                    return true;
                default:
                    return false;
            }
        }

        public static IEnumerable<PrivateArea> ConnectedAccessAreas(PrivateArea ward, WardConnectedAccessMode mode)
        {
            if (!IsActivePlayerWard(ward))
                yield break;

            HashSet<PrivateArea> visited = new();
            List<PrivateArea> queue = new();
            int queueIndex = 0;

            visited.Add(ward);
            queue.Add(ward);

            while (queueIndex < queue.Count)
            {
                PrivateArea current = queue[queueIndex++];
                yield return current;

                if (mode == WardConnectedAccessMode.Off)
                    continue;

                foreach (PrivateArea candidate in PrivateArea.m_allAreas)
                {
                    if (visited.Contains(candidate))
                        continue;

                    if (!IsActivePlayerWard(candidate) || !AreWardsOverlapping(current, candidate))
                        continue;

                    // Connected sharing rules are checked against the protected/root ward.
                    if (!CanShareConnectedAccess(ward, candidate, mode))
                        continue;

                    visited.Add(candidate);
                    queue.Add(candidate);
                }
            }
        }

        public static bool HasAccessToWardOrConnectedWard(PrivateArea ward, Player player)
        {
            if (wardAccessConnectedAccessMode == null)
                return HasDirectAccessToWard(ward, player);

            return HasAccessToWardOrConnectedWard(ward, player, wardAccessConnectedAccessMode.Value);
        }

        public static bool HasAccessToWardOrConnectedWard(PrivateArea ward, Player player, WardConnectedAccessMode mode)
        {
            if (ward == null || player == null)
                return true;

            return HasAccessToWardOrConnectedWard(ward, player.GetPlayerID(), mode);
        }

        public static bool HasAccessToWardOrConnectedWard(PrivateArea ward, long playerID, WardConnectedAccessMode mode)
        {
            if (HasDirectAccessToWard(ward, playerID))
                return true;

            if (mode == WardConnectedAccessMode.Off)
                return false;

            return ConnectedAccessAreas(ward, mode).Any(area => area != ward && HasDirectAccessToWard(area, playerID));
        }

        public static PrivateArea FindProtectedWard(Vector3 point)
        {
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (IsActivePlayerWard(area) && area.IsInside(point, 0f))
                    return area;
            }

            return null;
        }

        public static bool IsPointInsideWardNetwork(Vector3 point, PrivateArea ward, WardConnectedAccessMode mode)
        {
            return ConnectedAccessAreas(ward, mode).Any(area => area.IsInside(point, 0f));
        }

        public static bool IsPointInsideWardNetworkXZ(Vector3 point, PrivateArea ward, WardConnectedAccessMode mode)
        {
            return ConnectedAccessAreas(ward, mode).Any(area => IsInsideWardXZ(area, point));
        }

        public static bool TryFindProtectedWardNetwork(Vector3 sourcePoint, Vector3 targetPoint, out PrivateArea ward)
        {
            ward = null;
            WardConnectedAccessMode mode = wardAccessConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardAccessConnectedAccessMode.Value;

            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (!IsActivePlayerWard(area) || !area.IsInside(sourcePoint, 0f))
                    continue;

                if (!IsPointInsideWardNetwork(targetPoint, area, mode))
                    continue;

                ward = area;
                return true;
            }

            return false;
        }

        public static bool TryFindProtectedWardNetworkXZ(Vector3 sourcePoint, Vector3 targetPoint, out PrivateArea ward)
        {
            ward = null;
            WardConnectedAccessMode mode = wardAccessConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardAccessConnectedAccessMode.Value;

            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (!IsActivePlayerWard(area) || !IsInsideWardXZ(area, sourcePoint))
                    continue;

                if (!IsPointInsideWardNetworkXZ(targetPoint, area, mode))
                    continue;

                ward = area;
                return true;
            }

            return false;
        }

        public static bool TryGetObjectCreatorId(Component component, out long creatorId)
        {
            creatorId = 0L;

            if (component == null)
                return false;

            TombStone tombStone = component.GetComponentInParent<TombStone>();
            if (tombStone != null)
                creatorId = tombStone.GetOwner();

            if (creatorId == 0L)
            {
                Bed bed = component.GetComponentInParent<Bed>();
                if (bed != null)
                    creatorId = bed.GetOwner();
            }

            if (creatorId == 0L)
            {
                Tameable tameable = component.GetComponentInParent<Tameable>();
                if (tameable != null && tameable.m_piece != null)
                    creatorId = tameable.m_piece.GetCreator();
            }

            if (creatorId == 0L)
            {
                ShipControlls shipControlls = component.GetComponentInParent<ShipControlls>();
                Piece shipPiece = shipControlls != null && shipControlls.m_ship != null ? shipControlls.m_ship.GetComponent<Piece>() : null;
                if (shipPiece != null)
                    creatorId = shipPiece.GetCreator();
            }

            if (creatorId == 0L)
            {
                Piece piece = component.GetComponentInParent<Piece>();
                if (piece != null)
                    creatorId = piece.GetCreator();
            }

            if (creatorId == 0L)
            {
                ZDO zdo = component?.GetComponentZNetView()?.GetZDO();
                if (zdo != null)
                    creatorId = zdo.GetLong(ZDOVars.s_creator, 0L);
            }

            return creatorId != 0L;
        }

        public static bool TryGetObjectCreatorName(Component component, out string creatorName)
        {
            creatorName = "";

            if (component == null)
                return false;

            TombStone tombStone = component.GetComponentInParent<TombStone>();
            if (tombStone != null)
                creatorName = tombStone.GetOwnerName();

            if (string.IsNullOrWhiteSpace(creatorName))
            {
                Bed bed = component.GetComponentInParent<Bed>();
                if (bed != null)
                    creatorName = bed.GetOwnerName();
            }

            if (string.IsNullOrWhiteSpace(creatorName))
            {
                PrivateArea ward = component.GetComponentInParent<PrivateArea>();
                if (ward != null)
                    creatorName = ward.GetCreatorName();
            }

            if (string.IsNullOrWhiteSpace(creatorName))
            {
                ZDO zdo = component?.GetComponentZNetView()?.GetZDO();
                if (zdo != null)
                    creatorName = zdo.GetString(ZDOVars.s_creatorName);
            }

            if (string.IsNullOrWhiteSpace(creatorName) && TryGetObjectCreatorId(component, out long creatorId))
            {
                Player player = Player.GetPlayer(creatorId);
                if (player != null)
                    creatorName = player.GetPlayerName();
            }

            return !string.IsNullOrWhiteSpace(creatorName);
        }

        private static bool IsOwnershipSensitiveObject(Component component)
        {
            if (component == null)
                return false;

            return component.GetComponentInParent<Ship>() != null
                   || component.GetComponentInParent<ShipControlls>() != null
                   || component.GetComponentInParent<Vagon>() != null
                   || component.GetComponentInParent<TombStone>() != null
                   || component.GetComponentInParent<Ladder>() != null
                   || component.GetComponentInParent<TeleportWorld>() != null
                   || component.GetComponentInParent<ItemStand>() != null
                   || component.GetComponentInParent<ArmorStand>() != null
                   || component.GetComponentInParent<Sadle>() != null
                   || component.GetComponentInParent<Tameable>() != null
                   || component.GetComponentInParent<Pet>() != null
                   || component.GetComponentInParent<Petable>() != null;
        }

        public static bool IsObjectOwnedByPlayerWithWardAccess(Component component, Player interactingPlayer)
        {
            if (!IsOwnershipSensitiveObject(component) || interactingPlayer == null)
                return false;

            if (WasPlayerLastSaddleUser(component, interactingPlayer))
                return true;

            if (IsVehicleControlObject(component))
                return WasPlayerLastVehicleController(component, interactingPlayer);

            if (!TryGetObjectCreatorId(component, out long creatorId))
                return false;

            return creatorId == interactingPlayer.GetPlayerID();
        }

        private static bool IsVehicleControlObject(Component component)
        {
            return component != null
                   && (component.GetComponentInParent<Ship>() != null
                       || component.GetComponentInParent<ShipControlls>() != null
                       || component.GetComponentInParent<Vagon>() != null);
        }

        private static bool HasLastSaddleUser(ZNetView nview, long playerID)
        {
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            return zdo != null && zdo.GetLong(s_lastSaddleUser, 0L) == playerID;
        }

        private static bool HasLastVehicleController(ZNetView nview, long playerID)
        {
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            return zdo != null && zdo.GetLong(s_lastVehicleController, 0L) == playerID;
        }

        public static bool WasPlayerLastSaddleUser(Component component, Player player)
        {
            if (component == null || player == null)
                return false;

            long playerID = player.GetPlayerID();
            if (playerID == 0L)
                return false;

            if (HasLastSaddleUser(component.GetComponentZNetView(), playerID))
                return true;

            Sadle sadle = component.GetComponentInParent<Sadle>() ?? component.GetComponentInChildren<Sadle>(true);
            if (sadle == null)
                return false;

            if (HasLastSaddleUser(sadle.GetComponentZNetView(), playerID))
                return true;

            if (sadle.m_character != null && HasLastSaddleUser(sadle.m_character.GetComponentZNetView(), playerID))
                return true;

            return false;
        }
        public static bool WasPlayerLastVehicleController(Component component, Player player)
        {
            if (component == null || player == null)
                return false;

            long playerID = player.GetPlayerID();
            if (playerID == 0L)
                return false;

            ShipControlls shipControlls = component.GetComponentInParent<ShipControlls>();
            if (shipControlls != null && shipControlls.m_ship != null && HasLastVehicleController(shipControlls.m_ship.GetComponentZNetView(), playerID))
                return true;

            Ship ship = component.GetComponentInParent<Ship>();
            if (ship != null && HasLastVehicleController(ship.GetComponentZNetView(), playerID))
                return true;

            Vagon vagon = component.GetComponentInParent<Vagon>();
            if (vagon != null && HasLastVehicleController(vagon.GetComponentZNetView(), playerID))
                return true;

            return false;
        }


        public static bool ShouldBypassVanillaPrivateAreaCheck(Component component, Humanoid human)
        {
            if (component == null)
                return false;

            Player player = human as Player;
            if (player == null)
                return false;

            bool foundProtectedWard = false;

            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || !area.IsEnabled() || !area.IsInside(component.transform.position, 0f))
                    continue;

                if (!IsActivePlayerWard(area))
                    return false;

                foundProtectedWard = true;

                if (HasAccessToWardOrConnectedWard(area, player))
                    continue;

                if (IsObjectOwnedByPlayerWithWardAccess(component, player))
                    continue;

                return false;
            }

            return foundProtectedWard;
        }

        public static string GetWardOwnerName(PrivateArea ward)
        {
            string ownerName = "";

            if (ward != null)
                ownerName = ward.GetCreatorName();

            return string.IsNullOrWhiteSpace(ownerName) ? "Unknown" : ownerName;
        }

        internal static string GetWardOwnerName(ZDO zdo)
        {
            if (zdo == null)
                return "";

            string name = zdo.GetString(ZDOVars.s_creatorName);
            long creator = zdo.GetLong(ZDOVars.s_creator, 0L);

            return string.IsNullOrEmpty(name) ? (creator != 0L ? creator.ToString() : "") : CensorShittyWords.FilterUGC(name, UGCType.CharacterName, creator);
        }

        public static string GetPrivateZoneDeniedMessage(PrivateArea ward) => GetPrivateZoneDeniedMessage(GetWardOwnerName(ward));

        public static string GetPrivateZoneDeniedMessage(string ownerName) => $"{"$msg_privatezone".Localize()}. {"$piece_guardstone_owner".Localize()}: {ownerName}";

        public static bool HasEffectiveAccessPlayerNearby(PrivateArea ward, Vector3 point, float radius)
        {
            WardConnectedAccessMode mode = wardAccessConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardAccessConnectedAccessMode.Value;

            foreach (Player player in Player.GetAllPlayers())
            {
                if (Utils.DistanceXZ(player.transform.position, point) > radius)
                    continue;

                if (HasAccessToWardOrConnectedWard(ward, player, mode))
                    return true;
            }

            return false;
        }

        public static bool ShouldBlockUnauthorizedWardInteraction(Vector3 point, Humanoid human, bool flash, out PrivateArea ward) => ShouldBlockUnauthorizedWardInteraction(point, human, flash, out ward, null);

        public static bool ShouldBlockUnauthorizedWardInteraction(Component component, Humanoid human, bool flash, out PrivateArea ward)
        {
            ward = null;

            if (component == null)
                return false;

            return ShouldBlockUnauthorizedWardInteraction(component.transform.position, human, flash, out ward, component);
        }

        public static bool BlockUnauthorizedWardInteraction(Vector3 point, Humanoid human, bool flash = true)
        {
            return ShouldBlockUnauthorizedWardInteraction(point, human, flash, out _);
        }

        public static bool BlockUnauthorizedWardInteraction(Component component, Humanoid human, bool flash = true)
        {
            return ShouldBlockUnauthorizedWardInteraction(component, human, flash, out _);
        }

        private static bool ShouldBlockUnauthorizedWardInteraction(Vector3 point, Humanoid human, bool flash, out PrivateArea ward, Component component)
        {
            ward = null;

            Player player = human as Player;
            if (player == null)
                return false;

            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (!IsActivePlayerWard(area) || !area.IsInside(point, 0f))
                    continue;

                if (HasAccessToWardOrConnectedWard(area, player))
                    continue;

                if (component != null && IsObjectOwnedByPlayerWithWardAccess(component, player))
                    continue;

                if (flash)
                    area.FlashShield(false);

                ward = area;
                player.Message(MessageHud.MessageType.Center, GetPrivateZoneDeniedMessage(area));
                return true;
            }

            return false;
        }

        private static bool IsDisabledForeignWard(PrivateArea ward, long playerID)
        {
            if (ward == null || ward.IsEnabled() || playerID == 0L || ward.m_piece == null)
                return false;

            return ward.m_piece.GetCreator() != playerID;
        }

        private static bool IsDisabledForeignWard(ZDO zdo, long playerID)
        {
            if (zdo == null || playerID == 0L || zdo.GetBool(ZDOVars.s_enabled, false))
                return false;

            return zdo.GetLong(ZDOVars.s_creator, 0L) != playerID;
        }

        public static bool ShouldBlockInactiveWardAccess(PrivateArea inactiveWard, Player player)
        {
            if (inactiveWard == null || inactiveWard.IsEnabled() || player == null)
                return false;

            if (wardAccessProtectInactiveWards == null || !wardAccessProtectInactiveWards.Value)
                return false;

            WardConnectedAccessMode mode = wardAccessConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardAccessConnectedAccessMode.Value;
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || area == inactiveWard || !IsActivePlayerWard(area) || !area.IsInside(inactiveWard.transform.position, 0f))
                    continue;

                if (HasAccessToWardOrConnectedWard(area, player, mode))
                    continue;

                return true;
            }

            return false;
        }

        public static bool ShouldBlockInactiveWardAccess(PrivateArea inactiveWard, long playerID)
        {
            if (inactiveWard == null || inactiveWard.IsEnabled() || playerID == 0L)
                return false;

            if (wardAccessProtectInactiveWards == null || !wardAccessProtectInactiveWards.Value)
                return false;

            WardConnectedAccessMode mode = wardAccessConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardAccessConnectedAccessMode.Value;
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || area == inactiveWard || !IsActivePlayerWard(area) || !area.IsInside(inactiveWard.transform.position, 0f))
                    continue;

                if (HasAccessToWardOrConnectedWard(area, playerID, mode))
                    continue;

                return true;
            }

            return false;
        }

        public static bool CanEditWardSettings(PrivateArea ward, Player player)
        {
            if (ward == null || player == null)
                return false;

            if (wardSettingsAllowAdminEdit.Value && HasLocalWardAdminAccess())
                return true;

            if (ShouldBlockInactiveWardAccess(ward, player))
                return false;

            if (ward.m_piece == null)
                return false;

            if (wardSettingsRequireCreator.Value)
                return !IsDisabledForeignWard(ward, player.GetPlayerID()) && ward.m_piece.IsCreator();

            return ward.HaveLocalAccess();
        }

        public static bool CanApplyWardSettings(PrivateArea ward, long playerID)
        {
            if (ward == null || playerID == 0L)
                return false;

            if (wardSettingsAllowAdminEdit.Value && HasWardAdminAccess(playerID))
                return true;

            if (ShouldBlockInactiveWardAccess(ward, playerID))
                return false;

            if (wardSettingsRequireCreator.Value)
                return !IsDisabledForeignWard(ward, playerID) && ward.m_piece != null && ward.m_piece.GetCreator() == playerID;

            WardConnectedAccessMode mode = wardAccessConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardAccessConnectedAccessMode.Value;
            return HasAccessToWardOrConnectedWard(ward, playerID, mode);
        }

        public static bool CanApplyWardSettings(ZDO zdo, long playerID)
        {
            if (zdo == null || playerID == 0L)
                return false;

            if (wardSettingsAllowAdminEdit.Value && HasWardAdminAccess(playerID))
                return true;

            if (wardSettingsRequireCreator.Value)
                return !IsDisabledForeignWard(zdo, playerID) && zdo.IsCreator(playerID);

            WardConnectedAccessMode mode = wardAccessConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardAccessConnectedAccessMode.Value;
            return zdo.HasConnectedWardAccess(playerID, mode, IsActiveWardZdoForSettings);
        }

        private static bool IsActiveWardZdoForSettings(ZDO zdo)
        {
            return zdo.IsWard()
                   && zdo.GetBool(ZDOVars.s_enabled, false)
                   && !zdo.GetBool(WardExpiration.s_expirationExpired, false);
        }

        internal readonly struct RoutedPlayerContext
        {
            public readonly long PlayerID;
            public readonly string PlayerName;
            public readonly ZDOID CharacterID;
            public readonly Vector3 Position;
            public readonly bool HasPosition;
            public readonly long Sender;

            public RoutedPlayerContext(long playerID, string playerName, ZDOID characterID, Vector3 position, bool hasPosition, long sender)
            {
                PlayerID = playerID;
                PlayerName = playerName ?? "";
                CharacterID = characterID;
                Position = position;
                HasPosition = hasPosition;
                Sender = sender;
            }
        }

        public static bool HasLocalWardAdminAccess()
        {
            Player localPlayer = Player.m_localPlayer;
            return localPlayer != null && HasWardAdminAccess(localPlayer.GetPlayerID());
        }

        public static bool HasWardAdminAccess(long playerID)
        {
            if (playerID == 0L)
                return false;

            if (permitEveryone != null && permitEveryone.Value)
                return true;

            if (wardAdminAccess == null || wardAdminAccess.Value == WardAdminAccessMode.Off)
                return false;

            if (!IsPlayerServerAdminOrHost(playerID))
                return false;

            if (wardAdminAccess.Value == WardAdminAccessMode.Admins)
                return true;

            Player player = Player.GetPlayer(playerID);
            if (player != null)
                return player.InGodMode();

            Player localPlayer = Player.m_localPlayer;
            return localPlayer != null && localPlayer.GetPlayerID() == playerID && localPlayer.InGodMode();
        }

        public static bool IsPlayerServerAdminOrHost(long playerID)
        {
            if (playerID == 0L || ZNet.instance == null)
                return false;

            if (ZNet.IsSinglePlayer)
                return true;

            Player localPlayer = Player.m_localPlayer;
            if (localPlayer != null && localPlayer.GetPlayerID() == playerID && ZNet.instance.LocalPlayerIsAdminOrHost())
                return true;

            return TryFindPlayerInfo(playerID, out ZNet.PlayerInfo playerInfo)
                   && playerInfo.m_userInfo.m_id.IsValid
                   && ZNet.instance.PlayerIsAdmin(playerInfo.m_userInfo.m_id);
        }

        internal static bool TryGetRoutedPlayer(long sender, long claimedPlayerID, out RoutedPlayerContext player)
        {
            player = default;
            if (claimedPlayerID == 0L || ZNet.instance == null)
                return false;

            if (sender != 0L && ZRoutedRpc.instance != null && sender != ZRoutedRpc.instance.m_id)
                return TryGetPeerRoutedPlayer(sender, claimedPlayerID, out player);

            Player localPlayer = Player.m_localPlayer;
            if (localPlayer != null && localPlayer.GetPlayerID() == claimedPlayerID)
            {
                player = new RoutedPlayerContext(
                    claimedPlayerID,
                    localPlayer.GetPlayerName(),
                    localPlayer.GetZDOID(),
                    localPlayer.transform.position,
                    hasPosition: true,
                    sender);
                return true;
            }

            Player loadedPlayer = Player.GetPlayer(claimedPlayerID);
            if (loadedPlayer != null)
            {
                player = new RoutedPlayerContext(
                    claimedPlayerID,
                    loadedPlayer.GetPlayerName(),
                    loadedPlayer.GetZDOID(),
                    loadedPlayer.transform.position,
                    hasPosition: true,
                    sender);
                return true;
            }

            if (TryFindPlayerCharacterZdo(claimedPlayerID, out ZDO characterZdo))
            {
                player = new RoutedPlayerContext(
                    claimedPlayerID,
                    characterZdo.GetString(ZDOVars.s_playerName),
                    characterZdo.m_uid,
                    characterZdo.GetPosition(),
                    hasPosition: true,
                    sender);
                return true;
            }

            return false;
        }

        internal static bool TryGetRoutedPlayer(long sender, out RoutedPlayerContext player)
        {
            player = default;
            if (ZNet.instance == null)
                return false;

            if (sender != 0L && ZRoutedRpc.instance != null && sender != ZRoutedRpc.instance.m_id)
                return TryGetPeerRoutedPlayer(sender, out player);

            Player localPlayer = Player.m_localPlayer;
            if (localPlayer != null)
            {
                player = new RoutedPlayerContext(
                    localPlayer.GetPlayerID(),
                    localPlayer.GetPlayerName(),
                    localPlayer.GetZDOID(),
                    localPlayer.transform.position,
                    hasPosition: true,
                    sender);
                return true;
            }

            return false;
        }

        private static bool TryGetPeerRoutedPlayer(long sender, long claimedPlayerID, out RoutedPlayerContext player)
        {
            if (!TryGetPeerRoutedPlayer(sender, out player))
                return false;

            return player.PlayerID == claimedPlayerID;
        }

        private static bool TryGetPeerRoutedPlayer(long sender, out RoutedPlayerContext player)
        {
            player = default;
            if (ZNet.instance == null || ZDOMan.instance == null)
                return false;

            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            if (peer == null || !peer.IsReady() || peer.m_characterID.IsNone())
                return false;

            ZDO characterZdo = ZDOMan.instance.GetZDO(peer.m_characterID);
            if (characterZdo == null)
                return false;

            long actualPlayerID = characterZdo.GetLong(ZDOVars.s_playerID, 0L);
            if (actualPlayerID == 0L)
                return false;

            player = new RoutedPlayerContext(
                actualPlayerID,
                characterZdo.GetString(ZDOVars.s_playerName),
                peer.m_characterID,
                characterZdo.GetPosition(),
                hasPosition: true,
                sender);
            return true;
        }

        private static bool TryFindPlayerCharacterZdo(long playerID, out ZDO characterZdo)
        {
            characterZdo = null;
            if (playerID == 0L || ZNet.instance == null)
                return false;

            foreach (ZDO zdo in ZNet.instance.GetAllCharacterZDOS())
            {
                if (zdo == null || zdo.GetLong(ZDOVars.s_playerID, 0L) != playerID)
                    continue;

                characterZdo = zdo;
                return true;
            }

            return false;
        }

        private static bool TryFindPlayerInfo(long playerID, out ZNet.PlayerInfo playerInfo)
        {
            playerInfo = default;
            if (playerID == 0L || ZNet.instance == null || ZDOMan.instance == null)
                return false;

            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                if (info.m_characterID.IsNone())
                    continue;

                ZDO characterZdo = ZDOMan.instance.GetZDO(info.m_characterID);
                if (characterZdo == null || characterZdo.GetLong(ZDOVars.s_playerID, 0L) != playerID)
                    continue;

                playerInfo = info;
                return true;
            }

            return false;
        }

        public static bool HasZdoBool(ZDO zdo, int key)
        {
            return zdo != null && zdo.GetBool(key, false) == zdo.GetBool(key, true);
        }

        public static bool HasZdoFloat(ZDO zdo, int key)
        {
            if (zdo == null)
                return false;

            const float markerA = -987654.125f;
            const float markerB = -987653.125f;
            return !Mathf.Approximately(zdo.GetFloat(key, markerA), markerA) || !Mathf.Approximately(zdo.GetFloat(key, markerB), markerB);
        }

        public static bool HasZdoString(ZDO zdo, int key)
        {
            if (zdo == null)
                return false;

            const string markerA = "__pw_missing_a__";
            const string markerB = "__pw_missing_b__";
            string valueA = zdo.GetString(key, markerA);
            string valueB = zdo.GetString(key, markerB);
            return valueA == valueB && !string.IsNullOrEmpty(valueA);
        }

        public static bool HasZdoVec3(ZDO zdo, int key)
        {
            return zdo != null && zdo.GetVec3(key, out _);
        }

        public static bool GetWardBoolSetting(ZDO zdo, int key, bool defaultValue, bool disabledValue = false)
        {
            bool fallback = wardSettingsUseDefaultsForAllWards.Value ? defaultValue : disabledValue;
            return zdo != null ? zdo.GetBool(key, fallback) : fallback;
        }

        public static float GetWardFloatSetting(ZDO zdo, int key, float defaultValue)
        {
            return zdo != null ? zdo.GetFloat(key, defaultValue) : defaultValue;
        }

        public static string GetWardStringSetting(ZDO zdo, int key, string defaultValue)
        {
            if (zdo == null)
                return defaultValue;

            string value = zdo.GetString(key, defaultValue);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        public static Vector3 GetWardVec3Setting(ZDO zdo, int key, Vector3 defaultValue)
        {
            return zdo != null && zdo.GetVec3(key, out Vector3 value) ? value : defaultValue;
        }

        public static void RemoveZdoBool(ZDO zdo, int key)
        {
            zdo?.RemoveInt(key);
        }

        public static void RemoveZdoFloat(ZDO zdo, int key)
        {
            zdo?.RemoveFloat(key);
        }

        public static void RemoveZdoString(ZDO zdo, int key)
        {
            zdo?.Set(key, "");
        }

        public static void RemoveZdoVec3(ZDO zdo, int key)
        {
            zdo?.RemoveVec3(key);
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

        private static bool IsCraftingStationNear(Piece piece, PrivateArea ward)
        {
            return !wardPassiveRepairRequireStation.Value 
                || piece.m_craftingStation == null 
                || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench) 
                || CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, ward.transform.position)
                || CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, piece.transform.position);
        }

        private static bool CanBeRepaired(Piece piece, PrivateArea ward)
        {
            return (piece.IsPlacedByPlayer() ? IsCraftingStationNear(piece, ward) : wardPassiveRepairNonPlayer.Value)
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

                List<Piece> pieces = new();

                ConnectedAreas(ward).Do(area => Piece.GetAllPiecesInRadius(area.transform.position, area.m_radius, pieces));

                HashSet<Piece> piecesToRepair = pieces.Where(piece => CanBeRepaired(piece, ward)).ToHashSet();

                if (piecesToRepair.Count == 0)
                {
                    LogInfo($"Passive repairing stopped");
                    wardIsRepairing.Remove(ward);

                    if (initiator != null)
                    {
                        string str = "$msg_doesnotneedrepair".Localize();
                        initiator.Message(MessageHud.MessageType.TopLeft, char.ToUpper(str[0]) + str.Substring(1));
                    }

                    yield break;
                }

                wardIsRepairing[ward] = piecesToRepair.Count;
                foreach (Piece piece in piecesToRepair)
                {
                    if (piece.TryGetComponent(out WearNTear WNT) && WNT.Repair())
                    {
                        piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation);

                        wardIsRepairing.TryGetValue(ward, out int toRepair);
                        wardIsRepairing[ward] = Math.Max(toRepair - 1, 0);

                        initiator?.Message(MessageHud.MessageType.TopLeft, "$piece_repair".Localize());
                        break;
                    }
                }

                yield return new WaitForSecondsRealtime(10f);
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

                yield return new WaitForSeconds(1f);
            }
        }

        public static IEnumerable<PrivateArea> ConnectedAreas(PrivateArea ward)
        {
            return PrivateArea.m_allAreas.Where(area => area == ward || (area.IsEnabled() && area.m_radius + ward.m_radius >= Utils.DistanceXZ(area.transform.position, ward.transform.position)));
        }

        private static bool s_activatingConnectedWards;

        internal static void ActivateConnectedLoadedWards(PrivateArea rootWard, long requesterID, string requesterName = "")
        {
            if (s_activatingConnectedWards || rootWard == null || requesterID == 0L || !rootWard.IsEnabled())
                return;

            WardConnectedAccessMode mode = wardAccessConnectedAccessMode?.Value ?? WardConnectedAccessMode.Off;
            if (mode == WardConnectedAccessMode.Off)
                return;

            s_activatingConnectedWards = true;
            try
            {
                foreach (PrivateArea ward in ConnectedActivationAreas(rootWard, requesterID, mode))
                {
                    if (ward == rootWard)
                        continue;

                    if (WardExpiration.IsExpired(ward))
                        WardExpiration.SetExpired(ward.m_nview.GetZDO(), expired: false, requesterID, requesterName);

                    if (!ward.IsEnabled())
                        ward.SetEnabled(true);
                }
            }
            finally
            {
                s_activatingConnectedWards = false;
            }
        }

        private static IEnumerable<PrivateArea> ConnectedActivationAreas(PrivateArea rootWard, long requesterID, WardConnectedAccessMode mode)
        {
            HashSet<PrivateArea> visited = new();
            List<PrivateArea> queue = new();
            int queueIndex = 0;

            visited.Add(rootWard);
            queue.Add(rootWard);

            while (queueIndex < queue.Count)
            {
                PrivateArea current = queue[queueIndex++];
                yield return current;

                foreach (PrivateArea candidate in PrivateArea.m_allAreas)
                {
                    if (candidate == null || visited.Contains(candidate))
                        continue;

                    if (!IsPlayerWard(candidate) || !AreWardsOverlapping(current, candidate))
                        continue;

                    if (!HasDirectAccessToWard(candidate, requesterID) && !HasWardAdminAccess(requesterID))
                        continue;

                    if (!CanShareConnectedActivation(rootWard, candidate, mode))
                        continue;

                    visited.Add(candidate);
                    queue.Add(candidate);
                }
            }
        }

        private static bool IsPlayerWard(PrivateArea ward) => ward != null && ward.m_ownerFaction == Character.Faction.Players;

        private static bool CanShareConnectedActivation(PrivateArea protectedWard, PrivateArea candidateWard, WardConnectedAccessMode mode)
        {
            if (mode == WardConnectedAccessMode.Off)
                return false;

            if (!IsPlayerWard(protectedWard) || !IsPlayerWard(candidateWard))
                return false;

            if (protectedWard == candidateWard)
                return true;

            switch (mode)
            {
                case WardConnectedAccessMode.SameCreatorOnly:
                    long protectedCreator = GetCreatorId(protectedWard);
                    long candidateCreator = GetCreatorId(candidateWard);
                    return protectedCreator != 0L && protectedCreator == candidateCreator;
                case WardConnectedAccessMode.MutualTrust:
                    protectedCreator = GetCreatorId(protectedWard);
                    candidateCreator = GetCreatorId(candidateWard);
                    return protectedCreator != 0L
                           && candidateCreator != 0L
                           && HasDirectAccessToWard(protectedWard, candidateCreator)
                           && HasDirectAccessToWard(candidateWard, protectedCreator);
                case WardConnectedAccessMode.AnyConnected:
                    return true;
                default:
                    return false;
            }
        }

        [HarmonyPatch(typeof(CircleProjector), nameof(CircleProjector.CreateSegments))]
        public static class CircleProjector_CreateSegments_InitState
        {
            public static void Prefix(CircleProjector __instance, ref bool __state)
            {
                PrivateArea ward = __instance.transform.root.GetComponent<PrivateArea>();
                if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
                    return;

                ZDO zdo = ward.m_nview.GetZDO();
                if (zdo == null || !GetWardBoolSetting(zdo, s_circleEnabled, wardAreaMarkerPatch.Value))
                    return;

                float amount = GetWardFloatSetting(zdo, s_circleAmount, wardAreaMarkerAmount.Value);
                __instance.m_nrOfSegments = (int)(80 * amount * (__instance.m_radius / 32f));

                __state = (!__instance.m_sliceLines && __instance.m_segments.Count == __instance.m_nrOfSegments) || (__instance.m_sliceLines && __instance.m_calcStart == __instance.m_start && __instance.m_calcTurns == __instance.m_turns);
            }

            public static void Postfix(CircleProjector __instance, bool __state)
            {
                if (__state)
                    return;

                ZNetView m_nview = __instance.GetComponentZNetView();
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
            if (ward == null || !ward.m_model)
                return;

            if (ward.m_nview == null || !ward.m_nview.IsValid())
                return;

            ZDO zdo = ward.m_nview.GetZDO();
            if (zdo == null)
                return;

            int materialIndex = FindWardEmissionMaterialIndex(ward);
            if (materialIndex == -1)
                return;

            CacheWardEmissionDefaults(ward);

            if (!GetWardBoolSetting(zdo, s_customColor, wardEmissionColorEnabled.Value))
            {
                ResetWardEmissionColor(ward, materialIndex);
                return;
            }

            Vector3 defaultColor = new(wardEmissionColor.Value.r, wardEmissionColor.Value.g, wardEmissionColor.Value.b);
            Vector3 vector = GetWardVec3Setting(zdo, s_color, defaultColor);
            float multiplier = GetWardFloatSetting(zdo, s_colorMultiplier, wardEmissionColorMultiplier.Value);

            Color color;
            if (HasZdoVec3(zdo, s_color) && !HasZdoFloat(zdo, s_colorMultiplier))
                color = new Color(vector.x, vector.y, vector.z);
            else
                color = new Color(vector.x * multiplier, vector.y * multiplier, vector.z * multiplier);

            ward.m_model.GetPropertyBlock(s_matBlock, materialIndex);
            s_matBlock.SetColor("_EmissionColor", color);
            ward.m_model.SetPropertyBlock(s_matBlock, materialIndex);

            if (wardLightColorEnabled.Value && ward.m_enabledEffect)
            {
                float lightMultiplier = multiplier == 0f ? 1f : multiplier;

                foreach (ParticleSystem ps in ward.m_enabledEffect.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.startColor = new Color(color.r / lightMultiplier, color.g / lightMultiplier, color.b / lightMultiplier, main.startColor.color.a);
                }

                Light light = ward.m_enabledEffect.GetComponentInChildren<Light>();
                if (light)
                {
                    light.color = Color.Lerp(new Color(0.99f, 0.87f, 0.76f), new Color(color.r / lightMultiplier, color.g / lightMultiplier, color.b / lightMultiplier), 0.5f);
                    if (!light.TryGetComponent(out LightFlicker flicker) && addLightMovement.Value)
                        flicker = light.gameObject.AddComponent<LightFlicker>();

                    if (flicker != null)
                    {
                        flicker.enabled = addLightMovement.Value;
                        flicker.m_flickerIntensity = 0.1f;
                        flicker.m_flickerSpeed = 1f;
                        flicker.m_movement = 0.1f;
                        flicker.m_fadeInDuration = 3f;
                    }
                }
            }
        }

        private static int FindWardEmissionMaterialIndex(PrivateArea ward)
        {
            if (ward == null || !ward.m_model)
                return -1;

            for (int i = 0; i < ward.m_model.sharedMaterials.Length; i++)
            {
                if (ward.m_model.sharedMaterials[i].name.StartsWith("Guardstone_OdenGlow_mat"))
                    return i;
            }

            return -1;
        }

        private static void CacheWardEmissionDefaults(PrivateArea ward)
        {
            if (ward == null || s_wardEmissionDefaults.ContainsKey(ward))
                return;

            WardEmissionDefaults defaults = new();
            if (ward.m_enabledEffect)
            {
                defaults.ParticleSystems = ward.m_enabledEffect.GetComponentsInChildren<ParticleSystem>();
                defaults.ParticleStartColors = new ParticleSystem.MinMaxGradient[defaults.ParticleSystems.Length];
                for (int i = 0; i < defaults.ParticleSystems.Length; i++)
                    defaults.ParticleStartColors[i] = defaults.ParticleSystems[i].main.startColor;

                defaults.Light = ward.m_enabledEffect.GetComponentInChildren<Light>();
                if (defaults.Light)
                {
                    defaults.LightColor = defaults.Light.color;
                    defaults.HadFlicker = defaults.Light.TryGetComponent(out defaults.Flicker);
                    defaults.FlickerEnabled = defaults.Flicker != null && defaults.Flicker.enabled;
                }
            }

            s_wardEmissionDefaults[ward] = defaults;
        }

        private static void ResetWardEmissionColor(PrivateArea ward, int materialIndex)
        {
            if (ward == null || !ward.m_model)
                return;

            ward.m_model.SetPropertyBlock(null, materialIndex);

            if (!s_wardEmissionDefaults.TryGetValue(ward, out WardEmissionDefaults defaults))
                return;

            if (defaults.ParticleSystems != null && defaults.ParticleStartColors != null)
            {
                int count = Math.Min(defaults.ParticleSystems.Length, defaults.ParticleStartColors.Length);
                for (int i = 0; i < count; i++)
                {
                    ParticleSystem ps = defaults.ParticleSystems[i];
                    if (!ps)
                        continue;

                    ParticleSystem.MainModule main = ps.main;
                    main.startColor = defaults.ParticleStartColors[i];
                }
            }

            if (defaults.Light)
            {
                defaults.Light.color = defaults.LightColor;

                LightFlicker flicker = defaults.Light.GetComponent<LightFlicker>();
                if (flicker != null)
                {
                    if (defaults.HadFlicker)
                        flicker.enabled = defaults.FlickerEnabled;
                    else
                        UnityEngine.Object.Destroy(flicker);
                }
            }
        }

        private static string NormalizeHtmlColor(string value)
        {
            if (value.IsNullOrWhiteSpace())
                return value;

            return value.StartsWith("#") ? value : "#" + value;
        }

        public static void InitCircleProjectorState(CircleProjector marker, ZNetView nview)
        {
            if (marker == null || nview == null || !nview.IsValid())
                return;

            ZDO zdo = nview.GetZDO();
            if (!GetWardBoolSetting(zdo, s_circleEnabled, wardAreaMarkerPatch.Value))
                return;

            string start = GetWardStringSetting(zdo, s_circleStartColor, ColorUtility.ToHtmlStringRGBA(wardAreaMarkerStartColor.Value));
            Color startColor = wardAreaMarkerStartColor.Value;
            if (!start.IsNullOrWhiteSpace() && ColorUtility.TryParseHtmlString(NormalizeHtmlColor(start), out Color color))
                startColor = color;

            string end = GetWardStringSetting(zdo, s_circleEndColor, ColorUtility.ToHtmlStringRGBA(wardAreaMarkerEndColor.Value));
            Color endColor = wardAreaMarkerEndColor.Value;
            if (!end.IsNullOrWhiteSpace() && ColorUtility.TryParseHtmlString(NormalizeHtmlColor(end), out Color color1))
                endColor = color1;

            var gradient = new Gradient();
            if (endColor != Color.clear)
            {
                gradient.SetKeys(new GradientColorKey[4]
                                    {
                                        new(startColor, 0.0f),
                                        new(endColor, 0.45f),
                                        new(endColor, 0.55f),
                                        new(startColor, 1.0f)
                                    }, 
                                 Array.Empty<GradientAlphaKey>());
            }

            marker.m_speed = GetWardFloatSetting(zdo, s_circleSpeed, wardAreaMarkerSpeed.Value);

            for (int i = 0; i < marker.m_segments.Count; i++)
            {
                GameObject segment = marker.m_segments[i];
                
                segment.transform.localScale = new Vector3(marker.m_prefab.transform.localScale.x * GetWardFloatSetting(zdo, s_circleWidth, wardAreaMarkerWidth.Value), marker.m_prefab.transform.localScale.y, marker.m_prefab.transform.localScale.z * GetWardFloatSetting(zdo, s_circleLength, wardAreaMarkerLength.Value));

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

            if (m_nview == null || !m_nview.IsValid())
            {
                bubble.SetActive(false);
                return;
            }

            ZDO zdo = m_nview.GetZDO();
            if (zdo == null)
            {
                bubble.SetActive(false);
                return;
            }

            bubble.SetActive(GetWardBoolSetting(zdo, s_bubbleEnabled, wardBubbleShow.Value) && ward.IsEnabled());

            bubble.transform.localScale = Vector3.one * ward.m_radius * 2f;

            Transform noMonsterArea = bubble.transform.Find("NoMonsterArea");
            if (noMonsterArea != null)
                Destroy(noMonsterArea.gameObject);

            MeshRenderer renderer = bubble.GetComponent<MeshRenderer>();

            Vector3 vecColor = GetWardVec3Setting(zdo, s_bubbleColor, new Vector3(wardBubbleColor.Value.r, wardBubbleColor.Value.g, wardBubbleColor.Value.b));
            Color bubbleColor = new(vecColor.x, vecColor.y, vecColor.z, GetWardFloatSetting(zdo, s_bubbleColorAlpha, wardBubbleColor.Value.a));

            renderer.GetPropertyBlock(s_matBlock);

            s_matBlock.SetColor("_Color", bubbleColor);
            s_matBlock.SetFloat("_RefractionIntensity", GetWardFloatSetting(zdo, s_bubbleRefractionIntensity, wardBubbleRefractionIntensity.Value));
            s_matBlock.SetFloat("_WaveVel", GetWardFloatSetting(zdo, s_bubbleWaveVel, wardBubbleWaveIntensity.Value));
            s_matBlock.SetFloat("_Glossiness", GetWardFloatSetting(zdo, s_bubbleGlossiness, wardBubbleGlossiness.Value));
            s_matBlock.SetFloat("_Metallic", GetWardFloatSetting(zdo, s_bubbleMetallic, wardBubbleMetallic.Value));
            s_matBlock.SetFloat("_NormalScale", GetWardFloatSetting(zdo, s_bubbleNormalScale, wardBubbleNormalScale.Value));
            s_matBlock.SetFloat("_DepthFade", GetWardFloatSetting(zdo, s_bubbleDepthFade, wardBubbleDepthFade.Value));

            renderer.SetPropertyBlock(s_matBlock);
        }

        public static void InitDemisterState(PrivateArea ward, GameObject demister, ZNetView m_nview)
        {
            if (demister == null)
                return;

            if (m_nview == null || !m_nview.IsValid())
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
                if (autoCloseDoorsTime.Value == 0 || autoCloseDoorsIgnorePrefabs.Value.IndexOf(Utils.GetPrefabName(__instance.gameObject)) > -1)
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
                areaCache.Clear();

                wardIsHealing.Remove(__instance);
                wardIsRepairing.Remove(__instance);
                wardIsClosing.Remove(__instance);
                s_wardDefaultRanges.Remove(__instance);
                s_wardEmissionDefaults.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.HideMarker))]
        public static class PrivateArea_HideMarker_ShowAreaMarker
        {
            public static bool Prefix()
            {

                return !showAreaMarker.Value;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.AddUserList))]
        public static class PrivateArea_AddUserList_WardAltActionCaption
        {
            public static void Prefix(PrivateArea __instance, StringBuilder text)
            {
                if (!__instance.HaveLocalAccess())
                    return;

                bool wardEnabled = __instance.IsEnabled();
                if (!wardEnabled && !CanEditWardSettings(__instance, Player.m_localPlayer))
                    return;

                if (!wardEnabled)
                {
                    if (!ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive())
                        text.Append($"\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $pw_ward_open_settings");
                    else
                        text.Append($"\n[<color=yellow><b>$KEY_JoyAltKeys + $KEY_Use</b></color>] $pw_ward_open_settings");
                    return;
                }

                List<string> status = new();

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
                    status.Add($"$item_food_regen {TimeSpan.FromSeconds(secondsLeft):m\\:ss}");

                if (status.Count > 0)
                {
                    text.Append("\n$guardianstone_hook_power_activate: ");
                    text.Append(string.Join(", ", status.ToArray()));
                }

                if (offeringsTimer < showOfferingsInHoverAfterSeconds.Value + 0.5f)
                    offeringsTimer += Time.fixedDeltaTime * 2f;

                if (showOfferingsInHover.Value && offeringsTimer > showOfferingsInHoverAfterSeconds.Value)
                {
                    List<string> offeringsList = new();

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
                        text.Append(string.Join("\n", offeringsList));
                        text.Append('\n');
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.IsPermitted))]
        public static class PrivateArea_IsPermitted_GlobalAccessBypass
        {
            public static bool Prefix(long playerID, ref bool __result)
            {
                if (!HasWardAdminAccess(playerID))
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
                else if (!__instance.IsEnabled() && CanEditWardSettings(__instance, human as Player))
                {
                    __result = true;
                    WardSettingsUI.Open(__instance);
                    return false;
                }

                return true;
            }
        }

        private static bool IsWardToSetRange(PrivateArea ward) => WardZdoUtils.IsWardPrefab(ward?.gameObject);

        private static void CacheWardDefaultRange(PrivateArea ward)
        {
            if (ward == null || s_wardDefaultRanges.ContainsKey(ward))
                return;

            s_wardDefaultRanges[ward] = ward.m_radius;
        }

        private static void ResetWardRange(PrivateArea ward)
        {
            if (ward == null || !s_wardDefaultRanges.TryGetValue(ward, out float defaultRange))
                return;

            if (Math.Abs(ward.m_radius - defaultRange) < 0.001f)
                return;

            SetWardRange(ward, defaultRange);
            SetWardPlayerBase(ward, defaultRange);
        }

        private static void PatchRange(PrivateArea ward)
        {
            if (ward.m_nview == null || !ward.m_nview.IsValid())
                return;

            if (!IsWardToSetRange(ward))
                return;

            ZDO zdo = ward.m_nview.GetZDO();
            if (zdo == null)
                return;

            if (!WardZdoUtils.UseCustomWardRange(zdo))
            {
                ResetWardRange(ward);
                return;
            }

            float range = WardZdoUtils.GetConfiguredWardRange(zdo);
            if (Math.Abs(ward.m_radius - range) >= 0.001f)
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
                if (___m_nview == null || !___m_nview.IsValid())
                    return;

                CacheWardDefaultRange(__instance);
                PatchRange(__instance);
                WardExpiration.TryReactivateFromNearbyPlayer(__instance);

                if (showAreaMarker.Value)
                    __instance.m_areaMarker.gameObject.SetActive(value: true);

                InitBubbleState(__instance, EnsureWardBubble(__instance), ___m_nview);
                InitDemisterState(__instance, EnsureWardDemister(__instance), ___m_nview);
                InitEmissionColor(__instance);

                instance?.StartCoroutine(DelayedRefreshWardVisuals(__instance));
            }
        }

        private static GameObject EnsureWardBubble(PrivateArea ward)
        {
            if (ward == null)
                return null;

            Transform existing = ward.transform.Find(forceFieldName);
            if (existing != null)
                return existing.gameObject;

            if (forceField == null)
                return null;

            GameObject bubble = Instantiate(forceField, ward.transform);
            bubble.name = forceFieldName;
            return bubble;
        }

        private static GameObject EnsureWardDemister(PrivateArea ward)
        {
            if (ward == null)
                return null;

            Transform existing = ward.transform.Find(forceFieldDemisterName);
            if (existing != null)
                return existing.gameObject;

            if (forceFieldDemister == null)
                return null;

            GameObject demister = Instantiate(forceFieldDemister, ward.transform);
            demister.name = forceFieldDemisterName;
            return demister;
        }

        private static IEnumerator DelayedRefreshWardVisuals(PrivateArea ward)
        {
            yield return new WaitForSeconds(1f);
            RefreshWardVisuals(ward);
        }

        private static void RefreshAllLoadedWardVisuals()
        {
            foreach (PrivateArea ward in PrivateArea.m_allAreas)
                RefreshWardVisuals(ward);
        }

        public static void RefreshWardVisuals(PrivateArea ward)
        {
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
                return;

            PatchRange(ward);
            InitEmissionColor(ward);
            InitBubbleState(ward, EnsureWardBubble(ward), ward.m_nview);
            InitDemisterState(ward, EnsureWardDemister(ward), ward.m_nview);
            InitCircleProjectorState(ward.m_areaMarker, ward.m_nview);
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.RPC_FlashShield))]
        public static class PrivateArea_RPC_FlashShield_StopFlashShield
        {
            private static bool Prefix(PrivateArea __instance)
            {
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

                RefreshAllLoadedWardVisuals();
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        public static class ZoneSystem_OnDestroy_DestroyWardBubble
        {
            private static void Postfix()
            {
                UnityEngine.Object.Destroy(forceField);
                forceField = null;
                forceFieldDemister = null;
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
            private static void Postfix(PrivateArea __instance, ZNetView ___m_nview, long playerID)
            {
                if (___m_nview == null || !___m_nview.IsValid())
                    return;

                CacheWardDefaultRange(__instance);
                PatchRange(__instance);

                InitEmissionColor(__instance);

                InitBubbleState(__instance, __instance.transform.Find(forceFieldName)?.gameObject, ___m_nview);

                InitDemisterState(__instance, __instance.transform.Find(forceFieldDemisterName)?.gameObject, ___m_nview);

                InitCircleProjectorState(__instance.m_areaMarker, ___m_nview);

                if (__instance.IsEnabled())
                    ActivateConnectedLoadedWards(__instance, playerID, Player.GetPlayer(playerID)?.GetPlayerName() ?? "");
            }
        }
    }
}
