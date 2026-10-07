from support import *

write('WardRpc.cs', '''using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardRpc
    {
        private const string AdminGodModeKey = "pw_admin_god_mode";
        private const int MaximumPackageBytes = 262144;
        private static readonly HashSet<int> s_methods = new()
        {
            "PW_GrowPlant".GetStableHashCode(),
            "PW_AugmentStructure".GetStableHashCode()
        };
        private static readonly HashSet<int> s_registered = new();
        private static readonly int s_toggleEnabled = "ToggleEnabled".GetStableHashCode();
        private static readonly int s_togglePermitted = "TogglePermitted".GetStableHashCode();
        private static ZRoutedRpc s_router;
        private static ZNetPeer s_adminPeer;
        private static float s_nextWarning;

        internal static void Register(string name, Action<long, ZPackage> handler)
        {
            ZRoutedRpc router = ZRoutedRpc.instance;
            if (router == null || handler == null)
                return;

            if (s_router != router)
            {
                s_router = router;
                s_registered.Clear();
                s_adminPeer = null;
            }

            int hash = name.GetStableHashCode();
            s_methods.Add(hash);
            if (!s_registered.Add(hash))
                return;

            router.Register<ZPackage>(name, (sender, package) =>
            {
                if (package == null || package.Size() > MaximumPackageBytes)
                {
                    WarnRejected(name);
                    return;
                }

                try
                {
                    handler(sender, package);
                }
                catch (Exception error)
                {
                    // Never include payloads, passwords or other submitted values in the log.
                    Debug.LogWarning($"[ProtectiveWards] Request {name} failed: {error.GetType().Name}.");
                }
            });
        }

        internal static void SendToServer(string name, ZPackage package)
        {
            SyncLocalAdminMode();
            ZRoutedRpc.instance?.InvokeRoutedRPC(name, package);
        }

        internal static bool HasRemoteGodMode(long playerID)
        {
            ZNet network = ZNet.instance;
            if (network == null || !network.IsServer() || playerID == 0L)
                return false;

            foreach (ZNetPeer peer in network.GetPeers())
            {
                if (peer == null || !peer.IsReady() || peer.m_playerID != playerID)
                    continue;

                // This dictionary arrived through ZNet's socket-bound ServerSyncedPlayerData RPC.
                // It selects a mode for an already verified admin; it never establishes admin identity.
                return peer.m_serverSyncedPlayerData.TryGetValue(AdminGodModeKey, out string value) && value == "1";
            }

            return false;
        }

        internal static void SyncLocalAdminMode()
        {
            ZNet network = ZNet.instance;
            if (network == null || network.IsServer())
                return;

            ZNetPeer server = network.GetServerPeer();
            if (server == null || !server.IsReady())
                return;

            string value = Player.m_localPlayer?.InGodMode() == true ? "1" : "0";
            if (s_adminPeer == server
                && network.m_serverSyncedPlayerData.TryGetValue(AdminGodModeKey, out string previous)
                && previous == value)
                return;

            s_adminPeer = server;
            network.m_serverSyncedPlayerData[AdminGodModeKey] = value;
            // Flush on the same ordered connection before any ward operation that consumes this mode.
            network.SendServerSyncPlayerData(server);
        }

        internal static bool IsWithinReach(RoutedPlayerContext player, ZDO target, float distance = 10f, bool horizontal = false)
        {
            if (target == null || !player.HasPosition || distance < 0f || float.IsNaN(distance) || float.IsInfinity(distance))
                return false;

            Vector3 offset = player.Position - target.GetPosition();
            if (horizontal)
                offset.y = 0f;
            return offset.sqrMagnitude <= distance * distance;
        }

        private static void WarnRejected(string method)
        {
            if (Time.realtimeSinceStartup < s_nextWarning)
                return;
            s_nextWarning = Time.realtimeSinceStartup + 5f;
            Debug.LogWarning($"[ProtectiveWards] Rejected invalid ward RPC ({method}).");
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.SendServerSyncPlayerData))]
        private static class ZNet_SendServerSyncPlayerData_AdminMode
        {
            private static void Prefix(ZNet __instance)
            {
                if (!__instance.IsServer())
                    __instance.m_serverSyncedPlayerData[AdminGodModeKey] = Player.m_localPlayer?.InGodMode() == true ? "1" : "0";
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetGodMode))]
        private static class Player_SetGodMode_SynchronizeAdminMode
        {
            private static void Postfix(Player __instance)
            {
                if (__instance == Player.m_localPlayer)
                    SyncLocalAdminMode();
            }
        }

        [HarmonyPatch(typeof(ZRoutedRpc), nameof(ZRoutedRpc.RPC_RoutedRPC))]
        private static class ZRoutedRpc_RPC_RoutedRPC_ValidateWardSender
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ZRpc rpc, ZPackage pkg)
            {
                if (pkg == null || ZNet.instance == null)
                    return true;

                int position = pkg.GetPos();
                try
                {
                    pkg.ReadLong(); // Message ID.
                    long sender = pkg.ReadLong();
                    pkg.ReadLong(); // Destination peer.
                    ZDOID target = pkg.ReadZDOID();
                    int method = pkg.ReadInt();
                    bool wardMutation = (method == s_toggleEnabled || method == s_togglePermitted)
                        && WardZdoUtils.IsWard(ZDOMan.instance?.GetZDO(target));
                    if (!s_methods.Contains(method) && !wardMutation)
                        return true;

                    ZNet network = ZNet.instance;
                    ZNetPeer peer = network.GetPeer(rpc);
                    bool valid = network.IsServer()
                        ? peer != null && peer.IsReady() && sender == peer.m_uid
                        : network.GetServerPeer()?.m_rpc == rpc;
                    if (!valid)
                        WarnRejected("sender identity");
                    return valid;
                }
                catch (EndOfStreamException)
                {
                    WarnRejected("truncated header");
                    return false;
                }
                finally
                {
                    pkg.SetPos(position);
                }
            }
        }
    }
}
''')
add_compile('WardRpc.cs')
changed = ['WardRpc.cs', 'ProtectiveWards.csproj']
for path in Path('.').rglob('*.cs'):
    if path.name == 'WardRpc.cs':
        continue
    name = str(path)
    text = read(name)
    updated = text.replace('ZRoutedRpc.instance.Register<ZPackage>(', 'WardRpc.Register(')
    # Only the server-directed overload; peer-directed responses remain unchanged.
    updated = re.sub(r'ZRoutedRpc\.instance\??\.InvokeRoutedRPC\((RPC_\w+), (\w+)\)', r'WardRpc.SendToServer(\1, \2)', updated)
    if text != updated:
        write(name, updated)
        changed.append(name)
text = read('ProtectiveWards.cs')
text = replace(text, '''            Player player = Player.GetPlayer(playerID);
            if (player != null)
                return player.InGodMode();

            Player localPlayer = Player.m_localPlayer;
            return localPlayer != null && localPlayer.GetPlayerID() == playerID && localPlayer.InGodMode();''', '''            Player localPlayer = Player.m_localPlayer;
            if (localPlayer != null && localPlayer.GetPlayerID() == playerID)
                return localPlayer.InGodMode();

            return WardRpc.HasRemoteGodMode(playerID);''')
write('ProtectiveWards.cs', text)
if 'ProtectiveWards.cs' not in changed:
    changed.append('ProtectiveWards.cs')
commit('fix: synchronize admin mode and authenticate ward RPC senders', *changed)
