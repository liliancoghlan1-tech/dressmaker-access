using System.Linq;
using Framework;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// The sketchbook's pages. The game turns them with arrow buttons that only show up
    /// when the mouse is over that half of the book, so Page Up and Page Down turn them here.
    /// In order, front to back: the friendship book (your clients), the finished dresses,
    /// the dresses in progress (commissions and off-the-rack designs), and a page for
    /// starting a new off-the-rack design once that's unlocked.
    /// Every page turn, however it happened, says which page you're on.
    /// </summary>
    internal static class SketchPages
    {
        private static readonly AccessTools.FieldRef<Sketchbook, PageTurnButton> PrevButton =
            AccessTools.FieldRefAccess<Sketchbook, PageTurnButton>("prevDressButton");
        private static readonly AccessTools.FieldRef<Sketchbook, PageTurnButton> NextButton =
            AccessTools.FieldRefAccess<Sketchbook, PageTurnButton>("nextDressButton");
        private static readonly AccessTools.FieldRef<Sketchbook, int> OldIndex =
            AccessTools.FieldRefAccess<Sketchbook, int>("_oldQuestIndex");

        private static bool _turning;

        /// <summary>Page Up (-1) / Page Down (+1). True if the key was used.</summary>
        internal static bool Turn(int dir)
        {
            Sketchbook b = Sketch.Book;
            if (b == null)
                return false;
            if (SingletonBehaviour<GameManager>.Instance.isInIntro)
            {
                Speech.Say("The other pages open after the introduction.");
                return true;
            }
            if (b.garmentComponentSelectionUI.gameObject.activeInHierarchy)
            {
                Speech.Say("Close the list first.");
                return true;
            }
            PageTurnButton btn = dir < 0 ? PrevButton(b) : NextButton(b);
            if (btn == null || !btn.gameObject.activeSelf)
            {
                Speech.Say(dir < 0 ? "No more pages before this one." : "No more pages after this one.");
                return true;
            }
            if (dir < 0) b.ViewPreviousDress();
            else b.ViewNextDress();
            return true;
        }

        private static string Section(Sketchbook b) => Traverse.Create(b).Field("_viewingSection").GetValue()?.ToString();

        internal static string Kind(QuestState q)
        {
            QuestDefinition d = q?.questDefinition;
            if (d == null) return "Dress";
            if (d == QuestDefinition.RepeatingQuest) return "Off-the-rack design";
            if (d.IsGiftQuest()) return "Gift";
            return "Commission";
        }

        private static string Title(QuestState q)
        {
            QuestDefinition d = q.questDefinition;
            if (d == null || d == QuestDefinition.RepeatingQuest)
                return "";
            string client = d.questGiverCharacter != null ? d.questGiverCharacter.PrettyName : null;
            return ": " + Speech.Clean(d.PrettyName) + (client != null ? ", for " + client : "");
        }

        internal static string Describe(Sketchbook b)
        {
            PlayerProgress pp = PlayerProgress.Current;
            switch (Section(b))
            {
                case "Rolodex":
                    return "Friendship book: your clients, how well you know them, and the dresses you've made for them. Page Down for your dresses.";
                case "OldDresses":
                {
                    int i = OldIndex(b);
                    if (i < 0 || i >= pp.completedQuests.Count)
                        return "Finished dresses.";
                    QuestState q = pp.completedQuests[i];
                    return $"Finished dress {i + 1} of {pp.completedQuests.Count}. {Kind(q)}{Title(q)}. F4 reads the page.";
                }
                case "WipZone":
                {
                    QuestState q = pp.currentQuestState;
                    if (q == null)
                        return "Dress in progress.";
                    var gm = SingletonBehaviour<GameManager>.Instance;
                    bool drafted = gm.activeDress != null && gm.activeDress.state.bodiceDefinition != null;
                    string parts = b.SelectedBodice != null && b.SelectedSkirt != null
                        ? $" {b.SelectedBodice.PrettyName}, {b.SelectedSkirt.PrettyName}." : "";
                    return $"In progress, {pp.activeCommissionIndex + 1} of {pp.activeCommissions.Count}. {Kind(q)}{Title(q)}. " +
                           (drafted ? "Pattern drafted." : "Still designing.") + parts +
                           (pp.activeCommissions.Count > 1 ? " This is now the dress you work on in every room." : "");
                }
                case "NewDress":
                    return "New design page: start an off-the-rack dress of your own, to sell. Tab to the button to start one.";
            }
            return "";
        }

        private static System.Collections.IEnumerator Announce()
        {
            yield return null; // Show() fills the page title after drawing it
            Sketchbook b = Sketch.Book;
            if (b != null)
                Speech.Say(Describe(b));
        }

        // Anything that turns the page: Page Up/Down, the tabs, a dress in the friendship book.
        [HarmonyPatch]
        private static class TurnPatch
        {
            private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                foreach (string m in new[] { "ViewPreviousDress", "ViewNextDress", "GoToNewDress", "GoToActiveDress",
                                             "CreateNewNonCommissionedQuest", "RolodexButtonClicked", "OnDressHistoryItemClicked" })
                    yield return AccessTools.Method(typeof(Sketchbook), m);
            }

            private static void Prefix() => _turning = true;
        }

        [HarmonyPatch(typeof(Sketchbook), nameof(Sketchbook.UpdateDisplay))]
        private static class ShownPatch
        {
            private static void Postfix()
            {
                if (!_turning)
                    return;
                _turning = false;
                UINav.ResetFocus();
                Plugin.Instance.StartCoroutine(Announce());
            }
        }
    }
}
