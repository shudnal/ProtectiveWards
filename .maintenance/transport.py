from support import *

def apply():
    text = read('WardPlayers.cs')
    text = replace(text, '            internal readonly bool AdminAccess;', '            internal readonly bool AdminAccess;\n            internal readonly long PeerID;')
    text = replace(text, 'internal OnlinePlayer(long playerID, ZDOID characterID, string name, bool adminAccess = false)', 'internal OnlinePlayer(long playerID, ZDOID characterID, string name, bool adminAccess = false, long peerID = 0L)')
    text = replace(text, '                AdminAccess = adminAccess;', '                AdminAccess = adminAccess;\n                PeerID = peerID;')
    text = replace(text, 'new OnlinePlayer(localID, local.GetZDOID(), local.GetPlayerName())', 'new OnlinePlayer(localID, local.GetZDOID(), local.GetPlayerName(), peerID: ZRoutedRpc.instance.m_id)')
    text = replace(text, 'new OnlinePlayer(playerID, peer.m_characterID, peer.m_playerName)', 'new OnlinePlayer(playerID, peer.m_characterID, peer.m_playerName, peerID: peer.m_uid)')
    text = replace(text, '                package.Write(HasWardAdminAccess(player.PlayerID));', '                package.Write(HasWardAdminAccess(player.PlayerID));\n                package.Write(player.PeerID);')
    text = replace(text, '                bool adminAccess = package.ReadBool();', '                bool adminAccess = package.ReadBool();\n                long peerID = package.ReadLong();')
    text = replace(text, '                if (id == 0L || character.IsNone() || name.Length > 64 || !ids.Add(id))', '                if (id == 0L || peerID == 0L || character.IsNone() || name.Length > 64 || !ids.Add(id))')
    text = replace(text, 'new OnlinePlayer(id, character, name, adminAccess)', 'new OnlinePlayer(id, character, name, adminAccess, peerID)')
    marker = '        internal static bool HasRemoteAdminAccess(long playerID)'
    text = replace(text, marker, '''        internal static bool TryGetByPeer(long peerID, out OnlinePlayer result)
        {
            foreach (OnlinePlayer player in GetOnlinePlayers())
            {
                if (peerID == 0L || player.PeerID != peerID)
                    continue;
                result = player;
                return true;
            }
            result = default;
            return false;
        }

''' + marker)
    write('WardPlayers.cs', text)
    text = read('FullProtection.cs')
    for symbol in ['RPC_SetLastSaddleUser', 'RPC_SetLastVehicleController', 'saddleUserRecordMaxDistance', 'vehicleControllerRecordMaxDistance']:
        text, count = re.subn(r'^ *private const (?:string|float) ' + symbol + r' = [^\n]+\n', '', text, flags=re.M)
        assert count == 1, symbol
    text = replace(text, '        private static bool s_saddleRpcRegistered;\n', '')
    text = replace(text, '            s_saddleRpcRegistered = false;\n', '')
    text = re.sub(r'^ *RegisterSaddleUserRPC\(\);\n', '', text, flags=re.M)
    start = text.index('        private static void RegisterSaddleUserRPC()')
    end = text.index('        private static Component GetProtectedSwitchTarget(', start)
    text = text[:start] + text[end:]
    for kind, name in [('Sadle','RPC_RequestRespons'), ('ShipControlls','RPC_RequestRespons'), ('Vagon','AttachTo')]:
        marker = f'        [HarmonyPatch(typeof({kind}), nameof({kind}.{name}))]'
        start = text.index(marker)
        end = text.index('\n        }\n', start) + len('\n        }\n')
        text = text[:start] + text[end:]
    assert 'SetLastSaddleUser' not in text and 'SetLastVehicleController' not in text
    write('FullProtection.cs', text)
    text = read('ProtectiveWards.cs')
    text = replace(text, '''            if (HasLastSaddleUser(component.GetComponentZNetView(), playerID))
                return true;

''', '')
    write('ProtectiveWards.cs', text)
    write('WardTransportAccess.cs', '''using HarmonyLib;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardTransportAccess
    {
        internal static bool IsControlTarget(ZDO target, bool ownershipRequest)
        {
            if (target == null || ZNetScene.instance == null)
                return false;
            GameObject instance = ZNetScene.instance.FindInstance(target.m_uid)
                ?? ZNetScene.instance.GetPrefab(target.GetPrefab());
            if (instance == null)
                return false;
            return ownershipRequest ? instance.GetComponent<Vagon>() != null
                : instance.GetComponent<Ship>() != null || instance.GetComponentInChildren<Sadle>(true) != null;
        }

        private static void RecordSaddleUser(Sadle saddle, long sender, long characterUserID)
        {
            if (saddle?.m_nview?.IsValid() != true || !saddle.m_nview.IsOwner()
                || saddle.GetUser() != characterUserID
                || !WardPlayers.TryGetByPeer(sender, out WardPlayers.OnlinePlayer player)
                || player.CharacterID.UserID != characterUserID)
                return;
            saddle.m_nview.GetZDO().Set(s_lastSaddleUser, player.PlayerID);
        }

        private static void RecordShipUser(ShipControlls controls, long sender, long playerID)
        {
            if (controls?.m_nview?.IsValid() != true || !controls.m_nview.IsOwner()
                || controls.GetUser() != playerID
                || !WardPlayers.TryGetByPeer(sender, out WardPlayers.OnlinePlayer player)
                || player.PlayerID != playerID)
                return;
            controls.m_nview.GetZDO().Set(s_lastVehicleController, playerID);
        }

        [HarmonyPatch(typeof(Sadle), nameof(Sadle.RPC_RequestControl))]
        private static class Sadle_RPC_RequestControl_RecordGrantedUser
        {
            private static void Postfix(Sadle __instance, long sender, long playerID) => RecordSaddleUser(__instance, sender, playerID);
        }

        [HarmonyPatch(typeof(Sadle), nameof(Sadle.RPC_ReleaseControl))]
        private static class Sadle_RPC_ReleaseControl_PreserveGrantedUser
        {
            private static void Prefix(Sadle __instance, long sender, long playerID) => RecordSaddleUser(__instance, sender, playerID);
        }

        [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.RPC_RequestControl))]
        private static class ShipControlls_RPC_RequestControl_RecordGrantedUser
        {
            private static void Postfix(ShipControlls __instance, long sender, long playerID) => RecordShipUser(__instance, sender, playerID);
        }

        [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.RPC_ReleaseControl))]
        private static class ShipControlls_RPC_ReleaseControl_PreserveGrantedUser
        {
            private static void Prefix(ShipControlls __instance, long sender, long playerID) => RecordShipUser(__instance, sender, playerID);
        }

        [HarmonyPatch(typeof(Vagon), nameof(Vagon.AttachTo))]
        private static class Vagon_AttachTo_RecordAttachedPlayer
        {
            private static void Postfix(Vagon __instance)
            {
                if (__instance?.m_nview?.IsValid() != true || !__instance.m_nview.IsOwner())
                    return;
                Player player = __instance.m_attachedObject?.GetComponent<Player>();
                if (player != null && player.GetPlayerID() != 0L)
                    __instance.m_nview.GetZDO().Set(s_lastVehicleController, player.GetPlayerID());
            }
        }
    }
}
''')
    add_compile('WardTransportAccess.cs')
    text = read('WardRpc.cs')
    text = replace(text, '        private static readonly int s_togglePermitted = "TogglePermitted".GetStableHashCode();', '''        private static readonly int s_togglePermitted = "TogglePermitted".GetStableHashCode();
        private static readonly int s_requestControl = "RequestControl".GetStableHashCode();
        private static readonly int s_requestOwn = "RPC_RequestOwn".GetStableHashCode();''')
    text = replace(text, '''                    if (!s_methods.Contains(method) && !wardMutation)
                        return true;''', '''                    bool transportControl = (method == s_requestControl || method == s_requestOwn)
                        && WardTransportAccess.IsControlTarget(ZDOMan.instance?.GetZDO(target), method == s_requestOwn);
                    if (!s_methods.Contains(method) && !wardMutation && !transportControl)
                        return true;''')
    write('WardRpc.cs', text)
    commit('fix: record transport access only from owner-confirmed control and attachment', 'WardTransportAccess.cs', 'WardPlayers.cs', 'FullProtection.cs', 'ProtectiveWards.cs', 'WardRpc.cs', 'ProtectiveWards.csproj')
