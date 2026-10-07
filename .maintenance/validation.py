from support import *

def apply():
    text = read('WardRpc.cs')
    marker = '        internal static bool HasRemoteGodMode(long playerID)'
    text = replace(text, marker, '''        internal static ZPackage ReadBoundedPackage(ZPackage package, int maximumBytes)
        {
            int start = package.GetPos();
            int length = package.ReadInt();
            if (length < 0 || length > maximumBytes || length > package.Size() - package.GetPos())
                throw new ArgumentException("Invalid nested ward package length.");
            package.SetPos(start);
            return package.ReadPackage();
        }

''' + marker)
    write('WardRpc.cs', text)
    text = read('WardRequestFlow.cs').replace('envelope.ReadPackage()', 'WardRpc.ReadBoundedPackage(envelope, 65536)')
    write('WardRequestFlow.cs', text)
    text = read('WardPermittedPlayersUI.cs')
    text = replace(text, '''            if (query.Length > 64)
                query = query.Substring(0, 64);''', '''            if (query.Length > 64 || package.GetPos() != package.Size())
                throw new ArgumentException("Invalid permitted-player request.");''')
    text = replace(text, '            if (!CanApplyWardSettings(zdo, requester.PlayerID))', '            if (!CanApplyWardSettings(zdo, requester.PlayerID) || !WardRpc.IsWithinReach(requester, zdo))')
    text = replace(text, '''            int count = package.ReadInt();
            s_updatePending = false;''', '''            int count = package.ReadInt();
            if (count < 0 || count > 512 || detail.Length > 4096)
                throw new ArgumentException("Invalid permitted-player response.");
            List<KeyValuePair<long, string>> permitted = new(count);
            for (int i = 0; i < count; i++)
            {
                long id = package.ReadLong();
                string name = package.ReadString();
                if (id == 0L || name.Length > 256)
                    throw new ArgumentException("Invalid permitted-player entry.");
                permitted.Add(new KeyValuePair<long, string>(id, name));
            }
            if (package.GetPos() != package.Size())
                throw new ArgumentException("Unexpected permitted-player response payload.");
            s_updatePending = false;''')
    text = replace(text, '''            List<KeyValuePair<long, string>> permitted = new(count);
            for (int i = 0; i < count; i++)
                permitted.Add(new KeyValuePair<long, string>(package.ReadLong(), package.ReadString()));

''', '')
    write('WardPermittedPlayersUI.cs', text)
    text = read('WardExpiration.cs')
    text = replace(text, '''            if (!CanReactivate(zdo, requester.PlayerID))''', '''            if (!CanReactivate(zdo, requester.PlayerID) || !WardRpc.IsWithinReach(requester, zdo))''')
    write('WardExpiration.cs', text)
    commit('fix: validate ward interaction reach and bound nested request data', 'WardRpc.cs', 'WardRequestFlow.cs', 'WardPermittedPlayersUI.cs', 'WardExpiration.cs')

    text = read('WardPasswordProtection.cs')
    text = replace(text, 'using System;\n', 'using System;\nusing System.Collections.Generic;\n')
    text = replace(text, '        private static bool s_rpcRegistered;', '''        private static bool s_rpcRegistered;
        private static readonly Dictionary<long, PasswordAttempts> s_attempts = new();

        private sealed class PasswordAttempts
        {
            internal float NextAttempt;
            internal int Failures;
        }''')
    text = replace(text, '''            Unavailable,
            AlreadyPermitted''', '''            Unavailable,
            AlreadyPermitted,
            RateLimited''')
    text = replace(text, '''            s_rpcRegistered = false;
            ClosePrompt();''', '''            s_rpcRegistered = false;
            s_attempts.Clear();
            ClosePrompt();''')
    marker = '        private static void RPC_SubmitWardPasswordServer(long sender, ZPackage package)'
    text = replace(text, marker, '''        private static bool BeginPasswordAttempt(long sender)
        {
            foreach (long peerID in s_attempts.Keys.Where(id => id != 0L && ZNet.instance.GetPeer(id) == null).ToArray())
                s_attempts.Remove(peerID);
            if (!s_attempts.TryGetValue(sender, out PasswordAttempts attempts))
            {
                attempts = new PasswordAttempts();
                s_attempts[sender] = attempts;
            }
            if (Time.realtimeSinceStartup < attempts.NextAttempt)
                return false;
            attempts.NextAttempt = Time.realtimeSinceStartup + 1f;
            return true;
        }

        private static void RecordFailedPassword(long sender)
        {
            if (!s_attempts.TryGetValue(sender, out PasswordAttempts attempts))
                return;
            if (++attempts.Failures >= 5)
            {
                attempts.Failures = 0;
                attempts.NextAttempt = Time.realtimeSinceStartup + 30f;
            }
        }

''' + marker)
    text = replace(text, '''            if (WardZdoUtils.HasDirectAccessToWardZdo(zdo, requester.PlayerID))
            {''', '''            if (password.Length == 0 || password.Length > PasswordCharacterLimit
                || package.GetPos() != package.Size() || !WardRpc.IsWithinReach(requester, zdo)
                || ShouldBlockInactiveWardAccess(zdo, requester.PlayerID))
            {
                SendPasswordEntryResult(sender, wardID, PasswordEntryResult.Unavailable);
                return;
            }

            if (WardZdoUtils.HasDirectAccessToWardZdo(zdo, requester.PlayerID))
            {''')
    text = replace(text, '''            if (!VerifyPassword(zdo, password))
            {
                SendPasswordEntryResult''', '''            if (!BeginPasswordAttempt(sender))
            {
                SendPasswordEntryResult(sender, wardID, PasswordEntryResult.RateLimited);
                return;
            }

            if (!VerifyPassword(zdo, password))
            {
                RecordFailedPassword(sender);
                SendPasswordEntryResult''')
    text = replace(text, '''            WardZdoUtils.AddPermitted(zdo, requester.PlayerID, requester.PlayerName);''', '''            s_attempts.Remove(sender);
            WardZdoUtils.AddPermitted(zdo, requester.PlayerID, requester.PlayerName);''')
    text = replace(text, '''                case PasswordEntryResult.AlreadyPermitted:
''', '''                case PasswordEntryResult.RateLimited:
                    player.Message(MessageHud.MessageType.Center, "Too many password attempts. Wait briefly before trying again.");
                    if (s_submitButton != null)
                        s_submitButton.interactable = true;
                    break;
                case PasswordEntryResult.AlreadyPermitted:
''')
    write('WardPasswordProtection.cs', text)
    commit('fix: rate-limit and validate ward password enrollment attempts', 'WardPasswordProtection.cs')
