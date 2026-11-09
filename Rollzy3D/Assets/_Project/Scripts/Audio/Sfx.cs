using UnityEngine;

namespace Rollzy
{
    /// <summary>
    /// Synthesised sound effects (no audio files needed). Replace with real clips in the Polish
    /// milestone by assigning AudioClips to the public fields.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public AudioClip coin, jump, crash, win, click, reward, tick;
        public bool enabledByPlayer = true;

        AudioSource source;
        const int Rate = 44100;

        enum Wave { Sine, Square, Triangle, Saw }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            if (!coin) coin = Sweep(1046, 1568, 0.08f, Wave.Square, 0.25f);
            if (!jump) jump = Sweep(330, 700, 0.16f, Wave.Sine, 0.5f);
            if (!crash) crash = Noise(0.35f, 0.6f);
            if (!win) win = Arpeggio(new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.2f);
            if (!click) click = Sweep(660, 880, 0.05f, Wave.Sine, 0.3f);
            if (!reward) reward = Arpeggio(new[] { 784f, 1047f, 1319f }, 0.07f, 0.15f);
            if (!tick) tick = Sweep(600, 640, 0.04f, Wave.Square, 0.15f);
        }

        public void Play(AudioClip clip)
        {
            if (enabledByPlayer && clip != null) source.PlayOneShot(clip);
        }

        static float Osc(Wave w, double phase)
        {
            double p = phase / (2 * Mathf.PI) % 1.0;
            switch (w)
            {
                case Wave.Square: return p < 0.5 ? 1f : -1f;
                case Wave.Triangle: return (float)(4 * System.Math.Abs(p - 0.5) - 1);
                case Wave.Saw: return (float)(2 * p - 1);
                default: return Mathf.Sin((float)phase);
            }
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Sweep(float f1, float f2, float duration, Wave wave, float volume)
        {
            int n = (int)(Rate * duration);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = f1 * Mathf.Pow(f2 / f1, t);
                phase += 2 * Mathf.PI * f / Rate;
                d[i] = Osc(wave, phase) * volume * Mathf.Exp(-4f * t) * Mathf.Min(1f, i / 80f);
            }
            return Make("sweep", d);
        }

        static AudioClip Noise(float duration, float volume)
        {
            int n = (int)(Rate * duration);
            var d = new float[n];
            var rng = new System.Random(7);
            float low = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float raw = (float)(rng.NextDouble() * 2 - 1);
                low += (raw - low) * 0.12f; // simple low-pass for a "thud"
                float boom = Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(200, 55, t) * i / Rate) * 0.5f;
                d[i] = (low * 1.6f + boom) * volume * (1 - t);
            }
            return Make("crash", d);
        }

        static AudioClip Arpeggio(float[] notes, float gap, float noteLength)
        {
            int n = (int)(Rate * (gap * (notes.Length - 1) + noteLength));
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(Rate * gap * k), len = (int)(Rate * noteLength);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)len;
                    d[start + i] += Osc(Wave.Triangle, 2 * Mathf.PI * notes[k] * i / Rate) * 0.35f * Mathf.Exp(-3f * t);
                }
            }
            return Make("arp", d);
        }
    }

    /// <summary>Short vibration pulses on Android.</summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        public static void Pulse(long milliseconds)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    vibrator?.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception) { }
#endif
        }

        // Referencing Handheld.Vibrate makes Unity add the VIBRATE permission to the Android manifest.
        static void KeepVibratePermission() => Handheld.Vibrate();
    }
}
