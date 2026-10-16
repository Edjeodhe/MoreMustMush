using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 꼬마버섯 세계수 화면(월드): 나무와, 배치된 구역의 나무버섯 사이를 오가며 따는 꼬마, 쉬는 꼬마는 나무 앞 땅을 산책한다.
    // 따는 모습·떠오르는 골드는 보여 주기용 (실제 수확은 Game.BagRate → 수확 보따리).
    // 누르기: 꼬마 → 꼬마 탭에서 그 꼬마 · 나무 밑동 → 성장 탭 · 장식 → 꾸미기 탭 (FarmScreen이 연다).
    public class FarmView : MonoBehaviour
    {
        public static FarmView I;

        [Header("Scene")]
        public FarmTreeView tree;

        [Header("Templates (hidden slots)")]
        public Transform critterLayer;
        public FarmCritter critterTemplate;
        public SpriteRenderer partTemplate;
        public FarmFloat floatTemplate;
        public Transform fx;

        // ===== screen state (not saved) =====
        public class Critter
        {
            public string id; public Special kind;
            public float x, y, tx, ty, t, hop, animT, sayT;
            public int face = 1;
            public string state = "idle", anim, say, tool;   // state: idle · walk · work
            public int job = -1;                             // 따러 가는 나무버섯 슬롯
            public int bframe = -1;                          // (예전 건설 그림, 쓰지 않음)
        }
        class Part { public string k; public float x, y, vx, vy, t, life; }
        class Float { public float x, y, t; public string text; public bool gem; public Color col; }

        public readonly List<Critter> crits = new List<Critter>();
        readonly List<Part> parts = new List<Part>();
        readonly List<Float> floats = new List<Float>();
        Critter hover; int hoverSlot = -1;
        public bool dirty;                                // HUD를 다시 그려야 함 (FarmScreen이 본다)
        public System.Action<string, string> onPick;      // (탭, 인자): 월드를 눌러 패널을 연다

        Pool<FarmCritter> critPool;
        Pool<SpriteRenderer> partPool;
        Pool<FarmFloat> floatPool;

        void Awake()
        {
            I = this;
            critPool = new Pool<FarmCritter>(critterTemplate, critterLayer);
            partPool = new Pool<SpriteRenderer>(partTemplate, fx);
            floatPool = new Pool<FarmFloat>(floatTemplate, fx);
        }

        public void Enter()
        {
            FarmEnsure(); SaveGame();
            Sync(true);
            dirty = true;
        }

        public void Flush() { if (G?.farm != null) { BagSettle(); SaveGame(); } }

        static Critter NewCritter(Special k, float x, float y) => new Critter
        { id = k.id, kind = k, x = x, y = y, tx = x, ty = y, t = U.Rand(0.2f, 2), face = U.Chance(0.5f) ? 1 : -1, hop = U.Rand(0, 6) };

        // 합류한 꼬마가 들어온다 (새로 온 꼬마는 화면 아래에서 걸어 들어온다)
        public void Sync(bool initial)
        {
            foreach (var k in FarmOwned())
            {
                if (crits.Any(p => p.id == k.id)) continue;
                var p0 = initial ? GroundSpot() : new Vector2(WT.x + U.Rand(-1f, 1f) * HARV.halfW * Zoom, 1080 + 60);
                crits.Add(NewCritter(k, p0.x, p0.y));
            }
            crits.RemoveAll(p => !HasSpecial(p.id) || !KID.ContainsKey(p.id));
        }

        // the ground widens with the camera (around the tree)
        static Vector2 GroundSpot() => new Vector2(WT.x + U.Rand(-1f, 1f) * HARV.halfW * Zoom, U.Rand(HARV.y0, HARV.y1));

        // ===== critters =====
        public void Say(Critter p, string line, string anim)
        {
            p.say = line; p.sayT = 2.8f;
            if (anim != null) { p.anim = anim; p.animT = 0; if (p.job < 0) { p.state = "idle"; p.t = Mathf.Max(p.t, FARM_ANIMS[anim] + 0.3f); } }
        }
        public Critter Find(string id) => crits.FirstOrDefault(q => q.id == id);
        public void Cheer(string id, string line, string text)
        {
            var p = Find(id); if (p == null) return;
            Say(p, line, p.job < 0 ? "jump" : null);
            if (text != null) AddFloat(p.x, p.y - 200, text, false, U.Hex("#ffe36e"));
            Stars(p.x, p.y - 50);
        }

        void ClickCritter(Critter p)
        {
            Snd.Ui();
            string anim = U.Pick(FARM_ANIMS.Keys.ToList());
            string zone = ZoneOf(p.id);
            Say(p, zone == null ? U.Pick(FARM_LINES["idle"]) : p.job >= 0 ? U.Pick(FARM_LINES["field"]) : anim == "sleep" ? Loc.T("쿨쿨... 음냐...") : U.Pick(FARM_LINES[p.id].Concat(FARM_LINES["all"]).ToList()), p.job < 0 ? anim : null);
            onPick?.Invoke("kid", p.id);
        }

        public void TreeGrew(string text)
        {
            float top = tree.Top;
            AddFloat(WT.x, top - 30, text, false, U.Hex("#ffe36e"));
            Stars(WT.x, (top + WT.y) / 2);
            for (int i = 0; i < 3; i++) Stars(WT.x + U.Rand(-200, 200) * Zoom, top + U.Rand(50, 300) * Zoom);
            foreach (var p in crits) if (p.job < 0) Say(p, Loc.T("나무가 커졌다!"), "jump");
        }

        // 보따리를 받으면 나무에서 골드·균사석이 쏟아진다
        public void BagBurst(double gold, int gem)
        {
            float mid = Mathf.Lerp(WT.y, tree.Top, 0.35f);   // the bag pops from the trunk, wherever the tree's size puts it
            AddFloat(WT.x, mid - 60, $"+{U.Fmt(gold)}", false, U.Hex(GOLD_C));
            if (gem > 0) AddFloat(WT.x + 90 * Zoom, mid - 100, $"+{gem}", true, U.Hex("#a8f5d4"), -0.2f);
            GemBurst(WT.x, mid, Mathf.Clamp(gem, 6, 30));
            foreach (var p in crits) if (p.job < 0) Say(p, U.Pick(FARM_LINES["thanks"]), "jump");
        }

        void Stars(float x, float y) { for (int i = 0; i < 30; i++) { float a = U.Rand(0, U.TAU), v = U.Rand(80, 300); parts.Add(new Part { k = "star", x = x, y = y, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, life = U.Rand(0.6f, 1.1f) }); } }

        static float Radius(string id) => 40 * (1 + 0.1f * Awaken(id));

        Critter CritterAt(float x, float y)
        {
            Critter best = null; float bd = 1e9f;
            foreach (var p in crits)
            {
                float R = Radius(p.id), d = U.D2(x, y, p.x, p.y - R * 0.9f);
                if (d < R * 1.25f * R * 1.25f && d < bd) { best = p; bd = d; }
            }
            return best;
        }

        static bool WalkTo(Critter p, float x, float y, float spd, float dt)
        {
            float dx = x - p.x, dy = y - p.y, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 4) { p.x = x; p.y = y; return true; }
            float s = Mathf.Min(d, spd * dt);
            p.x += dx / d * s; p.y += dy / d * s; p.hop += dt * 9;
            if (Mathf.Abs(dx) > 2) p.face = dx > 0 ? 1 : -1;
            return false;
        }

        bool TickAnim(Critter p, float dt)
        {
            if (p.sayT > 0) p.sayT = Mathf.Max(0, p.sayT - dt);
            if (p.anim == null) return false;
            p.animT += dt;
            if (p.anim == "heart" && Random.value < dt * 6) parts.Add(new Part { k = "heart", x = p.x + U.Rand(-20, 20), y = p.y - 70, vx = U.Rand(-20, 20), vy = U.Rand(-90, -60), life = 1.1f });
            if (p.anim == "sleep" && Random.value < dt * 2) parts.Add(new Part { k = "z", x = p.x + 24, y = p.y - 80, vx = 18, vy = -40, life = 1.4f });
            if (p.animT >= FARM_ANIMS[p.anim]) p.anim = null;
            return true;
        }

        void UpdateCritters(float dt)
        {
            foreach (var p in crits)
            {
                p.t -= dt;
                bool anim = TickAnim(p, dt);
                string zone = ZoneOf(p.id);
                float speed = (float)(KidPickRate(p.id, zone ?? KID[p.id].zone) * HARV.sec);   // 수확 속도 (보여 주기: 걷기·따기 시간)

                // 일하는 꼬마: 자기 구역의 나무버섯으로 가서 따고, 다음 버섯으로
                if (zone != null)
                {
                    if (p.job >= 0 && (p.job >= tree.slots.Count || tree.slots[p.job].zone != zone)) { p.job = -1; p.state = "idle"; }
                    if (p.job >= 0)
                    {
                        if (p.state == "walk") { if (WalkTo(p, p.tx, p.ty, 260 * speed, dt)) { p.state = "work"; p.t = 1.2f / speed; p.tool = "basket"; } }
                        else if (p.state == "work" && p.t <= 0)
                        {
                            var s = tree.slots[p.job];
                            p.job = -1; p.tool = null; p.state = "idle"; p.t = U.Rand(0.3f, 1.2f);
                            double gold = s.mush.gold * KidMul(p.id, zone);
                            if (Random.value < 0.6f) AddFloat(s.pos.x, s.pos.y - 30, $"+{U.Fmt(System.Math.Round(gold))}", false, U.Hex(GOLD_C));
                            if (s.mush.gem > 0) GemBurst(s.pos.x, s.pos.y, 4);
                        }
                        continue;
                    }
                    if (anim || p.t > 0) continue;
                    var mine = tree.SlotsIn(zone).ToList();
                    if (mine.Count == 0) continue;
                    int slot = U.Pick(mine); var sp = tree.SlotPos(slot);
                    p.job = slot; p.tx = sp.x + U.Rand(-16, 16); p.ty = sp.y + 34; p.state = "walk"; p.anim = null;
                    continue;
                }
                p.job = -1; p.tool = null;
                if (anim) continue;

                // 쉬는 꼬마: 나무 앞 땅을 산책
                if (p.state == "walk") { if (WalkTo(p, p.tx, p.ty, 70, dt) || p.t <= 0) { p.state = "idle"; p.t = U.Rand(1, 3.5f); } }
                else if (p.t <= 0)
                {
                    var tg = GroundSpot(); p.tx = tg.x; p.ty = tg.y; p.state = "walk"; p.t = 8;
                    if (Random.value < 0.06f && !crits.Any(q => q.sayT > 0)) { p.say = U.Pick(FARM_LINES[p.id]); p.sayT = 2.4f; }
                }
            }
        }

        // ===== effects =====
        void GemBurst(float x, float y, int n) { for (int i = 0; i < n; i++) parts.Add(new Part { k = "gem", x = x, y = y, vx = U.Rand(-160, 160), vy = U.Rand(-320, -120), life = U.Rand(0.6f, 1) }); }
        void AddFloat(float x, float y, string text, bool gem, Color col, float t = 0) { if (floats.Count < 40) floats.Add(new Float { x = x, y = y, text = text, gem = gem, col = col, t = t }); }

        // ===== camera: the view widens as the tree grows (WT.zoom per picture) =====
        // The ground (screen bottom) stays put and the view opens up and to the sides around the stage centre.
        // Entering the farm snaps to the stage's zoom; growing while here pulls back smoothly. The round field shares
        // this camera, so leaving resets it. The UI canvas is Screen Space - Camera and follows the frustum.
        public static float Zoom = 1;
        const float CAM_SIZE = 5.4f;
        float zoomVel, zoomWait, zoomTarget; bool snapZoom = true;
        // the zoomed-out backgrounds, outermost last: each is the previous one seen from further back, registered and
        // measured by Tools/art/build_farm_bg.py (rects in stage px). The scene's Background shows the faded-edge core.
        static readonly (string key, Rect r)[] BG_LAYERS =
        {
            ("Backgrounds/bg_ranch_wide", new Rect(-877.0f, -1020.0f, 3731.8f, 2100.2f)),     // up to zoom 1.91
            ("Backgrounds/bg_ranch_wide2", new Rect(-2669.7f, -3044.4f, 7317.2f, 4118.1f)),   // up to zoom 3.78
        };
        readonly List<SpriteRenderer> bgLayers = new List<SpriteRenderer>();

        void OnEnable() { snapZoom = true; SetupBackground(); }
        void OnDisable() { Zoom = 1; ApplyCamera(); }

        void SetupBackground()
        {
            var bg = transform.Find("Background")?.GetComponent<SpriteRenderer>();
            var core = SpriteDB.Get("Backgrounds/bg_ranch_core");
            if (bg == null || core == null) return;
            if (bg.sprite != core)
            {
                bg.sprite = core;
                float k = 1920f / Art.PPU / core.bounds.size.x;
                bg.transform.localScale = new Vector3(k, k, 1);
                bg.transform.localPosition = new Vector3(9.6f, -5.4f, bg.transform.localPosition.z);
            }
            for (int i = 0; i < BG_LAYERS.Length; i++)
            {
                var sp = SpriteDB.Get(BG_LAYERS[i].key);
                if (sp == null) continue;
                while (bgLayers.Count <= i)
                {
                    var r = new GameObject("BackgroundWide" + bgLayers.Count).AddComponent<SpriteRenderer>();
                    r.transform.SetParent(transform, false);
                    r.sharedMaterial = bg.sharedMaterial;
                    r.sortingLayerID = bg.sortingLayerID;
                    r.sortingOrder = bg.sortingOrder - 1 - bgLayers.Count;
                    bgLayers.Add(r);
                }
                var R = BG_LAYERS[i].r;
                var l = bgLayers[i];
                l.sprite = sp;
                float kw = R.width / Art.PPU / sp.bounds.size.x;
                l.transform.localScale = new Vector3(kw, kw, 1);
                l.transform.localPosition = new Vector3((R.x + R.width / 2) / Art.PPU, -(R.y + R.height / 2) / Art.PPU, bg.transform.localPosition.z + 0.1f * (i + 1));
            }
        }

        void UpdateZoom(float dt)
        {
            float target = WT.zoom[WT.Picture(Stage())];
            if (snapZoom) { Zoom = zoomTarget = target; zoomVel = 0; snapZoom = false; }
            // grew while watching: the bigger tree shoots up first, then the camera pulls back slowly
            if (target != zoomTarget) { zoomTarget = target; zoomWait = 0.7f; }
            if (zoomWait > 0) zoomWait -= dt;
            else Zoom = Mathf.SmoothDamp(Zoom, zoomTarget, ref zoomVel, 1.2f, Mathf.Infinity, dt);
            ApplyCamera();
        }

        static void ApplyCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographicSize = CAM_SIZE * Zoom;
            cam.transform.position = new Vector3(WT.camX / Art.PPU, -(1080 - 540 * Zoom) / Art.PPU, cam.transform.position.z);
        }

        // ===== frame =====
        void Update()
        {
            if (G?.farm == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f), now = Time.time;
            UpdateZoom(Time.deltaTime);
            HandleInput();
            UpdateCritters(dt);
            foreach (var q in parts) { q.t += dt; q.x += q.vx * dt; q.y += q.vy * dt; if (q.k == "gem") q.vy += 700 * dt; }
            parts.RemoveAll(q => q.t >= q.life);
            if (parts.Count > 200) parts.RemoveRange(0, parts.Count - 200);
            foreach (var f in floats) { f.t += dt; f.y -= 40 * dt; }
            floats.RemoveAll(f => f.t >= 1.4f);

            tree.Draw(now, hoverSlot);
            for (int i = 0; i < crits.Count; i++)
            {
                var p = crits[i];
                critPool.Get(i).Draw(p, now, p.job >= 0 ? "straw" : null, hover == p);
            }
            critPool.Trim(crits.Count);
            SpreadSays();
            DrawFx();
        }

        // 말풍선 겹침 풀기: 앞(아래)에 선 꼬마의 말풍선부터 제자리에 놓고, 뒤 꼬마의 말풍선은 겹치지 않을 때까지 위로 올린다.
        const float SAY_GAP = 6;
        readonly List<int> sayIdx = new List<int>();
        readonly List<Rect> sayPlaced = new List<Rect>();
        System.Comparison<int> sayOrder;

        void SpreadSays()
        {
            sayIdx.Clear(); sayPlaced.Clear();
            for (int i = 0; i < crits.Count; i++) if (crits[i].sayT > 0) sayIdx.Add(i);
            if (sayIdx.Count < 2) return;
            sayOrder ??= (a, b) => crits[b].y.CompareTo(crits[a].y);
            sayIdx.Sort(sayOrder);
            for (int k = 0; k < sayIdx.Count; k++)
            {
                var fc = critPool.Get(sayIdx[k]);
                var r = fc.sayRect;
                float lift = 0;
                for (bool moved = true; moved;)
                {
                    moved = false;
                    foreach (var q in sayPlaced)
                    {
                        var cur = new Rect(r.x, r.y - lift, r.width, r.height);
                        if (!cur.Overlaps(new Rect(q.x, q.y - SAY_GAP, q.width, q.height + SAY_GAP * 2))) continue;
                        // 더 올려야 할 때만 받는다. float 오차로 경계가 살짝 겹쳐 같은 값을 다시 계산하면 무한 루프가 됐다 (WebGL float32)
                        float nl = r.y + r.height + SAY_GAP - q.y;
                        if (nl > lift) { lift = nl; moved = true; }
                    }
                }
                sayPlaced.Add(new Rect(r.x, r.y - lift, r.width, r.height));
                fc.PlaceSay(lift, sayIdx.Count - 1 - k);
            }
        }

        void DrawFx()
        {
            int n = 0;
            foreach (var q in parts)
            {
                var sr = partPool.Get(n++);
                sr.transform.localPosition = Art.P(q.x, q.y);
                var c = Color.white; c.a = Mathf.Clamp01(1 - q.t / q.life);
                string sp; float px;
                switch (q.k)
                {
                    case "gem": sp = "Icons/gem"; px = 24; break;
                    case "star": sp = "Icons/star"; px = 22; break;
                    case "heart": sp = "Farm/Icons/fx_heart"; px = 26; break;
                    default: sp = "Farm/Icons/fx_zzz"; px = 30; break;
                }
                sr.sprite = SpriteDB.Get(sp); sr.color = c;
                Art.FitPx(sr, px);
            }
            partPool.Trim(n);
            n = 0;
            foreach (var f in floats)
            {
                if (f.t < 0) continue;
                floatPool.Get(n++).Draw(f.x, f.y, f.text, f.gem, f.col, Mathf.Clamp01(1.4f - f.t));
            }
            floatPool.Trim(n);
        }

        // ===== input =====
        public static Vector2 ToStage(Vector2 screen)
        {
            var w = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            return new Vector2(w.x * Art.PPU, -w.y * Art.PPU);
        }

        bool pressOverUI;
        void HandleInput()
        {
            var p = Pointer.current;
            hover = null; hoverSlot = -1;
            if (p == null || GameFlow.I.ModalOpen) return;
            // -1 = 지금 쓰는 포인터. 손을 뗀 프레임에는 터치 기록이 이미 없을 수 있어, 누를 때 UI 위였는지를 기억해 둔다.
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (p.press.wasPressedThisFrame) pressOverUI = overUI;
            if (overUI || pressOverUI) return;
            var s = ToStage(p.position.ReadValue());
            if (p is Mouse) { hover = CritterAt(s.x, s.y); if (hover == null) hoverSlot = tree.SlotAt(s.x, s.y); }
            if (!p.press.wasPressedThisFrame) return;
            var c = CritterAt(s.x, s.y);
            if (c != null) { ClickCritter(c); return; }
            int slot = tree.SlotAt(s.x, s.y);
            if (slot >= 0)
            {
                var sl = tree.slots[slot];
                AddFloat(sl.pos.x, sl.pos.y - 30, $"{sl.mush.n} · {ZONE[sl.zone].n}", false, U.Hex("#fff6e0"));
                Snd.Ui(); onPick?.Invoke("kid", null); return;
            }
            int deco = tree.DecoAt(s.x, s.y);
            if (deco >= 0) { Snd.Ui(); onPick?.Invoke("deco", deco.ToString()); return; }
            if (tree.TrunkAt(s.x, s.y)) { Snd.Ui(); onPick?.Invoke("grow", null); }
        }
    }
}
