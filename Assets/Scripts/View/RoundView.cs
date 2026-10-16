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
        public RectTransform textLayer;         // same size as the 1920×1080 canvas, top-left anchored

        Pool<ShroomView> shrooms; Pool<BallView> balls; Pool<DeviceView> devices; Pool<CloudView> clouds;
        Pool<SpriteRenderer> parts, flyers; Pool<LineRenderer> lines;
        Pool<LineRenderer> aimLines;
        Pool<SpriteRenderer> extraBars;         // 분신 바 (씬의 바를 템플릿으로 복제)            // 조준 화살표 전용 (화살촉의 뾰족한 굵기 곡선이 공용 선 풀에 남지 않게)
        string bgTheme;

        static readonly AnimationCurve Taper = AnimationCurve.Linear(0, 1, 1, 0);
        const int MAX_RINGS = 64;               // 한 번에 그리는 고리 수 상한 (종 수확기는 맞힐 때마다 고리를 만든다)
        // SetPositions용 버퍼 (개수별로 하나씩, 프레임마다 배열을 만들지 않게)
        readonly Dictionary<int, Vector3[]> posBufs = new Dictionary<int, Vector3[]>();
        Vector3[] PosBuf(int n) { if (!posBufs.TryGetValue(n, out var b)) posBufs[n] = b = new Vector3[n]; return b; }

        // 씬에서 사람이 배경을 키우거나 옮겨 둔 만큼 (기본 = 필드에 딱 맞음). 라운드마다 지역 그림이 바뀌어도 같은 비율·간격을 지킨다
        Vector2 bgMul = Vector2.one;
        Vector3 bgOffset;

        void CaptureBackgroundLayout()
        {
            var sp = background.sprite;
            if (sp == null) return;
            var b = sp.bounds.size;
            var fit = new Vector2(Defs.W / Art.PPU / b.x, Defs.H / Art.PPU / b.y);   // 줌 1에서 필드에 딱 맞는 크기
            var s = background.transform.localScale;
            bgMul = new Vector2(s.x / fit.x, s.y / fit.y);
            bgOffset = background.transform.localPosition - Art.P(Defs.W / 2, Defs.H / 2);   // z는 그대로 둔다 (깊이)
        }

        void Awake()
        {
            CaptureBackgroundLayout();
            shrooms = new Pool<ShroomView>(shroomPrefab, shroomRoot);
            balls = new Pool<BallView>(ballPrefab, ballRoot);
            devices = new Pool<DeviceView>(devicePrefab, deviceRoot);
            clouds = new Pool<CloudView>(cloudPrefab, fxRoot);
            parts = new Pool<SpriteRenderer>(partPrefab, fxRoot);
            flyers = new Pool<SpriteRenderer>(flyerPrefab, flyerRoot);
            lines = new Pool<LineRenderer>(linePrefab, fxRoot);
            aimLines = new Pool<LineRenderer>(linePrefab, fxRoot);
            extraBars = new Pool<SpriteRenderer>(bar, bar.transform.parent);
        }

        public void Clear()
        {
            bossStr.Clear();
            shrooms.Trim(0); balls.Trim(0); extraBars.Trim(0); devices.Trim(0); clouds.Trim(0); parts.Trim(0); flyers.Trim(0); lines.Trim(0); aimLines.Trim(0);
            ReleaseTexts(true);
        }

        // live: 이번 프레임에 시뮬레이션이 진행됐는지 (일시정지·창·히트스톱이면 false → 흔들림·반짝이를 멈춘다)
        public void Draw(RoundSim R, bool live = true)
        {
            float now = Time.time;
            var st = R.st;
            float z = RoundSim.ZOOM;
            Vector2 shake = live && R.shake > 0 ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * R.shake * 8 : Vector2.zero;
            field.localPosition = Art.P(shake.x, shake.y);
            field.localScale = new Vector3(z, z, 1);
            if (textLayer != null) textLayer.anchoredPosition = new Vector2(shake.x, -shake.y);

            if (bgTheme != R.themeId)
            {
                bgTheme = R.themeId;
                background.sprite = SpriteDB.Get("Backgrounds/bg_" + bgTheme) ?? SpriteDB.Get("Backgrounds/bg_forest");
            }
            background.transform.localPosition = Art.P(RoundSim.WW / 2, RoundSim.WH / 2) + bgOffset;
            Art.SizePx(background.transform, background.sprite, RoundSim.WW * bgMul.x, RoundSim.WH * bgMul.y);

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
                if (live && m.golden && Random.value < 0.02f) R.AddPart(m.x + Random.Range(-m.r, m.r), m.y + Random.Range(-m.r, m.r), 0, -30, 0.5f, U.Hex("#fff3a0"), 3);
                shrooms.Get(i).Show(m, now, night, 1000 + Mathf.RoundToInt(m.y));
            }
            shrooms.Trim(R.shrooms.Count);

            // 포자 구름
            for (int i = 0; i < R.clouds.Count; i++) clouds.Get(i).Show(R.clouds[i], now);
            clouds.Trim(R.clouds.Count);

            int nl = 0;
            for (int ri = Mathf.Max(0, R.rings.Count - MAX_RINGS); ri < R.rings.Count; ri++)   // 가장 최근 것만
            {
                var r = R.rings[ri];
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
                var pts = PosBuf(1 + (bo.pts.Count - 1) * 4); int np = 0;
                pts[np++] = Art.P(bo.pts[0].x, bo.pts[0].y);
                float j = bo.slash ? 0 : 14;
                for (int i = 1; i < bo.pts.Count; i++)
                {
                    var a = bo.pts[i - 1]; var b = bo.pts[i];
                    for (int k = 1; k <= 4; k++) { float t = k / 4f; pts[np++] = Art.P(U.Lerp(a.x, b.x, t) + (k < 4 ? Random.Range(-j, j) : 0), U.Lerp(a.y, b.y, t) + (k < 4 ? Random.Range(-j, j) : 0)); }
                }
                l.positionCount = np; l.SetPositions(pts);
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
                var fp = PosBuf(4); fp[0] = Art.P(x0, y0); fp[1] = Art.P(x1, y0); fp[2] = Art.P(x1, y1); fp[3] = Art.P(x0, y1);
                acornFrame.SetPositions(fp);
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

            if (textLayer != null) DrawTexts(R, z);   // 필드 글자 층(캔버스)이 없으면 글자만 건너뛴다
        }

        LineRenderer Circle(int i, float x, float y, float r, float width) => Ellipse(i, x, y, r, r, 0, width);

        LineRenderer Ellipse(int i, float x, float y, float rx, float ry, float rot, float width)
        {
            var l = lines.Get(i);
            const int N = 40;
            l.loop = true; l.positionCount = N;
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            var buf = PosBuf(N);
            for (int k = 0; k < N; k++)
            {
                float a = k * U.TAU / N, ex = Mathf.Cos(a) * rx, ey = Mathf.Sin(a) * ry;
                buf[k] = Art.P(x + ex * cr - ey * sr, y + ex * sr + ey * cr);
            }
            l.SetPositions(buf);   // 점마다 SetPosition을 부르지 않고 한 번에
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
            // 분신 바: 반투명 하늘색
            for (int i = 0; i < R.extraBars.Count; i++)
            {
                var s = R.extraBars[i]; var sr = extraBars.Get(i);
                sr.transform.localPosition = Art.P(s.x, s.y);
                sr.size = new Vector2(s.len / Art.PPU, (BAR_T + 10) / Art.PPU);
                sr.sortingOrder = bar.sortingOrder;
                float blink = s.clone && s.life < 1 ? 0.4f + 0.4f * Mathf.Abs(Mathf.Sin(now * 12)) : 0.8f;
                sr.color = s.clone ? new Color(0.65f, 0.85f, 1, blink) : new Color(0.9f, 0.8f, 0.65f, 1);
                Art.SetFx(sr);
            }
            extraBars.Trim(R.extraBars.Count);
        }

        int DrawAim(RoundSim R, float now, int nl)
        {
            int na = 0;
            DrawArrows(R, ref nl, ref na, now);
            aimLines.Trim(na);
            return nl;
        }

        void DrawArrows(RoundSim R, ref int nl, ref int na, float now)
        {
            if (R.phase != "aim" || R.balls.Count == 0) return;
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
                        var s = aimLines.Get(na++); s.loop = false; s.positionCount = 2; s.widthCurve = Flat;
                        s.SetPosition(0, Art.P(b0.x + Mathf.Cos(ang) * 20, b0.y + Mathf.Sin(ang) * 20));
                        s.SetPosition(1, Art.P(b0.x + Mathf.Cos(ang) * (L - 30), b0.y + Mathf.Sin(ang) * (L - 30)));
                        s.widthMultiplier = 16 / Art.PPU; s.startColor = new Color(1, 220 / 255f, 60 / 255f, 0); s.endColor = new Color(1, 210 / 255f, 58 / 255f, alpha); s.sortingOrder = 3700;
                        // head
                        var h = aimLines.Get(na++); h.loop = false; h.positionCount = 2;
                        h.SetPosition(0, Art.P(b0.x + Mathf.Cos(ang) * (L - 30), b0.y + Mathf.Sin(ang) * (L - 30)));
                        h.SetPosition(1, Art.P(b0.x + Mathf.Cos(ang) * L, b0.y + Mathf.Sin(ang) * L));
                        h.widthCurve = Taper; h.widthMultiplier = 44 / Art.PPU;
                        h.startColor = h.endColor = new Color(1, 210 / 255f, 58 / 255f, alpha); h.sortingOrder = 3701;
                    }
                }
            }
        }

        static readonly AnimationCurve Flat = AnimationCurve.Constant(0, 1, 1);

        static Vector3[] ArcBuf = new Vector3[49];
        static void Arc(LineRenderer l, float x, float y, float r, float k, float width)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(48 * k) + 1);
            l.loop = k >= 1; l.positionCount = n;
            var buf = ArcBuf.Length == n ? ArcBuf : ArcBuf = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = -Mathf.PI / 2 + U.TAU * k * i / (n - 1);
                buf[i] = Art.P(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r);
            }
            l.SetPositions(buf);
            l.widthMultiplier = width / Art.PPU;
        }

        static readonly string[] TierCols = { "#ffffff", "#9fd8ff", "#e6b3ff", "#ffc94a" };
        static readonly float[] TierSizes = { 18, 21, 26, 32 };

        // 필드 글자: 글자 하나는 사라질 때까지 같은 TMP 칸을 쓴다. 예전처럼 목록 순서로 칸을 맡기면 앞 글자가 사라질 때마다
        // 뒤 글자들의 칸이 바뀌어 메시를 다시 만들었다. 투명도는 canvasRenderer 알파, 튀어나오는 크기는 scale로 바꿔 메시를 건드리지 않는다.
        class TextSlot { public TextMeshProUGUI tmp; public object key; public int seen; public string s, col; public float size = -1; }
        readonly List<TextSlot> textSlots = new List<TextSlot>();
        readonly Dictionary<object, TextSlot> slotOf = new Dictionary<object, TextSlot>();
        readonly Dictionary<int, string> comboStr = new Dictionary<int, string>();
        int textFrame;
        readonly Dictionary<Shroom, (int pct, string lang, string s)> bossStr = new Dictionary<Shroom, (int, string, string)>();   // 보스 글자 (체력 %가 바뀔 때만 다시 만든다)

        void DrawTexts(RoundSim R, float z)
        {
            textFrame++;
            foreach (var t in R.texts)
            {
                float life = t.tier >= 2 ? 1.2f : 0.8f, k = t.t / life;
                float size = TierSizes[t.tier] * (t.crit ? 1.1f : 1), pop = t.t < 0.1f ? 0.6f + t.t * 4 : 1;
                string col = t.coin ? "#ffd23a" : t.golden ? "#ffd23a" : t.crit ? "#ff5a4a" : TierCols[t.tier];
                t.view ??= t.coin ? $"+{U.FmtN(t.v, 1)}G" : (t.crit ? Loc.T("치명!") + " " : "+") + U.Fmt(t.v);   // 값이 바뀌지 않으니 한 번만 만든다
                Text(t, t.view, t.x, t.y, size, pop, col, k > 0.7f ? (1 - k) / 0.3f : 1, z);
            }
            foreach (var l in R.labels)
                Text(l, l.text, l.x, l.y, l.size, l.t < 0.12f ? 0.5f + l.t * 4 : 1, l.col, l.t > 1 ? (1.4f - l.t) / 0.4f : 1, z);
            foreach (var b in R.balls)
                if (b.combo >= 3 && b.perm)
                {
                    if (!comboStr.TryGetValue(b.combo, out var cs)) comboStr[b.combo] = cs = "x" + b.combo;
                    Text(b, cs, b.x, b.y - b.r - 14, 16, 1, "#fff3b0", 1, z);
                }
            // 보스 이름·남은 체력 (체력 막대 위)
            for (int i = 0; i < R.bosses.Count; i++)
            {
                var bm = R.bosses[i];
                if (bm.dead) continue;
                int pct = Mathf.CeilToInt((float)(bm.hp / bm.maxHp) * 100);
                if (!bossStr.TryGetValue(bm, out var bs) || bs.pct != pct || bs.lang != Loc.Lang)
                    bossStr[bm] = bs = (pct, Loc.Lang, (bm.bossClone ? Loc.F("{0}의 분신", R.Boss.n) : R.Boss.n) + $"  {pct}%");
                Text(bm, bs.s, bm.x, bm.y + bm.r * 1.2f + 30, bm.bossClone ? 20 : 26, 1, R.Boss.col, 1, z);
            }
            ReleaseTexts(false);
        }

        void Text(object key, string s, float x, float y, float size, float pop, string col, float alpha, float z)
        {
            if (!slotOf.TryGetValue(key, out var sl))
            {
                sl = null;
                foreach (var f in textSlots) if (f.key == null) { sl = f; break; }
                if (sl == null)
                {
                    sl = new TextSlot { tmp = Instantiate(textPrefab, textLayer) };
                    sl.tmp.name = textPrefab.name + " " + textSlots.Count;
                    textSlots.Add(sl);
                }
                sl.key = key; slotOf[key] = sl;
                sl.tmp.gameObject.SetActive(true);
            }
            sl.seen = textFrame;
            var t = sl.tmp;
            if (!ReferenceEquals(sl.s, s)) { sl.s = s; t.text = s; }
            float fs = size * z;
            if (fs != sl.size) { sl.size = fs; t.fontSize = fs; }
            if (!ReferenceEquals(sl.col, col)) { sl.col = col; t.color = U.Hex(col); }
            t.canvasRenderer.SetAlpha(Mathf.Clamp01(alpha));
            t.rectTransform.localScale = new Vector3(pop, pop, 1);
            t.rectTransform.anchoredPosition = new Vector2(x * z, -y * z);
        }

        // 이번 프레임에 그리지 않은 글자 칸을 비운다 (all = 전부)
        void ReleaseTexts(bool all)
        {
            foreach (var sl in textSlots)
            {
                if (sl.key == null || (!all && sl.seen == textFrame)) continue;
                slotOf.Remove(sl.key); sl.key = null; sl.s = null;
                sl.tmp.gameObject.SetActive(false);
            }
        }
    }
}
