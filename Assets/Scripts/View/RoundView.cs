using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static MoreMush.Defs;

namespace MoreMush
{
    // Draws RoundSim.R every frame (prototype drawRound) into pooled scene objects under the Field root.
    public class RoundView : MonoBehaviour
    {
        [Header("Field (world)")]
        public Transform field;                 // field root: scaled by ZOOM, shaken
        public SpriteRenderer background;
        public Transform deviceRoot, shroomRoot, fxRoot, ballRoot, flyerRoot;
        public SpriteRenderer bar;
        public SpriteRenderer giantWarn;
        public SpriteRenderer festTint;
        public LineRenderer acornFrame;
        public WeatherOverlay weather;

        [Header("Special critter")]
        public CritterRig special;
        public LineRenderer specialRingBack, specialRing;
        public SpriteRenderer specialHpBack, specialHpFill, specialShadow;

        [Header("Prefabs")]
        [AssetPath("Assets/Prefabs/Round/ShroomView.prefab")] public ShroomView shroomPrefab;
        [AssetPath("Assets/Prefabs/Round/BallView.prefab")] public BallView ballPrefab;
        [AssetPath("Assets/Prefabs/Round/DeviceView.prefab")] public DeviceView devicePrefab;
        [AssetPath("Assets/Prefabs/Round/CloudView.prefab")] public CloudView cloudPrefab;
        [AssetPath("Assets/Prefabs/Round/Part.prefab")] public SpriteRenderer partPrefab;
        [AssetPath("Assets/Prefabs/Round/Flyer.prefab")] public SpriteRenderer flyerPrefab;
        [AssetPath("Assets/Prefabs/Round/Line.prefab")] public LineRenderer linePrefab;
        [AssetPath("Assets/Prefabs/Round/FieldText.prefab")] public TextMeshProUGUI textPrefab;

        [Header("Field text layer (UI)")]
        [ScenePath("Canvas/RoundUI/FieldText")] public RectTransform textLayer;         // same size as the 1920×1080 canvas, top-left anchored

        Pool<ShroomView> shrooms; Pool<BallView> balls; Pool<DeviceView> devices; Pool<CloudView> clouds;
        Pool<SpriteRenderer> parts, flyers; Pool<LineRenderer> lines; Pool<TextMeshProUGUI> texts;
        readonly List<LineRenderer> aim = new List<LineRenderer>();
        string bgTheme;

        void Awake()
        {
            shrooms = new Pool<ShroomView>(shroomPrefab, shroomRoot);
            balls = new Pool<BallView>(ballPrefab, ballRoot);
            devices = new Pool<DeviceView>(devicePrefab, deviceRoot);
            clouds = new Pool<CloudView>(cloudPrefab, fxRoot);
            parts = new Pool<SpriteRenderer>(partPrefab, fxRoot);
            flyers = new Pool<SpriteRenderer>(flyerPrefab, flyerRoot);
            lines = new Pool<LineRenderer>(linePrefab, fxRoot);
            texts = new Pool<TextMeshProUGUI>(textPrefab, textLayer);
        }

        public void Clear()
        {
            shrooms.Trim(0); balls.Trim(0); devices.Trim(0); clouds.Trim(0); parts.Trim(0); flyers.Trim(0); lines.Trim(0); texts.Trim(0);
        }

        public void Draw(RoundSim R)
        {
            float now = Time.time;
            var st = R.st;
            float z = RoundSim.ZOOM;
            Vector2 shake = R.shake > 0 ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * R.shake * 8 : Vector2.zero;
            field.localPosition = Art.P(shake.x, shake.y);
            field.localScale = new Vector3(z, z, 1);
            textLayer.anchoredPosition = new Vector2(shake.x, -shake.y);

            if (bgTheme != R.themeId)
            {
                bgTheme = R.themeId;
                background.sprite = SpriteDB.Get("Backgrounds/bg_" + bgTheme) ?? SpriteDB.Get("Backgrounds/bg_forest");
            }
            background.transform.localPosition = Art.P(RoundSim.WW / 2, RoundSim.WH / 2);
            Art.SizePx(background.transform, background.sprite, RoundSim.WW, RoundSim.WH);

            // 숲 장치
            for (int i = 0; i < R.devices.Count; i++) devices.Get(i).Show(R.devices[i], now);
            devices.Trim(R.devices.Count);

            // 거대 버섯 예고 그림자
            giantWarn.enabled = R.giantWarn != null;
            if (giantWarn.enabled)
            {
                float k = R.giantWarn.t;
                giantWarn.transform.localPosition = Art.P(R.giantWarn.x, R.giantWarn.y + 60);
                Art.SizePx(giantWarn.transform, giantWarn.sprite, 200 * (0.5f + k * 0.5f), 72 * (0.5f + k * 0.5f));
                giantWarn.color = new Color(1, 1, 1, 0.55f + 0.3f * Mathf.Sin(now * 20));
            }

            // 버섯 (y 순서로 그린다)
            bool night = st.wId == "moon" || st.theme.id == "night";
            for (int i = 0; i < R.shrooms.Count; i++)
            {
                var m = R.shrooms[i];
                if (m.golden && Random.value < 0.02f) R.AddPart(m.x + Random.Range(-m.r, m.r), m.y + Random.Range(-m.r, m.r), 0, -30, 0.5f, U.Hex("#fff3a0"), 3);
                shrooms.Get(i).Show(m, now, night, 1000 + Mathf.RoundToInt(m.y));
            }
            shrooms.Trim(R.shrooms.Count);

            DrawSpecial(R, now);

            // 포자 구름
            for (int i = 0; i < R.clouds.Count; i++) clouds.Get(i).Show(R.clouds[i], now);
            clouds.Trim(R.clouds.Count);

            int nl = 0;
            foreach (var r in R.rings)
            {
                float k = r.t / r.dur;
                var l = Circle(nl++, r.x, r.y, r.r * (0.3f + 0.7f * Mathf.Sqrt(k)), r.quiet ? 3 : 8 * (1 - k) + 2);
                var c = U.Hex(r.col); c.a = 1 - k; l.startColor = l.endColor = c;
            }
            foreach (var w in R.waves)
            {
                float k = w.t / 0.4f;
                var l = Circle(nl++, w.x, w.y, w.r, 10 * (1 - k) + 2);
                var c = U.Hex("#ffd9a8"); c.a = 1 - k; l.startColor = l.endColor = c;
            }
            foreach (var tn in R.tornados)
                for (int i = 0; i < 5; i++)
                {
                    var l = Ellipse(nl++, tn.x, tn.y + (i - 2) * tn.h / 5, 30 + i * 6, 8, now * 10 + i, 4);
                    l.startColor = l.endColor = new Color(230 / 255f, 1, 250 / 255f, 0.8f - i * 0.12f);
                }
            foreach (var bo in R.bolts)
            {
                var l = lines.Get(nl++);
                l.loop = false;
                var pts = new List<Vector3> { Art.P(bo.pts[0].x, bo.pts[0].y) };
                float j = bo.slash ? 0 : 14;
                for (int i = 1; i < bo.pts.Count; i++)
                {
                    var a = bo.pts[i - 1]; var b = bo.pts[i];
                    for (int k = 1; k <= 4; k++) { float t = k / 4f; pts.Add(Art.P(U.Lerp(a.x, b.x, t) + (k < 4 ? Random.Range(-j, j) : 0), U.Lerp(a.y, b.y, t) + (k < 4 ? Random.Range(-j, j) : 0))); }
                }
                l.positionCount = pts.Count; l.SetPositions(pts.ToArray());
                l.widthMultiplier = (bo.big ? 6 : bo.slash ? 7 : 4) / Art.PPU;
                var c = U.Hex(bo.col ?? (bo.slash ? "#f4f8ff" : "#fff9a0")); c.a = Mathf.Min(1, bo.t / 0.15f);
                l.startColor = l.endColor = c;
                l.sortingOrder = 3050;
            }

            // 핀볼
            for (int i = 0; i < R.balls.Count; i++) balls.Get(i).Show(R.balls[i], R, now);
            balls.Trim(R.balls.Count);

            DrawBar(R, now);
            nl = DrawAim(R, now, nl);
            lines.Trim(nl);

            // 파티클
            for (int i = 0; i < R.parts.Count; i++)
            {
                var p = R.parts[i]; var s = parts.Get(i);
                s.transform.localPosition = Art.P(p.x, p.y);
                s.transform.localScale = Vector3.one * (p.size * 2 / Art.PPU);
                var c = p.col; c.a *= 1 - p.t / p.life; s.color = c;
            }
            parts.Trim(R.parts.Count);

            weather.Show(R, now);

            festTint.enabled = R.festOn;
            if (festTint.enabled) { festTint.color = new Color(1, 190 / 255f, 60 / 255f, 0.05f + 0.02f * Mathf.Sin(now * 8)); festTint.transform.localPosition = Art.P(W / 2, H / 2); festTint.transform.localScale = new Vector3(W / Art.PPU, H / Art.PPU, 1); }
            acornFrame.enabled = R.acornT > 0;
            if (acornFrame.enabled)
            {
                float x0 = RoundSim.FX0 + 6, y0 = RoundSim.FY0 + 6, x1 = RoundSim.FX1 - 6, y1 = RoundSim.FY1 - 6;
                acornFrame.positionCount = 4; acornFrame.loop = true;
                acornFrame.SetPositions(new[] { Art.P(x0, y0), Art.P(x1, y0), Art.P(x1, y1), Art.P(x0, y1) });
                acornFrame.widthMultiplier = 12 / Art.PPU;
                acornFrame.startColor = acornFrame.endColor = new Color(1, 220 / 255f, 100 / 255f, 0.4f + 0.2f * Mathf.Sin(now * 10));
            }

            // 날아가는 버섯
            int nf = 0;
            foreach (var f in R.flyers)
            {
                if (f.t < 0) continue;
                var s = flyers.Get(nf++);
                s.transform.localPosition = Art.P(f.x, f.y);
                if (f.clock) { s.sprite = SpriteDB.Icon("clock"); Art.FitPx(s, 30); Art.SetFx(s); }
                else { s.sprite = SpriteDB.Single(f.sp.id); Art.FitPx(s, 40); Art.SetFx(s, f.golden ? 1 : 0); }
            }
            flyers.Trim(nf);

            DrawTexts(R, z);
        }

        LineRenderer Circle(int i, float x, float y, float r, float width) => Ellipse(i, x, y, r, r, 0, width);

        LineRenderer Ellipse(int i, float x, float y, float rx, float ry, float rot, float width)
        {
            var l = lines.Get(i);
            const int N = 40;
            l.loop = true; l.positionCount = N;
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            for (int k = 0; k < N; k++)
            {
                float a = k * U.TAU / N, ex = Mathf.Cos(a) * rx, ey = Mathf.Sin(a) * ry;
                l.SetPosition(k, Art.P(x + ex * cr - ey * sr, y + ex * sr + ey * cr));
            }
            l.widthMultiplier = width / Art.PPU;
            l.sortingOrder = 3000;
            return l;
        }

        void DrawBar(RoundSim R, float now)
        {
            float x0 = R.barX - R.barLen / 2, y = RoundSim.BAR_Y - BAR_T / 2;
            bar.transform.localPosition = Art.P(R.barX, y + BAR_T / 2);
            bar.size = new Vector2(R.barLen / Art.PPU, (BAR_T + 10) / Art.PPU);
            float k = Mathf.Min(1, R.feverK);
            if (k > 0.03f)
            {
                var F = FLAME[R.flamePal];
                bar.color = Color.Lerp(Color.white, F.mid, k * Mathf.Min(1, 0.55f + 0.4f * R.flameInt));
            }
            else bar.color = Color.white;
            Art.SetFx(bar, 0, 0, R.barFx > 0 ? 0.6f : 0);
        }

        int DrawAim(RoundSim R, float now, int nl)
        {
            if (R.phase != "aim") return nl;
            var b0 = R.balls[0];
            var ring = Circle(nl++, RoundSim.LAUNCH.x, RoundSim.LAUNCH.y, 70 + Mathf.Sin(now * 4) * 6, 3);
            ring.startColor = ring.endColor = new Color(1, 1, 1, 0.35f);
            if (R.aimStart.HasValue && R.aimCur.HasValue)
            {
                float dx = R.aimStart.Value.x - R.aimCur.Value.x, dy = R.aimStart.Value.y - R.aimCur.Value.y, len = Mathf.Sqrt(dx * dx + dy * dy);
                if (len >= 20)
                {
                    float a = Mathf.Atan2(dy, dx), L = 90 + Mathf.Min(len, 220) * 1.1f;
                    int n = R.balls.Count;
                    for (int i = 0; i < n; i++)
                    {
                        float off = n > 1 ? (-8 + 16f * i / (n - 1)) * Mathf.Deg2Rad : 0;
                        float alpha = i == n / 2 ? 1 : 0.5f, ang = a + off;
                        // shaft
                        var s = lines.Get(nl++); s.loop = false; s.positionCount = 2;
                        s.SetPosition(0, Art.P(b0.x + Mathf.Cos(ang) * 20, b0.y + Mathf.Sin(ang) * 20));
                        s.SetPosition(1, Art.P(b0.x + Mathf.Cos(ang) * (L - 30), b0.y + Mathf.Sin(ang) * (L - 30)));
                        s.widthMultiplier = 16 / Art.PPU; s.startColor = new Color(1, 220 / 255f, 60 / 255f, 0); s.endColor = new Color(1, 210 / 255f, 58 / 255f, alpha); s.sortingOrder = 3700;
                        // head
                        var h = lines.Get(nl++); h.loop = false; h.positionCount = 2;
                        h.SetPosition(0, Art.P(b0.x + Mathf.Cos(ang) * (L - 30), b0.y + Mathf.Sin(ang) * (L - 30)));
                        h.SetPosition(1, Art.P(b0.x + Mathf.Cos(ang) * L, b0.y + Mathf.Sin(ang) * L));
                        h.widthCurve = AnimationCurve.Linear(0, 1, 1, 0); h.widthMultiplier = 44 / Art.PPU;
                        h.startColor = h.endColor = new Color(1, 210 / 255f, 58 / 255f, alpha); h.sortingOrder = 3701;
                    }
                }
            }
            return nl;
        }

        void DrawSpecial(RoundSim R, float now)
        {
            var sm = R.special;
            bool on = sm != null;
            special.gameObject.SetActive(on);
            specialRingBack.enabled = specialRing.enabled = specialHpBack.enabled = specialHpFill.enabled = specialShadow.enabled = on;
            if (!on) return;
            float k = U.Clamp(sm.t / sm.max, 0, 1), hop = Mathf.Abs(Mathf.Sin(now * 8)) * 8;
            specialShadow.transform.localPosition = Art.P(sm.x, sm.y + sm.r * 0.95f);
            specialShadow.transform.localScale = new Vector3(sm.r * 1.6f / Art.PPU, sm.r * 0.5f / Art.PPU, 1);
            float rr = sm.r + 22;
            Arc(specialRingBack, sm.x, sm.y, rr, 1, 7); specialRingBack.startColor = specialRingBack.endColor = new Color(0, 0, 0, 0.35f);
            Arc(specialRing, sm.x, sm.y, rr, k, 5);
            var rc = k < 0.3f ? (Mathf.Sin(now * 14) > 0 ? U.Hex("#ff5a4a") : U.Hex("#ffd23a")) : U.Hex(sm.kind.c1);
            specialRing.startColor = specialRing.endColor = rc;
            special.transform.localPosition = Art.P(sm.x, sm.y - hop);
            special.SetKind(sm.kind.id);
            special.Squash(sm.appear * (1 + sm.squish * 0.2f), sm.appear * (1 - sm.squish * 0.2f), sm.r);
            float w = 80, y = sm.y - rr - 14;
            specialHpBack.transform.localPosition = Art.P(sm.x, y + 3); specialHpBack.transform.localScale = new Vector3((w + 2) / Art.PPU, 8 / Art.PPU, 1);
            float hk = Mathf.Max(0, (float)(sm.hp / sm.maxHp));
            specialHpFill.transform.localPosition = Art.P(sm.x - w / 2 + w * hk / 2, y + 3); specialHpFill.transform.localScale = new Vector3(w * hk / Art.PPU, 6 / Art.PPU, 1);
            specialHpFill.color = U.Hex(sm.kind.c1);
        }

        static void Arc(LineRenderer l, float x, float y, float r, float k, float width)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(48 * k) + 1);
            l.loop = k >= 1; l.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = -Mathf.PI / 2 + U.TAU * k * i / (n - 1);
                l.SetPosition(i, Art.P(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r));
            }
            l.widthMultiplier = width / Art.PPU;
        }

        static readonly string[] TierCols = { "#ffffff", "#9fd8ff", "#e6b3ff", "#ffc94a" };
        static readonly float[] TierSizes = { 18, 21, 26, 32 };

        void DrawTexts(RoundSim R, float z)
        {
            int n = 0;
            foreach (var t in R.texts)
            {
                float life = t.tier >= 2 ? 1.2f : 0.8f, k = t.t / life;
                float size = TierSizes[t.tier] * (t.crit ? 1.1f : 1) * (t.t < 0.1f ? 0.6f + t.t * 4 : 1);
                string col = t.coin ? "#ffd23a" : t.golden ? "#ffd23a" : t.crit ? "#ff5a4a" : TierCols[t.tier];
                string s = t.coin ? $"+{U.FmtN(t.v, 1)}G" : (t.crit ? "치명! " : "+") + U.Fmt(t.v);
                Text(n++, s, t.x, t.y, size, col, k > 0.7f ? (1 - k) / 0.3f : 1, z);
            }
            foreach (var l in R.labels)
            {
                float s = l.size * (l.t < 0.12f ? 0.5f + l.t * 4 : 1);
                Text(n++, l.text, l.x, l.y, s, l.col, l.t > 1 ? (1.4f - l.t) / 0.4f : 1, z);
            }
            foreach (var b in R.balls)
                if (b.combo >= 3 && b.perm) Text(n++, "x" + b.combo, b.x, b.y - b.r - 14, 16, "#fff3b0", 1, z);
            if (R.special != null)
            {
                var sm = R.special; float rr = sm.r + 22;
                Text(n++, $"{sm.kind.n} {sm.t:F1}초", sm.x, sm.y - rr - 14 - 16, 18, "#ffffff", 1, z);
            }
            texts.Trim(n);
        }

        void Text(int i, string s, float x, float y, float size, string col, float alpha, float z)
        {
            var t = texts.Get(i);
            t.text = s;
            t.fontSize = size * z;
            var c = U.Hex(col); c.a = Mathf.Clamp01(alpha); t.color = c;
            t.rectTransform.anchoredPosition = new Vector2(x * z, -y * z);
        }
    }
}
