"""Generate src/FabricLooks.cs from tools/fabric_looks.txt (shown name|description).
tools/fabric_names.txt (from the `catalogue` test command: internal|shown|type|colours|notes) maps shown names
to the game's internal names, so translations of the shown name can't break the lookup."""
import io, os, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
T = os.path.join(root, 'tools')
names = {}
for l in io.open(os.path.join(T, 'fabric_names.txt'), encoding='utf-8'):
    p = l.rstrip('\n').split('|')
    if len(p) >= 2:
        if p[1].strip() in names and names[p[1].strip()] != p[0]:
            print('duplicate shown name:', p[1], names[p[1]], p[0])
        names.setdefault(p[1].strip(), p[0])
rows = [l.rstrip('\n').split('|', 1) for l in io.open(os.path.join(T, 'fabric_looks.txt'), encoding='utf-8')
        if not l.startswith('#') and l.strip()]

def esc(s):
    return s.replace('\\', '\\\\').replace('"', '\\"')

out = ["// Generated from tools/fabric_looks.txt by tools/gen_fabriclooks.py (written from the game's own fabric textures).",
       'using System.Collections.Generic;', '', 'namespace DressmakerAccess', '{', '    internal static class FabricLooks', '    {',
       '        internal static readonly Dictionary<string, string> Looks = new Dictionary<string, string>', '        {']
missing = 0
seen = set()
for shown, d in rows:
    if shown not in names:
        print('no internal name for', shown); missing += 1; continue
    if names[shown] in seen:
        continue
    seen.add(names[shown])
    out.append('            { "%s", "%s" },' % (esc(names[shown]), esc(d)))
out += ['        };', '    }', '}', '']
io.open(os.path.join(root, 'src', 'FabricLooks.cs'), 'w', encoding='utf-8').write('\n'.join(out))
print(len(seen), 'fabrics,', missing, 'missing')
sys.exit(1 if missing else 0)
