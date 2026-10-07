using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MoreMush
{
    // Ports of the prototype's small helpers (rand, fmt, seeded, ...). Same semantics as the JS versions.
    public static class U
    {
        public const float TAU = Mathf.PI * 2f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static float Rand(float a, float b) => a + UnityEngine.Random.value * (b - a);
        public static int RandI(int a, int b) => Mathf.FloorToInt(a + UnityEngine.Random.value * (b - a + 1));
        public static bool Chance(float p) => UnityEngine.Random.value < p;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float D2(float ax, float ay, float bx, float by) { float dx = ax - bx, dy = ay - by; return dx * dx + dy * dy; }
        public static T Pick<T>(IList<T> arr) => arr[Mathf.Min(arr.Count - 1, Mathf.FloorToInt(UnityEngine.Random.value * arr.Count))];

        public static T PickWeighted<T>(IList<T> arr, Func<T, double> wf) where T : class
        {
            double tot = 0;
            var ws = new double[arr.Count];
            for (int i = 0; i < arr.Count; i++) { ws[i] = Math.Max(0, wf(arr[i])); tot += ws[i]; }
            if (tot <= 0) return null;
            double r = UnityEngine.Random.value * tot;
            for (int i = 0; i < arr.Count; i++) { r -= ws[i]; if (r <= 0) return arr[i]; }
            return arr[arr.Count - 1];
        }

        public static float AngDiff(float a, float b)
        {
            float d = b - a;
            while (d > Mathf.PI) d -= TAU;
            while (d < -Mathf.PI) d += TAU;
            return d;
        }

        // Deterministic generator used for decorations (same sequence as the prototype's seeded()).
        public static Func<double> Seeded(int seed)
        {
            long s = ((long)seed * 9301 + 49297) % 233280;
            if (s == 0) s = 1;
            return () => { s = (s * 16807) % 2147483647; return (s - 1) / 2147483646.0; };
        }

        // JS Math.round: halves round up.
        public static double JsRound(double v) => Math.Floor(v + 0.5);

        static readonly string[] Units = { "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

        // Big-number short form: 1.2K, 4.5M ...
        public static string Fmt(double n)
        {
            if (double.IsInfinity(n) || double.IsNaN(n)) return "∞";
            n = Math.Floor(n);
            if (n < 10000) return n.ToString("#,0", Inv);
            int i = -1; double v = n;
            while (v >= 1000 && i < Units.Length - 1) { v /= 1000; i++; }
            string s = v < 10 ? v.ToString("F2", Inv) : v < 100 ? v.ToString("F1", Inv) : v.ToString("F0", Inv);
            return s + Units[i];
        }

        public static string FmtN(double v, int d = 2)
        {
            if (v >= 1000) return Fmt(v);
            double p = Math.Pow(10, d);
            return (JsRound(v * p) / p).ToString("0.##########", Inv);
        }

        public static string FmtTime(double s)
        {
            long t = (long)Math.Floor(s);
            long h = t / 3600, m = (t % 3600) / 60, ss = t % 60;
            return h > 0 ? $"{h}시간 {m}분" : m > 0 ? $"{m}분 {ss}초" : $"{ss}초";
        }

        public static string Pct(double v) => FmtN(v * 100, 1) + "%";

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color Rgba(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);

        // "가" or "이" depending on the last syllable's final consonant (prototype iga()).
        public static string Iga(string w)
        {
            int c = w[w.Length - 1] - 0xAC00;
            return w + (c >= 0 && c < 11172 && c % 28 != 0 ? "이" : "가");
        }

        public static string Stars(int s) => new string('★', s) + new string('☆', 5 - s);
    }
}
