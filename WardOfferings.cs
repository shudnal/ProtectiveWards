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

                bool taxi = offeringTaxi.Value && WardTaxi.IsTaxiOfferingItem(item.m_shared.m_name);

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
                    WardTaxi.TryStartTaxiFromOffering(item, player, __instance.transform.position);

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
    }
}
