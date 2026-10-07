from pathlib import Path
import re
import subprocess
import textwrap

TARGET = 'fix/authorization-review-and-server-issues'

def read(name):
    return Path(name).read_bytes().decode('utf-8-sig').replace('\r\n', '\n')

def write(name, text):
    path = Path(name)
    data = path.read_bytes() if path.exists() else b''
    bom = b'\xef\xbb\xbf' if data.startswith(b'\xef\xbb\xbf') else b''
    newline = '\r\n' if b'\r\n' in data else '\n'
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(bom + text.replace('\r\n', '\n').replace('\n', newline).encode('utf-8'))

def replace(text, old, new, count=1):
    assert text.count(old) == count, repr(old[:120]) + ': unexpected matches ' + str(text.count(old))
    return text.replace(old, new)

def method(text, signature, replacement, indent=8):
    prefix = ' ' * indent
    start = text.index(prefix + signature)
    opening = text.index('\n' + prefix + '{', start)
    assert '\n' not in text[start:opening].strip(), signature
    end = text.index('\n' + prefix + '}\n', opening) + len('\n' + prefix + '}\n')
    return text[:start] + textwrap.indent(textwrap.dedent(replacement).strip() + '\n', prefix) + text[end:]

def add_compile(name):
    file = 'ProtectiveWards.csproj'
    text = read(file)
    marker = '    <Compile Include="AdminServerFeatures.cs" />'
    text = replace(text, marker, marker + '\n    <Compile Include="' + name.replace('/', '\\') + '" />')
    write(file, text)

def commit(message, *files):
    subprocess.run(['git', 'diff', '--check'], check=True)
    for name in files:
        path = Path(name)
        if path.suffix in ('.cs', '.md', '.csproj', '.yml', '.ps1'):
            assert not re.search(r'[\u0400-\u052f]', read(name)), 'Unexpected Cyrillic: ' + name
    subprocess.run(['git', 'add', '--', *files], check=True)
    subprocess.run(['git', 'commit', '-m', message], check=True)
    subprocess.run(['git', 'push', 'origin', 'HEAD:' + TARGET], check=True)
