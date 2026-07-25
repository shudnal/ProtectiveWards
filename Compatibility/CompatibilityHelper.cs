namespace ProtectiveWards.Compatibility
{
    internal static class CompatibilityHelper
    {
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
