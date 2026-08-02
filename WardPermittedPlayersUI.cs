using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardPermittedPlayersUI
    {
        private const string RPC_UpdatePermittedPlayers = "PW_UpdatePermittedPlayers";
        private const string RPC_UpdatePermittedPlayersResult = "PW_UpdatePermittedPlayersResult";
        private const float PanelWidth = 600f;
        private const float PanelHeight = 600f;
        private const float PanelPadding = 30f;
        private const int PlayersPerPage = 10;
        private const int RowFontSize = 17;
        private const float RowStep = 38f;

        private static readonly List<KeyValuePair<long, string>> s_permittedPlayers = new();
        private static GameObject s_panel;
        private static ZDOID s_wardID = ZDOID.None;
        private static long s_wardCreatorID;
        private static int s_page;
        private static bool s_rpcRegistered;
        private static bool s_inputBlocked;

        private enum PermittedPlayerAction
        {
            Refresh,
            Add,
            Remove
        }

        private enum PermittedPlayerResult
        {
            Success,
            NoMatch,
            MultipleMatches,
            AlreadyPermitted,
            NotPermitted,
            NotAuthorized,
            TooFar,
            Unavailable
        }

        internal static void Open(PrivateArea ward)
        {
            if (!ArePerWardSettingsEnabled()
                || ward?.m_nview?.IsValid() != true
                || !CanEditWardSettings(ward, Player.m_localPlayer))
                return;

            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "ProtectiveWards: GUIManager is not ready");
                return;
            }

            WardSettingsUI.Close();
            Close();

            s_wardID = ward.m_nview.GetZDO().m_uid;
            s_wardCreatorID = ward.m_piece?.GetCreator() ?? 0L;
            s_page = 0;
            LoadPermittedPlayers(ward);
            CreatePanel();
            SetInputBlocked(true);
            RequestUpdate(s_wardID, PermittedPlayerAction.Refresh, 0L, "");
        }

        internal static void Close()
        {
            if (s_panel != null)
                UnityEngine.Object.Destroy(s_panel);

            s_panel = null;
            s_wardID = ZDOID.None;
            s_wardCreatorID = 0L;
            s_page = 0;
            s_permittedPlayers.Clear();
            SetInputBlocked(false);
        }

        private static void SetInputBlocked(bool blocked)
        {
            if (s_inputBlocked == blocked)
                return;

            GUIManager.BlockInput(blocked);
            s_inputBlocked = blocked;
        }

        private static void LoadPermittedPlayers(PrivateArea ward)
        {
            s_permittedPlayers.Clear();
            if (ward == null)
                return;

            s_permittedPlayers.AddRange(
                WardZdoUtils.GetPermittedPlayers(ward.GetWardZDO())
                    .OrderBy(player => player.Value, StringComparer.OrdinalIgnoreCase));
        }

        private static void CreatePanel()
        {
            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null || s_wardID.IsNone())
            {
                Close();
                return;
            }

            if (s_panel != null)
                UnityEngine.Object.Destroy(s_panel);

            s_panel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                width: PanelWidth,
                height: PanelHeight,
                draggable: true);
            s_panel.SetActive(true);

            CreateText("$pw_ward_permitted_title".Localize(), new Vector2(0f, 265f), 32, PanelWidth - PanelPadding * 2f, 44f, GUIManager.Instance.ValheimOrange, TextAnchor.MiddleCenter, FontStyle.Bold);

            float y = 205f;
            CreatePermittedPlayerRows(ref y);
            CreatePaginationControls();
            CreateBackButton();
        }

        private static void CreatePermittedPlayerRows(ref float y)
        {
            int pageCount = GetPageCount();
            s_page = Mathf.Clamp(s_page, 0, pageCount - 1);

            if (s_permittedPlayers.Count == 0)
            {
                CreateText("$pw_ward_permitted_empty".Localize(), new Vector2(0f, y - 10f), RowFontSize, PanelWidth - PanelPadding * 2f, 36f, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleCenter);
                return;
            }

            int first = s_page * PlayersPerPage;
            int last = Math.Min(first + PlayersPerPage, s_permittedPlayers.Count);
            float left = -PanelWidth * 0.5f + PanelPadding;
            float right = PanelWidth * 0.5f - PanelPadding;
            const float buttonWidth = 120f;
            float buttonX = right - buttonWidth * 0.5f;
            float textWidth = buttonX - buttonWidth * 0.5f - 8f - left;

            for (int i = first; i < last; i++)
            {
                KeyValuePair<long, string> player = s_permittedPlayers[i];
                CreateRowText(GetPlayerDisplayName(player.Value), new Vector2(left + textWidth * 0.5f, y), textWidth, Color.white);

                GameObject removeButton = GUIManager.Instance.CreateButton(
                    text: "$pw_ward_permitted_remove".Localize(),
                    parent: s_panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(buttonX, y),
                    width: buttonWidth,
                    height: 32f);
                long playerID = player.Key;
                removeButton.GetComponent<Button>().onClick.AddListener(() => RequestRemovePlayer(playerID));
                y -= RowStep;
            }
        }

        private static void CreatePaginationControls()
        {
            int pageCount = GetPageCount();
            if (pageCount <= 1)
                return;

            const float y = -205f;
            GameObject previousButton = GUIManager.Instance.CreateButton(
                text: "<",
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(-80f, y),
                width: 50f,
                height: 34f);
            Button previous = previousButton.GetComponent<Button>();
            previous.interactable = s_page > 0;
            previous.onClick.AddListener(() => ChangePage(-1));

            CreateText(
                "$pw_ward_permitted_page".Localize((s_page + 1).ToString(), pageCount.ToString()),
                new Vector2(0f, y),
                RowFontSize,
                90f,
                30f,
                Color.white,
                TextAnchor.MiddleCenter);

            GameObject nextButton = GUIManager.Instance.CreateButton(
                text: ">",
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(80f, y),
                width: 50f,
                height: 34f);
            Button next = nextButton.GetComponent<Button>();
            next.interactable = s_page + 1 < pageCount;
            next.onClick.AddListener(() => ChangePage(1));
        }

        private static void CreateBackButton()
        {
            GameObject backButton = GUIManager.Instance.CreateButton(
                text: "$pw_ward_settings_back".Localize(),
                parent: s_panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, -255f),
                width: 170f,
                height: 50f);
            backButton.GetComponent<Button>().onClick.AddListener(BackToWardSettings);
        }

        private static GameObject CreateText(string text, Vector2 position, int fontSize, float width, float height, Color color, TextAnchor alignment, FontStyle fontStyle = FontStyle.Normal)
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

            Text component = obj.GetComponent<Text>();
            if (component != null)
            {
                component.alignment = alignment;
                component.fontStyle = fontStyle;
            }
            return obj;
        }

        private static GameObject CreateRowText(string text, Vector2 position, float width, Color color)
        {
            return CreateText(text, position, RowFontSize, width, 30f, color, TextAnchor.MiddleLeft);
        }

        private static string GetPlayerDisplayName(string playerName)
        {
            return CensorShittyWords.FilterUGC(playerName ?? "", UGCType.CharacterName, s_wardCreatorID);
        }

        private static int GetPageCount()
        {
            return Math.Max(1, (s_permittedPlayers.Count + PlayersPerPage - 1) / PlayersPerPage);
        }

        private static void ChangePage(int direction)
        {
            s_page = Mathf.Clamp(s_page + direction, 0, GetPageCount() - 1);
            CreatePanel();
        }

        internal static void RequestAddPlayer(ZDOID wardID, string query)
        {
            string normalized = (query ?? "").Trim();
            if (normalized.Length == 0)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$pw_permit_no_match".Localize(normalized));
                return;
            }

            RequestUpdate(wardID, PermittedPlayerAction.Add, 0L, normalized);
        }

        private static void RequestRemovePlayer(long playerID)
        {
            RequestUpdate(s_wardID, PermittedPlayerAction.Remove, playerID, "");
        }

        private static void RequestUpdate(ZDOID wardID, PermittedPlayerAction action, long targetPlayerID, string query)
        {
            Player player = Player.m_localPlayer;
            if (player == null || wardID.IsNone())
                return;

            ZPackage package = new();
            package.Write(wardID);
            package.Write(player.GetPlayerID());
            package.Write((int)action);
            package.Write(targetPlayerID);
            package.Write(query ?? "");

            if (ZNet.instance?.IsServer() == true)
                RPC_UpdatePermittedPlayersServer(0L, new ZPackage(package.GetArray()));
            else if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(RPC_UpdatePermittedPlayers, package);
        }

        private static void RegisterRPCs()
        {
            if (s_rpcRegistered || ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.Register<ZPackage>(RPC_UpdatePermittedPlayersResult, RPC_UpdatePermittedPlayersResultClient);
            if (ZNet.instance?.IsServer() == true)
                ZRoutedRpc.instance.Register<ZPackage>(RPC_UpdatePermittedPlayers, RPC_UpdatePermittedPlayersServer);

            s_rpcRegistered = true;
        }

        private static void ResetRPCRegistration()
        {
            s_rpcRegistered = false;
        }

        private static void RPC_UpdatePermittedPlayersServer(long sender, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            long claimedPlayerID = package.ReadLong();
            PermittedPlayerAction action = (PermittedPlayerAction)package.ReadInt();
            long targetPlayerID = package.ReadLong();
            string query = (package.ReadString() ?? "").Trim();
            if (query.Length > 64)
                query = query.Substring(0, 64);

            if (!TryGetRoutedPlayer(sender, claimedPlayerID, out RoutedPlayerContext requester))
                return;

            if (!WardZdoUtils.TryGetWard(wardID, out ZDO zdo))
            {
                SendResult(sender, wardID, action, PermittedPlayerResult.Unavailable, "", null);
                return;
            }

            if (!CanApplyWardSettings(zdo, requester.PlayerID))
            {
                SendResult(sender, wardID, action, PermittedPlayerResult.NotAuthorized, "", zdo);
                return;
            }

            string detail = "";
            PermittedPlayerResult result = PermittedPlayerResult.Success;
            if ((int)action < (int)PermittedPlayerAction.Refresh || (int)action > (int)PermittedPlayerAction.Remove)
                result = PermittedPlayerResult.Unavailable;
            else if (action == PermittedPlayerAction.Add)
            {
                List<Player> matches = FindOnlinePlayers(query);
                if (matches.Count == 0)
                {
                    result = PermittedPlayerResult.NoMatch;
                    detail = query;
                }
                else if (matches.Count > 1)
                {
                    result = PermittedPlayerResult.MultipleMatches;
                    detail = string.Join(", ", matches.Select(player => player.GetPlayerName()).ToArray());
                }
                else
                {
                    Player target = matches[0];
                    targetPlayerID = target.GetPlayerID();
                    detail = target.GetPlayerName();
                    if (WardZdoUtils.IsExplicitlyPermitted(zdo, targetPlayerID))
                        result = PermittedPlayerResult.AlreadyPermitted;
                    else
                    {
                        WardZdoUtils.AddPermitted(zdo, targetPlayerID, detail);
                        LogInfo($"Added {detail} to ward {wardID} permitted list from ward settings");
                    }
                }
            }
            else if (action == PermittedPlayerAction.Remove)
            {
                if (!WardZdoUtils.RemovePermitted(zdo, targetPlayerID, out detail))
                    result = PermittedPlayerResult.NotPermitted;
                else
                    LogInfo($"Removed {detail} from ward {wardID} permitted list from ward settings");
            }

            SendResult(sender, wardID, action, result, detail, zdo);
        }

        private static List<Player> FindOnlinePlayers(string query)
        {
            string normalized = (query ?? "").Trim();
            if (normalized.Length == 0)
                return new List<Player>();

            List<Player> players = Player.GetAllPlayers().Where(player => player != null).ToList();
            List<Player> exact = players
                .Where(player => string.Equals(player.GetPlayerName(), normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exact.Count > 0)
                return exact;

            return players
                .Where(player => player.GetPlayerName().IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private static void SendResult(long targetPeerID, ZDOID wardID, PermittedPlayerAction action, PermittedPlayerResult result, string detail, ZDO zdo)
        {
            ZPackage response = new();
            response.Write(wardID);
            response.Write((int)action);
            response.Write((int)result);
            response.Write(detail ?? "");

            List<KeyValuePair<long, string>> permitted = WardZdoUtils.GetPermittedPlayers(zdo)
                .OrderBy(player => player.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            response.Write(permitted.Count);
            foreach (KeyValuePair<long, string> player in permitted)
            {
                response.Write(player.Key);
                response.Write(player.Value ?? "");
            }

            if (targetPeerID == 0L)
                RPC_UpdatePermittedPlayersResultClient(0L, new ZPackage(response.GetArray()));
            else if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(targetPeerID, RPC_UpdatePermittedPlayersResult, response);
        }

        private static void RPC_UpdatePermittedPlayersResultClient(long _, ZPackage package)
        {
            ZDOID wardID = package.ReadZDOID();
            PermittedPlayerAction action = (PermittedPlayerAction)package.ReadInt();
            PermittedPlayerResult result = (PermittedPlayerResult)package.ReadInt();
            string detail = package.ReadString();
            int count = package.ReadInt();

            List<KeyValuePair<long, string>> permitted = new(count);
            for (int i = 0; i < count; i++)
                permitted.Add(new KeyValuePair<long, string>(package.ReadLong(), package.ReadString()));

            if (s_wardID.Equals(wardID))
            {
                s_permittedPlayers.Clear();
                s_permittedPlayers.AddRange(permitted);
                s_page = Mathf.Clamp(s_page, 0, GetPageCount() - 1);
                if (s_panel != null)
                    CreatePanel();
            }

            if (result == PermittedPlayerResult.Success && action == PermittedPlayerAction.Add)
                WardSettingsUI.HandlePermittedPlayerAdded(wardID);

            ShowResultMessage(action, result, detail);
        }

        private static void ShowResultMessage(PermittedPlayerAction action, PermittedPlayerResult result, string detail)
        {
            if (action == PermittedPlayerAction.Refresh && result == PermittedPlayerResult.Success)
                return;

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            switch (result)
            {
                case PermittedPlayerResult.Success:
                    player.Message(
                        MessageHud.MessageType.Center,
                        action == PermittedPlayerAction.Add
                            ? "$pw_permit_added".Localize(detail)
                            : "$pw_ward_permitted_removed".Localize(detail));
                    break;
                case PermittedPlayerResult.NoMatch:
                    player.Message(MessageHud.MessageType.Center, "$pw_permit_no_match".Localize(detail));
                    break;
                case PermittedPlayerResult.MultipleMatches:
                    player.Message(MessageHud.MessageType.Center, "$pw_permit_multiple".Localize(detail));
                    break;
                case PermittedPlayerResult.AlreadyPermitted:
                    player.Message(MessageHud.MessageType.Center, "$pw_permit_already".Localize());
                    break;
                case PermittedPlayerResult.NotPermitted:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_permitted_not_found".Localize());
                    break;
                case PermittedPlayerResult.NotAuthorized:
                    player.Message(MessageHud.MessageType.Center, "$msg_privatezone");
                    break;
                case PermittedPlayerResult.TooFar:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_guild_too_far");
                    break;
                default:
                    player.Message(MessageHud.MessageType.Center, "$pw_ward_permitted_unavailable");
                    break;
            }
        }

        private static void BackToWardSettings()
        {
            PrivateArea ward = WardZdoUtils.FindLoadedWard(s_wardID);
            Close();
            if (ward != null)
                WardSettingsUI.Open(ward);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        private static class Player_Update_ClosePermittedPlayersUI
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

            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_RegisterPermittedPlayersRPCs
        {
            private static void Postfix() => RegisterRPCs();
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static class ZoneSystem_OnDestroy_ResetPermittedPlayersRPCs
        {
            private static void Postfix()
            {
                ResetRPCRegistration();
                Close();
            }
        }
    }
}
