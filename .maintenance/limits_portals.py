from support import *

def apply():
    text = read('AdminServerFeatures.cs')
    text = replace(text, '        private const string RPC_CheckWardBuildLimit = "PW_CheckWardBuildLimit";\n', '')
    text = replace(text, '        private static readonly HashSet<ZDOID> s_requestedWardLimitChecks = new();', '''        private static readonly HashSet<ZDOID> s_knownWards = new();
        private static readonly List<ZDOID> s_pendingNewWards = new();
        private static bool s_trackingBuilds;
        private static float s_nextBuildLimitCheck;''')
    text = replace(text, '''            if (ZNet.instance?.IsServer() == true)
            {
                WardRpc.Register(RPC_CheckWardBuildLimit, RPC_CheckWardBuildLimitServer);
            }

''', '')
    text = replace(text, '            s_requestedWardLimitChecks.Clear();', '''            s_trackingBuilds = false;
            s_knownWards.Clear();
            s_pendingNewWards.Clear();''')
    text = replace(text, '            private static void Postfix() => RegisterRPCs();', '''            private static void Postfix()
            {
                RegisterRPCs();
                InitializeBuildLimitTracking();
            }''')
    start = text.index('        [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]')
    end = text.index('        private static void SendDestroyWardForBuildLimit(', start)
    new = '''        private static void InitializeBuildLimitTracking()
        {
            s_trackingBuilds = false;
            s_knownWards.Clear();
            s_pendingNewWards.Clear();
            if (ZNet.instance?.IsServer() != true)
                return;
            // Existing saves are grandfathered. Only newly observed world objects are candidates.
            foreach (ZDO ward in WardZdoUtils.GetAllWards())
                s_knownWards.Add(ward.m_uid);
            s_nextBuildLimitCheck = 0f;
            s_trackingBuilds = true;
        }

        internal static void ObserveWardForBuildLimit(ZDO ward)
        {
            if (!s_trackingBuilds || !WardZdoUtils.IsWard(ward) || !s_knownWards.Add(ward.m_uid))
                return;
            if (wardBuildLimitPerPlayer.Value > 0)
                s_pendingNewWards.Add(ward.m_uid);
        }

        internal static void ForgetWardForBuildLimit(ZDOID ward)
        {
            s_knownWards.Remove(ward);
            s_pendingNewWards.Remove(ward);
        }

        internal static void UpdateBuildLimitChecks()
        {
            if (!s_trackingBuilds || ZNet.instance?.IsServer() != true
                || s_pendingNewWards.Count == 0 || Time.realtimeSinceStartup < s_nextBuildLimitCheck)
                return;
            s_nextBuildLimitCheck = Time.realtimeSinceStartup + 1f;
            // Evaluate newest candidates first, so a same-frame burst keeps the earlier valid placements.
            foreach (ZDOID id in s_pendingNewWards.ToArray().Reverse())
            {
                ZDO ward = ZDOMan.instance?.GetZDO(id);
                if (!WardZdoUtils.IsWard(ward) || wardBuildLimitPerPlayer.Value <= 0)
                {
                    s_pendingNewWards.Remove(id);
                    continue;
                }
                long creator = ward.GetLong(ZDOVars.s_creator, 0L);
                if (creator == 0L)
                    continue; // A later ZDO revision will contain SetCreator's data.
                s_pendingNewWards.Remove(id);
                int total = WardZdoUtils.CountWardsByCreator(creator);
                int limit = wardBuildLimitPerPlayer.Value;
                if (total > limit)
                    SendDestroyWardForBuildLimit(ward, total - 1, limit);
            }
        }

'''
    text = text[:start] + new + text[end:]
    write('AdminServerFeatures.cs', text)
    text = read('WardZdoUtils.cs')
    text = replace(text, '''            s_wardObjects.Add(zdo);
            EnsureWardSettingsInitialized(zdo);''', '''            s_wardObjects.Add(zdo);
            EnsureWardSettingsInitialized(zdo);
            AdminServerFeatures.ObserveWardForBuildLimit(zdo);''')
    text = replace(text, '''            if (zdo != null && IsWard(zdo))
                s_wardObjects.Remove(zdo);''', '''            if (zdo != null && IsWard(zdo))
            {
                s_wardObjects.Remove(zdo);
                AdminServerFeatures.ForgetWardForBuildLimit(zdo.m_uid);
            }''')
    text = replace(text, '''                    s_wardObjects.Add(__instance);
                    EnsureWardSettingsInitialized(__instance);''', '''                    AddIfWard(__instance);''')
    write('WardZdoUtils.cs', text)
    text = read('ProtectiveWards.cs')
    text = replace(text, '            WardExpiration.Update();', '            WardExpiration.Update();\n            AdminServerFeatures.UpdateBuildLimitChecks();')
    write('ProtectiveWards.cs', text)
    commit('fix: enforce new ward limits after server-side ZDO initialization', 'AdminServerFeatures.cs', 'WardZdoUtils.cs', 'ProtectiveWards.cs')

    text = read('FullProtection.cs')
    text = replace(text, '''            package.Write(sourceZdo.m_uid);
            package.Write(player.GetPlayerID());''', '''            package.Write(sourceZdo.m_uid);
            package.Write(player.GetPlayerID());
            package.Write(sourceZdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal));''')
    text = method(text, 'private static void RPC_CheckTeleportTargetAccessServer(long sender, ZPackage package)', '''private static void RPC_CheckTeleportTargetAccessServer(long sender, ZPackage package)
{
    ZDOID sourceZdoID = package.ReadZDOID();
    long playerID = package.ReadLong();
    ZDOID expectedTarget = package.ReadZDOID();
    if (ZDOMan.instance == null || package.GetPos() != package.Size()
        || !TryGetRoutedPlayer(sender, playerID, out RoutedPlayerContext requester))
        return;

    ZDO source = ZDOMan.instance.GetZDO(sourceZdoID);
    string blockingOwnerName = "";
    bool granted = source != null && !expectedTarget.IsNone()
        && source.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal) == expectedTarget
        && IsSourcePortalUsableByRequester(source, requester, out blockingOwnerName);
    ZDO target = granted ? ZDOMan.instance.GetZDO(expectedTarget) : null;
    granted = granted && target != null
        && IsTeleportTargetAccessibleToPlayer(target.GetPosition(), requester.PlayerID, out blockingOwnerName);
    SendTeleportTargetAccessResponse(sender, sourceZdoID, expectedTarget, granted, blockingOwnerName);
}''')
    text = replace(text, '''private static void SendTeleportTargetAccessResponse(long peerID, ZDOID sourceZdoID, bool granted, string blockingOwnerName)''', '''private static void SendTeleportTargetAccessResponse(long peerID, ZDOID sourceZdoID, ZDOID targetZdoID, bool granted, string blockingOwnerName)''')
    text = replace(text, '''            response.Write(sourceZdoID);
            response.Write(granted);''', '''            response.Write(sourceZdoID);
            response.Write(targetZdoID);
            response.Write(granted);''')
    text = replace(text, 'ZRoutedRpc.instance.InvokeRoutedRPC(peerID, RPC_TeleportTargetAccessResponse, response)', 'WardRpc.SendResponse(peerID, RPC_TeleportTargetAccessResponse, response)')
    text = replace(text, '''            ZDOID sourceZdoID = package.ReadZDOID();
            bool granted = package.ReadBool();''', '''            ZDOID sourceZdoID = package.ReadZDOID();
            ZDOID approvedTarget = package.ReadZDOID();
            bool granted = package.ReadBool();''')
    text = replace(text, '''            teleport.Teleport(player);''', '''            if (teleport.m_nview.GetZDO().GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal) != approvedTarget)
            {
                player.Message(MessageHud.MessageType.Center, "Portal destination changed. Try again.");
                return;
            }
            teleport.Teleport(player);''')
    write('FullProtection.cs', text)
    text = read('WardRequestFlow.cs')
    text = replace(text, '''            ["PW_ReactivateExpiredWard"] = "PW_ReactivateExpiredWardResult"''', '''            ["PW_ReactivateExpiredWard"] = "PW_ReactivateExpiredWardResult",
            ["PW_CheckTeleportTargetAccess"] = "PW_TeleportTargetAccessResponse"''')
    text = replace(text, 'Deadline = Time.realtimeSinceStartup + 20f', 'Deadline = Time.realtimeSinceStartup + (method == "PW_CheckTeleportTargetAccess" ? 5f : 20f)')
    text = replace(text, '''                if (!completed || envelope.GetPos() != envelope.Size() || payload.Size() > 65536)''', '''                if (!completed || Time.realtimeSinceStartup >= request.Deadline
                    || envelope.GetPos() != envelope.Size() || payload.Size() > 65536)''')
    write('WardRequestFlow.cs', text)
    commit('fix: bind portal access approval to the requested destination and operation', 'FullProtection.cs', 'WardRequestFlow.cs')
