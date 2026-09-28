using System.Collections.Generic;
using Framework;
using HarmonyLib;
using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// Measuring the client, then sizing the mannequin to match.
    ///
    /// Client: Up/Down slide the tape measure up and down the body. The mod says
    /// which area the tape is in (bust, waist, hips), the measurement there, and
    /// when the tape is on the guide line the game draws for that area. Holding
    /// still for half a second records it, exactly as with the mouse.
    ///
    /// Mannequin: the same, plus Left/Right turn the knob for the area the tape
    /// is on; the mannequin's measurement and the client's are both spoken.
    /// </summary>
    internal static class Measuring
    {
        private const float Step = 0.01f;       // world units per arrow press
        private static float _tapeY = float.NaN;
        private static PersonMeasurer _placedOn;
        private static float _sayAt;
        private static string _pending;
        private static bool _quietSetters;
        private static bool _allAnnounced;

        private static readonly AccessTools.FieldRef<PersonMeasurer, bool> TapeOnBody =
            AccessTools.FieldRefAccess<PersonMeasurer, bool>("_isTapeOnBody");
        private static readonly AccessTools.FieldRef<PersonMeasurer, MeasuringTape> TapeStart =
            AccessTools.FieldRefAccess<PersonMeasurer, MeasuringTape>("measuringTapeStart");
        private static readonly AccessTools.FieldRef<PersonMeasurer, MeasuringTape> TapeEnd =
            AccessTools.FieldRefAccess<PersonMeasurer, MeasuringTape>("measuringTapeEnd");
        private static readonly AccessTools.FieldRef<PersonMeasurer, MeasuringTapeIcon> Icon =
            AccessTools.FieldRefAccess<PersonMeasurer, MeasuringTapeIcon>("tapeIcon");
        private static readonly AccessTools.FieldRef<PersonMeasurer, Camera> Cam =
            AccessTools.FieldRefAccess<PersonMeasurer, Camera>("cam");
        private static readonly AccessTools.FieldRef<PersonMeasurer, Transform> Armpit =
            AccessTools.FieldRefAccess<PersonMeasurer, Transform>("armpitHeight");
        private static readonly AccessTools.FieldRef<PersonMeasurer, RectTransform> BustHit =
            AccessTools.FieldRefAccess<PersonMeasurer, RectTransform>("bustHitbox");
        private static readonly AccessTools.FieldRef<PersonMeasurer, RectTransform> WaistHit =
            AccessTools.FieldRefAccess<PersonMeasurer, RectTransform>("waistHitbox");
        private static readonly AccessTools.FieldRef<PersonMeasurer, RectTransform> HipsHit =
            AccessTools.FieldRefAccess<PersonMeasurer, RectTransform>("hipsHitbox");
        private static readonly AccessTools.FieldRef<PersonMeasurer, RectTransform> BustMarker =
            AccessTools.FieldRefAccess<PersonMeasurer, RectTransform>("bustMarker");
        private static readonly AccessTools.FieldRef<PersonMeasurer, RectTransform> WaistMarker =
            AccessTools.FieldRefAccess<PersonMeasurer, RectTransform>("waistMarker");
        private static readonly AccessTools.FieldRef<PersonMeasurer, RectTransform> HipsMarker =
            AccessTools.FieldRefAccess<PersonMeasurer, RectTransform>("hipsMarker");

        private static readonly AccessTools.FieldRef<MeasurePaper, RectTransform> GhostBust =
            AccessTools.FieldRefAccess<MeasurePaper, RectTransform>("ghostBust");
        private static readonly AccessTools.FieldRef<MeasurePaper, RectTransform> GhostWaist =
            AccessTools.FieldRefAccess<MeasurePaper, RectTransform>("ghostWaist");
        private static readonly AccessTools.FieldRef<MeasurePaper, RectTransform> GhostHips =
            AccessTools.FieldRefAccess<MeasurePaper, RectTransform>("ghostHips");
        private static readonly AccessTools.FieldRef<MeasurePaper, float> BandTolerance =
            AccessTools.FieldRefAccess<MeasurePaper, float>("bandTolerance");
        private static readonly AccessTools.FieldRef<MeasurePaper, MeasuringValue> MBust =
            AccessTools.FieldRefAccess<MeasurePaper, MeasuringValue>("mannequinBustValue");
        private static readonly AccessTools.FieldRef<MeasurePaper, MeasuringValue> MWaist =
            AccessTools.FieldRefAccess<MeasurePaper, MeasuringValue>("mannequinWaistValue");
        private static readonly AccessTools.FieldRef<MeasurePaper, MeasuringValue> MHips =
            AccessTools.FieldRefAccess<MeasurePaper, MeasuringValue>("mannequinHipsValue");

        private static readonly AccessTools.FieldRef<Gear, float> GearValue =
            AccessTools.FieldRefAccess<Gear, float>("_value");
        private static readonly System.Reflection.MethodInfo GearSetBodyPart =
            AccessTools.Method(typeof(Gear), "SetBodyPart");
        private static readonly System.Reflection.PropertyInfo DraggingProp =
            AccessTools.Property(typeof(PersonMeasurer), nameof(PersonMeasurer.IsDraggingTape));

        private static MeasuringScene Scene()
        {
            var gm = Rooms.GameManagerOrNull();
            if (gm == null)
                return null;
            if (gm.CurrentScene != GameManager.Scene.Measuring && gm.CurrentScene != GameManager.Scene.MannequinSizing)
                return null;
            return Object.FindFirstObjectByType<MeasuringScene>();
        }

        internal static bool Active => Scene() != null;

        // Test harness entry points (steps, may be negative).
        internal static void CmdTape(int steps) { var ms = Scene(); if (ms != null) MoveTape(ms, Step * steps); }
        internal static void CmdLine(int dir) { var ms = Scene(); if (ms != null) JumpLine(ms, dir); }
        internal static void CmdGear(float delta) { var ms = Scene(); if (ms != null) TurnGear(ms, delta); }

        // ---------- keys ----------

        /// <summary>Returns true if the key was used here.</summary>
        internal static bool HandleKeys(bool shift)
        {
            MeasuringScene ms = Scene();
            if (ms == null || Dialogue.Active || UINav.EditingText)
                return false;
            float mult = shift ? 5f : 1f;
            if (ms.IsMannequinScene)
            {
                // Sizing: the three lines are all that matter, so Up/Down go line to line.
                if (Input.GetKeyDown(KeyCode.UpArrow)) { JumpLine(ms, -1); return true; }
                if (Input.GetKeyDown(KeyCode.DownArrow)) { JumpLine(ms, 1); return true; }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveTape(ms, Step * mult); return true; }
                if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveTape(ms, -Step * mult); return true; }
                if (Input.GetKeyDown(KeyCode.PageUp)) { JumpLine(ms, -1); return true; }
                if (Input.GetKeyDown(KeyCode.PageDown)) { JumpLine(ms, 1); return true; }
            }
            if (ms.IsMannequinScene)
            {
                if (Input.GetKeyDown(KeyCode.RightArrow)) { TurnGear(ms, shift ? 0.05f : 0.005f); return true; }
                if (Input.GetKeyDown(KeyCode.LeftArrow)) { TurnGear(ms, shift ? -0.05f : -0.005f); return true; }
            }
            return false;
        }

        internal static void MoveTape(MeasuringScene ms, float dy)
        {
            PersonMeasurer m = ms.ActiveMeasurer;
            if (m == null)
                return;
            MeasuringTape start = TapeStart(m);
            if (_placedOn != m || !TapeOnBody(m) || !start.gameObject.activeSelf)
                PutTapeOn(m, ms);
            else
                _tapeY += dy;

            Transform armpit = Armpit(m);
            if (armpit != null && _tapeY > armpit.position.y)
            {
                _tapeY = armpit.position.y;
                if (dy > 0) _pending = "Top. ";
            }
            float bottom = m.body.position.y - 1f;
            if (_tapeY < bottom) _tapeY = bottom;

            start.transform.position = new Vector3(m.body.position.x, _tapeY, start.transform.position.z);
            m.UpdateTape(stayInPlace: true);
            QueueDescribe(m, ms);
        }

        /// <summary>Lines top to bottom: bust, waist, hips (the game's guide marks).</summary>
        private static (string name, RectTransform rt)[] Lines(PersonMeasurer m, MeasuringScene ms)
        {
            return ms.IsMannequinScene
                ? new[] { ("Bust", GhostBust(m.paper)), ("Waist", GhostWaist(m.paper)), ("Hips", GhostHips(m.paper)) }
                : new[] { ("Bust", BustMarker(m)), ("Waist", WaistMarker(m)), ("Hips", HipsMarker(m)) };
        }

        /// <summary>The y the game tests against the lines (sizing uses the tape's start piece).</summary>
        private static float TestY(PersonMeasurer m, MeasuringScene ms)
        {
            if (ms.IsMannequinScene)
            {
                MeasuringTape t = PaperTape(m.paper);
                if (t != null && t.StartPiece != null) return t.StartPiece.position.y;
            }
            return TapeStart(m).transform.position.y;
        }

        /// <summary>dir -1 = the line above, +1 = the line below.</summary>
        internal static void JumpLine(MeasuringScene ms, int dir)
        {
            PersonMeasurer m = ms.ActiveMeasurer;
            if (m == null)
                return;
            bool placed = _placedOn == m && TapeOnBody(m) && TapeStart(m).gameObject.activeSelf;
            if (!placed)
            {
                PutTapeOn(m, ms);
                _pending = "Tape on. ";
            }
            var lines = Lines(m, ms);
            float y = TestY(m, ms);
            int cur = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].rt != null && Mathf.Abs(lines[i].rt.position.y - y) < 0.01f) cur = i;
            int target;
            if (!placed) target = 1; // start on the waist, the middle one
            else if (cur < 0)
            {
                // Between lines: go to the nearest one in that direction.
                target = dir < 0 ? 0 : lines.Length - 1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].rt == null) continue;
                    float ly = lines[i].rt.position.y;
                    if (dir < 0 && ly > y) target = i;
                    if (dir > 0 && ly < y) { target = i; break; }
                }
            }
            else target = Mathf.Clamp(cur + dir, 0, lines.Length - 1);
            if (placed && target == cur)
                _pending = dir < 0 ? "That is the top line. " : "That is the bottom line. ";
            RectTransform line = lines[target].rt;
            if (line == null)
                return;
            // Move the tape so the tested point sits exactly on the line.
            float offset = TestY(m, ms) - TapeStart(m).transform.position.y;
            _tapeY = line.position.y - offset;
            MeasuringTape start = TapeStart(m);
            start.transform.position = new Vector3(m.body.position.x, _tapeY, start.transform.position.z);
            m.UpdateTape(stayInPlace: true);
            QueueDescribe(m, ms);
        }

        private static void PutTapeOn(PersonMeasurer m, MeasuringScene ms)
        {
            m.ShowTape();
            // ShowTape expects the mouse to be over the body; keep the tape on regardless.
            DraggingProp.SetValue(m, false);
            TapeOnBody(m) = true;
            TapeStart(m).gameObject.SetActive(true);
            TapeEnd(m).gameObject.SetActive(true);
            MeasuringTapeIcon icon = Icon(m);
            if (icon != null)
            {
                icon.CancelDrag(immediate: true);
                icon.gameObject.SetActive(false);
            }
            _placedOn = m;
            // Start at the waist line: the middle of the three.
            RectTransform waist = ms.IsMannequinScene ? GhostWaist(m.paper) : WaistMarker(m);
            _tapeY = waist != null ? waist.position.y : m.body.position.y;
            _pending = "Tape on. ";
        }

        private static void TurnGear(MeasuringScene ms, float delta)
        {
            PersonMeasurer m = ms.ActiveMeasurer;
            if (_placedOn != m || !TapeOnBody(m))
            {
                Speech.Say("Press Up or Down first to put the tape on a line.");
                return;
            }
            Gear.BodyPart? part = MannequinBand(m);
            if (part == null)
            {
                Speech.Say("Press Up or Down to go to the bust, waist or hips line; that chooses which knob turns.");
                return;
            }
            foreach (Gear g in ms.gears)
            {
                if (g == null || g.bodyPart != part.Value)
                    continue;
                float before = GearValue(g);
                float v = Mathf.Clamp01(before + delta);
                if (Mathf.Approximately(v, before))
                {
                    float? client = ClientValue(part.Value);
                    float now = TapeEnd(m).value;
                    bool beyond = client.HasValue && (delta < 0 ? client.Value < now - 0.3f : client.Value > now + 0.3f);
                    Speech.Say($"That knob won't turn any further; the mannequin's {PartName(part.Value).ToLowerInvariant()} can't get any " +
                               (delta < 0 ? "smaller" : "bigger") + ". " +
                               (beyond ? $"The client's {client.Value:0.0} is beyond what the mannequin can do, so that measurement was probably taken off its line. Leave it as close as you can." : ""));
                    return;
                }
                g.SetGearPosition(v);
                GearSetBodyPart.Invoke(g, new object[] { v });
                // The mannequin's shape updates a frame later; measure again once it has,
                // or the game stores the size from before this turn.
                _settlePart = part;
                _settleFrame = Time.frameCount + 2;
                QueueDescribe(m, ms);
                return;
            }
        }

        // ---------- describing ----------

        private static void QueueDescribe(PersonMeasurer m, MeasuringScene ms)
        {
            // Wait a moment after the last key so held/fast presses don't stack up speech.
            _sayAt = Time.unscaledTime + 0.12f;
        }

        private static Gear.BodyPart? _settlePart;
        private static int _settleFrame;

        internal static void Tick()
        {
            if (_settlePart.HasValue && Time.frameCount >= _settleFrame)
            {
                MeasuringScene sms = Scene();
                if (sms != null)
                {
                    PersonMeasurer sm = sms.ActiveMeasurer;
                    sm.UpdateTape(stayInPlace: true);
                    sm.paper.SetMannequinMeasurement(_settlePart.Value);
                }
                _settlePart = null;
            }
            if (_sayAt <= 0f || Time.unscaledTime < _sayAt)
                return;
            _sayAt = 0f;
            MeasuringScene ms = Scene();
            if (ms == null)
                return;
            string s = (_pending ?? "") + Describe(ms.ActiveMeasurer, ms);
            _pending = null;
            Speech.Say(s);
        }

        private static string Describe(PersonMeasurer m, MeasuringScene ms)
        {
            MeasuringTape start = TapeStart(m);
            float cm = TapeEnd(m).value;
            if (!m.TrySampleCircumferenceAt(start.transform.position.WithZ(m.body.position.z), out cm, out _, out _))
                return "Off the body.";
            string value = cm.ToString("0.0") + " centimetres";

            if (ms.IsMannequinScene)
            {
                Gear.BodyPart? part = MannequinBand(m);
                if (part == null)
                    return value + ". " + NearestLine(m, ms);
                string name = PartName(part.Value);
                float? client = ClientValue(part.Value);
                string target = client.HasValue ? $", client {client.Value:0.0}" : "";
                string diff = "";
                if (client.HasValue)
                {
                    float d = cm - client.Value;
                    diff = Mathf.Abs(d) < 0.3f ? ", matched!" : (d > 0 ? ", too big, turn Left" : ", too small, turn Right");
                }
                return $"{name} line. Mannequin {cm:0.0}{target}{diff}.";
            }

            // Client: area from the game's hitboxes, line from its guide markers.
            string area = PersonArea(m, start);
            if (area == null)
                return value + ". " + NearestLine(m, ms);
            RectTransform marker = area == "Bust" ? BustMarker(m) : area == "Waist" ? WaistMarker(m) : HipsMarker(m);
            string where = "";
            if (marker != null)
            {
                float d = marker.position.y - start.transform.position.y;
                where = Mathf.Abs(d) <= Step * 0.5f + 0.0001f ? ", on the line, hold still to record" : (d > 0 ? ", line is above, not recording" : ", line is below, not recording");
            }
            return $"{area}{where}. {value}.";
        }

        private static string NearestLine(PersonMeasurer m, MeasuringScene ms)
        {
            float y = TapeStart(m).transform.position.y;
            var lines = new List<(string, RectTransform)>();
            if (ms.IsMannequinScene)
            {
                lines.Add(("bust", GhostBust(m.paper)));
                lines.Add(("waist", GhostWaist(m.paper)));
                lines.Add(("hips", GhostHips(m.paper)));
            }
            else
            {
                lines.Add(("bust", BustMarker(m)));
                lines.Add(("waist", WaistMarker(m)));
                lines.Add(("hips", HipsMarker(m)));
            }
            string best = null;
            float bd = float.MaxValue;
            foreach (var (n, rt) in lines)
            {
                if (rt == null) continue;
                float d = rt.position.y - y;
                if (Mathf.Abs(d) < Mathf.Abs(bd)) { bd = d; best = n; }
            }
            if (best == null)
                return "";
            return $"Nearest line: {best}, {(bd > 0 ? "up" : "down")}.";
        }

        private static string PersonArea(PersonMeasurer m, MeasuringTape start)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(null, start.transform.position);
            if (BustHit(m) != null && RectTransformUtility.RectangleContainsScreenPoint(BustHit(m), p)) return "Bust";
            p = RectTransformUtility.WorldToScreenPoint(null, start.rectTransform.position);
            if (WaistHit(m) != null && RectTransformUtility.RectangleContainsScreenPoint(WaistHit(m), p)) return "Waist";
            if (HipsHit(m) != null && RectTransformUtility.RectangleContainsScreenPoint(HipsHit(m), p)) return "Hips";
            return null;
        }

        private static readonly AccessTools.FieldRef<MeasurePaper, MeasuringTape> PaperTape =
            AccessTools.FieldRefAccess<MeasurePaper, MeasuringTape>("mannequinTape");

        private static Gear.BodyPart? MannequinBand(PersonMeasurer m)
        {
            MeasuringTape t = PaperTape(m.paper);
            float y = t != null && t.StartPiece != null ? t.StartPiece.position.y : TapeStart(m).transform.position.y;
            float tol = BandTolerance(m.paper);
            if (Near(GhostBust(m.paper), y, tol)) return Gear.BodyPart.Bust;
            if (Near(GhostWaist(m.paper), y, tol)) return Gear.BodyPart.Waist;
            if (Near(GhostHips(m.paper), y, tol)) return Gear.BodyPart.Hips;
            return null;
        }

        private static bool Near(RectTransform rt, float y, float tol) => rt != null && Mathf.Abs(rt.position.y - y) < tol;

        private static string PartName(Gear.BodyPart p) => p == Gear.BodyPart.Bust ? "Bust" : p == Gear.BodyPart.Waist ? "Waist" : "Hips";

        private static float? ClientValue(Gear.BodyPart p)
        {
            QuestDefinition q = PlayerProgress.Current?.CurrentQuestDefinition;
            if (q == null || q.questGiverCharacter == null)
                return null;
            CharacterMeasurements c = PlayerProgress.Current.GetCharacterMeasurements(q.questGiverCharacter);
            if (c == null)
                return null;
            return p == Gear.BodyPart.Bust ? c.bust : p == Gear.BodyPart.Waist ? c.waist : c.hips;
        }

        /// <summary>F3 addition: what's been measured so far.</summary>
        internal static string Summary()
        {
            MeasuringScene ms = Scene();
            if (ms == null)
                return "";
            MeasurePaper paper = ms.ActiveMeasurer.paper;
            if (!ms.IsMannequinScene)
            {
                return $"Client measurements: bust {Val(paper.BustValue)}, waist {Val(paper.WaistValue)}, hips {Val(paper.HipsValue)}. ";
            }
            string s = $"Mannequin: bust {Val(MBust(paper))}, waist {Val(MWaist(paper))}, hips {Val(MHips(paper))}. ";
            float? b = ClientValue(Gear.BodyPart.Bust), w = ClientValue(Gear.BodyPart.Waist), h = ClientValue(Gear.BodyPart.Hips);
            if (b.HasValue)
                s += $"Client: bust {b.Value:0.0}, waist {w.Value:0.0}, hips {h.Value:0.0}. ";
            if (MBust(paper).HasBeenSet && MWaist(paper).HasBeenSet && MHips(paper).HasBeenSet)
                s += $"Fit {Mathf.FloorToInt(paper.Accuracy * 100)} percent. ";
            return s;
        }

        private static string Val(MeasuringValue v) => v != null && v.HasBeenSet ? v.Value.ToString("0.0") : "not measured";

        // ---------- hooks ----------

        [HarmonyPatch(typeof(MeasuringScene), nameof(MeasuringScene.Show))]
        private static class ShowPatch
        {
            private static void Prefix() { _quietSetters = true; _placedOn = null; _allAnnounced = false; }
            private static void Postfix(MeasuringScene __instance)
            {
                _quietSetters = false;
                Speech.Later(__instance.IsMannequinScene
                    ? "Size the mannequin to match your client. Up and Down go between the bust, waist and hips lines; Left and Right turn that line's knob, Shift for bigger turns. F7 compares all three."
                    : "Measure your client. Up and Down move the tape measure a little at a time, Page Up and Page Down jump between the bust, waist and hips lines. Hold still on each line to record it. Then Tab to Confirm.");
            }
        }

        /// <summary>
        /// The game records whenever the tape rests anywhere in an area for half a second. With
        /// arrow keys the tape rests after every press, so passing through an area overwrote good
        /// measurements (her bust went from 86.9 on the line to 78.1 just below it). Only let it
        /// record when the tape is on that area's guide line.
        /// </summary>
        [HarmonyPatch(typeof(PersonMeasurer), "RecordMeasurements")]
        private static class OnlyOnTheLinePatch
        {
            private static readonly AccessTools.FieldRef<PersonMeasurer, bool> IsMannequin =
                AccessTools.FieldRefAccess<PersonMeasurer, bool>("_isMannequin");
            private static readonly AccessTools.FieldRef<PersonMeasurer, float> MeasureTime =
                AccessTools.FieldRefAccess<PersonMeasurer, float>("_measureTime");

            private static bool Prefix(PersonMeasurer __instance)
            {
                if (IsMannequin(__instance))
                    return true;
                float y = TapeStart(__instance).transform.position.y;
                foreach (RectTransform line in new[] { BustMarker(__instance), WaistMarker(__instance), HipsMarker(__instance) })
                    if (line != null && Mathf.Abs(line.position.y - y) <= Step * 0.5f + 0.0001f)
                        return true;
                MeasureTime(__instance) = 0f;
                return false;
            }
        }

        private static void Recorded(string part, float value, MeasurePaper paper)
        {
            if (_quietSetters)
                return;
            string s = $"{part} recorded: {value:0.0} centimetres.";
            bool all = paper.BustValue.HasBeenSet && paper.WaistValue.HasBeenSet && paper.HipsValue.HasBeenSet;
            if (all && !_allAnnounced)
            {
                _allAnnounced = true;
                s += " All three measured. Tab to Confirm when you're happy, or re-measure any of them.";
            }
            Speech.Queue(s);
        }

        [HarmonyPatch(typeof(MeasurePaper), nameof(MeasurePaper.SetBustMeasurement))]
        private static class BustPatch { private static void Postfix(MeasurePaper __instance, float value) => Recorded("Bust", value, __instance); }

        [HarmonyPatch(typeof(MeasurePaper), nameof(MeasurePaper.SetWaistMeasurement))]
        private static class WaistPatch { private static void Postfix(MeasurePaper __instance, float value) => Recorded("Waist", value, __instance); }

        [HarmonyPatch(typeof(MeasurePaper), nameof(MeasurePaper.SetHipsMeasurement))]
        private static class HipsPatch { private static void Postfix(MeasurePaper __instance, float value) => Recorded("Hips", value, __instance); }
    }
}
