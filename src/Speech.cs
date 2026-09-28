using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DressmakerAccess
{
    /// <summary>
    /// Speech straight to NVDA via nvdaControllerClient64.dll (in the game root).
    /// Every line is also written to the BepInEx log as "[speech] ..." so a test run
    /// can be read back without anyone listening.
    /// </summary>
    internal static class Speech
    {
        [DllImport("nvdaControllerClient64.dll")]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode)]
        private static extern int nvdaController_speakText(string text);

        [DllImport("nvdaControllerClient64.dll")]
        private static extern int nvdaController_cancelSpeech();

        private static readonly Regex Sprite = new Regex("<sprite[^>]*>");
        private static readonly Regex Tags = new Regex("<[^>]+>");
        private static readonly Regex Spaces = new Regex("\\s+");
        private static bool _dllMissing;

        /// <summary>The last thing said with interrupt, for the repeat key.</summary>
        internal static string Last { get; private set; } = "";

        internal static void Say(string text, bool interrupt = true)
        {
            text = Clean(text);
            if (string.IsNullOrEmpty(text))
                return;
            if (interrupt)
                Last = text;
            Plugin.Log.LogInfo((interrupt ? "[speech] " : "[speech+] ") + text);
            if (_dllMissing || (Plugin.MuteSpeech != null && Plugin.MuteSpeech.Value))
                return;
            try
            {
                if (nvdaController_testIfRunning() != 0)
                    return;
                if (interrupt)
                    nvdaController_cancelSpeech();
                nvdaController_speakText(text);
            }
            catch (DllNotFoundException)
            {
                _dllMissing = true;
                Plugin.Log.LogError("nvdaControllerClient64.dll not found in the game folder.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Speech failed: " + e.Message);
            }
        }

        private static readonly System.Collections.Generic.List<(float at, string text)> _later =
            new System.Collections.Generic.List<(float, string)>();

        /// <summary>Queue a line a moment from now (after e.g. the room name has been said).</summary>
        internal static void Later(string text, float delay = 0.4f) => _later.Add((UnityEngine.Time.unscaledTime + delay, text));

        internal static void Tick()
        {
            for (int i = 0; i < _later.Count; i++)
            {
                if (UnityEngine.Time.unscaledTime < _later[i].at) continue;
                Queue(_later[i].text);
                _later.RemoveAt(i--);
            }
        }

        /// <summary>Queue behind whatever is being said.</summary>
        internal static void Queue(string text) => Say(text, interrupt: false);

        internal static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            text = Sprite.Replace(text, " ");
            text = Tags.Replace(text, "");
            text = text.Replace(" ", " ");
            text = text.Replace("New Text", ""); // unused placeholder labels left in the game's UI
            text = Spaces.Replace(text, " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\.\s*\.", ".");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"!\s*\.", "!");
            return text.Trim();
        }
    }
}
