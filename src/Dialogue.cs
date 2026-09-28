using System.Linq;
using HarmonyLib;
using UnityEngine;
using Yarn.Unity;

namespace DressmakerAccess
{
    /// <summary>
    /// Customer conversations (Yarn Spinner). Every line is spoken as it starts,
    /// Enter finishes/advances the line (the game wants a mouse click), and
    /// replies are read out numbered and picked with 1-9.
    /// </summary>
    internal static class Dialogue
    {
        internal static bool LineActive;
        internal static DialogueOption[] Options;
        private static DialoguePresenter _presenter;
        private static int _lineCounter;
        private static int _secondClickLine = -1;
        private static int _secondClickFrame;
        private static float _advanceUntil;

        internal static bool OptionsActive => Options != null && Options.Length > 0;
        private static int _optIndex = -1;

        internal static void MoveOption(int dir)
        {
            if (!OptionsActive)
                return;
            _optIndex = _optIndex < 0 ? (dir > 0 ? 0 : Options.Length - 1) : (_optIndex + dir + Options.Length) % Options.Length;
            DialogueOption o = Options[_optIndex];
            Speech.Say($"{o.Line.TextWithoutCharacterName.Text.TrimEnd('.')}{(o.IsAvailable ? "" : ", not available")}. {_optIndex + 1} of {Options.Length}");
        }

        internal static void ChooseHighlighted()
        {
            if (!OptionsActive)
                return;
            if (_optIndex < 0)
            {
                RepeatOptions();
                return;
            }
            Choose(_optIndex);
        }
        internal static bool Active => LineActive || OptionsActive;

        [HarmonyPatch(typeof(DialoguePresenter), nameof(DialoguePresenter.RunLineAsync))]
        private static class LinePatch
        {
            private static void Prefix(DialoguePresenter __instance, LocalizedLine line)
            {
                if (!__instance.gameObject.activeInHierarchy || line == null)
                    return;
                _presenter = __instance;
                _lineCounter++;
                LineActive = true;
                Options = null;
                string name = line.CharacterName;
                if (string.IsNullOrEmpty(name) || name == "QuestGiver")
                {
                    name = null;
                    var ch = Traverse.Create(__instance).Field("currentCharacter").GetValue<CharacterDefinition>();
                    if (ch != null)
                        name = ch.PrettyName;
                }
                string text = line.TextWithoutCharacterName.Text;
                Speech.Say(string.IsNullOrEmpty(name) ? text : name + ": " + text);
            }
        }

        [HarmonyPatch(typeof(DialoguePresenter), nameof(DialoguePresenter.RunOptionsAsync))]
        private static class OptionsPatch
        {
            private static void Prefix(DialoguePresenter __instance, DialogueOption[] options)
            {
                if (!__instance.gameObject.activeInHierarchy || options == null)
                    return;
                _presenter = __instance;
                LineActive = false;
                Options = options;
                _optIndex = -1;
                var parts = options.Select((o, i) =>
                    $"{i + 1}: {o.Line.TextWithoutCharacterName.Text.TrimEnd('.')}{(o.IsAvailable ? "" : " (not available)")}");
                // Queue so the last line of dialogue is not cut off by the choices.
                Speech.Queue("Choose. " + string.Join(". ", parts) + ". Up and Down, then Enter, or press the number.");
            }
        }

        [HarmonyPatch(typeof(DialoguePresenter), nameof(DialoguePresenter.OnDialogueCompleteAsync))]
        private static class CompletePatch
        {
            private static void Prefix()
            {
                LineActive = false;
                Options = null;
            }
        }

        [HarmonyPatch(typeof(DialoguePresenter), nameof(DialoguePresenter.Hide))]
        private static class HidePatch
        {
            private static void Prefix()
            {
                LineActive = false;
                Options = null;
            }
        }

        /// <summary>Enter during a line: finish typing and move on, in one press.</summary>
        internal static void Advance()
        {
            var runner = Object.FindFirstObjectByType<DialogueRunner>();
            runner?.RequestHurryUpLine();
            VirtualInput.Click();
            // If the line was still typing, the first click only hurried it; click again shortly.
            _secondClickLine = _lineCounter;
            _secondClickFrame = Time.frameCount + 4;
            _advanceUntil = Time.unscaledTime + 1.5f;
        }

        internal static void Choose(int index)
        {
            if (!OptionsActive || _presenter == null)
                return;
            if (index < 0 || index >= Options.Length)
            {
                Speech.Say("There are only " + Options.Length + " choices.");
                return;
            }
            DialogueOption o = Options[index];
            if (!o.IsAvailable)
            {
                Speech.Say("That choice is not available.");
                return;
            }
            Traverse.Create(_presenter).Field("selectedOption").SetValue(o);
            Options = null;
            Speech.Say(o.Line.TextWithoutCharacterName.Text);
        }

        internal static void RepeatOptions()
        {
            if (!OptionsActive)
                return;
            var parts = Options.Select((o, i) => $"{i + 1}: {o.Line.TextWithoutCharacterName.Text}");
            Speech.Say("Choose. " + string.Join(". ", parts));
        }

        internal static void Tick()
        {
            // Keep clicking (every 6 frames, up to ~1.5s) until the line really moves on:
            // a click that lands while the typewriter is mid-pause is ignored by the game.
            if (_secondClickLine >= 0 && Time.frameCount >= _secondClickFrame)
            {
                if (_secondClickLine == _lineCounter && LineActive && !OptionsActive &&
                    Time.unscaledTime < _advanceUntil)
                {
                    var runner = Object.FindFirstObjectByType<DialogueRunner>();
                    runner?.RequestHurryUpLine();
                    VirtualInput.Click();
                    _secondClickFrame = Time.frameCount + 6;
                }
                else
                {
                    _secondClickLine = -1;
                }
            }
            // If the dialogue box disappeared without telling us, stop claiming Enter.
            if (LineActive && (_presenter == null || !_presenter.isActiveAndEnabled))
                LineActive = false;
        }
    }
}
