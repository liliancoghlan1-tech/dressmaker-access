using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Framework;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// The sketchbook (designing the dress).
    ///  - The part arrows read as "Next bodice" etc., the style dots as "Bodice style 2 of 3".
    ///  - Changing a part says the new part and how the client's wishes now stand.
    ///  - Fabrics in the sidebar say their length and styles; Enter drops a swatch on
    ///    the sketch (that's what the game's style estimate uses). Swatches on the
    ///    sketch are listed too; Enter removes one.
    ///  - T reads the style estimate against what the client asked for.
    /// </summary>
    internal static class Sketch
    {
        private static readonly AccessTools.FieldRef<Sketchbook, Dictionary<ItemTag, float>> TagWeights =
            AccessTools.FieldRefAccess<Sketchbook, Dictionary<ItemTag, float>>("_tagWeights");
        private static readonly AccessTools.FieldRef<VariantSelector, int> VariantSelected =
            AccessTools.FieldRefAccess<VariantSelector, int>("_selected");
        private static readonly AccessTools.FieldRef<VariantSelector, int> VariantCount =
            AccessTools.FieldRefAccess<VariantSelector, int>("_count");
        private static readonly System.Reflection.MethodInfo SwatchUpdatePlacement =
            AccessTools.Method(typeof(SketchbookFabric), "UpdatePlacement");
        private static readonly System.Reflection.MethodInfo SwatchRemovePlacement =
            AccessTools.Method(typeof(SketchbookFabric), "RemovePlacement");

        internal static Sketchbook Book
        {
            get
            {
                var gm = Rooms.GameManagerOrNull();
                if (gm == null || gm.CurrentScene != GameManager.Scene.Sketchbook)
                    return null;
                try { return SingletonBehaviour<Sketchbook>.Instance; } catch { return null; }
            }
        }

        // ---------- labels ----------

        private static readonly Dictionary<string, string> Parts = new Dictionary<string, string>
        {
            { "Bodices", "bodice" }, { "Sleeves", "sleeves" }, { "Collars", "collar" }, { "Skirts", "skirt" },
        };

        internal static string Label(GameObject go)
        {
            // Part arrows and titles: .../Config/<Part>/Component/(Left|Right|TitleHolder)
            // Style arrows and dots:  .../Config/<Part>/Variation/(Left|Right|Dots/VariantDot)
            Transform t = go.transform;
            for (Transform p = t; p != null && p.parent != null; p = p.parent)
            {
                if (!Parts.TryGetValue(p.name, out string part) || p.parent.name != "Config")
                    continue;
                bool component = t.IsChildOf(p.Find("Component") ?? p);
                bool variation = p.Find("Variation") != null && t.IsChildOf(p.Find("Variation"));
                string current = CurrentName(part);
                if (variation)
                {
                    var vs = p.GetComponentInChildren<VariantSelector>(true);
                    GarmentComponent gcNow = CurrentPart(part);
                    string of = vs != null ? $" {VariantSelected(vs) + 1} of {VariantCount(vs)}" : "";
                    string note = vs != null ? DressInfo.Variant(gcNow, VariantSelected(vs), withNumber: false) : "";
                    if (note.Length > 0) of += ": " + note;
                    if (go.name == "Left") return $"Previous {part} style. Now style{of}";
                    if (go.name == "Right") return $"Next {part} style. Now style{of}";
                    if (go.name.StartsWith("VariantDot"))
                    {
                        int i = go.transform.GetSiblingIndex() + 1;
                        bool sel = vs != null && VariantSelected(vs) + 1 == i;
                        string dotNote = DressInfo.Variant(gcNow, i - 1, withNumber: false);
                        return $"{Cap(part)} style {i}{(dotNote.Length > 0 ? ": " + dotNote : "")}{(sel ? ", selected" : "")}";
                    }
                }
                if (component)
                {
                    if (go.name == "Left") return $"Previous {part}. Now {current}";
                    if (go.name == "Right") return $"Next {part}. Now {current}";
                    if (go.name == "TitleHolder") return $"{Cap(part)}: {current}. Choose from a list";
                }
            }

            if (go.name == "bookmark" && go.transform.parent != null && go.transform.parent.name == "OldDresses")
                return "Bookmark: back to the dress in progress";
            if (go.name == "Dress" && go.transform.parent != null && go.transform.parent.name == "OldDressRightPage")
                return "Picture of the dress. The lines at the top of the list describe it";
            if (go.name == "EnterPhotoMode")
                return "Photo Mode: take a new photo of this dress in the photo studio";
            if (go.name == "bookmark" && go.transform.parent != null && go.transform.parent.name == "NewDress")
                return "Bookmark: back to the dress in progress";
            if (go.transform.parent != null && go.transform.parent.name == "NavTabs")
            {
                string section = Book != null ? SketchPages.SectionName(Book) : "";
                if (go.name == "FriendshipButton") return "Friendship book tab" + (section == "Rolodex" ? ", selected" : "");
                if (go.name == "ActiveDressButton") return "Dress in progress tab" + (section == "WipZone" ? ", selected" : "");
                if (go.name == "NewDressButton") return "New off-the-rack design tab" + (section == "NewDress" ? ", selected" : "");
            }

            var gcb = go.GetComponent<GarmentComponentUIButton>();
            if (gcb != null && gcb.garmentComponent != null)
            {
                GarmentComponent gc = gcb.garmentComponent;
                Sketchbook sb = Book;
                bool current = sb != null && (gc == (GarmentComponent)sb.SelectedBodice || gc == (GarmentComponent)sb.SelectedSkirt
                                              || gc == (GarmentComponent)sb.SelectedSleeve || gc == (GarmentComponent)sb.SelectedCollar);
                string styles = ShopAccess.Styles(gc.additiveTags, 3);
                return gc.PrettyName + (current ? ", your current one" : "") + (styles.Length > 0 ? ". " + styles : "");
            }
            if (go.name == "Spawn" && go.transform.parent != null && go.transform.parent.name == "RightPageDraft")
            {
                string txt = UINav.TextOf(go);
                if (txt.ToLowerInvariant().Contains("draft"))
                    return "Draft Pattern: finishes the design and moves on to sizing the mannequin";
                return txt;
            }

            if (go.GetComponent<SketchbookPencil>() != null)
                return "Pencil: colours in the drawing, for looks only. It doesn't change the fabric or the score; the dress takes its real colours from the fabric you cut";
            string colour = ColourLabel(go);
            if (colour != null)
                return colour;

            var inv = go.GetComponent<InventoryItem>();
            if (inv != null)
                return InventoryLabel(inv);

            var swatch = go.GetComponent<SketchbookFabric>();
            if (swatch != null && Book != null && Book.fabricSamplesOldDress.Contains(swatch))
                return "Swatch pinned to this design: " + SwatchName(swatch);
            if (swatch != null && Book != null && Book.fabricSamplesDraft.Contains(swatch))
                return "Swatch on the sketch: " + SwatchName(swatch) + ". Enter removes it";

            var tag = go.GetComponent<TagBar>() ?? go.GetComponentInParent<TagBar>();
            if (tag != null)
                return null;
            return null;
        }

        private static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s.Substring(1);

        private static GarmentComponent CurrentPart(string part)
        {
            Sketchbook b;
            try { b = SingletonBehaviour<Sketchbook>.Instance; } catch { return null; }
            return part == "bodice" ? (GarmentComponent)b.SelectedBodice
                : part == "sleeves" ? b.SelectedSleeve
                : part == "collar" ? (GarmentComponent)b.SelectedCollar
                : b.SelectedSkirt;
        }

        private static string CurrentName(string part)
        {
            Sketchbook b;
            try { b = SingletonBehaviour<Sketchbook>.Instance; } catch { return ""; }
            GarmentComponent gc = part == "bodice" ? (GarmentComponent)b.SelectedBodice
                : part == "sleeves" ? b.SelectedSleeve
                : part == "collar" ? (GarmentComponent)b.SelectedCollar
                : b.SelectedSkirt;
            return gc != null ? gc.PrettyName : "none";
        }

        private static string SwatchName(SketchbookFabric s)
            => s.fabric != null ? s.fabric.PrettyName : s.placement?.accessory != null ? s.placement.accessory.PrettyName : "swatch";

        internal static string InventoryLabel(InventoryItem inv)
        {
            if (inv.FabricPiece != null && inv.FabricPiece.fabric != null)
            {
                Fabric f = inv.FabricPiece.fabric;
                return $"{f.PrettyName}, {inv.FabricPiece.length:0.##} metres. {FabricStyles(f)}";
            }
            if (inv.Accessory != null)
            {
                string count = UINav.TextOf(inv.gameObject);
                return inv.Accessory.PrettyName + (string.IsNullOrEmpty(count) ? "" : ", " + count);
            }
            return null;
        }

        internal static string FabricStyles(Fabric f)
        {
            if (f.tagWeights == null || f.tagWeights.Count == 0)
                return "";
            return "Styles: " + string.Join(", ", f.tagWeights.Where(t => t.tag != null)
                .OrderByDescending(t => t.weight).Select(t => $"{t.tag.PrettyName} {Mathf.RoundToInt(t.weight)}"));
        }

        // ---------- actions ----------

        internal static Action ActionFor(GameObject go)
        {
            Sketchbook b = Book;
            if (b == null)
                return null;
            var inv = go.GetComponent<InventoryItem>();
            if (inv != null && inv.FabricPiece != null && inv.FabricPiece.fabric != null)
                return () => AddSwatch(b, inv.FabricPiece.fabric);
            var swatch = go.GetComponent<SketchbookFabric>();
            if (swatch != null && b.fabricSamplesDraft.Contains(swatch))
                return () => RemoveSwatch(b, swatch);
            return null;
        }

        private static void AddSwatch(Sketchbook b, Fabric fabric)
        {
            if (b.GetFabricSampleForType(fabric) != null)
            {
                Speech.Say(fabric.PrettyName + " is already on the sketch.");
                return;
            }
            SketchbookFabric s = UnityEngine.Object.Instantiate(b.sketchbookFabricPrefab, b.transform);
            b.fabricSamplesDraft.Add(s);
            s.Initialize(fabric);
            // Put swatches in the page's two dashed sample boxes, then stack further ones below them.
            s.transform.position = SwatchSpot(b, b.fabricSamplesDraft.Count - 1);
            s.transform.SetParent(b.fabricSampleParent, worldPositionStays: true);
            s.placement.pos = s.transform.localPosition;
            SwatchUpdatePlacement.Invoke(s, null);
            b.UpdateTags();
            SingletonBehaviour<SidebarInventory>.Instance.RefreshFavourites();
            Speech.Say($"Added a {fabric.PrettyName} swatch to the sketch. " + StyleSummary(b, full: false));
        }

        private static Vector3 SwatchSpot(Sketchbook b, int n)
        {
            Transform page = b.fabricSampleParent.parent;
            Transform a = page.Find("FabricPlaceholder");
            Transform c = page.Find("FabricPlaceholder (1)");
            if (a == null || c == null)
                return b.fabricSampleParent.position;
            Vector3 first = Centre(a), second = Centre(c);
            if (n == 0) return first;
            if (n == 1) return second;
            Vector3 step = second - first;
            return second + step * (n - 1);
        }

        private static Vector3 Centre(Transform t)
        {
            var rt = t as RectTransform;
            if (rt == null) return t.position;
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            return (c[0] + c[2]) / 2f;
        }

        private static void RemoveSwatch(Sketchbook b, SketchbookFabric s)
        {
            string name = SwatchName(s);
            b.fabricSamplesDraft.Remove(s);
            SwatchRemovePlacement.Invoke(s, null);
            s.gameObject.SmartDestroy();
            b.UpdateTags();
            SingletonBehaviour<SidebarInventory>.Instance.RefreshFavourites();
            Speech.Say($"Removed {name}. " + StyleSummary(b, full: false));
        }

        // ---------- style estimate ----------

        internal static void SayStyles()
        {
            Sketchbook b = Book;
            if (b == null)
                return;
            Speech.Say(StyleSummary(b, full: true));
        }

        internal static string StyleSummary(Sketchbook b, bool full)
        {
            var weights = TagWeights(b) ?? new Dictionary<ItemTag, float>();
            QuestDefinition q = PlayerProgress.Current?.CurrentQuestDefinition;
            var sb = new StringBuilder();
            var mentioned = new HashSet<ItemTag>();
            if (q != null)
            {
                foreach (QuestRequirement r in q.ActiveRequirements(PlayerProgress.Current))
                {
                    if (r is TagScoreRequirement t && t.tag != null)
                    {
                        weights.TryGetValue(t.tag, out float v);
                        string cmp = t.comparisonType == ComparisonType.AtMost ? "at most" : "at least";
                        bool ok = t.comparisonType == ComparisonType.AtMost ? v <= t.targetScore : v >= t.targetScore;
                        sb.Append($"{t.tag.PrettyName} {Mathf.RoundToInt(v)}, needs {cmp} {Mathf.RoundToInt(t.targetScore)}{(ok ? ", met" : "")}. ");
                        mentioned.Add(t.tag);
                    }
                    else if (full && !(r is QualityRequirement))
                    {
                        sb.Append(Speech.Clean(r.GetSketchbookDisplayString())).Append(". ");
                    }
                }
            }
            if (b.fabricSamplesDraft.Count == 0)
                sb.Append("No fabric swatches on the sketch yet, so fabric styles aren't counted. ");
            var others = weights.Where(kv => kv.Key != null && !mentioned.Contains(kv.Key) && kv.Value >= 1f)
                .OrderByDescending(kv => kv.Value).Take(full ? 12 : 3).ToList();
            if (others.Count > 0)
                sb.Append(full ? "All styles: " : "Top styles: ").Append(string.Join(", ", others.Select(kv => $"{kv.Key.PrettyName} {Mathf.RoundToInt(kv.Value)}"))).Append(".");
            if (full)
            {
                TMP_Text usage = Traverse.Create(b).Field("fabricUsageText").GetValue<TMP_Text>();
                if (usage != null && usage.gameObject.activeInHierarchy)
                    sb.Append(" ").Append(Speech.Clean(usage.text)).Append(".");
            }
            return sb.ToString();
        }

        // ---------- pencil and colours ----------

        private static readonly AccessTools.FieldRef<SketchbookPencil, RectTransform> DressRect =
            AccessTools.FieldRefAccess<SketchbookPencil, RectTransform>("dressRect");
        private static readonly AccessTools.FieldRef<ColorButton, Color> ButtonColour =
            AccessTools.FieldRefAccess<ColorButton, Color>("_color");
        private static readonly System.Reflection.MethodInfo PencilCancel =
            AccessTools.Method(typeof(SketchbookPencil), "CancelDrag");

        // Where each part sits inside the pencil's dress area (fractions, 0,0 = bottom left).
        private static readonly (string name, float x, float y)[] FillSpots =
        {
            ("the bodice", 0.5f, 0.78f),
            ("the skirt", 0.5f, 0.35f),
            ("the left sleeve", 0.3f, 0.8f),
            ("the right sleeve", 0.7f, 0.8f),
            ("the collar", 0.5f, 0.92f),
        };

        /// <summary>A way out of the garment list without choosing (the game closes it by clicking outside).</summary>
        internal static List<UINav.Item> ListCloseItem()
        {
            var list = new List<UINav.Item>();
            Sketchbook b = Book;
            if (b == null)
                return list;
            foreach (GarmentComponentSelectionUI g in UnityEngine.Object.FindObjectsByType<GarmentComponentSelectionUI>(FindObjectsSortMode.None))
            {
                if (!g.isActiveAndEnabled) continue;
                GarmentComponentSelectionUI captured = g;
                list.Add(new UINav.Item
                {
                    Go = g.gameObject,
                    Screen = new Vector2(10000, -10000), // always last
                    Label = "Close the list without changing anything",
                    OnActivate = () => { captured.Hide(); Speech.Say("List closed."); },
                });
            }
            return list;
        }

        // The old buttons are only destroyed at the end of the frame, so count on the next one.
        private static System.Collections.IEnumerator AnnounceList(GarmentComponentSelectionUI ui, string what)
        {
            yield return null;
            int n = ui.buttonParent != null ? ui.buttonParent.GetComponentsInChildren<GarmentComponentUIButton>().Length : 0;
            Speech.Say($"Choose a {what}: {n} to choose from. Arrows move, Enter picks one. The last item closes the list without changing anything.");
        }

        [HarmonyPatch(typeof(GarmentComponentSelectionUI), nameof(GarmentComponentSelectionUI.InitializeFor))]
        private static class ListOpenPatch
        {
            private static void Postfix(GarmentComponentSelectionUI __instance, GarmentComponent.Type type)
            {
                string what = type == GarmentComponent.Type.Bodice ? "bodice" : type == GarmentComponent.Type.Skirt ? "skirt"
                    : type == GarmentComponent.Type.Sleeve ? "sleeves" : "collar";
                Plugin.Instance.StartCoroutine(AnnounceList(__instance, what));
            }
        }

        internal static List<UINav.Item> PencilItems()
        {
            var list = new List<UINav.Item>();
            Sketchbook b = Book;
            if (b == null || b.pencil == null || !b.pencil.IsDragging)
                return list;
            RectTransform area = DressRect(b.pencil);
            if (area == null)
                return list;
            Vector3[] c = new Vector3[4];
            area.GetWorldCorners(c);
            int i = 0;
            foreach (var spot in FillSpots)
            {
                Vector3 world = Vector3.Lerp(Vector3.Lerp(c[0], c[3], spot.x), Vector3.Lerp(c[1], c[2], spot.x), spot.y);
                string name = spot.name;
                list.Add(new UINav.Item
                {
                    Go = b.pencil.gameObject,
                    Screen = new Vector2(-1000 + i++, 10000), // keep these first, in this order
                    Label = "Colour " + name + " " + ColourName(b.FillColour),
                    OnActivate = () =>
                    {
                        b.sketchbookSprite.FillPoint(world);
                        Speech.Say("Coloured " + name + ".");
                    },
                });
            }
            list.Add(new UINav.Item
            {
                Go = b.gameObject,
                Screen = new Vector2(-1000 + i, 10000),
                Label = "Put the pencil down",
                OnActivate = () => { PencilCancel.Invoke(b.pencil, null); Speech.Say("Pencil down."); },
            });
            return list;
        }

        /// <summary>The game's 42 pencil colours, each with its own name (the grid has several blues, greys...).</summary>
        private static readonly Dictionary<string, string> PaletteNames = new Dictionary<string, string>
        {
            ["EEE9D6"] = "cream", ["EFDEAD"] = "butter yellow", ["E4C99D"] = "sand beige", ["CCAB84"] = "tan",
            ["BA6E4B"] = "terracotta, an orange brown", ["7E3F26"] = "chestnut brown", ["572E1B"] = "chocolate brown",
            ["D5D5D5"] = "light grey", ["E9B7BC"] = "blush pink", ["E48E8E"] = "dusty rose pink", ["E98C76"] = "coral, a pinkish orange",
            ["ECD667"] = "lemon yellow", ["ECB551"] = "marigold, a golden yellow", ["C3741E"] = "burnt orange",
            ["AFB7C0"] = "silver grey", ["DB5E63"] = "strawberry red", ["AD332A"] = "brick red", ["720016"] = "burgundy, a deep wine red",
            ["C1C37F"] = "pale olive green", ["9A9A2A"] = "olive green", ["494F00"] = "dark olive green",
            ["8B8E9F"] = "slate grey, a bluish grey", ["CB7BBF"] = "orchid pink", ["B94682"] = "raspberry pink", ["7B1452"] = "plum",
            ["99BC85"] = "sage green", ["6FA14B"] = "leaf green", ["2F5B21"] = "forest green", ["4C4B59"] = "charcoal grey",
            ["978ABF"] = "lavender", ["5E4790"] = "violet", ["3C2373"] = "deep purple",
            ["90C3B8"] = "seafoam, a pale blue green", ["54A48F"] = "jade green", ["205956"] = "dark teal", ["1C1B1D"] = "black",
            ["4D5DB6"] = "periwinkle blue", ["1C3284"] = "royal blue", ["0D2E5B"] = "navy blue", ["67B4DD"] = "sky blue",
            ["508ED1"] = "cornflower blue", ["1B497B"] = "denim blue",
        };

        /// <summary>A plain-English name for a colour swatch.</summary>
        internal static string ColourName(Color c)
        {
            if (PaletteNames.TryGetValue(ColorUtility.ToHtmlStringRGB(c), out string named))
                return named;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (v < 0.15f) return "black";
            if (s < 0.12f)
                return v > 0.9f ? "white" : v > 0.65f ? "light grey" : v > 0.35f ? "grey" : "dark grey";
            float deg = h * 360f;
            string hue = deg < 15 ? "red" : deg < 40 ? "orange" : deg < 65 ? "yellow" : deg < 95 ? "yellow green"
                : deg < 160 ? "green" : deg < 190 ? "teal" : deg < 250 ? "blue" : deg < 285 ? "purple"
                : deg < 330 ? "pink" : "red";
            if (hue == "orange" && v < 0.6f) hue = "brown";
            string shade = v < 0.45f ? "dark " : (s < 0.4f && v > 0.75f) ? "pale " : "";
            return shade + hue;
        }

        [HarmonyPatch(typeof(ColorPicker), nameof(ColorPicker.Show))]
        private static class PickerPatch
        {
            private static void Postfix(ColorPicker __instance)
            {
                // The picker opens where the mouse is; with no mouse that can be off-screen.
                var picker = Traverse.Create(__instance).Field("picker").GetValue<UnityEngine.UI.Image>();
                if (picker != null)
                {
                    picker.rectTransform.anchoredPosition = Vector2.zero;
                    Canvas.ForceUpdateCanvases();
                }
                Speech.Say("Colour picker. Tab through the colours; Enter picks one. Then Tab again for places to colour on the sketch. The pencil only colours the drawing; it doesn't change the fabric or the score.");
            }
        }

        internal static string ColourLabel(GameObject go)
        {
            var cb = go.GetComponent<ColorButton>();
            return cb == null ? null : "Colour: " + ColourName(ButtonColour(cb));
        }

        // ---------- hooks ----------

        private static void Changed(string part, GarmentComponent gc)
        {
            Sketchbook b = Book;
            if (b == null)
                return;
            Speech.Say($"{Cap(part)}: {(gc != null ? gc.PrettyName : "none")}. " + StyleSummary(b, full: false));
        }

        [HarmonyPatch(typeof(Sketchbook), nameof(Sketchbook.CycleBodice))]
        private static class BodicePatch { private static void Postfix(Sketchbook __instance) => Changed("bodice", __instance.SelectedBodice); }

        [HarmonyPatch(typeof(Sketchbook), nameof(Sketchbook.CycleSleeves))]
        private static class SleevesPatch { private static void Postfix(Sketchbook __instance) => Changed("sleeves", __instance.SelectedSleeve); }

        [HarmonyPatch(typeof(Sketchbook), nameof(Sketchbook.CycleCollar))]
        private static class CollarPatch { private static void Postfix(Sketchbook __instance) => Changed("collar", __instance.SelectedCollar); }

        [HarmonyPatch(typeof(Sketchbook), nameof(Sketchbook.CycleSkirt))]
        private static class SkirtPatch { private static void Postfix(Sketchbook __instance) => Changed("skirt", __instance.SelectedSkirt); }

        [HarmonyPatch(typeof(Sketchbook), nameof(Sketchbook.SelectGarmentComponent))]
        private static class SelectPatch
        {
            private static void Postfix(GarmentComponent gc)
            {
                string part = gc == null ? "chosen" : gc.type == GarmentComponent.Type.Bodice ? "bodice" : gc.type == GarmentComponent.Type.Skirt ? "skirt"
                    : gc.type == GarmentComponent.Type.Sleeve ? "sleeves" : "collar";
                Changed(part, gc);
            }
        }

        [HarmonyPatch(typeof(VariantSelector), nameof(VariantSelector.CycleForward))]
        private static class VarFwd { private static void Postfix(VariantSelector __instance) => Variant(__instance); }

        [HarmonyPatch(typeof(VariantSelector), nameof(VariantSelector.CycleBackward))]
        private static class VarBack { private static void Postfix(VariantSelector __instance) => Variant(__instance); }

        private static void Variant(VariantSelector vs)
        {
            if (Book == null)
                return;
            Speech.Say($"Style {VariantSelected(vs) + 1} of {VariantCount(vs)}. " + StyleSummary(Book, full: false));
        }
    }
}
