using UnityEngine;

namespace DressmakerAccess
{
    /// <summary>
    /// A soft, continuously playing hum the mod can steer: volume, pitch and
    /// left/right position change every frame. Used as the sewing guide - it
    /// sounds from the side the seam line is on, and is silent when on course.
    /// </summary>
    internal class Tone
    {
        private readonly AudioSource _src;
        private float _vol, _pitch = 1f, _pan;
        private readonly bool _pure;

        internal Tone(string name, float baseHz, bool pure = false)
        {
            _pure = pure;
            var go = new GameObject("DressmakerAccess_" + name);
            Object.DontDestroyOnLoad(go);
            _src = go.AddComponent<AudioSource>();
            _src.clip = MakeHum(baseHz, pure);
            _src.loop = true;
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _src.volume = 0f;
            _src.ignoreListenerPause = true;
        }

        /// <summary>Glide towards the target so changes never click.</summary>
        internal void Set(float volume, float pitch, float pan)
        {
            float k = Mathf.Clamp01(Time.unscaledDeltaTime * 12f);
            _vol = Mathf.Lerp(_vol, volume, k);
            _pitch = Mathf.Lerp(_pitch, pitch, k);
            _pan = Mathf.Lerp(_pan, pan, k);
            _src.volume = _vol;
            _src.pitch = _pitch;
            _src.panStereo = _pan;
            if (_vol > 0.001f && !_src.isPlaying)
                _src.Play();
            else if (_vol <= 0.001f && _src.isPlaying && volume <= 0f)
                _src.Stop();
        }

        internal void Silence()
        {
            _vol = 0f;
            _src.volume = 0f;
            if (_src.isPlaying)
                _src.Stop();
        }

        /// <summary>A warm hum: fundamental plus a little of the octave and fifth,
        /// with a slow wobble, so it reads as a sound in the room rather than a test beep.</summary>
        private static AudioClip MakeHum(float hz, bool pure)
        {
            const int rate = 44100;
            // Loop length a whole number of cycles of every partial and of the wobble.
            int n = rate; // 1 second
            float f = 2f * Mathf.Round(hz / 2f); // even, so the fifth also loops cleanly
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                if (pure)
                {
                    // Sine plus a touch of octave: soft, and two of them beat clearly.
                    data[i] = 0.4f * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * f * 2f * t)) / 1.15f;
                    continue;
                }
                float wobble = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 4f * t);
                float s = Mathf.Sin(2f * Mathf.PI * f * t)
                        + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2f * t)
                        + 0.2f * Mathf.Sin(2f * Mathf.PI * f * 1.5f * t);
                data[i] = 0.45f * s * wobble / 1.55f;
            }
            AudioClip clip = AudioClip.Create("hum", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
