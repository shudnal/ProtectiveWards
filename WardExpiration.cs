using HarmonyLib;
using System;
using System.Text;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardExpiration
    {
        internal static readonly int s_expirationLastActiveUnix = "pw_expiration_last_active_unix".GetStableHashCode();
        internal static readonly int s_expirationExpired = "pw_expiration_expired".GetStableHashCode();
        internal static readonly int s_expirationLastPlayerId = "pw_expiration_last_player_id".GetStableHashCode();
        internal static readonly int s_expirationLastPlayerName = "pw_expiration_last_player_name".GetStableHashCode();
        internal static readonly int s_expirationExpiredUnix = "pw_expiration_expired_unix".GetStableHashCode();
        internal static readonly int s_expirationExpiredReason = "pw_expiration_expired_reason".GetStableHashCode();

        private static float s_nextCheckTime;

        internal static void Update()
        {
            if (wardExpirationMinutes.Value <= 0)
                return;

            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZNet.IsSinglePlayer)
                return;

            if (Time.time < s_nextCheckTime)
                return;

            s_nextCheckTime = Time.time + GetCheckIntervalSeconds();
            CheckAllWardZdos();
        }

        private static float GetCheckIntervalSeconds()
        {
            int minutes = Math.Max(wardExpirationMinutes.Value, 1);
            if (minutes < 10)
                return 60f;

            return Mathf.Clamp(minutes / 10f, 10f, 60f) * 60f;
        }

        private static void CheckAllWardZdos()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long expirationSeconds = Math.Max(wardExpirationMinutes.Value, 1) * 60L;

            foreach (ZDO zdo in WardZdoUtils.GetAllGuardStoneZdos())
            {
                if (zdo == null)
                    continue;

                if (zdo.GetLong(s_expirationLastActiveUnix, 0L) == 0L)
                    zdo.Set(s_expirationLastActiveUnix, now);

                Player activePlayer = FindRefreshingPlayer(zdo);
                if (activePlayer != null)
                {
                    RefreshActivity(zdo, activePlayer, now);

                    if (IsExpired(zdo) && wardExpirationReactivationMode.Value == WardExpirationReactivationMode.AutomaticOnLogin)
                        Reactivate(zdo, activePlayer, now);

                    continue;
                }

                if (IsExpired(zdo))
                    continue;

                long lastActive = zdo.GetLong(s_expirationLastActiveUnix, now);
                if (now - lastActive < expirationSeconds)
                    continue;

                Expire(zdo, now);
            }
        }

        private static Player FindRefreshingPlayer(ZDO zdo)
        {
            if (zdo == null)
                return null;

            WardConnectedAccessMode mode = wardExpirationConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardExpirationConnectedAccessMode.Value;

            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null)
                    continue;

                long playerID = player.GetPlayerID();
                switch (wardExpirationRefreshMode.Value)
                {
                    case WardExpirationRefreshMode.DirectPermitted:
                        if (WardZdoUtils.HasDirectAccessToWardZdo(zdo, playerID))
                            return player;
                        break;

                    case WardExpirationRefreshMode.EffectiveAccess:
                    default:
                        if (WardZdoUtils.HasAccessToWardOrConnectedWardZdo(zdo, playerID, mode, IsActiveForExpirationConnectedAccess))
                            return player;
                        break;
                }
            }

            return null;
        }

        private static bool IsActiveForExpirationConnectedAccess(ZDO zdo)
        {
            return WardZdoUtils.IsGuardStoneZdo(zdo)
                   && zdo.GetBool(ZDOVars.s_enabled, false)
                   && !IsExpired(zdo);
        }

        private static void RefreshActivity(ZDO zdo, Player player, long now)
        {
            zdo.Set(s_expirationLastActiveUnix, now);
            zdo.Set(s_expirationLastPlayerId, player.GetPlayerID());
            zdo.Set(s_expirationLastPlayerName, player.GetPlayerName());
        }

        private static void Expire(ZDO zdo, long now)
        {
            zdo.Set(s_expirationExpired, true);
            zdo.Set(s_expirationExpiredUnix, now);
            zdo.Set(s_expirationExpiredReason, "inactivity");
        }

        private static void Reactivate(ZDO zdo, Player player, long now)
        {
            zdo.Set(s_expirationExpired, false);
            RefreshActivity(zdo, player, now);
        }

        internal static bool IsExpired(PrivateArea ward)
        {
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
                return false;

            return IsExpired(ward.m_nview.GetZDO());
        }

        private static bool IsExpired(ZDO zdo)
        {
            return zdo != null && zdo.GetBool(s_expirationExpired, false);
        }

        private static bool CanReactivate(PrivateArea ward, Player player)
        {
            if (ward == null || player == null || ward.m_nview == null || !ward.m_nview.IsValid())
                return false;

            ZDO zdo = ward.m_nview.GetZDO();
            long playerID = player.GetPlayerID();
            WardConnectedAccessMode mode = wardExpirationConnectedAccessMode == null ? WardConnectedAccessMode.Off : wardExpirationConnectedAccessMode.Value;

            return wardExpirationRefreshMode.Value == WardExpirationRefreshMode.DirectPermitted
                ? WardZdoUtils.HasDirectAccessToWardZdo(zdo, playerID)
                : WardZdoUtils.HasAccessToWardOrConnectedWardZdo(zdo, playerID, mode, IsActiveForExpirationConnectedAccess);
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.IsEnabled))]
        private static class PrivateArea_IsEnabled_Expiration
        {
            private static bool Prefix(PrivateArea __instance, ref bool __result)
            {
                if (wardExpirationMinutes.Value <= 0 || !IsExpired(__instance))
                    return true;

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
        private static class PrivateArea_Interact_ReactivateExpiredWard
        {
            private static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, ref bool __result)
            {
                if (hold || wardExpirationMinutes.Value <= 0 || wardExpirationReactivationMode.Value != WardExpirationReactivationMode.ManualInteraction)
                    return true;

                if (!IsExpired(__instance))
                    return true;

                Player player = human as Player;
                if (!CanReactivate(__instance, player))
                    return true;

                ZDO zdo = __instance.m_nview.GetZDO();
                Reactivate(zdo, player, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                player.Message(MessageHud.MessageType.Center, "$pw_ward_expiration_reactivated");
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.AddUserList))]
        private static class PrivateArea_AddUserList_ExpirationHover
        {
            private static void Postfix(PrivateArea __instance, StringBuilder text)
            {
                if (wardExpirationMinutes.Value <= 0 || !IsExpired(__instance))
                    return;

                text.Append("\n<color=orange>$pw_ward_expiration_expired</color>");
                text.Append("\n$pw_ward_expiration_hint");

                if (!wardExpirationAdminHover.Value || !HasLocalWardAdminAccess())
                    return;

                ZDO zdo = __instance.m_nview.GetZDO();
                if (zdo == null)
                    return;

                long last = zdo.GetLong(s_expirationLastActiveUnix, 0L);
                long expired = zdo.GetLong(s_expirationExpiredUnix, 0L);
                string lastPlayer = zdo.GetString(s_expirationLastPlayerName, "");
                text.Append($"\nLast active: {last}; Expired: {expired}; Last player: {lastPlayer}");
            }
        }
    }
}
