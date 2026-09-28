using System.IO;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// Only for the self-test copy (Debug.NoSteam = true in its config): skip the game's
    /// Steam start-up, so test runs don't count as Lilian's playtime on Steam.
    /// </summary>
    [HarmonyPatch(typeof(SteamManager), "Awake")]
    internal static class NoSteamPatch
    {
        private static bool Prefix()
        {
            if (Plugin.NoSteam == null || !Plugin.NoSteam.Value)
                return true;
            Plugin.Log.LogInfo("NoSteam: skipping Steam start-up (test copy).");
            return false;
        }
    }

    /// <summary>
    /// Only for the self-test copy (Debug.SaveFolder set): keep saves, options and thumbnails
    /// in a separate folder. Renaming the product in app.info does NOT move Unity 6's
    /// persistentDataPath, so without this the test copy shared her real save slots.
    /// </summary>
    internal static class TestSaves
    {
        internal static string Folder
        {
            get
            {
                string f = Plugin.SaveFolder?.Value;
                return string.IsNullOrWhiteSpace(f) ? null : f;
            }
        }

        private static string Real => Application.persistentDataPath.Replace('\\', '/');

        internal static string Redirect(string path)
        {
            string folder = Folder;
            if (folder == null || path == null)
                return path;
            Directory.CreateDirectory(folder);
            string p = path.Replace('\\', '/');
            string real = Real;
            if (p.StartsWith(real, System.StringComparison.OrdinalIgnoreCase))
                return folder.Replace('\\', '/') + p.Substring(real.Length);
            return path;
        }

        [HarmonyPatch(typeof(FileSystem), nameof(FileSystem.GetAbsolutePath))]
        private static class AbsolutePatch
        {
            private static void Postfix(ref string __result) => __result = Redirect(__result);
        }

        [HarmonyPatch(typeof(FileSystem), nameof(FileSystem.GetLocalPath))]
        private static class LocalPatch
        {
            private static bool Prefix(string absolutePath, ref string __result)
            {
                string folder = Folder;
                if (folder == null)
                    return true;
                string p = absolutePath.Replace('\\', '/');
                string f = folder.Replace('\\', '/').TrimEnd('/');
                __result = p.StartsWith(f, System.StringComparison.OrdinalIgnoreCase) ? p.Substring(f.Length).TrimStart('/') : p;
                return false;
            }
        }

        [HarmonyPatch(typeof(ScreenshotCapture), nameof(ScreenshotCapture.GetThumbnailPath))]
        private static class ThumbPatch
        {
            private static void Postfix(ref string __result) => __result = Redirect(__result);
        }
    }
}
