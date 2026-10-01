"""Generate src/PartLooks.cs from tools/part_looks.txt."""
import io, os
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
rows = [l.rstrip('\n').split('|') for l in io.open(os.path.join(root, 'tools', 'part_looks.txt'), encoding='utf-8')
        if not l.startswith('#') and l.strip()]

def esc(s):
    return s.replace('\\', '\\\\').replace('"', '\\"')

out = ["// Generated from tools/part_looks.txt by tools/gen_partlooks.py (descriptions written from the game's own sketches).",
       'using System.Collections.Generic;', '', 'namespace DressmakerAccess', '{', '    internal static class PartLooks', '    {',
       '        internal static readonly Dictionary<string, string> Looks = new Dictionary<string, string>', '        {']
for t, n, d in rows:
    out.append('            { "%s/%s", "%s" },' % (esc(t), esc(n), esc(d)))
out += ['        };', '    }', '}', '']
io.open(os.path.join(root, 'src', 'PartLooks.cs'), 'w', encoding='utf-8').write('\n'.join(out))
print(len(rows), 'parts')
