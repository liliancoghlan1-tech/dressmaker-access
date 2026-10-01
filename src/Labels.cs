using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DressmakerAccess
{
    /// <summary>
    /// Game-specific knowledge for the navigator: better names for buttons that
    /// are only pictures, and which popups should hold Tab while they are open.
    /// </summary>
    internal static class Labels
    {
        /// <summary>Return a spoken name for this element, or null to use the default.</summary>
        internal static string For(GameObject go)
        {
            var up = go.GetComponent<UI.UnlockedPattern>();
            if (up != null)
            {
                var gc = HarmonyLib.Traverse.Create(up).Field("_garmentComponent").GetValue<GarmentComponent>();
                if (gc != null)
                {
                    string styles = ShopAccess.Styles(gc.additiveTags, 3);
                    return "New pattern: " + gc.PrettyName + (styles.Length > 0 ? ". " + styles : "");
                }
            }
            string letter = Letters.Label(go);
            if (letter != null)
                return letter;
            if (go.name == "Continue" && TextWatch.SalePrice != null && go.GetComponentInParent<SellDressSummary>() != null)
                return $"Sell for {TextWatch.SalePrice} gold";
            string photo = PhotoAccess.Label(go);
            if (photo != null)
                return photo;
            var person = go.GetComponent<ContactListPersonItem>();
            if (person != null)
                return DressInfo.PersonLabel(person);
            var history = go.GetComponentInParent<DressHistoryItemUI>();
            if (history != null)
                return (UINav.TextOf(history.gameObject).TrimEnd('.') + ". Enter opens this dress").Replace(".. ", ". ");
            string opt = OptionLabel(go);
            if (opt != null)
                return opt;
            string sidebarFilter = SidebarFilter.Label(go);
            if (sidebarFilter != null)
                return sidebarFilter;
            string filter = FilterLabel(go);
            if (filter != null)
                return filter;
            if ((go.name == "Export" || go.name == "Import") && go.GetComponentInParent<PhotoScene>() != null)
                return go.name == "Export"
                    ? "Export: saves this dress design as a file on your computer. Opens a Windows save window; Escape there cancels"
                    : "Import: loads a dress design from a file. Opens a Windows file window; Escape there cancels";
            if (go.name == "Yes" && go.GetComponentInParent<PhotoScene>() != null && go.transform.parent != null && go.transform.parent.name == "Response"
                && go.transform.parent.parent != null && go.transform.parent.parent.name == "Dialogue")
                return "Save the photo as a picture file on your computer. Optional; this opens a Windows save window, press Escape there to skip";
            if (go.name == "inner" && go.transform.parent != null && go.transform.parent.name == "QuestSummary")
                return ScoreBox(go);
            if (go.name == "SewingTable" && go.transform.parent != null && go.transform.parent.name == "Background")
                return "Sewing Table. Seams are started from the Mannequin instead";
            if ((go.name == "Fabrics" || go.name == "Accessories") && go.transform.parent != null && go.transform.parent.name == "Sign")
                return go.name == "Fabrics" ? "Shop shelf: show fabrics" : "Shop shelf: show accessories, like buttons, bows and trims";
            string sketch = Sketch.Label(go);
            if (sketch != null)
                return sketch;
            string shop = ShopAccess.Label(go);
            if (shop != null)
                return shop;
            string acc = AccessoryAccess.Label(go);
            if (acc != null)
                return acc;
            string man = MannequinAccess.Label(go);
            if (man != null)
                return man;
            string cut = Cutting.Label(go);
            if (cut != null)
                return cut;
            // Last resort for unnamed arrows (photo studio rows): name them from their row.
            if ((go.name == "Left" || go.name == "Right") && go.transform.parent != null && string.IsNullOrEmpty(UINav.TextOf(go)))
            {
                string row = UINav.TextOf(go.transform.parent.gameObject);
                if (!string.IsNullOrEmpty(row))
                {
                    row = System.Text.RegularExpressions.Regex.Replace(row, "\\((\\d+)/(\\d+)\\)", "$1 of $2");
                    return (go.name == "Left" ? "Previous. " : "Next. ") + "Now " + row;
                }
            }
            var slot = go.GetComponent<SaveSlotButton>() ?? go.GetComponentInParent<SaveSlotButton>();
            if (slot != null)
            {
                string n = "Slot " + (slot.slot + 1);
                if (slot.deleteButton != null && (go == slot.deleteButton || go.transform.IsChildOf(slot.deleteButton.transform)))
                    return "Delete the saved game in " + n.ToLowerInvariant();
                if (!slot.Occupied)
                    return n + ", empty, start a new game";
                string rank = slot.rankText != null ? Speech.Clean(slot.rankText.text) : "";
                return n + ", saved game" + (string.IsNullOrEmpty(rank) ? "" : ", " + rank);
            }
            return null;
        }

        /// <summary>The options menu: its sliders and switches have their names beside them, not inside.</summary>
        private static string OptionLabel(GameObject go)
        {
            PauseMenu pm = go.GetComponentInParent<PauseMenu>();
            if (pm == null)
                return null;
            var s = go.GetComponent<Slider>();
            var t = go.GetComponent<Toggle>();
            string on = t != null && t.isOn ? "checked" : "not checked";
            if (s != null && s == pm.masterVolumeSlider)
                return "Master volume: all the game's sound, music included. Slider " + UINav.SliderValue(s) + ", Left and Right change it";
            if (s != null && s == pm.musicVolumeSlider)
                return "Music volume: the music only. Slider " + UINav.SliderValue(s) + ", Left and Right change it";
            if (t != null && t == pm.sewingAssistToggle)
                return "Sewing assist: the machine steers each seam for you, " + on + ". F5 also switches it";
            if (t != null && t == pm.rotationGizmoToggle)
                return "Accessory rotation handle: a mouse handle for turning accessories on the dress; the mod doesn't need it, " + on;
            // Arrow pairs: .../<Row>/Options/(Left|Right)
            if ((go.name == "Left" || go.name == "Right") && go.transform.parent != null && go.transform.parent.parent != null)
            {
                string row = go.transform.parent.parent.name;
                row = row == "FpsCap" ? "Frame limit" : row == "WindowMode" ? "Window mode" : row;
                string value = UINav.TextOf(go.transform.parent.gameObject);
                return row + ": " + (go.name == "Left" ? "previous" : "next") + " setting. Now " + value;
            }
            return null;
        }

        private static readonly AccessTools.FieldRef<FilterToggleButton, Color> FilterOnColour =
            AccessTools.FieldRefAccess<FilterToggleButton, Color>("selectedColor");

        /// <summary>Shop filters: say which group they're in and whether they're on.</summary>
        private static string FilterLabel(GameObject go)
        {
            FilterToggleButton f = go.GetComponentInParent<FilterToggleButton>();
            if (f == null || f.Button == null)
                return null;
            string name = f.gameObject.name;
            string group = f.transform.parent != null && f.transform.parent.parent != null ? f.transform.parent.parent.name : "";
            group = group == "FabricType" ? "Fabric type" : group == "ColorType" ? "Colour" : group == "Tag" ? "Style" : UINav.Prettify(group);
            bool on = f.Button.colors.normalColor == FilterOnColour(f);
            return (group.Length > 0 ? group + ": " : "") + name + ", " + (on ? "checked" : "not checked");
        }

        /// <summary>"Quality, 0/60, Professional, 0/40" -> a sentence that says what it measures.</summary>
        private static string ScoreBox(GameObject go)
        {
            string raw = UINav.TextOf(go);
            string[] parts = raw.Split(new[] { ", " }, System.StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder("Your dress so far: ");
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                string[] nums = parts[i + 1].Split('/');
                sb.Append(parts[i]).Append(' ').Append(nums.Length == 2 ? nums[0] + " of " + nums[1] : parts[i + 1]).Append(", ");
            }
            sb.Append("This scores the real dress as you cut and sew it, so it starts at zero; while designing, press T in the sketchbook for the estimate.");
            return sb.ToString();
        }

        /// <summary>
        /// Items that should come straight after another one instead of in screen order:
        /// each save slot's Delete button sits above the slots, so it would be read first.
        /// </summary>
        internal static GameObject ComesAfter(GameObject go)
        {
            var slot = go.GetComponentInParent<SaveSlotButton>();
            if (slot != null && slot.deleteButton != null && (go == slot.deleteButton || go.transform.IsChildOf(slot.deleteButton.transform)))
                return slot.gameObject;
            return null;
        }

        /// <summary>
        /// Pages read one after the other rather than row by row across both: in the friendship
        /// book, the list of people (left page) comes before the person's page (right).
        /// </summary>
        internal static int Column(GameObject go)
        {
            Sketchbook b = Sketch.Book;
            if (b == null || SketchPages.SectionName(b) != "Rolodex")
                return 0;
            for (Transform t = go.transform; t != null; t = t.parent)
                if (t.name.StartsWith("RightPage"))
                    return 1;
            return 0;
        }

        /// <summary>Elements the mod drives with its own keys; keep them out of the Tab list.</summary>
        internal static bool Skip(GameObject go)
        {
            return go.GetComponent<MeasuringTape>() != null || go.GetComponent<MeasuringTapeIcon>() != null
                || go.GetComponent<PersonMeasurer>() != null || go.GetComponent<Gear>() != null;
        }

        /// <summary>A custom action for Enter, or null to click normally.</summary>
        internal static System.Action ActionFor(GameObject go) => Sketch.ActionFor(go) ?? Cutting.ActionFor(go) ?? MannequinAccess.ActionFor(go);

        /// <summary>Open popups, innermost last. Tab stays inside the last one that has anything in it.</summary>
        internal static List<GameObject> ModalRoots()
        {
            var roots = new List<GameObject>();
            foreach (SaveSlotPopup p in Object.FindObjectsByType<SaveSlotPopup>(FindObjectsSortMode.None))
            {
                if (p.popupRoot != null && p.popupRoot.activeInHierarchy) roots.Add(p.popupRoot);
                if (p.deleteConfirmDialog != null && p.deleteConfirmDialog.activeInHierarchy) roots.Add(p.deleteConfirmDialog);
            }
            foreach (PauseMenu m in Object.FindObjectsByType<PauseMenu>(FindObjectsSortMode.None))
            {
                if (m.IsShowing) roots.Add(m.gameObject);
                if (m.confirmDialog != null && m.confirmDialog.activeInHierarchy) roots.Add(m.confirmDialog);
            }
            if (AccessoryAccess.ModalRoot != null) roots.Add(AccessoryAccess.ModalRoot);
            foreach (UnlockFanfare u in Object.FindObjectsByType<UnlockFanfare>(FindObjectsSortMode.None))
                if (u.isActiveAndEnabled) roots.Add(u.gameObject);
            foreach (PhotoScene ps in Object.FindObjectsByType<PhotoScene>(FindObjectsSortMode.None))
            {
                if (ps.confirmDialog != null && ps.confirmDialog.activeInHierarchy) roots.Add(ps.confirmDialog);
                if (ps.confirmBadSubmissionDialog != null && ps.confirmBadSubmissionDialog.activeInHierarchy) roots.Add(ps.confirmBadSubmissionDialog);
                var sell = HarmonyLib.Traverse.Create(ps).Field("sellDressSummary").GetValue<SellDressSummary>();
                if (sell != null && sell.gameObject.activeInHierarchy) roots.Add(sell.gameObject);
                var gift = HarmonyLib.Traverse.Create(ps).Field("giftCharacterPicker").GetValue<GameObject>();
                if (gift != null && gift.activeInHierarchy) roots.Add(gift);
            }
            foreach (GarmentComponentSelectionUI g in Object.FindObjectsByType<GarmentComponentSelectionUI>(FindObjectsSortMode.None))
                if (g.isActiveAndEnabled) roots.Add(g.gameObject);
            foreach (ColorPicker c in Object.FindObjectsByType<ColorPicker>(FindObjectsSortMode.None))
                if (c.isActiveAndEnabled) roots.Add(c.gameObject);
            foreach (FilterPopup f in Object.FindObjectsByType<FilterPopup>(FindObjectsSortMode.None))
                if (f.IsShowing) roots.Add(f.gameObject);
            foreach (TutorialMessage t in Object.FindObjectsByType<TutorialMessage>(FindObjectsSortMode.None))
                if (t.isActiveAndEnabled && t.GetComponent<UnityEngine.UI.Image>() is var img && img != null && img.enabled)
                    roots.Add(t.gameObject); // modal tutorial notes block everything else
            return roots;
        }
    }
}
