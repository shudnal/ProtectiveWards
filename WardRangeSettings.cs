using BepInEx.Configuration;
using ConditionalConfigSync;
using System;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardRangeSettings
    {
        internal const string SyncPluginGuid = "_shudnal.ConditionalConfigSync";
        private static readonly Vector2 DefaultLimits = new(1f, 200f);
        private static readonly ConfigSync Sync = new(pluginID)
        {
            DisplayName = pluginName,
            CurrentVersion = pluginVersion,
            MinimumRequiredVersion = pluginVersion,
            ModRequired = true
        };

        internal static ConfigEntry<Vector2> Limits { get; private set; }

        internal static void Initialize(ConfigFile config)
        {
            Limits = config.Bind("Range", "Ward range limits", DefaultLimits,
                "Minimum (x) and maximum (y) effective ward radius in meters, including saved per-ward values and the default radius. "
                + "Server-controlled by default; ownership can be changed through Conditional Config Sync policy. "
                + "Endpoints must be finite and positive. Invalid endpoints use 1 and 200 respectively; reversed limits are sorted.");

            // Jotunn continues to manage the existing settings; only CCS owns this entry.
            Sync.AddConfigEntry(Limits, ConfigSyncMode.Conditional, serverControlledByDefault: true);
            Limits.SettingChanged += OnLimitsChanged;
        }

        internal static void Shutdown()
        {
            if (Limits != null)
                Limits.SettingChanged -= OnLimitsChanged;
        }

        internal static float Clamp(float radius)
        {
            Vector2 limits = Limits?.Value ?? DefaultLimits;
            float minimum = IsValidLimit(limits.x) ? limits.x : DefaultLimits.x;
            float maximum = IsValidLimit(limits.y) ? limits.y : DefaultLimits.y;
            if (minimum > maximum)
                (minimum, maximum) = (maximum, minimum);

            if (float.IsNaN(radius))
                radius = 32f;

            return Mathf.Clamp(radius, minimum, maximum);
        }

        private static bool IsValidLimit(float value) => value > 0f && !float.IsInfinity(value);

        private static void OnLimitsChanged(object sender, EventArgs args) => RefreshWardRangeLimits();
    }
}
