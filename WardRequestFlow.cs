using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardRequestFlow
    {
        private static readonly Dictionary<string, string> s_results = new(StringComparer.Ordinal)
        {
            ["PW_ApplyWardSettings"] = "PW_ApplyWardSettingsResult",
            ["PW_UpdateWardPassword"] = "PW_UpdateWardPasswordResult",
            ["PW_SubmitWardPassword"] = "PW_SubmitWardPasswordResult",
            ["PW_UpdateGuildBinding"] = "PW_UpdateGuildBindingResult",
            ["PW_UpdatePermittedPlayers"] = "PW_UpdatePermittedPlayersResult",
            ["PW_ReactivateExpiredWard"] = "PW_ReactivateExpiredWardResult"
        };
        private static readonly Dictionary<long, Pending> s_pending = new();
        private static readonly Stack<DispatchContext> s_dispatch = new();

        private sealed class Pending
        {
            internal string Request;
            internal string Result;
            internal ZDOID Ward;
            internal ZNetPeer Server;
            internal float Deadline;
        }

        private sealed class DispatchContext
        {
            internal long ID;
            internal long Sender;
            internal string Result;
            internal bool Replied;
        }

        internal static bool TrySend(string method, ZPackage payload)
        {
            if (!s_results.TryGetValue(method, out string result))
                return false;

            int position = payload.GetPos();
            payload.SetPos(0);
            ZDOID ward = payload.ReadZDOID();
            payload.SetPos(position);
            Cancel(method, ward);
            if (s_pending.Count >= 32 || ZNet.instance?.GetServerPeer() == null)
            {
                Recover(ward);
                return true;
            }

            long id = Utils.GenerateUID();
            s_pending[id] = new Pending
            {
                Request = method, Result = result, Ward = ward, Server = ZNet.instance.GetServerPeer(),
                Deadline = Time.realtimeSinceStartup + 20f
            };
            ZPackage envelope = new();
            envelope.Write(id);
            envelope.Write(payload);
            ZRoutedRpc.instance.InvokeRoutedRPC(method, envelope);
            return true;
        }

        internal static void Dispatch(string method, long sender, ZPackage envelope, Action<long, ZPackage> handler)
        {
            if (s_results.TryGetValue(method, out string result))
            {
                if (ZNet.instance?.IsServer() != true)
                    return;
                long id = envelope.ReadLong();
                ZPackage payload = envelope.ReadPackage();
                if (id == 0L || envelope.GetPos() != envelope.Size() || payload.Size() > 65536)
                    return;

                DispatchContext context = new() { ID = id, Sender = sender, Result = result };
                s_dispatch.Push(context);
                try
                {
                    handler(sender, payload);
                }
                finally
                {
                    s_dispatch.Pop();
                    if (!context.Replied)
                        Reply(context, false, new ZPackage());
                }
                return;
            }

            if (s_results.ContainsValue(method))
            {
                if (!IsServerRpcSender(sender))
                    return;
                long id = envelope.ReadLong();
                bool completed = envelope.ReadBool();
                ZPackage payload = envelope.ReadPackage();
                if (!s_pending.TryGetValue(id, out Pending request) || request.Result != method
                    || request.Server != ZNet.instance?.GetServerPeer())
                    return;
                s_pending.Remove(id);
                if (!completed || envelope.GetPos() != envelope.Size() || payload.Size() > 65536)
                {
                    Recover(request.Ward);
                    return;
                }
                try
                {
                    handler(sender, payload);
                }
                catch
                {
                    Recover(request.Ward);
                    throw;
                }
                return;
            }

            handler(sender, envelope);
        }

        internal static bool TryReply(long peerID, string method, ZPackage payload)
        {
            if (s_dispatch.Count == 0)
                return false;
            DispatchContext context = s_dispatch.Peek();
            if (context.Sender != peerID || context.Result != method)
                return false;
            if (!context.Replied)
                Reply(context, true, payload);
            return true;
        }

        private static void Reply(DispatchContext context, bool completed, ZPackage payload)
        {
            context.Replied = true;
            ZPackage envelope = new();
            envelope.Write(context.ID);
            envelope.Write(completed);
            envelope.Write(payload);
            ZRoutedRpc.instance?.InvokeRoutedRPC(context.Sender, context.Result, envelope);
        }

        internal static void Cancel(string method, ZDOID ward)
        {
            foreach (long id in s_pending.Where(pair => pair.Value.Request == method && pair.Value.Ward == ward).Select(pair => pair.Key).ToArray())
                s_pending.Remove(id);
        }

        private static void Recover(ZDOID ward)
        {
            WardSettingsUI.CancelPendingNetworkOperation(ward);
            WardPermittedPlayersUI.CancelPendingNetworkOperation(ward);
            WardPasswordProtection.CancelPendingNetworkOperation(ward);
            if (!ward.IsNone())
                ZDOMan.instance?.RequestZDO(ward);
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                "Ward request outcome is unknown. Its state is being refreshed; reopen the ward before retrying.");
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static class Player_Update_RequestTimeouts
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || s_pending.Count == 0)
                    return;
                foreach (long id in s_pending.Where(pair => pair.Value.Server != ZNet.instance?.GetServerPeer()
                    || Time.realtimeSinceStartup >= pair.Value.Deadline).Select(pair => pair.Key).ToArray())
                {
                    if (!s_pending.TryGetValue(id, out Pending request))
                        continue;
                    s_pending.Remove(id);
                    Recover(request.Ward);
                }
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ClearRequests
        {
            private static void Postfix()
            {
                s_pending.Clear();
                s_dispatch.Clear();
            }
        }
    }
}
