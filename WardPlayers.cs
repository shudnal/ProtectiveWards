using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardPlayers
    {
        private const string RpcRoster = "PW_OnlinePlayerRoster";
        private const int MaximumPlayers = 512;
        private static readonly List<OnlinePlayer> s_remotePlayers = new();
        private static bool s_registered;

        internal readonly struct OnlinePlayer
        {
            internal readonly long PlayerID;
            internal readonly ZDOID CharacterID;
            internal readonly string Name;
            internal readonly bool AdminAccess;

            internal OnlinePlayer(long playerID, ZDOID characterID, string name, bool adminAccess = false)
            {
                PlayerID = playerID;
                CharacterID = characterID;
                Name = name ?? "";
                AdminAccess = adminAccess;
            }
        }

        internal static IEnumerable<OnlinePlayer> GetOnlinePlayers()
        {
            ZNet network = ZNet.instance;
            if (network == null)
                yield break;

            Player local = Player.m_localPlayer;
            long localID = local?.GetPlayerID() ?? 0L;
            if (localID != 0L)
                yield return new OnlinePlayer(localID, local.GetZDOID(), local.GetPlayerName());

            if (!network.IsServer())
            {
                foreach (OnlinePlayer player in s_remotePlayers)
                    if (player.PlayerID != localID)
                        yield return player;
                yield break;
            }

            foreach (ZNetPeer peer in network.GetPeers())
            {
                if (peer == null || !peer.IsReady() || peer.m_server || peer.m_characterID.IsNone())
                    continue;

                long playerID = peer.m_playerID;
                if (playerID == 0L)
                    playerID = ZDOMan.instance?.GetZDO(peer.m_characterID)?.GetLong(ZDOVars.s_playerID, 0L) ?? 0L;
                if (playerID != 0L && playerID != localID)
                    yield return new OnlinePlayer(playerID, peer.m_characterID, peer.m_playerName);
            }
        }

        internal static List<KeyValuePair<long, string>> FindByName(string query)
        {
            string normalized = (query ?? "").Trim();
            if (normalized.Length == 0 || normalized.Length > 64)
                return new List<KeyValuePair<long, string>>();

            List<KeyValuePair<long, string>> players = GetOnlinePlayers()
                .GroupBy(player => player.PlayerID)
                .Select(group => new KeyValuePair<long, string>(group.Key, group.First().Name))
                .ToList();
            List<KeyValuePair<long, string>> exact = players
                .Where(player => string.Equals(player.Value, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
            return exact.Count > 0 ? exact : players
                .Where(player => player.Value.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        internal static bool TryGet(long playerID, out OnlinePlayer result)
        {
            foreach (OnlinePlayer player in GetOnlinePlayers())
            {
                if (player.PlayerID != playerID)
                    continue;
                result = player;
                return true;
            }
            result = default;
            return false;
        }

        internal static bool HasRemoteAdminAccess(long playerID)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer())
                return false;
            foreach (OnlinePlayer player in s_remotePlayers)
                if (player.PlayerID == playerID)
                    return player.AdminAccess;
            return false;
        }

        private static void Publish()
        {
            if (!s_registered || ZNet.instance?.IsServer() != true)
                return;

            List<OnlinePlayer> players = GetOnlinePlayers().GroupBy(player => player.PlayerID)
                .Select(group => group.First()).Take(MaximumPlayers).ToList();
            ZPackage package = new();
            package.Write(players.Count);
            foreach (OnlinePlayer player in players)
            {
                package.Write(player.PlayerID);
                package.Write(player.CharacterID);
                package.Write(player.Name.Length > 64 ? player.Name.Substring(0, 64) : player.Name);
                package.Write(HasWardAdminAccess(player.PlayerID));
            }
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
                if (peer != null && peer.IsReady() && !peer.m_server)
                    ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, RpcRoster, package);
        }

        private static void Receive(long sender, ZPackage package)
        {
            if (!IsServerRpcSender(sender) || ZNet.instance?.IsServer() != false)
                return;

            int count = package.ReadInt();
            if (count < 0 || count > MaximumPlayers)
                return;
            List<OnlinePlayer> players = new(count);
            HashSet<long> ids = new();
            for (int i = 0; i < count; i++)
            {
                long id = package.ReadLong();
                ZDOID character = package.ReadZDOID();
                string name = package.ReadString();
                bool adminAccess = package.ReadBool();
                if (id == 0L || character.IsNone() || name.Length > 64 || !ids.Add(id))
                    return;
                players.Add(new OnlinePlayer(id, character, name, adminAccess));
            }
            if (package.GetPos() != package.Size())
                return;
            s_remotePlayers.Clear();
            s_remotePlayers.AddRange(players);
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_RegisterRoster
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null)
                    return;
                WardRpc.Register(RpcRoster, Receive);
                s_registered = true;
                Publish();
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.SendPlayerList))]
        private static class ZNet_SendPlayerList_PublishRoster
        {
            private static void Postfix() => Publish();
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ClearRoster
        {
            private static void Postfix()
            {
                s_registered = false;
                s_remotePlayers.Clear();
            }
        }
    }
}
