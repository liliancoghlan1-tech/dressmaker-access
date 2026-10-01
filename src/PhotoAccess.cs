using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DressmakerAccess
{
    /// <summary>
    /// The photo studio's settings, which the game only numbers ("Environment 3/12", "Light Colour 2/9").
    /// The backdrops are described from screenshots of each one (two don't match their internal names:
    /// "Cherry Blossom Room" is a gothic hall). Light colours are named from their values.
    /// </summary>
    internal static class PhotoAccess
    {
        // Keyed by the backdrop object's name in the game, so a reorder can't mislabel them.
        private static readonly Dictionary<string, string> Backdrops = new Dictionary<string, string>
        {
            { "Backdrop Environment", "Green parlour: striped green wallpaper over wooden panelling, framed embroideries, a potted plant in a gold pot, a red patterned rug" },
            { "Backdrop Environment 04", "Library: tall shelves of red and brown books, a wooden ladder, trailing ivy, a red rug" },
            { "Backdrop Environment 03", "Plain grey studio backdrop" },
            { "Backdrop Environment 02", "Plum parlour: dusky purple patterned wallpaper over dark panelling, a candle sconce with red candles, gilt-framed paintings, a dark wooden floor" },
            { "Backdrop Environment 05", "Plain pink studio backdrop" },
            { "Backdrop Environment 06 Infinity Green", "Leafy bower: curtains of hanging vines with purple flowers, tree trunks, grass with yellow buttercups" },
            { "Backdrop Environment 07 Marble Stairs", "Marble staircase: a white marble hall with gold trim, two curving staircases with gold banisters, gold candle sconces, vases of pink blossom" },
            { "Backdrop Environment 08 Cherry Blossom Room", "Gothic hall: a tall stained-glass arched window in blue, orange and purple, candle sconces, dark stone pillars, a black and white chequered floor" },
            { "Backdrop Environment 09 Sunflowers", "Sunflower meadow: blue sky with clouds, golden hills, sunflowers and wild flowers in the grass" },
            { "Backdrop Environment 03 Infinity Curve Blue", "Plain deep blue studio backdrop" },
            { "Backdrop Environment 05 Infinity Yellow", "Plain golden yellow studio backdrop" },
            { "Backdrop Environment 10 Infinity Stars", "Starry night: black, scattered with twinkling stars" },
        };

        private static readonly Dictionary<string, string> Lights = new Dictionary<string, string>
        {
            { "FFF4E1", "warm white" },
            { "FDF1CD", "soft golden yellow" },
            { "F8DDF1", "soft pink" },
            { "B4C3E0", "cool blue, like moonlight" },
            { "CAE7D2", "pale mint green" },
            { "FFFFFF", "pure white" },
            { "F8D5D1", "blush rose" },
            { "F5D5AC", "peach" },
            { "BCA184", "dim amber, an old-photo sepia" },
        };

        private static readonly AccessTools.FieldRef<PhotoScene, int> EnvIndex = AccessTools.FieldRefAccess<PhotoScene, int>("_environmentIndex");
        private static readonly AccessTools.FieldRef<PhotoScene, int> LightIndex = AccessTools.FieldRefAccess<PhotoScene, int>("_lightColorIndex");

        internal static string Backdrop(PhotoScene ps)
        {
            int i = EnvIndex(ps);
            if (i < 0 || i >= ps.environments.Count) return "";
            string name = ps.environments[i].name;
            string d = Backdrops.TryGetValue(name, out string s) ? s : UINav.Prettify(name);
            return $"Backdrop {i + 1} of {ps.environments.Count}: {d}";
        }

        internal static string LightColour(PhotoScene ps)
        {
            int i = LightIndex(ps);
            if (i < 0 || i >= ps.lightColors.Count) return "";
            string hex = ColorUtility.ToHtmlStringRGB(ps.lightColors[i]);
            string d = Lights.TryGetValue(hex, out string s) ? s : "colour " + hex;
            return $"Light colour {i + 1} of {ps.lightColors.Count}: {d}";
        }

        internal static string Label(GameObject go)
        {
            PhotoScene ps = go.GetComponentInParent<PhotoScene>();
            if (ps == null || go.transform.parent == null)
                return null;
            string row = go.transform.parent.name;
            if (go.name == "Left" || go.name == "Right")
            {
                string dir = go.name == "Left" ? "Previous" : "Next";
                if (row == "Environment") return $"{dir} backdrop. Now {Backdrop(ps)}";
                if (row == "Light Color") return $"{dir} light colour. Now {LightColour(ps)}";
            }
            var s = go.GetComponent<Slider>();
            if (s != null)
            {
                string v = UINav.SliderValue(s);
                if (go.name == "YawSlider") return $"Light direction, round the dress from side to side: slider {v}";
                if (go.name == "PitchSlider") return $"Light height, from low to overhead: slider {v}";
                if (go.name == "IntensitySlider") return $"Light brightness: slider {v}";
            }
            return null;
        }
    }
}
