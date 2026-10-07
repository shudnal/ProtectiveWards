from support import *

write('WardPlayers.cs', '''using System;
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
''')
add_compile('WardPlayers.cs')
text = read('ProtectiveWards.cs')
needle = '''            if (!IsPlayerServerAdminOrHost(playerID))
                return false;'''
text = replace(text, needle, '''            if (ZNet.instance?.IsServer() == false && Player.m_localPlayer?.GetPlayerID() != playerID)
                return WardPlayers.HasRemoteAdminAccess(playerID);

''' + needle)
needle = '''            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                if (info.m_characterID.IsNone())
                    continue;

                ZDO characterZdo = ZDOMan.instance.GetZDO(info.m_characterID);'''
text = replace(text, needle, '''            bool knownCharacter = WardPlayers.TryGet(playerID, out WardPlayers.OnlinePlayer onlinePlayer);
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                if (info.m_characterID.IsNone())
                    continue;

                if (knownCharacter && info.m_characterID == onlinePlayer.CharacterID)
                {
                    playerInfo = info;
                    return true;
                }

                ZDO characterZdo = ZDOMan.instance.GetZDO(info.m_characterID);''')
write('ProtectiveWards.cs', text)
text = read('BackgroundProtection.cs')
needle = '''            float radius = Mathf.Max(wardBackgroundPresenceRadius.Value, 0f);

            if (!TryResolveWardCheckPoint(point, out Vector3 resolvedPoint))'''
text = replace(text, needle, '''            if (wardBackgroundPresenceMode.Value == WardBackgroundPresenceMode.PermittedOnline)
            {
                foreach (WardPlayers.OnlinePlayer player in WardPlayers.GetOnlinePlayers())
                    if (ward.HasConnectedWardAccess(player.PlayerID, mode, activeWardPredicate))
                        return true;
                return false;
            }

            float radius = Mathf.Max(wardBackgroundPresenceRadius.Value, 0f);

            if (!TryResolveWardCheckPoint(point, out Vector3 resolvedPoint))''')
write('BackgroundProtection.cs', text)
text = read('WardPermittedPlayersUI.cs')
text = method(text, 'private static List<KeyValuePair<long, string>> FindOnlinePlayers(string query)', '''private static List<KeyValuePair<long, string>> FindOnlinePlayers(string query)
{
    return WardPlayers.FindByName(query);
}''')
write('WardPermittedPlayersUI.cs', text)
text = read('AdminServerFeatures.cs')
text = replace(text, 'List<Player> matches = FindOnlinePlayers(query);', 'List<KeyValuePair<long, string>> matches = WardPlayers.FindByName(query);')
text = replace(text, 'matches.Select(p => p.GetPlayerName())', 'matches.Select(p => p.Value)')
text = replace(text, 'Player target = matches[0];', 'KeyValuePair<long, string> target = matches[0];')
text = text.replace('target.GetPlayerID()', 'target.Key').replace('target.GetPlayerName()', 'target.Value')
text = replace(text, '''            Player target = Player.GetPlayer(targetID);
            if (target == null || target.Value != targetName)
                return;''', '''            if (!WardPlayers.TryGet(targetID, out WardPlayers.OnlinePlayer target))
                return;
            targetName = target.Name;''')
start = text.index('        private static List<Player> FindOnlinePlayers(string query)')
end = text.index('        [HarmonyPatch(typeof(Terminal)', start)
text = text[:start] + text[end:]
write('AdminServerFeatures.cs', text)
commit('fix: resolve online ward access and command targets from server identities', 'WardPlayers.cs', 'ProtectiveWards.csproj', 'ProtectiveWards.cs', 'BackgroundProtection.cs', 'WardPermittedPlayersUI.cs', 'AdminServerFeatures.cs')
