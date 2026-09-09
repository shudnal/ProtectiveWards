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

            // EpicLoot 0.13+ owns the live table type in the plugin assembly. Prefer it even
            // when an obsolete EpicLoot-UnityLib.dll was left behind during an upgrade.
            Assembly epicLootAssembly = pluginInfo.Instance?.GetType().Assembly;
            Type enchantingTableType = epicLootAssembly?.GetType(EnchantingTableTypeName, throwOnError: false, ignoreCase: false);
            if (enchantingTableType == null)
            {
                // Before 0.13 the table lives in a separate Unity library.
                Assembly legacyUnityLibAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                    string.Equals(assembly.GetName().Name, LegacyUnityLibAssemblyName, StringComparison.OrdinalIgnoreCase));
                enchantingTableType = legacyUnityLibAssembly?.GetType(EnchantingTableTypeName, throwOnError: false, ignoreCase: false);
            }

            if (enchantingTableType == null)
                return;

            // Optional compatibility discovery must stay silent when the API is absent.
            s_enchantingTableInteract = enchantingTableType.GetMethod(nameof(Interactable.Interact),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(Humanoid), typeof(bool), typeof(bool) }, null);
            if (s_enchantingTableInteract == null || s_enchantingTableInteract.ReturnType != typeof(bool))
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
