using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// Turning a pattern piece by ear, like tuning an instrument. Two soft tones play for a
    /// moment after each turn:
    ///   straight grain (the best): the two tones meet in unison - one clear note;
    ///   cross grain (second best): they settle a fifth apart - calm, but not the one;
    ///   anywhere else: close but not together, so they beat and clash - the further off,
    ///   the harsher.  Bias-cut pieces are in tune with their (diagonal) arrow along or across.
    /// </summary>
    internal static class GrainTuner
    {
        private static Tone _a, _b;
        private static float _until;
        private const float Semitone = 1.0595f;

        internal static void Play(PatternPanel p)
        {
            if (p == null)
                return;
            if (_a == null)
            {
                _a = new Tone("GrainA", 330f, pure: true);
                _b = new Tone("GrainB", 330f, pure: true);
            }
            _until = Time.unscaledTime + 1.4f;
            Update(p);
        }

        /// <summary>0 = grain lies along the fabric, 90 = across it.</summary>
        internal static float GrainAngle(PatternPanel p)
        {
            Vector3 g = p.WorldObj.transform.TransformVector(p.idealGrainDirection);
            if (g.sqrMagnitude < 1e-8f)
                return 0f;
            float a = Mathf.Abs(Vector3.Angle(g.normalized, Vector3.right)); // 0..180
            if (a > 90f) a = 180f - a;                                     // fold to 0..90
            return a;
        }

        internal static void Update(PatternPanel p)
        {
            if (_a == null)
                return;
            if (p == null || Time.unscaledTime > _until)
            {
                _a.Set(0f, 1f, 0f);
                _b.Set(0f, 1f, 0f);
                return;
            }
            float ratio;
            if (p.idealGrainDirection == Vector3.zero)
                ratio = 1f; // piece with no grain requirement: always in tune
            else
            {
                float d = GrainAngle(p);
                if (p.definition != null && p.definition.isBiasCut)
                {
                    // Bias pieces have their grain arrow drawn diagonally already; the game scores
                    // them best with that arrow along OR across the fabric (both 100%).
                    float off = Mathf.Min(d, 90f - d) / 45f;       // 0 = arrow along or across
                    ratio = off < 0.02f ? 1f : Mathf.Lerp(1.015f, Semitone, off);
                }
                else if (d <= 45f)
                {
                    float off = d / 45f;                            // 0 = straight
                    ratio = off < 0.02f ? 1f : Mathf.Lerp(1.015f, Semitone, off);
                }
                else
                {
                    float off = (90f - d) / 45f;                    // 0 = cross grain
                    ratio = off < 0.02f ? 1.5f : 1.5f * Mathf.Lerp(1.015f, Semitone, off);
                }
            }
            float fade = Mathf.Clamp01((_until - Time.unscaledTime) / 0.4f);
            _a.Set(0.16f * fade, 1f, -0.15f);
            _b.Set(0.16f * fade, ratio, 0.15f);
        }
    }
}
