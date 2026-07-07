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
        private const string RPC_PermitPlayer = "PW_PermitPlayer";
        private const string RPC_UnpermitPlayer = "PW_UnpermitPlayer";
        private const string RPC_SetWardEnabled = "PW_SetWardEnabled";
        private const string RPC_CheckWardBuildLimit = "PW_CheckWardBuildLimit";
        private const string RPC_DestroyWardForBuildLimit = "PW_DestroyWardForBuildLimit";
        private static readonly HashSet<ZDOID> s_requestedWardLimitChecks = new HashSet<ZDOID>();
        private static bool s_rpcRegistered;
        private static bool s_commandRegistered;

        internal static void RegisterRPCs()
        {
            if (s_rpcRegistered || ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.Register<ZPackage>(RPC_DestroyWardForBuildLimit, RPC_DestroyWardForBuildLimitClient);

            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                ZRoutedRpc.instance.Register<ZPackage>(RPC_PermitPlayer, RPC_PermitPlayerServer);
                ZRoutedRpc.instance.Register<ZPackage>(RPC_UnpermitPlayer, RPC_UnpermitPlayerServer);
                ZRoutedRpc.instance.Register<ZPackage>(RPC_SetWardEnabled, RPC_SetWardEnabledServer);
                ZRoutedRpc.instance.Register<ZPackage>(RPC_CheckWardBuildLimit, RPC_CheckWardBuildLimitServer);
            }

            s_rpcRegistered = true;
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

        private static void RequestPermit(string query, Terminal context)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                context.AddString("Player is not available.");
                return;
            }

            PrivateArea ward = FindNearestWard(player.transform.position, GetWardControlCommandRange());
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
            {
                context.AddString("$pw_permit_no_ward".Localize());
                return;
            }

            if (!HasAccessToWardOrConnectedWard(ward, player))
            {
                context.AddString("$pw_permit_no_access".Localize());
                return;
            }

            List<Player> matches = FindOnlinePlayers(query);
            if (matches.Count == 0)
            {
                context.AddString("$pw_permit_no_match".Localize(query));
                return;
            }

            if (matches.Count > 1)
            {
                context.AddString("$pw_permit_multiple".Localize(string.Join(", ", matches.Select(p => p.GetPlayerName()).ToArray())));
                return;
            }

            Player target = matches[0];
            if (IsPlayerInPermittedList(ward, target.GetPlayerID()))
            {
                context.AddString("$pw_permit_already".Localize());
                return;
            }

            ZPackage package = new ZPackage();
            package.Write(ward.m_nview.GetZDO().m_uid);
            package.Write(player.GetPlayerID());
            package.Write(target.GetPlayerID());
            package.Write(target.GetPlayerName());

            if (ZNet.instance != null && ZNet.instance.IsServer())
                RPC_PermitPlayerServer(0L, new ZPackage(package.GetArray()));
            else
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_PermitPlayer, package);

            context.AddString("$pw_permit_added".Localize(target.GetPlayerName()));
        }

        private static void RequestUnpermit(string query, Terminal context)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                context.AddString("Player is not available.");
                return;
            }

            PrivateArea ward = FindNearestWard(player.transform.position, GetWardControlCommandRange());
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
            {
                context.AddString("$pw_permit_no_ward".Localize());
                return;
            }

            if (!HasAccessToWardOrConnectedWard(ward, player))
            {
                context.AddString("$pw_permit_no_access".Localize());
                return;
            }

            List<KeyValuePair<long, string>> matches = FindPermittedPlayers(ward, query);
            if (matches.Count == 0)
            {
                context.AddString($"No permitted player matched: {query}");
                return;
            }

            if (matches.Count > 1)
            {
                context.AddString($"Multiple permitted players matched: {string.Join(", ", matches.Select(p => p.Value).ToArray())}");
                return;
            }

            KeyValuePair<long, string> target = matches[0];
            ZPackage package = new ZPackage();
            package.Write(ward.m_nview.GetZDO().m_uid);
            package.Write(player.GetPlayerID());
            package.Write(target.Key);

            if (ZNet.instance != null && ZNet.instance.IsServer())
                RPC_UnpermitPlayerServer(0L, new ZPackage(package.GetArray()));
            else
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_UnpermitPlayer, package);

            context.AddString($"Removed {target.Value} from ward permitted list.");
        }

        private static void RequestSetWardEnabled(bool enabled, Terminal context)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                context.AddString("Player is not available.");
                return;
            }

            PrivateArea ward = FindNearestWard(player.transform.position, GetWardControlCommandRange());
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
            {
                context.AddString("$pw_permit_no_ward".Localize());
                return;
            }

            if (!CanLocalToggleWard(ward))
            {
                context.AddString("$pw_permit_no_access".Localize());
                return;
            }

            ZPackage package = new ZPackage();
            package.Write(ward.m_nview.GetZDO().m_uid);
            package.Write(player.GetPlayerID());
            package.Write(enabled);

            if (ZNet.instance != null && ZNet.instance.IsServer())
                RPC_SetWardEnabledServer(0L, new ZPackage(package.GetArray()));
            else
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_SetWardEnabled, package);

            context.AddString(enabled ? "Ward enable requested." : "Ward disable requested.");
        }

        private static void RPC_PermitPlayerServer(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            long requesterID = package.ReadLong();
            long targetID = package.ReadLong();
            string targetName = package.ReadString();

            if (!AreWardControlCommandsEnabled())
                return;

            PrivateArea ward = WardZdoUtils.FindLoadedWard(wardID);
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
                return;

            if (!CanRequesterPermit(ward, requesterID))
                return;

            Player requester = Player.GetPlayer(requesterID);
            if (requester == null || Utils.DistanceXZ(requester.transform.position, ward.transform.position) > GetWardControlCommandRange())
                return;

            Player target = Player.GetPlayer(targetID);
            if (target == null || target.GetPlayerName() != targetName)
                return;

            if (IsPlayerInPermittedList(ward, targetID))
                return;

            ward.AddPermitted(targetID, targetName);
            LogInfo($"Added {targetName} to ward permitted list by command");
        }

        private static void RPC_UnpermitPlayerServer(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            long requesterID = package.ReadLong();
            long targetID = package.ReadLong();

            if (!AreWardControlCommandsEnabled())
                return;

            PrivateArea ward = WardZdoUtils.FindLoadedWard(wardID);
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
                return;

            if (!CanRequesterPermit(ward, requesterID))
                return;

            Player requester = Player.GetPlayer(requesterID);
            if (requester == null || Utils.DistanceXZ(requester.transform.position, ward.transform.position) > GetWardControlCommandRange())
                return;

            if (!IsPlayerInPermittedList(ward, targetID))
                return;

            string targetName = ward.GetPermittedPlayers().FirstOrDefault(player => player.Key == targetID).Value;
            ward.RemovePermitted(targetID);
            LogInfo($"Removed {targetName} from ward permitted list by command");
        }

        private static void RPC_SetWardEnabledServer(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            long requesterID = package.ReadLong();
            bool enabled = package.ReadBool();

            if (!AreWardControlCommandsEnabled())
                return;

            PrivateArea ward = WardZdoUtils.FindLoadedWard(wardID);
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
                return;

            if (!CanRequesterToggleWard(ward, requesterID))
                return;

            Player requester = Player.GetPlayer(requesterID);
            if (requester == null || Utils.DistanceXZ(requester.transform.position, ward.transform.position) > GetWardControlCommandRange())
                return;

            if (ward.IsEnabled() == enabled)
                return;

            ward.SetEnabled(enabled);
            LogInfo($"{(enabled ? "Enabled" : "Disabled")} ward by command");
        }

        private static bool CanRequesterPermit(PrivateArea ward, long requesterID) => ward != null && requesterID != 0L && HasAccessToWardOrConnectedWard(ward, requesterID, wardAccessConnectedAccessMode.Value);

        private static bool CanLocalToggleWard(PrivateArea ward) => ward != null && ((ward.m_piece != null && ward.m_piece.IsCreator()) || HasLocalWardAdminAccess());

        private static bool CanRequesterToggleWard(PrivateArea ward, long requesterID)
        {
            if (ward == null || requesterID == 0L)
                return false;

            if (HasWardAdminAccess(requesterID))
                return true;

            return ward.m_piece != null && ward.m_piece.GetCreator() == requesterID;
        }

        private static bool IsPlayerInPermittedList(PrivateArea ward, long playerID) => ward != null && ward.GetPermittedPlayers().Any(player => player.Key == playerID);

        private static List<KeyValuePair<long, string>> FindPermittedPlayers(PrivateArea ward, string query)
        {
            string normalized = query.Trim();
            List<KeyValuePair<long, string>> permitted = ward.GetPermittedPlayers();
            List<KeyValuePair<long, string>> exact = permitted
                .Where(player => string.Equals(player.Value, normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (exact.Count > 0)
                return exact;

            return permitted
                .Where(player => player.Value.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private static PrivateArea FindNearestWard(Vector3 point, float range)
        {
            PrivateArea result = null;
            float best = Math.Max(range, 0f);

            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || area.m_ownerFaction != Character.Faction.Players)
                    continue;

                float distance = Utils.DistanceXZ(area.transform.position, point);
                if (distance <= best)
                {
                    best = distance;
                    result = area;
                }
            }

            return result;
        }

        private static List<Player> FindOnlinePlayers(string query)
        {
            List<Player> players = Player.GetAllPlayers();
            string normalized = query.Trim();
            List<Player> exact = players.Where(player => string.Equals(player.GetPlayerName(), normalized, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count > 0)
                return exact;

            return players.Where(player => player.GetPlayerName().IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
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
            ZPackage package = new ZPackage();
            package.Write(creatorID);
            package.Write(newWardID);

            if (ZNet.instance != null && ZNet.instance.IsServer())
                RPC_CheckWardBuildLimitServer(0L, new ZPackage(package.GetArray()));
            else if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_CheckWardBuildLimit, package);
        }

        private static void RPC_CheckWardBuildLimitServer(long sender, ZPackage package)
        {
            if (wardBuildLimitPerPlayer.Value <= 0)
                return;

            long creatorID = package.ReadLong();
            ZDOID newWardID = package.ReadZDOID();
            if (creatorID == 0L || newWardID.Equals(ZDOID.None))
                return;

            ZDO newWardZdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(newWardID) : null;
            if (!newWardZdo.IsWard())
                return;

            if (newWardZdo.GetLong(ZDOVars.s_creator, 0L) != creatorID)
                return;

            int limit = wardBuildLimitPerPlayer.Value;
            int total = WardZdoUtils.CountWardsByCreator(creatorID);
            if (total <= limit)
                return;

            int existingBeforeNewWard = Math.Max(total - 1, 0);
            SendDestroyWardForBuildLimit(newWardZdo, existingBeforeNewWard, limit);
        }

        private static void SendDestroyWardForBuildLimit(ZDO zdo, int current, int limit)
        {
            if (zdo == null)
                return;

            ZPackage package = new ZPackage();
            package.Write(zdo.m_uid);
            package.Write(current);
            package.Write(limit);

            long owner = zdo.GetOwner();
            if (owner != 0L && ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(owner, RPC_DestroyWardForBuildLimit, package);
            else
                RPC_DestroyWardForBuildLimitClient(0L, new ZPackage(package.GetArray()));
        }

        private static void RPC_DestroyWardForBuildLimitClient(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            int current = package.ReadInt();
            int limit = package.ReadInt();

            DestroyLocalWard(wardID);

            Player player = Player.m_localPlayer;
            if (player != null)
                player.Message(MessageHud.MessageType.Center, "$pw_ward_limit_reached".Localize(current.ToString(), limit.ToString()));
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
