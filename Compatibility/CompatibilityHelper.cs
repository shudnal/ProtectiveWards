using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace ProtectiveWards.Compatibility
{
    internal static class CompatibilityHelper
    {
        internal static Type FindType(Assembly assembly, string fullName)
        {
            if (assembly == null || string.IsNullOrEmpty(fullName))
                return null;

            return AccessTools.GetTypesFromAssembly(assembly)
                .FirstOrDefault(type => string.Equals(type.FullName, fullName, StringComparison.Ordinal));
        }

        internal static void CheckForCompatibility()
        {
            GuildsCompat.CheckForCompatibility();
            EpicLootCompat.CheckForCompatibility();
        }

        internal static void ResetRuntimeState()
        {
            GuildsCompat.ResetRuntimeState();
        }
    }
}
