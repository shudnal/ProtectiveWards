using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class AdminServerFeatures
    {
        private const string RPC_CheckWardBuildLimit = "PW_CheckWardBuildLimit";
        private const string RPC_DestroyWardForBuildLimit = "PW_DestroyWardForBuildLimit";
        private static readonly HashSet<ZDOID> s_requestedWardLimitChecks = new();
        private static bool s_rpcRegistered;
        private static bool s_commandRegistered;

        internal static void RegisterRPCs()
        {
            if (s_rpcRegistered || ZRoutedRpc.instance == null)
                return;

            WardRpc.Register(RPC_DestroyWardForBuildLimit, RPC_DestroyWardForBuildLimitClient);

            if (ZNet.instance?.IsServer() == true)
            {
                WardRpc.Register(RPC_CheckWardBuildLimit, RPC_CheckWardBuildLimitServer);
            }

            s_rpcRegistered = true;
        }

        internal static void ResetRPCRegistration()
        {
            s_rpcRegistered = false;
            s_requestedWardLimitChecks.Clear();
        }

        private static void RegisterCommands()
        {
            if (s_commandRegistered)
                return;

            RegisterPlayerListCommand("pw_permit", "<player name> - add an online player to the nearest ward within configured range", RequestPermit);
            RegisterPlayerListCommand("ward_permit", "<player name> - add an online player to the nearest ward within configured range", RequestPermit);
            RegisterPlayerListCommand("pw_unpermit", "<player name> - remove a player from the nearest ward permitted list", RequestUnpermit);
            RegisterPlayerListCommand("ward_unpermit", "<player name> - remove a player from the nearest ward permitted list", RequestUnpermit);
            RegisterWardStateCommand("pw_enable", "enable the nearest ward within configured range", enabled: true);
            RegisterWardStateCommand("ward_enable", "enable the nearest ward within configured range", enabled: true);
            RegisterWardStateCommand("pw_disable", "disable the nearest ward within configured range", enabled: false);
            RegisterWardStateCommand("ward_disable", "disable the nearest ward within configured range", enabled: false);
            RegisterWardExpirationStateCommand("pw_set_expired", "mark the nearest ward as expired", expired: true);
            RegisterWardExpirationStateCommand("ward_set_expired", "mark the nearest ward as expired", expired: true);
            RegisterWardExpirationStateCommand("pw_set_unexpired", "clear expired state from the nearest ward", expired: false);
            RegisterWardExpirationStateCommand("ward_set_unexpired", "clear expired state from the nearest ward", expired: false);

            s_commandRegistered = true;
        }

        private static void RegisterPlayerListCommand(string command, string description, Action<string, Terminal> action)
        {
            new Terminal.ConsoleCommand(command, description, args =>
            {
                if (!ValidateWardControlCommand(args.Context))
                    return;

                if (args.Length < 2)
                {
                    args.Context.AddString("Usage: <player name>");
                    return;
                }

                action(GetCommandQuery(args), args.Context);
            });
        }

        private static void RegisterWardStateCommand(string command, string description, bool enabled)
        {
            new Terminal.ConsoleCommand(command, description, args =>
            {
                if (!ValidateWardControlCommand(args.Context))
                    return;

                RequestSetWardEnabled(enabled, args.Context);
            });
        }

        private static void RegisterWardExpirationStateCommand(string command, string description, bool expired)
        {
            new Terminal.ConsoleCommand(command, description, args =>
            {
                if (!ValidateWardControlCommand(args.Context))
                    return;

                RequestSetWardExpired(expired, args.Context);
            });
        }

        private static string GetCommandQuery(Terminal.ConsoleEventArgs args) => string.Join(" ", args.Args.Skip(1).ToArray()).Trim();

        private static bool ValidateWardControlCommand(Terminal context)
        {
            if (AreWardControlCommandsEnabled())
                return true;

            context.AddString("Ward control commands are disabled.");
            return false;
        }

        private static bool AreWardControlCommandsEnabled() => wardExternalControlCommandsEnabled.Value;

        private static float GetWardControlCommandRange() => Math.Max(wardExternalControlCommandRange.Value, 0f);

        private static void RequestPermit(string query, Terminal context) => RequestControl(WardControl.Operation.Permit, query, context);
        private static void RequestUnpermit(string query, Terminal context) => RequestControl(WardControl.Operation.Unpermit, query, context);
        private static void RequestSetWardEnabled(bool enabled, Terminal context) => RequestControl(enabled ? WardControl.Operation.Enable : WardControl.Operation.Disable, "", context);
        private static void RequestSetWardExpired(bool expired, Terminal context) => RequestControl(expired ? WardControl.Operation.Expire : WardControl.Operation.Unexpire, "", context);

        internal static void RequestPermittedWardToggle(PrivateArea ward, long playerID)
        {
            if (ward?.m_nview?.IsValid() == true && Player.m_localPlayer?.GetPlayerID() == playerID)
                WardControl.Request(ward.m_nview.GetZDO().m_uid, WardControl.Operation.Toggle);
        }

        private static void RequestControl(WardControl.Operation action, string query, Terminal context)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                context.AddString("Player is not available.");
                return;
            }
            PrivateArea nearest = null;
            float distance = GetWardControlCommandRange();
            foreach (PrivateArea ward in PrivateArea.m_allAreas)
            {
                if (!IsPlayerWardPrefab(ward) || ward.m_nview?.IsValid() != true)
                    continue;
                float candidateDistance = Utils.DistanceXZ(ward.transform.position, player.transform.position);
                if (candidateDistance <= distance)
                {
                    distance = candidateDistance;
                    nearest = ward;
                }
            }
            if (nearest == null)
            {
                context.AddString("$pw_permit_no_ward".Localize());
                return;
            }
            WardControl.Request(nearest.m_nview.GetZDO().m_uid, action, query, context);
        }

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class Terminal_InitTerminal_RegisterCommands
        {
            private static void Postfix() => RegisterCommands();
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_RegisterAdminRPCs
        {
            private static void Postfix() => RegisterRPCs();
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ResetAdminRPCs
        {
            private static void Postfix() => ResetRPCRegistration();
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
        private static class Piece_SetCreator_CheckWardBuildLimit
        {
            private static void Postfix(Piece __instance, long uid)
            {
                if (wardBuildLimitPerPlayer.Value <= 0)
                    return;

                if (__instance == null || uid == 0L || __instance.GetComponent<PrivateArea>() == null)
                    return;

                if (!WardZdoUtils.IsWardPrefab(__instance.gameObject))
                    return;

                ZNetView nview = __instance.GetComponentZNetView();
                if (nview == null || !nview.IsValid())
                    return;

                ZDO zdo = nview.GetZDO();
                if (zdo == null)
                    return;

                ZDOID zdoID = zdo.m_uid;
                if (s_requestedWardLimitChecks.Contains(zdoID))
                    return;

                s_requestedWardLimitChecks.Add(zdoID);
                RequestWardBuildLimitCheck(uid, zdoID);
            }
        }

        private static void RequestWardBuildLimitCheck(long creatorID, ZDOID newWardID)
        {
            ZPackage package = new();
            package.Write(creatorID);
            package.Write(newWardID);

            if (ZNet.instance?.IsServer() == true)
                RPC_CheckWardBuildLimitServer(0L, new(package.GetArray()));
            else
                WardRpc.SendToServer(RPC_CheckWardBuildLimit, package);
        }

        private static void RPC_CheckWardBuildLimitServer(long sender, ZPackage package)
        {
            long creatorID = package.ReadLong();
            ZDOID newWardID = package.ReadZDOID();

            try
            {
                if (wardBuildLimitPerPlayer.Value <= 0)
                    return;

                if (creatorID == 0L || newWardID.Equals(ZDOID.None))
                    return;

                if (!TryGetRoutedPlayer(sender, creatorID, out RoutedPlayerContext requester))
                    return;

                ZDO newWardZdo = ZDOMan.instance?.GetZDO(newWardID);
                if (!newWardZdo.IsWard())
                    return;

                if (!newWardZdo.IsCreator(requester.PlayerID))
                    return;

                if (sender != 0L && newWardZdo.GetOwner() != sender)
                    return;

                int limit = wardBuildLimitPerPlayer.Value;
                int total = WardZdoUtils.CountWardsByCreator(requester.PlayerID);
                if (total <= limit)
                    return;

                int existingBeforeNewWard = Math.Max(total - 1, 0);
                SendDestroyWardForBuildLimit(newWardZdo, existingBeforeNewWard, limit);
            }
            finally
            {
                s_requestedWardLimitChecks.Remove(newWardID);
            }
        }

        private static void SendDestroyWardForBuildLimit(ZDO zdo, int current, int limit)
        {
            if (zdo == null)
                return;

            ZDOID wardID = zdo.m_uid;
            long owner = zdo.GetOwner();
            DestroyWardForBuildLimitServer(zdo);

            ZPackage package = new();
            package.Write(wardID);
            package.Write(current);
            package.Write(limit);

            if (owner != 0L && ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(owner, RPC_DestroyWardForBuildLimit, package);
            else if (Player.m_localPlayer != null)
                RPC_DestroyWardForBuildLimitClient(0L, new(package.GetArray()));
        }

        private static void DestroyWardForBuildLimitServer(ZDO zdo)
        {
            if (zdo == null || ZDOMan.instance == null)
                return;

            zdo.SetOwner(ZDOMan.instance.m_sessionID);
            ZDOMan.instance.DestroyZDO(zdo);
        }

        private static void RPC_DestroyWardForBuildLimitClient(long sender, ZPackage package)
        {
            if (!IsServerRpcSender(sender))
                return;

            ZDOID wardID = package.ReadZDOID();
            int current = package.ReadInt();
            int limit = package.ReadInt();

            DestroyLocalWard(wardID);

            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$pw_ward_limit_reached".Localize(current.ToString(), limit.ToString()));
        }

        private static void DestroyLocalWard(ZDOID wardID)
        {
            if (ZNetScene.instance == null)
                return;

            GameObject instance = ZNetScene.instance.FindInstance(wardID);
            if (instance != null)
                ZNetScene.instance.Destroy(instance);
        }
    }
}
