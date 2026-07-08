using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal class FullProtection
    {
        private const string RPC_SetLastSaddleUser = "PW_SetLastSaddleUser";
        private const string RPC_SetLastVehicleController = "PW_SetLastVehicleController";
        private const string RPC_CheckTeleportTargetAccess = "PW_CheckTeleportTargetAccess";
        private const string RPC_TeleportTargetAccessResponse = "PW_TeleportTargetAccessResponse";
        private const float saddleUserRecordMaxDistance = 10f;
        private const float vehicleControllerRecordMaxDistance = 10f;
        private static int s_privateAreaCheckBypassDepth;
        private static bool s_saddleRpcRegistered;
        private static bool s_teleportAccessRpcRegistered;
        private static bool s_interactablePatchesApplied;
        private static Player s_autoPickupPlayer;

        private static bool BlockProtectedInteraction(Component component, Humanoid human, ref bool result)
        {
            if (!BlockUnauthorizedWardInteraction(component, human))
                return false;

            result = true;
            return true;
        }

        private static bool ShouldBlockProtectedInteraction(Component component, Humanoid human)
        {
            return human != null && BlockUnauthorizedWardInteraction(component, human);
        }

        private static bool ShouldSilentlyBlockProtectedInteraction(Component component, Humanoid human)
        {
            if (component == null || human is not Player player)
                return false;

            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (!IsActivePlayerWard(area) || !area.IsInside(component.transform.position, 0f))
                    continue;

                if (HasAccessToWardOrConnectedWard(area, player))
                    continue;

                if (IsObjectOwnedByPlayerWithWardAccess(component, player))
                    continue;

                return true;
            }

            return false;
        }

        private static void StartPrivateAreaCheckBypass(Component component, Humanoid human, ref bool state)
        {
            if (!ShouldBypassVanillaPrivateAreaCheck(component, human))
                return;

            s_privateAreaCheckBypassDepth++;
            state = true;
        }

        private static void StopPrivateAreaCheckBypass(bool state)
        {
            if (!state)
                return;

            s_privateAreaCheckBypassDepth = Math.Max(0, s_privateAreaCheckBypassDepth - 1);
        }

        private static void RegisterTeleportAccessRPC()
        {
            if (s_teleportAccessRpcRegistered || ZRoutedRpc.instance == null)
                return;

            if (ZNet.instance != null && ZNet.instance.IsServer())
                ZRoutedRpc.instance.Register<ZPackage>(RPC_CheckTeleportTargetAccess, RPC_CheckTeleportTargetAccessServer);

            ZRoutedRpc.instance.Register<ZPackage>(RPC_TeleportTargetAccessResponse, RPC_TeleportTargetAccessResponseClient);
            s_teleportAccessRpcRegistered = true;
        }

        private static void ResetRPCRegistration()
        {
            s_saddleRpcRegistered = false;
            s_teleportAccessRpcRegistered = false;
        }

        internal static void PatchLoadedInteractables(Harmony harmony)
        {
            if (s_interactablePatchesApplied || harmony == null)
                return;

            MethodInfo prefix = AccessTools.Method(typeof(Interactable_PreventUnauthorizedAccess), nameof(Interactable_PreventUnauthorizedAccess.Prefix));
            if (prefix == null)
                return;

            foreach (MethodBase method in Interactable_PreventUnauthorizedAccess.TargetMethods())
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));

            s_interactablePatchesApplied = true;
        }

        internal static void ResetDynamicPatchState() => s_interactablePatchesApplied = false;

        private static bool TeleportTargetAccessCheckStarted(TeleportWorld teleport, Player player)
        {
            if (player == null || teleport?.TargetFound() != true || teleport.m_nview?.IsValid() != true)
                return false;

            ZDO sourceZdo = teleport.m_nview.GetZDO();
            if (sourceZdo == null)
                return false;

            ZPackage package = new();
            package.Write(sourceZdo.m_uid);
            package.Write(player.GetPlayerID());

            if (ZNet.instance != null && ZNet.instance.IsServer())
                RPC_CheckTeleportTargetAccessServer(0L, new(package.GetArray()));
            else if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_CheckTeleportTargetAccess, package);
            else
                return false;

            return true;
        }

        private static void RPC_CheckTeleportTargetAccessServer(long sender, ZPackage package)
        {
            ZDOID sourceZdoID = package.ReadZDOID();
            long playerID = package.ReadLong();

            if (ZDOMan.instance == null)
                return;

            if (!TryGetRoutedPlayer(sender, playerID, out RoutedPlayerContext requester))
                return;

            ZDO sourceZdo = ZDOMan.instance.GetZDO(sourceZdoID);
            if (sourceZdo == null)
            {
                SendTeleportTargetAccessResponse(sender, sourceZdoID, granted: true, blockingOwnerName: "");
                return;
            }

            ZDOID targetZdoID = sourceZdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);
            ZDO targetZdo = ZDOMan.instance.GetZDO(targetZdoID);
            if (targetZdo == null)
            {
                SendTeleportTargetAccessResponse(sender, sourceZdoID, granted: true, blockingOwnerName: "");
                return;
            }

            bool granted = IsTeleportTargetAccessibleToPlayer(targetZdo.GetPosition(), requester.PlayerID, out string blockingOwnerName);
            SendTeleportTargetAccessResponse(sender, sourceZdoID, granted, blockingOwnerName);
        }

        private static void SendTeleportTargetAccessResponse(long peerID, ZDOID sourceZdoID, bool granted, string blockingOwnerName)
        {
            ZPackage response = new();
            response.Write(sourceZdoID);
            response.Write(granted);
            response.Write(blockingOwnerName ?? "");

            if (ZNet.instance != null && ZNet.instance.IsServer() && ZRoutedRpc.instance != null && peerID != 0L)
                ZRoutedRpc.instance.InvokeRoutedRPC(peerID, RPC_TeleportTargetAccessResponse, response);
            else
                RPC_TeleportTargetAccessResponseClient(0L, new(response.GetArray()));
        }

        private static void RPC_TeleportTargetAccessResponseClient(long sender, ZPackage package)
        {
            ZDOID sourceZdoID = package.ReadZDOID();
            bool granted = package.ReadBool();
            string blockingOwnerName = package.ReadString();

            Player player = Player.m_localPlayer;
            if (player == null || ZNetScene.instance == null)
                return;

            GameObject instance = ZNetScene.instance.FindInstance(sourceZdoID);
            TeleportWorld teleport = instance?.GetComponent<TeleportWorld>();
            if (teleport == null)
                return;

            if (!granted)
            {
                player.Message(MessageHud.MessageType.Center, GetPrivateZoneDeniedMessage(blockingOwnerName));
                return;
            }

            teleport.Teleport(player);
        }

        private static bool IsTeleportTargetAccessibleToPlayer(Vector3 targetPoint, long playerID, out string blockingOwnerName)
        {
            blockingOwnerName = "";

            if (playerID == 0L)
                return true;

            WardConnectedAccessMode mode = wardAccessConnectedAccessMode?.Value ?? WardConnectedAccessMode.Off;

            foreach (ZDO zdo in WardZdoUtils.GetAllWards())
            {
                if (!IsActiveWardZdo(zdo))
                    continue;

                if (Utils.DistanceXZ(zdo.GetPosition(), targetPoint) > zdo.GetWardRadius())
                    continue;

                if (zdo.HasConnectedWardAccess(playerID, mode, IsActiveWardZdo))
                    continue;

                blockingOwnerName = GetWardOwnerName(zdo);
                return false;
            }

            return true;
        }

        private static bool IsActiveWardZdo(ZDO zdo)
        {
            return zdo.IsWard()
                   && zdo.GetBool(ZDOVars.s_enabled, false)
                   && !zdo.GetBool(WardExpiration.s_expirationExpired, false);
        }

        private static void RegisterSaddleUserRPC()
        {
            if (s_saddleRpcRegistered || ZRoutedRpc.instance == null)
                return;

            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                ZRoutedRpc.instance.Register<ZPackage>(RPC_SetLastSaddleUser, RPC_SetLastSaddleUserServer);
                ZRoutedRpc.instance.Register<ZPackage>(RPC_SetLastVehicleController, RPC_SetLastVehicleControllerServer);
            }

            s_saddleRpcRegistered = true;
        }

        private static void RequestSetLastSaddleUser(Sadle sadle, long playerID)
        {
            ZNetView nview = sadle.GetComponentZNetView();
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            if (zdo == null || playerID == 0L)
                return;

            ZPackage package = new();
            package.Write(zdo.m_uid);
            package.Write(playerID);

            if (ZNet.instance != null && ZNet.instance.IsServer())
                RPC_SetLastSaddleUserServer(0L, new(package.GetArray()));
            else
                ZRoutedRpc.instance?.InvokeRoutedRPC(RPC_SetLastSaddleUser, package);
        }

        private static void RPC_SetLastSaddleUserServer(long sender, ZPackage package)
        {
            ZDOID zdoID = package.ReadZDOID();
            long playerID = package.ReadLong();
            if (playerID == 0L || ZDOMan.instance == null)
                return;

            if (!TryGetRoutedPlayer(sender, playerID, out RoutedPlayerContext requester))
                return;

            ZDO zdo = ZDOMan.instance.GetZDO(zdoID);
            if (zdo == null)
                return;

            long currentUser = zdo.GetLong(ZDOVars.s_user, 0L);
            if (currentUser != requester.CharacterID.UserID
                && (!requester.HasPosition || Vector3.Distance(requester.Position, zdo.GetPosition()) > saddleUserRecordMaxDistance))
                return;

            zdo.Set(s_lastSaddleUser, requester.PlayerID);
        }

        private static void SetLastSaddleUserLocal(Sadle sadle, long playerID)
        {
            if (sadle == null || playerID == 0L)
                return;

            SetLastSaddleUserOnView(sadle.GetComponentZNetView(), playerID);

            if (sadle.m_character != null)
                SetLastSaddleUserOnView(sadle.m_character.GetComponentZNetView(), playerID);
        }

        private static void SetLastSaddleUserOnView(ZNetView nview, long playerID)
        {
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            if (zdo != null && playerID != 0L)
                zdo.Set(s_lastSaddleUser, playerID);
        }

        private static void RequestSetLastVehicleController(ZNetView nview, long playerID)
        {
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            if (zdo == null || playerID == 0L)
                return;

            ZPackage package = new();
            package.Write(zdo.m_uid);
            package.Write(playerID);

            if (ZNet.instance != null && ZNet.instance.IsServer())
                RPC_SetLastVehicleControllerServer(0L, new(package.GetArray()));
            else
                ZRoutedRpc.instance?.InvokeRoutedRPC(RPC_SetLastVehicleController, package);
        }

        private static void RPC_SetLastVehicleControllerServer(long sender, ZPackage package)
        {
            ZDOID zdoID = package.ReadZDOID();
            long playerID = package.ReadLong();
            if (playerID == 0L || ZDOMan.instance == null || ZNetScene.instance == null)
                return;

            if (!TryGetRoutedPlayer(sender, playerID, out RoutedPlayerContext requester))
                return;

            ZDO zdo = ZDOMan.instance.GetZDO(zdoID);
            if (zdo == null)
                return;

            GameObject instance = ZNetScene.instance.FindInstance(zdoID);
            if (instance == null)
                return;

            if (instance.GetComponent<Ship>() != null)
            {
                if (zdo.GetLong(ZDOVars.s_user, 0L) != requester.PlayerID
                    && (!requester.HasPosition || Vector3.Distance(requester.Position, zdo.GetPosition()) > vehicleControllerRecordMaxDistance))
                    return;
            }
            else if (instance.GetComponent<Vagon>() != null)
            {
                if (!requester.HasPosition || Vector3.Distance(requester.Position, zdo.GetPosition()) > vehicleControllerRecordMaxDistance)
                    return;
            }
            else
            {
                return;
            }

            zdo.Set(s_lastVehicleController, requester.PlayerID);
        }

        private static void SetLastVehicleControllerLocal(ZNetView nview, long playerID)
        {
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            if (zdo != null && playerID != 0L)
                zdo.Set(s_lastVehicleController, playerID);
        }

        private static Component GetProtectedSwitchTarget(Switch sw)
        {
            if (sw == null)
                return null;

            ArmorStand armorStand = sw.GetComponentInParent<ArmorStand>();
            if (armorStand != null && wardAccessProtectItemStands.Value)
                return armorStand;

            MapTable mapTable = sw.GetComponentInParent<MapTable>();
            if (mapTable != null && wardAccessProtectMapTables.Value)
                return mapTable;

            Catapult catapult = sw.GetComponentInParent<Catapult>();
            if (catapult != null && wardAccessProtectCatapults.Value)
                return catapult;

            Barber barber = sw.GetComponentInParent<Barber>();
            if (barber != null && wardAccessProtectBarbers.Value)
                return barber;

            CraftingStation craftingStation = sw.GetComponentInParent<CraftingStation>();
            if (craftingStation != null && wardAccessProtectCraftingStations.Value)
                return craftingStation;

            Fireplace fireplace = sw.GetComponentInParent<Fireplace>();
            if (fireplace != null && wardAccessProtectFireplaces.Value)
                return fireplace;

            Turret turret = sw.GetComponentInParent<Turret>();
            if (turret != null && wardAccessProtectTurrets.Value)
                return turret;

            if (wardAccessProtectProductionStations.Value)
            {
                CookingStation cookingStation = sw.GetComponentInParent<CookingStation>();
                if (cookingStation != null)
                    return cookingStation;

                Smelter smelter = sw.GetComponentInParent<Smelter>();
                if (smelter != null)
                    return smelter;

                Fermenter fermenter = sw.GetComponentInParent<Fermenter>();
                if (fermenter != null)
                    return fermenter;

                Beehive beehive = sw.GetComponentInParent<Beehive>();
                if (beehive != null)
                    return beehive;

                SapCollector sapCollector = sw.GetComponentInParent<SapCollector>();
                if (sapCollector != null)
                    return sapCollector;
            }

            return null;
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.CheckAccess))]
        public static class PrivateArea_CheckAccess_ScopedWardAccessBypass
        {
            private static bool Prefix(ref bool __result)
            {
                if (s_privateAreaCheckBypassDepth <= 0)
                    return true;

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.AddFireDamage))]
        public static class Character_AddFireDamage_IndirectFireDamageProtection
        {
            private static void Prefix(Character __instance, ref float damage)
            {
                if (boarsHensProtection.Value && __instance.IsTamed() && _boarsHensProtectionGroupList.Contains(__instance.m_group.ToLower()) && InsideEnabledPlayersArea(__instance.transform.position))
                    damage = 0f;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.UpdateSmoke))]
        public static class Character_UpdateSmoke_IndirectSmokeDamageProtection
        {
            private static void Prefix(Character __instance, ref float dt)
            {
                if (boarsHensProtection.Value && __instance.IsTamed() && _boarsHensProtectionGroupList.Contains(__instance.m_group.ToLower()) && InsideEnabledPlayersArea(__instance.transform.position))
                    dt = 0f;
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
        public static class WearNTear_UpdateWear_RainProtection
        {
            private static void Prefix(WearNTear __instance, ref bool ___m_noRoofWear, ref bool __state)
            {
                if (__instance == null || wardRainProtection == null || !wardRainProtection.Value)
                    return;

                if (!___m_noRoofWear)
                    return;

                if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !InsideEnabledPlayersArea(__instance.transform.position, checkCache: true))
                    return;

                __state = ___m_noRoofWear;

                ___m_noRoofWear = false;
            }

            private static void Postfix(ref bool ___m_noRoofWear, bool __state)
            {
                if (wardRainProtection == null || !wardRainProtection.Value)
                    return;

                if (__state != true)
                    return;

                ___m_noRoofWear = __state;
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateUpsideDmg))]
        public static class Ship_UpdateUpsideDmg_PreventShipDamage
        {
            private static bool Prefix(Ship __instance)
            {
                if (__instance == null || wardShipProtection == null || wardShipProtection.Value == ShipDamageType.Off)
                    return true;

                return !InsideEnabledPlayersArea(__instance.transform.position);
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateWaterForce))]
        public static class Ship_UpdateWaterForce_PreventShipDamage
        {
            private static void Prefix(Ship __instance, ref float __state)
            {
                if (__instance == null || wardShipProtection == null || wardShipProtection.Value == ShipDamageType.Off)
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position))
                    return;

                __state = __instance.m_waterImpactDamage;
                __instance.m_waterImpactDamage = 0f;
            }

            private static void Postfix(Ship __instance, float __state)
            {
                if (__state == 0f)
                    return;

                __instance.m_waterImpactDamage = __state;
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
        public static class Container_Interact_PreventUnauthorizedAccess
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Container __instance, Humanoid character, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectChests.Value)
                    return true;

                if (!BlockUnauthorizedWardInteraction(__instance, character))
                {
                    if (__instance.m_checkGuardStone)
                        StartPrivateAreaCheckBypass(__instance, character, ref __state);

                    return true;
                }

                __result = true;
                return false;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(Door), nameof(Door.Interact))]
        public static class Door_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Door __instance, Humanoid character, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectDoors.Value)
                    return true;

                if (!BlockUnauthorizedWardInteraction(__instance, character))
                {
                    if (__instance.m_checkGuardStone)
                        StartPrivateAreaCheckBypass(__instance, character, ref __state);

                    return true;
                }

                __result = true;
                return false;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
        public static class Pickable_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Pickable __instance, Humanoid character, ref bool __result)
            {
                if (!wardAccessProtectPlants.Value)
                    return true;

                if (!BlockUnauthorizedWardInteraction(__instance, character))
                    return true;

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.Interact))]
        public static class ShipControlls_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(ShipControlls __instance, Humanoid character, ref bool __result)
            {
                if (!wardAccessProtectBoats.Value)
                    return true;

                if (!BlockUnauthorizedWardInteraction(__instance, character))
                    return true;

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Sadle), nameof(Sadle.Interact))]
        public static class Sadle_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Sadle __instance, Humanoid character, ref bool __result)
            {
                if (!wardAccessProtectTames.Value)
                    return true;

                if (!BlockUnauthorizedWardInteraction(__instance, character))
                    return true;

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
        public static class Tameable_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Tameable __instance, Humanoid user, bool hold, ref bool __result)
            {
                if (!wardAccessProtectTames.Value)
                    return true;

                if (hold)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.UseItem))]
        public static class Tameable_UseItem_PreventUnauthorizedAccess
        {
            private static bool Prefix(Tameable __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
            {
                if (!wardAccessProtectTames.Value)
                    return true;

                if (!IsProtectedTameUseItem(__instance, item))
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }

            private static bool IsProtectedTameUseItem(Tameable tameable, ItemDrop.ItemData item)
            {
                if (tameable == null || item == null || tameable.m_saddleItem == null || tameable.m_saddleItem.m_itemData == null)
                    return false;

                return tameable.IsTamed() && item.m_shared.m_name == tameable.m_saddleItem.m_itemData.m_shared.m_name;
            }
        }

        [HarmonyPatch(typeof(Petable), nameof(Petable.Interact))]
        public static class Petable_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Petable __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectTames.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Pet), nameof(Pet.Interact))]
        public static class Pet_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Pet __instance, Humanoid user, bool hold, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectTames.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                {
                    if (IsProtectedPetItemStandInteraction(__instance, hold))
                        StartPrivateAreaCheckBypass(__instance.m_itemStand, user, ref __state);

                    return true;
                }

                return false;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }

            private static bool IsProtectedPetItemStandInteraction(Pet pet, bool hold)
            {
                return hold && pet != null && pet.m_itemStand != null && pet.m_itemStand.HaveAttachment();
            }
        }

        [HarmonyPatch(typeof(Pet), nameof(Pet.UseItem))]
        public static class Pet_UseItem_PreventUnauthorizedAccess
        {
            private static bool Prefix(Pet __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
            {
                if (!wardAccessProtectTames.Value)
                    return true;

                if (!IsProtectedPetUseItem(__instance, item))
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }

            private static bool IsProtectedPetUseItem(Pet pet, ItemDrop.ItemData item)
            {
                if (pet == null || item == null || pet.m_FeedItem == null || pet.m_FeedItem.m_itemData == null)
                    return false;

                return item.m_shared.m_name == pet.m_FeedItem.m_itemData.m_shared.m_name;
            }
        }

        [HarmonyPatch(typeof(Vagon), nameof(Vagon.Interact))]
        public static class Vagon_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Vagon __instance, Humanoid character, ref bool __result)
            {
                if (!wardAccessProtectCarts.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, character, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Feast), nameof(Feast.Interact))]
        public static class Feast_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Feast __instance, Humanoid human, ref bool __result)
            {
                if (!wardAccessProtectFood.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, human, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Interact))]
        public static class ItemDrop_Interact_PreventUnauthorizedFoodAccess
        {
            private static bool Prefix(ItemDrop __instance, Humanoid character, bool alt, ref bool __result)
            {
                if (!wardAccessProtectFood.Value)
                    return true;

                if (alt || !IsPlacedConsumable(__instance))
                    return true;

                if (!BlockProtectedInteraction(__instance, character, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Pickup))]
        public static class ItemDrop_Pickup_PreventUnauthorizedAccess
        {
            private static bool Prefix(ItemDrop __instance, Humanoid character)
            {
                return !ShouldBlockItemPickup(__instance, character);
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup), new Type[] { typeof(GameObject), typeof(bool), typeof(bool) })]
        public static class Humanoid_Pickup_PreventUnauthorizedAccess
        {
            private static bool Prefix(GameObject go, Humanoid __instance)
            {
                return !ShouldBlockItemPickup(go?.GetComponent<ItemDrop>(), __instance);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.AutoPickup))]
        public static class Player_AutoPickup_PreventBlockedItemPull
        {
            private static void Prefix(Player __instance)
            {
                s_autoPickupPlayer = __instance;
            }

            private static void Finalizer(Player __instance)
            {
                if (s_autoPickupPlayer == __instance)
                    s_autoPickupPlayer = null;
            }
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.InTar))]
        public static class ItemDrop_InTar_PreventBlockedAutoPickupPull
        {
            private static void Postfix(ItemDrop __instance, ref bool __result)
            {
                if (__result || s_autoPickupPlayer == null)
                    return;

                if (ShouldBlockItemPickup(__instance, s_autoPickupPlayer, silent: true))
                    __result = true;
            }
        }

        [HarmonyPatch(typeof(ShieldGenerator), nameof(ShieldGenerator.OnAddFuel))]
        public static class ShieldGenerator_OnAddFuel_PreventUnauthorizedAccess
        {
            private static bool Prefix(ShieldGenerator __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectShieldGenerators.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Incinerator), nameof(Incinerator.OnIncinerate))]
        public static class Incinerator_OnIncinerate_PreventUnauthorizedAccess
        {
            private static bool Prefix(Incinerator __instance, Humanoid user, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectIncinerators.Value)
                    return true;

                if (BlockProtectedInteraction(__instance, user, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(__instance, user, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        private static bool IsPlacedConsumable(ItemDrop itemDrop)
        {
            return IsConsumableItem(itemDrop) && itemDrop.IsPiece();
        }

        private static bool IsConsumableItem(ItemDrop itemDrop)
        {
            return itemDrop != null
                   && itemDrop.m_itemData != null
                   && itemDrop.m_itemData.m_shared != null
                   && itemDrop.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable;
        }

        private static bool ShouldBlockItemPickup(ItemDrop itemDrop, Humanoid character, bool silent = false)
        {
            if (itemDrop == null || IsConsumableItem(itemDrop))
                return false;

            switch (wardAccessProtectItemPickupMode.Value)
            {
                case WardItemPickupMode.AllowAll:
                    return false;
                case WardItemPickupMode.AllowNonPlayerDropped:
                    if (itemDrop.m_autoPickup)
                        return false;
                    break;
                case WardItemPickupMode.BlockAll:
                    break;
                default:
                    return false;
            }

            return silent
                ? ShouldSilentlyBlockProtectedInteraction(itemDrop, character)
                : BlockUnauthorizedWardInteraction(itemDrop, character);
        }


        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Interact))]
        public static class TeleportWorld_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(TeleportWorld __instance, Humanoid human, bool hold, ref bool __result, ref bool __state)
            {
                if (wardAccessProtectPortals.Value == WardPortalAccessMode.AllowAll)
                    return true;

                if (hold)
                    return true;

                if (BlockProtectedInteraction(__instance, human, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(__instance, human, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.SetText))]
        public static class TeleportWorld_SetText_PreventUnauthorizedAccess
        {
            private static bool Prefix(TeleportWorld __instance)
            {
                if (wardAccessProtectPortals.Value == WardPortalAccessMode.AllowAll)
                    return true;

                Player player = Player.m_localPlayer;
                if (player == null)
                    return true;

                return !BlockUnauthorizedWardInteraction(__instance, player);
            }
        }

        [HarmonyPatch(typeof(TeleportWorldTrigger), nameof(TeleportWorldTrigger.OnTriggerEnter))]
        public static class TeleportWorldTrigger_OnTriggerEnter_PreventUnauthorizedTargetAccess
        {
            private static bool Prefix(TeleportWorldTrigger __instance, Collider colliderIn)
            {
                if (wardAccessProtectPortals.Value != WardPortalAccessMode.BlockAll)
                    return true;

                if (__instance == null || __instance.m_teleportWorld == null || colliderIn == null)
                    return true;

                Player player = colliderIn.GetComponent<Player>();
                if (player == null || Player.m_localPlayer != player)
                    return true;

                if (BlockUnauthorizedWardInteraction(__instance.m_teleportWorld, player))
                    return false;

                if (TeleportTargetAccessCheckStarted(__instance.m_teleportWorld, player))
                    return false;

                return true;
            }
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.Interact))]
        public static class CookingStation_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(CookingStation __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.UseItem))]
        public static class CookingStation_UseItem_PreventUnauthorizedAccess
        {
            private static bool Prefix(CookingStation __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnAddFuelSwitch))]
        public static class CookingStation_OnAddFuelSwitch_PreventUnauthorizedAccess
        {
            private static bool Prefix(CookingStation __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnAddFoodSwitch))]
        public static class CookingStation_OnAddFoodSwitch_PreventUnauthorizedAccess
        {
            private static bool Prefix(CookingStation __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddOre))]
        public static class Smelter_OnAddOre_PreventUnauthorizedAccess
        {
            private static bool Prefix(Smelter __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnEmpty))]
        public static class Smelter_OnEmpty_PreventUnauthorizedAccess
        {
            private static bool Prefix(Smelter __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddFuel))]
        public static class Smelter_OnAddFuel_PreventUnauthorizedAccess
        {
            private static bool Prefix(Smelter __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.Interact))]
        public static class Fermenter_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Fermenter __instance, Humanoid user, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (BlockProtectedInteraction(__instance, user, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(__instance, user, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.UseItem))]
        public static class Fermenter_UseItem_PreventUnauthorizedAccess
        {
            private static bool Prefix(Fermenter __instance, Humanoid user, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (BlockProtectedInteraction(__instance, user, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(__instance, user, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(Beehive), nameof(Beehive.Interact))]
        public static class Beehive_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Beehive __instance, Humanoid character, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (BlockProtectedInteraction(__instance, character, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(__instance, character, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(SapCollector), nameof(SapCollector.Interact))]
        public static class SapCollector_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(SapCollector __instance, Humanoid character, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectProductionStations.Value)
                    return true;

                if (BlockProtectedInteraction(__instance, character, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(__instance, character, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.Interact))]
        public static class ItemStand_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(ItemStand __instance, Humanoid user, bool hold, bool alt, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectItemStands.Value)
                    return true;

                if (!IsProtectedItemStandInteraction(__instance, hold, alt))
                    return true;

                if (BlockProtectedInteraction(__instance, user, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(__instance, user, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }

            private static bool IsProtectedItemStandInteraction(ItemStand itemStand, bool hold, bool alt)
            {
                if (itemStand == null)
                    return false;

                if (!itemStand.HaveAttachment())
                    return itemStand.m_autoAttach && itemStand.m_supportedItems.Count == 1;

                if (hold && itemStand.m_canBeRemoved)
                    return true;

                return alt && itemStand.m_visualItemDrop != null && itemStand.m_orientationSettings.Count > 1;
            }
        }

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.UseItem))]
        public static class ItemStand_UseItem_PreventUnauthorizedAccess
        {
            private static bool Prefix(ItemStand __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
            {
                if (!wardAccessProtectItemStands.Value)
                    return true;

                if (item == null || __instance.HaveAttachment() || !__instance.CanAttach(item))
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
        public static class Switch_Interact_PreventUnauthorizedExplicitAccess
        {
            private static bool Prefix(Switch __instance, Humanoid character, ref bool __result, ref bool __state)
            {
                Component target = GetProtectedSwitchTarget(__instance);
                if (target == null)
                    return true;

                if (BlockProtectedInteraction(target, character, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(target, character, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(Switch), nameof(Switch.UseItem))]
        public static class Switch_UseItem_PreventUnauthorizedExplicitAccess
        {
            private static bool Prefix(Switch __instance, Humanoid user, ref bool __result, ref bool __state)
            {
                Component target = GetProtectedSwitchTarget(__instance);
                if (target == null)
                    return true;

                if (BlockProtectedInteraction(target, user, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(target, user, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(ArmorStand), nameof(ArmorStand.UseItem))]
        public static class ArmorStand_UseItem_PreventUnauthorizedAccess
        {
            private static bool Prefix(ArmorStand __instance, Humanoid user, ref bool __result, ref bool __state)
            {
                if (!wardAccessProtectItemStands.Value)
                    return true;

                if (BlockProtectedInteraction(__instance, user, ref __result))
                    return false;

                StartPrivateAreaCheckBypass(__instance, user, ref __state);
                return true;
            }

            private static void Finalizer(bool __state)
            {
                StopPrivateAreaCheckBypass(__state);
            }
        }

        [HarmonyPatch(typeof(MapTable), nameof(MapTable.OnRead), new Type[] { typeof(Switch), typeof(Humanoid), typeof(ItemDrop.ItemData), typeof(bool) })]
        public static class MapTable_OnRead_PreventUnauthorizedAccess
        {
            private static bool Prefix(MapTable __instance, Humanoid user)
            {
                if (!wardAccessProtectMapTables.Value)
                    return true;

                return !ShouldBlockProtectedInteraction(__instance, user);
            }
        }

        [HarmonyPatch(typeof(MapTable), nameof(MapTable.OnWrite))]
        public static class MapTable_OnWrite_PreventUnauthorizedAccess
        {
            private static bool Prefix(MapTable __instance, Humanoid user)
            {
                if (!wardAccessProtectMapTables.Value)
                    return true;

                return !ShouldBlockProtectedInteraction(__instance, user);
            }
        }

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
        public static class Fireplace_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Fireplace __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectFireplaces.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.UseItem))]
        public static class Fireplace_UseItem_PreventUnauthorizedAccess
        {
            private static bool Prefix(Fireplace __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectFireplaces.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.Interact))]
        public static class Turret_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Turret __instance, Humanoid character, ref bool __result)
            {
                if (!wardAccessProtectTurrets.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, character, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.UseItem))]
        public static class Turret_UseItem_PreventUnauthorizedAccess
        {
            private static bool Prefix(Turret __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectTurrets.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Interact))]
        public static class CraftingStation_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(CraftingStation __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectCraftingStations.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.AddKnownStation))]
        public static class Player_AddKnownStation_PreventUnauthorizedStationDiscovery
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Player __instance, CraftingStation station)
            {
                if (!wardAccessProtectCraftingStations.Value)
                    return true;

                if (__instance == null || station == null)
                    return true;

                int level = station.GetLevel();
                if (__instance.m_knownStations.TryGetValue(station.m_name, out int knownLevel) && knownLevel >= level)
                    return true;

                return !ShouldSilentlyBlockProtectedInteraction(station, __instance);
            }
        }

        [HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
        public static class Bed_Interact_PreventUnauthorizedSleep
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Bed __instance, Humanoid human, ref bool __result)
            {
                if (!wardAccessProtectBeds.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, human, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Catapult), nameof(Catapult.OnLegUse))]
        public static class Catapult_OnLegUse_PreventUnauthorizedAccess
        {
            private static bool Prefix(Catapult __instance, Humanoid user)
            {
                if (!wardAccessProtectCatapults.Value)
                    return true;

                return !ShouldBlockProtectedInteraction(__instance, user);
            }
        }

        [HarmonyPatch(typeof(Catapult), nameof(Catapult.OnLoadPointUse))]
        public static class Catapult_OnLoadPointUse_PreventUnauthorizedAccess
        {
            private static bool Prefix(Catapult __instance, Humanoid user)
            {
                if (!wardAccessProtectCatapults.Value)
                    return true;

                return !ShouldBlockProtectedInteraction(__instance, user);
            }
        }

        [HarmonyPatch(typeof(ArcheryTarget), nameof(ArcheryTarget.Interact))]
        public static class ArcheryTarget_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(ArcheryTarget __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectArcheryTargets.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, user, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Barber), nameof(Barber.Interact))]
        public static class Barber_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Barber __instance, Humanoid human, ref bool __result)
            {
                if (!wardAccessProtectBarbers.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, human, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
        public static class PrivateArea_Interact_PreventInactiveWardAccessInsideOtherWard
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(PrivateArea __instance, Humanoid human, ref bool __result)
            {
                if (!wardAccessProtectInactiveWards.Value)
                    return true;

                if (__instance == null || __instance.IsEnabled())
                    return true;

                if (!BlockProtectedInteraction(__instance, human, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        public static class ZoneSystem_Start_RegisterSaddleUserRPC
        {
            private static void Postfix()
            {
                RegisterSaddleUserRPC();
                RegisterTeleportAccessRPC();
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        public static class ZoneSystem_OnDestroy_ResetFullProtectionRPCs
        {
            private static void Postfix() => ResetRPCRegistration();
        }

        [HarmonyPatch(typeof(Sadle), nameof(Sadle.RPC_RequestRespons))]
        public static class Sadle_RPC_RequestRespons_RecordLastUser
        {
            private static void Postfix(Sadle __instance, bool granted)
            {
                if (!granted || Player.m_localPlayer == null)
                    return;

                long playerID = Player.m_localPlayer.GetPlayerID();
                if (playerID == 0L)
                    return;

                SetLastSaddleUserLocal(__instance, playerID);
                RequestSetLastSaddleUser(__instance, playerID);
            }
        }

        [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.RPC_RequestRespons))]
        public static class ShipControlls_RPC_RequestRespons_RecordLastController
        {
            private static void Postfix(ShipControlls __instance, bool granted)
            {
                if (!granted || __instance?.m_ship == null || Player.m_localPlayer == null)
                    return;

                long playerID = Player.m_localPlayer.GetPlayerID();
                if (playerID == 0L)
                    return;

                ZNetView nview = __instance.m_ship.GetComponentZNetView();
                SetLastVehicleControllerLocal(nview, playerID);
                RequestSetLastVehicleController(nview, playerID);
            }
        }

        [HarmonyPatch(typeof(Vagon), nameof(Vagon.AttachTo))]
        public static class Vagon_AttachTo_RecordLastController
        {
            private static void Postfix(Vagon __instance, GameObject go)
            {
                if (__instance == null || go == null)
                    return;

                Player player = go.GetComponent<Player>();
                if (player == null)
                    return;

                long playerID = player.GetPlayerID();
                if (playerID == 0L)
                    return;

                ZNetView nview = __instance.GetComponentZNetView();
                SetLastVehicleControllerLocal(nview, playerID);
                RequestSetLastVehicleController(nview, playerID);
            }
        }

        public static class Interactable_PreventUnauthorizedAccess
        {
            private static readonly HashSet<Type> ExcludedInteractableTypes = new()
            {
                typeof(Container),
                typeof(Door),
                typeof(Pickable),
                typeof(PrivateArea),
                typeof(Ladder),
                typeof(ShipControlls),
                typeof(Sadle),
                typeof(Switch),
                typeof(Chair),
                typeof(TombStone),
                typeof(TeleportWorld),
                typeof(Vagon),
                typeof(Feast),
                typeof(ShieldGenerator),
                typeof(Tameable),
                typeof(Pet),
                typeof(Petable),
                typeof(ItemStand),
                typeof(CookingStation),
                typeof(Fermenter),
                typeof(Beehive),
                typeof(SapCollector),
                typeof(ItemDrop),
                typeof(PickableItem),
                typeof(Fish),
                typeof(RopeAttachment),
                typeof(Teleport),
                typeof(MapTable),
                typeof(Fireplace),
                typeof(Turret),
                typeof(CraftingStation),
                typeof(Bed),
                typeof(Catapult),
                typeof(ArcheryTarget),
                typeof(Barber)
            };

            internal static IEnumerable<MethodBase> TargetMethods()
            {
                HashSet<MethodBase> methods = new();
                foreach (Type type in GetLoadedInteractableTypes())
                {
                    MethodInfo interact = AccessTools.Method(type, nameof(Interactable.Interact), new[] { typeof(Humanoid), typeof(bool), typeof(bool) });
                    if (ShouldPatchInteractableMethod(interact) && methods.Add(interact))
                        yield return interact;

                    MethodInfo useItem = AccessTools.Method(type, nameof(Interactable.UseItem), new[] { typeof(Humanoid), typeof(ItemDrop.ItemData) });
                    if (ShouldPatchInteractableMethod(useItem) && methods.Add(useItem))
                        yield return useItem;
                }
            }

            private static IEnumerable<Type> GetLoadedInteractableTypes()
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly == null || assembly.IsDynamic)
                        continue;

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types;
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (Type type in types)
                    {
                        if (type == null || type == typeof(Interactable) || type.IsAbstract || type.ContainsGenericParameters || ExcludedInteractableTypes.Contains(type))
                            continue;

                        if (typeof(Interactable).IsAssignableFrom(type))
                            yield return type;
                    }
                }
            }

            private static bool ShouldPatchInteractableMethod(MethodInfo method)
            {
                return method != null
                       && method.DeclaringType != null
                       && method.DeclaringType != typeof(Interactable)
                       && !ExcludedInteractableTypes.Contains(method.DeclaringType);
            }

            public static bool Prefix(object __instance, Humanoid __0, ref bool __result)
            {
                if (!wardAccessProtectInteractables.Value)
                    return true;

                if (__0 is not Player)
                    return true;

                if (__instance is not Component component)
                    return true;

                if (!BlockUnauthorizedWardInteraction(component, __0))
                    return true;

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Trap), nameof(Trap.Interact))]
        public static class Trap_Interact_PreventUnauthorizedAccess
        {
            private static bool Prefix(Trap __instance, Humanoid character, ref bool __result)
            {
                if (!wardTrapProtection.Value)
                    return true;

                if (!BlockProtectedInteraction(__instance, character, ref __result))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Trap), nameof(Trap.OnTriggerEnter))]
        static class Trap_OnTriggerEnter_TrapProtection
        {
            private static bool Prefix(Trap __instance, Collider collider)
            {
                if (!wardTrapProtection.Value)
                    return true;

                Player player = collider.GetComponentInParent<Player>();
                if (player == null)
                    return true;

                if (!InsideEnabledPlayersArea(__instance.transform.position, out _, checkCache: true))
                    return true;

                if (BackgroundProtection.IsBackgroundProtectionActiveAt(__instance.transform.position, out PrivateArea backgroundWard)
                    && backgroundWard != null
                    && !HasAccessToWardOrConnectedWard(backgroundWard, player, wardBackgroundConnectedAccessMode.Value))
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        public static class Destructible_Damage_PlantProtection
        {
            private static void ModifyHitDamage(HitData hit, float value)
            {
                hit.m_damage.Modify(Math.Max(value, 0));
            }

            private static void Prefix(Destructible __instance, HitData hit)
            {
                if (!wardPlantProtection.Value)
                    return;

                if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || __instance.m_destroyed)
                    return;

                if (__instance.GetDestructibleType() != DestructibleType.Default || __instance.m_health != 1)
                    return;

                if (hit == null || !hit.HaveAttacker())
                    return;

                if (!InsideEnabledPlayersArea(__instance.transform.position, out PrivateArea ward, checkCache: true))
                    return;

                if (__instance.GetComponent<Plant>() != null)
                {
                    ModifyHitDamage(hit, 0f);
                    ward.FlashShield(false);
                }
                else if (__instance.TryGetComponent(out Pickable pickable))
                {
                    ItemDrop.ItemData.SharedData m_shared = pickable.m_itemPrefab?.GetComponent<ItemDrop>()?.m_itemData.m_shared;

                    if (m_shared != null && _wardPlantProtectionList.Contains(m_shared.m_name.ToLower()))
                    {
                        ModifyHitDamage(hit, 0f);
                        ward.FlashShield(false);
                    }
                }
            }
        }
    }
}
