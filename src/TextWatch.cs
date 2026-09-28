using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Yarn.Unity;

namespace DressmakerAccess
{
    /// <summary>
    /// Everything the game shows as text outside the customer conversations:
    /// story/intro boxes, tutorial notes, notifications, popups, hover labels,
    /// friendship and prestige changes, unlocks, and the room you are in.
    /// </summary>
    internal static class TextWatch
    {
        private static readonly Dictionary<int, string> _spokenBoxes = new Dictionary<int, string>();
        private static float _nextScan;

        private static readonly HashSet<int> _openPopups = new HashSet<int>();

        private static void AnnouncePopups()
        {
            var now = new HashSet<int>();
            foreach (GameObject root in Labels.ModalRoots())
            {
                int id = root.GetInstanceID();
                now.Add(id);
                if (_openPopups.Contains(id))
                    continue;
                // Tips and the colour picker / garment list announce themselves already.
                if (root.GetComponent<TutorialMessage>() != null || root.GetComponent<ColorPicker>() != null
                    || root.GetComponent<GarmentComponentSelectionUI>() != null)
                    continue;
                var photo = root.GetComponentInParent<PhotoScene>();
                var gm = Rooms.GameManagerOrNull();
                if (photo != null && (gm == null || gm.CurrentScene != GameManager.Scene.Photo))
                    continue;
                var texts = new List<string>();
                foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (t.GetComponentInParent<UnityEngine.UI.Selectable>() != null) continue;
                    string s = Speech.Clean(t.text);
                    if (!string.IsNullOrEmpty(s) && !texts.Contains(s)) texts.Add(s);
                }
                int choices = root.GetComponentsInChildren<UnityEngine.UI.Selectable>(false).Length;
                if (texts.Count == 0)
                    continue;
                Speech.Queue(string.Join(". ", texts) + (choices > 0 ? $". {choices} {(choices == 1 ? "choice" : "choices")}, arrows to move, Enter to choose." : ""));
            }
            _openPopups.Clear();
            foreach (int id in now) _openPopups.Add(id);
        }

        private static bool _catcherAnnounced;

        /// <summary>The friendship/prestige display waiting for a click before showing unlocks.</summary>
        internal static UnityEngine.UI.Button WaitingCatcher()
        {
            foreach (RelationshipProgressDisplay d in Object.FindObjectsByType<RelationshipProgressDisplay>(FindObjectsSortMode.None))
            {
                var b = HarmonyLib.Traverse.Create(d).Field("clickCatcher").GetValue<UnityEngine.UI.Button>();
                if (b != null && b.enabled && b.gameObject.activeInHierarchy) return b;
            }
            foreach (PrestigeProgressDisplay d in Object.FindObjectsByType<PrestigeProgressDisplay>(FindObjectsSortMode.None))
            {
                var b = HarmonyLib.Traverse.Create(d).Field("clickCatcher").GetValue<UnityEngine.UI.Button>();
                if (b != null && b.enabled && b.gameObject.activeInHierarchy) return b;
            }
            return null;
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < _nextScan)
                return;
            _nextScan = Time.unscaledTime + 0.3f;
            AnnouncePopups();
            bool waiting = WaitingCatcher() != null;
            if (waiting && !_catcherAnnounced)
                Speech.Queue("You've unlocked something new! Press Enter to see it.");
            _catcherAnnounced = waiting;

            // TextBox = the game's big story/intro panels. They type out; read them whole.
            foreach (TextBox box in Object.FindObjectsByType<TextBox>(FindObjectsSortMode.None))
            {
                if (!box.isActiveAndEnabled)
                    continue;
                var tmp = Traverse.Create(box).Field("typewriterText").GetValue<TextMeshProUGUI>();
                if (tmp == null)
                    continue;
                string text = Speech.Clean(tmp.text);
                if (string.IsNullOrEmpty(text))
                    continue;
                int id = box.GetInstanceID();
                if (_spokenBoxes.TryGetValue(id, out string said) && said == text)
                    continue;
                _spokenBoxes[id] = text;
                Speech.Say(text + " Press Space to continue.");
            }
        }

        /// <summary>F4: read every piece of visible text on the screen, top to bottom.</summary>
        internal static void ReadScreen()
        {
            var found = new List<(Vector2 p, string s)>();
            foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled || t.alpha < 0.05f)
                    continue;
                string s = Speech.Clean(t.text);
                if (string.IsNullOrEmpty(s))
                    continue;
                if (t.maxVisibleCharacters == 0)
                    continue;
                Vector2 p;
                if (t is TextMeshProUGUI)
                {
                    Canvas c = t.canvas;
                    if (c == null || !c.isActiveAndEnabled)
                        continue;
                    bool hidden = t.GetComponentsInParent<CanvasGroup>().Any(g => g.alpha < 0.05f);
                    if (hidden)
                        continue;
                    Camera cam = c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
                    p = RectTransformUtility.WorldToScreenPoint(cam, t.transform.position);
                }
                else
                {
                    Camera cam = Camera.main;
                    if (cam == null)
                        continue;
                    Vector3 v = cam.WorldToScreenPoint(t.transform.position);
                    if (v.z < 0)
                        continue;
                    p = v;
                }
                if (p.x < 0 || p.y < 0 || p.x > Screen.width || p.y > Screen.height)
                    continue;
                found.Add((p, s));
            }
            var ordered = found.OrderByDescending(f => Mathf.Round(f.p.y / 30f)).ThenBy(f => f.p.x)
                .Select(f => f.s).Distinct().ToList();
            Speech.Say(ordered.Count == 0 ? "No text on screen." : string.Join(". ", ordered));
        }

        // ---------- hooks ----------

        [HarmonyPatch(typeof(TutorialMessage), nameof(TutorialMessage.Show))]
        private static class TutorialPatch
        {
            private static void Prefix(string message, bool modal)
            {
                Speech.Say("Tip: " + message + Hints.ForTip(message) + (modal ? " Press Enter to continue." : ""));
            }
        }

        [HarmonyPatch(typeof(NotificationHandler), nameof(NotificationHandler.ShowNotification))]
        private static class NotificationPatch
        {
            private static void Prefix(string text) => Speech.Queue(text);
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.ShowPopupLabel))]
        private static class PopupPatch
        {
            private static void Prefix(string text) => Speech.Queue(text);
        }

        // HoverLabel (the game's mouse-over name tags) is deliberately NOT spoken: it fires every
        // frame the real mouse rests on something, and it only repeats what the mod's own labels say.
        // Reading it caused ~10,000 repeats of piece names at the cutting table.

        [HarmonyPatch(typeof(GossipReportPresenter), nameof(GossipReportPresenter.RunLineAsync))]
        private static class GossipPatch
        {
            private static void Prefix(GossipReportPresenter __instance, LocalizedLine line)
            {
                if (__instance.gameObject.activeInHierarchy && line != null)
                    Speech.Queue(line.TextWithoutCharacterName.Text);
            }
        }

        [HarmonyPatch(typeof(RelationshipProgressDisplay), nameof(RelationshipProgressDisplay.PlayRoutine))]
        private static class RelationshipPatch
        {
            private static void Prefix(CharacterDefinition character, float oldValue, float newValue)
            {
                if (character == null)
                    return;
                int oldLevel = Mathf.FloorToInt(oldValue), newLevel = Mathf.FloorToInt(newValue);
                string s = newLevel > oldLevel
                    ? $"Friendship with {character.PrettyName} went up to {newLevel} {(newLevel == 1 ? "star" : "stars")}!"
                    : $"Friendship with {character.PrettyName} grew. {newLevel} {(newLevel == 1 ? "star" : "stars")}, {Mathf.RoundToInt((newValue - newLevel) * 100)} percent to the next.";
                Speech.Queue(s);
            }
        }

        [HarmonyPatch(typeof(PrestigeProgressDisplay), nameof(PrestigeProgressDisplay.PlayRoutine))]
        private static class PrestigePatch
        {
            private static void Prefix(float oldValue, float newValue)
            {
                int oldLevel = Mathf.FloorToInt(oldValue), newLevel = Mathf.FloorToInt(newValue);
                Speech.Queue(newLevel > oldLevel
                    ? $"Prestige went up to level {newLevel}!"
                    : $"Prestige grew. Level {newLevel}, {Mathf.RoundToInt((newValue - newLevel) * 100)} percent to the next.");
            }
        }

        [HarmonyPatch(typeof(UnlockFanfare), nameof(UnlockFanfare.CharacterPlayRoutine))]
        private static class PatternUnlockPatch
        {
            private static void Prefix(List<GarmentComponent> patterns)
            {
                if (patterns != null && patterns.Count > 0)
                    Speech.Queue("New patterns unlocked: " + string.Join(", ", patterns.Select(p => p.PrettyName)));
            }
        }

        [HarmonyPatch(typeof(UnlockFanfare), nameof(UnlockFanfare.PrestigePlayRoutine))]
        private static class ShopUnlockPatch
        {
            private static void Prefix(List<Fabric> fabrics, List<AccessoryDefinition> accessories)
            {
                var names = new List<string>();
                if (fabrics != null) names.AddRange(fabrics.Select(f => f.PrettyName));
                if (accessories != null) names.AddRange(accessories.Select(a => a.PrettyName));
                if (names.Count > 0)
                    Speech.Queue("New in the shop: " + string.Join(", ", names));
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SetScene))]
        private static class ScenePatch
        {
            private static void Postfix(GameManager __instance, GameManager.Scene newScene)
            {
                if (__instance.CurrentScene == newScene)
                    Rooms.OnSceneChanged(newScene);
            }
        }
    }
}
