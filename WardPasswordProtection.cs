using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardPasswordProtection
    {
        private const string RPC_SubmitWardPassword = "PW_SubmitWardPassword";
        private const string RPC_SubmitWardPasswordResult = "PW_SubmitWardPasswordResult";
        private const string RPC_UpdateWardPassword = "PW_UpdateWardPassword";
        private const string RPC_UpdateWardPasswordResult = "PW_UpdateWardPasswordResult";
        internal const int PasswordCharacterLimit = 64;
        private const int PasswordHashIterations = 10000;

        internal static readonly int s_passwordProtectionEnabled = "pw_password_enabled".GetStableHashCode();
        internal static readonly int s_passwordHash = "pw_password_hash".GetStableHashCode();
        internal static readonly int s_passwordSalt = "pw_password_salt".GetStableHashCode();
        internal static readonly int s_passwordPlaintext = "pw_password_plaintext".GetStableHashCode();

        private static bool s_rpcRegistered;
        private static GameObject s_panel;
        private static InputField s_passwordInput;
        private static Button s_submitButton;
        private static ZDOID s_promptWardID = ZDOID.None;
        private static bool s_inputBlocked;

        private enum PasswordEntryResult
        {
            Success,
            IncorrectPassword,
            Unavailable,
            AlreadyPermitted
        }

        internal enum PasswordSettingsResult
        {
            Success,
            MissingPassword,
            NotAuthorized,
            Unavailable,
            PasswordTooLong
        }

        internal static void RegisterRPCs()
        {
            if (s_rpcRegistered || ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.Register<ZPackage>(RPC_SubmitWardPasswordResult, RPC_SubmitWardPasswordResultClient);
            ZRoutedRpc.instance.Register<ZPackage>(RPC_UpdateWardPasswordResult, RPC_UpdateWardPasswordResultClient);

            if (ZNet.instance?.IsServer() == true)
            {
                ZRoutedRpc.instance.Register<ZPackage>(RPC_SubmitWardPassword, RPC_SubmitWardPasswordServer);
                ZRoutedRpc.instance.Register<ZPackage>(RPC_UpdateWardPassword, RPC_UpdateWardPasswordServer);
                HandlePasswordFieldModeChanged();
            }

            s_rpcRegistered = true;
        }

        internal static void ResetRPCRegistration()
        {
            s_rpcRegistered = false;
            ClosePrompt();
        }

        internal static bool IsPasswordProtectionActive(PrivateArea ward)
        {
            if (ward?.m_nview?.IsValid() != true)
                return false;

            return IsPasswordProtectionActive(ward.m_nview.GetZDO());
        }

        internal static bool IsPasswordProtectionActive(ZDO zdo)
        {
            return ArePerWardSettingsEnabled()
                   && zdo != null
                   && zdo.GetBool(s_passwordProtectionEnabled, false)
                   && HasPassword(zdo);
        }

        internal static bool HasPassword(ZDO zdo)
        {
            if (zdo == null)
                return false;

            string hash = zdo.GetString(s_passwordHash, "");
            string salt = zdo.GetString(s_passwordSalt, "");
            if (!string.IsNullOrEmpty(hash) && !string.IsNullOrEmpty(salt))
                return true;

            return !string.IsNullOrEmpty(zdo.GetString(s_passwordPlaintext, ""));
        }

        internal static string GetEditablePassword(ZDO zdo)
        {
            if (zdo == null || wardPasswordFieldMode.Value != WardPasswordFieldMode.EditablePassword)
                return "";

            return zdo.GetString(s_passwordPlaintext, "");
        }

        internal static bool CanChangePassword(PrivateArea ward, Player player)
        {
            return player != null && CanChangePassword(ward, player.GetPlayerID());
        }

        internal static bool CanChangePassword(PrivateArea ward, long playerID)
        {
            if (!ArePerWardSettingsEnabled() || ward == null || playerID == 0L)
                return false;

            if (HasWardManagementAccess(ward, playerID))
                return true;

            if (ShouldBlockInactiveWardAccess(ward, playerID) || ward.m_piece == null)
                return false;

            if (ward.m_piece.GetCreator() == playerID)
                return true;

            if (wardPasswordChangeAccess.Value != WardPasswordChangeAccess.CreatorAndPermitted)
                return false;

            return HasDirectAccessToWard(ward, playerID);
        }

        internal static bool CanChangePassword(ZDO zdo, long playerID)
        {
            if (!ArePerWardSettingsEnabled() || !WardZdoUtils.IsWard(zdo) || playerID == 0L)
                return false;

            if (HasWardManagementAccess(zdo, playerID))
                return true;

            if (ShouldBlockInactiveWardAccess(zdo, playerID))
                return false;

            if (zdo.IsCreator(playerID))
                return true;

            if (wardPasswordChangeAccess.Value != WardPasswordChangeAccess.CreatorAndPermitted)
                return false;

            return WardZdoUtils.HasDirectAccessToWardZdo(zdo, playerID);
        }

        internal static bool AppendPasswordHoverAction(PrivateArea ward, StringBuilder text)
        {
            if (text == null || !ShouldOfferPasswordEntry(ward, Player.m_localPlayer))
                return false;

            const string vanillaAddToken = "$piece_guardstone_add";
            int vanillaAddIndex = text.ToString().LastIndexOf(vanillaAddToken, StringComparison.Ordinal);
            if (vanillaAddIndex >= 0)
            {
                text.Remove(vanillaAddIndex, vanillaAddToken.Length);
                text.Insert(vanillaAddIndex, "$pw_ward_password_enter");
            }
            else
            {
                text.Append("\n[<color=yellow><b>$KEY_Use</b></color>] $pw_ward_password_enter");
            }

            return true;
        }

        internal static bool TryHandleInteraction(PrivateArea ward, Player player)
        {
            if (!ShouldOfferPasswordEntry(ward, player))
                return false;

            OpenPrompt(ward);
            return true;
        }

        private static bool ShouldOfferPasswordEntry(PrivateArea ward, Player player)
        {
            if (ward == null || player == null || ward.m_ownerFaction != Character.Faction.Players)
                return false;

            if (!IsPasswordProtectionActive(ward))
                return false;

            long playerID = player.GetPlayerID();
            if (playerID == 0L)
                return false;

            return !HasDirectAccessToWard(ward, playerID);
        }

        private static void OpenPrompt(PrivateArea ward)
        {
            if (ward?.m_nview?.IsValid() != true)
                return;

            ClosePrompt();
            WardSettingsUI.Close();

            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$pw_ward_password_unavailable");
                return;
            }

            s_promptWardID = ward.m_nview.GetZDO().m_uid;
            s_panel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                width: 520f,
                height: 270f,
                draggable: true);
            s_panel.SetActive(true);

            CreatePromptText("$pw_ward_password_prompt_title".Localize(), new Vector2(0f, 92f), 28, 450f, 44f, GUIManager.Instance.ValheimOrange, FontStyle.Bold);

            GameObject inputObject = GUIManager.Instance.CreateInputField(
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 20f),
                contentType: InputField.ContentType.Password,
                placeholderText: "$pw_ward_password_placeholder".Localize(),
                fontSize: 18,
                width: 390f,
                height: 38f);
            s_passwordInput = inputObject.GetComponent<InputField>();
            s_passwordInput.characterLimit = PasswordCharacterLimit;
            if (s_passwordInput.textComponent != null)
                s_passwordInput.textComponent.alignment = TextAnchor.MiddleLeft;

            GameObject submitObject = GUIManager.Instance.CreateButton(
                text: "$pw_ward_password_submit".Localize(),
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(-105f, -72f),
                width: 180f,
                height: 48f);
            s_submitButton = submitObject.GetComponent<Button>();
            s_submitButton.onClick.AddListener(SubmitPassword);

            GameObject cancelObject = GUIManager.Instance.CreateButton(
                text: "$pw_ward_settings_cancel".Localize(),
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(115f, -72f),
                width: 180f,
                height: 48f);
            cancelObject.GetComponent<Button>().onClick.AddListener(ClosePrompt);

            SetPromptInputBlocked(true);
            s_passwordInput.ActivateInputField();
        }

        private static void CreatePromptText(string value, Vector2 position, int fontSize, float width, float height, Color color, FontStyle style)
        {
            GameObject obj = GUIManager.Instance.CreateText(
                text: value,
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

            Text text = obj.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleCenter;
                text.fontStyle = style;
            }
        }

        private static void SubmitPassword()
        {
            if (s_submitButton != null && !s_submitButton.interactable)
                return;

            Player player = Player.m_localPlayer;
            if (player == null || s_promptWardID.IsNone())
            {
                ClosePrompt();
                return;
            }

            string password = s_passwordInput != null ? s_passwordInput.text : "";
            if (string.IsNullOrEmpty(password))
            {
                player.Message(MessageHud.MessageType.Center, "$pw_ward_password_required");
                return;
            }

            if (password.Length > PasswordCharacterLimit)
            {
                player.Message(MessageHud.MessageType.Center, "$pw_ward_password_too_long");
                return;
            }

            if (s_submitButton != null)
                s_submitButton.interactable = false;

            ZPackage package = new();
            package.Write(s_promptWardID);
            package.Write(player.GetPlayerID());
            package.Write(password);

            if (ZNet.instance?.IsServer() == true)
                RPC_SubmitWardPasswordServer(0L, new ZPackage(package.GetArray()));
            else if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_SubmitWardPassword, package);
            else
                HandlePasswordEntryResult(s_promptWardID, PasswordEntryResult.Unavailable);
        }

        private static void RPC_SubmitWardPasswordServer(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            long claimedPlayerID = package.ReadLong();
            string password = package.ReadString() ?? "";

            if (!TryGetRoutedPlayer(sender, claimedPlayerID, out RoutedPlayerContext requester))
                return;

            if (!WardZdoUtils.TryGetWard(wardID, out ZDO zdo))
            {
                SendPasswordEntryResult(sender, wardID, PasswordEntryResult.Unavailable);
                return;
            }

            if (WardZdoUtils.HasDirectAccessToWardZdo(zdo, requester.PlayerID))
            {
                SendPasswordEntryResult(sender, wardID, PasswordEntryResult.AlreadyPermitted);
                return;
            }

            if (!IsPasswordProtectionActive(zdo))
            {
                SendPasswordEntryResult(sender, wardID, PasswordEntryResult.Unavailable);
                return;
            }

            if (!VerifyPassword(zdo, password))
            {
                SendPasswordEntryResult(sender, wardID, PasswordEntryResult.IncorrectPassword);
                return;
            }

            WardZdoUtils.AddPermitted(zdo, requester.PlayerID, requester.PlayerName);
            LogInfo($"Added {requester.PlayerName} to ward {wardID} permitted list by password");
            SendPasswordEntryResult(sender, wardID, PasswordEntryResult.Success);
        }

        private static void SendPasswordEntryResult(long peerID, ZDOID wardID, PasswordEntryResult result)
        {
            ZPackage response = new();
            response.Write(wardID);
            response.Write((int)result);

            if (ZNet.instance?.IsServer() == true && ZRoutedRpc.instance != null && peerID != 0L)
                ZRoutedRpc.instance.InvokeRoutedRPC(peerID, RPC_SubmitWardPasswordResult, response);
            else
                RPC_SubmitWardPasswordResultClient(0L, new ZPackage(response.GetArray()));
        }

        private static void RPC_SubmitWardPasswordResultClient(long _, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            PasswordEntryResult result = (PasswordEntryResult)package.ReadInt();
            HandlePasswordEntryResult(wardID, result);
        }

        private static void HandlePasswordEntryResult(ZDOID wardID, PasswordEntryResult result)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            switch (result)
            {
                case PasswordEntryResult.Success:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_password_accepted");
                    ClosePrompt();
                    break;
                case PasswordEntryResult.IncorrectPassword:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_password_incorrect");
                    if (s_promptWardID.Equals(wardID) && s_passwordInput != null)
                    {
                        s_passwordInput.text = "";
                        s_passwordInput.ActivateInputField();
                    }
                    if (s_submitButton != null)
                        s_submitButton.interactable = true;
                    break;
                case PasswordEntryResult.AlreadyPermitted:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_password_already_permitted");
                    ClosePrompt();
                    break;
                default:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_password_unavailable");
                    ClosePrompt();
                    break;
            }
        }

        internal static void RequestSettingsUpdate(ZDOID wardID, bool enabled, bool replacePassword, string password)
        {
            Player player = Player.m_localPlayer;
            if (player == null || wardID.IsNone())
                return;

            password = replacePassword ? password ?? "" : "";
            ZPackage package = new();
            package.Write(wardID);
            package.Write(player.GetPlayerID());
            package.Write(enabled);
            package.Write(replacePassword);
            package.Write(password);

            if (ZNet.instance?.IsServer() == true)
                RPC_UpdateWardPasswordServer(0L, new ZPackage(package.GetArray()));
            else if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_UpdateWardPassword, package);
            else
                WardSettingsUI.OnPasswordSettingsResult(wardID, PasswordSettingsResult.Unavailable, enabled: false, hasPassword: false);
        }

        private static void RPC_UpdateWardPasswordServer(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            long claimedPlayerID = package.ReadLong();
            bool enabled = package.ReadBool();
            bool replacePassword = package.ReadBool();
            string password = package.ReadString() ?? "";

            if (!TryGetRoutedPlayer(sender, claimedPlayerID, out RoutedPlayerContext requester))
                return;

            if (!WardZdoUtils.TryGetWard(wardID, out ZDO zdo))
            {
                SendPasswordSettingsResult(sender, wardID, PasswordSettingsResult.Unavailable, false, false);
                return;
            }

            if (!CanChangePassword(zdo, requester.PlayerID))
            {
                SendPasswordSettingsResult(sender, wardID, PasswordSettingsResult.NotAuthorized, false, false);
                return;
            }

            if (replacePassword && password.Length > PasswordCharacterLimit)
            {
                SendPasswordSettingsResult(sender, wardID, PasswordSettingsResult.PasswordTooLong, false, false);
                return;
            }

            if (replacePassword)
            {
                StorePassword(zdo, password);
                if (string.IsNullOrEmpty(password))
                    enabled = false;
            }

            bool hasPassword = HasPassword(zdo);
            if (enabled && !hasPassword)
            {
                SendPasswordSettingsResult(sender, wardID, PasswordSettingsResult.MissingPassword, false, false);
                return;
            }

            zdo.Set(s_passwordProtectionEnabled, enabled);
            LogInfo($"Ward password protection {(enabled ? "enabled" : "disabled")} for {wardID}");
            SendPasswordSettingsResult(sender, wardID, PasswordSettingsResult.Success, enabled, hasPassword);
        }

        private static void SendPasswordSettingsResult(long peerID, ZDOID wardID, PasswordSettingsResult result, bool enabled, bool hasPassword)
        {
            ZPackage response = new();
            response.Write(wardID);
            response.Write((int)result);
            response.Write(enabled);
            response.Write(hasPassword);

            if (ZNet.instance?.IsServer() == true && ZRoutedRpc.instance != null && peerID != 0L)
                ZRoutedRpc.instance.InvokeRoutedRPC(peerID, RPC_UpdateWardPasswordResult, response);
            else
                RPC_UpdateWardPasswordResultClient(0L, new ZPackage(response.GetArray()));
        }

        private static void RPC_UpdateWardPasswordResultClient(long _, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            PasswordSettingsResult result = (PasswordSettingsResult)package.ReadInt();
            bool enabled = package.ReadBool();
            bool hasPassword = package.ReadBool();
            WardSettingsUI.OnPasswordSettingsResult(wardID, result, enabled, hasPassword);
        }

        private static void StorePassword(ZDO zdo, string password)
        {
            if (zdo == null)
                return;

            if (string.IsNullOrEmpty(password))
            {
                RemoveZdoString(zdo, s_passwordHash);
                RemoveZdoString(zdo, s_passwordSalt);
                RemoveZdoString(zdo, s_passwordPlaintext);
                return;
            }

            byte[] salt = new byte[16];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(salt);

            byte[] hash;
            using (Rfc2898DeriveBytes derive = new(password, salt, PasswordHashIterations))
                hash = derive.GetBytes(32);

            zdo.Set(s_passwordSalt, Convert.ToBase64String(salt));
            zdo.Set(s_passwordHash, Convert.ToBase64String(hash));

            if (wardPasswordFieldMode.Value == WardPasswordFieldMode.EditablePassword)
                zdo.Set(s_passwordPlaintext, password);
            else
                RemoveZdoString(zdo, s_passwordPlaintext);
        }

        private static bool VerifyPassword(ZDO zdo, string password)
        {
            if (zdo == null || password == null)
                return false;

            string encodedSalt = zdo.GetString(s_passwordSalt, "");
            string encodedHash = zdo.GetString(s_passwordHash, "");
            if (!string.IsNullOrEmpty(encodedSalt) && !string.IsNullOrEmpty(encodedHash))
            {
                try
                {
                    byte[] salt = Convert.FromBase64String(encodedSalt);
                    byte[] expected = Convert.FromBase64String(encodedHash);
                    byte[] actual;
                    using (Rfc2898DeriveBytes derive = new(password, salt, PasswordHashIterations))
                        actual = derive.GetBytes(expected.Length);

                    return FixedTimeEquals(expected, actual);
                }
                catch (FormatException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (CryptographicException)
                {
                }
            }

            return string.Equals(zdo.GetString(s_passwordPlaintext, ""), password, StringComparison.Ordinal);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];

            return difference == 0;
        }

        internal static void HandleSettingsModeChanged()
        {
            if (!ArePerWardSettingsEnabled())
                ClosePrompt();
        }

        internal static void HandlePasswordFieldModeChanged()
        {
            if (wardPasswordFieldMode.Value != WardPasswordFieldMode.SetNewPasswordOnly || ZNet.instance?.IsServer() != true)
                return;

            foreach (ZDO ward in WardZdoUtils.GetAllWards())
                RemoveZdoString(ward, s_passwordPlaintext);
        }

        private static void ClosePrompt()
        {
            if (s_panel != null)
                UnityEngine.Object.Destroy(s_panel);

            s_panel = null;
            s_passwordInput = null;
            s_submitButton = null;
            s_promptWardID = ZDOID.None;
            SetPromptInputBlocked(false);
        }

        private static void SetPromptInputBlocked(bool blocked)
        {
            if (s_inputBlocked == blocked)
                return;

            GUIManager.BlockInput(blocked);
            s_inputBlocked = blocked;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static class Player_Update_ClosePasswordPrompt
        {
            private static void Postfix()
            {
                if (s_panel == null)
                    return;

                if (ZInput.GetKeyDown(KeyCode.Escape))
                    ClosePrompt();
                else if (ZInput.GetKeyDown(KeyCode.Return) || ZInput.GetKeyDown(KeyCode.KeypadEnter))
                    SubmitPassword();
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_RegisterPasswordRPCs
        {
            private static void Postfix() => RegisterRPCs();
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ResetPasswordRPCs
        {
            private static void Postfix() => ResetRPCRegistration();
        }

    }
}
