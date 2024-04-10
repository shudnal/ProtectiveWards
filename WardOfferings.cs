using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal class WardOfferings
    {
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

        private static void GetPlantsInRange(Vector3 point, float radius, List<Plant> plants, bool growableOnly)
        {
            List<SlowUpdate> allPlants = SlowUpdate.GetAllInstaces();

            float num = radius * radius;
            foreach (SlowUpdate su_plant in allPlants)
            {
                if (!su_plant.TryGetComponent<Plant>(out Plant plant)) continue;

                if (Utils.DistanceSqr(su_plant.transform.position, point) < num && plant.m_nview.IsOwner() && (!growableOnly || plant.m_status == 0))
                    plants.Add(plant);
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

                instance.StartCoroutine(PassiveHealingEffect(ward, amount: item.m_shared.m_foodRegen / 2, seconds: 1));
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
            }
            else if (item.m_shared.m_name == "$item_trophy_eikthyr" ||
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
            }
            else
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
                else if (item.m_shared.m_name == "$item_coins")
                {
                    int stack = targetingClosest ? 2000 : 500;
                    if (initiator.GetInventory().CountItems("$item_coins") < stack)
                    {
                        initiator.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_incompleteoffering"));
                        Player.m_localPlayer.SetLookDir(location.m_position - initiator.transform.position, 3.5f);
                        return;
                    }
                    initiator.GetInventory().RemoveItem("$item_coins", stack);
                }
            }

            initiator.StartCoroutine(TaxiToPosition(initiator, location.m_position, returnBack: true, waitSeconds: 10));
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
                UnityEngine.Object.Instantiate(prefab, player.transform.position, Quaternion.identity);
            }

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
            private static void Postfix()
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

                if (!offeringTaxi.Value)
                    return;

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
                if (!modEnabled.Value)
                    return;

                canTravel = true;
            }
        }

    }
}
