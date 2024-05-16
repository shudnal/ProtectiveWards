using HarmonyLib;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal class RaidProtection
    {
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateBaseValue))]
        public static class Player_UpdateBaseValue_SittingRaidProtection
        {
            public static void Postfix(Player __instance, float ___m_baseValueUpdateTimer, ref int ___m_baseValue, ZNetView ___m_nview)
            {
                if (!modEnabled.Value || !sittingRaidProtection.Value)
                    return;

                if ((___m_baseValueUpdateTimer == 0f) && (___m_baseValue >= 3))
                {
                    if (!__instance.IsSitting() || !__instance.m_attached || !__instance.m_seman.HaveStatusEffect(SEMan.s_statusEffectCampFire) || !InsideEnabledPlayersArea(__instance.transform.position))
                        return;

                    ___m_baseValue = baseValueProtected;

                    ZNet.instance.m_serverSyncedPlayerData["baseValue"] = ___m_baseValue.ToString();
                    ___m_nview.GetZDO().Set(ZDOVars.s_baseValue, ___m_baseValue);
                }
            }
        }

        [HarmonyPatch(typeof(RandEventSystem), nameof(RandEventSystem.CheckBase))]
        public static class RandEventSystem_CheckBase_SittingRaidProtection
        {
            public static void Postfix(RandEventSystem.PlayerEventData player, ref bool __result)
            {
                if (!modEnabled.Value || !sittingRaidProtection.Value || __result == false)
                    return;

                if (player.baseValue != baseValueProtected)
                    return;

                LogInfo($"Player at {player.position.x} {player.position.z} is in raid protected state.");
                __result = false;
            }
        }

    }
}
