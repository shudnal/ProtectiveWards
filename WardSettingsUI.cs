using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using static ProtectiveWards.ProtectiveWards;
using ProtectiveWards.Compatibility;

namespace ProtectiveWards
{
    internal static class WardSettingsUI
    {
        private const string RPC_ApplyWardSettings = "PW_ApplyWardSettings";
        private const string RPC_RefreshWardSettings = "PW_RefreshWardSettings";
        private const float MaxPanelWidth = 666f;
        private const float MinPanelWidth = 558f;
        private const float PanelHeight = 600f;
        private const int TitleFontSize = 32;
        private const int HeaderFontSize = 18;
        private const int RowFontSize = 17;
        private const float RowStep = 34f;
        private const float AccessRowStep = 30f;
        private const float NoteTopSpacing = 6f;
        private const float PanelPadding = 30.6f;
        private const float ColumnGap = 14.4f;
        private const float UseDefaultColumnWidth = 99f;
        private const float ValueWidth = 99f;
        private const float ValueGap = 7.2f;
        private const float ToggleSize = 26f;
        private const float MaxLabelWidth = 256.5f;
        private const float MinLabelWidth = 148.5f;

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
        private static readonly List<WardSettingRow> s_rows = new();
        private static readonly Dictionary<FieldId, WardSettingValue> s_values = new();
        private static bool s_rpcRegistered;
        private static bool s_inputBlocked;
        private static bool s_canEditGeneralSettings;
        private static bool s_canChangePassword;
        private static SettingsPage s_currentPage;
        private static Toggle s_passwordEnabledToggle;
        private static InputField s_passwordInput;
        private static Text s_passwordStatusText;
        private static Button s_removePasswordButton;
        private static bool s_passwordEnabled;
        private static bool s_passwordExists;
        private static string s_passwordValue = "";
        private static bool s_passwordValueChanged;
        private static Toggle s_guildAccessEnabledToggle;
        private static Text s_boundGuildText;
        private static Button s_guildBindingButton;
        private static bool s_guildAccessEnabled;
        private static bool s_guildAccessEnabledChanged;
        private static int s_boundGuildId;
        private static string s_boundGuildName = "";
        private static bool s_currentGuildAvailable;
        private static InputField s_permittedPlayerInput;
        private static string s_permittedPlayerQuery = "";

        private enum SettingsPage
        {
            Main,
            Visuals,
            BubbleVisual,
            CircleVisual,
            Access
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
            CircleAmount,
            PermitEveryone
        }

        private static readonly string[] s_sectionLayoutTokens =
        {
            "$pw_ward_settings_section_range",
            "$pw_ward_settings_section_visuals",
            "$pw_ward_settings_section_emission",
            "$pw_ward_settings_section_bubble",
            "$pw_ward_settings_section_circle",
            "$pw_ward_settings_section_access",
            "$pw_ward_guild_section",
            "$pw_ward_password_section"
        };

        private static readonly string[] s_labelLayoutTokens =
        {
            "$pw_ward_settings_custom_range",
            "$pw_ward_settings_range",
            "$pw_ward_settings_emission_enabled",
            "$pw_ward_settings_emission_color",
            "$pw_ward_settings_emission_multiplier",
            "$pw_ward_settings_visuals",
            "$pw_ward_settings_permitted_players",
            "$pw_ward_permitted_add_section",
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
            "$pw_ward_settings_circle_amount",
            "$pw_ward_settings_permit_everyone",
            "$pw_ward_guild_enabled",
            "$pw_ward_guild_bound",
            "$pw_ward_password_enabled",
            "$pw_ward_password_status",
            "$pw_ward_password_value",
            "$pw_ward_password_new"
        };

        internal static void RegisterRPCs()
        {
            if (s_rpcRegistered || ZRoutedRpc.instance == null)
                return;

            if (ZNet.instance != null && ZNet.instance.IsServer())
                ZRoutedRpc.instance.Register<ZPackage>(RPC_ApplyWardSettings, RPC_ApplyWardSettingsServer);

            ZRoutedRpc.instance.Register<ZPackage>(RPC_RefreshWardSettings, RPC_RefreshWardSettingsClient);
            s_rpcRegistered = true;
        }

        internal static void ResetRPCRegistration() => s_rpcRegistered = false;

        internal static void HandleSettingsModeChanged()
        {
            if (!ArePerWardSettingsEnabled())
            {
                Close();
                WardPermittedPlayersUI.Close();
            }
        }

        internal static void Open(PrivateArea ward)
        {
            if (!ArePerWardSettingsEnabled() || ward == null || ward.m_nview == null || !ward.m_nview.IsValid())
                return;

            WardPermittedPlayersUI.Close();
            Close();

            Player player = Player.m_localPlayer;
            s_canEditGeneralSettings = CanEditWardSettings(ward, player);
            s_canChangePassword = WardPasswordProtection.CanChangePassword(ward, player);
            if (!s_canEditGeneralSettings && !s_canChangePassword)
            {
                player?.Message(MessageHud.MessageType.Center, "$msg_privatezone");
                return;
            }

            ZDO zdo = ward.m_nview.GetZDO();
            if (zdo == null)
                return;

            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null)
            {
                player?.Message(MessageHud.MessageType.Center, "ProtectiveWards: GUIManager is not ready");
                return;
            }

            s_zdo = zdo;
            LoadValuesFromZDO();
            LoadPasswordValuesFromZDO();
            LoadGuildValuesFromZDO();
            OpenPage(s_canEditGeneralSettings ? SettingsPage.Main : SettingsPage.Access);
        }

        internal static void Close()
        {
            DestroyPanel();
            s_zdo = null;
            s_values.Clear();
            s_canEditGeneralSettings = false;
            s_canChangePassword = false;
            s_currentPage = SettingsPage.Main;
            s_passwordEnabledToggle = null;
            s_passwordInput = null;
            s_passwordStatusText = null;
            s_removePasswordButton = null;
            s_passwordEnabled = false;
            s_passwordExists = false;
            s_passwordValue = "";
            s_passwordValueChanged = false;
            s_guildAccessEnabledToggle = null;
            s_boundGuildText = null;
            s_guildBindingButton = null;
            s_guildAccessEnabled = false;
            s_guildAccessEnabledChanged = false;
            s_boundGuildId = 0;
            s_boundGuildName = "";
            s_currentGuildAvailable = false;
            s_permittedPlayerInput = null;
            s_permittedPlayerQuery = "";
            SetInputBlocked(false);
        }

        private static void DestroyPanel()
        {
            if (s_panel != null)
                UnityEngine.Object.Destroy(s_panel);

            s_panel = null;
            s_rows.Clear();
            s_passwordEnabledToggle = null;
            s_passwordInput = null;
            s_passwordStatusText = null;
            s_removePasswordButton = null;
            s_guildAccessEnabledToggle = null;
            s_boundGuildText = null;
            s_guildBindingButton = null;
            s_permittedPlayerInput = null;
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

            if (s_currentPage == SettingsPage.Access)
            {
                CaptureGuildControls();
                CapturePasswordControls();
            }
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
            s_currentPage = page;
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
            List<string> values = new(s_labelLayoutTokens.Length + s_sectionLayoutTokens.Length);
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

            string title = page == SettingsPage.Visuals
                ? "$pw_ward_settings_visuals_title"
                : page == SettingsPage.BubbleVisual
                    ? "$pw_ward_settings_bubble_visual_title"
                    : page == SettingsPage.CircleVisual
                        ? "$pw_ward_settings_circle_visual_title"
                        : page == SettingsPage.Access
                            ? "$pw_ward_settings_access_title"
                            : "$pw_ward_settings_title";

            float halfHeight = PanelHeight * 0.5f;
            CreateText(title.Localize(), new Vector2(0f, halfHeight - 35f), TitleFontSize, s_panelWidth - PanelPadding * 2f, 44f, GUIManager.Instance.ValheimOrange, TextAnchor.MiddleCenter, FontStyle.Bold);

            bool showColumnHeaders = page != SettingsPage.Access || s_canEditGeneralSettings;
            if (showColumnHeaders)
                CreateColumnHeaders(halfHeight - 72f);

            float y = halfHeight - 96f;
            switch (page)
            {
                case SettingsPage.Visuals:
                    CreateVisualRows(ref y);
                    CreateFooterButtons(showBack: true);
                    break;
                case SettingsPage.BubbleVisual:
                    CreateBubbleVisualRows(ref y);
                    CreateFooterButtons(showBack: true, backPage: SettingsPage.Visuals);
                    break;
                case SettingsPage.CircleVisual:
                    CreateCircleVisualRows(ref y);
                    CreateFooterButtons(showBack: true, backPage: SettingsPage.Visuals);
                    break;
                case SettingsPage.Access:
                    CreateAccessRows(ref y);
                    CreateFooterButtons(showBack: s_canEditGeneralSettings);
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
            AddBool(FieldId.CustomRange, "$pw_ward_settings_custom_range", ref y);
            AddFloat(FieldId.Range, "$pw_ward_settings_range", ref y);

            AddSection("$pw_ward_settings_section_visuals", ref y);
            AddNavigationRow("$pw_ward_settings_visuals", "$pw_ward_settings_open", SettingsPage.Visuals, ref y);

            AddSection("$pw_ward_settings_section_access", ref y);
            AddNavigationRow("$pw_ward_settings_access", "$pw_ward_settings_open", SettingsPage.Access, ref y);
            AddNavigationRow("$pw_ward_settings_permitted_players", "$pw_ward_settings_open", OpenPermittedPlayersPage, ref y);
            CreateAddOnlinePlayerRow(ref y);

        }

        private static void CreateVisualRows(ref float y)
        {
            AddSection("$pw_ward_settings_section_emission", ref y);
            AddBool(FieldId.CustomColor, "$pw_ward_settings_emission_enabled", ref y);
            AddEmissionColor(ref y);
            AddFloat(FieldId.EmissionColorMultiplier, "$pw_ward_settings_emission_multiplier", ref y);

            AddSection("$pw_ward_settings_section_bubble", ref y);
            AddBool(FieldId.BubbleEnabled, "$pw_ward_settings_bubble_enabled", ref y);
            AddColor(FieldId.BubbleColor, "$pw_ward_settings_bubble_color", ref y);
            AddNavigationRow("$pw_ward_settings_bubble_visual", "$pw_ward_settings_open", SettingsPage.BubbleVisual, ref y);

            AddSection("$pw_ward_settings_section_circle", ref y);
            AddBool(FieldId.CircleEnabled, "$pw_ward_settings_circle_enabled", ref y);
            AddNavigationRow("$pw_ward_settings_circle_visual", "$pw_ward_settings_open", SettingsPage.CircleVisual, ref y);
        }

        private static void CreateBubbleVisualRows(ref float y)
        {
            AddSection("$pw_ward_settings_section_bubble", ref y);
            AddFloat(FieldId.BubbleRefraction, "$pw_ward_settings_bubble_refraction", ref y);
            AddFloat(FieldId.BubbleWave, "$pw_ward_settings_bubble_wave", ref y);
            AddFloat(FieldId.BubbleGlossiness, "$pw_ward_settings_bubble_glossiness", ref y);
            AddFloat(FieldId.BubbleMetallic, "$pw_ward_settings_bubble_metallic", ref y);
            AddFloat(FieldId.BubbleNormalScale, "$pw_ward_settings_bubble_normal", ref y);
            AddFloat(FieldId.BubbleDepthFade, "$pw_ward_settings_bubble_depth", ref y);
        }

        private static void CreateCircleVisualRows(ref float y)
        {
            AddSection("$pw_ward_settings_section_circle", ref y);
            AddStringColor(FieldId.CircleStartColor, "$pw_ward_settings_circle_start", ref y);
            AddStringColor(FieldId.CircleEndColor, "$pw_ward_settings_circle_end", ref y);
            AddFloat(FieldId.CircleSpeed, "$pw_ward_settings_circle_speed", ref y);
            AddFloat(FieldId.CircleLength, "$pw_ward_settings_circle_length", ref y);
            AddFloat(FieldId.CircleWidth, "$pw_ward_settings_circle_width", ref y);
            AddFloat(FieldId.CircleAmount, "$pw_ward_settings_circle_amount", ref y);
        }

        private static void CreateAccessRows(ref float y)
        {
            if (s_canEditGeneralSettings)
            {
                AddAccessSection("$pw_ward_settings_section_access", ref y);
                AddAccessBool(FieldId.PermitEveryone, "$pw_ward_settings_permit_everyone", ref y);
                AddInfoNote("$pw_ward_settings_permit_everyone_note", ref y, 34f, new Color(0.85f, 0.85f, 0.85f), -5f);

                if (GuildsCompat.IsEnabled)
                    CreateGuildRows(ref y);
            }

            if (s_canChangePassword)
                CreatePasswordRows(ref y);
        }

        private static void CreateGuildRows(ref float y)
        {
            AddAccessSection("$pw_ward_guild_section", ref y);

            CreateRowText(s_panel.transform, "$pw_ward_guild_enabled".Localize(), new Vector2(s_labelX, y), s_labelWidth, Color.white);
            GameObject enabledObject = GUIManager.Instance.CreateToggle(parent: s_panel.transform, width: ToggleSize, height: ToggleSize);
            SetRect(enabledObject, new Vector2(s_valueBoolX, y), ToggleSize, ToggleSize);
            s_guildAccessEnabledToggle = enabledObject.GetComponent<Toggle>();
            s_guildAccessEnabledToggle.isOn = s_guildAccessEnabled;
            s_guildAccessEnabledToggle.interactable = HasBoundGuild();
            s_guildAccessEnabledToggle.onValueChanged.AddListener(value =>
            {
                s_guildAccessEnabled = value;
                s_guildAccessEnabledChanged = true;
            });
            y -= AccessRowStep;

            CreateRowText(s_panel.transform, "$pw_ward_guild_bound".Localize(), new Vector2(s_labelX, y), s_labelWidth, Color.white);
            GameObject guildTextObject = CreateRowText(
                s_panel.transform,
                GetBoundGuildDisplay(),
                new Vector2(GetPasswordInputX(150f, 170f), y),
                150f,
                HasBoundGuild() ? Color.white : new Color(1f, 0.72f, 0.42f));
            s_boundGuildText = guildTextObject.GetComponent<Text>();

            GameObject bindingObject = GUIManager.Instance.CreateButton(
                text: GetGuildBindingButtonText(),
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(GetPasswordButtonX(), y),
                width: 170f,
                height: 32f);
            s_guildBindingButton = bindingObject.GetComponent<Button>();
            s_guildBindingButton.onClick.AddListener(ToggleGuildBinding);
            y -= AccessRowStep;

            AddInfoNote("$pw_ward_guild_note", ref y, 48f, new Color(0.85f, 0.85f, 0.85f), -2f);
            UpdateGuildControls();
        }

        private static void CreatePasswordRows(ref float y)
        {
            AddAccessSection("$pw_ward_password_section", ref y);

            CreateRowText(s_panel.transform, "$pw_ward_password_enabled".Localize(), new Vector2(s_labelX, y), s_labelWidth, Color.white);
            GameObject enabledObject = GUIManager.Instance.CreateToggle(parent: s_panel.transform, width: ToggleSize, height: ToggleSize);
            SetRect(enabledObject, new Vector2(s_valueBoolX, y), ToggleSize, ToggleSize);
            s_passwordEnabledToggle = enabledObject.GetComponent<Toggle>();
            s_passwordEnabledToggle.isOn = s_passwordEnabled;
            s_passwordEnabledToggle.onValueChanged.AddListener(value => s_passwordEnabled = value);
            y -= AccessRowStep;

            if (wardPasswordFieldMode.Value == WardPasswordFieldMode.SetNewPasswordOnly)
                CreateSetNewPasswordRows(ref y);
            else
                CreateEditablePasswordRow(ref y);
        }

        private static void CreateSetNewPasswordRows(ref float y)
        {
            CreateRowText(s_panel.transform, "$pw_ward_password_status".Localize(), new Vector2(s_labelX, y), s_labelWidth, Color.white);
            GameObject statusObject = CreateRowText(
                s_panel.transform,
                GetPasswordStatusToken().Localize(),
                new Vector2(GetPasswordInputX(150f, 170f), y),
                150f,
                s_passwordExists ? Color.white : new Color(1f, 0.72f, 0.42f));
            s_passwordStatusText = statusObject.GetComponent<Text>();

            GameObject removeObject = GUIManager.Instance.CreateButton(
                text: "$pw_ward_password_remove".Localize(),
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(GetPasswordButtonX(), y),
                width: 170f,
                height: 32f);
            s_removePasswordButton = removeObject.GetComponent<Button>();
            s_removePasswordButton.interactable = s_passwordExists;
            s_removePasswordButton.onClick.AddListener(RemovePassword);
            y -= AccessRowStep;

            CreateRowText(s_panel.transform, "$pw_ward_password_new".Localize(), new Vector2(s_labelX, y), s_labelWidth, Color.white);
            CreatePasswordInput(y, showPassword: false, initialValue: "", width: 150f, x: GetPasswordInputX(150f, 170f));

            GameObject setObject = GUIManager.Instance.CreateButton(
                text: "$pw_ward_password_set_new".Localize(),
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(GetPasswordButtonX(), y),
                width: 170f,
                height: 32f);
            setObject.GetComponent<Button>().onClick.AddListener(SetNewPassword);
            y -= AccessRowStep;

            AddInfoNote("$pw_ward_password_hash_note", ref y, 44f, new Color(0.85f, 0.85f, 0.85f), -5f);
        }

        private static void CreateEditablePasswordRow(ref float y)
        {
            CreateRowText(s_panel.transform, "$pw_ward_password_value".Localize(), new Vector2(s_labelX, y), s_labelWidth, Color.white);
            CreatePasswordInput(y, showPassword: true, initialValue: s_passwordValue, width: 320f, x: GetPasswordWideInputX(320f));
            y -= AccessRowStep;

            AddInfoNote("$pw_ward_password_plaintext_note", ref y, 44f, new Color(0.85f, 0.85f, 0.85f), -5f);
        }

        private static void AddInfoNote(string token, ref float y, float height, Color color, float topSpacing = NoteTopSpacing)
        {
            y -= topSpacing;
            CreateText(
                token.Localize(),
                new Vector2(0f, y - height * 0.5f),
                15,
                s_panelWidth - PanelPadding * 2f,
                height,
                color,
                TextAnchor.UpperLeft);
            y -= height;
        }

        private static void CreatePasswordInput(float y, bool showPassword, string initialValue, float width, float x)
        {
            GameObject inputObject = GUIManager.Instance.CreateInputField(
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(x, y),
                contentType: showPassword ? InputField.ContentType.Standard : InputField.ContentType.Password,
                placeholderText: "$pw_ward_password_placeholder".Localize(),
                fontSize: RowFontSize,
                width: width,
                height: 30f);
            s_passwordInput = inputObject.GetComponent<InputField>();
            s_passwordInput.characterLimit = WardPasswordProtection.PasswordCharacterLimit;
            s_passwordInput.text = initialValue ?? "";
            if (s_passwordInput.textComponent != null)
                s_passwordInput.textComponent.alignment = TextAnchor.MiddleLeft;
            s_passwordInput.onValueChanged.AddListener(value =>
            {
                s_passwordValue = value;
                s_passwordValueChanged = true;
            });
        }

        private static float GetPasswordButtonX()
        {
            float right = s_panelWidth * 0.5f - PanelPadding;
            return right - 85f;
        }

        private static float GetPasswordInputX(float inputWidth, float buttonWidth)
        {
            float buttonLeft = GetPasswordButtonX() - buttonWidth * 0.5f;
            return buttonLeft - ValueGap - inputWidth * 0.5f;
        }

        private static float GetPasswordWideInputX(float width)
        {
            float right = s_panelWidth * 0.5f - PanelPadding;
            return right - width * 0.5f;
        }

        private static string GetPasswordStatusToken()
        {
            return s_passwordExists ? "$pw_ward_password_is_set" : "$pw_ward_password_not_set";
        }

        private static void CreateColumnHeaders(float y)
        {
            CreateText("$pw_ward_settings_use_default".Localize(), new Vector2(s_useDefaultHeaderX, y), HeaderFontSize, s_useDefaultHeaderWidth, 30f, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            CreateText("$pw_ward_settings_value".Localize(), new Vector2(s_valueHeaderX + s_valueHeaderWidth * 0.5f, y), HeaderFontSize, s_valueHeaderWidth, 30f, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private static void CreateFooterButtons(bool showBack, SettingsPage backPage = SettingsPage.Main)
        {
            float footerY = -PanelHeight * 0.5f + 45f;
            if (showBack)
            {
                GameObject backButton = GUIManager.Instance.CreateButton(
                    text: "$pw_ward_settings_back".Localize(),
                    parent: s_panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0f, footerY),
                    width: 170f,
                    height: 50f);
                backButton.GetComponent<Button>().onClick.AddListener(() => OpenPage(backPage));
            }
            else
            {
                GameObject applyButton = GUIManager.Instance.CreateButton(
                    text: "$pw_ward_settings_apply".Localize(),
                    parent: s_panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(-120f, footerY),
                    width: 220f,
                    height: 50f);
                applyButton.GetComponent<Button>().onClick.AddListener(Apply);

                GameObject cancelButton = GUIManager.Instance.CreateButton(
                    text: "$pw_ward_settings_cancel".Localize(),
                    parent: s_panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(140f, footerY),
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

            CaptureCurrentRows();

            bool replacePassword = false;
            if (s_canChangePassword && wardPasswordFieldMode.Value == WardPasswordFieldMode.EditablePassword)
                replacePassword = s_passwordValueChanged || !s_passwordExists;

            bool effectiveHasPassword = replacePassword ? !string.IsNullOrEmpty(s_passwordValue) : s_passwordExists;
            if (s_canChangePassword && s_passwordEnabled && !effectiveHasPassword)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$pw_ward_password_required");
                return;
            }

            if (s_canEditGeneralSettings)
            {
                ZPackage package = CreateApplyPackage();
                if (ZNet.instance != null && ZNet.instance.IsServer())
                    RPC_ApplyWardSettingsServer(0L, new ZPackage(package.GetArray()));
                else
                    ZRoutedRpc.instance.InvokeRoutedRPC(RPC_ApplyWardSettings, package);
            }

            if (s_canChangePassword)
                WardPasswordProtection.RequestSettingsUpdate(s_zdo.m_uid, s_passwordEnabled, replacePassword, s_passwordValue);

            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$pw_ward_settings_applied");
            Close();
        }

        private static void CapturePasswordControls()
        {
            if (s_passwordEnabledToggle != null)
                s_passwordEnabled = s_passwordEnabledToggle.isOn;

            if (wardPasswordFieldMode.Value == WardPasswordFieldMode.EditablePassword && s_passwordInput != null)
                s_passwordValue = s_passwordInput.text ?? "";
        }

        private static void CaptureGuildControls()
        {
            if (s_guildAccessEnabledToggle == null)
                return;

            bool value = s_guildAccessEnabledToggle.isOn;
            if (value != s_guildAccessEnabled)
                s_guildAccessEnabledChanged = true;

            s_guildAccessEnabled = value;
        }

        private static void LoadGuildValuesFromZDO()
        {
            s_guildAccessEnabled = GuildsCompat.IsGuildAccessEnabled(s_zdo);
            s_guildAccessEnabledChanged = false;
            s_boundGuildId = GuildsCompat.GetBoundGuildId(s_zdo);
            s_boundGuildName = GuildsCompat.GetBoundGuildName(s_zdo);
            s_currentGuildAvailable = HasCurrentPlayerGuild();
        }

        private static bool HasBoundGuild() => s_boundGuildId > 0 && !string.IsNullOrEmpty(s_boundGuildName);

        private static string GetBoundGuildDisplay()
        {
            return HasBoundGuild() ? s_boundGuildName : "$pw_ward_guild_not_bound".Localize();
        }

        private static bool HasCurrentPlayerGuild()
        {
            Player player = Player.m_localPlayer;
            return player != null && GuildsCompat.TryGetPlayerGuild(player.GetPlayerID(), out _);
        }

        private static string GetGuildBindingButtonText()
        {
            if (HasBoundGuild())
                return "$pw_ward_guild_unbind".Localize();

            return s_currentGuildAvailable
                ? "$pw_ward_guild_bind_current".Localize()
                : "$pw_ward_guild_not_found".Localize();
        }

        private static void ToggleGuildBinding()
        {
            if (s_zdo == null || !GuildsCompat.IsEnabled)
                return;

            if (HasBoundGuild())
                GuildsCompat.RequestUnbindGuild(s_zdo.m_uid);
            else if (s_currentGuildAvailable)
                GuildsCompat.RequestBindCurrentGuild(s_zdo.m_uid);
        }

        internal static void OnGuildBindingResult(ZDOID wardID, GuildsCompat.GuildBindingResult result, bool enabled, int guildId, string guildName)
        {
            bool currentWard = s_zdo != null && s_zdo.m_uid.Equals(wardID);
            if (currentWard && (result == GuildsCompat.GuildBindingResult.Success || result == GuildsCompat.GuildBindingResult.NoGuild))
            {
                if (result == GuildsCompat.GuildBindingResult.Success)
                {
                    s_guildAccessEnabled = enabled;
                    s_guildAccessEnabledChanged = false;
                    s_boundGuildId = guildId;
                    s_boundGuildName = guildName ?? "";
                }
                else
                {
                    s_currentGuildAvailable = false;
                }

                UpdateGuildControls();
            }

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            switch (result)
            {
                case GuildsCompat.GuildBindingResult.Success:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_guild_updated");
                    break;
                case GuildsCompat.GuildBindingResult.NoGuild:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_guild_no_guild");
                    break;
                case GuildsCompat.GuildBindingResult.NotAuthorized:
                    player.Message(MessageHud.MessageType.Center, "$msg_privatezone");
                    break;
                case GuildsCompat.GuildBindingResult.TooFar:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_guild_too_far");
                    break;
                default:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_guild_unavailable");
                    break;
            }
        }

        private static void UpdateGuildControls()
        {
            bool hasBoundGuild = HasBoundGuild();
            bool canBindGuild = !hasBoundGuild && s_currentGuildAvailable;

            if (s_guildAccessEnabledToggle != null)
            {
                s_guildAccessEnabledToggle.SetIsOnWithoutNotify(s_guildAccessEnabled);
                s_guildAccessEnabledToggle.interactable = hasBoundGuild;
            }

            if (s_boundGuildText != null)
            {
                s_boundGuildText.text = GetBoundGuildDisplay();
                s_boundGuildText.color = hasBoundGuild ? Color.white : new Color(1f, 0.72f, 0.42f);
            }

            if (s_guildBindingButton != null)
            {
                s_guildBindingButton.interactable = hasBoundGuild || canBindGuild;
                Text buttonText = s_guildBindingButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                    buttonText.text = GetGuildBindingButtonText();
            }
        }

        private static void SetNewPassword()
        {
            if (s_zdo == null || !s_canChangePassword)
                return;

            CapturePasswordControls();
            string password = s_passwordInput != null ? s_passwordInput.text : "";
            if (string.IsNullOrEmpty(password))
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$pw_ward_password_required");
                return;
            }

            WardPasswordProtection.RequestSettingsUpdate(s_zdo.m_uid, s_passwordEnabled, replacePassword: true, password);
        }

        private static void RemovePassword()
        {
            if (s_zdo == null || !s_canChangePassword)
                return;

            s_passwordEnabled = false;
            if (s_passwordEnabledToggle != null)
                s_passwordEnabledToggle.isOn = false;

            WardPasswordProtection.RequestSettingsUpdate(s_zdo.m_uid, enabled: false, replacePassword: true, password: "");
        }

        private static void LoadPasswordValuesFromZDO()
        {
            s_passwordEnabled = s_zdo != null && s_zdo.GetBool(WardPasswordProtection.s_passwordProtectionEnabled, false);
            s_passwordExists = WardPasswordProtection.HasPassword(s_zdo);
            s_passwordValue = WardPasswordProtection.GetEditablePassword(s_zdo);
            s_passwordValueChanged = false;
        }

        internal static void OnPasswordSettingsResult(ZDOID wardID, WardPasswordProtection.PasswordSettingsResult result, bool enabled, bool hasPassword)
        {
            bool currentWard = s_zdo != null && s_zdo.m_uid.Equals(wardID);
            if (result == WardPasswordProtection.PasswordSettingsResult.Success && currentWard)
            {
                s_passwordEnabled = enabled;
                s_passwordExists = hasPassword;
                if (!hasPassword)
                    s_passwordValue = "";

                if (s_passwordEnabledToggle != null)
                    s_passwordEnabledToggle.isOn = enabled;

                if (wardPasswordFieldMode.Value == WardPasswordFieldMode.SetNewPasswordOnly && s_passwordInput != null)
                {
                    s_passwordInput.text = "";
                    s_passwordValueChanged = false;
                }

                UpdatePasswordStatusControls();
            }

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            switch (result)
            {
                case WardPasswordProtection.PasswordSettingsResult.Success:
                    if (s_panel != null)
                        player.Message(MessageHud.MessageType.Center, "$pw_ward_password_settings_saved");
                    break;
                case WardPasswordProtection.PasswordSettingsResult.MissingPassword:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_password_required");
                    break;
                case WardPasswordProtection.PasswordSettingsResult.NotAuthorized:
                    player.Message(MessageHud.MessageType.Center, "$msg_privatezone");
                    break;
                case WardPasswordProtection.PasswordSettingsResult.TooFar:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_password_too_far");
                    break;
                case WardPasswordProtection.PasswordSettingsResult.PasswordTooLong:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_password_too_long");
                    break;
                default:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_password_unavailable");
                    break;
            }
        }

        private static void UpdatePasswordStatusControls()
        {
            if (s_passwordStatusText != null)
            {
                s_passwordStatusText.text = GetPasswordStatusToken().Localize();
                s_passwordStatusText.color = s_passwordExists ? Color.white : new Color(1f, 0.72f, 0.42f);
            }

            if (s_removePasswordButton != null)
                s_removePasswordButton.interactable = s_passwordExists;
        }

        private static ZPackage CreateApplyPackage()
        {
            ZPackage package = new();
            package.Write(s_zdo.m_uid);
            package.Write(Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L);
            package.Write(s_values.Count);

            foreach (KeyValuePair<FieldId, WardSettingValue> pair in s_values)
                WriteStoredField(package, pair.Key, pair.Value);

            bool includeGuildAccessEnabled = GuildsCompat.IsEnabled && s_guildAccessEnabledChanged;
            package.Write(includeGuildAccessEnabled);
            if (includeGuildAccessEnabled)
                package.Write(s_guildAccessEnabled);

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
                case FieldId.PermitEveryone:
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

            if (!TryGetRoutedPlayer(sender, playerID, out RoutedPlayerContext requester))
                return;

            ZDO zdo = ZDOMan.instance.GetZDO(zdoID);
            if (zdo == null || !CanApplyWardSettings(zdoID, zdo, requester.PlayerID))
                return;

            int count = package.ReadInt();
            ZPackage refreshPackage = new();
            refreshPackage.Write(zdoID);
            refreshPackage.Write(count);

            for (int i = 0; i < count; i++)
                ApplyField(zdo, package, refreshPackage);

            bool includeGuildAccessEnabled = package.ReadBool();
            refreshPackage.Write(includeGuildAccessEnabled);
            if (includeGuildAccessEnabled)
            {
                bool guildAccessEnabled = package.ReadBool();
                GuildsCompat.SetGuildAccessEnabled(zdo, guildAccessEnabled);
                refreshPackage.Write(GuildsCompat.IsGuildAccessEnabled(zdo));
            }

            BroadcastWardSettingsRefresh(refreshPackage);
            LogInfo($"Ward settings applied for {zdoID}");
        }

        private static void BroadcastWardSettingsRefresh(ZPackage package)
        {
            if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_RefreshWardSettings, package);
            else
                RPC_RefreshWardSettingsClient(0L, new ZPackage(package.GetArray()));
        }

        private static void RPC_RefreshWardSettingsClient(long _, ZPackage package)
        {
            ZDOID zdoID = package.ReadZDOID();
            ZDO zdo = ZDOMan.instance?.GetZDO(zdoID);
            if (zdo == null)
                return;

            int count = package.ReadInt();
            for (int i = 0; i < count; i++)
                ApplyField(zdo, package);

            bool includeGuildAccessEnabled = package.ReadBool();
            if (includeGuildAccessEnabled)
                GuildsCompat.SetGuildAccessEnabled(zdo, package.ReadBool());

            RefreshLoadedWard(zdoID);
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

        private static void ApplyField(ZDO zdo, ZPackage package, ZPackage mirror = null)
        {
            FieldId field = (FieldId)package.ReadInt();
            bool useDefault = package.ReadBool();

            mirror?.Write((int)field);
            mirror?.Write(useDefault);

            switch (field)
            {
                case FieldId.BubbleEnabled:
                    ApplyBool(zdo, s_bubbleEnabled, useDefault, package, mirror);
                    break;
                case FieldId.BubbleColor:
                    ApplyColor(zdo, s_bubbleColor, s_bubbleColorAlpha, useDefault, package, mirror);
                    break;
                case FieldId.BubbleRefraction:
                    ApplyFloat(zdo, s_bubbleRefractionIntensity, useDefault, package, mirror);
                    break;
                case FieldId.BubbleWave:
                    ApplyFloat(zdo, s_bubbleWaveVel, useDefault, package, mirror);
                    break;
                case FieldId.BubbleGlossiness:
                    ApplyFloat(zdo, s_bubbleGlossiness, useDefault, package, mirror);
                    break;
                case FieldId.BubbleMetallic:
                    ApplyFloat(zdo, s_bubbleMetallic, useDefault, package, mirror);
                    break;
                case FieldId.BubbleNormalScale:
                    ApplyFloat(zdo, s_bubbleNormalScale, useDefault, package, mirror);
                    break;
                case FieldId.BubbleDepthFade:
                    ApplyFloat(zdo, s_bubbleDepthFade, useDefault, package, mirror);
                    break;
                case FieldId.CustomRange:
                    ApplyBool(zdo, s_customRange, useDefault, package, mirror);
                    break;
                case FieldId.Range:
                    ApplyFloat(zdo, s_range, useDefault, package, mirror);
                    break;
                case FieldId.CustomColor:
                    ApplyBool(zdo, s_customColor, useDefault, package, mirror);
                    break;
                case FieldId.EmissionColor:
                    ApplyColor(zdo, s_color, 0, useDefault, package, mirror, writeAlpha: false);
                    break;
                case FieldId.EmissionColorMultiplier:
                    ApplyFloat(zdo, s_colorMultiplier, useDefault, package, mirror);
                    break;
                case FieldId.CircleEnabled:
                    ApplyBool(zdo, s_circleEnabled, useDefault, package, mirror);
                    break;
                case FieldId.CircleStartColor:
                    ApplyString(zdo, s_circleStartColor, useDefault, package, mirror);
                    break;
                case FieldId.CircleEndColor:
                    ApplyString(zdo, s_circleEndColor, useDefault, package, mirror);
                    break;
                case FieldId.CircleSpeed:
                    ApplyFloat(zdo, s_circleSpeed, useDefault, package, mirror);
                    break;
                case FieldId.CircleLength:
                    ApplyFloat(zdo, s_circleLength, useDefault, package, mirror);
                    break;
                case FieldId.CircleWidth:
                    ApplyFloat(zdo, s_circleWidth, useDefault, package, mirror);
                    break;
                case FieldId.CircleAmount:
                    ApplyFloat(zdo, s_circleAmount, useDefault, package, mirror);
                    break;
                case FieldId.PermitEveryone:
                    ApplyBool(zdo, s_permitEveryone, useDefault, package, mirror);
                    break;
            }
        }

        private static Color ReadColor(ZPackage package)
        {
            return new Color(package.ReadSingle(), package.ReadSingle(), package.ReadSingle(), package.ReadSingle());
        }

        private static void WriteColor(ZPackage package, Color color)
        {
            if (package == null)
                return;

            package.Write(color.r);
            package.Write(color.g);
            package.Write(color.b);
            package.Write(color.a);
        }

        private static void ApplyBool(ZDO zdo, int key, bool useDefault, ZPackage package, ZPackage mirror = null)
        {
            if (useDefault)
            {
                RemoveZdoBool(zdo, key);
                return;
            }

            bool value = package.ReadBool();
            mirror?.Write(value);
            zdo.Set(key, value);
        }

        private static void ApplyFloat(ZDO zdo, int key, bool useDefault, ZPackage package, ZPackage mirror = null)
        {
            if (useDefault)
            {
                RemoveZdoFloat(zdo, key);
                return;
            }

            float value = package.ReadSingle();
            mirror?.Write(value);
            zdo.Set(key, value);
        }

        private static void ApplyColor(ZDO zdo, int colorKey, int alphaKey, bool useDefault, ZPackage package, ZPackage mirror = null, bool writeAlpha = true)
        {
            if (useDefault)
            {
                RemoveZdoVec3(zdo, colorKey);
                if (writeAlpha)
                    RemoveZdoFloat(zdo, alphaKey);
                return;
            }

            Color color = ReadColor(package);
            WriteColor(mirror, color);
            zdo.Set(colorKey, new Vector3(color.r, color.g, color.b));
            if (writeAlpha)
                zdo.Set(alphaKey, color.a);
        }

        private static void ApplyString(ZDO zdo, int key, bool useDefault, ZPackage package, ZPackage mirror = null)
        {
            if (useDefault)
            {
                RemoveZdoString(zdo, key);
                return;
            }

            string value = package.ReadString();
            mirror?.Write(value);
            zdo.Set(key, string.IsNullOrEmpty(value) ? "#FFFFFFFF" : value);
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

            StoreBool(FieldId.PermitEveryone, s_permitEveryone, permitEveryone.Value);
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
            if (!ColorUtility.TryParseHtmlString("#" + html, out Color color))
                color = defaultValue;
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

        private static void AddBool(FieldId field, string labelToken, ref float y)
        {
            WardSettingValue value = GetValue(field);
            BoolRow row = new(field, labelToken, !value.UseDefault, value.BoolValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddFloat(FieldId field, string labelToken, ref float y)
        {
            WardSettingValue value = GetValue(field);
            FloatRow row = new(field, labelToken, !value.UseDefault, value.FloatValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddColor(FieldId field, string labelToken, ref float y)
        {
            WardSettingValue value = GetValue(field);
            ColorRow row = new(field, labelToken, !value.UseDefault, value.ColorValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddEmissionColor(ref float y)
        {
            WardSettingValue value = GetValue(FieldId.EmissionColor);
            ColorRow row = new(FieldId.EmissionColor, "$pw_ward_settings_emission_color", !value.UseDefault, value.ColorValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddStringColor(FieldId field, string labelToken, ref float y)
        {
            WardSettingValue value = GetValue(field);
            StringColorRow row = new(field, labelToken, !value.UseDefault, value.ColorValue);
            row.Create(s_panel.transform, y);
            s_rows.Add(row);
            y -= RowStep;
        }

        private static void AddAccessBool(FieldId field, string labelToken, ref float y)
        {
            AddBool(field, labelToken, ref y);
            y += RowStep - AccessRowStep;
        }

        private static void AddAccessSection(string labelToken, ref float y)
        {
            y -= 4f;
            CreateText(labelToken.Localize(), new Vector2(s_sectionX, y), HeaderFontSize, s_labelWidth, 30f, GUIManager.Instance.ValheimOrange, TextAnchor.MiddleLeft, FontStyle.Bold);
            CreateDivider(new Vector2(s_sectionDividerX, y - 1f), s_sectionDividerWidth);
            y -= 30f;
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
            AddNavigationRow(labelToken, buttonToken, () => OpenPage(targetPage), ref y);
        }

        private static void AddNavigationRow(string labelToken, string buttonToken, Action action, ref float y)
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
            button.GetComponent<Button>().onClick.AddListener(() => action?.Invoke());
            y -= RowStep;
        }

        private static void CreateAddOnlinePlayerRow(ref float y)
        {
            const float inputWidth = 190f;
            const float buttonWidth = 110f;
            float right = s_panelWidth * 0.5f - PanelPadding;
            float buttonX = right - buttonWidth * 0.5f;
            float inputRight = buttonX - buttonWidth * 0.5f - ValueGap;
            float inputX = inputRight - inputWidth * 0.5f;

            CreateRowText(s_panel.transform, "$pw_ward_permitted_add_section".Localize(), new Vector2(s_labelX, y), s_labelWidth, Color.white);

            GameObject inputObject = GUIManager.Instance.CreateInputField(
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(inputX, y),
                contentType: InputField.ContentType.Standard,
                placeholderText: "$pw_ward_permitted_placeholder".Localize(),
                fontSize: RowFontSize,
                width: inputWidth,
                height: 30f);
            s_permittedPlayerInput = inputObject.GetComponent<InputField>();
            s_permittedPlayerInput.characterLimit = 64;
            s_permittedPlayerInput.text = s_permittedPlayerQuery;
            s_permittedPlayerInput.onValueChanged.AddListener(value => s_permittedPlayerQuery = value ?? "");
            if (s_permittedPlayerInput.textComponent != null)
                s_permittedPlayerInput.textComponent.alignment = TextAnchor.MiddleLeft;

            GameObject addButton = GUIManager.Instance.CreateButton(
                text: "$pw_ward_permitted_add".Localize(),
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(buttonX, y),
                width: buttonWidth,
                height: 32f);
            addButton.GetComponent<Button>().onClick.AddListener(RequestAddOnlinePlayer);
            y -= RowStep;

            AddInfoNote("$pw_ward_permitted_add_note", ref y, 34f, new Color(0.85f, 0.85f, 0.85f), -5f);
        }

        private static void RequestAddOnlinePlayer()
        {
            if (s_zdo == null)
                return;

            string query = s_permittedPlayerInput != null
                ? s_permittedPlayerInput.text.Trim()
                : s_permittedPlayerQuery.Trim();
            s_permittedPlayerQuery = query;
            WardPermittedPlayersUI.RequestAddPlayer(s_zdo.m_uid, query);
        }

        internal static void HandlePermittedPlayerAdded(ZDOID wardID)
        {
            if (s_zdo == null || !s_zdo.m_uid.Equals(wardID))
                return;

            s_permittedPlayerQuery = "";
            if (s_permittedPlayerInput != null)
                s_permittedPlayerInput.text = "";
        }

        private static void OpenPermittedPlayersPage()
        {
            if (s_zdo == null)
                return;

            PrivateArea ward = WardZdoUtils.FindLoadedWard(s_zdo.m_uid);
            if (ward != null)
                WardPermittedPlayersUI.Open(ward);
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
            Text text = obj?.GetComponent<Text>();
            if (text == null)
                return;

            text.alignment = alignment;
            text.fontStyle = fontStyle;
        }

        private static void CreateDivider(Vector2 position, float width)
        {
            GameObject line = new("Divider", typeof(RectTransform), typeof(Image));
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
                WardSettingValue value = new() { UseDefault = UseDefault };
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
                if (!TryParseColor(Input != null ? Input.text : "#FFFFFFFF", InitialValue, out Color current))
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
                {
                    Close();
                    return;
                }

                if (s_permittedPlayerInput != null
                    && s_permittedPlayerInput.isFocused
                    && (ZInput.GetKeyDown(KeyCode.Return) || ZInput.GetKeyDown(KeyCode.KeypadEnter)))
                    RequestAddOnlinePlayer();
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

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ResetWardSettingsRPC
        {
            private static void Postfix() => ResetRPCRegistration();
        }
    }
}
