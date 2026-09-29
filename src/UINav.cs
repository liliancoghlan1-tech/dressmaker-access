using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DressmakerAccess
{
    /// <summary>
    /// Keyboard navigation over whatever the game has on screen. The game is
    /// mouse-only, so this finds every clickable UI element that is actually
    /// reachable right now (visible, interactable, not covered by a popup),
    /// orders it like reading a page, and clicks it with synthetic pointer events.
    /// </summary>
    internal static class UINav
    {
        internal class Item
        {
            public GameObject Go;
            public Selectable Sel;
            public Vector2 Screen;
            public string Label;
            public Action OnActivate;
        }

        /// <summary>Selectables inside these components are listed by a feature module instead.</summary>
        internal static Func<GameObject, bool> Exclude;

        private static List<Item> _items = new List<Item>();
        private static GameObject _current;
        private static Vector2 _currentScreen;

        /// <summary>Several items can share one object (e.g. pencil spots); pick by position too.</summary>
        private static int IndexOf(List<Item> items, GameObject go, Vector2 screen)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Go != go) continue;
                float d = (items[i].Screen - screen).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Extra elements feature modules want in the list (e.g. 3D buttons).</summary>
        internal static readonly List<Func<List<Item>>> ExtraSources = new List<Func<List<Item>>>();

        // ---------- gathering ----------

        internal static List<Item> Gather()
        {
            var list = new List<Item>();
            var seen = new HashSet<GameObject>();

            foreach (Selectable s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.gameObject.activeInHierarchy || !s.IsInteractable())
                    continue;
                if (s is Scrollbar)
                    continue;
                if (Exclude != null && Exclude(s.gameObject))
                    continue;
                if (Labels.Skip(s.gameObject))
                    continue;
                if (!Reachable(s.gameObject, out Vector2 p))
                    continue;
                if (seen.Add(s.gameObject))
                    list.Add(new Item { Go = s.gameObject, Sel = s, Screen = p });
            }

            // Game-specific clickables that are not Selectables (inventory items, pickers...).
            foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null || !mb.isActiveAndEnabled || mb is Selectable)
                    continue;
                if (!(mb is IPointerClickHandler) && !(mb is IPointerDownHandler))
                    continue;
                if (!(mb.transform is RectTransform))
                    continue;
                string asm = mb.GetType().Assembly.GetName().Name;
                bool gameType = asm == "Assembly-CSharp";
                bool trigger = mb is EventTrigger et && et.triggers.Any(t =>
                    t.eventID == EventTriggerType.PointerClick || t.eventID == EventTriggerType.PointerDown);
                if (!gameType && !trigger)
                    continue;
                GameObject go = mb.gameObject;
                // A Selectable on the same object already covers it; helpers like ButtonSounds
                // inside a button are not separate targets. Real click targets that merely sit
                // inside a big button (e.g. a colour grid inside a click-outside-to-close catcher) are.
                if (seen.Contains(go) || go.GetComponent<Selectable>() != null || Labels.Skip(go))
                    continue;
                if (go.GetComponentInParent<Selectable>() != null && !(mb is IPointerClickHandler) )
                    continue;
                if (Helper(mb))
                    continue;
                if (!Reachable(go, out Vector2 p))
                    continue;
                seen.Add(go);
                list.Add(new Item { Go = go, Screen = p });
            }

            foreach (var src in ExtraSources)
            {
                try { list.AddRange(src()); }
                catch (Exception e) { Plugin.Log.LogError("ExtraItems: " + e.Message); }
            }

            // While a popup is open, only its contents count.
            List<GameObject> roots = Labels.ModalRoots();
            for (int r = roots.Count - 1; r >= 0; r--)
            {
                Transform root = roots[r].transform;
                var inside = list.Where(i => i.Go.transform.IsChildOf(root)).ToList();
                if (inside.Count > 0)
                {
                    list = inside;
                    break;
                }
            }

            // Reading order: rows top to bottom (20px bands), then left to right.
            list = list.OrderByDescending(i => Mathf.Round(i.Screen.y / 20f)).ThenBy(i => i.Screen.x).ToList();
            foreach (Item i in list)
                if (i.Label == null)
                    i.Label = Describe(i);
            return list;
        }

        /// <summary>
        /// On screen, not hidden, not covered - or inside a scroll list whose visible window is,
        /// in which case it only needs scrolling to (done when it gets focus).
        /// </summary>
        private static bool Reachable(GameObject go, out Vector2 p)
        {
            bool onScreen = TryScreenPoint(go, out p);
            if (!Visible(go))
                return false;
            ScrollRect sr = go.GetComponentInParent<ScrollRect>();
            if (sr != null && sr.viewport != null && go.transform.IsChildOf(sr.content))
            {
                // The list itself must be showing and not covered by a popup.
                if (!TryScreenPoint(sr.viewport.gameObject, out Vector2 vp) || !Visible(sr.gameObject))
                    return false;
                if (InsideViewport(go, sr))
                    return onScreen && OnTop(go, p);
                return OnTop(sr.viewport.gameObject, vp) || OnTopWithin(sr.viewport.gameObject, vp);
            }
            return onScreen && OnTop(go, p);
        }

        private static bool OnTopWithin(GameObject viewport, Vector2 p)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return true;
            var ped = new PointerEventData(es) { position = p };
            _hits.Clear();
            es.RaycastAll(ped, _hits);
            return _hits.Count == 0 || _hits[0].gameObject.transform.IsChildOf(viewport.transform);
        }

        private static bool InsideViewport(GameObject go, ScrollRect sr)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return true;
            Vector3[] a = new Vector3[4], v = new Vector3[4];
            rt.GetWorldCorners(a);
            sr.viewport.GetWorldCorners(v);
            return a[0].y >= v[0].y - 0.5f && a[1].y <= v[1].y + 0.5f && a[0].x >= v[0].x - 0.5f && a[2].x <= v[2].x + 0.5f;
        }

        /// <summary>Scroll the list so this item is fully in view.</summary>
        internal static void ScrollIntoView(GameObject go)
        {
            ScrollRect sr = go.GetComponentInParent<ScrollRect>();
            if (sr == null || sr.viewport == null || sr.content == null || !go.transform.IsChildOf(sr.content))
                return;
            var rt = go.transform as RectTransform;
            if (rt == null) return;
            Vector3[] a = new Vector3[4], v = new Vector3[4];
            rt.GetWorldCorners(a);
            sr.viewport.GetWorldCorners(v);
            Vector3 shift = Vector3.zero;
            if (sr.vertical)
            {
                if (a[0].y < v[0].y) shift.y = v[0].y - a[0].y;          // below: move content up
                else if (a[1].y > v[1].y) shift.y = v[1].y - a[1].y;     // above: move content down
            }
            if (sr.horizontal)
            {
                if (a[2].x > v[2].x) shift.x = v[2].x - a[2].x;
                else if (a[0].x < v[0].x) shift.x = v[0].x - a[0].x;
            }
            if (shift == Vector3.zero) return;
            sr.StopMovement();
            sr.content.position += shift;
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Components that react to clicks but are not things you'd choose (sounds, flashes).</summary>
        private static bool Helper(MonoBehaviour mb)
        {
            string n = mb.GetType().Name;
            return n == "ButtonSounds" || n == "FlashButton" || n == "HoverTrigger" || n == "HoverTriggerAccessory";
        }

        private static bool TryScreenPoint(GameObject go, out Vector2 p)
        {
            p = default;
            var rt = go.transform as RectTransform;
            if (rt == null)
                return false;
            Canvas canvas = go.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled)
                return false;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 centre = (corners[0] + corners[2]) / 2f;
            p = RectTransformUtility.WorldToScreenPoint(cam, centre);
            // Size on screen, in pixels (world-space canvases are tiny in world units).
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            if (Mathf.Abs(b.x - a.x) < 2f && Mathf.Abs(b.y - a.y) < 2f)
                return false;
            return p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height;
        }

        private static bool Visible(GameObject go)
        {
            foreach (CanvasGroup cg in go.GetComponentsInParent<CanvasGroup>())
            {
                if (cg.alpha < 0.05f)
                    return false;
                if (!cg.interactable || !cg.blocksRaycasts)
                    return false;
                if (cg.ignoreParentGroups)
                    break;
            }
            return true;
        }

        private static readonly List<RaycastResult> _hits = new List<RaycastResult>();

        /// <summary>Would a real click at this point land on this element?</summary>
        private static bool OnTop(GameObject go, Vector2 p)
        {
            EventSystem es = EventSystem.current;
            if (es == null)
                return true;
            var ped = new PointerEventData(es) { position = p };
            _hits.Clear();
            es.RaycastAll(ped, _hits);
            if (_hits.Count == 0)
                return true; // nothing reports a hit (e.g. no raycaster) - don't hide it
            GameObject top = _hits[0].gameObject;
            return top == go || top.transform.IsChildOf(go.transform) || go.transform.IsChildOf(top.transform);
        }

        // ---------- labels ----------

        private static readonly Regex Camel = new Regex("(?<=[a-z])(?=[A-Z])");

        internal static string Describe(Item i)
        {
            GameObject go = i.Go;
            string custom = Labels.For(go);
            if (custom != null)
                return custom;
            string text = TextOf(go);
            if (string.IsNullOrEmpty(text))
                text = Prettify(go.name);

            var sb = new StringBuilder(text);
            switch (i.Sel)
            {
                case Toggle t:
                    sb.Append(t.isOn ? ", checked" : ", not checked");
                    break;
                case Slider s:
                    if (text == Prettify(go.name) && go.GetComponentInParent<ShopItemInfo>() != null)
                        sb.Clear().Append("Amount");
                    sb.Append(", slider ").Append(SliderValue(s));
                    break;
                case TMP_InputField f:
                    sb.Append(", edit box").Append(string.IsNullOrEmpty(f.text) ? ", empty" : ", " + f.text);
                    break;
                case TMP_Dropdown d:
                    if (d.options.Count > d.value && d.value >= 0)
                        sb.Append(", ").Append(d.options[d.value].text).Append(", dropdown");
                    break;
            }
            return sb.ToString();
        }

        internal static string TextOf(GameObject go)
        {
            var parts = new List<string>();
            foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(false))
            {
                string s = Speech.Clean(t.text);
                if (!string.IsNullOrEmpty(s) && !parts.Contains(s))
                    parts.Add(s);
            }
            foreach (Text t in go.GetComponentsInChildren<Text>(false))
            {
                string s = Speech.Clean(t.text);
                if (!string.IsNullOrEmpty(s) && !parts.Contains(s))
                    parts.Add(s);
            }
            return string.Join(", ", parts);
        }

        internal static string Prettify(string name)
        {
            name = name.Replace("(Clone)", "").Replace("_", " ").Trim();
            name = Regex.Replace(name, "\\(\\d+\\)", "").Trim();
            name = Camel.Replace(name, " ");
            name = Regex.Replace(name, "\\b(Btn|Button)\\b", "", RegexOptions.IgnoreCase).Trim();
            return name.Length == 0 ? "button" : name;
        }

        internal static string SliderValue(Slider s)
        {
            float range = s.maxValue - s.minValue;
            if (range <= 0)
                return s.value.ToString("0");
            if (s.wholeNumbers && range <= 20)
                return s.value.ToString("0");
            return Mathf.RoundToInt((s.value - s.minValue) / range * 100f) + " percent";
        }

        // ---------- navigation ----------

        internal static void Move(int dir)
        {
            ShopAccess.ShelfFocused = false;
            _items = Gather();
            if (_items.Count == 0)
            {
                Speech.Say("Nothing to select here.");
                return;
            }
            int idx = _current == null ? -1 : IndexOf(_items, _current, _currentScreen);
            if (idx < 0)
                idx = dir > 0 ? 0 : _items.Count - 1;
            else
                idx = (idx + dir + _items.Count) % _items.Count;
            Focus(_items[idx], idx);
        }

        /// <summary>Test harness: focus the first item whose label contains text.</summary>
        internal static void FocusMatching(string text)
        {
            _items = Gather();
            ShopAccess.ShelfFocused = false;
            string t = text.ToLowerInvariant();
            int i = _items.FindIndex(x => x.Label.ToLowerInvariant().StartsWith(t));
            if (i < 0) i = _items.FindIndex(x => x.Label.ToLowerInvariant().Contains(t));
            if (i >= 0) Focus(_items[i], i);
            else Plugin.Log.LogInfo("[cmd] nothing to focus matching " + text);
        }

        internal static void ResetFocus()
        {
            _current = null;
        }

        internal static void First()
        {
            _items = Gather();
            if (_items.Count == 0)
                return;
            Focus(_items[0], 0);
        }

        private static void Focus(Item it, int idx)
        {
            ScrollIntoView(it.Go);
            TryScreenPoint(it.Go, out Vector2 after);
            if (it.OnActivate == null) it.Screen = after;
            _current = it.Go;
            _currentScreen = it.Screen;
            Hover(it.Go);
            Speech.Say($"{it.Label}. {idx + 1} of {_items.Count}");
        }

        internal static GameObject Current => _current != null && _current.activeInHierarchy ? _current : null;

        internal static void ActivateCurrent()
        {
            GameObject go = Current;
            if (go == null)
            {
                Move(1);
                return;
            }
            _items = Gather();
            int ix = IndexOf(_items, go, _currentScreen);
            Item it = ix >= 0 ? _items[ix] : null;
            if (it != null && it.OnActivate != null)
            {
                Plugin.Log.LogInfo("[click] " + it.Label);
                it.OnActivate();
                return;
            }
            Activate(go);
        }

        /// <summary>Activate with any game-specific action (used by Enter and the test harness).</summary>
        internal static void ActivateItem(Item it)
        {
            if (it.OnActivate != null) { it.OnActivate(); return; }
            Activate(it.Go);
        }

        internal static void Activate(GameObject go)
        {
            System.Action custom = Labels.ActionFor(go);
            if (custom != null)
            {
                Plugin.Log.LogInfo("[action] " + go.name);
                custom();
                return;
            }
            var field = go.GetComponent<TMP_InputField>();
            if (field != null)
            {
                EventSystem.current?.SetSelectedGameObject(go);
                field.ActivateInputField();
                Speech.Say("Editing. Type, then press Enter.");
                return;
            }
            Plugin.Log.LogInfo("[click] " + go.name);
            string before = Describe(new Item { Go = go, Sel = go.GetComponent<Selectable>() });
            Click(go);
            Plugin.Instance.StartCoroutine(ReannounceIfChanged(go, before));
            // Announce the new state of a toggle once the click has landed.
            var t = go.GetComponent<Toggle>();
            if (t != null)
                Speech.Say(t.isOn ? "checked" : "not checked");
        }

        /// <summary>Send the same pointer events a real left click would.</summary>
        internal static void Click(GameObject go)
        {
            EventSystem es = EventSystem.current;
            if (es == null)
                return;
            ScrollIntoView(go);
            TryScreenPoint(go, out Vector2 p);
            var ped = new PointerEventData(es)
            {
                button = PointerEventData.InputButton.Left,
                position = p,
                pressPosition = p,
                clickCount = 1,
                eligibleForClick = true,
                pointerPress = go,
                rawPointerPress = go,
                pointerEnter = go,
            };
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerDownHandler);
            if (IsHoldButton(go))
            {
                // Buttons that act while held (the photo camera controls): hold for a moment.
                Plugin.Instance.StartCoroutine(ReleaseLater(go, ped, 0.5f));
                return;
            }
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerClickHandler);
        }

        private static System.Collections.IEnumerator ReannounceIfChanged(GameObject go, string before)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            if (go == null || !go.activeInHierarchy || Current != go)
                yield break;
            string after = Describe(new Item { Go = go, Sel = go.GetComponent<Selectable>() });
            if (after != before && go.GetComponent<Toggle>() == null)
                Speech.Say(after);
        }

        private static bool IsHoldButton(GameObject go)
        {
            var et = go.GetComponent<EventTrigger>();
            return et != null && et.triggers.Any(t => t.eventID == EventTriggerType.PointerDown)
                              && et.triggers.Any(t => t.eventID == EventTriggerType.PointerUp);
        }

        private static System.Collections.IEnumerator ReleaseLater(GameObject go, PointerEventData ped, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (go != null)
            {
                ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerClickHandler);
            }
        }

        private static GameObject _hovered;

        /// <summary>Tell the element the pointer is over it, so the game shows its hover label/state.</summary>
        private static void Hover(GameObject go)
        {
            EventSystem es = EventSystem.current;
            if (es == null)
                return;
            TryScreenPoint(go, out Vector2 p);
            var ped = new PointerEventData(es) { position = p };
            if (_hovered != null && _hovered != go)
                ExecuteEvents.ExecuteHierarchy(_hovered, ped, ExecuteEvents.pointerExitHandler);
            if (_hovered != go)
                ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerEnterHandler);
            _hovered = go;
        }

        internal static bool AdjustCurrent(int dir)
        {
            GameObject go = Current;
            if (go == null)
                return false;
            var s = go.GetComponent<Slider>();
            if (s != null)
            {
                float step = s.wholeNumbers ? 1f : (s.maxValue - s.minValue) / 20f;
                s.value = Mathf.Clamp(s.value + dir * step, s.minValue, s.maxValue);
                Speech.Say(SliderValue(s));
                return true;
            }
            var d = go.GetComponent<TMP_Dropdown>();
            if (d != null && d.options.Count > 0)
            {
                d.value = (d.value + dir + d.options.Count) % d.options.Count;
                Speech.Say(d.options[d.value].text);
                return true;
            }
            return false;
        }

        internal static bool EditingText
        {
            get
            {
                GameObject sel = EventSystem.current?.currentSelectedGameObject;
                if (sel == null)
                    return false;
                var f = sel.GetComponent<TMP_InputField>();
                return f != null && f.isFocused;
            }
        }

        /// <summary>F8 diagnostic: write everything the navigator sees to the log.</summary>
        internal static void Dump()
        {
            var items = Gather();
            Plugin.Log.LogInfo($"[dump] {items.Count} items, scene={Rooms.SceneName()}");
            foreach (Item i in items)
                Plugin.Log.LogInfo($"[dump]  {Path(i.Go)} @({i.Screen.x:0},{i.Screen.y:0}) '{i.Label}'");
        }

        /// <summary>Every Selectable and which test it fails - for finding over-eager filters.</summary>
        internal static void DumpAll()
        {
            foreach (Selectable s in Selectable.allSelectablesArray)
            {
                if (s == null) continue;
                string why = !s.gameObject.activeInHierarchy ? "inactive"
                    : !s.IsInteractable() ? "not interactable"
                    : !TryScreenPoint(s.gameObject, out Vector2 p) ? "offscreen/nocanvas"
                    : !Visible(s.gameObject) ? "canvasgroup hidden"
                    : !OnTop(s.gameObject, p) ? "covered by " + (_hits.Count > 0 ? Path(_hits[0].gameObject) : "?")
                    : "OK";
                TryScreenPoint(s.gameObject, out Vector2 q);
                Plugin.Log.LogInfo($"[dumpall] {Path(s.gameObject)} @({q.x:0},{q.y:0}) {why}");
            }
            foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null || mb is Selectable || !(mb is IPointerClickHandler || mb is IPointerDownHandler))
                    continue;
                if (mb.GetType().Assembly.GetName().Name != "Assembly-CSharp")
                    continue;
                GameObject go = mb.gameObject;
                string why = !mb.isActiveAndEnabled ? "inactive"
                    : !(mb.transform is RectTransform) ? "not UI"
                    : go.GetComponentInParent<Selectable>() != null ? "inside selectable"
                    : !TryScreenPoint(go, out Vector2 p) ? "offscreen/nocanvas"
                    : !Visible(go) ? "canvasgroup hidden"
                    : !OnTop(go, p) ? "covered by " + (_hits.Count > 0 ? Path(_hits[0].gameObject) : "?")
                    : "OK";
                if (why == "inactive") continue;
                TryScreenPoint(go, out Vector2 q);
                Plugin.Log.LogInfo($"[dumpall] ({mb.GetType().Name}) {Path(go)} @({q.x:0},{q.y:0}) {why}");
            }
        }

        internal static string Path(GameObject go)
        {
            var parts = new List<string>();
            for (Transform t = go.transform; t != null && parts.Count < 5; t = t.parent)
                parts.Insert(0, t.name);
            return string.Join("/", parts);
        }
    }
}
