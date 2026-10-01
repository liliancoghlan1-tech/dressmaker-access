using System.IO;
using System.Linq;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>Test-only dumps, so I can look at what the game draws (variant sketches, photo sets).</summary>
    internal static class DevDump
    {
        private static string Dir
        {
            get
            {
                string d = Path.Combine(Path.GetDirectoryName(Application.dataPath), "dump");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        internal static void SavePng(Texture tex, string file, Rect? part = null)
        {
            if (tex == null) return;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            Rect r = part ?? new Rect(0, 0, tex.width, tex.height);
            var t2 = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
            t2.ReadPixels(r, 0, 0);
            t2.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            string path = Path.Combine(Dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, t2.EncodeToPNG());
            Object.Destroy(t2);
        }

        private static string Safe(string s) => string.Join("_", s.Split(Path.GetInvalidFileNameChars()));

        /// <summary>Every part's first sketch, every fabric's texture, every accessory's icon, plus their notes.</summary>
        internal static void Catalogue()
        {
            foreach (GarmentComponent gc in Resources.FindObjectsOfTypeAll<GarmentComponent>().OrderBy(g => g.type.ToString()).ThenBy(g => g.name))
            {
                if (gc.variations == null || gc.variations.Count == 0) continue;
                Plugin.Log.LogInfo($"[cat] part|{gc.type}|{gc.name}|{gc.PrettyName}|{(gc.notes ?? "").Replace("\n", " ")}");
                SavePng(gc.variations[0].sketchedSprite, $"parts/{gc.type}_{Safe(gc.name)}.png");
            }
            foreach (Fabric f in Resources.FindObjectsOfTypeAll<Fabric>().OrderBy(x => x.name))
            {
                Plugin.Log.LogInfo($"[cat] fabric|{f.name}|{f.PrettyName}|{(f.fabricType != null ? f.fabricType.name : "")}|{string.Join(",", f.colors)}|{(f.notes ?? "").Replace("\n", " ")}");
                SavePng(f.texture, $"fabrics/{Safe(f.name)}.png");
            }
            foreach (AccessoryDefinition a in Resources.FindObjectsOfTypeAll<AccessoryDefinition>().OrderBy(x => x.name))
            {
                Plugin.Log.LogInfo($"[cat] acc|{a.name}|{a.PrettyName}|{(a.notes ?? "").Replace("\n", " ")}");
                if (a.icon != null) SavePng(a.icon.texture, $"acc/{Safe(a.name)}.png", a.icon.textureRect);
            }
        }

        internal static void Variants()
        {
            foreach (GarmentComponent gc in Resources.FindObjectsOfTypeAll<GarmentComponent>().OrderBy(g => g.type.ToString()).ThenBy(g => g.name))
            {
                if (gc.variations == null) continue;
                Plugin.Log.LogInfo($"[variants] {gc.type} {gc.name} '{gc.PrettyName}' variants={gc.variations.Count}");
                if (gc.variations.Count < 2) continue;
                for (int i = 0; i < gc.variations.Count; i++)
                {
                    var v = gc.variations[i];
                    string panels = string.Join("; ", v.panels.Select(p => p.prettyName + (p.mannequinMesh != null
                        ? $" [{p.mannequinMesh.name} {p.mannequinMesh.bounds.size.x:0.00}x{p.mannequinMesh.bounds.size.y:0.00}x{p.mannequinMesh.bounds.size.z:0.00}]" : "")));
                    Plugin.Log.LogInfo($"[variants]   v{i + 1} mat={(v.mannequinMaterial != null ? v.mannequinMaterial.name : "-")} sprite={(v.sketchedSprite != null ? v.sketchedSprite.name : "-")} panels: {panels}");
                    SavePng(v.sketchedSprite, $"{gc.type}_{gc.name}_v{i + 1}.png");
                }
            }
        }

        internal static void SkirtLengths()
        {
            foreach (GarmentComponent gc in Resources.FindObjectsOfTypeAll<GarmentComponent>().Where(g => g.type == GarmentComponent.Type.Skirt).OrderBy(g => g.name))
                for (int i = 0; i < gc.variations.Count; i++)
                {
                    var b = gc.variations[i].panels.Where(p => p.mannequinMesh != null).Select(p => p.mannequinMesh.bounds).ToList();
                    if (b.Count == 0) continue;
                    float top = b.Max(x => x.max.y), bottom = b.Min(x => x.min.y);
                    Plugin.Log.LogInfo($"[skirt] {gc.name} v{i + 1} top={top:0.00} bottom={bottom:0.00} len={top - bottom:0.00}");
                }
        }

        internal static void Photo()
        {
            var ps = Object.FindObjectsByType<PhotoScene>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (ps == null) { Plugin.Log.LogInfo("[photo] no PhotoScene"); return; }
            for (int i = 0; i < ps.environments.Count; i++)
            {
                GameObject e = ps.environments[i];
                string kids = string.Join(", ", e.GetComponentsInChildren<Transform>(true).Take(40).Select(t => t.name).Distinct());
                Plugin.Log.LogInfo($"[photo] env {i + 1} '{e.name}': {kids}");
            }
            for (int i = 0; i < ps.lightColors.Count; i++)
            {
                Color c = ps.lightColors[i];
                Color.RGBToHSV(c, out float h, out float s, out float v);
                Plugin.Log.LogInfo($"[photo] light {i + 1} #{ColorUtility.ToHtmlStringRGB(c)} h={h * 360:0} s={s:0.00} v={v:0.00}");
            }
        }
    }
}
