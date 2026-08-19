using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards.Compatibility
{
    internal static class GuildsCompat
    {
        private const string PluginGuid = "org.bepinex.plugins.guilds";
        private const string ApiTypeName = "Guilds.API";
        private const string PlayerReferenceTypeName = "Guilds.PlayerReference";
        private const string GuildTypeName = "Guilds.Guild";
        private const string GuildGeneralTypeName = "Guilds.GuildGeneral";
        private const string RPC_UpdateGuildBinding = "PW_UpdateGuildBinding";
        private const string RPC_UpdateGuildBindingResult = "PW_UpdateGuildBindingResult";
        private const float PlayerGuildCacheSeconds = 1f;

        internal static readonly int s_guildAccessEnabled = "pw_guild_access_enabled".GetStableHashCode();
        internal static readonly int s_guildId = "pw_guild_id".GetStableHashCode();
        internal static readonly int s_guildName = "pw_guild_name".GetStableHashCode();

        private static PluginInfo s_plugin;
        private static Assembly s_assembly;
        private static Type s_apiType;
        private static Type s_playerReferenceType;
        private static Type s_guildType;
        private static Type s_guildGeneralType;
        private static MethodInfo s_getPlayerGuildByPlayer;
        private static MethodInfo s_getPlayerGuildByReference;
        private static MethodInfo s_playerReferenceFromPlayerInfo;
        private static FieldInfo s_guildNameField;
        private static FieldInfo s_guildGeneralField;
        private static FieldInfo s_guildIdField;
        private static bool s_rpcRegistered;
        private static readonly Dictionary<long, CachedGuildIdentity> s_playerGuildCache = new();

        internal static bool IsEnabled { get; private set; }

        internal readonly struct GuildIdentity
        {
            internal readonly int Id;
            internal readonly string Name;

            internal GuildIdentity(int id, string name)
            {
                Id = id;
                Name = name ?? "";
            }

            internal bool IsValid => Id > 0 && !string.IsNullOrEmpty(Name);
        }

        internal enum GuildBindingResult
        {
            Success,
            NotAuthorized,
            Unavailable,
            NoGuild
        }

        private sealed class CachedGuildIdentity
        {
            internal float CachedAt;
            internal bool HasGuild;
            internal GuildIdentity Guild;
        }

        internal static void CheckForCompatibility()
        {
            IsEnabled = false;

            if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out s_plugin))
                return;

            s_assembly = s_plugin.Instance.GetType().Assembly;
            s_apiType = s_assembly.GetType(ApiTypeName);
            s_playerReferenceType = s_assembly.GetType(PlayerReferenceTypeName);
            s_guildType = s_assembly.GetType(GuildTypeName);
            s_guildGeneralType = s_assembly.GetType(GuildGeneralTypeName);

            if (s_apiType == null
                || s_playerReferenceType == null
                || s_guildType == null
                || s_guildGeneralType == null)
            {
                LogInfo("Guilds is loaded but the required membership types could not be resolved");
                return;
            }

            s_getPlayerGuildByPlayer = AccessTools.Method(s_apiType, "GetPlayerGuild", new[] { typeof(Player) });
            s_getPlayerGuildByReference = s_apiType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "GetPlayerGuild"
                                          && method.GetParameters().Length == 1
                                          && method.GetParameters()[0].ParameterType == s_playerReferenceType);
            s_playerReferenceFromPlayerInfo = AccessTools.Method(s_playerReferenceType, "fromPlayerInfo", new[] { typeof(ZNet.PlayerInfo) });
            s_guildNameField = AccessTools.Field(s_guildType, "Name");
            s_guildGeneralField = AccessTools.Field(s_guildType, "General");
            s_guildIdField = AccessTools.Field(s_guildGeneralType, "id");

            if (s_getPlayerGuildByPlayer == null
                || s_getPlayerGuildByReference == null
                || s_playerReferenceFromPlayerInfo == null
                || s_guildNameField == null
                || s_guildGeneralField == null
                || s_guildIdField == null)
            {
                LogInfo("Guilds is loaded but the required membership API could not be resolved");
                return;
            }

            IsEnabled = true;
        }

        internal static void ResetRuntimeState()
        {
            s_rpcRegistered = false;
            s_playerGuildCache.Clear();
        }

        internal static bool IsGuildAccessEnabled(ZDO zdo)
        {
            return ArePerWardSettingsEnabled()
                   && zdo != null
                   && zdo.GetBool(s_guildAccessEnabled, false)
                   && HasBoundGuild(zdo);
        }

        internal static bool HasBoundGuild(ZDO zdo)
        {
            return zdo != null
                   && zdo.GetInt(s_guildId, 0) > 0
                   && !string.IsNullOrEmpty(zdo.GetString(s_guildName, ""));
        }

        internal static int GetBoundGuildId(ZDO zdo) => zdo?.GetInt(s_guildId, 0) ?? 0;

        internal static string GetBoundGuildName(ZDO zdo) => zdo?.GetString(s_guildName, "") ?? "";

        internal static void SetGuildAccessEnabled(ZDO zdo, bool enabled)
        {
            if (zdo == null)
                return;

            zdo.Set(s_guildAccessEnabled, enabled && HasBoundGuild(zdo));
        }

        internal static void SetBoundGuildState(ZDO zdo, bool enabled, int guildId, string guildName)
        {
            if (zdo == null)
                return;

            zdo.Set(s_guildId, Math.Max(guildId, 0));
            zdo.Set(s_guildName, guildName ?? "");
            zdo.Set(s_guildAccessEnabled, enabled && guildId > 0 && !string.IsNullOrEmpty(guildName));
        }

        internal static bool HasWardGuildAccess(PrivateArea ward, long playerID)
        {
            if (ward?.m_nview?.IsValid() != true)
                return false;

            return HasWardGuildAccess(ward.m_nview.GetZDO(), playerID);
        }

        internal static bool HasWardGuildAccess(ZDO zdo, long playerID)
        {
            if (!IsEnabled || playerID == 0L || !IsGuildAccessEnabled(zdo))
                return false;

            if (!TryGetPlayerGuild(playerID, out GuildIdentity playerGuild))
                return false;

            return playerGuild.Id == GetBoundGuildId(zdo)
                   && string.Equals(playerGuild.Name, GetBoundGuildName(zdo), StringComparison.Ordinal);
        }

        internal static bool TryGetPlayerGuild(long playerID, out GuildIdentity guild)
        {
            guild = default;
            if (!IsEnabled || playerID == 0L)
                return false;

            float now = Time.realtimeSinceStartup;
            if (s_playerGuildCache.TryGetValue(playerID, out CachedGuildIdentity cached)
                && now - cached.CachedAt <= PlayerGuildCacheSeconds)
            {
                guild = cached.Guild;
                return cached.HasGuild;
            }

            bool hasGuild = TryResolvePlayerGuild(playerID, out guild);
            s_playerGuildCache[playerID] = new CachedGuildIdentity
            {
                CachedAt = now,
                HasGuild = hasGuild,
                Guild = guild
            };
            return hasGuild;
        }

        private static bool TryResolvePlayerGuild(long playerID, out GuildIdentity guild)
        {
            guild = default;

            try
            {
                object guildObject = null;
                if (TryFindPlayerInfo(playerID, out ZNet.PlayerInfo playerInfo))
                {
                    object playerReference = s_playerReferenceFromPlayerInfo.Invoke(null, new object[] { playerInfo });
                    guildObject = s_getPlayerGuildByReference.Invoke(null, new[] { playerReference });
                }

                if (guildObject == null)
                {
                    Player player = Player.GetPlayer(playerID);
                    if (player != null)
                        guildObject = s_getPlayerGuildByPlayer.Invoke(null, new object[] { player });
                }

                return TryReadGuildIdentity(guildObject, out guild);
            }
            catch (Exception ex)
            {
                LogInfo($"Guild membership lookup failed: {ex.GetType().Name}");
                return false;
            }
        }

        private static bool TryReadGuildIdentity(object guildObject, out GuildIdentity guild)
        {
            guild = default;
            if (guildObject == null)
                return false;

            object general = s_guildGeneralField.GetValue(guildObject);
            if (general == null)
                return false;

            int id = (int)s_guildIdField.GetValue(general);
            string name = s_guildNameField.GetValue(guildObject) as string ?? "";
            guild = new GuildIdentity(id, name);
            return guild.IsValid;
        }

        internal static void RequestBindCurrentGuild(ZDOID wardID)
        {
            RequestGuildBindingUpdate(wardID, bind: true);
        }

        internal static void RequestUnbindGuild(ZDOID wardID)
        {
            RequestGuildBindingUpdate(wardID, bind: false);
        }

        private static void RequestGuildBindingUpdate(ZDOID wardID, bool bind)
        {
            Player player = Player.m_localPlayer;
            if (!IsEnabled || player == null || wardID.IsNone())
            {
                WardSettingsUI.OnGuildBindingResult(wardID, GuildBindingResult.Unavailable, false, 0, "");
                return;
            }

            ZPackage package = new();
            package.Write(wardID);
            package.Write(player.GetPlayerID());
            package.Write(bind);

            if (ZNet.instance?.IsServer() == true)
                RPC_UpdateGuildBindingServer(0L, new ZPackage(package.GetArray()));
            else if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_UpdateGuildBinding, package);
            else
                WardSettingsUI.OnGuildBindingResult(wardID, GuildBindingResult.Unavailable, false, 0, "");
        }

        private static void RegisterRPCs()
        {
            if (!IsEnabled || s_rpcRegistered || ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.Register<ZPackage>(RPC_UpdateGuildBindingResult, RPC_UpdateGuildBindingResultClient);
            if (ZNet.instance?.IsServer() == true)
                ZRoutedRpc.instance.Register<ZPackage>(RPC_UpdateGuildBinding, RPC_UpdateGuildBindingServer);

            s_rpcRegistered = true;
        }

        private static void RPC_UpdateGuildBindingServer(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            long claimedPlayerID = package.ReadLong();
            bool bind = package.ReadBool();

            if (!TryGetRoutedPlayer(sender, claimedPlayerID, out RoutedPlayerContext requester))
                return;

            if (!WardZdoUtils.TryGetWard(wardID, out ZDO zdo))
            {
                SendGuildBindingResult(sender, wardID, GuildBindingResult.Unavailable, null);
                return;
            }

            if (!CanApplyWardSettings(zdo, requester.PlayerID))
            {
                SendGuildBindingResult(sender, wardID, GuildBindingResult.NotAuthorized, zdo);
                return;
            }

            if (bind)
            {
                s_playerGuildCache.Remove(requester.PlayerID);
                if (!TryGetPlayerGuild(requester.PlayerID, out GuildIdentity guild))
                {
                    SendGuildBindingResult(sender, wardID, GuildBindingResult.NoGuild, zdo);
                    return;
                }

                SetBoundGuildState(zdo, true, guild.Id, guild.Name);
                LogInfo($"Bound ward {wardID} to guild {guild.Name} ({guild.Id})");
            }
            else
            {
                SetBoundGuildState(zdo, false, 0, "");
                LogInfo($"Removed guild binding from ward {wardID}");
            }

            SendGuildBindingResult(sender, wardID, GuildBindingResult.Success, zdo);
        }

        private static void SendGuildBindingResult(long peerID, ZDOID wardID, GuildBindingResult result, ZDO zdo)
        {
            ZPackage response = new();
            response.Write(wardID);
            response.Write((int)result);
            response.Write(zdo != null && zdo.GetBool(s_guildAccessEnabled, false));
            response.Write(GetBoundGuildId(zdo));
            response.Write(GetBoundGuildName(zdo));

            if (ZNet.instance?.IsServer() == true && ZRoutedRpc.instance != null && peerID != 0L)
                ZRoutedRpc.instance.InvokeRoutedRPC(peerID, RPC_UpdateGuildBindingResult, response);
            else
                RPC_UpdateGuildBindingResultClient(0L, new ZPackage(response.GetArray()));
        }

        private static void RPC_UpdateGuildBindingResultClient(long _, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            GuildBindingResult result = (GuildBindingResult)package.ReadInt();
            bool enabled = package.ReadBool();
            int guildId = package.ReadInt();
            string guildName = package.ReadString() ?? "";

            WardSettingsUI.OnGuildBindingResult(wardID, result, enabled, guildId, guildName);
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_RegisterGuildCompatibilityRPCs
        {
            private static bool Prepare() => IsEnabled;

            private static void Postfix() => RegisterRPCs();
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ResetGuildCompatibility
        {
            private static bool Prepare() => IsEnabled;

            private static void Postfix() => ResetRuntimeState();
        }
    }
}
