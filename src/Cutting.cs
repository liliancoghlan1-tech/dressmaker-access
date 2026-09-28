using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Framework;
using HarmonyLib;
using SoundManager;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// The cutting table.
    ///  - Fabric in the sidebar: Enter lays it on the table (the game's own click).
    ///  - Pattern pieces in the sidebar: Enter lays the piece on the fabric in the first
    ///    free space from the left (as close to the used end as it fits).
    ///  - Pieces on the table are in the Tab list; Enter picks one up for the keys below.
    ///  - With a piece selected: arrows nudge it (Shift = further), Q/E turn it one notch
    ///    (the same 9 degrees as the mouse wheel) and say the grain, F moves it to the next
    ///    free space, C cuts it out, Backspace puts it back in the sidebar.
    ///  - X trims off the fabric you've used. F7 = what's on the table.
    /// </summary>
    internal static class Cutting
    {
        private const float Nudge = 0.01f;

        private static readonly AccessTools.FieldRef<CuttingRoom, GameObject> FabricObj =
            AccessTools.FieldRefAccess<CuttingRoom, GameObject>("fabric");
        private static readonly AccessTools.FieldRef<CuttingRoom, FabricUsageCounter> Usage =
            AccessTools.FieldRefAccess<CuttingRoom, FabricUsageCounter>("fabricUsageCounter");
        private static readonly AccessTools.FieldRef<CuttingRoom, FabricScissors> Scissors =
            AccessTools.FieldRefAccess<CuttingRoom, FabricScissors>("fabricScissors");
        private static readonly AccessTools.FieldRef<PatternPanel, Collider2D> PanelCollider =
            AccessTools.FieldRefAccess<PatternPanel, Collider2D>("_collider");
        private static readonly System.Reflection.MethodInfo Reorder = AccessTools.Method(typeof(CuttingRoom), "ReorderPanels");
        private static readonly System.Reflection.MethodInfo ReturnAnim = AccessTools.Method(typeof(CuttingRoom), "ReturnToInventoryAnimation");

        private static int _arrowFrame = -1;
        private static PatternPanel _describeAfter;
        private static int _describeFrame;
        private static PatternPanel _cutting;

        internal static CuttingRoom Room
        {
            get
            {
                var gm = Rooms.GameManagerOrNull();
                if (gm == null || gm.CurrentScene != GameManager.Scene.CuttingRoom)
                    return null;
                try { return SingletonBehaviour<CuttingRoom>.Instance; } catch { return null; }
            }
        }

        private static SharedPanelInteractor Interactor => SingletonBehaviour<SharedPanelInteractor>.Instance;

        private static IEnumerable<PatternPanel> Pieces
            => SingletonBehaviour<GameManager>.Instance.activeDress?.PanelPieces ?? new List<PatternPanel>();

        // ---------- labels / actions ----------

        internal static string Label(GameObject go)
        {
            var btn = go.GetComponent<OnMouseDownInventoryButton>();
            if (btn != null && btn.panel != null)
            {
                PatternPanel p = btn.panel;
                string bias = p.definition != null && p.definition.isBiasCut ? ", cut on the bias" : "";
                string where = Room != null ? ". Enter lays it on the fabric" : "";
                return $"Pattern piece: {p.DisplayName}{(p.HasBeenCut ? ", already cut" : "")}{bias}{where}";
            }
            return null;
        }

        internal static System.Action ActionFor(GameObject go)
        {
            CuttingRoom room = Room;
            if (room == null)
                return null;
            var btn = go.GetComponent<OnMouseDownInventoryButton>();
            if (btn != null && btn.panel != null)
                return () => Place(room, btn.panel);
            return null;
        }

        internal static List<UINav.Item> TableItems()
        {
            var list = new List<UINav.Item>();
            CuttingRoom room = Room;
            if (room == null)
                return list;
            Camera cam = Camera.main;
            foreach (PatternPanel p in Pieces)
            {
                if (!p.IsWorld3D)
                    continue;
                PatternPanel captured = p;
                Vector2 sp = cam != null ? (Vector2)cam.WorldToScreenPoint(p.PatternRenderer.bounds.center) : Vector2.zero;
                list.Add(new UINav.Item
                {
                    Go = p.WorldObj,
                    Screen = sp,
                    Label = "On the fabric: " + Describe(p),
                    OnActivate = () => Pick(captured),
                });
            }
            return list;
        }

        internal static string Describe(PatternPanel p)
        {
            var sb = new StringBuilder(p.DisplayName);
            if (p.HasBeenCut)
            {
                sb.Append($", cut, grain {Mathf.RoundToInt(p.GrainQuality)} percent");
                return sb.ToString();
            }
            sb.Append(", ").Append(GrainText(p));
            if (p.IsOverlapping)
                sb.Append(", overlapping something, can't cut");
            return sb.ToString();
        }

        private static string GrainText(PatternPanel p)
        {
            PatternPanel.Grain g = p.GetAlignment();
            string name;
            if (p.definition != null && p.definition.isBiasCut)
                name = g == PatternPanel.Grain.Bias ? "on the bias, the best" : "not on the bias yet";
            else
                name = g == PatternPanel.Grain.Straight ? "straight grain, the best" : g == PatternPanel.Grain.Cross ? "cross grain, second best" : "grain not lined up";
            return $"{name}, {Mathf.RoundToInt(p.CalculateGrainQuality())} percent";
        }

        // ---------- placing ----------

        private static void Place(CuttingRoom room, PatternPanel p)
        {
            if (!room.HasFabricPiece)
            {
                Speech.Say("Choose a fabric first: Tab to one in the sidebar's Fabric tab.");
                return;
            }
            p.ShowRepresentation3D(resetPositionToCenter: true, clearSelection: true, hideInv: false);
            p.InventoryButton.gameObject.SetActive(false);
            p.ShowWorldObj(show: true);
            // New pieces go to the far end of the unrolled fabric; slide them into place from there.
            bool fits = MoveToFarEnd(room, p);
            if (!fits)
            {
                // Long pieces can be taller than the fabric at the starting angle: try the other
                // quarter turns, best grain first.
                Transform tr = p.PatternRenderer.transform;
                int start = Mathf.RoundToInt(tr.rotation.eulerAngles.z / 9f);
                var turns = new List<(int notch, float q)>();
                foreach (int k in new[] { 10, 20, 30 })
                {
                    tr.rotation = Quaternion.Euler(0f, 0f, (start + k) * 9f);
                    turns.Add((start + k, p.CalculateGrainQuality()));
                }
                foreach (var (notch, _) in turns.OrderByDescending(x => x.q))
                {
                    tr.rotation = Quaternion.Euler(0f, 0f, notch * 9f);
                    if (MoveToFarEnd(room, p)) { fits = true; break; }
                }
                if (!fits) tr.rotation = Quaternion.Euler(0f, 0f, start * 9f);
                var knob = SingletonBehaviour<PatternRotationKnob>.Instance;
                if (knob != null) knob.transform.rotation = tr.rotation;
            }
            if (!fits)
            {
                room.StartCoroutine((IEnumerator)ReturnAnim.Invoke(room, new object[] { p }));
                Speech.Say($"There isn't room for {p.DisplayName} on the unrolled fabric at any angle, so it's back in the sidebar. " +
                           "Press X to trim off the fabric you've used and unroll more, or choose another fabric.");
                return;
            }
            Select(room, p);
            Speech.Say($"Laid {p.DisplayName} at the far end of the fabric. {GrainText(p)}. Q and E turn it, listen for the tones to come into tune. Arrows slide it until it bumps something. C cuts.");
        }

        private static void Pick(PatternPanel p)
        {
            CuttingRoom room = Room;
            if (room == null)
                return;
            Select(room, p);
            Speech.Say("Picked up " + Describe(p) + (p.HasBeenCut ? ". Backspace puts it back in the sidebar." : ". Q and E turn it, arrows slide it until it bumps, Shift with an arrow nudges, C cuts, Backspace puts it back."));
        }

        private static void Select(CuttingRoom room, PatternPanel p)
        {
            Transform t = p.PatternRenderer.transform;
            t.position = t.position.WithZ(0f);
            Interactor.ClearSelection();
            Interactor.Select(p, noDragOffset: true);
            p.lastPlacedOn = room.GetCurrentDisplayedFabricPiece();
            Reorder.Invoke(room, new object[] { p });
        }

        private static readonly List<Collider2D> _hits = new List<Collider2D>();

        private static bool Clear(PatternPanel p)
        {
            Physics2D.SyncTransforms();
            Collider2D c = PanelCollider(p);
            if (c == null)
                return true;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("Bounds", "Default", "Cutout") };
            _hits.Clear();
            c.Overlap(filter, _hits);
            return _hits.Count == 0;
        }

        /// <summary>Put the piece at the far (right) end of the unrolled fabric, the first clear spot from there.</summary>
        private static bool MoveToFarEnd(CuttingRoom room, PatternPanel p)
        {
            Transform t = p.PatternRenderer.transform;
            GameObject fabric = FabricObj(room);
            FabricUsageCounter usage = Usage(room);
            float startX = usage.FabricStartOffset, endX = startX + room.GetCurrentDisplayedFabricPieceRolloutLength();
            float midY = fabric.transform.position.y, half = room.fabricHeight / 2f;
            Vector3 original = t.position;
            Bounds b = p.PatternRenderer.bounds;
            Vector3 centreOffset = b.center - t.position;
            const float step = 0.02f;
            for (float x = endX - b.extents.x - 0.01f; x >= startX + b.extents.x - 0.001f; x -= step)
            {
                // Middle first, then outwards, so it starts somewhere neutral.
                for (int k = 0; k < 40; k++)
                {
                    float y = midY + ((k % 2 == 0) ? 1 : -1) * (k / 2) * step;
                    if (y + b.extents.y > midY + half || y - b.extents.y < midY - half) continue;
                    t.position = new Vector3(x, y, 0f) - centreOffset.WithZ(0f);
                    if (Clear(p))
                        return true;
                }
            }
            t.position = original;
            Physics2D.SyncTransforms();
            return false;
        }

        /// <summary>
        /// Slide the piece in a direction until it touches something (another piece, a cut-out,
        /// the edge of the fabric). Says what it bumped into.
        /// </summary>
        private static void Slide(CuttingRoom room, PatternPanel p, Vector3 dir)
        {
            Transform t = p.PatternRenderer.transform;
            const float step = 0.004f;
            if (!Clear(p) && !FreeUp(p))
            {
                Speech.Say("There's no room around it to move. " + Touching(p) + " Try turning it, or F finds space.");
                return;
            }
            int moved = 0;
            for (int i = 0; i < 1500; i++)
            {
                t.position += dir * step;
                if (!Clear(p))
                {
                    string what = Touching(p, dir);
                    t.position -= dir * step;
                    Physics2D.SyncTransforms();
                    if (moved == 0)
                    {
                        Speech.Say("Already against it. " + what);
                        return;
                    }
                    SingletonBehaviour<SoundController>.Instance.paperDrop.Play();
                    Speech.Say(what);
                    return;
                }
                moved++;
            }
            Speech.Say("Slid a long way without touching anything.");
        }

        /// <summary>If the piece overlaps something, move it the shortest distance that frees it.</summary>
        private static bool FreeUp(PatternPanel p)
        {
            if (Clear(p))
                return true;
            Transform t = p.PatternRenderer.transform;
            Vector3 start = t.position;
            const float step = 0.005f;
            for (int ring = 1; ring <= 80; ring++)
            {
                float r = ring * step;
                int n = 8 + ring * 2;
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2f / n;
                    t.position = start + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                    if (Clear(p))
                        return true;
                }
            }
            t.position = start;
            Physics2D.SyncTransforms();
            return false;
        }

        /// <summary>What the piece is overlapping right now, in words.</summary>
        private static string Touching(PatternPanel p) => Touching(p, Vector3.zero);

        private static string Touching(PatternPanel p, Vector3 dir)
        {
            var names = new List<string>();
            foreach (Collider2D c in _hits)
            {
                if (c == null) continue;
                string layer = LayerMask.LayerToName(c.gameObject.layer);
                PatternPanel other = c.GetComponentInParent<PatternPanel>();
                string n;
                if (other != null && other != p) n = other.DisplayName;
                else if (c.GetComponentInParent<CutoutPatternPiece>() != null) n = "a cut-out piece";
                else if (layer == "Bounds") n = dir == Vector3.zero ? "the edge of the fabric" : EdgeFor(dir);
                else
                {
                    n = "something";
                    Plugin.Log.LogInfo($"[touch] unknown collider {UINav.Path(c.gameObject)} layer={layer}");
                }
                if (!names.Contains(n)) names.Add(n);
            }
            if (names.Count > 1) names.Remove("a cut-out piece");
            return names.Count == 0 ? "" : "Touching " + string.Join(" and ", names) + ".";
        }

        private static string EdgeFor(Vector3 dir)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                return dir.x < 0 ? "the used end of the fabric" : "the end of the unrolled fabric";
            return dir.y > 0 ? "the top edge of the fabric" : "the bottom edge of the fabric";
        }

        private static string EdgeName(PatternPanel p, Collider2D c)
        {
            Vector3 d = c.bounds.center - p.PatternRenderer.bounds.center;
            if (Mathf.Abs(d.x) * 0.6f > Mathf.Abs(d.y))
                return d.x < 0 ? "the used end of the fabric" : "the end of the unrolled fabric";
            return d.y > 0 ? "the top edge of the fabric" : "the bottom edge of the fabric";
        }

        /// <summary>Scan the fabric left to right, top to bottom, for the first spot the piece fits.</summary>
        private static bool MoveToFreeSpot(CuttingRoom room, PatternPanel p, float fromX)
        {
            Transform t = p.PatternRenderer.transform;
            GameObject fabric = FabricObj(room);
            FabricUsageCounter usage = Usage(room);
            float startX = usage.FabricStartOffset, endX = startX + room.GetCurrentDisplayedFabricPieceRolloutLength();
            float midY = fabric.transform.position.y, half = room.fabricHeight / 2f;
            Vector3 original = t.position;
            Bounds b = p.PatternRenderer.bounds;
            Vector3 centreOffset = b.center - t.position;
            const float step = 0.02f;
            for (float x = Mathf.Max(startX + b.extents.x, fromX); x <= endX - b.extents.x + 0.001f; x += step)
            {
                for (float y = midY + half - b.extents.y; y >= midY - half + b.extents.y - 0.001f; y -= step)
                {
                    t.position = new Vector3(x, y, 0f) - centreOffset.WithZ(0f);
                    if (Clear(p))
                        return true;
                }
            }
            t.position = original;
            Physics2D.SyncTransforms();
            return false;
        }

        /// <summary>
        /// F: try every quarter/half turn that keeps the grain as good as it is now, and use
        /// whichever fits furthest to the left (so the least fabric is used).
        /// </summary>
        private static bool FitTightest(CuttingRoom room, PatternPanel p)
        {
            Transform t = p.PatternRenderer.transform;
            int now = Mathf.RoundToInt(t.rotation.eulerAngles.z / 9f);
            float quality = p.CalculateGrainQuality();
            float bestRight = float.MaxValue;
            Vector3 bestPos = t.position;
            int bestNotch = now;
            bool any = false;
            foreach (int k in new[] { 0, 20, 10, 30 })
            {
                int notch = now + k;
                t.rotation = Quaternion.Euler(0f, 0f, notch * 9f);
                if (p.CalculateGrainQuality() < quality - 0.5f)
                    continue;
                if (!MoveToFreeSpot(room, p, float.NegativeInfinity))
                    continue;
                float right = p.PatternRenderer.bounds.max.x;
                if (right < bestRight - 0.005f)
                {
                    bestRight = right;
                    bestPos = t.position;
                    bestNotch = notch;
                    any = true;
                }
            }
            t.rotation = Quaternion.Euler(0f, 0f, bestNotch * 9f);
            t.position = bestPos;
            Physics2D.SyncTransforms();
            var knob = SingletonBehaviour<PatternRotationKnob>.Instance;
            if (knob != null) knob.transform.rotation = t.rotation;
            return any;
        }

        // ---------- keys ----------

        internal static bool HandleKeys(bool shift)
        {
            CuttingRoom room = Room;
            if (room == null || Dialogue.Active)
                return false;

            if (Input.GetKeyDown(KeyCode.X)) { TrimUsed(room); return true; }
            if (shift && Input.GetKeyDown(KeyCode.Backspace)) { AllBack(room); return true; }

            PatternPanel p = Interactor.SelectedPanel;
            if (p == null || !p.IsWorld3D)
                return false;

            float d = Nudge;
            bool holdingUncut = !p.HasBeenCut && !p.beingCut;
            Vector3 move = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) move = Vector3.left * d;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) move = Vector3.right * d;
            else if (Input.GetKeyDown(KeyCode.UpArrow)) move = Vector3.up * d;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) move = Vector3.down * d;
            if (move != Vector3.zero && holdingUncut)
            {
                _arrowFrame = Time.frameCount; // stops the game panning the camera this frame
                if (shift)
                {
                    p.PatternRenderer.transform.position += move;   // a small nudge
                    DescribeSoon(p);
                }
                else
                    Slide(room, p, move.normalized);                 // slide until it bumps
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E))
            {
                if (p.HasBeenCut || p.beingCut) { Speech.Say("That piece is already cut."); return true; }
                Turn(p, (Input.GetKeyDown(KeyCode.E) ? -1 : 1) * (shift ? 10 : 1));
                return true;
            }
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (p.HasBeenCut) { Speech.Say("That piece is already cut."); return true; }
                bool ok = FitTightest(room, p);
                Speech.Say(ok ? "Moved to the tightest free space. " + GrainText(p) : "No free space big enough. Put it back with Backspace and press X to trim used fabric, or use another fabric.");
                return true;
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                if (p.HasBeenCut) { Speech.Say("That piece is already cut."); return true; }
                Straighten(p);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.C)) { Cut(room, p); return true; }
            if (Input.GetKeyDown(KeyCode.Backspace)) { PutBack(room, p); return true; }
            return false;
        }

        /// <summary>Test harness: the same actions the keys trigger.</summary>
        internal static void CmdKey(string key, bool shift)
        {
            CuttingRoom room = Room;
            if (room == null) return;
            if (key == "x") { TrimUsed(room); return; }
            if (key == "allback") { AllBack(room); return; }
            PatternPanel p = Interactor.SelectedPanel;
            if (p == null) { Plugin.Log.LogInfo("[cmd] no piece selected"); return; }
            float d = shift ? Nudge * 5f : Nudge;
            switch (key)
            {
                case "slideleft": Slide(room, p, Vector3.left); break;
                case "slideright": Slide(room, p, Vector3.right); break;
                case "slideup": Slide(room, p, Vector3.up); break;
                case "slidedown": Slide(room, p, Vector3.down); break;
                case "left": p.PatternRenderer.transform.position += Vector3.left * d; DescribeSoon(p); break;
                case "right": p.PatternRenderer.transform.position += Vector3.right * d; DescribeSoon(p); break;
                case "up": p.PatternRenderer.transform.position += Vector3.up * d; DescribeSoon(p); break;
                case "down": p.PatternRenderer.transform.position += Vector3.down * d; DescribeSoon(p); break;
                case "q": Turn(p, shift ? 10 : 1); break;
                case "e": Turn(p, shift ? -10 : -1); break;
                case "f":
                    bool ok = FitTightest(room, p);
                    Speech.Say(ok ? "Moved to the tightest free space. " + GrainText(p) : "No free space big enough.");
                    break;
                case "g": Straighten(p); break;
                case "c": Cut(room, p); break;
                case "back": PutBack(room, p); break;
            }
        }

        /// <summary>G: turn to whichever quarter turn gives the best grain.</summary>
        internal static void Straighten(PatternPanel p)
        {
            Transform t = p.PatternRenderer.transform;
            int start = Mathf.RoundToInt(t.rotation.eulerAngles.z / 9f);
            int best = start;
            float bestQ = -1f;
            for (int k = 0; k < 4; k++)
            {
                int notch = start + k * 10;
                t.rotation = Quaternion.Euler(0f, 0f, notch * 9f);
                float q = p.CalculateGrainQuality();
                if (q > bestQ + 0.5f) { bestQ = q; best = notch; }
            }
            // Round to the nearest whole quarter too, in case it started off-notch.
            for (int k = 0; k < 4; k++)
            {
                int notch = k * 10;
                t.rotation = Quaternion.Euler(0f, 0f, notch * 9f);
                float q = p.CalculateGrainQuality();
                if (q > bestQ + 0.5f) { bestQ = q; best = notch; }
            }
            t.rotation = Quaternion.Euler(0f, 0f, best * 9f);
            var knob = SingletonBehaviour<PatternRotationKnob>.Instance;
            if (knob != null) knob.transform.rotation = t.rotation;
            bool freed = Clear(p) || FreeUp(p);
            Speech.Say("Straightened. " + GrainText(p) + (freed ? "" : ". It's overlapping and there's no room nearby."));
            DescribeSoon(p, overlapOnly: true);
        }

        internal static void Turn(PatternPanel p, int dir)
        {
            Transform t = p.PatternRenderer.transform;
            Vector3 e = t.rotation.eulerAngles;
            int notch = Mathf.RoundToInt(e.z / 9f) + dir;
            t.rotation = Quaternion.Euler(0f, 0f, notch * 9f);
            var knob = SingletonBehaviour<PatternRotationKnob>.Instance;
            if (knob != null) knob.transform.rotation = t.rotation;
            GrainTuner.Play(p);
            if (!Clear(p))
            {
                if (FreeUp(p)) Speech.Queue("Shifted a little to fit.");
                else Speech.Queue("Overlapping, and no room nearby.");
            }
            string kind = p.GetAlignment().ToString();
            bool spotOn = p.CalculateGrainQuality() >= 99.5f;
            if (spotOn && !_lastSpotOn)
                Speech.Say("Spot on. " + GrainText(p));
            else if (kind != _lastGrainKind || (_lastSpotOn && !spotOn))
                Speech.Say(GrainText(p));
            _lastGrainKind = kind;
            _lastSpotOn = spotOn;
            DescribeSoon(p, overlapOnly: true);
        }

        private static bool _overlapOnly;
        private static string _lastGrainKind;
        private static bool _lastSpotOn;

        private static void DescribeSoon(PatternPanel p, bool overlapOnly = false)
        {
            _describeAfter = p;
            _describeFrame = Time.frameCount + 3; // IsOverlapping updates in the piece's own Update
            _overlapOnly = overlapOnly;
        }

        private static void Cut(CuttingRoom room, PatternPanel p)
        {
            if (p.HasBeenCut) { Speech.Say("Already cut."); return; }
            if (p.beingCut || Pieces.Any(x => x.beingCut)) { Speech.Say("Still cutting."); return; }
            Physics2D.SyncTransforms();
            if (p.IsOverlapping || !Clear(p))
            {
                Speech.Say("Can't cut: it's overlapping another piece or the edge of the fabric. Try F to find space.");
                return;
            }
            FabricScissors s = Scissors(room);
            if (s != null && s.gameObject.activeInHierarchy)
                s.ActivateScissors(activate: true);
            room.CutOutFabric(p);
            _cutting = p;
            Speech.Say("Cutting " + p.DisplayName + ".");
        }

        private static void PutBack(CuttingRoom room, PatternPanel p)
        {
            room.StartCoroutine((IEnumerator)ReturnAnim.Invoke(room, new object[] { p }));
            Speech.Say(p.DisplayName + " back in the sidebar.");
        }

        private static void AllBack(CuttingRoom room)
        {
            var cut = Pieces.Where(x => x.IsWorld3D && x.HasBeenCut && !x.beingCut).ToList();
            if (cut.Count == 0) { Speech.Say("No cut pieces on the table."); return; }
            foreach (PatternPanel p in cut)
                room.StartCoroutine((IEnumerator)ReturnAnim.Invoke(room, new object[] { p }));
            Speech.Say($"Put {cut.Count} cut {(cut.Count == 1 ? "piece" : "pieces")} back in the sidebar.");
        }

        private static void TrimUsed(CuttingRoom room)
        {
            if (!room.HasFabricPiece) { Speech.Say("No fabric on the table."); return; }
            FabricUsageCounter u = Usage(room);
            float used = u.UsedLength;
            if (used <= 0.01f) { Speech.Say("Nothing cut from this fabric yet, so nothing to trim."); return; }
            float cutX = u.ClampCutX(u.FabricStartOffset + used);
            FabricScissors s = Scissors(room);
            List<PatternPanel> blocking = s != null ? s.GetPanelsBlockingDiscard(cutX) : new List<PatternPanel>();
            if (blocking.Count > 0)
            {
                Speech.Say("Can't trim yet: uncut pieces are in the way: " + string.Join(", ", blocking.Select(b => b.DisplayName)) + ". Cut them or move them right.");
                return;
            }
            room.DiscardFabricSlice(cutX, fromBottom: false);
            Speech.Say($"Trimmed off {used:0.00} metres of used fabric.");
        }

        // ---------- per frame ----------

        internal static void Tick()
        {
            GrainTuner.Update(Room != null ? Interactor.SelectedPanel : null);
            if (_describeAfter != null && Time.frameCount >= _describeFrame)
            {
                PatternPanel p = _describeAfter;
                _describeAfter = null;
                if (p != null && p.IsWorld3D && !p.HasBeenCut)
                {
                    if (_overlapOnly)
                    {
                        if (p.IsOverlapping) Speech.Queue("Overlapping.");
                    }
                    else
                        Speech.Say(p.IsOverlapping ? "Overlapping." : "Clear.");
                }
            }
            if (_cutting != null && !_cutting.beingCut && _cutting.HasBeenCut)
            {
                CuttingRoom room = Room;
                PatternPanel p = _cutting;
                _cutting = null;
                if (room != null)
                {
                    FabricScissors s = Scissors(room);
                    if (s != null && s.Selected) s.ActivateScissors(activate: false);
                }
                int left = Pieces.Count(x => !x.HasBeenCut);
                string used = "";
                if (room != null)
                {
                    FabricPiece fp = room.GetCurrentDisplayedFabricPiece();
                    if (fp != null) used = $" Fabric used from this roll so far: {Usage(room).UsedLength:0.00} metres.";
                }
                Speech.Say($"Cut out {p.DisplayName}. Grain {Mathf.RoundToInt(p.GrainQuality)} percent." + used + " " +
                           (left == 0 ? "Every piece is cut! Next, press 6 for the mannequin to put the pieces on and sew them." : $"{left} {(left == 1 ? "piece" : "pieces")} left to cut."));
            }
        }

        internal static string Summary()
        {
            CuttingRoom room = Room;
            if (room == null)
                return "";
            var sb = new StringBuilder();
            FabricPiece fp = room.GetCurrentDisplayedFabricPiece();
            if (fp != null)
                sb.Append($"On the table: {fp.fabric.PrettyName}, {fp.length:0.00} metres. Used so far {Usage(room).UsedLength:0.00}. ");
            else
                sb.Append("No fabric on the table. ");
            var all = Pieces.ToList();
            int cut = all.Count(x => x.HasBeenCut), onTable = all.Count(x => x.IsWorld3D && !x.HasBeenCut);
            sb.Append($"{cut} of {all.Count} pieces cut, {onTable} laid out waiting. ");
            PatternPanel sel = Interactor.SelectedPanel;
            if (sel != null)
                sb.Append("Holding ").Append(Describe(sel)).Append(". ");
            return sb.ToString();
        }

        // ---------- hooks ----------

        [HarmonyPatch(typeof(CuttingRoom), nameof(CuttingRoom.PanLeft))]
        private static class PanLeftPatch { private static bool Prefix() => _arrowFrame != Time.frameCount; }

        [HarmonyPatch(typeof(CuttingRoom), nameof(CuttingRoom.PanRight))]
        private static class PanRightPatch { private static bool Prefix() => _arrowFrame != Time.frameCount; }

        [HarmonyPatch(typeof(CuttingRoom), nameof(CuttingRoom.UpdateDisplayedFabric))]
        private static class FabricShownPatch
        {
            private static void Postfix(CuttingRoom __instance)
            {
                FabricPiece fp = __instance.GetCurrentDisplayedFabricPiece();
                if (fp != null && Room != null)
                    Speech.Say($"On the table: {fp.fabric.PrettyName}, {fp.length:0.00} metres.");
            }
        }

        [HarmonyPatch(typeof(SidebarInventory), nameof(SidebarInventory.SetTab))]
        private static class TabPatch
        {
            private static void Postfix(SidebarInventory.Tab tab)
            {
                if (Room != null || Rooms.GameManagerOrNull()?.CurrentScene == GameManager.Scene.Mannequin)
                    Speech.Queue(tab + " tab.");
            }
        }
    }
}
