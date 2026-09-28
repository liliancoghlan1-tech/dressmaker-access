using System.Collections.Generic;
using System.Linq;
using Framework;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// The sewing machine and the seam list on the mannequin.
    ///  - Every seam button on the dress (front or back) is listed for Tab.
    ///  - Starting a seam says which pieces it joins and the keys.
    ///  - A steering hum sounds from the side the line is heading; quiet = on course.
    ///    Pan follows the game's own assist logic: line to the left = press Left.
    ///  - Progress every quarter, speed changes, mistakes and the result are spoken.
    /// </summary>
    internal static class Sewing
    {
        private static Tone _tone;
        private static int _lastQuarter;
        private static float _lastSpeed = -1f;
        private static float _speedSayAt;
        private static bool _assistDefaultDone;
        private static float _nextToneLog;

        private static readonly AccessTools.FieldRef<SewingController, bool> Finished =
            AccessTools.FieldRefAccess<SewingController, bool>("finished");
        private static readonly AccessTools.FieldRef<SewingController, List<Vector2>> PatternLine =
            AccessTools.FieldRefAccess<SewingController, List<Vector2>>("patternLine");
        private static readonly AccessTools.FieldRef<SewingController, int> ProgressSeg =
            AccessTools.FieldRefAccess<SewingController, int>("patternProgressSeg");
        private static readonly AccessTools.FieldRef<SewingController, float> ProgressT =
            AccessTools.FieldRefAccess<SewingController, float>("patternProgressT");
        private static readonly AccessTools.FieldRef<SewingController, float> SeamLength =
            AccessTools.FieldRefAccess<SewingController, float>("seamLength");
        private static readonly AccessTools.FieldRef<SewingController, int> Mistakes =
            AccessTools.FieldRefAccess<SewingController, int>("mistakes");
        private static readonly AccessTools.FieldRef<SewingController, SewingSpeedControl> SpeedControl =
            AccessTools.FieldRefAccess<SewingController, SewingSpeedControl>("speedControl");
        private static readonly AccessTools.FieldRef<SewingController, bool> ShowingNote =
            AccessTools.FieldRefAccess<SewingController, bool>("_isShowingNote");

        private static readonly System.Reflection.MethodInfo ClosestPoint =
            AccessTools.Method(typeof(SewingController), "ClosestPointOnPatternLine");
        private static readonly System.Reflection.MethodInfo AssistTarget =
            AccessTools.Method(typeof(SewingController), "GetAssistTarget");

        internal static void Init()
        {
            UINav.Exclude = go => go.GetComponentInParent<SewButton>() != null;
            UINav.ExtraSources.Add(SeamItems);
            UINav.ExtraSources.Add(Sketch.PencilItems);
            UINav.ExtraSources.Add(Sketch.ListCloseItem);
            UINav.ExtraSources.Add(AccessoryAccess.Items);
            UINav.ExtraSources.Add(Cutting.TableItems);
        }

        private static SewingController Controller
        {
            get
            {
                try
                {
                    var gm = Rooms.GameManagerOrNull();
                    if (gm == null || gm.CurrentScene != GameManager.Scene.Sewing)
                        return null;
                    return SingletonBehaviour<SewingController>.Instance;
                }
                catch { return null; }
            }
        }

        internal static bool InSeam
        {
            get
            {
                var c = Controller;
                return c != null && !Finished(c) && PatternLine(c).Count >= 2;
            }
        }

        // ---------- seam list on the mannequin ----------

        private static List<UINav.Item> SeamItems()
        {
            var list = new List<UINav.Item>();
            var gm = Rooms.GameManagerOrNull();
            if (gm == null || gm.CurrentScene != GameManager.Scene.Mannequin)
                return list;
            Camera cam = Camera.main;
            foreach (SewButton sb in Object.FindObjectsByType<SewButton>(FindObjectsSortMode.None))
            {
                if (!sb.isActiveAndEnabled || sb.join == null)
                    continue;
                Vector2 p = cam != null ? (Vector2)cam.WorldToScreenPoint(sb.transform.position) : Vector2.zero;
                SewButton captured = sb;
                string side = SeamWhere(sb);
                list.Add(new UINav.Item
                {
                    Go = sb.gameObject,
                    Screen = p,
                    Label = "Sew seam: " + JoinName(sb.join) + side,
                    OnActivate = () => captured.ItsSewingTime(),
                });
            }
            return list;
        }

        /// <summary>Where on the body a seam button sits, e.g. ", upper, your left, on the back".</summary>
        private static string SeamWhere(SewButton sb)
        {
            MannequinScene ms;
            try { ms = SingletonBehaviour<MannequinScene>.Instance; } catch { ms = null; }
            Transform body = ms != null && ms.mannequin != null ? ms.mannequin.transform : null;
            string s = "";
            if (body != null)
            {
                Vector3 local = body.InverseTransformPoint(sb.transform.position);
                Camera cam = Camera.main;
                float x = cam != null ? cam.transform.InverseTransformPoint(sb.transform.position).x - cam.transform.InverseTransformPoint(body.position).x : local.x;
                s += x < -0.03f ? ", your left" : x > 0.03f ? ", your right" : ", centre";
            }
            if (sb.transform.position.z > 0f)
                s += ", on the back";
            return s;
        }

        internal static string JoinName(PanelJoin j)
        {
            string a = j.panel != null ? j.panel.DisplayName : "?";
            string b = j.connectedPanel != null ? j.connectedPanel.DisplayName : "?";
            return a == b ? a : a + " to " + b;
        }

        // ---------- hooks ----------

        [HarmonyPatch(typeof(SewingController), nameof(SewingController.InitializeWith))]
        private static class StartPatch
        {
            private static void Postfix(PanelJoin join)
            {
                _lastQuarter = 0;
                bool assist = PlayerOptions.Current.sewingAssist;
                Speech.Say($"Sewing {JoinName(join)}. Hold Space to sew. " +
                           (assist ? "Sewing assist is steering for you. " : "Steer with Left and Right arrows, towards the hum. ") +
                           "Up and Down change speed. F7 for progress.");
            }
        }

        [HarmonyPatch(typeof(SewingController), "Finish")]
        private static class FinishPatch
        {
            private static void Prefix(SewingController __instance, bool success)
            {
                if (success)
                {
                    _tone?.Silence();
                    Speech.Say($"Seam finished! Accuracy {Mathf.RoundToInt(__instance.AverageAccuracy)} percent.");
                    Rooms.QueueSceneUntil = Time.unscaledTime + 0.5f;
                }
            }

            private static void Postfix(SewingController __instance, bool success)
            {
                _tone?.Silence();
                if (success)
                {
                }
                else
                {
                    int m = Mistakes(__instance);
                    Speech.Say($"Off the line! Going back to your last good stitch. {m} {(m == 1 ? "mistake" : "mistakes")} so far.");
                }
            }
        }

        [HarmonyPatch(typeof(SewingController), "RestartSegment")]
        private static class RestartPatch
        {
            private static void Postfix()
            {
                _lastQuarter = 0;
                if (InSeam)
                    Speech.Say("Seam restarted from the beginning.");
            }
        }

        // ---------- per frame ----------

        internal static void Tick()
        {
            if (!_assistDefaultDone)
            {
                _assistDefaultDone = true;
                if (!Plugin.AssistDefaultApplied.Value)
                {
                    PlayerOptions.Current.sewingAssist = true;
                    PlayerOptions.Save();
                    Plugin.AssistDefaultApplied.Value = true;
                    Plugin.Log.LogInfo("Sewing assist switched on (first run).");
                }
                Init();
            }

            var c = Controller;
            if (c == null || Finished(c) || PatternLine(c).Count < 2)
            {
                _tone?.Silence();
                _lastSpeed = -1f;
                return;
            }

            // Progress along the seam, in quarters.
            float frac = Progress(c);
            int quarter = Mathf.FloorToInt(frac * 4f);
            if (quarter > _lastQuarter && quarter < 4)
            {
                _lastQuarter = quarter;
                Speech.Queue((quarter * 25) + " percent");
            }

            // Speed: say it once the key is let go.
            SewingSpeedControl sc = SpeedControl(c);
            if (sc != null)
            {
                float sp = sc.SpeedSetting;
                if (_lastSpeed >= 0f && Mathf.Abs(sp - _lastSpeed) > 0.001f)
                    _speedSayAt = Time.unscaledTime + 0.3f;
                _lastSpeed = sp;
                if (_speedSayAt > 0f && Time.unscaledTime >= _speedSayAt)
                {
                    _speedSayAt = 0f;
                    Speech.Say("Speed " + Mathf.RoundToInt(sp * 100f) + " percent");
                }
            }

            UpdateTone(c);
        }

        internal static float Progress(SewingController c)
        {
            List<Vector2> line = PatternLine(c);
            float total = SeamLength(c);
            if (line.Count < 2 || total <= 0f)
                return 0f;
            int seg = Mathf.Clamp(ProgressSeg(c), 0, line.Count - 2);
            float done = 0f;
            for (int i = 0; i < seg; i++)
                done += Vector2.Distance(line[i], line[i + 1]);
            done += Vector2.Distance(line[seg], line[seg + 1]) * ProgressT(c);
            return Mathf.Clamp01(done / total);
        }

        private static void UpdateTone(SewingController c)
        {
            if (!Plugin.SteeringTone.Value)
            {
                _tone?.Silence();
                return;
            }
            if (_tone == null)
                _tone = new Tone("SewingGuide", 220f);

            Vector3 target = (Vector3)AssistTarget.Invoke(c, null);
            Vector3 closest = (Vector3)ClosestPoint.Invoke(c, null);

            // Where the line is heading: -1..1, positive = press Left (same sign the assist uses).
            float dir = target.sqrMagnitude > 1e-10f ? target.normalized.x : 0f;
            // How far off the line the needle is now: 0 = on it, 1 = about to fail.
            float err = Mathf.Clamp01(Mathf.Max(0f, Mathf.Abs(closest.x) - c.tolerance) / Mathf.Max(0.0001f, c.maxError));

            float turn = Mathf.Clamp01((Mathf.Abs(dir) - 0.12f) / 0.6f);
            float loud = Mathf.Max(turn, err);
            float volume = loud <= 0f ? 0f : 0.08f + 0.3f * loud;
            float pitch = 1f + 0.8f * err;           // rises as you near the edge
            float pan = dir > 0 ? -1f : 1f;          // sound comes from the side to steer to
            pan *= Mathf.Clamp01(0.4f + Mathf.Abs(dir));
            _tone.Set(volume, pitch, pan);
            if (Plugin.TestCommands.Value && Time.unscaledTime >= _nextToneLog)
            {
                _nextToneLog = Time.unscaledTime + 0.5f;
                Plugin.Log.LogInfo($"[tone] dir={dir:0.00} err={err:0.00} vol={volume:0.00} pan={pan:0.00} progress={Progress(c):0.00}");
            }
        }

        internal static void SayProgress()
        {
            var c = Controller;
            if (c == null || Finished(c))
            {
                Speech.Say("Not sewing right now.");
                return;
            }
            Speech.Say($"{Mathf.RoundToInt(Progress(c) * 100)} percent sewn. Accuracy {Mathf.RoundToInt(c.AverageAccuracy)} percent. " +
                       $"Speed {Mathf.RoundToInt((SpeedControl(c)?.SpeedSetting ?? 0f) * 100)} percent.");
        }

        internal static void ToggleAssist()
        {
            PlayerOptions.Current.sewingAssist = !PlayerOptions.Current.sewingAssist;
            PlayerOptions.Save();
            Speech.Say(PlayerOptions.Current.sewingAssist ? "Sewing assist on. The machine steers for you." : "Sewing assist off. You steer.");
        }

        internal static void ToggleTone()
        {
            Plugin.SteeringTone.Value = !Plugin.SteeringTone.Value;
            Speech.Say(Plugin.SteeringTone.Value ? "Steering hum on." : "Steering hum off.");
        }

        /// <summary>The sewing note (tutorial paper) waits for a real mouse click; Enter closes it.</summary>
        internal static bool TryCloseNote()
        {
            var gm = Rooms.GameManagerOrNull();
            if (gm == null || gm.CurrentScene != GameManager.Scene.Sewing)
                return false;
            SewingController c;
            try { c = SingletonBehaviour<SewingController>.Instance; } catch { return false; }
            if (c == null || !c.isActiveAndEnabled || !ShowingNote(c))
                return false;
            c.HideNote();
            return true;
        }
    }
}
