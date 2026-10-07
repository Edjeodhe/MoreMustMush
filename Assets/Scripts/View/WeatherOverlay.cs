using System.Collections.Generic;
using UnityEngine;

namespace MoreMush
{
    // Weather layer drawn over the field (prototype drawWeather): fog/night mask with holes, rain, snow,
    // drought heat lines, autumn leaves, spore wind, thunder tint, rainbow arcs.
    public class WeatherOverlay : MonoBehaviour
    {
        public SpriteRenderer mask;       // MoreMush/WeatherMask material
        public SpriteRenderer tint;       // full-field color tint
        public SpriteRenderer moonIcon;
        [AssetPath("Assets/Prefabs/Round/Line.prefab")] public LineRenderer linePrefab;
        [AssetPath("Assets/Prefabs/Round/Part.prefab")] public SpriteRenderer dotPrefab;

        readonly List<LineRenderer> lines = new List<LineRenderer>();
        readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
        static readonly int HolesId = Shader.PropertyToID("_Holes"), CountId = Shader.PropertyToID("_Count"), ColorId = Shader.PropertyToID("_Color");
        readonly Vector4[] holes = new Vector4[65];
        MaterialPropertyBlock mpb;

        LineRenderer Line(int i)
        {
            while (lines.Count <= i) { var l = Instantiate(linePrefab, transform); l.name = "WLine " + lines.Count; lines.Add(l); }
            lines[i].enabled = true; return lines[i];
        }

        SpriteRenderer Dot(int i)
        {
            while (dots.Count <= i) { var d = Instantiate(dotPrefab, transform); d.name = "WDot " + dots.Count; dots.Add(d); }
            dots[i].enabled = true; return dots[i];
        }

        public void Show(RoundSim R, float now)
        {
            const float W = Defs.W, H = Defs.H;
            string w = R.st.wId;
            bool night = R.st.theme.id == "night";
            int nl = 0, nd = 0;
            mpb ??= new MaterialPropertyBlock();

            // fog / full moon / night: dark (or white) layer with holes around balls and the bar
            mask.enabled = w == "fog" || w == "moon" || night;
            if (mask.enabled)
            {
                mask.transform.localPosition = Art.P(W / 2, H / 2);
                mask.transform.localScale = new Vector3(W / Art.PPU, H / Art.PPU, 1);
                int n = 0;
                float rad = w == "fog" ? 330 : 260;
                foreach (var b in R.balls) { if (n >= 64) break; holes[n++] = new Vector4(b.x, b.y, rad, 0); }
                holes[n++] = new Vector4(R.barX, RoundSim.BAR_Y, 240, 1);
                mask.GetPropertyBlock(mpb);
                mpb.SetVectorArray(HolesId, holes);
                mpb.SetInt(CountId, n);
                mpb.SetColor(ColorId, w == "fog" ? U.Rgba(235, 240, 240, 0.78f) : w == "moon" ? U.Rgba(10, 15, 45, 0.62f) : U.Rgba(10, 15, 45, 0.42f));
                mask.SetPropertyBlock(mpb);
            }
            moonIcon.enabled = w == "moon" || night;
            if (moonIcon.enabled)
            {
                moonIcon.sprite = SpriteDB.Icon(w == "moon" ? "w_moon" : "t_night");
                moonIcon.transform.localPosition = Art.P(RoundSim.WW - 120, 110);
                Art.FitPx(moonIcon, 80);
            }

            tint.enabled = false;
            void Tint(Color c) { tint.enabled = true; tint.color = c; tint.transform.localPosition = Art.P(W / 2, H / 2); tint.transform.localScale = new Vector3(W / Art.PPU, H / Art.PPU, 1); }

            if (w == "rain" || w == "storm")
            {
                int n = w == "storm" ? 220 : 80;
                if (w == "storm") Tint(U.Rgba(20, 30, 60, 0.25f));
                var col = w == "storm" ? U.Rgba(190, 215, 255, 0.6f) : U.Rgba(200, 225, 255, 0.45f);
                float len = w == "storm" ? 34 : 22, fall = w == "storm" ? 1400 : 900;
                for (int i = 0; i < n; i++)
                {
                    float x = Mod(i * 97 + now * 300, W), y = Mod(i * 53 + now * fall, H);
                    var l = Line(nl++); l.positionCount = 2; l.widthMultiplier = 2 / Art.PPU; l.startColor = l.endColor = col;
                    l.SetPosition(0, Art.P(x, y)); l.SetPosition(1, Art.P(x - 8, y + len));
                }
            }
            else if (w == "cold")
            {
                Tint(U.Rgba(190, 225, 255, 0.22f));
                for (int i = 0; i < 70; i++)
                {
                    float x = Mod(i * 131 + Mathf.Sin(now + i) * 30, W), y = Mod(i * 67 + now * 60, H);
                    var d = Dot(nd++); d.sprite = Art.Circle; d.color = new Color(1, 1, 1, 0.85f);
                    d.transform.localPosition = Art.P(x, y); d.transform.localScale = Vector3.one * ((2 + i % 3) * 2 / Art.PPU);
                }
            }
            else if (w == "drought")
            {
                Tint(U.Rgba(230, 150, 60, 0.18f));
                for (int i = 0; i < 12; i++)
                {
                    float y = Mod(i * 90 + now * 40, H);
                    var l = Line(nl++); l.positionCount = 33; l.widthMultiplier = 3 / Art.PPU; l.startColor = l.endColor = U.Rgba(255, 220, 160, 0.25f);
                    for (int k = 0; k <= 32; k++) { float x = k * 60; l.SetPosition(k, Art.P(x, y + Mathf.Sin(x / 80 + now * 3 + i) * 6)); }
                }
            }
            else if (w == "autumn")
            {
                string[] cols = { "#e8892a", "#d9542a", "#f2c14e" };
                for (int i = 0; i < 30; i++)
                {
                    float x = Mod(i * 131 + now * 60 + Mathf.Sin(now + i) * 40, W), y = Mod(i * 71 + now * 80, H);
                    var d = Dot(nd++); d.sprite = SpriteDB.Get("Props/leaf"); d.color = U.Hex(cols[i % 3]);
                    d.transform.localPosition = Art.P(x, y); d.transform.localRotation = Quaternion.Euler(0, 0, -(now + i) * Mathf.Rad2Deg);
                    Art.FitPx(d, 20);
                }
            }
            else if (w == "wind")
            {
                for (int i = 0; i < 18; i++)
                {
                    float x = Mod(i * 211 + now * 500, W + 300) - 150, y = 80 + (i * 97) % 800;
                    var l = Line(nl++); l.positionCount = 12; l.widthMultiplier = 3 / Art.PPU; l.startColor = l.endColor = U.Rgba(210, 190, 240, 0.4f);
                    for (int k = 0; k < 12; k++)
                    {
                        float t = k / 11f, u = 1 - t;
                        float bx = u * u * u * x + 3 * u * u * t * (x + 40) + 3 * u * t * t * (x + 80) + t * t * t * (x + 140);
                        float by = u * u * u * y + 3 * u * u * t * (y - 12) + 3 * u * t * t * (y + 12) + t * t * t * y;
                        l.SetPosition(k, Art.P(bx, by));
                    }
                }
            }
            else if (w == "thunder") Tint(U.Rgba(20, 20, 50, 0.18f));
            else if (w == "rainbow")
            {
                string[] cols = { "#ff6b6b", "#ffb347", "#fff36b", "#7be07b", "#6bb8ff", "#b07bff" };
                for (int i = 0; i < 6; i++)
                {
                    var l = Line(nl++); l.positionCount = 40; l.widthMultiplier = 16 / Art.PPU;
                    var c = U.Hex(cols[i]); c.a = 0.18f; l.startColor = l.endColor = c;
                    float r = 1150 - i * 16;
                    for (int k = 0; k < 40; k++) { float a = Mathf.PI * (1.15f + 0.7f * k / 39f); l.SetPosition(k, Art.P(960 + Mathf.Cos(a) * r, 1250 + Mathf.Sin(a) * r)); }
                }
            }
            for (int i = nl; i < lines.Count; i++) lines[i].enabled = false;
            for (int i = nd; i < dots.Count; i++) dots[i].enabled = false;
        }

        static float Mod(float a, float m) { a %= m; return a < 0 ? a + m : a; }
    }
}
