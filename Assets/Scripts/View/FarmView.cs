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
    // 누르기: 꼬마 → 꼬마 탭에서 그 꼬마 · 나무 밑동 → 성장 탭 · 장식 → 꾸미기 탭 (onPick을 받은 OutGame UI가 연다).
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
            public string state = "idle", anim, say, tool;   // state: idle · walk · work · carry (딴 버섯을 밑동 보따리로)
            public int job = -1;                             // 따러 가는 나무버섯 슬롯
            public int bframe = -1;                          // (예전 건설 그림, 쓰지 않음)
            public double carryGold;                         // 나르는 버섯 값 (내려놓을 때 뜨는 숫자)
            public float pokeAt = -99;                       // 눌린 시각 (진화 정령 리그: 갓이 통통 튄다)
            // 들기·나무 오르기 (2026-10-16): state held(들림) · fall(떨어짐) · climb(나무 오르내림) · perch(나무 위에서 노는 중) · pick(나무 위에서 따기)
            public float vy, fallTo, swing, swingV, landT, perchT, prevX;
            public int perchSlot = -1, perchDeco = -1;       // (예전) 나무 위 자리: 버섯 슬롯 · 장식
            public bool goingDown;                           // climb: 내려가는 중
            public List<int> path; public int pathI;         // 나무 길(tree_nav) 위 경로와 지금 향하는 마디
            public int navAt = -1;                           // 머무는 마디 (perch/pick)
            public string edge = "walk";                     // 지금 지나는 길 종류: walk · climb · hop
        }
        class Part { public string k; public float x, y, vx, vy, t, life; }
        class Float { public float x, y, t; public string text; public bool gem; public Color col; }

        public readonly List<Critter> crits = new List<Critter>();
        readonly List<Part> parts = new List<Part>();
        readonly List<Float> floats = new List<Float>();
        Critter hover; int hoverSlot = -1;
        public bool dirty;                                // HUD를 다시 그려야 함 (OutGame UI가 본다)
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

        // 지금 행동에 맞는 복장 (CritterRig 통짜 그림): 나르기 · 높은 구역(가지·수관)으로 가서 따기 = 오르기 · 낮은 구역 = 따기
        public string OutfitOf(Critter p)
        {
            if (p.state == "carry") return "carry";
            if (p.job < 0 || p.job >= tree.slots.Count) return null;
            return ZONE.TryGetValue(tree.slots[p.job].zone, out var z) && z.high ? "climb" : "pick";
        }
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
            p.pokeAt = Time.time;
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

        // ===== 들기 · 떨어뜨리기 · 나무 오르기 =====
        static readonly string[] HELD_LINES = { "으앗!", "높다~!", "내려 줘~", "와아!", "어디 가?" };
        static readonly string[] LAND_LINES = { "아야!", "콩!", "휴~", "다시 해 줘!" };
        static readonly string[] PERCH_LINES = { "여기 좋다~", "높은 데 최고!", "냠냠 버섯 냄새", "바람 시원해~" };
        static float Hang(Critter p) => Radius(p.id) * 1.9f;    // 갓을 잡고 든다: 손가락 아래로 몸이 매달린다

        void Pick(Critter p, Vector2 s)
        {
            held = p;
            p.state = "held"; p.job = -1; p.tool = null; p.anim = null; p.t = 0; p.goingDown = false;
            p.path = null; p.navAt = -1; p.edge = "walk";
            p.prevX = s.x; p.swing = p.swingV = 0;
            p.pokeAt = Time.time;
            Snd.Ui();
            Say(p, Loc.T(U.Pick(HELD_LINES)), null);
            Carry(p, s);
        }

        void Carry(Critter p, Vector2 s)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            p.x = s.x; p.y = s.y + Hang(p);
            // 진자: 옆으로 끌면 반대로 기울었다가 흔들리며 돌아온다
            float vx = (p.x - p.prevX) / dt; p.prevX = p.x;
            p.swingV += (-p.swing * 60 - p.swingV * 5 - vx * 0.004f) * dt;
            p.swing = Mathf.Clamp(p.swing + p.swingV * dt, -0.8f, 0.8f);
            if (Mathf.Abs(vx) > 30) p.face = vx > 0 ? 1 : -1;
        }

        // 놓기: 나무 길(tree_nav)의 마디 가까이면 거기 올라앉고, 아니면 땅으로 떨어진다
        void Drop(Critter p)
        {
            p.swing = 0; p.swingV = 0;
            int n = tree.NearestNav(new Vector2(p.x, p.y), false, 80 * Zoom);
            if (n >= 0 && !tree.CurrentNav().nodes[n].ground)
            {
                var at = tree.NavPos(n);
                Perch(p, n);
                Stars(at.x, at.y - 40);
                return;
            }
            p.state = "fall"; p.vy = 0;
            p.fallTo = Mathf.Max(p.y, HARV.y0 + (HARV.y1 - HARV.y0) * Random.value * 0.6f);   // 땅보다 아래면 그 자리
        }

        void Perch(Critter p, int node)
        {
            var at = tree.NavPos(node);
            p.state = "perch"; p.x = at.x; p.y = at.y; p.goingDown = false; p.path = null;
            p.navAt = node; p.edge = "walk";
            p.perchT = U.Rand(6f, 11f); p.t = U.Rand(0.4f, 1f);
            p.pokeAt = Time.time;
        }

        // 쉬는 정령이 나무에 오르기 시작: 머물 곳 하나를 골라 가장 가까운 땅 입구로 걸어간 뒤 길을 따라 오른다
        bool StartClimb(Critter p)
        {
            int spot = tree.RandomSpot(); if (spot < 0) return false;
            int g = tree.GroundFor(spot); if (g < 0) return false;
            p.path = tree.NavPath(g, spot); p.pathI = 0; p.goingDown = false;
            p.state = "climb"; p.edge = "walk"; p.navAt = -1;
            return p.path != null;
        }

        // 내려가기: 머물던 마디에서 가장 가까운 땅 입구까지 길을 따라
        void StartDescend(Critter p)
        {
            var path = tree.NavPathToGround(p.navAt);
            if (path == null) { p.state = "fall"; p.vy = 0; p.fallTo = Mathf.Max(p.y, HARV.y0); return; }
            p.path = path; p.pathI = 1; p.goingDown = true; p.state = "climb"; p.navAt = -1;
            p.edge = path.Count > 1 ? tree.NavEdge(path[0], path[1]) : "walk";
        }

        // 길 따라가기: 사다리·줄기는 천천히 기어오르고, 버섯 계단은 폴짝, 발판은 걷기. true = 끝까지 옴
        bool FollowPath(Critter p, float dt)
        {
            if (p.path == null || p.pathI >= p.path.Count) return true;
            var to = tree.NavPos(p.path[p.pathI]);
            float spd = p.edge == "climb" ? 110 : p.edge == "hop" ? 170 : 130;
            if (p.pathI == 0) spd = 120;                                     // 땅 입구까지 걷기
            if (p.edge == "hop") p.hop += dt * 6;                           // 폴짝 (FarmCritter가 hop으로 띄운다)
            if (!WalkTo(p, to.x, to.y, spd * Mathf.Sqrt(Zoom), dt)) return false;
            p.pathI++;
            if (p.pathI >= p.path.Count) return true;
            p.edge = tree.NavEdge(p.path[p.pathI - 1], p.path[p.pathI]);
            return false;
        }

        // 나무 위에서 오르내리는 중인가 (리그: 기어오르는 팔)
        public static bool Climbing(Critter p) => p.state == "climb" && p.edge == "climb" && p.pathI > 0;
        public static bool Hopping(Critter p) => p.state == "climb" && p.edge == "hop" && p.pathI > 0;

        // 리그2 동작 이름 (SpiritRig2 · MushRig: Tools/rig-web MOTIONS). 놀이 동작(anim)이 먼저, 그다음 상태.
        // 나무 길의 climb(사다리·줄기·밧줄)은 거의 곧게 서 있으면 사다리(뒷모습), 비스듬하면 줄기(옆모습)로 오른다. 내려가면 Down
        public string RigMotion(Critter p)
        {
            if (p.anim != null) return p.anim;
            switch (p.state)
            {
                case "held": return "held";
                case "fall": return "fall";
                case "pick": case "work": return "work";
                case "carry": return "carry";
                case "walk": return "walk";
                case "perch": return "idle";
                case "climb":
                    if (p.pathI <= 0 || p.path == null || p.pathI >= p.path.Count || p.edge == "walk") return "walk";
                    if (p.edge == "hop") return "hop";
                    var to = tree.NavPos(p.path[p.pathI]);
                    float dx = to.x - p.x, dy = to.y - p.y;            // 농장 px (y 아래로 +)
                    bool down = dy > 0;
                    return Mathf.Abs(dx) < Mathf.Abs(dy) * 0.3f ? (down ? "ladderDown" : "ladder") : (down ? "climbDown" : "climb");
            }
            return p.landT > 0 ? "land" : "idle";
        }

        // 들기·떨어지기·나무 위 놀이. true = 이번 프레임은 여기서 다 했다
        bool UpdateTreePlay(Critter p, float dt)
        {
            switch (p.state)
            {
                case "held":
                    if (held != p) { Drop(p); return true; }
                    return true;
                case "fall":
                    p.vy += 2600 * dt; p.y += p.vy * dt;
                    if (p.y >= p.fallTo)
                    {
                        p.y = p.fallTo; p.state = "idle"; p.t = U.Rand(1f, 2f); p.landT = 0.35f; p.pokeAt = Time.time;
                        if (p.vy > 900) Say(p, Loc.T(U.Pick(LAND_LINES)), null);
                    }
                    return true;
                case "climb":
                    if (FollowPath(p, dt))
                    {
                        if (p.goingDown || p.path == null) { p.state = "idle"; p.goingDown = false; p.path = null; p.t = U.Rand(1f, 3f); p.y = Mathf.Max(p.y, HARV.y0); }
                        else Perch(p, p.path[p.path.Count - 1]);
                    }
                    return true;
                case "perch":
                case "pick":
                    p.perchT -= dt; p.t -= dt;
                    if (p.state == "pick")
                    {
                        if (p.t <= 0) { p.state = "perch"; p.t = U.Rand(1.5f, 3f); parts.Add(new Part { k = "heart", x = p.x, y = p.y - 90, vx = 0, vy = -70, life = 1.1f }); }
                        return true;
                    }
                    if (p.perchT <= 0 && p.anim == null)
                    {
                        // 내려간다 (일하는 구역이 있으면 내려와서 다시 일하러 간다). 가끔은 옆 마디로 옮겨 가서 더 논다
                        var nav = tree.CurrentNav();
                        if (nav != null && Random.value < 0.4f && p.navAt >= 0)
                        {
                            var next = nav.adj[p.navAt].Where(e => !nav.nodes[e.to].ground).ToList();
                            if (next.Count > 0)
                            {
                                var e = U.Pick(next);
                                // 머무는 곳까지 이어서 간다 (계단 중간 마디에 서지 않게)
                                int spot = e.to; var seen = new HashSet<int> { p.navAt };
                                p.path = new List<int> { p.navAt, spot };
                                while (!nav.nodes[spot].spot && seen.Add(spot))
                                {
                                    var on = nav.adj[spot].Where(x => !seen.Contains(x.to) && !nav.nodes[x.to].ground).ToList();
                                    if (on.Count == 0) break;
                                    spot = U.Pick(on).to; p.path.Add(spot);
                                }
                                if (nav.nodes[spot].spot)
                                {
                                    p.pathI = 1; p.edge = e.kind; p.state = "climb"; p.goingDown = false; p.navAt = -1;
                                    return true;
                                }
                            }
                        }
                        StartDescend(p);
                        return true;
                    }
                    if (p.t <= 0 && p.anim == null)
                    {
                        // 나무 위 놀이: 버섯 자리면 따 보고, 장식이면 좋아하고, 아니면 춤·폴짝·말하기
                        p.t = U.Rand(1.8f, 3.5f);
                        float r = Random.value;
                        // 근처에 나무버섯이 있으면 따 보고, 아니면 춤·폴짝·하트·말하기
                        var me = new Vector2(p.x, p.y);
                        bool mush = tree.slots.Any(sl => ZoneOpen(sl.zone) && (sl.pos - me).sqrMagnitude < 140 * 140 * Zoom * Zoom);
                        if (mush && r < 0.4f) { p.state = "pick"; p.t = 1.3f; }
                        else if (r < 0.55f) { p.anim = "heart"; p.animT = 0; }
                        else if (r < 0.8f) { p.anim = U.Pick(new[] { "dance", "jump" }); p.animT = 0; }
                        else if (!crits.Any(q => q.sayT > 0)) Say(p, Loc.T(U.Pick(PERCH_LINES)), null);
                    }
                    return true;
            }
            return false;
        }

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
                if (p.landT > 0) p.landT -= dt;
                if (p.state == "held" || p.state == "fall" || p.state == "climb" || p.state == "perch" || p.state == "pick")
                {
                    TickAnim(p, dt);
                    if (UpdateTreePlay(p, dt)) continue;
                }
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
                            if (s.mush.gem > 0) GemBurst(s.pos.x, s.pos.y, 4);
                            // 딴 버섯은 지고 밑동 보따리까지 나른다 (골드는 내려놓을 때 뜬다)
                            p.job = -1; p.tool = null; p.state = "carry";
                            p.carryGold = s.mush.gold * KidMul(p.id, zone);
                            p.tx = WT.x + U.Rand(-60, 60) * Zoom; p.ty = WT.y + U.Rand(10, 30);
                        }
                        continue;
                    }
                    if (p.state == "carry")
                    {
                        if (WalkTo(p, p.tx, p.ty, 200 * speed, dt))
                        {
                            p.state = "idle"; p.t = U.Rand(0.3f, 1.2f);
                            if (Random.value < 0.6f) AddFloat(p.x, p.y - 90, $"+{U.Fmt(System.Math.Round(p.carryGold))}", false, U.Hex(GOLD_C));
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
                if (p.state == "carry") p.state = "idle";   // 일하는 구역에서 빠지면 짐을 내려놓는다
                if (anim) continue;

                // 쉬는 꼬마: 나무 앞 땅을 산책
                if (p.state == "walk") { if (WalkTo(p, p.tx, p.ty, 70, dt) || p.t <= 0) { p.state = "idle"; p.t = U.Rand(1, 3.5f); } }
                else if (p.t <= 0)
                {
                    // 가끔은 나무에 올라가 논다
                    if (Random.value < 0.3f && crits.Count(q => q.state == "perch" || q.state == "climb" || q.state == "pick") < 4 && StartClimb(p))
                        continue;
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
        // 정령 누르기: 짧게 눌렀다 떼면 톡 (갓 통통), 꾹 누르거나 끌면 들어 올린다 (들린 채 버둥버둥). 놓은 곳이 나무 위 자리면 거기 올라가 놀고, 아니면 떨어진다
        Critter pressed, held;
        float pressT; Vector2 pressAt;
        const float HOLD_SEC = 0.25f, DRAG_PX = 14;

        void HandleInput()
        {
            var p = Pointer.current;
            hover = null; hoverSlot = -1;
            if (p == null || App.ModalOpen) { if (held != null) Drop(held); held = pressed = null; return; }
            var s = ToStage(p.position.ReadValue());
            if (held != null)
            {
                if (p.press.isPressed) { Carry(held, s); return; }
                Drop(held); held = null; return;
            }
            if (pressed != null)
            {
                if (!p.press.isPressed) { ClickCritter(pressed); pressed = null; return; }
                if (Time.unscaledTime - pressT > HOLD_SEC || (s - pressAt).sqrMagnitude > DRAG_PX * DRAG_PX) { Pick(pressed, s); pressed = null; }
                return;
            }
            // -1 = 지금 쓰는 포인터. 손을 뗀 프레임에는 터치 기록이 이미 없을 수 있어, 누를 때 UI 위였는지를 기억해 둔다.
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (p.press.wasPressedThisFrame) pressOverUI = overUI;
            if (overUI || pressOverUI) return;
            if (p is Mouse) { hover = CritterAt(s.x, s.y); if (hover == null) hoverSlot = tree.SlotAt(s.x, s.y); }
            if (!p.press.wasPressedThisFrame) return;
            var c = CritterAt(s.x, s.y);
            if (c != null) { pressed = c; pressT = Time.unscaledTime; pressAt = s; return; }
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
