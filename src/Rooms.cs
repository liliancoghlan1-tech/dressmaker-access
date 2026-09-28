using System.Text;
using Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DressmakerAccess
{
    /// <summary>
    /// Which room you are in, keys to go to each room, and the status summary.
    /// Rooms are reached by pressing the game's own buttons, so anything the
    /// game has locked (e.g. during the tutorial) stays locked.
    /// </summary>
    internal static class Rooms
    {
        private static bool _hooked;
        private static Button _pendingRoom;
        private static int _pendingFrame;

        internal static string SceneName()
        {
            var gm = GameManagerOrNull();
            if (gm == null)
                return SceneManager.GetActiveScene().name;
            return Friendly(gm.CurrentScene);
        }

        internal static GameManager GameManagerOrNull()
        {
            try { return SingletonBehaviour<GameManager>.Instance; }
            catch { return null; }
        }

        internal static string Friendly(GameManager.Scene s)
        {
            switch (s)
            {
                case GameManager.Scene.FrontDesk: return "Front desk";
                case GameManager.Scene.Sketchbook: return "Sketchbook";
                case GameManager.Scene.CuttingRoom: return "Cutting table";
                case GameManager.Scene.Mannequin: return "Mannequin";
                case GameManager.Scene.Sewing: return "Sewing machine";
                case GameManager.Scene.Store: return "Fabric shop";
                case GameManager.Scene.Photo: return "Photo studio";
                case GameManager.Scene.Measuring: return "Measuring";
                case GameManager.Scene.MannequinSizing: return "Mannequin sizing";
                case GameManager.Scene.Ending: return "Ending";
            }
            return s.ToString();
        }

        internal static float QueueSceneUntil;

        internal static void OnSceneChanged(GameManager.Scene s)
        {
            if (UnityEngine.Time.unscaledTime < QueueSceneUntil)
                Speech.Queue(Friendly(s));
            else
                Speech.Say(Friendly(s));
            string intro = Screens.Intro(s);
            if (intro != null)
                Speech.Later(intro, 0.3f);
        }

        internal static void Tick()
        {
            if (!_hooked)
            {
                _hooked = true;
                SceneManager.sceneLoaded += (scene, mode) =>
                {
                    if (scene.name == "TitleScreen")
                        Speech.Say("Title screen. Arrows or Tab move through the menu, Enter chooses. H for help.");
                };
            }

            // Second half of a room jump: we went back to the front desk first.
            if (_pendingRoom != null && Time.frameCount >= _pendingFrame)
            {
                Button b = _pendingRoom;
                _pendingRoom = null;
                PressRoomButton(b);
            }
        }

        // ---------- room keys ----------

        internal static void GoTo(int number)
        {
            var gm = GameManagerOrNull();
            FrontDesk fd = null;
            try { fd = SingletonBehaviour<FrontDesk>.Instance; } catch { }
            if (gm == null || fd == null)
                return;

            if (number == 1)
            {
                if (gm.CurrentScene == GameManager.Scene.FrontDesk)
                {
                    Speech.Say("You are at the front desk.");
                    return;
                }
                GameObject back = gm.FrontDeskButton;
                if (back == null || !back.activeInHierarchy)
                {
                    Speech.Say("You can't go back to the front desk right now.");
                    return;
                }
                var btn = back.GetComponentInChildren<Button>();
                UINav.Click(btn != null ? btn.gameObject : back);
                return;
            }

            Button target;
            switch (number)
            {
                case 2: target = fd.measuringButton; break;
                case 3: target = fd.sketchBookButton; break;
                case 4: target = fd.fabricStoreButton; break;
                case 5: target = fd.cuttingRoomButton; break;
                case 6: target = fd.mannequinButton; break;
                case 7: target = fd.sewingDeskButton; break;
                default: return;
            }
            if (target == null)
                return;

            if (gm.CurrentScene != GameManager.Scene.FrontDesk)
            {
                GameObject back = gm.FrontDeskButton;
                if (back == null || !back.activeInHierarchy)
                {
                    Speech.Say("You can't leave this room right now.");
                    return;
                }
                var btn = back.GetComponentInChildren<Button>();
                UINav.Click(btn != null ? btn.gameObject : back);
                _pendingRoom = target;
                _pendingFrame = Time.frameCount + 3;
                return;
            }
            PressRoomButton(target);
        }

        private static void PressRoomButton(Button b)
        {
            if (!b.gameObject.activeInHierarchy || !b.enabled || !b.IsInteractable())
            {
                Speech.Say("That room isn't open yet.");
                return;
            }
            UINav.Click(b.gameObject);
        }

        // ---------- status ----------

        internal static void Status()
        {
            var gm = GameManagerOrNull();
            if (gm == null)
            {
                Speech.Say(SceneName());
                return;
            }
            var sb = new StringBuilder();
            sb.Append(Friendly(gm.CurrentScene)).Append(". ");
            sb.Append(Measuring.Summary());
            sb.Append(Cutting.Summary());
            PlayerProgress p = PlayerProgress.Current;
            if (p != null)
            {
                sb.Append(p.gold).Append(" gold. ");
                sb.Append("Prestige level ").Append(Mathf.FloorToInt(p.prestige)).Append(". ");
                QuestState q = p.currentQuestState;
                if (q != null && q.questDefinition != null)
                {
                    QuestDefinition d = q.questDefinition;
                    sb.Append("Commission: ").Append(d.PrettyName);
                    if (d.questGiverCharacter != null)
                        sb.Append(", for ").Append(d.CharacterName);
                    sb.Append(". ");
                    string req = Speech.Clean(d.GetRequirementsString().Replace("\n", ". "));
                    var dress = gm.activeDress;
                    if (dress != null)
                        sb.Append($"The dress so far scores quality {Mathf.RoundToInt(dress.GetQualityScore())}. ");
                    if (!string.IsNullOrEmpty(req))
                        sb.Append("Needs: ").Append(req).Append(". ");
                }
                else
                {
                    sb.Append("No commission right now. ");
                }
            }
            Speech.Say(sb.ToString());
        }
    }
}
