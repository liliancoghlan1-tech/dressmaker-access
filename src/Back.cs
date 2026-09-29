using Framework;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// Backspace and Escape as "get me out of here".
    ///   Escape: closes the open popup (list, colour picker, filters, confirm box...); with no
    ///     popup open it still does what the game does, open or close the options menu.
    ///   Backspace: the same, but with no popup it presses the room's Back button instead
    ///     (never at the cutting table, where Backspace puts a piece back).
    /// </summary>
    internal static class Back
    {
        private static readonly System.Reflection.MethodInfo PencilCancel =
            AccessTools.Method(typeof(SketchbookPencil), "CancelDrag");

        internal static void Backspace()
        {
            if (ClosePopup())
                return;
            PauseMenu pm = Object.FindFirstObjectByType<PauseMenu>();
            if (pm != null && pm.IsShowing)
            {
                SingletonBehaviour<GameManager>.Instance.ShowHideOptions();
                Speech.Say("Options closed.");
                return;
            }
            if (Cutting.Room != null)
            {
                Speech.Say("Nothing to close. To leave the cutting table, Tab to Back or press a room number.");
                return;
            }
            foreach (UINav.Item it in UINav.Gather())
            {
                if (it.OnActivate == null && it.Label != null && it.Label.Trim().ToLowerInvariant() == "back")
                {
                    Plugin.Log.LogInfo("[back] " + UINav.Path(it.Go));
                    UINav.ActivateItem(it);
                    return;
                }
            }
            Speech.Say("Nothing to close here.");
        }

        /// <summary>Close the innermost popup the mod knows how to close. True if one was closed.</summary>
        internal static bool ClosePopup()
        {
            foreach (PauseMenu m in Object.FindObjectsByType<PauseMenu>(FindObjectsSortMode.None))
                if (m.confirmDialog != null && m.confirmDialog.activeInHierarchy)
                {
                    m.HideResetConfirm();
                    return Said("Cancelled.");
                }
            foreach (LanguageSelectorPopup l in Object.FindObjectsByType<LanguageSelectorPopup>(FindObjectsSortMode.None))
                if (l.isActiveAndEnabled)
                {
                    l.Hide();
                    return Said("Language list closed.");
                }
            foreach (SaveSlotPopup p in Object.FindObjectsByType<SaveSlotPopup>(FindObjectsSortMode.None))
            {
                if (p.deleteConfirmDialog != null && p.deleteConfirmDialog.activeInHierarchy)
                {
                    p.OnCancelDelete();
                    return Said("Not deleted.");
                }
                if (p.popupRoot != null && p.popupRoot.activeInHierarchy)
                {
                    p.Hide();
                    return Said("Save slots closed.");
                }
            }
            foreach (ColorPicker c in Object.FindObjectsByType<ColorPicker>(FindObjectsSortMode.None))
                if (c.isActiveAndEnabled)
                {
                    c.Cancel();
                    return Said("Colour picker closed.");
                }
            foreach (GarmentComponentSelectionUI g in Object.FindObjectsByType<GarmentComponentSelectionUI>(FindObjectsSortMode.None))
                if (g.isActiveAndEnabled)
                {
                    g.Hide();
                    return Said("List closed.");
                }
            foreach (FilterPopup f in Object.FindObjectsByType<FilterPopup>(FindObjectsSortMode.None))
                if (f.IsShowing)
                {
                    f.Apply();
                    return Said("Filters closed, your choices kept.");
                }
            foreach (SellDressSummary s in Object.FindObjectsByType<SellDressSummary>(FindObjectsSortMode.None))
                if (s.isActiveAndEnabled)
                {
                    s.Cancel();
                    return Said("Not sold.");
                }
            Sketchbook b = Sketch.Book;
            if (b != null && b.pencil != null && b.pencil.IsDragging)
            {
                PencilCancel.Invoke(b.pencil, null);
                return Said("Pencil down.");
            }
            return false;
        }

        private static bool Said(string s)
        {
            UINav.ResetFocus();
            Speech.Say(s);
            return true;
        }

        /// <summary>The game's own Escape opens the options; close a popup first if one is open.</summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.ShowHideOptions))]
        private static class EscapePatch
        {
            private static bool Prefix()
            {
                if (!Input.GetKeyDown(KeyCode.Escape))
                    return true; // the Resume button and the mod's own calls
                PauseMenu pm = Object.FindFirstObjectByType<PauseMenu>();
                if (pm != null && pm.IsShowing)
                    return true;
                return !ClosePopup();
            }
        }
    }
}
