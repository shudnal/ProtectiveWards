using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardExpiration
    {
        internal static readonly int s_expirationLastActiveUnix = "pw_expiration_last_active_unix".GetStableHashCode();
        internal static readonly int s_expirationExpired = "pw_expiration_expired".GetStableHashCode();
        internal static readonly int s_expirationLastPlayerId = "pw_expiration_last_player_id".GetStableHashCode();
        internal static readonly int s_expirationLastPlayerName = "pw_expiration_last_player_name".GetStableHashCode();
        internal static readonly int s_expirationExpiredUnix = "pw_expiration_expired_unix".GetStableHashCode();
        internal static readonly int s_expirationExpiredReason = "pw_expiration_expired_reason".GetStableHashCode();

        private const string RPC_ReactivateExpiredWard = "PW_ReactivateExpiredWard";
        private const string RPC_ReactivateExpiredWardResult = "PW_ReactivateExpiredWardResult";
        private const string RPC_ActivateConnectedWards = "PW_ActivateConnectedWards";
        private static float s_nextCheckTime;
        private static bool s_rpcRegistered;

        private enum ReactivationResult
        {
            Success,
            NotAuthorized,
            Unavailable
        }

        internal static void ResetNextCheckTime() => s_nextCheckTime = 0f;

        private static void RegisterRPCs()
        {
            if (s_rpcRegistered || ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.Register<ZPackage>(RPC_ReactivateExpiredWardResult, RPC_ReactivateExpiredWardResultClient);
            if (ZNet.instance?.IsServer() == true)
            {
                ZRoutedRpc.instance.Register<ZPackage>(RPC_ReactivateExpiredWard, RPC_ReactivateExpiredWardServer);
                ZRoutedRpc.instance.Register<ZPackage>(RPC_ActivateConnectedWards, RPC_ActivateConnectedWardsServer);
            }

            s_rpcRegistered = true;
        }

        private static void ResetRPCRegistration() => s_rpcRegistered = false;

        internal static void SetExpired(ZDO zdo, bool expired, long playerID, string playerName)
        {
            if (zdo == null)
                return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            zdo.Set(s_expirationExpired, expired);

            if (expired)
            {
                zdo.Set(s_expirationExpiredUnix, now);
                zdo.Set(s_expirationExpiredReason, "admin_command");
            }
            else
            {
                zdo.Set(s_expirationLastActiveUnix, now);
                zdo.Set(s_expirationExpiredReason, "admin_command_reactivated");
            }

            if (playerID == 0L)
                return;

            zdo.Set(s_expirationLastPlayerId, playerID);
            zdo.Set(s_expirationLastPlayerName, playerName ?? "");
        }

        internal static void Update()
        {
            if (wardExpirationMinutes.Value <= 0)
                return;

            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZNet.IsSinglePlayer)
                return;

            List<ZDO> characterZdos = ZNet.instance.GetAllCharacterZDOS();
            if (characterZdos.Count == 0)
                return;

            if (Time.time < s_nextCheckTime)
                return;

            s_nextCheckTime = Time.time + GetCheckIntervalSeconds();
            CheckTrackedWards(characterZdos);
        }

        private static float GetCheckIntervalSeconds()
        {
            int minutes = Math.Max(wardExpirationMinutes.Value, 1);
            if (minutes < 10)
                return 60f;

            return Mathf.Clamp(minutes / 10f, 10f, 60f) * 60f;
        }

        private static void CheckTrackedWards(List<ZDO> characterZdos)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long expirationSeconds = Math.Max(wardExpirationMinutes.Value, 1) * 60L;

            foreach (ZDO zdo in WardZdoUtils.GetAllWards())
            {
                if (zdo == null || IsPermitEveryone(zdo))
                    continue;

                if (zdo.GetLong(s_expirationLastActiveUnix, 0L) == 0L)
                    zdo.Set(s_expirationLastActiveUnix, now);

                RefreshingPlayer activePlayer = FindRefreshingPlayer(zdo, characterZdos);
                if (activePlayer != null)
                {
                    RefreshActivity(zdo, activePlayer, now);

                    if (IsExpired(zdo) && wardExpirationReactivationMode.Value == WardExpirationReactivationMode.AutomaticOnLogin)
                    {
                        Reactivate(zdo, activePlayer, now);
                        ActivateConnectedWardZdos(zdo, activePlayer.PlayerID, activePlayer.PlayerName);
                    }

                    continue;
                }

                if (IsExpired(zdo))
                    continue;

                long lastActive = zdo.GetLong(s_expirationLastActiveUnix, now);
                if (now - lastActive < expirationSeconds)
                    continue;

                Expire(zdo, now);
            }
        }

        private static RefreshingPlayer FindRefreshingPlayer(ZDO zdo, List<ZDO> characterZdos)
        {
            if (zdo == null || characterZdos == null)
                return null;

            WardConnectedAccessMode mode = wardExpirationConnectedAccessMode?.Value ?? WardConnectedAccessMode.Off;

            foreach (ZDO characterZdo in characterZdos)
            {
                if (characterZdo == null)
                    continue;

                long playerID = characterZdo.GetLong(ZDOVars.s_playerID, 0L);
                if (playerID == 0L)
                    continue;

                Vector3 playerPosition = characterZdo.GetPosition();
                switch (wardExpirationRefreshMode.Value)
                {
                    case WardExpirationRefreshMode.DirectPermitted:
                        if (IsPointInsideWardArea(zdo, playerPosition) && zdo.HasDirectWardAccess(playerID))
                            return RefreshingPlayer.FromCharacterZdo(characterZdo, playerID);
                        break;

                    case WardExpirationRefreshMode.EffectiveAccess:
                    default:
                        if (IsInsideExpirationRefreshArea(zdo, mode, playerPosition) && zdo.HasConnectedWardAccess(playerID, mode, IsActiveForExpirationConnectedAccess))
                            return RefreshingPlayer.FromCharacterZdo(characterZdo, playerID);
                        break;
                }
            }

            return null;
        }

        private static bool IsInsideExpirationRefreshArea(ZDO rootWard, WardConnectedAccessMode mode, Vector3 playerPosition)
        {
            if (IsPointInsideWardArea(rootWard, playerPosition))
                return true;

            if (mode == WardConnectedAccessMode.Off)
                return false;

            foreach (ZDO candidate in WardZdoUtils.ConnectedAccessWardZdos(rootWard, mode, IsActiveForExpirationConnectedAccess))
            {
                if (candidate == null || candidate.m_uid.Equals(rootWard.m_uid))
                    continue;

                if (IsPointInsideWardArea(candidate, playerPosition))
                    return true;
            }

            return false;
        }

        private static bool IsActiveForExpirationConnectedAccess(ZDO zdo)
        {
            return zdo.IsWard()
                   && zdo.GetBool(ZDOVars.s_enabled, false)
                   && !IsExpired(zdo);
        }

        private static void RefreshActivity(ZDO zdo, RefreshingPlayer player, long now)
        {
            zdo.Set(s_expirationLastActiveUnix, now);
            zdo.Set(s_expirationLastPlayerId, player.PlayerID);
            zdo.Set(s_expirationLastPlayerName, player.PlayerName);
        }

        private static void Expire(ZDO zdo, long now)
        {
            zdo.Set(s_expirationExpired, true);
            zdo.Set(s_expirationExpiredUnix, now);
            zdo.Set(s_expirationExpiredReason, "inactivity");
        }

        private static void Reactivate(ZDO zdo, RefreshingPlayer player, long now)
        {
            zdo.Set(s_expirationExpired, false);
            RefreshActivity(zdo, player, now);
        }

        internal static bool IsExpired(PrivateArea ward)
        {
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
                return false;

            return IsExpired(ward.m_nview.GetZDO());
        }

        internal static bool IsExpired(ZDO zdo)
        {
            return zdo != null && zdo.GetBool(s_expirationExpired, false);
        }

        private static bool CanReactivate(ZDO zdo, long playerID)
        {
            if (!WardZdoUtils.IsWard(zdo) || playerID == 0L)
                return false;

            WardConnectedAccessMode mode = wardExpirationConnectedAccessMode?.Value ?? WardConnectedAccessMode.Off;
            return wardExpirationRefreshMode.Value == WardExpirationRefreshMode.DirectPermitted
                ? zdo.HasDirectWardAccess(playerID)
                : zdo.HasConnectedWardAccess(playerID, mode, IsActiveForExpirationConnectedAccess);
        }

        private static void RequestManualReactivation(ZDOID wardID, long playerID)
        {
            if (wardID.IsNone() || playerID == 0L)
                return;

            ZPackage package = new();
            package.Write(wardID);
            package.Write(playerID);

            if (ZNet.instance?.IsServer() == true)
                RPC_ReactivateExpiredWardServer(0L, new ZPackage(package.GetArray()));
            else if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_ReactivateExpiredWard, package);
        }

        internal static void RequestConnectedActivation(ZDOID wardID, long playerID)
        {
            if (wardID.IsNone() || playerID == 0L || (wardAccessConnectedAccessMode?.Value ?? WardConnectedAccessMode.Off) == WardConnectedAccessMode.Off)
                return;

            ZPackage package = new();
            package.Write(wardID);
            package.Write(playerID);

            if (ZNet.instance?.IsServer() == true)
                RPC_ActivateConnectedWardsServer(0L, new ZPackage(package.GetArray()));
            else if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_ActivateConnectedWards, package);
        }

        private static void RPC_ReactivateExpiredWardServer(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            long claimedPlayerID = package.ReadLong();
            if (!TryGetRoutedPlayer(sender, claimedPlayerID, out RoutedPlayerContext requester))
                return;

            ZDO zdo = WardZdoUtils.GetWard(wardID);
            if (zdo == null
                || wardExpirationMinutes.Value <= 0
                || wardExpirationReactivationMode.Value != WardExpirationReactivationMode.ManualInteraction
                || IsPermitEveryone(zdo)
                || !IsExpired(zdo))
            {
                SendReactivationResult(sender, wardID, ReactivationResult.Unavailable);
                return;
            }

            if (!CanReactivate(zdo, requester.PlayerID))
            {
                SendReactivationResult(sender, wardID, ReactivationResult.NotAuthorized);
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            zdo.Set(s_expirationExpired, false);
            zdo.Set(s_expirationLastActiveUnix, now);
            zdo.Set(s_expirationLastPlayerId, requester.PlayerID);
            zdo.Set(s_expirationLastPlayerName, requester.PlayerName ?? "");
            ActivateConnectedWardZdos(zdo, requester.PlayerID, requester.PlayerName);
            SendReactivationResult(sender, wardID, ReactivationResult.Success);
        }

        private static void RPC_ActivateConnectedWardsServer(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            long claimedPlayerID = package.ReadLong();
            if (!TryGetRoutedPlayer(sender, claimedPlayerID, out RoutedPlayerContext requester))
                return;

            ZDO zdo = WardZdoUtils.GetWard(wardID);
            if (zdo == null)
                return;

            if (!zdo.GetBool(ZDOVars.s_enabled, false))
                zdo.Set(ZDOVars.s_enabled, true);

            ActivateConnectedWardZdos(zdo, requester.PlayerID, requester.PlayerName);
        }

        private static void SendReactivationResult(long peerID, ZDOID wardID, ReactivationResult result)
        {
            ZPackage response = new();
            response.Write(wardID);
            response.Write((int)result);

            if (ZNet.instance?.IsServer() == true && ZRoutedRpc.instance != null && peerID != 0L)
                ZRoutedRpc.instance.InvokeRoutedRPC(peerID, RPC_ReactivateExpiredWardResult, response);
            else
                RPC_ReactivateExpiredWardResultClient(0L, new ZPackage(response.GetArray()));
        }

        private static void RPC_ReactivateExpiredWardResultClient(long _, ZPackage package)
        {
            package.ReadZDOID();
            ReactivationResult result = (ReactivationResult)package.ReadInt();
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            if (result == ReactivationResult.Success)
                player.Message(MessageHud.MessageType.Center, "$pw_ward_expiration_reactivated");
            else if (result == ReactivationResult.NotAuthorized)
                player.Message(MessageHud.MessageType.Center, "$msg_privatezone");
        }

        private sealed class RefreshingPlayer
        {
            internal long PlayerID { get; private set; }
            internal string PlayerName { get; private set; }

            internal static RefreshingPlayer FromCharacterZdo(ZDO characterZdo, long playerID)
            {
                Player player = Player.GetPlayer(playerID);
                return new RefreshingPlayer
                {
                    PlayerID = playerID,
                    PlayerName = player != null ? player.GetPlayerName() : characterZdo.GetString(ZDOVars.s_playerName, "")
                };
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_ResetExpirationCheckTime
        {
            private static void Postfix()
            {
                ResetNextCheckTime();
                RegisterRPCs();
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ResetExpirationCheckTime
        {
            private static void Postfix()
            {
                ResetNextCheckTime();
                ResetRPCRegistration();
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.IsEnabled))]
        private static class PrivateArea_IsEnabled_Expiration
        {
            private static bool Prefix(PrivateArea __instance, ref bool __result)
            {
                if (wardExpirationMinutes.Value <= 0 || IsPermitEveryone(__instance) || !IsExpired(__instance))
                    return true;

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
        private static class PrivateArea_Interact_ReactivateExpiredWard
        {
            private static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, ref bool __result)
            {
                if (hold || wardExpirationMinutes.Value <= 0 || IsPermitEveryone(__instance) || wardExpirationReactivationMode.Value != WardExpirationReactivationMode.ManualInteraction)
                    return true;

                if (!IsExpired(__instance))
                    return true;

                Player player = human as Player;
                ZDO zdo = __instance.m_nview?.GetZDO();
                if (player == null || zdo == null || !CanReactivate(zdo, player.GetPlayerID()))
                    return true;

                RequestManualReactivation(zdo.m_uid, player.GetPlayerID());
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.AddUserList))]
        private static class PrivateArea_AddUserList_ExpirationHover
        {
            private static void Postfix(PrivateArea __instance, StringBuilder text)
            {
                if (wardExpirationMinutes.Value <= 0 || IsPermitEveryone(__instance) || !IsExpired(__instance))
                    return;

                text.Append("\n<color=orange>$pw_ward_expiration_expired</color>");
                text.Append("\n$pw_ward_expiration_hint");

                long localPlayerID = Player.m_localPlayer?.GetPlayerID() ?? 0L;
                if (!wardExpirationAdminHover.Value || !HasWardManagementAccess(__instance, localPlayerID))
                    return;

                ZDO zdo = __instance.m_nview.GetZDO();
                if (zdo == null)
                    return;

                long last = zdo.GetLong(s_expirationLastActiveUnix, 0L);
                long expired = zdo.GetLong(s_expirationExpiredUnix, 0L);
                string lastPlayer = zdo.GetString(s_expirationLastPlayerName, "");
                text.Append($"\nLast active: {last}; Expired: {expired}; Last player: {lastPlayer}");
            }
        }
    }
}
