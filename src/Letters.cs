using System.Collections;
using Framework;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DressmakerAccess
{
    /// <summary>
    /// The two typewriter screens at the front desk: the Spilt Tea newspaper (after some
    /// commissions) and the letter that comes when every commission is done. Both type
    /// their text out and wait for a mouse click at each paragraph, with no button to
    /// press until the page is finished - so with the keyboard alone they looked stuck.
    /// Enter now skips to the end of the page (the text has already been read out), and
    /// then Tab reaches Next Page, Continue or Close as usual.
    /// </summary>
    internal static class Letters
    {
        private static readonly AccessTools.FieldRef<GossipReport, Button> GossipNext =
            AccessTools.FieldRefAccess<GossipReport, Button>("nextPageButton");
        private static readonly AccessTools.FieldRef<GossipReport, Button> GossipContinue =
            AccessTools.FieldRefAccess<GossipReport, Button>("continueButton");
        private static readonly AccessTools.FieldRef<GossipReport, RectTransform> GossipPaper =
            AccessTools.FieldRefAccess<GossipReport, RectTransform>("newspaper");
        private static readonly AccessTools.FieldRef<GossipReport, bool> GossipDone =
            AccessTools.FieldRefAccess<GossipReport, bool>("_canContinue");

        private static readonly AccessTools.FieldRef<KnighthoodScene, Image> LetterImage =
            AccessTools.FieldRefAccess<KnighthoodScene, Image>("letter");
        private static readonly AccessTools.FieldRef<KnighthoodScene, GameObject> LetterClose =
            AccessTools.FieldRefAccess<KnighthoodScene, GameObject>("closeButton");
        private static readonly AccessTools.FieldRef<KnighthoodScene, Button> LetterTurnOver =
            AccessTools.FieldRefAccess<KnighthoodScene, Button>("_turnOverButton");
        private static readonly AccessTools.FieldRef<KnighthoodScene, TextMeshProUGUI[]> LetterPages =
            AccessTools.FieldRefAccess<KnighthoodScene, TextMeshProUGUI[]>("_pages");

        private static bool _skipping;
        private static GossipReport _hinted;

        /// <summary>Called every frame: one hint when the newspaper appears.</summary>
        internal static void Update()
        {
            GossipReport g = OpenGossip();
            if (g == null)
            {
                _hinted = null;
                return;
            }
            RectTransform paper = GossipPaper(g);
            if (_hinted != g && paper != null && paper.gameObject.activeInHierarchy)
            {
                _hinted = g;
                Speech.Queue("That was the Spilt Tea newspaper. Enter skips to the end of the page, then Tab to Next Page or Continue. R repeats the last line.");
            }
        }

        private static GossipReport OpenGossip()
        {
            foreach (GossipReport g in Object.FindObjectsByType<GossipReport>(FindObjectsSortMode.None))
                if (g.isActiveAndEnabled && !GossipDone(g))
                    return g;
            return null;
        }

        private static bool Showing(Button b) => b != null && b.gameObject.activeInHierarchy;

        private static bool GossipWaiting(GossipReport g)
        {
            RectTransform paper = GossipPaper(g);
            return paper != null && paper.gameObject.activeInHierarchy && !Showing(GossipNext(g)) && !Showing(GossipContinue(g));
        }

        private static bool LetterWaiting(KnighthoodScene k)
        {
            Image letter = LetterImage(k);
            GameObject close = LetterClose(k);
            Button turn = LetterTurnOver(k);
            return letter != null && letter.gameObject.activeInHierarchy
                && (close == null || !close.activeInHierarchy) && (turn == null || !turn.enabled);
        }

        /// <summary>Enter while a page is still typing itself out: skip to its end.</summary>
        internal static bool TryEnter()
        {
            if (_skipping)
                return true;
            GossipReport g = OpenGossip();
            if (g != null && GossipWaiting(g))
            {
                Plugin.Instance.StartCoroutine(Skip(() => g != null && g.isActiveAndEnabled && GossipWaiting(g),
                    () => Showing(GossipNext(g)) ? "End of the page. Tab to Next Page for the rest." : "End of the newspaper. Tab to Continue."));
                return true;
            }
            KnighthoodScene k = SingletonBehaviour<KnighthoodScene>.HasInstance ? SingletonBehaviour<KnighthoodScene>.Instance : null;
            if (k != null && k.IsOpen && LetterWaiting(k))
            {
                Plugin.Instance.StartCoroutine(Skip(() => k != null && k.IsOpen && LetterWaiting(k),
                    () => LetterClose(k) != null && LetterClose(k).activeInHierarchy
                        ? "End of the letter. Tab to Close when you're ready."
                        : "End of this side. Tab to the letter and press Enter to turn it over."));
                return true;
            }
            return false;
        }

        // Both screens reveal a paragraph per click, so click every other frame until the page is done.
        private static IEnumerator Skip(System.Func<bool> waiting, System.Func<string> done)
        {
            _skipping = true;
            for (int i = 0; i < 600 && waiting(); i++)
            {
                VirtualInput.Click();
                yield return null;
                yield return null;
            }
            _skipping = false;
            yield return null;
            UINav.ResetFocus();
            Speech.Say(done());
        }

        internal static string Label(GameObject go)
        {
            if (!SingletonBehaviour<KnighthoodScene>.HasInstance)
                return null;
            KnighthoodScene k = SingletonBehaviour<KnighthoodScene>.Instance;
            if (!k.IsOpen)
                return null;
            if (go.name == "Envelope")
                return "The envelope: Enter opens the letter";
            Image letter = LetterImage(k);
            if (letter != null && go == letter.gameObject)
                return "The letter: Enter turns it over";
            if (LetterClose(k) != null && (go == LetterClose(k) || go.transform.IsChildOf(LetterClose(k).transform)))
                return "Close the letter";
            return null;
        }

        [HarmonyPatch(typeof(KnighthoodScene), nameof(KnighthoodScene.ShowKnighthoodEnvelope))]
        private static class EnvelopePatch
        {
            private static void Postfix() => Speech.Queue("A letter has arrived. Press Enter to open it.");
        }

        [HarmonyPatch(typeof(KnighthoodScene), nameof(KnighthoodScene.ShowLetter))]
        private static class LetterPatch
        {
            private static void Postfix(KnighthoodScene __instance)
            {
                TextMeshProUGUI[] pages = LetterPages(__instance);
                if (pages != null && pages.Length > 0 && pages[0] != null)
                    foreach (string para in pages[0].text.Split('\n'))
                    {
                        string s = Speech.Clean(para);
                        if (!string.IsNullOrEmpty(s))
                            Speech.Queue(s);
                    }
                Speech.Queue("Enter skips to the end of the writing; R repeats the last line.");
            }
        }
    }
}
