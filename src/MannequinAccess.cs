using System.Linq;
using Framework;
using HarmonyLib;
using SoundManager;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// The mannequin: pattern pieces go onto the body, then seams get sewn.
    /// Each cut piece has one place on the body; dragging it there snaps it on. With the
    /// mod, Enter on a piece in the sidebar puts it straight onto its place. Once two
    /// neighbouring pieces are on, their seam appears in the Tab list ("Sew seam: ...").
    /// </summary>
    internal static class MannequinAccess
    {
        internal static MannequinScene Scene
        {
            get
            {
                var gm = Rooms.GameManagerOrNull();
                if (gm == null || gm.CurrentScene != GameManager.Scene.Mannequin)
                    return null;
                try { return SingletonBehaviour<MannequinScene>.Instance; } catch { return null; }
            }
        }

        internal static System.Action ActionFor(GameObject go)
        {
            MannequinScene ms = Scene;
            if (ms == null)
                return null;
            var btn = go.GetComponent<OnMouseDownInventoryButton>();
            if (btn != null && btn.panel != null)
                return () => PutOn(ms, btn.panel);
            return null;
        }

        internal static string Label(GameObject go)
        {
            if (Scene == null)
                return null;
            var btn = go.GetComponent<OnMouseDownInventoryButton>();
            if (btn == null || btn.panel == null)
                return null;
            PatternPanel p = btn.panel;
            if (!p.HasBeenCut)
                return $"Pattern piece: {p.DisplayName}, not cut yet";
            return $"Pattern piece: {p.DisplayName}. Enter puts it on the mannequin";
        }

        private static void PutOn(MannequinScene ms, PatternPanel p)
        {
            if (!p.HasBeenCut)
            {
                Speech.Say(p.DisplayName + " hasn't been cut yet. Cut it at the cutting table first.");
                return;
            }
            GameManager gm = SingletonBehaviour<GameManager>.Instance;
            p.ShowRepresentation3D(resetPositionToCenter: true, clearSelection: false);
            p.InventoryButton.gameObject.SetActive(false);
            p.ShowRepresentationMannequinHovering();
            p.ShowRepresentationMannequin(animate: true);
            SingletonBehaviour<SharedPanelInteractor>.Instance.ClearSelection();
            SingletonBehaviour<SoundController>.Instance?.placeFabricOnMannequin.Play();
            ms.UpdateScores();
            if (!gm.activeDress.state.sewingComplete)
                gm.CheckDressCompletion();
            ms.UpdateSewButtons();
            int left = gm.activeDress.PanelPieces.Count(x => x.HasBeenCut && !x.PlacedOnMannequin && !x.HasBeenStitched);
            int seams = Object.FindObjectsByType<SewButton>(FindObjectsSortMode.None).Count(s => s.join != null);
            Speech.Say($"Put {p.DisplayName} on the mannequin. " +
                       (left > 0 ? $"{left} more {(left == 1 ? "piece" : "pieces")} in the sidebar. " : "All pieces are on. ") +
                       (seams > 0 ? $"{seams} {(seams == 1 ? "seam" : "seams")} ready to sew in the Tab list." : ""));
        }

        /// <summary>The game's Finish Dress prompt pops up silently once every seam is sewn.</summary>
        [HarmonyPatch(typeof(MannequinScene), "Show")]
        private static class FinishPromptPatch
        {
            private static void Postfix()
            {
                GameManager gm = Rooms.GameManagerOrNull();
                if (gm != null && !gm.isInIntro && gm.CurrentDressSewingComplete)
                    Speech.Queue("Every seam is sewn! Finish Dress is now first in the Tab list. It takes the dress to the photo studio to hand it in, "
                               + "so put any accessories on before you choose it.");
            }
        }
    }
}
