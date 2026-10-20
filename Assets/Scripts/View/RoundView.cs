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
        [ScenePath("World/Round/Field/Decor")] public Transform decor;   // 지역별 테두리 장식 묶음 (자식 이름 = 지역 id, 없으면 forest)
        public Transform deviceRoot, shroomRoot, fxRoot, ballRoot, flyerRoot;
        public SpriteRenderer bar;
        // 바 그림(UI/bar_log, 수채화 통나무) 전체 높이: 통나무 몸통 ≈ 20px + 위로 솟은 잎, 아래는 같은 만큼 투명 여백 (몸통이 판정 줄 가운데에 온다)
        const float BAR_VIS_H = 39;
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
        Pool<SpriteRenderer> ringFx;            // 수채 고리·물감 튐 (dream FX가 있을 때 선 대신)
        Sprite ringSp, ringThinSp, splashSp, leafSp, dotSp;
        Pool<SpriteRenderer> fxPool;            // one-shot effects (R.fxs): hit bursts, spawn puffs, bar dust
        Sprite[] fxSp;                          // by FxShape
        Sprite[] hitFrames;                     // flipbook burst FX/hit_0..4 (FxShape.HitAnim)
        // the FX slicer fits every frame to the same canvas, so the burst's growth is put back here
        static readonly float[] HitFrameScale = { 0.5f, 0.82f, 1f, 1.12f, 1.2f };
        Material brushMat;                      // 번개·회오리·베기 선에 붓 결 (FX/brush)
        Pool<SpriteRenderer> extraBars;         // 분신 바 (씬의 바를 템플릿으로 복제)            // 조준 화살표 전용 (화살촉의 뾰족한 굵기 곡선이 공용 선 풀에 남지 않게)
        string bgTheme;

        static readonly AnimationCurve Taper = AnimationCurve.Linear(0, 1, 1, 0);
        const int MAX_RINGS = 64;               // 한 번에 그리는 고리 수 상한 (종 수확기는 맞힐 때마다 고리를 만든다)
        // 모바일: 한 번에 그리는 조각·날아가는 버섯 수 상한 (시뮬 상한 parts 260 · flyers 220). 군락 하나를 거두면 조각 36개 + 날아가는 버섯
        // 13~16개가 한 점에서 겹쳐 나와 줄여도 차이가 작다. 넘치면 가장 최근 것만 그린다 (시뮬은 그대로라 밸런스 무관)
        const int MOBILE_MAX_PARTS = 140, MOBILE_MAX_FLYERS = 60;
        // 날아가는 버섯·시계 아이콘의 화면 크기 (스테이지 px, 가로 1920 기준). 필드(ZOOM 배율) 아래에 있어 배율로 나눠 화면 크기를 고정한다.
        // 예전 40·30은 배율을 곱해 화면에서 60~67px(필드 0레벨 1.68)로 너무 컸다 (2026-10-20 사용자)
        const float FLYER_PX = 36, FLYER_CLOCK_PX = 27;
        readonly Dictionary<SpriteRenderer, float> flyerFit = new Dictionary<SpriteRenderer, float>();   // 칸 → 크기를 맞춘 필드 배율
        int MaxPartsDrawn = int.MaxValue, MaxFlyersDrawn = int.MaxValue;   // Awake에서 플랫폼 보고 정한다

        static bool IsMobile()
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            return Application.isMobilePlatform;   // 휴대폰 브라우저의 웹 데모
#endif
        }
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

        // ===== 라운드 간 풀 유지 =====
        // 풀 항목은 Stage 씬 밑에 있어 라운드가 끝나면 씬과 함께 버려졌다. 다음 라운드는 시작·첫 발사 때 GameObject 약 1,500개를
        // 다시 Instantiate해 끊겼다 → 씬을 떠날 때(App.Leaving) 씬 밖 보관소로 옮겨 두고 다음 RoundView가 그대로 다시 쓴다.
        // 처음 라운드는 아래 Prewarm이 프레임마다 조금씩 미리 채운다.
        static Transform stashRoot;
        static readonly Dictionary<string, object> stash = new Dictionary<string, object>();
        static Material brushShared;            // 붓 결 선 재질: 라운드마다 새로 만들지 않는다 (보관한 선이 같은 재질을 쓴다)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStash() { stashRoot = null; stash.Clear(); brushShared = null; }

        static Transform StashRoot()
        {
            if (stashRoot == null)
            {
                var go = new GameObject("~RoundPoolStash");
                go.SetActive(false);   // 맡긴 항목이 보이거나 Update되지 않게
                DontDestroyOnLoad(go);
                stashRoot = go.transform;
            }
            return stashRoot;
        }

        void Keep<T>(string key, Pool<T> pool) where T : Component
        {
            var list = pool.Detach(StashRoot());
            if (stash.TryGetValue(key, out var o) && o is List<T> had) had.AddRange(list);   // 이미 맡긴 것이 있으면 합친다 (잃어버리지 않게)
            else stash[key] = list;
        }
        void Take<T>(string key, Pool<T> pool) where T : Component { if (stash.TryGetValue(key, out var o)) { pool.Adopt(o as List<T>); stash.Remove(key); } }

        void OnEnable() { App.Leaving += StashPools; Loc.Changed += ApplyTextFonts; }
        void OnDisable() { App.Leaving -= StashPools; Loc.Changed -= ApplyTextFonts; }

        void StashPools()
        {
            Keep("shroom", shrooms); Keep("ball", balls); Keep("device", devices); Keep("cloud", clouds);
            Keep("part", parts); Keep("flyer", flyers); Keep("line", lines); Keep("aim", aimLines); Keep("ring", ringFx); Keep("fx", fxPool);
            // 필드 글자 칸 (UI): 빈 칸으로 돌려 맡긴다
            var tmps = new List<TextMeshProUGUI>(textSlots.Count);
            foreach (var sl in textSlots)
            {
                if (sl.tmp == null) continue;
                sl.tmp.gameObject.SetActive(false);
                sl.tmp.transform.SetParent(StashRoot(), false);
                tmps.Add(sl.tmp);
            }
            textSlots.Clear(); slotOf.Clear();
            if (stash.TryGetValue("text", out var o) && o is List<TextMeshProUGUI> had) had.AddRange(tmps);
            else stash["text"] = tmps;
        }

        void AdoptStash()
        {
            Take("shroom", shrooms); Take("ball", balls); Take("device", devices); Take("cloud", clouds);
            Take("part", parts); Take("flyer", flyers); Take("line", lines); Take("aim", aimLines); Take("ring", ringFx); Take("fx", fxPool);
            if (stash.TryGetValue("text", out var o) && o is List<TextMeshProUGUI> tmps)
            {
                foreach (var t in tmps) if (t != null) { t.transform.SetParent(textLayer, false); LocFonts.Apply(t, fieldRole, fieldPreset); textSlots.Add(new TextSlot { tmp = t, letter = t.transform.Find("Lettering")?.GetComponent<UnityEngine.UI.Image>() }); }
                stash.Remove("text");
            }
        }

        // 첫 라운드: 한 판에 필요한 만큼 프레임마다 시간 예산(ms) 안에서 꺼진 채로 미리 만든다 (조준하는 동안 끝난다)
        const float PREWARM_MS = 2f;
        System.Collections.IEnumerator Prewarm()
        {
            yield return null;   // 첫 프레임(씬 진입)은 건드리지 않는다
            var sw = new System.Diagnostics.Stopwatch();
            var steps = new (System.Func<int> count, int target, System.Action grow)[]
            {
                (() => balls.Count, 60, balls.Grow), (() => textSlots.Count, TEXT_SLOTS, () => WarmText(NewTextSlot())), (() => shrooms.Count, 200, shrooms.Grow),
                (() => fxPool.Count, 90, fxPool.Grow), (() => parts.Count, 260, parts.Grow), (() => ringFx.Count, MAX_RINGS, ringFx.Grow),
                (() => flyers.Count, 40, flyers.Grow), (() => lines.Count, 24, lines.Grow),
            };
            foreach (var st in steps)
                while (st.count() < st.target)
                {
                    sw.Restart();
                    while (st.count() < st.target && sw.Elapsed.TotalMilliseconds < PREWARM_MS) st.grow();
                    if (st.count() < st.target) yield return null;
                }
        }

        void Start() => StartCoroutine(Prewarm());

        // 필드 글자 칸 수: 숫자 45 + 라벨 11 + 콤보(공마다) + 보스 이름이 한꺼번에 뜬다. 45개만 미리 만들었더니 판 중에 90개 가까이까지
        // 새로 만들며(Instantiate) TMP 배열을 키웠다 (2026-10-20 할당 조사)
        const int TEXT_SLOTS = 120;
        // 미리 만든 칸에 긴 예시 글자(그림 숫자 + 한글)로 메시를 한 번 만들어 둔다: TMP 배열과 그림 숫자용 서브메시를 판 중이 아니라 여기서 만든다
        static string warmText;
        void WarmText(TextSlot sl)
        {
            warmText ??= (DmgDigits.Spell("+999.99Qa!") ?? "+999.99Qa!") + " " + (DmgDigits.Numbers("+12.3B") ?? "+12.3B") + " 새 버섯! 황금 버섯 배수 획득";
            sl.tmp.text = warmText;
            sl.tmp.ForceMeshUpdate(true, true);
        }

        void Awake()
        {
            if (IsMobile()) { MaxPartsDrawn = MOBILE_MAX_PARTS; MaxFlyersDrawn = MOBILE_MAX_FLYERS; }
            CaptureBackgroundLayout();
            shrooms = new Pool<ShroomView>(shroomPrefab, shroomRoot);
            balls = new Pool<BallView>(ballPrefab, ballRoot);
            devices = new Pool<DeviceView>(devicePrefab, deviceRoot);
            clouds = new Pool<CloudView>(cloudPrefab, fxRoot);
            parts = new Pool<SpriteRenderer>(partPrefab, fxRoot);
            flyers = new Pool<SpriteRenderer>(flyerPrefab, flyerRoot);
            lines = new Pool<LineRenderer>(linePrefab, fxRoot);
            aimLines = new Pool<LineRenderer>(linePrefab, fxRoot);
            // 슬라이드 바: 씬에는 옛 그림(ArtArchive)이 걸려 있다. 얇은 바용 9슬라이스 그림(SpriteDB UI/bar_log)으로 바꾼다 (분신 바도 같이)
            var barSp = SpriteDB.Get("UI/bar_log");
            if (barSp != null) { bar.sprite = barSp; bar.drawMode = SpriteDrawMode.Sliced; }
            extraBars = new Pool<SpriteRenderer>(bar, bar.transform.parent);
            ringFx = new Pool<SpriteRenderer>(partPrefab, fxRoot);
            // dream watercolor effects (Tools/art sheets fx): without them the old line rings stay
            splashSp = SpriteDB.Get("FX/splash"); leafSp = SpriteDB.Get("FX/leaf");
            ringSp = splashSp != null ? SpriteDB.Get("FX/ring") : null; ringThinSp = splashSp != null ? SpriteDB.Get("FX/ring_thin") : null;
            dotSp = partPrefab.sprite;
            fxPool = new Pool<SpriteRenderer>(partPrefab, fxRoot);
            AdoptStash();
            string[] fxKeys = { null, "FX/chip", "FX/leaf", "FX/impact", "FX/impact_ring", "FX/puff", "FX/dust", "FX/streak", "FX/sprout", "FX/drop", "FX/hit_0", "FX/twinkle", "FX/dot_ring" };
            hitFrames = new Sprite[5];
            for (int i = 0; i < 5; i++) hitFrames[i] = SpriteDB.Get("FX/hit_" + i);
            fxSp = new Sprite[fxKeys.Length];
            for (int i = 0; i < fxKeys.Length; i++) fxSp[i] = fxKeys[i] != null ? SpriteDB.Get(fxKeys[i]) : null;
            fxSp[0] = dotSp;
            var brush = SpriteDB.Get("FX/brush");
            if (brush != null) brushMat = brushShared != null ? brushShared : brushShared = new Material(linePrefab.sharedMaterial) { mainTexture = brush.texture };
        }

        // a line of the shared pool, painted with the brush texture when the dream set has one
        LineRenderer Line(int i)
        {
            var l = lines.Get(i);
            if (brushMat != null && l.sharedMaterial != brushMat) { l.sharedMaterial = brushMat; l.textureMode = LineTextureMode.Stretch; }
            return l;
        }

        static float EaseOut(float k) { k = 1 - Mathf.Clamp01(k); return 1 - k * k * k; }

        // one watercolor sprite of the ring pool: FX sprites are one world unit wide, so scale = diameter in units
        SpriteRenderer Blot(ref int n, Sprite sp, float x, float y, float diameter, Color c, float rot, int order)
        {
            var s = ringFx.Get(n++);
            s.sprite = sp;
            s.transform.localPosition = Art.P(x, y);
            s.transform.localScale = Vector3.one * (diameter / Art.PPU);
            s.transform.localRotation = Quaternion.Euler(0, 0, rot);
            s.color = c;
            s.sortingOrder = order;
            return s;
        }

        public void Clear()
        {
            bossStr.Clear();
            shrooms.Trim(0); balls.Trim(0); extraBars.Trim(0); devices.Trim(0); clouds.Trim(0); parts.Trim(0); flyers.Trim(0); lines.Trim(0); aimLines.Trim(0); ringFx.Trim(0); fxPool.Trim(0);
            ReleaseTexts(true);
        }

        // live: 이번 프레임에 시뮬레이션이 진행됐는지 (일시정지·창·히트스톱이면 false → 흔들림·반짝이를 멈춘다)
        public void Draw(RoundSim R, bool live = true)
        {
            float now = Time.time;
            var st = R.st;
            float z = RoundSim.ZOOM;
            // shake grows with its square (after NKK): taps stay subtle, big pops really kick
            Vector2 shake = live && R.shake > 0 ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * R.shake * (0.4f + R.shake) * 16 : Vector2.zero;
            field.localPosition = Art.P(shake.x, shake.y);
            field.localScale = new Vector3(z, z, 1);
            if (textLayer != null) textLayer.anchoredPosition = new Vector2(shake.x, -shake.y);

            if (bgTheme != R.themeId)
            {
                bgTheme = R.themeId;
                background.sprite = SpriteDB.Get("Backgrounds/bg_" + bgTheme) ?? SpriteDB.Get("Backgrounds/bg_forest");
                if (decor != null)
                {
                    bool any = decor.Find(bgTheme) != null;
                    foreach (Transform g in decor) g.gameObject.SetActive(g.name == (any ? bgTheme : "forest"));
                }
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
            for (int i = 0; i < R.shrooms.Count; i++)
            {
                var m = R.shrooms[i];
                if (live && m.golden && Random.value < 0.02f) R.AddPart(m.x + Random.Range(-m.r, m.r), m.y + Random.Range(-m.r, m.r), 0, -30, 0.5f, U.Hex("#fff3a0"), 3);
                shrooms.Get(i).Show(m, now, 1000 + Mathf.RoundToInt(m.y));
            }
            shrooms.Trim(R.shrooms.Count);

            // 포자 구름
            bool noSkill = FxPrefs.Simple;   // 연출 간소화: 스킬 효과(구름·고리·충격파·회오리·번개)를 그리지 않는다 (판정은 그대로)
            int ncl = noSkill ? 0 : R.clouds.Count;
            for (int i = 0; i < ncl; i++) clouds.Get(i).Show(R.clouds[i], now);
            clouds.Trim(ncl);

            int nl = 0, nr = 0;
            if (!noSkill && ringSp != null)
            {
                // watercolor: the ring bursts out fast and settles (ease-out), a paint splash pops at the hit for an
                // instant and fades; quiet rings (every bell hit) stay thin and small
                for (int ri = Mathf.Max(0, R.rings.Count - MAX_RINGS); ri < R.rings.Count; ri++)
                {
                    var r = R.rings[ri];
                    float k = r.t / r.dur, e = EaseOut(k);
                    var c = U.Hex(r.col); c.a = (1 - k) * (1 - k) * (r.quiet ? 0.7f : 0.95f);
                    float rot = (r.x * 7 + r.y * 3) % 360;
                    Blot(ref nr, r.quiet ? ringThinSp : ringSp, r.x, r.y, 2 * r.r * (0.35f + 0.75f * e), c, rot, 3040);
                    if (!r.quiet && k < 0.3f)
                    {
                        float p = k / 0.3f;
                        var sc = c; sc.a = (1 - p) * 0.75f;
                        Blot(ref nr, splashSp, r.x, r.y, r.r * (0.7f + 0.9f * EaseOut(p)), sc, rot + 40, 3041);
                    }
                }
                foreach (var w in R.waves)
                {
                    float k = w.t / 0.4f;
                    Blot(ref nr, ringSp, w.x, w.y, 2 * w.r, new Color(1, 0.9f, 0.76f, (1 - k) * 0.9f), w.x % 360, 3040);
                }
            }
            else if (!noSkill)
            {
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
            }
            if (!noSkill)
            foreach (var tn in R.tornados)
                for (int i = 0; i < 5; i++)
                {
                    var l = Ellipse(nl++, tn.x, tn.y + (i - 2) * tn.h / 5, 30 + i * 6, 8, now * 10 + i, 4);
                    l.startColor = l.endColor = new Color(230 / 255f, 1, 250 / 255f, 0.8f - i * 0.12f);
                }
            if (!noSkill)
            foreach (var bo in R.bolts)
            {
                var l = Line(nl++);
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
            // dream: one in four bigger bits is a spinning leaf; every bit pops big for a moment, then shrinks away
            int p0 = Mathf.Max(0, R.parts.Count - MaxPartsDrawn);
            for (int i = p0; i < R.parts.Count; i++)
            {
                var p = R.parts[i]; var s = parts.Get(i - p0);
                float k = p.t / p.life;
                int h = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p);
                bool chip = p.shape == (byte)FxShape.Chip && fxSp[(int)FxShape.Chip] != null;
                bool twinkle = p.shape == (byte)FxShape.Twinkle && fxSp[(int)FxShape.Twinkle] != null;
                bool leaf = !chip && !twinkle && leafSp != null && p.size >= 4 && (h & 3) == 0;
                var want = twinkle ? fxSp[(int)FxShape.Twinkle] : chip ? fxSp[(int)FxShape.Chip] : leaf ? leafSp : dotSp;
                if (twinkle) s.sortingOrder = 4140; else if (s.sortingOrder == 4140) s.sortingOrder = partPrefab.sortingOrder;
                if (s.sprite != want) s.sprite = want;
                float pop = splashSp == null ? 1 : k < 0.12f ? 0.6f + k / 0.12f * 0.65f : 1.25f - 0.65f * (k - 0.12f) / 0.88f;
                s.transform.localPosition = Art.P(p.x, p.y);
                s.transform.localScale = Vector3.one * (p.size * 2 / Art.PPU * pop * (leaf ? 1.9f : chip ? 1.6f : twinkle ? 2.6f : 1));
                s.transform.localRotation = leaf || chip ? Quaternion.Euler(0, 0, (h % 360) + p.t * ((h & 8) != 0 ? 420 : -420)) : Quaternion.identity;
                var c = p.col; c.a *= 1 - k * k; s.color = c;
            }
            parts.Trim(R.parts.Count - p0);
            ringFx.Trim(nr);
            DrawFx(R);

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
            // 재질 SpriteFx: 황금은 정점 색으로 (속성 블록 없이 묶어 그린다)
            int nf = 0, flying = 0;
            foreach (var f in R.flyers) if (f.t >= 0) flying++;
            int skip = Mathf.Max(0, flying - MaxFlyersDrawn);
            foreach (var f in R.flyers)
            {
                if (f.t < 0 || FxPrefs.Simple || skip-- > 0) continue;   // 연출 간소화: 아이콘은 안 그리고 도착할 때 칸이 튀는 것은 그대로
                var s = flyers.Get(nf++);
                s.transform.localPosition = Art.P(f.x, f.y);
                var fsp = f.clock ? SpriteDB.Icon("clock") : SpriteDB.Single(f.sp.id);
                // 칸마다 맞춘 배율을 기억한다: 프리팹 기본 그림이 양송이(ed0)라, 그림만 비교하면 튜토리얼 판(양송이뿐)에서 새 칸이 크기를 안 맞추고 프리팹 크기로 크게 나왔다
                if (!ReferenceEquals(s.sprite, fsp) || !flyerFit.TryGetValue(s, out var fz) || fz != RoundSim.ZOOM)
                {
                    s.sprite = fsp; Art.FitPx(s, (f.clock ? FLYER_CLOCK_PX : FLYER_PX) / RoundSim.ZOOM);
                    flyerFit[s] = RoundSim.ZOOM;
                }
                s.color = Art.FxColor(!f.clock && f.golden ? 1 : 0);
            }
            flyers.Trim(nf);

            if (textLayer != null) DrawTexts(R, z);   // 필드 글자 층(캔버스)이 없으면 글자만 건너뛴다
        }

        LineRenderer Circle(int i, float x, float y, float r, float width) => Ellipse(i, x, y, r, r, 0, width);

        LineRenderer Ellipse(int i, float x, float y, float rx, float ry, float rot, float width)
        {
            var l = Line(i);
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
            // landing: the bar dips, squashes flat and tilts a little toward where the harvester hit, then springs back
            float kick = R.barKick, spring = kick * kick * Mathf.Cos((1 - kick) * 14f);
            float tilt = R.barLen > 0 ? Mathf.Clamp((R.barHitX - R.barX) / (R.barLen / 2), -1, 1) : 0;
            bar.transform.localPosition = Art.P(R.barX, y + BAR_T / 2 + spring * 7);
            bar.transform.localRotation = Quaternion.Euler(0, 0, tilt * spring * 3.5f);
            bar.size = new Vector2(R.barLen * (1 + spring * 0.04f) / Art.PPU, BAR_VIS_H * (1 - spring * 0.28f) / Art.PPU);
            float k = Mathf.Min(1, R.feverK);
            if (k > 0.03f)
            {
                var F = FLAME[R.flamePal];
                bar.color = Color.Lerp(Color.white, F.mid, k * Mathf.Min(1, 0.55f + 0.4f * R.flameInt));
            }
            else bar.color = Color.white;
            Art.SetFx(bar, 0, 0, R.barFx > 0 ? R.barFx / 0.15f * 0.4f : 0);   // a quick light flash; full white read as the bar vanishing
            // 분신 바: 반투명 하늘색
            for (int i = 0; i < R.extraBars.Count; i++)
            {
                var s = R.extraBars[i]; var sr = extraBars.Get(i);
                sr.transform.localPosition = Art.P(s.x, s.y);
                sr.size = new Vector2(s.len / Art.PPU, BAR_VIS_H / Art.PPU);
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

        // damage numbers on the pale field: cream / sky / violet / gold, muted to the watercolour field (the painted
        // glyphs multiply these, so full-strength colours read as glossy stickers)
        static readonly string[] TierCols = { "#f7efe0", "#a9cfe8", "#cfb8e6", "#f0d07c" };
        static readonly float[] TierSizes = { 22, 25, 30, 38 };

        // 필드 글자: 글자 하나는 사라질 때까지 같은 TMP 칸을 쓴다. 예전처럼 목록 순서로 칸을 맡기면 앞 글자가 사라질 때마다
        // 뒤 글자들의 칸이 바뀌어 메시를 다시 만들었다. 투명도는 canvasRenderer 알파, 튀어나오는 크기는 scale로 바꿔 메시를 건드리지 않는다.
        class TextSlot { public TextMeshProUGUI tmp; public UnityEngine.UI.Image letter; public object key; public int seen; public string s, col; public float size = -1; public int style = -1; }
        readonly List<TextSlot> textSlots = new List<TextSlot>();
        readonly Dictionary<object, TextSlot> slotOf = new Dictionary<object, TextSlot>();
        readonly Dictionary<int, string> comboStr = new Dictionary<int, string>();
        int textFrame;
        readonly Dictionary<Shroom, (int pct, string lang, string s)> bossStr = new Dictionary<Shroom, (int, string, string)>();   // 보스 글자 (체력 %가 바뀔 때만 다시 만든다)

        void DrawTexts(RoundSim R, float z)
        {
            textFrame++;
            if (!FxPrefs.Simple)   // 연출 간소화: 대미지 숫자를 그리지 않는다
            foreach (var t in R.texts)
            {
                float life = t.tier >= 2 ? 1.2f : 0.8f, k = t.t / life;
                float size = TierSizes[t.tier] * (t.crit ? 1.2f : 1);
                // pop: easeOutBack from small over 0.18 s (after NKK popups), crits overshoot harder; shrinks a little at the end
                float c = t.crit ? 3.4f : 2.4f, e = Mathf.Clamp01(t.t / 0.18f) - 1;
                float pop = Mathf.Max(0.05f, 1 + (c + 1) * e * e * e + c * e * e) * (k > 0.75f ? 1 - (k - 0.75f) * 0.8f : 1);
                string col = t.coin || t.golden ? "#efcb6a" : t.crit ? "#e8705c" : TierCols[t.tier];
                // 값이 바뀌지 않으니 한 번만 만든다. 그림 숫자(DmgDigits)가 있으면 치명타는 "치명!" 글자 대신 숫자 뒤 느낌표
                t.view ??= t.coin ? DmgDigits.Spell($"+{U.FmtN(t.v, 1)}G") ?? $"+{U.FmtN(t.v, 1)}G"
                    : DmgDigits.Spell(t.crit ? U.Fmt(t.v) + "!" : "+" + U.Fmt(t.v)) ?? (t.crit ? Loc.T("치명!") + " " : "+") + U.Fmt(t.v);
                Text(t, t.view, t.x, t.y, size, pop, col, k > 0.7f ? (1 - k) / 0.3f : 1, z);
            }
            bool noSkillLabel = FxPrefs.Simple;
            foreach (var l in R.labels)
                if (!(noSkillLabel && l.skill)) Text(l, l.view ??= DmgDigits.Numbers(l.text) ?? l.text, l.x, l.y, l.size, l.t < 0.12f ? 0.5f + l.t * 4 : 1, l.col, l.t > 1 ? (1.4f - l.t) / 0.4f : 1, z);
            foreach (var b in R.balls)
                if (b.combo >= 3 && b.perm)
                {
                    if (!comboStr.TryGetValue(b.combo, out var cs)) comboStr[b.combo] = cs = DmgDigits.Spell("x" + b.combo) ?? "x" + b.combo;
                    Text(b, cs, b.x, b.y - b.r - 14, 16, 1, "#fff3b0", 1, z);
                }
            // 보스 이름·남은 체력 (체력 막대 위)
            for (int i = 0; i < R.bosses.Count; i++)
            {
                var bm = R.bosses[i];
                if (bm.dead) continue;
                int pct = Mathf.CeilToInt((float)(bm.hp / bm.maxHp) * 100);
                if (!bossStr.TryGetValue(bm, out var bs) || bs.pct != pct || bs.lang != Loc.Lang)
                    bossStr[bm] = bs = (pct, Loc.Lang, (bm.bossClone ? Loc.F("{0}의 분신", R.Boss.n) : R.Boss.n) + "  " + (DmgDigits.Numbers(pct + "%") ?? pct + "%"));
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
                if (sl == null) sl = NewTextSlot();
                sl.key = key; slotOf[key] = sl;
                if (!sl.tmp.gameObject.activeSelf) sl.tmp.gameObject.SetActive(true);   // 한 번 켠 칸은 끄지 않는다 (ReleaseTexts 참고)
            }
            sl.seen = textFrame;
            var t = sl.tmp;
            float fs = size * z;
            bool sChanged = !ReferenceEquals(sl.s, s);
            if (sChanged) { sl.s = s; t.text = s; }
            if (fs != sl.size) { sl.size = fs; t.fontSize = fs; }
            // fixed headline words (거대 버섯 수확! · 분신 소환! · 황금!) show their hand-lettered image instead
            if (sChanged || (sl.letter != null && sl.letter.rectTransform.sizeDelta.y != fs * 1.4f)) sl.letter = Lettering.Apply(t, FieldLetter(s), 1.4f);
            if (!ReferenceEquals(sl.col, col)) { sl.col = col; t.color = U.Hex(col); }
            int style = key is FloatText ? 1 : 0;
            if (sl.style != style)
            {
                // numbers get a thicker outline than labels so they read over mushrooms and the light ground
                sl.style = style;
                t.outlineWidth = style == 1 ? 0.3f : 0.2f;
                t.outlineColor = new Color32(0x3a, 0x26, 0x1c, 255);
            }
            t.canvasRenderer.SetAlpha(Mathf.Clamp01(alpha));
            if (sl.letter != null) sl.letter.canvasRenderer.SetAlpha(Mathf.Clamp01(alpha));
            t.rectTransform.localScale = new Vector3(pop, pop, 1);
            t.rectTransform.anchoredPosition = new Vector2(x * z, -y * z);
            // numbers tilt a little each way as they pop (crits more); labels stay level
            t.rectTransform.localRotation = key is FloatText ft ? Quaternion.Euler(0, 0, (ft.crit ? 11 : 6) * Mathf.Sin(ft.y * 0.37f + ft.v.GetHashCode()) * Mathf.Clamp01(ft.t / 0.18f)) : Quaternion.identity;
        }

        // One-shot effects: they burst open fast and fade while they finish growing (ease-out), so hits read as a
        // short sharp pop rather than a slow bloom. Order: dust and puffs under the mushrooms' feet stay low, bursts on top.
        void DrawFx(RoundSim R)
        {
            int n = 0;
            foreach (var f in R.fxs)
            {
                var sp = fxSp[(int)f.shape];
                float fk = f.t / f.dur, frameK = 1;
                if (f.shape == FxShape.HitAnim && hitFrames[0] != null)
                {
                    // flipbook: holds the bright first frames a touch longer, then breaks apart fast
                    int fi = Mathf.Min(4, (int)(Mathf.Pow(fk, 0.8f) * 5));
                    sp = hitFrames[fi] ?? sp;
                    frameK = HitFrameScale[fi];
                }
                if (sp == null) continue;
                var s = fxPool.Get(n++);
                if (s.sprite != sp) s.sprite = sp;
                float k = f.t / f.dur, e = EaseOut(k);
                float scale;
                switch (f.shape)
                {
                    case FxShape.Impact: scale = 0.45f + 0.75f * e; break;              // snaps open
                    case FxShape.HitAnim: scale = frameK; break;                        // the frames do the growing
                    case FxShape.ImpactRing: scale = 0.35f + 0.9f * e; break;
                    case FxShape.Puff: scale = 0.6f + 0.55f * e; break;                 // swells softly
                    case FxShape.Dust: scale = 0.55f + 0.6f * e; break;
                    case FxShape.Streak: scale = 0.7f + 0.5f * e; break;
                    case FxShape.Sprout: scale = k < 0.35f ? EaseOut(k / 0.35f) * 1.15f : 1.15f - 0.15f * (k - 0.35f) / 0.65f; break;   // pops up
                    default: scale = 1; break;
                }
                float sx = f.size * scale / Art.PPU / Mathf.Max(0.01f, sp.bounds.size.x);
                float sy = f.shape == FxShape.Dust || f.shape == FxShape.Streak ? sx : sx;
                s.transform.localPosition = Art.P(f.x, f.y);
                s.transform.localScale = new Vector3(f.flip ? -sx : sx, sy, 1);
                s.transform.localRotation = Quaternion.Euler(0, 0, f.shape == FxShape.Sprout || f.shape == FxShape.Dust ? f.rot * 0.2f : -f.rot);
                float a = f.shape == FxShape.Sprout ? (k < 0.7f ? 1 : (1 - k) / 0.3f) : f.shape == FxShape.HitAnim ? (k < 0.6f ? 1 : (1 - k) / 0.4f) : (1 - k) * (1 - k);
                var c = f.col; c.a *= a; s.color = c;
                // hit bursts and streaks draw over the harvesters for their few frames, or the disc hides the contact point
                s.sortingOrder = f.shape == FxShape.Dust || f.shape == FxShape.Sprout ? 900 : f.shape == FxShape.Puff ? 3030 : f.shape == FxShape.Impact || f.shape == FxShape.Streak || f.shape == FxShape.HitAnim ? 4150 : 3045;
            }
            fxPool.Trim(n);
        }

        static string letterLang;
        static readonly Dictionary<string, string> letterOf = new Dictionary<string, string>();
        // field label text (already translated) → lettering id
        static string FieldLetter(string s)
        {
            if (letterLang != Loc.Lang)
            {
                letterLang = Loc.Lang; letterOf.Clear();
                letterOf[Loc.T("거대 버섯 수확!")] = "giant"; letterOf[Loc.T("분신 소환!")] = "clone"; letterOf[Loc.T("황금!")] = "golden";
            }
            return s != null && letterOf.TryGetValue(s, out var id) ? id : null;
        }

        // 글자 칸의 언어 폰트: 프리팹의 LocText는 칸을 켜고 끌 때마다 Loc.Changed 구독·해제로 할당이 생겨 떼어 내고, 같은 역할로 여기서 맞춘다
        static LocFonts.Role fieldRole = LocFonts.Role.Title; static int fieldPreset = -1;
        void ApplyTextFonts() { foreach (var sl in textSlots) LocFonts.Apply(sl.tmp, fieldRole, fieldPreset); }

        TextSlot NewTextSlot()
        {
            var sl = new TextSlot { tmp = Instantiate(textPrefab, textLayer) };
            sl.tmp.name = textPrefab.name + " " + textSlots.Count;
            if (sl.tmp.TryGetComponent<LocText>(out var lt)) { fieldRole = lt.role; fieldPreset = lt.preset; Destroy(lt); }
            LocFonts.Apply(sl.tmp, fieldRole, fieldPreset);
            // the prefab picked up auto-size (max 20) from the localisation pass, which capped every number and
            // label at one small size; the field sets its own sizes per tier
            sl.tmp.enableAutoSizing = false;
            sl.tmp.gameObject.SetActive(false);
            textSlots.Add(sl);
            return sl;
        }

        // 이번 프레임에 그리지 않은 글자 칸을 비운다 (all = 전부)
        void ReleaseTexts(bool all)
        {
            foreach (var sl in textSlots)
            {
                if (sl.key == null || (!all && sl.seen == textFrame)) continue;
                slotOf.Remove(sl.key); sl.key = null; sl.s = null;
                // 끄지도 비우지도 않고 크기 0으로 숨긴다: SetActive를 오가면 TMP·서브메시가 OnEnable/OnDisable로, 글자를 비우면
                // 다시 채울 때 TMP 배열을 매번 새로 할당했다 (숫자가 초당 수십 개 뜨고 진다). 다시 쓰면 Text()가 크기를 되돌린다
                sl.tmp.rectTransform.localScale = Vector3.zero;
            }
        }
    }
}
