using BepInEx.Bootstrap;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards.Compatibility
{
    internal static class EpicLootCompat
    {
        internal const string PluginGuid = "randyknapp.mods.epicloot";
        private const string LegacyUnityLibAssemblyName = "EpicLoot-UnityLib";
        private const string EnchantingTableTypeName = "EpicLoot_UnityLib.EnchantingTable";

        private static MethodInfo s_enchantingTableInteract;

        internal static bool IsEnabled { get; private set; }

        internal static void CheckForCompatibility()
        {
            IsEnabled = false;
            s_enchantingTableInteract = null;

            if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out BepInEx.PluginInfo pluginInfo))
                return;

            Type enchantingTableType;
            Assembly legacyUnityLibAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, LegacyUnityLibAssemblyName, StringComparison.OrdinalIgnoreCase));

            if (legacyUnityLibAssembly != null)
            {
                enchantingTableType = legacyUnityLibAssembly.GetType(
                    EnchantingTableTypeName,
                    throwOnError: false,
                    ignoreCase: false);
            }
            else
            {
                Assembly epicLootAssembly = pluginInfo.Instance?.GetType().Assembly;
                enchantingTableType = epicLootAssembly?.GetType(
                    EnchantingTableTypeName,
                    throwOnError: false,
                    ignoreCase: false);
            }

            if (enchantingTableType == null)
                return;

            s_enchantingTableInteract = AccessTools.Method(
                enchantingTableType,
                nameof(Interactable.Interact),
                new[] { typeof(Humanoid), typeof(bool), typeof(bool) });

            if (s_enchantingTableInteract == null)
                return;

            FullProtection.ExcludeInteractableType(enchantingTableType);
            IsEnabled = true;
        }

        [HarmonyPatch]
        private static class EnchantingTable_Interact_PreventUnauthorizedCraftingStationAccess
        {
            private static bool Prepare(MethodBase original)
            {
                if (!IsEnabled || s_enchantingTableInteract == null)
                    return false;

                if (original == null)
                    LogInfo($"{EnchantingTableTypeName}.Interact is patched as a crafting station");

                return true;
            }

            private static MethodBase TargetMethod() => s_enchantingTableInteract;

            [HarmonyPriority(Priority.First)]
            private static bool Prefix(object __instance, Humanoid user, ref bool __result)
            {
                if (!wardAccessProtectCraftingStations.Value)
                    return true;

                if (__instance is not Component component)
                    return true;

                if (!FullProtection.BlockProtectedInteraction(component, user, ref __result))
                    return true;

                return false;
            }
        }
    }
}
