from support import *

def apply():
    write('WardControl.cs', '''using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardControl
    {
        private const string RpcRequest = "PW_WardControlV2";
        private const string RpcResult = "PW_WardControlResultV2";
        private const int Protocol = 1;
        private static readonly Dictionary<long, PendingRequest> s_pending = new();
        private static ZNet s_network;

        internal enum Operation { Permit, Unpermit, Enable, Disable, Expire, Unexpire, Toggle, ToggleSelfPermit }

        private sealed class PendingRequest
        {
            internal Terminal Console;
            internal Operation Action;
            internal float Deadline;
        }

        internal static void Request(ZDOID wardID, Operation action, string query = "", Terminal console = null)
        {
            Player player = Player.m_localPlayer;
            if (player == null || wardID.IsNone() || ZNet.instance == null || s_pending.Count >= 32)
                return;

            if (s_network != ZNet.instance)
            {
                s_pending.Clear();
                s_network = ZNet.instance;
            }

            long requestID = Utils.GenerateUID();
            ZPackage package = new();
            package.Write(Protocol);
            package.Write(requestID);
            package.Write((int)action);
            package.Write(wardID);
            package.Write(player.GetPlayerID());
            package.Write(query ?? "");
            s_pending[requestID] = new PendingRequest
            {
                Console = console, Action = action, Deadline = Time.realtimeSinceStartup + 15f
            };

            if (ZNet.instance.IsServer())
                HandleRequest(0L, new ZPackage(package.GetArray()));
            else
                WardRpc.SendToServer(RpcRequest, package);
        }

        private static void HandleRequest(long sender, ZPackage package)
        {
            long requestID = 0L;
            Operation action = Operation.Toggle;
            string message = "Ward request was rejected.";
            try
            {
                if (ZNet.instance?.IsServer() != true || package.ReadInt() != Protocol)
                    return;
                requestID = package.ReadLong();
                action = (Operation)package.ReadInt();
                ZDOID wardID = package.ReadZDOID();
                long playerID = package.ReadLong();
                string query = package.ReadString().Trim();
                if (!Enum.IsDefined(typeof(Operation), action) || query.Length > 64 || package.GetPos() != package.Size())
                {
                    message = "Invalid ward control request.";
                    return;
                }
                if (!TryGetRoutedPlayer(sender, playerID, out RoutedPlayerContext actor))
                {
                    message = "The requesting player is not available.";
                    return;
                }
                ZDO ward = WardZdoUtils.GetWard(wardID);
                if (ward == null)
                {
                    message = "The ward is no longer available.";
                    return;
                }
                bool command = action <= Operation.Unexpire;
                if (command && !wardExternalControlCommandsEnabled.Value)
                {
                    message = "Ward control commands are disabled.";
                    return;
                }
                float reach = command ? wardExternalControlCommandRange.Value : 10f;
                if (!WardRpc.IsWithinReach(actor, ward, reach, horizontal: command))
                {
                    message = "The ward is outside the allowed interaction range.";
                    return;
                }
                bool manager = ward.IsCreator(actor.PlayerID) || HasWardManagementAccess(ward, actor.PlayerID);
                if (!HasWardManagementAccess(ward, actor.PlayerID) && ShouldBlockInactiveWardAccess(ward, actor.PlayerID))
                {
                    message = "Another ward blocks access to this inactive ward.";
                    return;
                }
                switch (action)
                {
                    case Operation.Permit:
                    case Operation.Unpermit:
                        if (!CanManageMembersByCommand(ward, actor.PlayerID))
                        {
                            message = "You do not have access to this ward.";
                            return;
                        }
                        message = ChangeMember(ward, action, query);
                        break;
                    case Operation.Enable:
                    case Operation.Disable:
                    case Operation.Toggle:
                        bool permittedToggle = action == Operation.Toggle && CanPermittedPlayersToggleWard(ward, actor.PlayerID);
                        if (!manager && !permittedToggle)
                        {
                            message = "You are not allowed to toggle this ward.";
                            return;
                        }
                        bool enabled = action == Operation.Toggle ? !ward.GetBool(ZDOVars.s_enabled, false) : action == Operation.Enable;
                        if (ward.GetBool(ZDOVars.s_enabled, false) == enabled)
                        {
                            message = enabled ? "Ward is already enabled." : "Ward is already disabled.";
                            return;
                        }
                        SetEnabled(ward, enabled);
                        // A permitted non-manager's toggle applies only to this ward.
                        if (enabled && manager)
                            ActivateConnectedWardZdos(ward, actor.PlayerID, actor.PlayerName);
                        message = enabled ? "Ward enabled." : "Ward disabled.";
                        break;
                    case Operation.Expire:
                    case Operation.Unexpire:
                        if (!HasWardManagementAccess(ward, actor.PlayerID))
                        {
                            message = "You are not allowed to change ward expiration.";
                            return;
                        }
                        WardExpiration.SetExpired(ward, action == Operation.Expire, actor.PlayerID, actor.PlayerName);
                        message = action == Operation.Expire ? "Ward marked expired." : "Ward expiration cleared.";
                        break;
                    case Operation.ToggleSelfPermit:
                        if (ward.GetBool(ZDOVars.s_enabled, false)
                            || (WardPasswordProtection.IsPasswordProtectionActive(ward)
                                && !WardZdoUtils.HasDirectAccessToWardZdo(ward, actor.PlayerID)))
                        {
                            message = "You cannot change your permission on this ward.";
                            return;
                        }
                        if (WardZdoUtils.IsExplicitlyPermitted(ward, actor.PlayerID))
                        {
                            WardZdoUtils.RemovePermitted(ward, actor.PlayerID, out _);
                            message = "You were removed from the ward's permitted list.";
                        }
                        else
                        {
                            WardZdoUtils.AddPermitted(ward, actor.PlayerID, actor.PlayerName);
                            message = "You were added to the ward's permitted list.";
                        }
                        break;
                }
                WardZdoUtils.UpdateWardRuntimeFields(ward);
                PrivateArea loaded = WardZdoUtils.FindLoadedWard(wardID);
                if (loaded != null)
                    RefreshWardVisuals(loaded);
            }
            catch (Exception error)
            {
                message = "The ward request could not be completed. Refresh its state before retrying.";
                Debug.LogWarning($"[ProtectiveWards] Ward control failed: {error.GetType().Name}.");
            }
            finally
            {
                if (requestID != 0L)
                    SendResult(sender, requestID, action, message);
            }
        }

        private static bool CanManageMembersByCommand(ZDO ward, long playerID)
        {
            // Keep the documented console delegation policy distinct from creator-only settings editing.
            return WardZdoUtils.HasDirectAccessToWardZdo(ward, playerID)
                || (WardExpiration.IsWardActive(ward)
                    && ward.HasConnectedWardAccess(playerID, wardAccessConnectedAccessMode.Value, WardExpiration.IsWardActive));
        }

        private static string ChangeMember(ZDO ward, Operation action, string query)
        {
            if (query.Length == 0)
                return "Specify a player name.";
            List<KeyValuePair<long, string>> matches;
            if (action == Operation.Permit)
                matches = WardPlayers.FindByName(query);
            else
            {
                List<KeyValuePair<long, string>> permitted = WardZdoUtils.GetPermittedPlayers(ward);
                matches = permitted.Where(p => string.Equals(p.Value, query, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 0)
                    matches = permitted.Where(p => p.Value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            if (matches.Count == 0)
                return "No matching player was found.";
            if (matches.Count != 1)
                return "More than one player matches. Use the exact name.";
            KeyValuePair<long, string> target = matches[0];
            if (action == Operation.Permit)
            {
                if (WardZdoUtils.IsExplicitlyPermitted(ward, target.Key))
                    return "The player is already permitted.";
                WardZdoUtils.AddPermitted(ward, target.Key, target.Value);
                return $"Added {target.Value} to the ward's permitted list.";
            }
            WardZdoUtils.RemovePermitted(ward, target.Key, out _);
            return $"Removed {target.Value} from the ward's permitted list.";
        }

        private static void SetEnabled(ZDO ward, bool enabled)
        {
            PrivateArea loaded = WardZdoUtils.FindLoadedWard(ward.m_uid);
            if (loaded != null)
                loaded.SetEnabled(enabled);
            else
                ward.Set(ZDOVars.s_enabled, enabled);
        }

        private static void SendResult(long peerID, long requestID, Operation action, string message)
        {
            ZPackage response = new();
            response.Write(requestID);
            response.Write((int)action);
            response.Write(message);
            if (peerID == 0L)
                HandleResult(0L, new ZPackage(response.GetArray()));
            else
                ZRoutedRpc.instance?.InvokeRoutedRPC(peerID, RpcResult, response);
        }

        private static void HandleResult(long sender, ZPackage package)
        {
            if (!IsServerRpcSender(sender) || s_network != ZNet.instance)
                return;
            long id = package.ReadLong();
            Operation action = (Operation)package.ReadInt();
            string message = package.ReadString();
            if (message.Length > 1024 || package.GetPos() != package.Size()
                || !s_pending.TryGetValue(id, out PendingRequest request) || request.Action != action)
                return;
            s_pending.Remove(id);
            Report(request.Console, message);
        }

        private static void Report(Terminal console, string message)
        {
            if (console != null)
                console.AddString(message);
            else
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, message);
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_RegisterControl
        {
            private static void Postfix()
            {
                WardRpc.Register(RpcResult, HandleResult);
                if (ZNet.instance?.IsServer() == true)
                    WardRpc.Register(RpcRequest, HandleRequest);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static class Player_Update_ExpirePendingControl
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || s_pending.Count == 0)
                    return;
                foreach (long id in s_pending.Where(entry => Time.realtimeSinceStartup >= entry.Value.Deadline).Select(entry => entry.Key).ToArray())
                {
                    PendingRequest request = s_pending[id];
                    s_pending.Remove(id);
                    Report(request.Console, "No ward response was received. The outcome is unknown; check the ward before retrying.");
                }
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ClearControl
        {
            private static void Postfix()
            {
                s_pending.Clear();
                s_network = null;
            }
        }

        // All managed ward mutations use the authenticated server operation above, not an owner relay.
        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.RPC_ToggleEnabled))]
        private static class PrivateArea_RPC_ToggleEnabled_RejectLegacyMutation
        {
            private static bool Prefix(PrivateArea __instance) => !IsPlayerWardPrefab(__instance);
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.RPC_TogglePermitted))]
        private static class PrivateArea_RPC_TogglePermitted_RejectLegacyMutation
        {
            private static bool Prefix(PrivateArea __instance) => !IsPlayerWardPrefab(__instance);
        }
    }
}
''')
    add_compile('WardControl.cs')
    text = read('AdminServerFeatures.cs')
    for name in ['PermitPlayer','UnpermitPlayer','SetWardEnabled','ToggleWardPermitted','SetWardExpired']:
        text = re.sub(r'^ *private const string RPC_' + name + r' = [^\n]+\n', '', text, flags=re.M)
        text = re.sub(r'^ *WardRpc.Register\(RPC_' + name + r', [^\n]+\n', '', text, flags=re.M)
    start = text.index('        private static void RequestPermit(string query, Terminal context)')
    end = text.index('        [HarmonyPatch(typeof(Terminal)', start)
    new = '''        private static void RequestPermit(string query, Terminal context) => RequestControl(WardControl.Operation.Permit, query, context);
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

'''
    text = text[:start] + new + text[end:]
    write('AdminServerFeatures.cs', text)
    text = read('ProtectiveWards.cs')
    old = '''                    if (!CanPermittedPlayersToggleWard(__instance, playerID))
                        return true;

                    AdminServerFeatures.RequestPermittedWardToggle(__instance, playerID);
                    __result = true;
                    return false;'''
    new = '''                    if (!IsPlayerWardPrefab(__instance) || __instance.m_nview?.IsValid() != true || playerID == 0L)
                        return true;

                    bool canToggle = __instance.m_piece?.GetCreator() == playerID
                        || HasWardManagementAccess(__instance, playerID)
                        || CanPermittedPlayersToggleWard(__instance, playerID);
                    if (canToggle)
                        WardControl.Request(__instance.m_nview.GetZDO().m_uid, WardControl.Operation.Toggle);
                    else if (!__instance.IsEnabled())
                        WardControl.Request(__instance.m_nview.GetZDO().m_uid, WardControl.Operation.ToggleSelfPermit);
                    else
                        return false;
                    __result = true;
                    return false;'''
    text = replace(text, old, new)
    start = text.index('        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.RPC_ToggleEnabled))]')
    end = text.index('\n        }\n', start) + len('\n        }\n')
    text = text[:start] + text[end:]
    start = text.index('            Player loadedPlayer = Player.GetPlayer(claimedPlayerID);')
    end = text.index('            return false;\n        }', start)
    text = text[:start] + text[end:]
    write('ProtectiveWards.cs', text)
    text = read('WardExpiration.cs')
    text = re.sub(r'^ *private const string RPC_ActivateConnectedWards[^\n]+\n', '', text, flags=re.M)
    text = re.sub(r'^ *WardRpc.Register\(RPC_ActivateConnectedWards[^\n]+\n', '', text, flags=re.M)
    for signature in ['internal static void RequestConnectedActivation(ZDOID wardID, long playerID)', 'private static void RPC_ActivateConnectedWardsServer(long sender, ZPackage package)']:
        text = method(text, signature, '// Connected activation is committed with the authenticated root toggle.')
    write('WardExpiration.cs', text)
    commit('fix: authorize ward toggles and acknowledge console commands on the server', 'WardControl.cs', 'ProtectiveWards.csproj', 'AdminServerFeatures.cs', 'ProtectiveWards.cs', 'WardExpiration.cs')
