using Febucci.UI;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// All of the mod's keys.
    ///   Arrows or Tab: move through what's on screen (rooms that use arrows for their own
    ///     job - the tape, the shop shelf, a piece on the cutting table, sewing - say so).
    ///   Enter: choose.  H: help for this screen.  M: money, rank and your commission.
    ///   R: repeat (except while sewing, where R restarts the seam).  1-7: rooms.
    ///   F4 reads the whole screen; F5/F6 sewing assist and hum; F7 this room's details.
    /// </summary>
    internal static class Keys
    {
        /// <summary>True on frames where the mod used an arrow key, so the game ignores it.</summary>
        internal static int ArrowFrame = -1;

        internal static void Handle()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Function keys work everywhere, even while typing a name.
            if (Input.GetKeyDown(KeyCode.F1)) Screens.Help();
            if (Input.GetKeyDown(KeyCode.F2)) Speech.Say(Speech.Last);
            if (Input.GetKeyDown(KeyCode.F3)) Rooms.Status();
            if (Input.GetKeyDown(KeyCode.F4)) TextWatch.ReadScreen();
            if (Input.GetKeyDown(KeyCode.F5)) Sewing.ToggleAssist();
            if (Input.GetKeyDown(KeyCode.F6)) Sewing.ToggleTone();
            if (Input.GetKeyDown(KeyCode.F7)) RoomDetails();
            if (Input.GetKeyDown(KeyCode.F8)) UINav.Dump();

            if (UINav.EditingText)
                return;

            // Room-specific keys first (they return true if they used the key).
            if (Measuring.HandleKeys(shift)) { ArrowFrame = Time.frameCount; return; }
            if (ShopAccess.HandleKeys(shift)) { ArrowFrame = Time.frameCount; return; }
            if (Cutting.HandleKeys(shift)) { ArrowFrame = Time.frameCount; return; }

            if (Input.GetKeyDown(KeyCode.H)) Screens.Help();
            if (Input.GetKeyDown(KeyCode.M)) Rooms.Status();
            if (Input.GetKeyDown(KeyCode.R) && !Sewing.InSeam) Speech.Say(Speech.Last);
            if (Input.GetKeyDown(KeyCode.T) && Sketch.Book != null) Sketch.SayStyles();

            if (Input.GetKeyDown(KeyCode.Tab))
                Do(shift ? "prev" : "next");
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Do("enter");

            // Arrows: menus everywhere except at the sewing machine, where they steer.
            if (!Sewing.InSeam)
            {
                bool up = Input.GetKeyDown(KeyCode.UpArrow), down = Input.GetKeyDown(KeyCode.DownArrow);
                bool left = Input.GetKeyDown(KeyCode.LeftArrow), right = Input.GetKeyDown(KeyCode.RightArrow);
                if (up || down || left || right)
                {
                    ArrowFrame = Time.frameCount;
                    if (Dialogue.OptionsActive)
                        Dialogue.MoveOption(up || left ? -1 : 1);
                    else if (left || right)
                    {
                        // Left/Right change a slider or list that has focus; otherwise they move too.
                        if (!UINav.AdjustCurrent(left ? -1 : 1))
                            Do(left ? "prev" : "next");
                    }
                    else
                        Do(up ? "prev" : "next");
                }
            }

            for (int n = 1; n <= 9; n++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + n) || Input.GetKeyDown(KeyCode.Keypad0 + n))
                    Do(n.ToString());
        }

        /// <summary>One entry point shared by real keys and the test command file.</summary>
        internal static void Do(string action)
        {
            switch (action)
            {
                case "next": UINav.Move(1); return;
                case "prev": UINav.Move(-1); return;
                case "enter": Enter(); return;
            }
            if (action.Length == 1 && char.IsDigit(action[0]))
            {
                int n = action[0] - '0';
                if (Dialogue.OptionsActive)
                    Dialogue.Choose(n - 1);
                else if (n >= 1 && n <= 7)
                    Rooms.GoTo(n);
            }
        }

        private static void Enter()
        {
            if (Sewing.TryCloseNote())
                return;
            if (TryCloseTip())
                return;
            var catcher = TextWatch.WaitingCatcher();
            if (catcher != null)
            {
                catcher.onClick.Invoke();
                return;
            }
            if (Dialogue.OptionsActive)
            {
                Dialogue.ChooseHighlighted();
                return;
            }
            if (Dialogue.LineActive)
            {
                Dialogue.Advance();
                return;
            }
            if (TryAdvanceTextBox())
                return;
            if (ShopAccess.TakeDownCurrent())
                return;
            UINav.ActivateCurrent();
        }

        /// <summary>A modal tip blocks everything else; Enter presses its Continue button.</summary>
        private static bool TryCloseTip()
        {
            foreach (TutorialMessage t in Object.FindObjectsByType<TutorialMessage>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled)
                    continue;
                var accept = Traverse.Create(t).Field("acceptButton").GetValue<GameObject>();
                if (accept == null || !accept.activeInHierarchy)
                    continue;
                var btn = accept.GetComponentInChildren<UnityEngine.UI.Button>();
                UINav.Click(btn != null ? btn.gameObject : accept);
                return true;
            }
            return false;
        }

        /// <summary>Story boxes listen for Space or a mouse click; make Enter work too.</summary>
        private static bool TryAdvanceTextBox()
        {
            foreach (TextBox box in Object.FindObjectsByType<TextBox>(FindObjectsSortMode.None))
            {
                if (!box.isActiveAndEnabled)
                    continue;
                var grp = Traverse.Create(box).Field("group").GetValue<CanvasGroup>();
                if (grp != null && grp.alpha < 0.05f)
                    continue;
                var tw = Traverse.Create(box).Field("typewriter").GetValue<TypewriterByCharacter>();
                if (tw != null && tw.isShowingText)
                    tw.SkipTypewriter();
                else
                    Traverse.Create(box).Method("HideBox").GetValue();
                return true;
            }
            return false;
        }

        /// <summary>F7: the details of whatever this room is about.</summary>
        internal static void RoomDetails()
        {
            if (Cutting.Room != null) { Speech.Say(Cutting.Summary()); return; }
            if (Sketch.Book != null) { Sketch.SayStyles(); return; }
            if (Measuring.Active) { Speech.Say(Measuring.Summary()); return; }
            Sewing.SayProgress();
        }
    }
}
