using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// Lets the mod press the game's own buttons. The game reads the mouse and its
    /// keys through InputReader's static properties, so postfixing those getters
    /// makes a "virtual" left click or a held key look real to every scene script.
    /// </summary>
    internal static class VirtualInput
    {
        // Left mouse: a click is down on frame N, held for N, up on frame N+1.
        private static int _downFrame = -10;
        private static int _upFrame = -10;
        private static bool _held;

        // Held game keys (for the self-test harness and for mod features).
        internal static readonly HashSet<string> HeldKeys = new HashSet<string>();
        private static readonly Dictionary<string, int> KeyDownFrame = new Dictionary<string, int>();

        internal static void Click()
        {
            _held = true;
            _downFrame = Time.frameCount;
            _upFrame = -10;
        }

        internal static void Tick()
        {
            // Release a click one frame after it went down.
            if (_held && Time.frameCount > _downFrame)
            {
                _held = false;
                _upFrame = Time.frameCount;
            }
        }

        internal static void HoldKey(string key)
        {
            if (HeldKeys.Add(key))
                KeyDownFrame[key] = Time.frameCount;
        }

        internal static void ReleaseKey(string key) => HeldKeys.Remove(key);

        internal static bool KeyDownNow(string key)
            => KeyDownFrame.TryGetValue(key, out int f) && f == Time.frameCount;

        internal static bool LeftHeld => _held;
        internal static bool LeftDownNow => _downFrame == Time.frameCount;
        internal static bool LeftUpNow => _upFrame == Time.frameCount;

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.LeftMouse), MethodType.Getter)]
        private static class LeftMousePatch
        {
            private static void Postfix(ref bool __result) { if (LeftHeld) __result = true; }
        }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.LeftMouseDown), MethodType.Getter)]
        private static class LeftMouseDownPatch
        {
            private static void Postfix(ref bool __result) { if (LeftDownNow) __result = true; }
        }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.LeftMouseUp), MethodType.Getter)]
        private static class LeftMouseUpPatch
        {
            private static void Postfix(ref bool __result) { if (LeftUpNow) __result = true; }
        }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.LeftMouseClicked), MethodType.Getter)]
        private static class LeftMouseClickedPatch
        {
            private static void Postfix(ref bool __result) { if (LeftUpNow) __result = true; }
        }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.SewButton), MethodType.Getter)]
        private static class SewPatch
        {
            private static void Postfix(ref bool __result) { if (HeldKeys.Contains("sew")) __result = true; }
        }

        /// <summary>
        /// Outside the sewing machine the game only uses arrows/WASD to swing the camera,
        /// and the mod uses arrows for menus, so the game doesn't see them there.
        /// </summary>
        private static bool GameMayUseArrows()
        {
            var gm = Rooms.GameManagerOrNull();
            return gm == null || gm.CurrentScene == GameManager.Scene.Sewing;
        }

        private static void Arrow(ref bool r, string held, bool down)
        {
            if (!GameMayUseArrows()) r = false;
            if (down ? KeyDownNow(held) : HeldKeys.Contains(held)) r = true;
        }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.LeftKey), MethodType.Getter)]
        private static class LeftKeyPatch { private static void Postfix(ref bool __result) => Arrow(ref __result, "left", false); }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.RightKey), MethodType.Getter)]
        private static class RightKeyPatch { private static void Postfix(ref bool __result) => Arrow(ref __result, "right", false); }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.LeftKeyDown), MethodType.Getter)]
        private static class LeftKeyDownPatch { private static void Postfix(ref bool __result) => Arrow(ref __result, "left", true); }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.RightKeyDown), MethodType.Getter)]
        private static class RightKeyDownPatch { private static void Postfix(ref bool __result) => Arrow(ref __result, "right", true); }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.UpKey), MethodType.Getter)]
        private static class UpKeyPatch { private static void Postfix(ref bool __result) => Arrow(ref __result, "up", false); }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.DownKey), MethodType.Getter)]
        private static class DownKeyPatch { private static void Postfix(ref bool __result) => Arrow(ref __result, "down", false); }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.UpKeyDown), MethodType.Getter)]
        private static class UpKeyDownPatch { private static void Postfix(ref bool __result) => Arrow(ref __result, "up", true); }

        [HarmonyPatch(typeof(InputReader), nameof(InputReader.DownKeyDown), MethodType.Getter)]
        private static class DownKeyDownPatch { private static void Postfix(ref bool __result) => Arrow(ref __result, "down", true); }
    }
}
