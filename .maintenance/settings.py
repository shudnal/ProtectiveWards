from support import *

def apply():
    text = read('WardSettingsUI.cs')
    text = replace(text, '        private static ZDO s_zdo;', '''        private static ZDO s_zdo;
        private static long s_loadedSettingsRevision;
        private static readonly int s_settingsRevision = "pw_settings_revision".GetStableHashCode();''')
    marker = '        internal static void RegisterRPCs()'
    text = replace(text, marker, '''        internal static long GetSettingsRevision(ZDO zdo) => zdo?.GetLong(s_settingsRevision, 0L) ?? 0L;

        internal static void AdvanceSettingsRevision(ZDO zdo)
        {
            zdo.Set(s_settingsRevision, unchecked(GetSettingsRevision(zdo) + 1L));
        }

        internal static long GetOpenSettingsRevision(ZDOID ward)
        {
            return s_zdo != null && s_zdo.m_uid == ward ? s_loadedSettingsRevision : -1L;
        }

        internal static void AcceptSettingsRevision(ZDOID ward, long revision)
        {
            if (s_zdo != null && s_zdo.m_uid == ward && revision == unchecked(s_loadedSettingsRevision + 1L))
                s_loadedSettingsRevision = revision;
        }

        internal static void HandleSettingsConflict(ZDOID ward)
        {
            CancelPendingNetworkOperation(ward);
            ZDOMan.instance?.RequestZDO(ward);
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                "Ward settings changed while this window was open. Reopen the ward and apply your changes again.");
        }

''' + marker)
    text = replace(text, '''        private enum ApplySettingsResult
        {
            Success,
            NotAuthorized,
            Unavailable
        }''', '''        private enum ApplySettingsResult
        {
            Success,
            NotAuthorized,
            Unavailable,
            Conflict
        }''')
    text = replace(text, '            s_waitingPasswordApply = s_canChangePassword && (replacePassword || s_passwordEnabled != s_appliedPasswordEnabled);', '            s_waitingPasswordApply = !s_canEditGeneralSettings && s_canChangePassword && (replacePassword || s_passwordEnabled != s_appliedPasswordEnabled);')
    text = replace(text, '''            // Password changes use the current permissions. General access changes may revoke
            // the editor's own Permit everyone or guild access, so they must be sent last.''', '''            // General and password changes are validated against the same state and committed together.
            // Password-only editors retain their independently authorized operation.''')
    needle = '''            Player player = Player.m_localPlayer;
            if (result == ApplySettingsResult.NotAuthorized)'''
    text = replace(text, needle, '''            if (result == ApplySettingsResult.Conflict)
            {
                HandleSettingsConflict(wardID);
                return;
            }

''' + needle)
    text = replace(text, '''            package.Write(Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L);
            int storedFieldCount''', '''            package.Write(Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L);
            package.Write(s_loadedSettingsRevision);
            int storedFieldCount''')
    text = replace(text, '            // Appended after the 2.0.10 payload so older servers safely ignore the new field.', '            // The explicit toggle is carried separately from the historical field IDs.')
    text = replace(text, '''            if (includePermittedToggle)
                package.Write(permittedToggle.BoolValue);

            return package;''', '''            if (includePermittedToggle)
                package.Write(permittedToggle.BoolValue);

            bool includePassword = s_canChangePassword && (s_applyReplacePassword || s_passwordEnabled != s_appliedPasswordEnabled);
            package.Write(includePassword);
            if (includePassword)
            {
                package.Write(s_passwordEnabled);
                package.Write(s_applyReplacePassword);
                package.Write(s_applyReplacePassword ? s_passwordValue : "");
            }
            return package;''')
    text = method(text, 'private static void RPC_ApplyWardSettingsServer(long sender, ZPackage package)', '''private static void RPC_ApplyWardSettingsServer(long sender, ZPackage package)
{
    ZDOID zdoID = package.ReadZDOID();
    long playerID = package.ReadLong();
    long expectedRevision = package.ReadLong();
    if (!TryGetRoutedPlayer(sender, playerID, out RoutedPlayerContext requester))
        return;

    ZDO zdo = WardZdoUtils.GetWard(zdoID);
    if (zdo == null)
    {
        SendApplySettingsResult(sender, zdoID, ApplySettingsResult.Unavailable);
        return;
    }
    if (!CanApplyWardSettings(zdo, requester.PlayerID) || !WardRpc.IsWithinReach(requester, zdo))
    {
        SendApplySettingsResult(sender, zdoID, ApplySettingsResult.NotAuthorized);
        return;
    }
    if (GetSettingsRevision(zdo) != expectedRevision)
    {
        SendApplySettingsResult(sender, zdoID, ApplySettingsResult.Conflict);
        return;
    }

    int count = package.ReadInt();
    if (count < 0 || count > (int)FieldId.PermittedPlayersCanToggle)
        throw new ArgumentException("Invalid ward field count.");
    int fieldsStart = package.GetPos();
    HashSet<FieldId> fields = new();
    for (int i = 0; i < count; i++)
        ValidateStoredField(package, fields);

    bool includeGuild = package.ReadBool();
    bool guildEnabled = includeGuild && package.ReadBool();
    if (includeGuild && !GuildsCompat.IsEnabled)
        throw new ArgumentException("Guild integration is unavailable.");
    bool includeToggle = package.ReadBool();
    bool toggleEnabled = includeToggle && package.ReadBool();
    bool includePassword = package.ReadBool();
    Action<ZDO> applyPassword = null;
    if (includePassword)
    {
        bool passwordEnabled = package.ReadBool();
        bool replacePassword = package.ReadBool();
        string password = package.ReadString();
        if (!WardPasswordProtection.CanChangePassword(zdo, requester.PlayerID))
        {
            SendApplySettingsResult(sender, zdoID, ApplySettingsResult.NotAuthorized);
            return;
        }
        if (!WardPasswordProtection.TryPrepareSettingsUpdate(zdo, passwordEnabled, replacePassword, password, out applyPassword, out _))
        {
            SendApplySettingsResult(sender, zdoID, ApplySettingsResult.Unavailable);
            return;
        }
    }
    if (package.GetPos() != package.Size())
        throw new ArgumentException("Unexpected ward settings payload.");

    // No live field is changed until the complete request, permissions and password hash are ready.
    WardZdoUtils.EnsureWardSettingsInitialized(zdo);
    package.SetPos(fieldsStart);
    for (int i = 0; i < count; i++)
        ApplyField(zdo, package);
    if (includeGuild)
        GuildsCompat.SetGuildAccessEnabled(zdo, guildEnabled);
    if (includeToggle)
        zdo.Set(s_permittedPlayersCanToggle, toggleEnabled);
    applyPassword?.Invoke(zdo);
    AdvanceSettingsRevision(zdo);
    WardZdoUtils.UpdateWardRuntimeFields(zdo);
    PrivateArea loadedWard = WardZdoUtils.FindLoadedWard(zdoID);
    if (loadedWard != null)
        RefreshWardVisuals(loadedWard);
    SendApplySettingsResult(sender, zdoID, ApplySettingsResult.Success);
    LogInfo($"Ward settings applied for {zdoID}");
}''')
    marker = '        private static void ApplyField(ZDO zdo, ZPackage package)'
    validation = '''        private static void ValidateStoredField(ZPackage package, HashSet<FieldId> seen)
        {
            FieldId field = (FieldId)package.ReadInt();
            if (!Enum.IsDefined(typeof(FieldId), field) || field == FieldId.PermittedPlayersCanToggle
                || !seen.Add(field) || package.ReadBool())
                throw new ArgumentException("Invalid or duplicate explicit ward field.");

            switch (field)
            {
                case FieldId.BubbleEnabled:
                case FieldId.CustomRange:
                case FieldId.CustomColor:
                case FieldId.CircleEnabled:
                case FieldId.PermitEveryone:
                    package.ReadBool();
                    break;
                case FieldId.BubbleColor:
                case FieldId.EmissionColor:
                    for (int i = 0; i < 4; i++)
                        ReadFiniteSetting(package);
                    break;
                case FieldId.CircleStartColor:
                case FieldId.CircleEndColor:
                    string color = package.ReadString();
                    if (color.Length != 8 || !ColorUtility.TryParseHtmlString("#" + color, out _))
                        throw new ArgumentException("Invalid ward marker color.");
                    break;
                default:
                    ReadFiniteSetting(package);
                    break;
            }
        }

        private static float ReadFiniteSetting(ZPackage package)
        {
            float value = package.ReadSingle();
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Ward settings must be finite.");
            return value;
        }

'''
    text = replace(text, marker, validation + marker)
    text = replace(text, '''            bool useDefault = package.ReadBool();
            if (useDefault)
            {
                ApplyConfiguredDefaultField(zdo, field);
                return;
            }''', '            package.ReadBool(); // Validated explicit-value marker.')
    start = text.index('        private static void ApplyConfiguredDefaultField(')
    end = text.index('        private static void ApplyBool(', start)
    text = text[:start] + text[end:]
    text = replace(text, '''        private static void LoadValuesFromZDO()
        {
            s_values.Clear();''', '''        private static void LoadValuesFromZDO()
        {
            s_loadedSettingsRevision = GetSettingsRevision(s_zdo);
            s_values.Clear();''')
    write('WardSettingsUI.cs', text)

    text = read('WardPasswordProtection.cs')
    text = replace(text, '''            Unavailable,
            PasswordTooLong''', '''            Unavailable,
            PasswordTooLong,
            Conflict''')
    text = replace(text, '''            package.Write(enabled);
            package.Write(replacePassword);
            package.Write(password);''', '''            package.Write(enabled);
            package.Write(replacePassword);
            package.Write(password);
            package.Write(WardSettingsUI.GetOpenSettingsRevision(wardID));''')
    text = method(text, 'private static void RPC_UpdateWardPasswordServer(long sender, ZPackage package)', '''private static void RPC_UpdateWardPasswordServer(long sender, ZPackage package)
{
    ZDOID wardID = package.ReadZDOID();
    long claimedPlayerID = package.ReadLong();
    bool enabled = package.ReadBool();
    bool replacePassword = package.ReadBool();
    string password = package.ReadString() ?? "";
    long expectedRevision = package.ReadLong();
    if (package.GetPos() != package.Size())
        throw new ArgumentException("Unexpected ward password payload.");
    if (!TryGetRoutedPlayer(sender, claimedPlayerID, out RoutedPlayerContext requester))
        return;
    if (!WardZdoUtils.TryGetWard(wardID, out ZDO zdo))
    {
        SendPasswordSettingsResult(sender, wardID, PasswordSettingsResult.Unavailable, false, false);
        return;
    }
    if (!CanChangePassword(zdo, requester.PlayerID) || !WardRpc.IsWithinReach(requester, zdo))
    {
        SendPasswordSettingsResult(sender, wardID, PasswordSettingsResult.NotAuthorized, false, false);
        return;
    }
    if (WardSettingsUI.GetSettingsRevision(zdo) != expectedRevision)
    {
        SendPasswordSettingsResult(sender, wardID, PasswordSettingsResult.Conflict, false, false);
        return;
    }
    if (!TryPrepareSettingsUpdate(zdo, enabled, replacePassword, password, out Action<ZDO> update, out PasswordSettingsResult error))
    {
        SendPasswordSettingsResult(sender, wardID, error, false, false);
        return;
    }
    update(zdo);
    WardSettingsUI.AdvanceSettingsRevision(zdo);
    SendPasswordSettingsResult(sender, wardID, PasswordSettingsResult.Success, zdo.GetBool(s_passwordProtectionEnabled, false), HasPassword(zdo));
}''')
    start = text.index('        private static void StorePassword(ZDO zdo, string password)')
    end = text.index('        private static bool VerifyPassword(', start)
    prepared = '''        internal static bool TryPrepareSettingsUpdate(ZDO zdo, bool enabled, bool replacePassword, string password,
            out Action<ZDO> update, out PasswordSettingsResult error)
        {
            update = null;
            error = PasswordSettingsResult.Success;
            if (zdo == null || password == null || password.Length > PasswordCharacterLimit)
            {
                error = PasswordSettingsResult.PasswordTooLong;
                return false;
            }
            if (replacePassword && password.Length == 0)
                enabled = false;
            bool hasPassword = replacePassword ? password.Length > 0 : HasPassword(zdo);
            if (enabled && !hasPassword)
            {
                error = PasswordSettingsResult.MissingPassword;
                return false;
            }
            string saltText = "";
            string hashText = "";
            if (replacePassword && password.Length > 0)
            {
                byte[] salt = new byte[16];
                using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                    random.GetBytes(salt);
                using Rfc2898DeriveBytes derive = new(password, salt, PasswordHashIterations);
                saltText = Convert.ToBase64String(salt);
                hashText = Convert.ToBase64String(derive.GetBytes(32));
            }
            bool storePlaintext = wardPasswordFieldMode.Value == WardPasswordFieldMode.EditablePassword;
            update = target =>
            {
                if (replacePassword)
                {
                    if (password.Length == 0)
                    {
                        RemoveZdoString(target, s_passwordHash);
                        RemoveZdoString(target, s_passwordSalt);
                    }
                    else
                    {
                        target.Set(s_passwordSalt, saltText);
                        target.Set(s_passwordHash, hashText);
                    }
                    if (storePlaintext && password.Length > 0)
                        target.Set(s_passwordPlaintext, password);
                    else
                        RemoveZdoString(target, s_passwordPlaintext);
                }
                target.Set(s_passwordProtectionEnabled, enabled);
            };
            return true;
        }

'''
    text = text[:start] + prepared + text[end:]
    text = replace(text, '''            response.Write(hasPassword);

            if (ZNet.instance''', '''            response.Write(hasPassword);
            response.Write(WardSettingsUI.GetSettingsRevision(WardZdoUtils.GetWard(wardID)));

            if (ZNet.instance''')
    text = replace(text, '''            bool hasPassword = package.ReadBool();
            WardSettingsUI.OnPasswordSettingsResult''', '''            bool hasPassword = package.ReadBool();
            long revision = package.ReadLong();
            if (result == PasswordSettingsResult.Conflict)
            {
                WardSettingsUI.HandleSettingsConflict(wardID);
                return;
            }
            if (result == PasswordSettingsResult.Success)
                WardSettingsUI.AcceptSettingsRevision(wardID, revision);
            WardSettingsUI.OnPasswordSettingsResult''')
    assert 'StorePassword(' not in text
    write('WardPasswordProtection.cs', text)

    text = read('Compatibility/GuildsCompat.cs')
    # Keep existing result values stable; conflict is handled using the generic unavailable result plus a revision mismatch.
    text = replace(text, '            package.Write(bind);', '            package.Write(bind);\n            package.Write(WardSettingsUI.GetOpenSettingsRevision(wardID));')
    text = replace(text, '            bool bind = package.ReadBool();', '            bool bind = package.ReadBool();\n            long expectedRevision = package.ReadLong();\n            if (package.GetPos() != package.Size())\n                throw new ArgumentException("Unexpected guild binding payload.");')
    text = replace(text, '            if (!CanApplyWardSettings(zdo, requester.PlayerID))', '            if (!CanApplyWardSettings(zdo, requester.PlayerID) || !WardRpc.IsWithinReach(requester, zdo))')
    text = replace(text, '''            if (bind)
            {''', '''            if (WardSettingsUI.GetSettingsRevision(zdo) != expectedRevision)
            {
                SendGuildBindingResult(sender, wardID, GuildBindingResult.Unavailable, zdo);
                return;
            }

            if (bind)
            {''')
    text = replace(text, '''            SendGuildBindingResult(sender, wardID, GuildBindingResult.Success, zdo);''', '''            WardSettingsUI.AdvanceSettingsRevision(zdo);
            SendGuildBindingResult(sender, wardID, GuildBindingResult.Success, zdo);''')
    text = replace(text, '            response.Write(GetBoundGuildName(zdo));', '            response.Write(GetBoundGuildName(zdo));\n            response.Write(WardSettingsUI.GetSettingsRevision(zdo));')
    text = replace(text, '''            string guildName = package.ReadString() ?? "";

            WardSettingsUI.OnGuildBindingResult''', '''            string guildName = package.ReadString() ?? "";
            long revision = package.ReadLong();
            if (result == GuildBindingResult.Success)
                WardSettingsUI.AcceptSettingsRevision(wardID, revision);
            else if (result == GuildBindingResult.Unavailable && revision != WardSettingsUI.GetOpenSettingsRevision(wardID))
            {
                WardSettingsUI.HandleSettingsConflict(wardID);
                return;
            }

            WardSettingsUI.OnGuildBindingResult''')
    write('Compatibility/GuildsCompat.cs', text)
    commit('fix: validate and commit versioned ward settings atomically', 'WardSettingsUI.cs', 'WardPasswordProtection.cs', 'Compatibility/GuildsCompat.cs')
