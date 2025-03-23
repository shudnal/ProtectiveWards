using HarmonyLib;
using SoftReferenceableAssets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardOfferings
    {
        internal static int slowFallHash = "SlowFall".GetStableHashCode();
        internal static int moderPowerHash = "GP_Moder".GetStableHashCode();
        private static readonly WaitForSeconds wait1sec = new WaitForSeconds(1);

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

                List<Character> characters = new List<Character>();
                ConnectedAreas(ward).Do(area => Character.GetCharactersInRange(area.transform.position, area.m_radius, characters));
                
                characters.ToHashSet().DoIf(character => character.IsTamed() || character.IsPlayer(), character => character.Heal(amount * seconds));

                yield return new WaitForSecondsRealtime(seconds);
            }
        }

        public static IEnumerator LightningStrikeEffect(PrivateArea ward)
        {
            if (ward == null)
                yield break;

            List<Player> players = new List<Player>();
            ConnectedAreas(ward).Do(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            players.ToHashSet().Do(player => preLightning.Create(player.transform.position, player.transform.rotation));

            LogInfo("Thor is preparing his strike");

            yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 7f));

            if (Game.IsPaused())
                yield return wait1sec;

            List<Character> characters = new List<Character>();
            ConnectedAreas(ward).Do(area => Character.GetCharactersInRange(area.transform.position, area.m_radius, characters));
            
            characters.ToHashSet().DoIf(character => character.IsMonsterFaction(Time.time), character => UnityEngine.Object.Instantiate(lightningAOE, character.transform.position, character.transform.rotation));
        }

        public static IEnumerator InstantGrowthEffect(PrivateArea ward, List<Plant> plants)
        {
            if (ward == null)
                yield break;

            LogInfo("Instant growth started");

            yield return wait1sec;

            foreach (Plant plant in plants.ToHashSet())
            {
                if (!plant || plant.m_nview == null || !plant.m_nview.IsValid() || !plant.m_nview.IsOwner())
                    continue;

                if (plant.m_status != 0)
                    plant.UpdateHealth(0);

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
                player.Message((i > 15) ? MessageHud.MessageType.TopLeft : MessageHud.MessageType.Center, Localization.instance.Localize("$pw_msg_travel_back", TimeSpan.FromSeconds(i).ToString(@"m\:ss")));
                yield return wait1sec;
            }

            LogInfo("Timer of player returnal ended");

            player.StartCoroutine(TaxiToPosition(player, position));
        }

        private static void RepairNearestStructures(bool augment, PrivateArea ward, Player initiator, ItemDrop.ItemData item)
        {
            int repaired = 0;
            int augmented = 0;

            List<Piece> pieces = new List<Piece>();

            ConnectedAreas(ward).Do(area => Piece.GetAllPiecesInRadius(area.transform.position, area.m_radius, pieces));

            foreach (Piece piece in pieces.Where(piece => piece.IsPlacedByPlayer()))
            {
                if (!piece.TryGetComponent(out WearNTear component))
                    continue;

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
                    initiator.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_repaired", repaired.ToString()));
            }
            else if (repaired > 0)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_repaired", repaired.ToString()));
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

        private static void GetPlantsInRange(Vector3 point, float radius, List<Plant> plants, bool growableOnly)
        {
            List<SlowUpdate> allPlants = SlowUpdate.GetAllInstaces();

            float num = radius * radius;
            foreach (SlowUpdate su_plant in allPlants)
            {
                if (!su_plant.TryGetComponent(out Plant plant))
                    continue;

                if (Utils.DistanceSqr(su_plant.transform.position, point) < num && plant.m_nview.IsOwner() && (!growableOnly || plant.m_status == 0))
                    plants.Add(plant);
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.UseItem))]
        public static class PrivateArea_UseItem_Offerings
        {
            private static bool Prefix(PrivateArea __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
            {
                if (!__instance.IsEnabled())
                {
                    LogInfo("Ward disabled");
                    return true;
                }

                if (!__instance.HaveLocalAccess())
                {
                    LogInfo("No access");
                    return true;
                }

                Player player = user as Player;

                if (!player || player != Player.m_localPlayer)
                {
                    LogInfo("UseItem user not a player");
                    return true;
                }

                LogInfo($"{player.GetPlayerName()} used {item.m_shared.m_name} on {__instance.m_nview.GetZDO()}");

                bool augment = item.m_shared.m_name == "$item_blackcore" && offeringAugmenting.Value;
                bool repair = augment || (item.m_shared.m_name == "$item_surtlingcore" && offeringActiveRepair.Value);

                bool consumable = (offeringFood.Value || offeringMead.Value) && (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable);

                bool thunderstrike = offeringThundertone.Value && item.m_shared.m_name == "$item_thunderstone";

                bool trophy = offeringTrophy.Value && (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy);

                bool growAll = offeringEitr.Value && item.m_shared.m_name == "$item_eitr";
                bool growth = (offeringYmirRemains.Value && item.m_shared.m_name == "$item_ymirremains") || growAll;

                bool moderPower = item.m_shared.m_name == "$item_dragonegg" && ZoneSystem.instance.GetGlobalKey(GlobalKeys.defeated_dragon);

                bool taxi = offeringTaxi.Value && (item.m_shared.m_name == "$item_coins" ||
                                                   IsBossTrophy(item.m_shared.m_name) ||
                                                   IsItemForHildirTravel(item.m_shared.m_name) ||
                                                   IsItemForBogWitchTravel(item.m_shared.m_name) ||
                                                   item.m_shared.m_name == "$item_chest_hildir1" ||
                                                   item.m_shared.m_name == "$item_chest_hildir2" ||
                                                   item.m_shared.m_name == "$item_chest_hildir3");

                if (!repair && !consumable && !thunderstrike && !trophy && !growth && !moderPower && !taxi)
                    return true;

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
                return false;
            }
        }

        private static void ApplyTrophyEffectOnNearbyEnemies(PrivateArea ward, ItemDrop.ItemData item, Player initiator)
        {
            trophyTargets = Resources.FindObjectsOfTypeAll<Turret>().FirstOrDefault(ws => ws.name == "piece_turret")?.m_configTargets;
            if (trophyTargets == null)
                return;

            bool killed = false;

            foreach (Turret.TrophyTarget configTarget in trophyTargets.Where(configTarget => (item.m_shared.m_name == configTarget.m_item.m_itemData.m_shared.m_name)))
            {
                List<Character> characters = new List<Character>();
                ConnectedAreas(ward).Do(area => Character.GetCharactersInRange(area.transform.position, area.m_radius, characters));

                foreach (Character character in characters.ToHashSet().Where(character => character.IsMonsterFaction(Time.time)))
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
            if (!lightningAOE)
            {
                Incinerator incinerator = Resources.FindObjectsOfTypeAll<Incinerator>().FirstOrDefault();

                lightningAOE = incinerator.m_lightingAOEs;
                preLightning = incinerator.m_leverEffects;
            }

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

                instance.StartCoroutine(PassiveHealingEffect(ward, amount: item.m_shared.m_foodRegen / 2, seconds: 1));
                LogInfo("Passive healing begins");

                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize($"$msg_consumed: {item.m_shared.m_name}"));
                initiator.GetInventory().RemoveOneItem(item);

                return;
            }

            if (!(bool)item.m_shared.m_consumeStatusEffect)
                return;

            LogInfo("Consumable effect offered");

            List<Player> players = new List<Player>();

            ConnectedAreas(ward).Do(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            bool applied = false;

            foreach (Player player in players.ToHashSet().Where(player => player.CanConsumeItem(item)))
            {
                player.m_seman.AddStatusEffect(item.m_shared.m_consumeStatusEffect.NameHash(), resetTime: true);
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

        private static void ApplyInstantGrowthEffectOnNearbyPlants(PrivateArea ward, ItemDrop.ItemData item, Player initiator, bool growableOnly = true)
        {
            List<Plant> plants = new List<Plant>();
            ConnectedAreas(ward).Do(area => GetPlantsInRange(area.transform.position, area.m_radius, plants, growableOnly));

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

            ConnectedAreas(ward).Do(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            foreach (Player player in players.ToHashSet())
            {
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
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$pw_msg_notravel"));
                return;
            }

            if (!canTravel)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$pw_msg_canttravel"));
                return;
            }

            Vector3 location = Vector3.zero;
            int stack = 0;

            bool locationFound; string locationName;
            if (IsBossTrophy(item.m_shared.m_name))
            {
                locationName = "StartTemple";
                locationFound = TryGetFoundLocation(locationName, initiator.transform.position, ref location);
            }
            else if (item.m_shared.m_name == "$item_coins")
            {
                locationName = "Vendor_BlackForest";
                locationFound = TryGetFoundLocation(locationName, initiator.transform.position, ref location);
                stack = locationFound ? offeringTaxiPriceHaldorDiscovered.Value : offeringTaxiPriceHaldorUndiscovered.Value;
            }
            else if (IsItemForHildirTravel(item.m_shared.m_name) ||
                       item.m_shared.m_name == "$item_chest_hildir1" ||
                       item.m_shared.m_name == "$item_chest_hildir2" ||
                       item.m_shared.m_name == "$item_chest_hildir3")
            {
                locationName = "Hildir_camp";
                locationFound = TryGetFoundLocation(locationName, initiator.transform.position, ref location);
                stack = 1;
            }
            else if (IsItemForBogWitchTravel(item.m_shared.m_name))
            {
                locationName = "BogWitch_Camp";
                locationFound = TryGetFoundLocation(locationName, initiator.transform.position, ref location);
                stack = offeringTaxiPriceBogWitchAmount.Value;
            }
            else
                return;

            if (locationFound)
                StartTaxi(initiator, location, item.m_shared.m_name, stack);
            else if (!ZNet.instance.IsServer())
                ClosestLocationRequest(locationName, initiator.transform.position, item.m_shared.m_name, stack);
            else
                LogInfo($"Location {locationName} is not found");
        }

        internal static void RegisterRPCs()
        {
            if (ZNet.instance.IsServer())
                ZRoutedRpc.instance.Register<ZPackage>("ClosestLocationRequest", RPC_ClosestLocationRequest);
            else
                ZRoutedRpc.instance.Register<ZPackage>("StartTaxi", RPC_StartTaxi);
        }

        public static void ClosestLocationRequest(string name, Vector3 position, string itemName, int stack)
        {
            LogInfo($"{name} closest location request");

            ZPackage zPackage = new ZPackage();
            zPackage.Write(name);
            zPackage.Write(position);
            zPackage.Write(itemName);
            zPackage.Write(stack);

            ZRoutedRpc.instance.InvokeRoutedRPC("ClosestLocationRequest", zPackage);
        }

        public static void RPC_ClosestLocationRequest(long sender, ZPackage pkg)
        {
            // Server

            string name = pkg.ReadString();
            Vector3 position = pkg.ReadVector3();
            string itemName = pkg.ReadString();
            int stack = pkg.ReadInt();

            Vector3 target = Vector3.zero;
            if (!TryGetFoundLocation(name, position, ref target))
            {
                LogInfo($"Location {name} is not found");
                return;
            }

            ZPackage zPackage = new ZPackage();
            zPackage.Write(target);
            zPackage.Write(itemName);
            zPackage.Write(stack);

            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "StartTaxi", zPackage);
        }

        public static void RPC_StartTaxi(long sender, ZPackage pkg)
        {
            Vector3 location = pkg.ReadVector3();
            string itemName = pkg.ReadString();
            int stack = pkg.ReadInt();

            LogInfo($"Server responded with closest location");
            StartTaxi(Player.m_localPlayer, location, itemName, stack);
        }

        internal static void StartTaxi(Player initiator, Vector3 position, string itemName, int stack)
        {
            if (Utils.DistanceXZ(initiator.transform.position, position) < 300f)
            {
                initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$pw_msg_tooclose"));
                return;
            }

            if (stack > 0 && (IsItemForHildirTravel(itemName) || IsItemForBogWitchTravel(itemName) || itemName == "$item_coins"))
            {
                if (initiator.GetInventory().CountItems(itemName) < stack)
                {
                    initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_incompleteoffering"));
                    return;
                }
                initiator.GetInventory().RemoveItem(itemName, stack);
            }

            initiator.StartCoroutine(TaxiToPosition(initiator, position, returnBack: true, waitSeconds: 10));
        }

        internal static bool TryGetFoundLocation(string name, Vector3 position, ref Vector3 target)
        {
            ZoneSystem.instance.tempIconList.Clear();
            ZoneSystem.instance.GetLocationIcons(ZoneSystem.instance.tempIconList);
            foreach (KeyValuePair<Vector3, string> loc in ZoneSystem.instance.tempIconList)
                if (loc.Value == name)
                {
                    target = loc.Key;
                    LogInfo($"Found closest {name} in icon list");
                    return true;
                }

            if (ZoneSystem.instance.FindClosestLocation(name, position, out ZoneSystem.LocationInstance location))
            {
                target = location.m_position;
                LogInfo($"Found closest {name} in location list");
                return true;
            }

            return false;
        }

        internal static bool IsItemForHildirTravel(string itemName) => offeringTaxiPriceHildirItem.Value != "" && itemName == offeringTaxiPriceHildirItem.Value;

        internal static bool IsItemForBogWitchTravel(string itemName) => offeringTaxiPriceBogWitchItem.Value != "" && itemName == offeringTaxiPriceBogWitchItem.Value;

        internal static bool IsBossTrophy(string itemName)
        {
            return itemName == "$item_trophy_eikthyr" ||
                   itemName == "$item_trophy_elder" ||
                   itemName == "$item_trophy_bonemass" ||
                   itemName == "$item_trophy_dragonqueen" ||
                   itemName == "$item_trophy_goblinking" ||
                   itemName == "$item_trophy_seekerqueen" ||
                   itemName == "$item_trophy_fader";
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
                    player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$pw_msg_travel_starting", TimeSpan.FromSeconds(i).ToString(@"m\:ss")));
                    yield return wait1sec;
                }
            }

            while (Valkyrie.m_instance != null)
            {
                player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$menu_pleasewait"));
                yield return wait1sec;
            }

            DateTime flightInitiated = DateTime.Now;

            bool playerShouldExit = player.IsAttachedToShip() || player.IsAttached() || player.IsDead() || player.IsRiding() || player.IsSleeping() || player.IsTeleporting()
                                            || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();

            while (playerShouldExit || player.IsEncumbered() || !player.IsTeleportable())
            {
                string timeSpent = (DateTime.Now - flightInitiated).ToString(@"m\:ss");
                if (playerShouldExit)
                    player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$pw_msg_travel_inside", timeSpent));
                else
                    player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$pw_msg_travel_blocked", timeSpent) + Localization.instance.Localize(player.IsEncumbered() ? " $se_encumbered_start" : " $msg_noteleport"));

                yield return wait1sec;

                playerShouldExit = player.IsAttachedToShip() || player.IsAttached() || player.IsDead() || player.IsRiding() || player.IsSleeping() || player.IsTeleporting()
                                            || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();
            }

            player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$pw_msg_travel_start"));

            taxiTargetPosition = position;
            taxiReturnBack = returnBack;
            taxiPlayerPositionToReturn = player.transform.position;
            playerDropped = false;

            Player.m_localPlayer.m_valkyrie.Load();
            GameObject valkyrie = UnityEngine.Object.Instantiate(Player.m_localPlayer.m_valkyrie.Asset, player.transform.position, Quaternion.identity);
            valkyrie.GetComponent<ZNetView>().HoldReferenceTo((IReferenceCounted)(object)Player.m_localPlayer.m_valkyrie);
            Player.m_localPlayer.m_valkyrie.Release();

            canTravel = true;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetIntro))]
        public static class Player_SetIntro_Taxi
        {
            static void Postfix(Player __instance)
            {
                if (!modEnabled.Value)
                    return;

                if (__instance.InIntro())
                    return;

                if (taxiReturnBack && canTravel)
                {
                    isTravelingPlayer = __instance;
                    __instance.StartCoroutine(ReturnPlayerToPosition(__instance, taxiPlayerPositionToReturn, offeringTaxiSecondsToFlyBack.Value));
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

                __instance.m_nview = __instance.GetComponent<ZNetView>();
                __instance.m_animator = __instance.GetComponentInChildren<Animator>();
                if (!__instance.m_nview.IsOwner() || Valkyrie.m_instance != null && Valkyrie.m_instance != __instance)
                {
                    __instance.enabled = false;
                    return false;
                }

                Valkyrie.m_instance = __instance;

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
                if (!modEnabled.Value)
                    return;

                if (!offeringTaxi.Value)
                    return;

                if (ZInput.GetButton("Use") && ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyUse") && ZInput.GetButton("JoyAltPlace"))
                    __instance.DropPlayer();
            }
        }

        [HarmonyPatch(typeof(Valkyrie), nameof(Valkyrie.DropPlayer))]
        public static class Valkyrie_DropPlayer_Taxi
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                if (!offeringTaxi.Value)
                    return;

                playerDropped = true;
                if (!Player.m_localPlayer.m_seman.HaveStatusEffect(slowFallHash))
                {
                    castSlowFall = true;
                    Player.m_localPlayer.m_seman.AddStatusEffect(slowFallHash);
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

                if (!offeringTaxi.Value)
                    return;

                if (Player.m_localPlayer != __instance)
                    return;

                if (playerDropped && castSlowFall && (__instance.IsOnGround() || __instance.IsSwimming()))
                {
                    castSlowFall = false;
                    playerDropped = false;
                    
                    if (__instance.m_seman.HaveStatusEffect(slowFallHash))
                        __instance.m_seman.RemoveStatusEffect(slowFallHash, true);
                    
                    LogInfo("Remove slow fall");
                }
            }
        }

        [HarmonyPatch(typeof(Valkyrie), nameof(Valkyrie.OnDestroy))]
        public static class Valkyrie_OnDestroy_Taxi
        {
            private static void Prefix(Valkyrie __instance)
            {
                if (!modEnabled.Value)
                    return;

                canTravel = true;
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        public static class ZoneSystem_Start_Taxi
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                RegisterRPCs();
            }
        }
    }
}
