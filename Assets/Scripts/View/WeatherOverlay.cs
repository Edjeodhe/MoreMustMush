using System.Collections.Generic;
using UnityEngine;

namespace MoreMush
{
    // Weather layer drawn over the field (prototype drawWeather): night/full-moon mask with holes, rain/storm, snow, spore wind.
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
        const int MOBILE_HOLES = 12;
        MaterialPropertyBlock mpb;
        string shownWeather;
        readonly Vector3[] linePoints = new Vector3[40];

        static void Configure(LineRenderer line, int count, float width, Color color)
        {
            line.positionCount = count; line.widthMultiplier = width / Art.PPU;
            line.startColor = line.endColor = color;
            line.transform.localPosition = Vector3.zero;
        }

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
            bool changed = w != shownWeather;
            shownWeather = w;
            bool night = R.st.theme.id == "night";
            int nl = 0, nd = 0;
            mpb ??= new MaterialPropertyBlock();

            // full moon / night: dark layer with holes around balls and the bar
            mask.enabled = w == "moon" || night;
            if (mask.enabled)
            {
                mask.transform.localPosition = Art.P(W / 2, H / 2);
                mask.transform.localScale = new Vector3(W / Art.PPU, H / Art.PPU, 1);
                int n = 0;
                const float rad = 260;
                // 셰이더가 화면 전체 픽셀마다 구멍 수만큼 돈다 → 모바일은 구멍을 줄인다 (바 먼저, 공은 리스트 앞의 수확기부터)
                int max = Application.isMobilePlatform ? MOBILE_HOLES : holes.Length;
                holes[n++] = new Vector4(R.barX, RoundSim.BAR_Y, 240, 1);
                foreach (var b in R.balls) { if (n >= max) break; holes[n++] = new Vector4(b.x, b.y, rad, 0); }
                mask.GetPropertyBlock(mpb);
                mpb.SetVectorArray(HolesId, holes);
                mpb.SetInt(CountId, n);
                mpb.SetColor(ColorId, w == "moon" ? U.Rgba(20, 40, 70, 0.5f) : U.Rgba(20, 40, 70, 0.28f));   // 밝은 연초록 바닥 위라 어둠을 옅게
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
                    var l = Line(nl++);
                    if (changed)
                    {
                        Configure(l, 2, 2, col);
                        linePoints[0] = Vector3.zero; linePoints[1] = Art.P(-8, len);
                        l.SetPositions(linePoints);
                    }
                    l.transform.localPosition = Art.P(x, y);
                }
            }
            else if (w == "cold")
            {
                Tint(U.Rgba(190, 225, 255, 0.22f));
                for (int i = 0; i < 70; i++)
                {
                    float x = Mod(i * 131 + Mathf.Sin(now + i) * 30, W), y = Mod(i * 67 + now * 60, H);
                    var d = Dot(nd++);
                    if (changed) { d.sprite = Art.Circle; d.color = new Color(1, 1, 1, 0.85f); d.transform.localRotation = Quaternion.identity; d.transform.localScale = Vector3.one * ((2 + i % 3) * 2 / Art.PPU); }
                    d.transform.localPosition = Art.P(x, y);
                }
            }
            else if (w == "wind")
            {
                for (int i = 0; i < 18; i++)
                {
                    float x = Mod(i * 211 + now * 500, W + 300) - 150, y = 80 + (i * 97) % 800;
                    var l = Line(nl++);
                    if (changed)
                    {
                        Configure(l, 12, 3, U.Rgba(210, 190, 240, 0.4f));
                        for (int k = 0; k < 12; k++)
                        {
                            float t = k / 11f, u = 1 - t;
                            linePoints[k] = Art.P(3 * u * u * t * 40 + 3 * u * t * t * 80 + t * t * t * 140, -36 * u * u * t + 36 * u * t * t);
                        }
                        l.SetPositions(linePoints);
                    }
                    l.transform.localPosition = Art.P(x, y);
                }
            }
            for (int i = nl; i < lines.Count; i++) lines[i].enabled = false;
            for (int i = nd; i < dots.Count; i++) dots[i].enabled = false;
        }

        static float Mod(float a, float m) { a %= m; return a < 0 ? a + m : a; }
    }
}
