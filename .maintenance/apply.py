from pathlib import Path
import json
import re
import subprocess


TARGET = 'fix/authorization-review-and-server-issues'
EXPECTED = '31cf24d21d797ec43b593e57ddbe473c9cda70fc'
VERSION = '2.1.0'


def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()


def read(filename):
    return Path(filename).read_bytes().decode('utf-8-sig').replace('\r\n', '\n')


def write(filename, text):
    path = Path(filename)
    data = path.read_bytes()
    bom = b'\xef\xbb\xbf' if data.startswith(b'\xef\xbb\xbf') else b''
    newline = '\r\n' if b'\r\n' in data else '\n'
    if re.search(r'[\u0400-\u052f]', text):
        raise RuntimeError('Unexpected Cyrillic in ' + filename)
    path.write_bytes(bom + text.replace('\n', newline).encode('utf-8'))


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise RuntimeError('Expected exactly one match for ' + repr(old))
    return text.replace(old, new, 1)


if git('rev-parse', 'HEAD') != EXPECTED or git('status', '--porcelain'):
    raise RuntimeError('Unexpected source branch state; inspect concurrent changes before applying.')

source_file = 'ProtectiveWards.cs'
manifest_file = 'package/thunderstore/ProtectiveWards/manifest.json'
changelog_file = 'package/thunderstore/ProtectiveWards/CHANGELOG.md'

source = read(source_file)
compatibility = '    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]'
source = replace_once(source, compatibility,
    '    // Major/minor versions define the RPC compatibility boundary; patch releases must preserve the wire format.\n'
    + compatibility)
source = replace_once(source,
    'public const string pluginVersion = "2.0.15";',
    'public const string pluginVersion = "' + VERSION + '";')
write(source_file, source)

manifest_text = read(manifest_file)
old_manifest = json.loads(manifest_text)
manifest_text = replace_once(manifest_text, '"version_number": "2.0.15"', '"version_number": "' + VERSION + '"')
manifest = json.loads(manifest_text)
if old_manifest['dependencies'] != manifest['dependencies']:
    raise RuntimeError('Dependencies must not change in this correction.')
write(manifest_file, manifest_text)

changelog = read(changelog_file)
if not changelog.startswith('# 2.0.15\n'):
    raise RuntimeError('Unexpected changelog head.')
changelog = (
    '# ' + VERSION + '\n'
    '* IMPORTANT: update the server and all clients together. The revised ward RPC protocol is not network-compatible with 2.0.x.\n'
    '* kept minor-version network compatibility enforcement so 2.0.x and 2.1.x installations are rejected during connection instead of failing ward settings and access operations.\n\n'
    + changelog)
write(changelog_file, changelog)

# Validate metadata and the intended source delta without building or executing the mod.
assembly_info = read('Properties/AssemblyInfo.cs')
for attribute in ['AssemblyVersion', 'AssemblyFileVersion']:
    if '[assembly: ' + attribute + '(ProtectiveWards.ProtectiveWards.pluginVersion)]' not in assembly_info:
        raise RuntimeError('Assembly version is not linked to pluginVersion.')
if json.loads(read(manifest_file))['version_number'] != VERSION:
    raise RuntimeError('Manifest version mismatch.')
if compatibility not in read(source_file):
    raise RuntimeError('The minor-version compatibility policy must be retained.')
expected_files = {source_file, manifest_file, changelog_file}
if set(git('diff', '--name-only').splitlines()) != expected_files:
    raise RuntimeError('Unexpected changed files.')
subprocess.run(['git', 'diff', '--check'], check=True)
subprocess.run(['git', 'diff', '--stat'], check=True)
subprocess.run(['git', 'diff', '--', *sorted(expected_files)], check=True)
subprocess.run(['git', 'add', '--', *sorted(expected_files)], check=True)
subprocess.run(['git', 'commit', '-m', 'fix: establish 2.1 network compatibility for revised ward RPCs'], check=True)
subprocess.run(['git', 'push', 'origin', 'HEAD:' + TARGET], check=True)
print('Source commit:', git('rev-parse', 'HEAD'), flush=True)
print('Changed source files:', git('diff-tree', '--no-commit-id', '--name-only', '-r', 'HEAD'), flush=True)
