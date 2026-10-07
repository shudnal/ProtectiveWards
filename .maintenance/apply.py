from pathlib import Path
import subprocess


def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()


expected = 'b0536300fa596d9a2dc4b73aff5830504fc7a363'
if git('rev-parse', 'HEAD') != expected:
    raise RuntimeError('Unexpected source branch head; inspect concurrent changes before applying.')

path = Path('FullProtection.cs')
data = path.read_bytes()
has_bom = data.startswith(b'\xef\xbb\xbf')
newline = '\r\n' if b'\r\n' in data else '\n'
text = data.decode('utf-8-sig').replace('\r\n', '\n')
text = 'using BepInEx.Bootstrap;\n' + text
old = '''                HashSet<MethodBase> methods = new();
                foreach (Type type in GetLoadedInteractableTypes())'''
new = '''                HashSet<Assembly> assemblies = GetInteractableAssemblies();
                HashSet<MethodBase> methods = new();
                foreach (Type type in GetLoadedInteractableTypes(assemblies))'''
assert text.count(old) == 1
text = text.replace(old, new)
text = text.replace('ShouldPatchInteractableMethod(interact) && methods.Add(interact)', 'ShouldPatchInteractableMethod(interact, assemblies) && methods.Add(interact)')
text = text.replace('ShouldPatchInteractableMethod(useItem) && methods.Add(useItem)', 'ShouldPatchInteractableMethod(useItem, assemblies) && methods.Add(useItem)')
old = '''            private static IEnumerable<Type> GetLoadedInteractableTypes()
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())'''
new = '''            private static HashSet<Assembly> GetInteractableAssemblies()
            {
                // Preserve generic vanilla protection through this explicit game assembly.
                // Only live BepInEx plugin instances may contribute optional mod assemblies.
                HashSet<Assembly> assemblies = new() { typeof(Interactable).Assembly };
                foreach (BepInEx.PluginInfo pluginInfo in Chainloader.PluginInfos.Values)
                {
                    if (pluginInfo?.Instance == null)
                        continue;

                    Assembly assembly = pluginInfo.Instance.GetType().Assembly;
                    if (assembly != null && !assembly.IsDynamic)
                        assemblies.Add(assembly);
                }
                return assemblies;
            }

            private static IEnumerable<Type> GetLoadedInteractableTypes(IEnumerable<Assembly> assemblies)
            {
                foreach (Assembly assembly in assemblies)'''
assert text.count(old) == 1
text = text.replace(old, new)
old = 'private static bool ShouldPatchInteractableMethod(MethodInfo method)'
new = 'private static bool ShouldPatchInteractableMethod(MethodInfo method, HashSet<Assembly> assemblies)'
assert text.count(old) == 1
text = text.replace(old, new)
old = '''                    || method.DeclaringType == null
                    || method.DeclaringType == typeof(Interactable)'''
new = '''                    || method.DeclaringType == null
                    || !assemblies.Contains(method.DeclaringType.Assembly)
                    || method.DeclaringType == typeof(Interactable)'''
assert text.count(old) == 1
text = text.replace(old, new)
assert 'AppDomain.CurrentDomain.GetAssemblies()' not in text
assert 'catch (ReflectionTypeLoadException ex)' in text
assert 'SafeIsInteractableType(type)' in text
assert 'method.GetMethodBody() != null' in text
assert 'AccessTools.DeclaredMethod(declaringType' in text
encoded = text.replace('\n', newline).encode('utf-8')
path.write_bytes((b'\xef\xbb\xbf' if has_bom else b'') + encoded)
subprocess.run(['git', 'diff', '--check'], check=True)
subprocess.run(['git', 'diff', '--stat'], check=True)
subprocess.run(['git', 'diff', '--', 'FullProtection.cs'], check=True)
subprocess.run(['git', 'add', 'FullProtection.cs'], check=True)
subprocess.run(['git', 'commit', '-m', 'fix: restrict interactable discovery to active plugin assemblies (#16)'], check=True)
subprocess.run(['git', 'push', 'origin', 'HEAD:fix/authorization-review-and-server-issues'], check=True)
print('Source commit:', git('rev-parse', 'HEAD'))
print('Changed source files:', git('diff-tree', '--no-commit-id', '--name-only', '-r', 'HEAD'))
