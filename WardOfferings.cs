using BepInEx.Configuration;
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
    public static class WardOfferings
    {
        internal static int slowFallHash = "SlowFall".GetStableHashCode();
        internal static int moderPowerHash = "GP_Moder".GetStableHashCode();
        private static readonly WaitForSeconds wait1sec = new(1);

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

                List<Character> characters = new();
                ConnectedAreas(ward).Do(area => Character.GetCharactersInRange(area.transform.position, area.m_radius, characters));
                
                characters.ToHashSet().DoIf(character => character.IsTamed() || character.IsPlayer(), character => character.Heal(amount * seconds));

                yield return new WaitForSecondsRealtime(seconds);
            }
        }

        public static IEnumerator LightningStrikeEffect(PrivateArea ward)
        {
            if (ward == null)
                yield break;

            List<Player> players = new();
            ConnectedAreas(ward).Do(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            players.ToHashSet().Do(player => preLightning.Create(player.transform.position, player.transform.rotation));

            LogInfo("Thor is preparing his strike");

            yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 7f));

            if (Game.IsPaused())
                yield return wait1sec;

            List<Character> characters = new();
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
                player.Message((i > 15) ? MessageHud.MessageType.TopLeft : MessageHud.MessageType.Center, "$pw_msg_travel_back".Localize(TimeSpan.FromSeconds(i).ToString(@"m\:ss")));
                yield return wait1sec;
            }

            LogInfo("Timer of player returnal ended");

            player.StartCoroutine(TaxiToPosition(player, position));
        }

        private static void RepairNearestStructures(bool augment, PrivateArea ward, Player initiator, ItemDrop.ItemData item)
        {
            int repaired = 0;
            int augmented = 0;

            List<Piece> pieces = new();

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
                initiator.Message(MessageHud.MessageType.Center, "$msg_offerdone".Localize());
                if (repaired > 0)
                    initiator.Message(MessageHud.MessageType.TopLeft, "$msg_repaired".Localize(repaired.ToString()));
            }
            else if (repaired > 0)
            {
                initiator.Message(MessageHud.MessageType.Center, "$msg_repaired".Localize(repaired.ToString()));
            }
            else if (augment)
            {
                initiator.Message(MessageHud.MessageType.Center, "$msg_cantoffer".Localize());
            }
            else
            {
                string str = "$msg_doesnotneedrepair".Localize();
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

                if (plant.m_nview == null || !plant.m_nview.IsValid() || !plant.m_nview.IsOwner())
                    continue;

                if (Utils.DistanceSqr(su_plant.transform.position, point) < num && (!growableOnly || plant.m_status == 0))
                    plants.Add(plant);
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.UseItem))]
        public static class PrivateArea_UseItem_Offerings
        {
            private static bool Prefix(PrivateArea __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
            {
                if (__instance == null || __instance.m_nview == null || !__instance.m_nview.IsValid() || item == null || item.m_shared == null)
                    return true;

                if (__instance.m_ownerFaction != Character.Faction.Players || !WardZdoUtils.IsWardPrefab(__instance.gameObject))
                    return true;

                if (!__instance.IsEnabled())
                {
                    LogInfo("Ward disabled");
                    return true;
                }

                Player player = user as Player;

                if (!player || player != Player.m_localPlayer)
                {
                    LogInfo("UseItem user not a player");
                    return true;
                }

                if (offeringProtectFromNonPermitted.Value && !HasAccessToWardOrConnectedWard(__instance, player))
                {
                    LogInfo("No access");
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

                bool taxi = offeringTaxi.Value && IsTaxiOfferingItem(item.m_shared.m_name);

                if (!repair && !consumable && !thunderstrike && !trophy && !growth && !moderPower && !taxi)
                {
                    if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
                    {
                        __result = true;
                        return false;
                    }

                    return true;
                }

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
                List<Character> characters = new();
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
                initiator.Message(MessageHud.MessageType.Center, "$msg_offerdone".Localize());
            }
            else
            {
                initiator.Message(MessageHud.MessageType.Center, "$msg_offerwrong".Localize());
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
            initiator.Message(MessageHud.MessageType.Center, "$piece_incinerator_conversion".Localize());
        }

        private static void ApplyConsumableEffectToNearestPlayers(PrivateArea ward, ItemDrop.ItemData item, Player initiator)
        {
            if (item.m_shared.m_food > 0f)
            {
                if (wardIsHealing.ContainsKey(ward))
                {
                    initiator.Message(MessageHud.MessageType.Center, "$msg_offerwrong".Localize());
                    return;
                }

                wardIsHealing.Add(ward, 180);

                instance.StartCoroutine(PassiveHealingEffect(ward, amount: item.m_shared.m_foodRegen / 2, seconds: 1));
                LogInfo("Passive healing begins");

                initiator.Message(MessageHud.MessageType.Center, $"{"$msg_consumed".Localize()}: {item.m_shared.m_name}");
                initiator.GetInventory().RemoveOneItem(item);

                return;
            }

            if (!(bool)item.m_shared.m_consumeStatusEffect)
                return;

            LogInfo("Consumable effect offered");

            List<Player> players = new();

            ConnectedAreas(ward).Do(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            bool applied = false;

            foreach (Player player in players.ToHashSet().Where(player => player.CanConsumeItem(item)))
            {
                player.m_seman.AddStatusEffect(item.m_shared.m_consumeStatusEffect.NameHash(), resetTime: true);
                applied = true;
            }

            if (!applied)
            {
                initiator.Message(MessageHud.MessageType.Center, "$msg_cantoffer".Localize());
                return;
            }

            initiator.GetInventory().RemoveOneItem(item);
            initiator.Message(MessageHud.MessageType.Center, "$msg_offerdone".Localize());
        }

        private static void ApplyInstantGrowthEffectOnNearbyPlants(PrivateArea ward, ItemDrop.ItemData item, Player initiator, bool growableOnly = true)
        {
            List<Plant> plants = new();
            ConnectedAreas(ward).Do(area => GetPlantsInRange(area.transform.position, area.m_radius, plants, growableOnly));

            if (plants.Count == 0)
            {
                initiator.Message(MessageHud.MessageType.Center, "$msg_cantoffer".Localize());
                return;
            }

            Inventory inventory = initiator.GetInventory();
            if (item.m_shared.m_name == "$item_eitr" && inventory.CountItems("$item_eitr") < 5)
            {
                initiator.Message(MessageHud.MessageType.Center, "$msg_incompleteoffering".Localize());
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

            initiator.Message(MessageHud.MessageType.Center, "$msg_offerdone".Localize());
        }

        private static void ApplyModerPowerEffectToNearbyPlayers(PrivateArea ward, ItemDrop.ItemData item, Player initiator)
        {
            LogInfo("Dragon egg offered");

            List<Player> players = new();

            ConnectedAreas(ward).Do(area => Player.GetPlayersInRange(area.transform.position, area.m_radius, players));

            foreach (Player player in players.ToHashSet())
            {
                StatusEffect moderSE = ObjectDB.instance.GetStatusEffect(moderPowerHash);
                player.GetSEMan().AddStatusEffect(moderSE.NameHash(), resetTime: true);
            }

            initiator.GetInventory().RemoveOneItem(item);
        }

        private struct TaxiOffer
        {
            public string LocationName;
            public string ItemName;
            public int Stack;
            public bool ConsumeItem;
        }

        private const string EikthyrAltarLocation = "Eikthyrnir";
        private const string ElderAltarLocation = "GDKing";
        private const string BonemassAltarLocation = "Bonemass";
        private const string ModerAltarLocation = "Dragonqueen";
        private const string YagluthAltarLocation = "GoblinKing";
        private const string QueenAltarLocation = "Mistlands_DvergrBossEntrance1";
        private const string FaderAltarLocation = "FaderLocation";

        private static void TaxiToLocation(ItemDrop.ItemData item, Player initiator)
        {
            if (initiator.IsEncumbered())
            {
                initiator.Message(MessageHud.MessageType.Center, "$se_encumbered_start".Localize());
                return;
            }

            if (!IsTeleportable(initiator))
            {
                initiator.Message(MessageHud.MessageType.Center, "$pw_msg_notravel".Localize());
                return;
            }

            if (!canTravel)
            {
                initiator.Message(MessageHud.MessageType.Center, "$pw_msg_canttravel".Localize());
                return;
            }

            if (!TryGetTaxiOffer(item.m_shared.m_name, out TaxiOffer offer))
                return;

            Vector3 location = Vector3.zero;
            if (TryGetFoundLocation(offer.LocationName, initiator.transform.position, ref location))
            {
                StartTaxi(initiator, location, offer);
            }
            else if (!ZNet.instance.IsServer())
            {
                ClosestLocationRequest(offer);
            }
            else
            {
                LogInfo($"Location {offer.LocationName} is not found");
            }
        }

        internal static void RegisterRPCs()
        {
            if (ZNet.instance.IsServer())
                ZRoutedRpc.instance.Register<ZPackage>("ClosestLocationRequest", RPC_ClosestLocationRequest);
            else
                ZRoutedRpc.instance.Register<ZPackage>("StartTaxi", RPC_StartTaxi);
        }

        private static void ClosestLocationRequest(TaxiOffer offer)
        {
            LogInfo($"{offer.LocationName} closest location request");

            ZPackage zPackage = new();
            zPackage.Write(offer.LocationName);
            zPackage.Write(offer.ItemName);
            zPackage.Write(offer.Stack);

            ZRoutedRpc.instance.InvokeRoutedRPC("ClosestLocationRequest", zPackage);
        }

        public static void RPC_ClosestLocationRequest(long sender, ZPackage pkg)
        {
            // Server

            string name = pkg.ReadString();
            string itemName = pkg.ReadString().GetItemName();
            int stack = pkg.ReadInt();

            if (!TryGetTaxiRequester(sender, out RoutedPlayerContext requester))
                return;

            if (!IsValidServerTaxiRequest(name, itemName, stack))
                return;

            Vector3 target = Vector3.zero;
            if (!TryGetFoundLocation(name, requester.Position, ref target))
            {
                LogInfo($"Location {name} is not found");
                return;
            }

            ZPackage zPackage = new();
            zPackage.Write(target);
            zPackage.Write(name);
            zPackage.Write(itemName);
            zPackage.Write(stack);
            zPackage.Write(ShouldConsumeTaxiItem(name, itemName));

            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "StartTaxi", zPackage);
        }

        public static void RPC_StartTaxi(long sender, ZPackage pkg)
        {
            Vector3 location = pkg.ReadVector3();
            string locationName = pkg.ReadString();
            string itemName = pkg.ReadString().GetItemName();
            int stack = pkg.ReadInt();
            bool consumeItem = pkg.ReadBool();

            LogInfo("Server responded with closest location");
            StartTaxi(Player.m_localPlayer, location, new TaxiOffer { LocationName = locationName, ItemName = itemName, Stack = stack, ConsumeItem = consumeItem });
        }

        private static bool TryGetTaxiRequester(long sender, out RoutedPlayerContext requester)
        {
            requester = default;
            return sender != 0L && TryGetRoutedPlayer(sender, out requester);
        }

        private static bool IsValidServerTaxiRequest(string name, string itemName, int stack)
        {
            itemName = itemName.GetItemName();
            return name switch
            {
                "StartTemple" => offeringTaxiStartTempleEnabled.Value && IsBossTrophy(itemName) && stack == GetSacrificialStonesTaxiStack(),
                "Vendor_BlackForest" => offeringTaxiHaldorEnabled.Value && IsItemForHaldorTravel(itemName) && stack == GetExpectedHaldorTaxiPrice(),
                "Hildir_camp" => offeringTaxiHildirEnabled.Value && ((offeringTaxiHildirChestsEnabled.Value && IsHildirChestItem(itemName) && stack == 1) || (IsItemForHildirTravel(itemName) && stack == GetHildirTaxiPrice())),
                "BogWitch_Camp" => offeringTaxiBogWitchEnabled.Value && IsItemForBogWitchTravel(itemName) && stack == offeringTaxiPriceBogWitchAmount.Value,
                _ => IsValidBossAltarTaxiRequest(name, itemName, stack),
            };
        }

        private static int GetExpectedHaldorTaxiPrice()
        {
            return HasLocationIcon("Vendor_BlackForest")
                ? offeringTaxiPriceHaldorDiscovered.Value
                : offeringTaxiPriceHaldorUndiscovered.Value;
        }

        private static int GetHildirTaxiPrice() => Math.Max(offeringTaxiPriceHildirAmount.Value, 0);

        private static int GetSacrificialStonesTaxiStack() => offeringTaxiStartTempleConsumeItem.Value ? 1 : 0;

        private static bool HasLocationIcon(string name)
        {
            ZoneSystem.instance.tempIconList.Clear();
            ZoneSystem.instance.GetLocationIcons(ZoneSystem.instance.tempIconList);
            return ZoneSystem.instance.tempIconList.Any(icon => icon.Value == name);
        }

        private static void StartTaxi(Player initiator, Vector3 position, TaxiOffer offer)
        {
            if (Utils.DistanceXZ(initiator.transform.position, position) < 300f)
            {
                initiator.Message(MessageHud.MessageType.Center, "$pw_msg_tooclose".Localize());
                return;
            }

            if (!HasTaxiPayment(initiator, offer))
            {
                initiator.Message(MessageHud.MessageType.Center, "$msg_incompleteoffering".Localize());
                return;
            }

            initiator.StartCoroutine(TaxiToPosition(initiator, position, returnBack: offeringTaxiSecondsToFlyBack.Value > 0, waitSeconds: 10, offer: offer));
        }

        private static void StartTaxi(Player initiator, Vector3 position, string itemName, int stack)
        {
            StartTaxi(initiator, position, new TaxiOffer { LocationName = "", ItemName = itemName.GetItemName(), Stack = stack, ConsumeItem = true });
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

        private static bool IsTaxiOfferingItem(string itemName)
        {
            return TryGetTaxiOffer(itemName, out _);
        }

        private static bool TryGetTaxiOffer(string itemName, out TaxiOffer offer)
        {
            offer = default;
            itemName = itemName.GetItemName();

            if (offeringTaxiStartTempleEnabled.Value && IsBossTrophy(itemName))
            {
                offer = new TaxiOffer { LocationName = "StartTemple", ItemName = itemName, Stack = GetSacrificialStonesTaxiStack(), ConsumeItem = offeringTaxiStartTempleConsumeItem.Value };
                return true;
            }

            if (offeringTaxiHaldorEnabled.Value && IsItemForHaldorTravel(itemName))
            {
                offer = new TaxiOffer { LocationName = "Vendor_BlackForest", ItemName = itemName, Stack = GetExpectedHaldorTaxiPrice(), ConsumeItem = offeringTaxiHaldorConsumeItem.Value };
                return true;
            }

            if (offeringTaxiHildirEnabled.Value && offeringTaxiHildirChestsEnabled.Value && IsHildirChestItem(itemName))
            {
                offer = new TaxiOffer { LocationName = "Hildir_camp", ItemName = itemName, Stack = 1, ConsumeItem = false };
                return true;
            }

            if (offeringTaxiHildirEnabled.Value && IsItemForHildirTravel(itemName))
            {
                offer = new TaxiOffer { LocationName = "Hildir_camp", ItemName = itemName, Stack = GetHildirTaxiPrice(), ConsumeItem = offeringTaxiHildirConsumeItem.Value };
                return true;
            }

            if (offeringTaxiBogWitchEnabled.Value && IsItemForBogWitchTravel(itemName))
            {
                offer = new TaxiOffer { LocationName = "BogWitch_Camp", ItemName = itemName, Stack = offeringTaxiPriceBogWitchAmount.Value, ConsumeItem = offeringTaxiBogWitchConsumeItem.Value };
                return true;
            }

            return TryGetBossAltarTaxiOffer(itemName, out offer);
        }

        internal static bool IsItemForHaldorTravel(string itemName) => ConfiguredItemName(offeringTaxiPriceHaldorItem) != "" && itemName.GetItemName() == ConfiguredItemName(offeringTaxiPriceHaldorItem);

        internal static bool IsItemForHildirTravel(string itemName) => ConfiguredItemName(offeringTaxiPriceHildirItem) != "" && itemName.GetItemName() == ConfiguredItemName(offeringTaxiPriceHildirItem);

        internal static bool IsItemForBogWitchTravel(string itemName) => ConfiguredItemName(offeringTaxiPriceBogWitchItem) != "" && itemName.GetItemName() == ConfiguredItemName(offeringTaxiPriceBogWitchItem);

        private static string ConfiguredItemName(ConfigEntry<string> entry) => entry.Value.GetItemName();

        private static bool HasTaxiPayment(Player player, TaxiOffer offer)
        {
            return offer.Stack <= 0 || player.GetInventory().CountItems(offer.ItemName) >= offer.Stack;
        }

        private static bool TryConsumeTaxiPayment(Player player, TaxiOffer offer)
        {
            if (!HasTaxiPayment(player, offer))
                return false;

            if (offer.ConsumeItem && offer.Stack > 0)
                player.GetInventory().RemoveItem(offer.ItemName, offer.Stack);

            return true;
        }

        private static bool ShouldConsumeTaxiItem(string locationName, string itemName)
        {
            itemName = itemName.GetItemName();
            return locationName switch
            {
                "StartTemple" => offeringTaxiStartTempleConsumeItem.Value,
                "Vendor_BlackForest" => offeringTaxiHaldorConsumeItem.Value,
                "Hildir_camp" => !IsHildirChestItem(itemName) && offeringTaxiHildirConsumeItem.Value,
                "BogWitch_Camp" => offeringTaxiBogWitchConsumeItem.Value,
                _ => TryGetBossAltarConsumeItem(locationName, itemName, out bool consumeItem) && consumeItem,
            };
        }

        private static bool TryGetBossAltarTaxiOffer(string itemName, out TaxiOffer offer)
        {
            itemName = itemName.GetItemName();
            return TryGetBossAltarTaxiOffer(offeringTaxiEikthyrAltarEnabled, EikthyrAltarLocation, offeringTaxiEikthyrAltarItem, offeringTaxiEikthyrAltarAmount, offeringTaxiEikthyrAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiElderAltarEnabled, ElderAltarLocation, offeringTaxiElderAltarItem, offeringTaxiElderAltarAmount, offeringTaxiElderAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiBonemassAltarEnabled, BonemassAltarLocation, offeringTaxiBonemassAltarItem, offeringTaxiBonemassAltarAmount, offeringTaxiBonemassAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiModerAltarEnabled, ModerAltarLocation, offeringTaxiModerAltarItem, offeringTaxiModerAltarAmount, offeringTaxiModerAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiYagluthAltarEnabled, YagluthAltarLocation, offeringTaxiYagluthAltarItem, offeringTaxiYagluthAltarAmount, offeringTaxiYagluthAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiQueenAltarEnabled, QueenAltarLocation, offeringTaxiQueenAltarItem, offeringTaxiQueenAltarAmount, offeringTaxiQueenAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiFaderAltarEnabled, FaderAltarLocation, offeringTaxiFaderAltarItem, offeringTaxiFaderAltarAmount, offeringTaxiFaderAltarConsumeItem, itemName, out offer);
        }

        private static bool TryGetBossAltarTaxiOffer(ConfigEntry<bool> enabled, string locationName, ConfigEntry<string> item, ConfigEntry<int> amount, ConfigEntry<bool> consume, string itemName, out TaxiOffer offer)
        {
            offer = default;
            string configuredItem = ConfiguredItemName(item);
            if (!enabled.Value || configuredItem == "" || itemName != configuredItem)
                return false;

            offer = new TaxiOffer { LocationName = locationName, ItemName = configuredItem, Stack = Math.Max(amount.Value, 0), ConsumeItem = consume.Value };
            return true;
        }

        private static bool IsValidBossAltarTaxiRequest(string locationName, string itemName, int stack)
        {
            itemName = itemName.GetItemName();
            return IsValidBossAltarTaxiRequest(offeringTaxiEikthyrAltarEnabled, EikthyrAltarLocation, offeringTaxiEikthyrAltarItem, offeringTaxiEikthyrAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiElderAltarEnabled, ElderAltarLocation, offeringTaxiElderAltarItem, offeringTaxiElderAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiBonemassAltarEnabled, BonemassAltarLocation, offeringTaxiBonemassAltarItem, offeringTaxiBonemassAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiModerAltarEnabled, ModerAltarLocation, offeringTaxiModerAltarItem, offeringTaxiModerAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiYagluthAltarEnabled, YagluthAltarLocation, offeringTaxiYagluthAltarItem, offeringTaxiYagluthAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiQueenAltarEnabled, QueenAltarLocation, offeringTaxiQueenAltarItem, offeringTaxiQueenAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiFaderAltarEnabled, FaderAltarLocation, offeringTaxiFaderAltarItem, offeringTaxiFaderAltarAmount, locationName, itemName, stack);
        }

        private static bool IsValidBossAltarTaxiRequest(ConfigEntry<bool> enabled, string expectedLocationName, ConfigEntry<string> item, ConfigEntry<int> amount, string locationName, string itemName, int stack)
        {
            return enabled.Value && locationName == expectedLocationName && itemName == ConfiguredItemName(item) && stack == Math.Max(amount.Value, 0);
        }

        private static bool TryGetBossAltarConsumeItem(string locationName, string itemName, out bool consumeItem)
        {
            itemName = itemName.GetItemName();
            return TryGetBossAltarConsumeItem(EikthyrAltarLocation, offeringTaxiEikthyrAltarItem, offeringTaxiEikthyrAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(ElderAltarLocation, offeringTaxiElderAltarItem, offeringTaxiElderAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(BonemassAltarLocation, offeringTaxiBonemassAltarItem, offeringTaxiBonemassAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(ModerAltarLocation, offeringTaxiModerAltarItem, offeringTaxiModerAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(YagluthAltarLocation, offeringTaxiYagluthAltarItem, offeringTaxiYagluthAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(QueenAltarLocation, offeringTaxiQueenAltarItem, offeringTaxiQueenAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(FaderAltarLocation, offeringTaxiFaderAltarItem, offeringTaxiFaderAltarConsumeItem, locationName, itemName, out consumeItem);
        }

        private static bool TryGetBossAltarConsumeItem(string expectedLocationName, ConfigEntry<string> item, ConfigEntry<bool> consume, string locationName, string itemName, out bool consumeItem)
        {
            consumeItem = false;
            if (locationName != expectedLocationName || itemName != ConfiguredItemName(item))
                return false;

            consumeItem = consume.Value;
            return true;
        }

        private static void CancelTaxi(Player player, string messageToken)
        {
            player?.Message(MessageHud.MessageType.Center, messageToken.Localize());
            isTravelingPlayer = null;
            canTravel = true;
        }


        internal static bool IsBossTrophy(string itemName)
        {
            itemName = itemName.GetItemName();
            return itemName == "$item_trophy_eikthyr" ||
                   itemName == "$item_trophy_elder" ||
                   itemName == "$item_trophy_bonemass" ||
                   itemName == "$item_trophy_dragonqueen" ||
                   itemName == "$item_trophy_goblinking" ||
                   itemName == "$item_trophy_seekerqueen" ||
                   itemName == "$item_trophy_fader";
        }

        internal static bool IsHildirChestItem(string itemName)
        {
            itemName = itemName.GetItemName();
            return itemName == "$item_chest_hildir1" ||
                   itemName == "$item_chest_hildir2" ||
                   itemName == "$item_chest_hildir3";
        }

        public static bool IsTeleportable(Player player)
        {
            if (player.IsTeleportable())
            {
                return true;
            }

            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (IsHildirChestItem(item.m_shared.m_name))
                    continue;

                if (!item.m_shared.m_teleportable)
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerator TaxiToPosition(Player player, Vector3 position, bool returnBack = false, int waitSeconds = 0, TaxiOffer offer = default)
        {
            canTravel = false;
            isTravelingPlayer = player;

            if (waitSeconds > 0)
            {
                for (int i = waitSeconds; i > 0; i--)
                {
                    player.Message(MessageHud.MessageType.Center, "$pw_msg_travel_starting".Localize(TimeSpan.FromSeconds(i).ToString(@"m\:ss")));
                    yield return wait1sec;
                }
            }

            while (Valkyrie.m_instance != null)
            {
                player.Message(MessageHud.MessageType.Center, "$menu_pleasewait".Localize());
                yield return wait1sec;
            }

            DateTime flightInitiated = DateTime.Now;

            bool playerShouldExit = player.IsAttachedToShip() || player.IsAttached() || player.IsDead() || player.IsRiding() || player.IsSleeping() || player.IsTeleporting()
                                            || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();

            while (playerShouldExit || player.IsEncumbered() || !IsTeleportable(player))
            {
                string timeSpent = (DateTime.Now - flightInitiated).ToString(@"m\:ss");
                if (playerShouldExit)
                    player.Message(MessageHud.MessageType.TopLeft, "$pw_msg_travel_inside".Localize(timeSpent));
                else
                    player.Message(MessageHud.MessageType.Center, "$pw_msg_travel_blocked".Localize(timeSpent) + (player.IsEncumbered() ? " $se_encumbered_start" : " $msg_noteleport").Localize());

                yield return wait1sec;

                playerShouldExit = player.IsAttachedToShip() || player.IsAttached() || player.IsDead() || player.IsRiding() || player.IsSleeping() || player.IsTeleporting()
                                            || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();
            }

            bool assetLoaded = false;
            try
            {
                Player.m_localPlayer.m_valkyrie.Load();
                assetLoaded = true;

                GameObject valkyriePrefab = Player.m_localPlayer.m_valkyrie.Asset;
                if (valkyriePrefab == null || !valkyriePrefab.GetComponent<ZNetView>())
                    throw new InvalidOperationException("Failed to load taxi Valkyrie prefab.");

                if (!TryConsumeTaxiPayment(player, offer))
                {
                    CancelTaxi(player, "$msg_incompleteoffering");
                    yield break;
                }

                player.Message(MessageHud.MessageType.Center, "$pw_msg_travel_start".Localize());

                taxiTargetPosition = position;
                taxiReturnBack = returnBack;
                taxiPlayerPositionToReturn = player.transform.position;
                playerDropped = false;

                GameObject valkyrie = UnityEngine.Object.Instantiate(valkyriePrefab, player.transform.position, Quaternion.identity);
                if (valkyrie == null || !valkyrie.TryGetComponent(out ZNetView zNetView))
                    throw new InvalidOperationException("Failed to create taxi Valkyrie instance.");

                zNetView.HoldReferenceTo((IReferenceCounted)(object)Player.m_localPlayer.m_valkyrie);
            }
            catch (Exception e)
            {
                LogInfo($"Taxi start failed: {e}");
                CancelTaxi(player, "$pw_msg_canttravel");
                yield break;
            }
            finally
            {
                if (assetLoaded)
                    Player.m_localPlayer.m_valkyrie.Release();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetIntro))]
        public static class Player_SetIntro_Taxi
        {
            static void Postfix(Player __instance)
            {
                if (__instance.InIntro())
                    return;

                canTravel = true;

                if (taxiReturnBack && offeringTaxiSecondsToFlyBack.Value > 0)
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
            private static void Prefix() => canTravel = true;
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        public static class ZoneSystem_Start_Taxi
        {
            private static void Postfix()
            {
                RegisterRPCs();
            }
        }
    }
}
