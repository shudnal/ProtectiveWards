from pathlib import Path
import re
import subprocess


TARGET = 'fix/authorization-review-and-server-issues'
EXPECTED = '9646762f3936f83df683a5a5650bfe007e037302'


def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()


def replace_and_commit(filename, old, new, message):
    path = Path(filename)
    data = path.read_bytes()
    bom = b'\xef\xbb\xbf' if data.startswith(b'\xef\xbb\xbf') else b''
    newline = '\r\n' if b'\r\n' in data else '\n'
    text = data.decode('utf-8-sig').replace('\r\n', '\n')
    if text.count(old) != 1:
        raise RuntimeError('Expected exactly one authorization guard in ' + filename)
    result = text.replace(old, new, 1)
    if re.search(r'[\u0400-\u052f]', result):
        raise RuntimeError('Unexpected Cyrillic in ' + filename)
    path.write_bytes(bom + result.replace('\n', newline).encode('utf-8'))
    if git('diff', '--name-only') != filename:
        raise RuntimeError('Unexpected changed files')
    subprocess.run(['git', 'diff', '--check'], check=True)
    subprocess.run(['git', 'diff', '--', filename], check=True)
    subprocess.run(['git', 'add', '--', filename], check=True)
    subprocess.run(['git', 'commit', '-m', message], check=True)
    subprocess.run(['git', 'push', 'origin', 'HEAD:' + TARGET], check=True)
    print('Source commit:', git('rev-parse', 'HEAD'), flush=True)


if git('rev-parse', 'HEAD') != EXPECTED or git('status', '--porcelain'):
    raise RuntimeError('Unexpected source branch state; inspect concurrent changes before applying.')

old = '''                    case Operation.Expire:
                    case Operation.Unexpire:
                        if (!HasWardManagementAccess(ward, actor.PlayerID))'''
new = '''                    case Operation.Expire:
                    case Operation.Unexpire:
                        if (!HasWardAdminAccess(actor.PlayerID))'''
replace_and_commit('WardControl.cs', old, new,
                   'fix: require administrator access for ward expiration commands')

old = '''            if (!CanApplyWardSettings(zdo, requester.PlayerID))
            {
                SendResult(sender, wardID, action, PermittedPlayerResult.NotAuthorized, "", zdo);'''
new = '''            if (!CanApplyWardSettings(zdo, requester.PlayerID) || !WardRpc.IsWithinReach(requester, zdo))
            {
                SendResult(sender, wardID, action, PermittedPlayerResult.NotAuthorized, "", zdo);'''
replace_and_commit('WardPermittedPlayersUI.cs', old, new,
                   'fix: enforce server-side reach for ward permitted-list requests')

print('Final source head:', git('rev-parse', 'HEAD'))
print('Changed files:', git('diff', '--name-only', EXPECTED, 'HEAD'))
