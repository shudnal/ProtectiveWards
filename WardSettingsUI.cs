using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardSettingsUI
    {
        private const string RPC_ApplyWardSettings = "PW_ApplyWardSettings";
        private const float MaxPanelWidth = 740f;
        private const float MinPanelWidth = 620f;
        private const float PanelHeight = 700f;
        private const int TitleFontSize = 32;
        private const int HeaderFontSize = 18;
        private const int RowFontSize = 17;
        private const float RowStep = 34f;
        private const float PanelPadding = 34f;
        private const float ColumnGap = 16f;
        private const float UseDefaultColumnWidth = 110f;
        private const float ValueWidth = 110f;
        private const float ValueGap = 8f;
        private const float ToggleSize = 26f;
        private const float MaxLabelWidth = 285f;
        private const float MinLabelWidth = 165f;

        private static float s_panelWidth = MaxPanelWidth;
        private static float s_labelX = -215f;
        private static float s_labelWidth = 260f;
        private static float s_useDefaultX = 55f;
        private static float s_useDefaultHeaderX = 55f;
        private static float s_useDefaultHeaderWidth = UseDefaultColumnWidth;
        private static float s_valueX = 190f;
        private static float s_valueHeaderX = 190f;
        private static float s_valueHeaderWidth = ValueWidth * 2f + ValueGap;
        private static float s_valueBoolX = 190f;
        private static float s_colorInputX = 170f;
        private static float s_colorButtonX = 285f;
        private static float s_sectionX = -215f;
        private static float s_sectionDividerX = 30f;
        private static float s_sectionDividerWidth = 470f;
        private static string s_layoutLanguageKey = "";

        private static GameObject s_panel;
        private static ZDO s_zdo;
        private static readonly List<WardSettingRow> s_rows = new List<WardSettingRow>();
        private static readonly Dictionary<FieldId, WardSettingValue> s_values = new Dictionary<FieldId, WardSettingValue>();
        private static bool s_rpcRegistered;
        private static bool s_inputBlocked;

        private enum SettingsPage
        {
            Main,
            BubbleVisual,
            CircleVisual
        }

        private enum FieldId
        {
            BubbleEnabled,
            BubbleColor,
            BubbleRefraction,
            BubbleWave,
            BubbleGlossiness,
            BubbleMetallic,
            BubbleNormalScale,
            BubbleDepthFade,
            CustomRange,
            Range,
            CustomColor,
            EmissionColor,
            EmissionColorMultiplier,
            CircleEnabled,
            CircleStartColor,
            CircleEndColor,
            CircleSpeed,
            CircleLength,
            CircleWidth,
            CircleAmount
        }

        private static readonly string[] s_sectionLayoutTokens =
        {
            "$pw_ward_settings_section_range",
            "$pw_ward_settings_section_emission",
            "$pw_ward_settings_section_bubble",
            "$pw_ward_settings_section_circle"
        };

        private static readonly string[] s_labelLayoutTokens =
        {
            "$pw_ward_settings_custom_range",
            "$pw_ward_settings_range",
            "$pw_ward_settings_emission_enabled",
            "$pw_ward_settings_emission_color",
            "$pw_ward_settings_emission_multiplier",
            "$pw_ward_settings_bubble_enabled",
            "$pw_ward_settings_bubble_color",
            "$pw_ward_settings_bubble_visual",
            "$pw_ward_settings_circle_enabled",
            "$pw_ward_settings_circle_visual",
            "$pw_ward_settings_bubble_refraction",
            "$pw_ward_settings_bubble_wave",
            "$pw_ward_settings_bubble_glossiness",
            "$pw_ward_settings_bubble_metallic",
            "$pw_ward_settings_bubble_normal",
            "$pw_ward_settings_bubble_depth",
            "$pw_ward_settings_circle_start",
            "$pw_ward_settings_circle_end",
            "$pw_ward_settings_circle_speed",
            "$pw_ward_settings_circle_length",
            "$pw_ward_settings_circle_width",
            "$pw_ward_settings_circle_amount"
        };

        internal static void RegisterRPCs()
        {
            if (s_rpcRegistered || ZRoutedRpc.instance == null)
                return;

            if (ZNet.instance != null && ZNet.instance.IsServer())
                ZRoutedRpc.instance.Register<ZPackage>(RPC_ApplyWardSettings, RPC_ApplyWardSettingsServer);

            s_rpcRegistered = true;
        }

        internal static void Open(PrivateArea ward)
        {
            if (ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
                return;

            Player player = Player.m_localPlayer;
            if (!CanEditWardSettings(ward, player))
            {
                player?.Message(MessageHud.MessageType.Center, "$msg_privatezone");
                return;
            }

            ZDO zdo = ward.m_nview.GetZDO();
            if (zdo == null)
                return;

            Close();

            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null)
            {
                player?.Message(MessageHud.MessageType.Center, "ProtectiveWards: GUIManager is not ready");
                return;
            }

            s_zdo = zdo;
            LoadValuesFromZDO();
            OpenPage(SettingsPage.Main);
        }

        internal static void Close()
        {
            DestroyPanel();
            s_zdo = null;
            s_values.Clear();
            SetInputBlocked(false);
        }

        private static void DestroyPanel()
        {
            if (s_panel != null)
                UnityEngine.Object.Destroy(s_panel);

            s_panel = null;
            s_rows.Clear();
        }

        private static void SetInputBlocked(bool blocked)
        {
            if (s_inputBlocked == blocked)
                return;

            GUIManager.BlockInput(blocked);
            s_inputBlocked = blocked;
        }

        private static void CaptureCurrentRows()
        {
            foreach (WardSettingRow row in s_rows)
                s_values[row.FieldId] = row.Capture();
        }

        private static void OpenPage(SettingsPage page)
        {
            if (s_zdo == null)
            {
                Close();
                return;
            }

            CaptureCurrentRows();
            DestroyPanel();
            CreatePanel(page);
            SetInputBlocked(true);
        }

        private static void EnsureLayout()
        {
            string layoutKey = BuildLayoutLanguageKey();
            if (layoutKey == s_layoutLanguageKey)
                return;

            s_layoutLanguageKey = layoutKey;

            float measuredLabelWidth = 0f;
            foreach (string token in s_labelLayoutTokens)
                measuredLabelWidth = Math.Max(measuredLabelWidth, EstimateTextWidth(token.Localize(), RowFontSize) + 14f);

            float measuredSectionWidth = 0f;
            foreach (string token in s_sectionLayoutTokens)
                measuredSectionWidth = Math.Max(measuredSectionWidth, EstimateTextWidth(token.Localize(), HeaderFontSize) + 18f);

            float valueBlockWidth = ValueWidth * 2f + ValueGap;
            s_labelWidth = Mathf.Clamp(measuredLabelWidth, MinLabelWidth, MaxLabelWidth);
            s_panelWidth = Mathf.Clamp(PanelPadding * 2f + s_labelWidth + UseDefaultColumnWidth + valueBlockWidth + ColumnGap * 2f, MinPanelWidth, MaxPanelWidth);

            float left = -s_panelWidth * 0.5f + PanelPadding;
            float labelRight = left + s_labelWidth;
            float useDefaultLeft = labelRight + ColumnGap;
            float valueLeft = useDefaultLeft + UseDefaultColumnWidth + ColumnGap;
            float panelRight = s_panelWidth * 0.5f - PanelPadding;

            s_labelX = left + s_labelWidth * 0.5f;
            s_sectionX = s_labelX;

            s_useDefaultHeaderWidth = UseDefaultColumnWidth + 30f;
            s_useDefaultHeaderX = useDefaultLeft + UseDefaultColumnWidth - 8f - s_useDefaultHeaderWidth * 0.5f;
            s_useDefaultX = useDefaultLeft + UseDefaultColumnWidth - ToggleSize * 0.5f;

            s_valueHeaderWidth = valueBlockWidth;
            s_valueHeaderX = valueLeft;
            s_valueX = valueLeft + ValueWidth * 0.5f;
            s_valueBoolX = valueLeft + ToggleSize * 0.5f + 8f;
            s_colorInputX = s_valueX;
            s_colorButtonX = s_colorInputX + ValueWidth + ValueGap;

            float dividerLeft = left + Math.Max(80f, measuredSectionWidth);
            s_sectionDividerWidth = Math.Max(80f, panelRight - dividerLeft);
            s_sectionDividerX = dividerLeft + s_sectionDividerWidth * 0.5f;
        }

        private static string BuildLayoutLanguageKey()
        {
            List<string> values = new List<string>(s_labelLayoutTokens.Length + s_sectionLayoutTokens.Length);
            foreach (string token in s_labelLayoutTokens)
                values.Add(token.Localize());
            foreach (string token in s_sectionLayoutTokens)
                values.Add(token.Localize());

            return string.Join("|", values.ToArray());
        }

        private static float EstimateTextWidth(string text, int fontSize)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;

            float width = 0f;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                    width += fontSize * 0.28f;
                else if (c <= 0x007F)
                    width += fontSize * 0.52f;
                else if (c >= 0x0400 && c <= 0x04FF)
                    width += fontSize * 0.58f;
                else if (c >= 0x2E80)
                    width += fontSize * 0.9f;
                else
                    width += fontSize * 0.62f;
            }

            return width;
        }

        private static void CreatePanel(SettingsPage page)
        {
            EnsureLayout();

            s_panel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                width: s_panelWidth,
                height: PanelHeight,
                draggable: true);
            s_panel.SetActive(true);

            string title = page == SettingsPage.BubbleVisual
                ? "$pw_ward_settings_bubble_visual_title"
                : page == SettingsPage.CircleVisual
                    ? "$pw_ward_settings_circle_visual_title"
                    : "$pw_ward_settings_title";

            CreateText(title.Localize(), new Vector2(0f, 315f), TitleFontSize, 650f, 44f, GUIManager.Instance.ValheimOrange, TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateColumnHeaders();

            float y = 240f;
            switch (page)
            {
                case SettingsPage.BubbleVisual:
                    CreateBubbleVisualRows(ref y);
                    CreateFooterButtons(showBack: true);
                    break;
                case SettingsPage.CircleVisual:
                    CreateCircleVisualRows(ref y);
                    CreateFooterButtons(showBack: true);
                    break;
                default:
                    CreateMainRows(ref y);
                    CreateFooterButtons(showBack: false);
                    break;
            }
        }

        private static void CreateMainRows(ref float y)
        {
            AddSection("$pw_ward_settings_section_range", ref y);
            AddBool(FieldId.CustomRange, "$pw_ward_settings_custom_range", s_customRange, setWardRange, ref y);
            AddFloat(FieldId.Range, "$pw_ward_settings_range", s_range, wardRange, ref y);

            AddSection("$pw_ward_settings_section_emission", ref y);
            AddBool(FieldId.CustomColor, "$pw_ward_settings_emission_enabled", s_customColor, wardEmissionColorEnabled, ref y);
            AddEmissionColor(ref y);
            AddFloat(FieldId.EmissionColorMultiplier, "$pw_ward_settings_emission_multiplier", s_colorMultiplier, wardEmissionColorMultiplier, ref y);

            AddSection("$pw_ward_settings_section_bubble", ref y);
            AddBool(FieldId.BubbleEnabled, "$pw_ward_settings_bubble_enabled", s_bubbleEnabled, wardBubbleShow, ref y);
            AddColor(FieldId.BubbleColor, "$pw_ward_settings_bubble_color", s_bubbleColor, s_bubbleColorAlpha, wardBubbleColor.Value, ref y);
            AddNavigationRow("$pw_ward_settings_bubble_visual", "$pw_ward_settings_open", SettingsPage.BubbleVisual, ref y);

            AddSection("$pw_ward_settings_section_circle", ref y);
            AddBool(FieldId.CircleEnabled, "$pw_ward_settings_circle_enabled", s_circleEnabled, wardAreaMarkerPatch, ref y);
            AddNavigationRow("$pw_ward_settings_circle_visual", "$pw_ward_settings_open", SettingsPage.CircleVisual, ref y);
        }

        private static void CreateBubbleVisualRows(ref float y)
        {
            AddSection("$pw_ward_settings_section_bubble", ref y);
            AddFloat(FieldId.BubbleRefraction, "$pw_ward_settings_bubble_refraction", s_bubbleRefractionIntensity, wardBubbleRefractionIntensity, ref y);
            AddFloat(FieldId.BubbleWave, "$pw_ward_settings_bubble_wave", s_bubbleWaveVel, wardBubbleWaveIntensity, ref y);
            AddFloat(FieldId.BubbleGlossiness, "$pw_ward_settings_bubble_glossiness", s_bubbleGlossiness, wardBubbleGlossiness, ref y);
            AddFloat(FieldId.BubbleMetallic, "$pw_ward_settings_bubble_metallic", s_bubbleMetallic, wardBubbleMetallic, ref y);
            AddFloat(FieldId.BubbleNormalScale, "$pw_ward_settings_bubble_normal", s_bubbleNormalScale, wardBubbleNormalScale, ref y);
            AddFloat(FieldId.BubbleDepthFade, "$pw_ward_settings_bubble_depth", s_bubbleDepthFade, wardBubbleDepthFade, ref y);
        }

        private static void CreateCircleVisualRows(ref float y)
        {
            AddSection("$pw_ward_settings_section_circle", ref y);
            AddStringColor(FieldId.CircleStartColor, "$pw_ward_settings_circle_start", s_circleStartColor, wardAreaMarkerStartColor.Value, ref y);
            AddStringColor(FieldId.CircleEndColor, "$pw_ward_settings_circle_end", s_circleEndColor, wardAreaMarkerEndColor.Value, ref y);
            AddFloat(FieldId.CircleSpeed, "$pw_ward_settings_circle_speed", s_circleSpeed, wardAreaMarkerSpeed, ref y);
            AddFloat(FieldId.CircleLength, "$pw_ward_settings_circle_length", s_circleLength, wardAreaMarkerLength, ref y);
            AddFloat(FieldId.CircleWidth, "$pw_ward_settings_circle_width", s_circleWidth, wardAreaMarkerWidth, ref y);
            AddFloat(FieldId.CircleAmount, "$pw_ward_settings_circle_amount", s_circleAmount, wardAreaMarkerAmount, ref y);
        }

        private static void CreateColumnHeaders()
        {
            CreateText("$pw_ward_settings_use_default".Localize(), new Vector2(s_useDefaultHeaderX, 278f), HeaderFontSize, s_useDefaultHeaderWidth, 30f, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            CreateText("$pw_ward_settings_value".Localize(), new Vector2(s_valueHeaderX + s_valueHeaderWidth * 0.5f, 278f), HeaderFontSize, s_valueHeaderWidth, 30f, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private static void CreateFooterButtons(bool showBack)
        {
            if (showBack)
            {
                GameObject backButton = GUIManager.Instance.CreateButton(
                    text: "$pw_ward_settings_back".Localize(),
                    parent: s_panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0f, -300f),
                    width: 170f,
                    height: 50f);
                backButton.GetComponent<Button>().onClick.AddListener(() => OpenPage(SettingsPage.Main));
            }
            else
            {
                GameObject applyButton = GUIManager.Instance.CreateButton(
                    text: "$pw_ward_settings_apply".Localize(),
                    parent: s_panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(-120f, -300f),
                    width: 220f,
                    height: 50f);
                applyButton.GetComponent<Button>().onClick.AddListener(Apply);

                GameObject cancelButton = GUIManager.Instance.CreateButton(
                    text: "$pw_ward_settings_cancel".Localize(),
                    parent: s_panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(140f, -300f),
                    width: 170f,
                    height: 50f);
                cancelButton.GetComponent<Button>().onClick.AddListener(Close);
            }
        }

        private static void Apply()
        {
            if (s_zdo == null)
            {
                Close();
                return;
            }

            ZPackage package = CreateApplyPackage();

            if (ZNet.instance != null && ZNet.instance.IsServer())
                RPC_ApplyWardSettingsServer(0L, new ZPackage(package.GetArray()));
            else
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_ApplyWardSettings, package);

            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$pw_ward_settings_applied");
            Close();
        }

        private static ZPackage CreateApplyPackage()
        {
            CaptureCurrentRows();

            ZPackage package = new ZPackage();
            package.Write(s_zdo.m_uid);
            package.Write(Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L);
            package.Write(s_values.Count);

            foreach (KeyValuePair<FieldId, WardSettingValue> pair in s_values)
                WriteStoredField(package, pair.Key, pair.Value);

            return package;
        }

        private static void WriteStoredField(ZPackage package, FieldId field, WardSettingValue value)
        {
            package.Write((int)field);
            package.Write(value.UseDefault);
            if (value.UseDefault)
                return;

            switch (field)
            {
                case FieldId.BubbleEnabled:
                case FieldId.CustomRange:
                case FieldId.CustomColor:
                case FieldId.CircleEnabled:
                    package.Write(value.BoolValue);
                    break;
                case FieldId.Range:
                case FieldId.BubbleRefraction:
                case FieldId.BubbleWave:
                case FieldId.BubbleGlossiness:
                case FieldId.BubbleMetallic:
                case FieldId.BubbleNormalScale:
                case FieldId.BubbleDepthFade:
                case FieldId.EmissionColorMultiplier:
                case FieldId.CircleSpeed:
                case FieldId.CircleLength:
                case FieldId.CircleWidth:
                case FieldId.CircleAmount:
                    package.Write(value.FloatValue);
                    break;
                case FieldId.BubbleColor:
                case FieldId.EmissionColor:
                    WriteColor(package, value.ColorValue);
                    break;
                case FieldId.CircleStartColor:
                case FieldId.CircleEndColor:
                    package.Write(ColorUtility.ToHtmlStringRGBA(value.ColorValue));
                    break;
            }
        }

        private static void RPC_ApplyWardSettingsServer(long sender, ZPackage package)
        {
            ZDOID zdoID = package.ReadZDOID();
            long playerID = package.ReadLong();
            ZDO zdo = ZDOMan.instance.GetZDO(zdoID);
            if (zdo == null || !CanApplyWardSettings(zdoID, zdo, playerID))
                return;

            int count = package.ReadInt();
            for (int i = 0; i < count; i++)
                ApplyField(zdo, package);

            RefreshLoadedWard(zdoID);
            LogInfo($"Ward settings applied for {zdoID}");
        }

        private static bool CanApplyWardSettings(ZDOID zdoID, ZDO zdo, long playerID)
        {
            if (playerID == 0L)
                return false;

            PrivateArea loadedWard = FindLoadedWard(zdoID);
            if (loadedWard != null)
                return ProtectiveWards.CanApplyWardSettings(loadedWard, playerID);

            return ProtectiveWards.CanApplyWardSettings(zdo, playerID);
        }

        private static PrivateArea FindLoadedWard(ZDOID zdoID)
        {
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || area.m_nview == null || !area.m_nview.IsValid())
                    continue;

                ZDO zdo = area.m_nview.GetZDO();
                if (zdo != null && zdo.m_uid.Equals(zdoID))
                    return area;
            }

            return null;
        }

        private static void ApplyField(ZDO zdo, ZPackage package)
        {
            FieldId field = (FieldId)package.ReadInt();
            bool useDefault = package.ReadBool();

            switch (field)
            {
                case FieldId.BubbleEnabled:
                    ApplyBool(zdo, s_bubbleEnabled, useDefault, package);
                    break;
                case FieldId.BubbleColor:
                    if (useDefault)
                    {
                        RemoveZdoVec3(zdo, s_bubbleColor);
                        RemoveZdoFloat(zdo, s_bubbleColorAlpha);
                    }
                    else
                    {
                        Color color = ReadColor(package);
                        zdo.Set(s_bubbleColor, new Vector3(color.r, color.g, color.b));
                        zdo.Set(s_bubbleColorAlpha, color.a);
                    }
                    break;
                case FieldId.BubbleRefraction:
                    ApplyFloat(zdo, s_bubbleRefractionIntensity, useDefault, package);
                    break;
                case FieldId.BubbleWave:
                    ApplyFloat(zdo, s_bubbleWaveVel, useDefault, package);
                    break;
                case FieldId.BubbleGlossiness:
                    ApplyFloat(zdo, s_bubbleGlossiness, useDefault, package);
                    break;
                case FieldId.BubbleMetallic:
                    ApplyFloat(zdo, s_bubbleMetallic, useDefault, package);
                    break;
                case FieldId.BubbleNormalScale:
                    ApplyFloat(zdo, s_bubbleNormalScale, useDefault, package);
                    break;
                case FieldId.BubbleDepthFade:
                    ApplyFloat(zdo, s_bubbleDepthFade, useDefault, package);
                    break;
                case FieldId.CustomRange:
                    ApplyBool(zdo, s_customRange, useDefault, package);
                    break;
                case FieldId.Range:
                    ApplyFloat(zdo, s_range, useDefault, package);
                    break;
                case FieldId.CustomColor:
                    ApplyBool(zdo, s_customColor, useDefault, package);
                    break;
                case FieldId.EmissionColor:
                    if (useDefault)
                        RemoveZdoVec3(zdo, s_color);
                    else
                    {
                        Color color = ReadColor(package);
                        zdo.Set(s_color, new Vector3(color.r, color.g, color.b));
                    }
                    break;
                case FieldId.EmissionColorMultiplier:
                    ApplyFloat(zdo, s_colorMultiplier, useDefault, package);
                    break;
                case FieldId.CircleEnabled:
                    ApplyBool(zdo, s_circleEnabled, useDefault, package);
                    break;
                case FieldId.CircleStartColor:
                    ApplyString(zdo, s_circleStartColor, useDefault, package);
                    break;
                case FieldId.CircleEndColor:
                    ApplyString(zdo, s_circleEndColor, useDefault, package);
                    break;
                case FieldId.CircleSpeed:
                    ApplyFloat(zdo, s_circleSpeed, useDefault, package);
                    break;
                case FieldId.CircleLength:
                    ApplyFloat(zdo, s_circleLength, useDefault, package);
                    break;
                case FieldId.CircleWidth:
                    ApplyFloat(zdo, s_circleWidth, useDefault, package);
                    break;
                case FieldId.CircleAmount:
                    ApplyFloat(zdo, s_circleAmount, useDefault, package);
                    break;
            }
        }

        private static Color ReadColor(ZPackage package)
        {
            return new Color(package.ReadSingle(), package.ReadSingle(), package.ReadSingle(), package.ReadSingle());
        }

        private static void WriteColor(ZPackage package, Color color)
        {
            package.Write(color.r);
            package.Write(color.g);
            package.Write(color.b);
            package.Write(color.a);
        }

        private static void ApplyBool(ZDO zdo, int key, bool useDefault, ZPackage package)
        {
            if (useDefault)
                RemoveZdoBool(zdo, key);
            else
                zdo.Set(key, package.ReadBool());
        }

        private static void ApplyFloat(ZDO zdo, int key, bool useDefault, ZPackage package)
        {
            if (useDefault)
                RemoveZdoFloat(zdo, key);
            else
                zdo.Set(key, package.ReadSingle());
        }

        private static void ApplyString(ZDO zdo, int key, bool useDefault, ZPackage package)
        {
            if (useDefault)
            {
                RemoveZdoString(zdo, key);
            }
            else
            {
                string value = package.ReadString();
                zdo.Set(key, string.IsNullOrEmpty(value) ? "#FFFFFFFF" : value);
            }
        }

        private static void RefreshLoadedWard(ZDOID zdoID)
        {
            PrivateArea area = FindLoadedWard(zdoID);
            if (area == null)
                return;

            RefreshWardVisuals(area);
            area.m_addPermittedEffect.Create(area.transform.position, area.transform.rotation);
        }

        private static void LoadValuesFromZDO()
        {
            s_values.Clear();

            StoreBool(FieldId.CustomRange, s_customRange, setWardRange.Value);
            StoreFloat(FieldId.Range, s_range, wardRange.Value);
            StoreBool(FieldId.CustomColor, s_customColor, wardEmissionColorEnabled.Value);
            StoreEmissionColor();
            StoreFloat(FieldId.EmissionColorMultiplier, s_colorMultiplier, wardEmissionColorMultiplier.Value);

            StoreBool(FieldId.BubbleEnabled, s_bubbleEnabled, wardBubbleShow.Value);
            StoreColor(FieldId.BubbleColor, s_bubbleColor, s_bubbleColorAlpha, wardBubbleColor.Value);
            StoreFloat(FieldId.BubbleRefraction, s_bubbleRefractionIntensity, wardBubbleRefractionIntensity.Value);
            StoreFloat(FieldId.BubbleWave, s_bubbleWaveVel, wardBubbleWaveIntensity.Value);
            StoreFloat(FieldId.BubbleGlossiness, s_bubbleGlossiness, wardBubbleGlossiness.Value);
            StoreFloat(FieldId.BubbleMetallic, s_bubbleMetallic, wardBubbleMetallic.Value);
            StoreFloat(FieldId.BubbleNormalScale, s_bubbleNormalScale, wardBubbleNormalScale.Value);
            StoreFloat(FieldId.BubbleDepthFade, s_bubbleDepthFade, wardBubbleDepthFade.Value);

            StoreBool(FieldId.CircleEnabled, s_circleEnabled, wardAreaMarkerPatch.Value);
            StoreStringColor(FieldId.CircleStartColor, s_circleStartColor, wardAreaMarkerStartColor.Value);
            StoreStringColor(FieldId.CircleEndColor, s_circleEndColor, wardAreaMarkerEndColor.Value);
            StoreFloat(FieldId.CircleSpeed, s_circleSpeed, wardAreaMarkerSpeed.Value);
            StoreFloat(FieldId.CircleLength, s_circleLength, wardAreaMarkerLength.Value);
            StoreFloat(FieldId.CircleWidth, s_circleWidth, wardAreaMarkerWidth.Value);
            StoreFloat(FieldId.CircleAmount, s_circleAmount, wardAreaMarkerAmount.Value);
        }

        private static void StoreBool(FieldId field, int key, bool defaultValue)
        {
            bool hasValue = HasZdoBool(s_zdo, key);
            s_values[field] = new WardSettingValue
            {
                UseDefault = !hasValue,
                BoolValue = hasValue ? s_zdo.GetBool(key, defaultValue) : defaultValue
            };
        }

        private static void StoreFloat(FieldId field, int key, float defaultValue)
        {
            bool hasValue = HasZdoFloat(s_zdo, key);
            s_values[field] = new WardSettingValue
            {
                UseDefault = !hasValue,
                FloatValue = hasValue ? s_zdo.GetFloat(key, defaultValue) : defaultValue
            };
        }

        private static void StoreColor(FieldId field, int colorKey, int alphaKey, Color defaultValue)
        {
            bool hasValue = HasZdoVec3(s_zdo, colorKey) || HasZdoFloat(s_zdo, alphaKey);
            Vector3 vector = GetWardVec3Setting(s_zdo, colorKey, new Vector3(defaultValue.r, defaultValue.g, defaultValue.b));
            float alpha = GetWardFloatSetting(s_zdo, alphaKey, defaultValue.a);
            s_values[field] = new WardSettingValue
            {
                UseDefault = !hasValue,
                ColorValue = new Color(vector.x, vector.y, vector.z, alpha)
            };
        }

        private static void StoreEmissionColor()
        {
            bool hasValue = HasZdoVec3(s_zdo, s_color);
            Vector3 vector = GetWardVec3Setting(s_zdo, s_color, new Vector3(wardEmissionColor.Value.r, wardEmissionColor.Value.g, wardEmissionColor.Value.b));
            s_values[FieldId.EmissionColor] = new WardSettingValue
            {
                UseDefault = !hasValue,
                ColorValue = new Color(vector.x, vector.y, vector.z, 1f)
            };
        }

        private static void StoreStringColor(FieldId field, int key, Color defaultValue)
        {
            bool hasValue = HasZdoString(s_zdo, key);
            string html = hasValue ? s_zdo.GetString(key, ColorUtility.ToHtmlStringRGBA(defaultValue)) : ColorUtility.ToHtmlStringRGBA(defaultValue);
            Color color = defaultValue;
            ColorUtility.TryParseHtmlString("#" + html, out color);
            s_values[field] = new WardSettingValue
            {
                UseDefault = !hasValue,
                ColorValue = color
            };
        }

        private static WardSettingValue GetValue(FieldId field)
        {
            return s_values[field];
        }

        private static void AddBool(FieldId field, string labelToken, int key, ConfigEntry<bool> defaultEntry, ref float y)
        {
            WardSettingValue value = GetValue(field);
            BoolRow row = new BoolRow(field, labelToken, !value.UseDefault, value.BoolValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddFloat(FieldId field, string labelToken, int key, ConfigEntry<float> defaultEntry, ref float y)
        {
            WardSettingValue value = GetValue(field);
            FloatRow row = new FloatRow(field, labelToken, !value.UseDefault, value.FloatValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddColor(FieldId field, string labelToken, int colorKey, int alphaKey, Color defaultValue, ref float y)
        {
            WardSettingValue value = GetValue(field);
            ColorRow row = new ColorRow(field, labelToken, !value.UseDefault, value.ColorValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddEmissionColor(ref float y)
        {
            WardSettingValue value = GetValue(FieldId.EmissionColor);
            ColorRow row = new ColorRow(FieldId.EmissionColor, "$pw_ward_settings_emission_color", !value.UseDefault, value.ColorValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddStringColor(FieldId field, string labelToken, int key, Color defaultValue, ref float y)
        {
            WardSettingValue value = GetValue(field);
            StringColorRow row = new StringColorRow(field, labelToken, !value.UseDefault, value.ColorValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddSection(string labelToken, ref float y)
        {
            y -= 8f;
            CreateText(labelToken.Localize(), new Vector2(s_sectionX, y), HeaderFontSize, s_labelWidth, 30f, GUIManager.Instance.ValheimOrange, TextAnchor.MiddleLeft, FontStyle.Bold);
            CreateDivider(new Vector2(s_sectionDividerX, y - 1f), s_sectionDividerWidth);
            y -= 34f;
        }

        private static void AddNavigationRow(string labelToken, string buttonToken, SettingsPage targetPage, ref float y)
        {
            CreateRowText(s_panel.transform, labelToken.Localize(), new Vector2(s_labelX, y), s_labelWidth, Color.white);
            GameObject button = GUIManager.Instance.CreateButton(
                text: buttonToken.Localize(),
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(s_valueX, y),
                width: ValueWidth,
                height: 32f);
            button.GetComponent<Button>().onClick.AddListener(() => OpenPage(targetPage));
            y -= RowStep;
        }

        private static GameObject CreateText(string text, Vector2 position, int fontSize, float width, float height, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle fontStyle = FontStyle.Normal)
        {
            GameObject obj = GUIManager.Instance.CreateText(
                text: text,
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: position,
                font: GUIManager.Instance.AveriaSerif,
                fontSize: fontSize,
                color: color,
                outline: true,
                outlineColor: Color.black,
                width: width,
                height: height,
                addContentSizeFitter: false);

            ConfigureText(obj, alignment, fontStyle);
            return obj;
        }

        private static GameObject CreateRowText(Transform parent, string text, Vector2 position, float width, Color color)
        {
            GameObject obj = GUIManager.Instance.CreateText(
                text: text,
                parent: parent,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: position,
                font: GUIManager.Instance.AveriaSerif,
                fontSize: RowFontSize,
                color: color,
                outline: true,
                outlineColor: Color.black,
                width: width,
                height: 30f,
                addContentSizeFitter: false);

            ConfigureText(obj, TextAnchor.MiddleLeft, FontStyle.Normal);
            return obj;
        }

        private static void ConfigureText(GameObject obj, TextAnchor alignment, FontStyle fontStyle)
        {
            Text text = obj != null ? obj.GetComponent<Text>() : null;
            if (text == null)
                return;

            text.alignment = alignment;
            text.fontStyle = fontStyle;
        }

        private static void CreateDivider(Vector2 position, float width)
        {
            GameObject line = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(s_panel.transform, false);
            Image image = line.GetComponent<Image>();
            image.color = new Color(1f, 0.62f, 0.18f, 0.55f);
            SetRect(line, position, width, 2f);
        }

        private static void SetRect(GameObject obj, Vector2 position, float width, float height)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static string ColorToHtml(Color color, bool includeAlpha)
        {
            return includeAlpha ? "#" + ColorUtility.ToHtmlStringRGBA(color) : "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static bool TryParseColor(string value, Color fallback, out Color color)
        {
            value = value.Trim();
            if (!value.StartsWith("#"))
                value = "#" + value;

            if (ColorUtility.TryParseHtmlString(value, out color))
                return true;

            color = fallback;
            return false;
        }

        private sealed class WardSettingValue
        {
            public bool UseDefault;
            public bool BoolValue;
            public float FloatValue;
            public Color ColorValue;
        }

        private abstract class WardSettingRow
        {
            protected readonly FieldId Field;
            private readonly string m_labelToken;
            protected Toggle UseDefaultToggle;

            protected WardSettingRow(FieldId field, string labelToken, bool hasOverride)
            {
                Field = field;
                m_labelToken = labelToken;
                UseDefault = !hasOverride;
            }

            public FieldId FieldId => Field;

            protected bool UseDefault { get; private set; }

            public WardSettingValue Capture()
            {
                WardSettingValue value = new WardSettingValue { UseDefault = UseDefault };
                CaptureValue(value);
                return value;
            }

            public void Create(Transform parent, float y)
            {
                CreateRowText(parent, m_labelToken.Localize(), new Vector2(s_labelX, y), s_labelWidth, Color.white);

                GameObject useDefaultObject = GUIManager.Instance.CreateToggle(parent: parent, width: 26f, height: 26f);
                SetRect(useDefaultObject, new Vector2(s_useDefaultX, y), 26f, 26f);
                UseDefaultToggle = useDefaultObject.GetComponent<Toggle>();
                UseDefaultToggle.isOn = UseDefault;
                UseDefaultToggle.onValueChanged.AddListener(value =>
                {
                    UseDefault = value;
                    SetValueInteractable(!value);
                });

                CreateValueControl(parent, y);
                SetValueInteractable(!UseDefault);
            }

            public void Write(ZPackage package)
            {
                package.Write((int)Field);
                package.Write(UseDefault);
                if (!UseDefault)
                    WriteValue(package);
            }

            protected abstract void CreateValueControl(Transform parent, float y);
            protected abstract void SetValueInteractable(bool interactable);
            protected abstract void CaptureValue(WardSettingValue value);
            protected abstract void WriteValue(ZPackage package);
        }

        private sealed class BoolRow : WardSettingRow
        {
            private readonly bool m_initialValue;
            private Toggle m_valueToggle;

            public BoolRow(FieldId field, string labelToken, bool hasOverride, bool initialValue) : base(field, labelToken, hasOverride)
            {
                m_initialValue = initialValue;
            }

            protected override void CreateValueControl(Transform parent, float y)
            {
                GameObject obj = GUIManager.Instance.CreateToggle(parent: parent, width: 26f, height: 26f);
                SetRect(obj, new Vector2(s_valueBoolX, y), 26f, 26f);
                m_valueToggle = obj.GetComponent<Toggle>();
                m_valueToggle.isOn = m_initialValue;
            }

            protected override void SetValueInteractable(bool interactable)
            {
                if (m_valueToggle != null)
                    m_valueToggle.interactable = interactable;
            }

            protected override void CaptureValue(WardSettingValue value)
            {
                value.BoolValue = m_valueToggle != null && m_valueToggle.isOn;
            }

            protected override void WriteValue(ZPackage package)
            {
                package.Write(m_valueToggle != null && m_valueToggle.isOn);
            }
        }

        private sealed class FloatRow : WardSettingRow
        {
            private readonly float m_initialValue;
            private InputField m_input;

            public FloatRow(FieldId field, string labelToken, bool hasOverride, float initialValue) : base(field, labelToken, hasOverride)
            {
                m_initialValue = initialValue;
            }

            protected override void CreateValueControl(Transform parent, float y)
            {
                GameObject obj = GUIManager.Instance.CreateInputField(
                    parent: parent,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(s_valueX, y),
                    contentType: InputField.ContentType.DecimalNumber,
                    placeholderText: null,
                    fontSize: RowFontSize,
                    width: ValueWidth,
                    height: 30f);
                m_input = obj.GetComponent<InputField>();
                m_input.text = m_initialValue.ToString(CultureInfo.InvariantCulture);
                if (m_input.textComponent != null)
                    m_input.textComponent.alignment = TextAnchor.MiddleLeft;
            }

            protected override void SetValueInteractable(bool interactable)
            {
                if (m_input != null)
                    m_input.interactable = interactable;
            }

            protected override void CaptureValue(WardSettingValue value)
            {
                if (m_input == null || !float.TryParse(m_input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out value.FloatValue))
                    value.FloatValue = 0f;
            }

            protected override void WriteValue(ZPackage package)
            {
                if (!float.TryParse(m_input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    value = 0f;

                package.Write(value);
            }
        }

        private class ColorRow : WardSettingRow
        {
            protected readonly Color InitialValue;
            protected InputField Input;
            private Button m_pickerButton;

            public ColorRow(FieldId field, string labelToken, bool hasOverride, Color initialValue) : base(field, labelToken, hasOverride)
            {
                InitialValue = initialValue;
            }

            protected override void CreateValueControl(Transform parent, float y)
            {
                GameObject inputObject = GUIManager.Instance.CreateInputField(
                    parent: parent,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(s_colorInputX, y),
                    contentType: InputField.ContentType.Standard,
                    placeholderText: "#RRGGBBAA",
                    fontSize: RowFontSize,
                    width: ValueWidth,
                    height: 30f);
                Input = inputObject.GetComponent<InputField>();
                Input.text = ColorToHtml(InitialValue, true);
                ConfigureColorInputText();
                Input.onValueChanged.AddListener(_ => ConfigureColorInputText());

                GameObject buttonObject = GUIManager.Instance.CreateButton(
                    text: "$pw_ward_settings_set_color".Localize(),
                    parent: parent,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(s_colorButtonX, y),
                    width: ValueWidth,
                    height: 30f);
                m_pickerButton = buttonObject.GetComponent<Button>();
                m_pickerButton.onClick.AddListener(OpenColorPicker);
            }

            protected override void SetValueInteractable(bool interactable)
            {
                if (Input != null)
                    Input.interactable = interactable;

                if (m_pickerButton != null)
                    m_pickerButton.interactable = interactable;
            }

            private void OpenColorPicker()
            {
                Color current;
                if (!TryParseColor(Input != null ? Input.text : "#FFFFFFFF", InitialValue, out current))
                    current = InitialValue;

                GUIManager.Instance.CreateColorPicker(
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    current,
                    "$pw_ward_settings_set_color".Localize(),
                    color => SetColor(color),
                    color => SetColor(color),
                    true);
            }

            private void SetColor(Color color)
            {
                if (Input != null)
                    Input.text = ColorToHtml(color, true);

                ConfigureColorInputText();
            }

            private void ConfigureColorInputText()
            {
                if (Input == null || Input.textComponent == null)
                    return;

                if (!TryParseColor(Input.text, InitialValue, out Color color))
                    color = InitialValue;

                color.a = 1f;
                Input.textComponent.color = color;
                Input.textComponent.alignment = TextAnchor.MiddleLeft;
            }

            protected override void CaptureValue(WardSettingValue value)
            {
                if (!TryParseColor(Input != null ? Input.text : "#FFFFFFFF", InitialValue, out value.ColorValue))
                    value.ColorValue = InitialValue;
            }

            protected override void WriteValue(ZPackage package)
            {
                if (!TryParseColor(Input != null ? Input.text : "#FFFFFFFF", InitialValue, out Color color))
                    color = InitialValue;

                WriteColor(package, color);
            }
        }

        private sealed class StringColorRow : ColorRow
        {
            public StringColorRow(FieldId field, string labelToken, bool hasOverride, Color initialValue) : base(field, labelToken, hasOverride, initialValue)
            {
            }

            protected override void WriteValue(ZPackage package)
            {
                if (!TryParseColor(Input != null ? Input.text : "#FFFFFFFF", InitialValue, out Color color))
                    color = InitialValue;

                package.Write(ColorUtility.ToHtmlStringRGBA(color));
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static class Player_Update_CloseWardSettingsUI
        {
            private static void Postfix()
            {
                if (s_panel == null)
                    return;

                if (ZInput.GetKeyDown(KeyCode.Escape))
                    Close();
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_RegisterWardSettingsRPC
        {
            private static void Postfix()
            {
                RegisterRPCs();
            }
        }
    }
}
