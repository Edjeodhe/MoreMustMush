using System.Collections.Generic;
using UnityEngine;

namespace MoreMush
{
    // Short soft "pop/tick" sound effects synthesized at runtime, like the prototype's WebAudio Snd.
    // Clips are built from PCM (works on WebGL, unlike OnAudioFilterRead) and cached per parameter set.
    public class Snd : MonoBehaviour
    {
        public enum Wave { Sine, Triangle, Square, Saw }

        public static Snd I;
        public static bool muted;
        public static float vol = 0.2f;
        const int RATE = 44100;

        static readonly Dictionary<string, float> last = new Dictionary<string, float>();
        static readonly Dictionary<long, AudioClip> cache = new Dictionary<long, AudioClip>();
        public AudioSource[] voices = new AudioSource[16];   // voice pool, placed on this object in the hierarchy
        int next;
        // 조금 뒤에 낼 소리 (클로저 대신 값으로 들고 있어 소리마다 할당하지 않는다)
        struct Pending { public float at, f, d, v, slide; public Wave type; }
        readonly List<Pending> delayed = new List<Pending>();

        void Awake()
        {
            I = this;
        }

        void Update()
        {
            for (int i = delayed.Count - 1; i >= 0; i--)
                if (Time.unscaledTime >= delayed[i].at) { var p = delayed[i]; delayed.RemoveAt(i); Tone(p.f, p.d, p.type, p.v, p.slide); }
        }

        static void Later(float sec, float f, float d, Wave type, float v, float slide) { if (I != null) I.delayed.Add(new Pending { at = Time.unscaledTime + sec, f = f, d = d, type = type, v = v, slide = slide }); }

        static readonly float[] RARE_F = { 660, 880, 1320 }, RECORD_F = { 523, 659, 784, 1046 };
        static readonly Dictionary<float, string> skillKeys = new Dictionary<float, string>();
        static string SkillKey(float f) { if (!skillKeys.TryGetValue(f, out var k)) skillKeys[f] = k = "sk" + f; return k; }

        public static void Tone(float f, float d = 0.08f, Wave type = Wave.Sine, float v = 1, float slide = 0, string key = null, float gap = 0.035f)
        {
            if (muted || I == null) return;
            float now = Time.unscaledTime;
            if (key != null) { if (last.TryGetValue(key, out var l) && now - l < gap) return; last[key] = now; }
            var clip = Clip(Mathf.Round(f), d, type, slide);
            var src = I.voices[I.next++ % I.voices.Length];
            src.clip = clip; src.volume = vol * v; src.Play();
        }

        static AudioClip Clip(float f, float d, Wave type, float slide)
        {
            long k = ((long)f << 32) ^ ((long)(d * 1000) << 20) ^ ((long)type << 16) ^ (long)(slide * 100);
            if (cache.TryGetValue(k, out var c)) return c;
            int n = Mathf.CeilToInt((d + 0.03f) * RATE);
            var data = new float[n];
            double phase = 0;
            float f1 = slide > 0 ? Mathf.Max(30, f * slide) : f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / RATE;
                // exponential frequency ramp over d, like exponentialRampToValueAtTime
                float k01 = Mathf.Clamp01(t / d);
                float freq = slide > 0 ? f * Mathf.Pow(f1 / f, k01) : f;
                phase += freq / RATE;
                float p = (float)(phase - System.Math.Floor(phase));
                float s = type switch
                {
                    Wave.Sine => Mathf.Sin(p * Mathf.PI * 2),
                    Wave.Triangle => 1 - 4 * Mathf.Abs(p - 0.5f),
                    Wave.Square => p < 0.5f ? 1 : -1,
                    _ => 2 * p - 1,
                };
                // envelope: 0.0001 → 1 in 6 ms, then exponential decay to 0.0001 at d
                float env = t < 0.006f ? Mathf.Pow(10000, t / 0.006f) / 10000 : t < d ? Mathf.Pow(0.0001f, (t - 0.006f) / Mathf.Max(0.001f, d - 0.006f)) : 0;
                data[i] = s * env;
            }
            c = AudioClip.Create("tone", n, 1, RATE, false);
            c.SetData(data, 0);
            cache[k] = c;
            return c;
        }

        public static void Hit() => Tone(Random.Range(560f, 680f), 0.05f, Wave.Triangle, 0.45f, 1.35f, "hit");
        public static void Harvest(int combo) => Tone(420 * Mathf.Pow(2, Mathf.Min(combo, 24) / 12f), 0.09f, Wave.Sine, 0.75f, 1.7f, "harv", 0.03f);
        public static void Bar() => Tone(240, 0.08f, Wave.Square, 0.25f, 1.6f, "bar");
        public static void Wall() => Tone(150, 0.04f, Wave.Triangle, 0.25f, 0.8f, "wall", 0.06f);
        public static void Skill(float f) => Tone(f, 0.16f, Wave.Saw, 0.22f, 0.55f, SkillKey(f), 0.08f);
        public static void Rare() { for (int i = 0; i < RARE_F.Length; i++) Later(i * 0.06f, RARE_F[i], 0.14f, Wave.Sine, 0.6f, 1.2f); }
        public static void Buy() { Tone(520, 0.07f, Wave.Sine, 0.6f, 1.5f); Later(0.06f, 780, 0.1f, Wave.Sine, 0.6f, 1.5f); }
        public static void Ui() => Tone(720, 0.03f, Wave.Triangle, 0.35f, 0, "ui");
        public static void Err() => Tone(170, 0.12f, Wave.Square, 0.25f, 0.8f, "err");
        public static void Launch() => Tone(300, 0.2f, Wave.Sine, 0.6f, 3);
        public static void Bumper() => Tone(900, 0.06f, Wave.Square, 0.25f, 0.5f, "bump");
        public static void Record() { for (int i = 0; i < RECORD_F.Length; i++) Later(i * 0.07f, RECORD_F[i], 0.12f, Wave.Triangle, 0.5f, 0); }
    }
}
