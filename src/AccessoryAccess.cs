using System.Collections.Generic;
using System.Linq;
using Framework;
using HarmonyLib;
using SoundManager;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// Accessories on the mannequin (Accessories tab of the sidebar).
    ///  - Each accessory in the sidebar says what it is, how many you have and its styles.
    ///  - Enter picks it up (the game's own pick-up). The list then becomes places to put it:
    ///      buttons, bows etc.: spots on every piece that's on the body ("Bodice Front, top, your left");
    ///      trims (ribbon, lace...): routes along the dress's seams, as far as you have trim for.
    ///    plus "Put it back".
    ///  - After placing a single item it stays selected: the game's size and turn buttons are in
    ///    the list, plus "Done" and "Remove it".
    ///  - Accessories already on the dress are listed too; Enter selects one to adjust or remove.
    /// Where things go is a design choice: the client's wishes only count how many and what kind.
    /// </summary>
    internal static class AccessoryAccess
    {
        private static readonly AccessTools.FieldRef<AccessoryPlacementController, AccessoryItem> Selected =
            AccessTools.FieldRefAccess<AccessoryPlacementController, AccessoryItem>("selectedItem");
        private static readonly AccessTools.FieldRef<AccessoryPlacementController, bool> OnSurface =
            AccessTools.FieldRefAccess<AccessoryPlacementController, bool>("onSurface");
        private static readonly AccessTools.FieldRef<AccessoryPlacementController, AccessoryPlacementController.PlacementMode> ModeField =
            AccessTools.FieldRefAccess<AccessoryPlacementController, AccessoryPlacementController.PlacementMode>("mode");
        private static readonly System.Reflection.MethodInfo Consume = AccessTools.Method(typeof(AccessoryPlacementController), "ConsumeFromInventory");
        private static readonly System.Reflection.MethodInfo SelectItemM = AccessTools.Method(typeof(AccessoryPlacementController), "SelectItem");
        private static readonly System.Reflection.MethodInfo DeselectM = AccessTools.Method(typeof(AccessoryPlacementController), "DeselectItem");
        private static readonly System.Reflection.MethodInfo DeleteSimple = AccessTools.Method(typeof(AccessoryPlacementController), "DeleteSimpleItem");

        private static AccessoryPlacementController Controller
        {
            get
            {
                MannequinScene ms = MannequinAccess.Scene;
                return ms != null ? ms.AccessoryPlacementController : null;
            }
        }

        private static Mannequin Body => MannequinAccess.Scene?.mannequin;

        internal static bool Placing
        {
            get
            {
                var c = Controller;
                return c != null && (c.Mode == AccessoryPlacementController.PlacementMode.Simple || c.Mode == AccessoryPlacementController.PlacementMode.Path);
            }
        }

        private static GameObject _menu;
        private static GameObject Menu
        {
            get
            {
                if (_menu == null)
                {
                    _menu = new GameObject("DressmakerAccess_AccessoryMenu");
                    Object.DontDestroyOnLoad(_menu);
                }
                return _menu;
            }
        }

        private static PatternPanel _piece;   // chosen piece while placing a single item

        /// <summary>While something is held, only the placing choices count (a popup).</summary>
        internal static GameObject ModalRoot => Placing ? Menu : null;

        // ---------- labels ----------

        internal static string Label(GameObject go)
        {
            var b = go.GetComponent<AccessoryInventoryButton>();
            if (b == null || b.accessory == null)
                return null;
            AccessoryDefinition a = b.accessory;
            float have = PlayerProgress.Current?.AccessoryCount(a) ?? 0f;
            bool trim = a.prefab is AccessoryItemPath;
            string amount = trim ? $"{have:0.##} metres" : $"{Mathf.RoundToInt(have)} left";
            string kind = a.accessoryType != null ? a.accessoryType.PrettyName + ", " : "";
            string styles = ShopAccess.Styles(a.tagWeights, 3);
            string where = MannequinAccess.Scene != null ? ". Enter picks it up" : "";
            return $"{a.PrettyName}, {kind}{amount}{(styles.Length > 0 ? ". " + styles : "")}{where}";
        }

        // ---------- the list while holding / after placing ----------

        internal static List<UINav.Item> Items()
        {
            var list = new List<UINav.Item>();
            var c = Controller;
            Mannequin body = Body;
            if (c == null || body == null || SingletonBehaviour<SidebarInventory>.Instance.CurrentTab != SidebarInventory.Tab.Accessory)
                return list;
            int order = 0;
            UINav.Item Add(string label, System.Action act)
            {
                var it = new UINav.Item { Go = Menu, Screen = new Vector2(order++, 20000), Label = label, OnActivate = act };
                list.Add(it);
                return it;
            }

            if (c.Mode == AccessoryPlacementController.PlacementMode.Simple)
            {
                if (_piece == null || !_piece.VisibleOnMannequin)
                {
                    // Step 1: which piece of the dress.
                    foreach (PatternPanel p in Panels().OrderBy(PieceOrder))
                    {
                        var captured = p;
                        int n = Spots(body, p).Count;
                        if (n == 0) continue;
                        Add($"{p.DisplayName}, {n} {(n == 1 ? "spot" : "spots")}", () =>
                        {
                            _piece = captured;
                            UINav.ResetFocus();
                            Speech.Say($"{captured.DisplayName}. Arrows go through its spots; Enter puts it there.");
                        });
                    }
                }
                else
                {
                    // Step 2: where on that piece.
                    foreach (var spot in Spots(body, _piece))
                    {
                        var s = spot;
                        Add("Put it " + s.name, () => { PlaceSimple(c, body, s); _piece = null; });
                    }
                    Add("Back to the list of pieces", () => { _piece = null; UINav.ResetFocus(); Speech.Say("Pieces."); });
                }
                Add("Put it back", () => { _piece = null; c.CancelPlacement(); Speech.Say("Put back."); });
            }
            else if (c.Mode == AccessoryPlacementController.PlacementMode.Path)
            {
                foreach (var route in Routes(body))
                {
                    var r = route;
                    Add($"Run it along {r.name}, {r.length:0.00} metres", () => PlacePath(c, body, r));
                }
                Add("Put it back", () => { c.CancelPlacement(); Speech.Say("Put back."); });
            }
            else if (c.Mode == AccessoryPlacementController.PlacementMode.Selected && Selected(c) != null)
            {
                AccessoryItem sel = Selected(c);
                Add("Done with " + NameOf(sel), () => { DeselectM.Invoke(c, null); ModeField(c) = AccessoryPlacementController.PlacementMode.None; Speech.Say("Done."); });
                if (!(sel is AccessoryItemPath))
                    Add("Remove " + NameOf(sel), () =>
                    {
                        DeselectM.Invoke(c, null);
                        DeleteSimple.Invoke(c, new object[] { sel });
                        ModeField(c) = AccessoryPlacementController.PlacementMode.None;
                        Speech.Say("Removed and back in the sidebar.");
                    });
            }
            else
            {
                // Nothing held: list what's already on the dress so it can be adjusted or removed.
                Dress dress = SingletonBehaviour<GameManager>.Instance.activeDress;
                if (dress != null)
                    foreach (AccessoryItem ai in body.GetComponentsInChildren<AccessoryItem>())
                    {
                        if (ai == null || ai.state == null) continue;
                        var item = ai;
                        Add($"On the dress: {NameOf(ai)}{WhereIs(body, ai)}. Enter selects it", () =>
                        {
                            SelectItemM.Invoke(c, new object[] { item });
                            Speech.Say($"Selected {NameOf(item)}. Its size and turn buttons are in the list, then Done.");
                        });
                    }
            }
            return list;
        }

        private static string NameOf(AccessoryItem ai) => ai?.state?.definition != null ? ai.state.definition.PrettyName : "accessory";

        private static string WhereIs(Mannequin body, AccessoryItem ai)
        {
            PatternPanel best = null;
            float bd = float.MaxValue;
            foreach (PatternPanel p in Panels())
            {
                var col = p.MannequinObj.GetComponent<Collider>();
                if (col == null) continue;
                float d = Vector3.Distance(col.ClosestPoint(ai.transform.position), ai.transform.position);
                if (d < bd) { bd = d; best = p; }
            }
            return best != null ? ", on " + best.DisplayName : "";
        }

        private static IEnumerable<PatternPanel> Panels()
        {
            Dress dress = SingletonBehaviour<GameManager>.Instance.activeDress;
            if (dress == null) yield break;
            foreach (PatternPanel p in dress.PanelPieces)
                if (p != null && p.VisibleOnMannequin && p.MannequinObj != null)
                    yield return p;
        }

        // ---------- spots for single items ----------

        internal struct Spot { public string name; public Vector3 pos; public Vector3 normal; }

        private static int PieceOrder(PatternPanel p)
        {
            string n = p.DisplayName.ToLowerInvariant();
            int part = n.Contains("bodice") ? 0 : n.Contains("collar") ? 1 : n.Contains("sleeve") ? 2 : n.Contains("skirt") ? 3 : 4;
            int side = n.Contains("front") ? 0 : n.Contains("back") ? 1 : 2;
            return part * 10 + side;
        }

        private static List<Spot> Spots(Mannequin body, PatternPanel only)
        {
            var spots = new List<Spot>();
            Camera cam = SingletonBehaviour<GameManager>.Instance.mainCamera;
            Vector3 axis = body.transform.position;
            foreach (PatternPanel p in Panels())
            {
                if (only != null && p != only) continue;
                Collider col = p.MannequinObj.GetComponent<Collider>();
                Renderer ren = p.MannequinObj.GetComponent<Renderer>();
                if (col == null || ren == null) continue;
                Bounds b = ren.bounds;
                foreach (var (vy, vname) in new[] { (0.8f, "top"), (0.5f, "middle"), (0.2f, "bottom") })
                {
                    foreach (var (hx, hname) in new[] { (0.5f, "") , (0.2f, "L"), (0.8f, "R") })
                    {
                        Vector3 target = new Vector3(Mathf.Lerp(b.min.x, b.max.x, hx), Mathf.Lerp(b.min.y, b.max.y, vy), Mathf.Lerp(b.min.z, b.max.z, 0.5f));
                        Vector3 outward = (target - new Vector3(axis.x, target.y, axis.z));
                        outward.y = 0f;
                        if (outward.sqrMagnitude < 1e-6f) outward = -cam.transform.forward;
                        outward.Normalize();
                        var ray = new Ray(target + outward * 0.8f, -outward);
                        if (!col.Raycast(ray, out RaycastHit hit, 2f)) continue;
                        // Skip near-duplicates on narrow pieces.
                        if (spots.Any(s => Vector3.Distance(s.pos, hit.point) < 0.03f)) continue;
                        string side = hname == "" ? "centre" : SideWord(cam, hit.point, body);
                        string label = $"{vname}, {side}";
                        if (spots.Any(s => s.name == label)) continue;
                        spots.Add(new Spot { name = label, pos = hit.point, normal = hit.normal });
                    }
                }
            }
            return spots;
        }

        private static string SideWord(Camera cam, Vector3 p, Mannequin body)
        {
            float x = cam.transform.InverseTransformPoint(p).x - cam.transform.InverseTransformPoint(body.transform.position).x;
            return x < 0 ? "your left" : "your right";
        }

        private static void PlaceSimple(AccessoryPlacementController c, Mannequin body, Spot s)
        {
            AccessoryItem item = Selected(c);
            if (item == null) return;
            item.transform.parent = null;
            item.transform.position = s.pos;
            item.transform.forward = s.normal;
            item.SetSurfacePosition(body.transform.InverseTransformPoint(s.pos),
                Quaternion.Inverse(body.transform.rotation) * item.transform.rotation,
                body.transform.InverseTransformDirection(s.normal), body);
            item.state.normalOffset = 0f;
            OnSurface(c) = true;
            item.transform.parent = body.transform;
            Consume.Invoke(c, new object[] { item, 1f });
            var sounds = SingletonBehaviour<SoundController>.Instance;
            if (item.state.definition.accessoryType != null && item.state.definition.accessoryType.hard) sounds.hardAccessoryPlace.Play();
            else sounds.softAccessoryPlace.Play();
            SelectItemM.Invoke(c, new object[] { item });
            SingletonBehaviour<MannequinScene>.Instance.UpdateScores();
            float left = PlayerProgress.Current.AccessoryCount(item.state.definition);
            Speech.Say($"Put {NameOf(item)} on, {s.name}. {Mathf.RoundToInt(left)} left. It's selected: its size and turn buttons are in the list, then Done.");
        }

        // ---------- routes for trims ----------

        internal struct Route { public string name; public List<(Vector3 pos, Vector3 n)> points; public float length; }

        private static List<Route> Routes(Mannequin body)
        {
            var routes = new List<Route>();
            Dress dress = SingletonBehaviour<GameManager>.Instance.activeDress;
            if (dress == null) return routes;
            foreach (PanelJoin j in dress.panelJoins)
            {
                if (j.panel == null || j.connectedPanel == null || !j.panel.VisibleOnMannequin || !j.connectedPanel.VisibleOnMannequin)
                    continue;
                Mesh mesh = j.panel.MannequinMeshFilter != null ? j.panel.MannequinMeshFilter.sharedMesh : null;
                if (mesh == null) continue;
                Transform tr = j.panel.MannequinObj.transform;
                // Chain the seam's edges into one line of vertices.
                var chain = new List<int>();
                foreach (JoinedEdge e in j.joinedEdges)
                {
                    if (chain.Count == 0) { chain.Add(e.panelEdge.v1); chain.Add(e.panelEdge.v2); continue; }
                    if (chain[chain.Count - 1] == e.panelEdge.v1) chain.Add(e.panelEdge.v2);
                    else if (chain[chain.Count - 1] == e.panelEdge.v2) chain.Add(e.panelEdge.v1);
                    else if (chain[0] == e.panelEdge.v2) chain.Insert(0, e.panelEdge.v1);
                    else if (chain[0] == e.panelEdge.v1) chain.Insert(0, e.panelEdge.v2);
                    else { chain.Add(e.panelEdge.v1); chain.Add(e.panelEdge.v2); }
                }
                var pts = new List<(Vector3, Vector3)>();
                foreach (int v in chain)
                {
                    Vector3 p = tr.TransformPoint(EdgeHelpers.GetVertexPositionFromIndex(mesh, v));
                    Vector3 n = tr.TransformDirection(EdgeHelpers.GetVertexNormalFromIndex(mesh, v)).normalized;
                    if (pts.Count == 0 || Vector3.Distance(pts[pts.Count - 1].Item1, p) > 0.005f)
                        pts.Add((p, n));
                }
                if (pts.Count < 2) continue;
                float len = 0f;
                for (int i = 0; i + 1 < pts.Count; i++) len += Vector3.Distance(pts[i].Item1, pts[i + 1].Item1);
                routes.Add(new Route { name = "the seam " + Sewing.JoinName(j), points = pts, length = len });
            }
            return routes;
        }

        private static void PlacePath(AccessoryPlacementController c, Mannequin body, Route r)
        {
            var path = Selected(c) as AccessoryItemPath;
            if (path == null) return;
            float have = PlayerProgress.Current.AccessoryCount(path.state.definition);
            // Resample the seam at short steps (the game joins nodes by following the surface).
            var nodes = new List<(Vector3 pos, Vector3 n)> { r.points[0] };
            float used = 0f, step = Mathf.Min(0.05f, path.maxDistance * 0.9f);
            for (int i = 1; i < r.points.Count; i++)
            {
                var (a, an) = nodes[nodes.Count - 1];
                var (b, bn) = r.points[i];
                float d = Vector3.Distance(a, b);
                while (d > step)
                {
                    Vector3 mid = Vector3.MoveTowards(a, b, step);
                    if (used + step > have) goto done;
                    used += step;
                    nodes.Add((mid, Vector3.Slerp(an, bn, 0.5f).normalized));
                    a = mid;
                    d = Vector3.Distance(a, b);
                }
                if (used + d > have) goto done;
                used += d;
                nodes.Add((b, bn));
            }
        done:
            if (nodes.Count < 2)
            {
                Speech.Say("You don't have enough of it for that.");
                return;
            }
            var first = nodes[0];
            path.transform.position = first.pos;
            path.transform.forward = first.n;
            path.SetSurfacePosition(body.transform.InverseTransformPoint(first.pos),
                Quaternion.Inverse(body.transform.rotation) * path.transform.rotation,
                body.transform.InverseTransformDirection(first.n), body);
            OnSurface(c) = true;
            for (int i = 1; i < nodes.Count; i++)
                path.AddNode(nodes[i].pos, nodes[i].n);
            if (!path.EndPlacing())
            {
                c.CancelPlacement();
                Speech.Say("That didn't work; it's back in the sidebar.");
                return;
            }
            Consume.Invoke(c, new object[] { path, path.state.PathLength() });
            Selected(c) = null;
            ModeField(c) = AccessoryPlacementController.PlacementMode.None;
            c.usageLabel.Hide();
            SingletonBehaviour<MannequinScene>.Instance.UpdateScores();
            bool partial = used + 0.001f < r.length;
            Speech.Say($"Ran {path.state.definition.PrettyName} along {r.name}: {path.state.PathLength():0.00} metres" +
                       (partial ? ", as far as it would go" : "") + $". {PlayerProgress.Current.AccessoryCount(path.state.definition):0.##} metres left.");
        }

        // ---------- hooks ----------

        [HarmonyPatch(typeof(AccessoryPlacementController), "OnInventoryAccessoryPressed")]
        private static class PickUpPatch
        {
            private static void Postfix(AccessoryPlacementController __instance, AccessoryInventoryButton button)
            {
                _piece = null;
                UINav.ResetFocus();
                if (button == null || button.accessory == null || !Placing)
                    return;
                bool trim = __instance.Mode == AccessoryPlacementController.PlacementMode.Path;
                Speech.Say($"Holding {button.accessory.PrettyName}. " + (trim
                    ? "Arrows go through the seams you can run it along; Enter places it."
                    : "First choose a piece of the dress, then a spot on it.") + " The last choice puts it back.");
            }
        }
    }
}
