using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Mushroom farm world: one ranch with the mushroom tree inside. Caught critters wander and ask for things, walk to
    // the tree to pick ripe mushrooms, and build buildings (a builder is busy until the building is done, real time).
    // Placing a building: the ghost follows the mouse over a tile grid inside the fence; each footprint tile turns green
    // (free) or red (blocked) and a left click builds right there. Moving a building: press and hold it (or drag it), and
    // the same ghost follows the pointer until it is released on free tiles.
    // Fixed objects (background, tree, ghost tiles) are placed in the hierarchy; critters, buildings, particles and
    // floating texts are clones of hidden template slots.
    public class FarmView : MonoBehaviour
    {
        public static FarmView I;

        [Header("Scene")]
        public GrandpaRig grandpa;
        public FarmTreeView tree;

        [Header("Buildings")]
        public Transform buildLayer;
        public BuildingView buildingTemplate;             // hidden slot, one clone per building
        public GameObject ghost;                          // placement preview
        public SpriteRenderer ghostSprite;
        public SpriteRenderer[] ghostTiles = new SpriteRenderer[24];   // footprint tiles (largest building 6×4)
        public SpriteRenderer buildGrid;                  // where buildings may go (shown while placing)

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
            public string state = "idle", anim, say, tool;   // state: idle · walk · work (picking) · build
            public int job = -1;                             // tree slot this critter is going to pick
            public int bframe = -1;                          // hammering frame while building (0 raised · 1 strike, -1 not building)
        }
        class Part { public string k; public float x, y, vx, vy, t, life; }
        class Float { public float x, y, t; public string text; public bool gem; public Color col; }

        public readonly List<Critter> crits = new List<Critter>();
        public readonly List<int> jobs = new List<int>();            // ripe tree slots waiting for a critter
        public readonly HashSet<int> busy = new HashSet<int>();      // slots queued or being picked
        readonly List<Part> parts = new List<Part>();
        readonly List<Float> floats = new List<Float>();
        Critter hover; int hoverFruit = -1;
        public bool dirty;                                // HUD needs a redraw (FarmScreen polls it)

        // placement (건물 배치 · 옮기기)
        public string PlaceId { get; private set; }
        public int MoveUid { get; private set; }          // 옮기는 중인 건물 (0 = 새로 짓기)
        public int placeX, placeY;
        public bool Placing => PlaceId != null;
        public bool Moving => Placing && MoveUid != 0;
        public bool PlaceOk => Placing && CanPlace(BUILDING[PlaceId], placeX, placeY, MoveUid);
        Vector2 grab;                                     // 잡은 곳 − 건물 가운데 (옮길 때 건물이 손 밑에서 튀지 않게)
        SaveData.Bld pressB; Vector2 pressAt; float pressT;   // 누르고 있는 건물 (꾹 누르거나 끌면 옮기기)

        Pool<FarmCritter> critPool;
        Pool<BuildingView> buildPool;
        Pool<SpriteRenderer> partPool;
        Pool<FarmFloat> floatPool;

        void Awake()
        {
            I = this;
            critPool = new Pool<FarmCritter>(critterTemplate, critterLayer);
            buildPool = new Pool<BuildingView>(buildingTemplate, buildLayer);
            partPool = new Pool<SpriteRenderer>(partTemplate, fx);
            floatPool = new Pool<FarmFloat>(floatTemplate, fx);
        }

        // ===== enter · leave =====
        public void Enter()
        {
            FarmEnsure(); SaveGame();
            CancelPlace();
            Sync(true);
            dirty = true;
        }

        // Leaving the farm: queued picking finishes at once. Buildings and growing mushrooms keep their real-time timers.
        public void Flush()
        {
            if (G?.farm == null) return;
            CancelPlace();
            foreach (var w in crits) if (w.job >= 0) { PickFruit(w.job, w.id); w.job = -1; w.state = "idle"; w.tool = null; }
            foreach (var j in jobs) PickFruit(j, null);
            jobs.Clear(); busy.Clear();
            SaveGame();
        }

        static Critter NewCritter(Special k, float x, float y) => new Critter
        { id = k.id, kind = k, x = x, y = y, tx = x, ty = y, t = U.Rand(0.2f, 2), face = U.Chance(0.5f) ? 1 : -1, hop = U.Rand(0, 6) };

        // Caught critters move in; a newcomer walks in from the ranch gate. Builders start at their site.
        public void Sync(bool initial)
        {
            foreach (var k in FarmOwned())
            {
                if (crits.Any(p => p.id == k.id)) continue;
                var site = BuilderSpot(k.id);
                var p0 = site ?? (initial ? WanderSpot() : new Vector2(960, FARM.y1));
                crits.Add(NewCritter(k, p0.x, p0.y));
            }
            crits.RemoveAll(p => !HasSpecial(p.id));
        }

        static Vector2 WanderSpot() => new Vector2(U.Rand(FARM.x0, FARM.x1), U.Rand(FARM.y0, FARM.y1));

        // 짓고 있는 건물 앞 (그 꼬마가 서는 곳)
        static Vector2? BuilderSpot(string critter)
        {
            var b = G.farm.blds.FirstOrDefault(x => !x.done && x.critter == critter);
            if (b == null) return null;
            var r = BuildRect(BUILDING[b.id], b.gx, b.gy);
            return new Vector2(r.center.x + (b.uid % 2 == 0 ? -1 : 1) * r.width * 0.32f, r.yMax + 14);
        }

        static float BuildCenterX(string critter)
        {
            var b = G.farm.blds.FirstOrDefault(x => !x.done && x.critter == critter);
            return b == null ? 0 : BuildRect(BUILDING[b.id], b.gx, b.gy).center.x;
        }

        // ===== critters =====
        public void Say(Critter p, string line, string anim)
        {
            p.say = line; p.sayT = 2.8f;
            if (anim != null) { p.anim = anim; p.animT = 0; if (p.job < 0 && p.state != "build") { p.state = "idle"; p.t = Mathf.Max(p.t, FARM_ANIMS[anim] + 0.3f); } }
        }

        void ClickCritter(Critter p)
        {
            bool free = p.job < 0 && p.state != "build";
            if (FarmReady(p.id))
            {
                var r = FarmServe(p.id).Value; SaveGame(); Snd.Buy();
                Say(p, r.full ? "호감도 가득! 진화할 수 있어!" : U.Pick(FARM_LINES["thanks"]), free ? "heart" : null);
                AddFloat(p.x, p.y - 200, $"+{r.gem}", true, U.Hex("#a8f5d4"));
                if (r.bonus > 0) AddFloat(p.x + 60, p.y - 240, $"보너스 +{r.bonus}", true, U.Hex("#ffe36e"), -0.15f);
                if (r.dia > 0) AddFloat(p.x - 70, p.y - 240, $"{UIUtil.Ic("dia")}+{r.dia}", false, U.Hex("#f2b8ff"), -0.1f);
                GemBurst(p.x, p.y - 40, 12);
                if (r.heart) AddFloat(p.x, p.y - 280, $"호감도 {HeartStr(p.id)}", false, U.Hex("#ff7aa8"), -0.3f);
                dirty = true;
                return;
            }
            Snd.Ui();
            if (p.state == "build") { Say(p, U.Pick(FARM_LINES["build"]), null); return; }
            if (p.job >= 0) { Say(p, U.Pick(FARM_LINES["field"]), null); return; }
            string anim = U.Pick(FARM_ANIMS.Keys.ToList());
            Say(p, anim == "sleep" ? "쿨쿨... 음냐..." : U.Pick(FARM_LINES[p.id].Concat(FARM_LINES["all"]).ToList()), anim);
        }

        // 모두 돌보기 → (돌본 꼬마 수, 균사석, 보너스, 다이아몬드)
        public (int n, int gem, int bonus, int dia) CareAll()
        {
            int n = 0, gem = 0, bonus = 0, dia = 0;
            foreach (var p in crits)
            {
                var rr = FarmServe(p.id); if (rr == null) continue;
                var r = rr.Value;
                n++; gem += r.gem; bonus += r.bonus; dia += r.dia;
                Say(p, r.full ? "호감도 가득! 진화할 수 있어!" : U.Pick(FARM_LINES["thanks"]), p.job < 0 && p.state != "build" ? "jump" : null);
                AddFloat(p.x, p.y - 200, $"+{r.gem + r.bonus}", true, U.Hex("#a8f5d4"));
                GemBurst(p.x, p.y - 40, 5);
            }
            if (n > 0) { SaveGame(); dirty = true; }
            return (n, gem, bonus, dia);
        }

        public void Evolved(string id)
        {
            var p = crits.FirstOrDefault(q => q.id == id);
            if (p == null) return;
            Say(p, "반짝반짝… 몸이 커졌어!", "spin");
            AddFloat(p.x, p.y - 200, "진화!", false, U.Hex("#ffe36e"));
            Stars(p.x, p.y - 50);
        }

        public void StarredUp(string id)
        {
            var p = crits.FirstOrDefault(q => q.id == id);
            if (p == null) return;
            Say(p, "힘이 솟아나! 더 열심히 할게!", p.job < 0 && p.state != "build" ? "jump" : null);
            AddFloat(p.x, p.y - 200, $"★{CStarOf(id)}", false, U.Hex("#ffe36e"));
            Stars(p.x, p.y - 50);
        }

        public void TreeLeveled()
        {
            var top = tree.Top;
            AddFloat(TREE.x, top - 30, $"버섯 나무 Lv.{TreeLv()}!", false, U.Hex("#ffe36e"));
            Stars(TREE.x, (top + TREE.y) / 2);
            foreach (var p in crits) if (p.job < 0 && p.state != "build" && Vector2.Distance(new Vector2(p.x, p.y), new Vector2(TREE.x, TREE.y)) < 500) Say(p, "나무가 커졌다!", "jump");
        }

        void Stars(float x, float y) { for (int i = 0; i < 30; i++) { float a = U.Rand(0, U.TAU), v = U.Rand(80, 300); parts.Add(new Part { k = "star", x = x, y = y, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, life = U.Rand(0.6f, 1.1f) }); } }

        static float Radius(string id) => 44 * (1 + 0.15f * EvoOf(id));

        // 누를 수 있는 꼬마 (부탁 말풍선 포함)
        Critter CritterAt(float x, float y)
        {
            Critter best = null; float bd = 1e9f;
            foreach (var p in crits)
            {
                float R = Radius(p.id), d = U.D2(x, y, p.x, p.y - R * 0.9f);
                bool bub = FarmReady(p.id) && p.sayT <= 0 && x > p.x - 40 && x < p.x + 40 && y > p.y - 170 && y < p.y - 90;
                if ((d < R * 1.25f * R * 1.25f || bub) && d < bd) { best = p; bd = d; }
            }
            return best;
        }

        // 쉬러 갈 곳: 다 지은 건물 근처를 좋아한다
        static Vector2 WanderTarget(Critter p)
        {
            var built = G.farm.blds.Where(b => b.done).ToList();
            if (built.Count > 0 && U.Chance(0.3f))
            {
                var b = U.Pick(built); var r = BuildRect(BUILDING[b.id], b.gx, b.gy);
                return new Vector2(U.Clamp(r.center.x + U.Rand(-r.width / 2, r.width / 2), FARM.x0, FARM.x1), U.Clamp(r.yMax + U.Rand(10, 50), FARM.y0, FARM.y1));
            }
            float a = U.Rand(0, U.TAU), rr = U.Rand(120, 380);
            return new Vector2(U.Clamp(p.x + Mathf.Cos(a) * rr, FARM.x0, FARM.x1), U.Clamp(p.y + Mathf.Sin(a) * rr * 0.6f, FARM.y0, FARM.y1));
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
            return true;   // 동작 중에는 걷지 않는다
        }

        void UpdateCritters(float dt)
        {
            foreach (var p in crits)
            {
                p.t -= dt;
                bool anim = TickAnim(p, dt);

                // 건설: 현장으로 가서 다 지을 때까지 망치질 (그동안 버섯을 따지 못한다)
                var site = BuilderSpot(p.id);
                if (site != null)
                {
                    if (p.job >= 0) { jobs.Add(p.job); p.job = -1; }
                    if (p.state == "build" && (Mathf.Abs(p.x - site.Value.x) > 6 || Mathf.Abs(p.y - site.Value.y) > 6)) p.state = "walk";   // 짓던 건물을 옮겼다
                    if (p.state != "build")
                    {
                        p.state = "walk"; p.tool = null;
                        if (WalkTo(p, site.Value.x, site.Value.y, 240, dt)) { p.state = "build"; p.tool = "hammer"; p.face = site.Value.x < BuildCenterX(p.id) ? 1 : -1; }
                    }
                    continue;
                }
                if (p.state == "build") { p.state = "idle"; p.tool = null; p.t = U.Rand(0.5f, 1.5f); }

                // 버섯 따기: 나무 아래로 가서 따 온다
                if (p.job >= 0)
                {
                    float ws = WorkSpeed(p.id);
                    if (p.state == "walk") { if (WalkTo(p, p.tx, p.ty, 380 * ws, dt)) { p.state = "work"; p.t = TREE.pick / ws; p.face = tree.SlotPos(p.job).x > p.x ? 1 : -1; } }
                    else if (p.state == "work" && p.t <= 0)
                    {
                        int slot = p.job; p.job = -1; p.tool = null; p.state = "idle"; p.t = U.Rand(0.2f, 0.6f);
                        busy.Remove(slot);
                        var pos = tree.SlotPos(slot);
                        var y = PickFruit(slot, p.id);
                        if (y != null)
                        {
                            SaveGame(); dirty = true; Snd.Buy();
                            AddFloat(pos.x, pos.y - 30, $"+{y.Value.gem}{(y.Value.spore > 0 ? $"  포자 +{y.Value.spore}" : "")}", true, U.Hex("#a8f5d4"));
                            GemBurst(pos.x, pos.y, 6);
                        }
                    }
                    continue;
                }
                if (anim) continue;
                if (jobs.Count > 0)
                {
                    int bi = 0; float bd = 1e12f;
                    for (int i = 0; i < jobs.Count; i++) { var sp = tree.SlotPos(jobs[i]); float d = U.D2(p.x, p.y, sp.x, TREE.y); if (d < bd) { bd = d; bi = i; } }
                    int slot = jobs[bi]; jobs.RemoveAt(bi);
                    var spos = tree.SlotPos(slot);
                    p.job = slot; p.tx = Mathf.Clamp(spos.x + U.Rand(-20, 20), FARM.x0, FARM.x1); p.ty = TREE.y + U.Rand(8, 50); p.state = "walk"; p.anim = null;
                    p.tool = "basket";
                    continue;
                }

                // 산책
                if (p.state == "walk") { if (WalkTo(p, p.tx, p.ty, 70, dt) || p.t <= 0) { p.state = "idle"; p.t = U.Rand(1, 3.5f); } }
                else if (p.t <= 0)
                {
                    var tg = WanderTarget(p); p.tx = tg.x; p.ty = tg.y; p.state = "walk"; p.t = 8;
                    if (Random.value < 0.06f && !crits.Any(q => q.sayT > 0)) { p.say = U.Pick(FARM_LINES[p.id]); p.sayT = 2.4f; }
                }
            }
        }

        // ===== tree =====
        public int Workers => crits.Count(p => BuilderSpot(p.id) == null);
        public int Picking => jobs.Count + crits.Count(w => w.job >= 0);

        // 다 자란 버섯 하나를 따러 보내기
        public bool PickQueue(int slot, bool quiet)
        {
            var sl = G.farm.tree.slots;
            if (slot < 0 || slot >= sl.Count || busy.Contains(slot)) return false;
            if (!FruitRipe(sl[slot])) return false;
            if (Workers == 0)
            {
                if (!quiet) { Snd.Err(); GameFlow.I.ShowToast(crits.Count == 0 ? "특수 버섯(꼬마)을 잡으면 버섯을 따 줘요" : "꼬마들이 모두 건물을 짓고 있어요"); }
                return false;
            }
            jobs.Add(slot); busy.Add(slot);
            return true;
        }

        // 다 자란 버섯을 모두 따러 보내기 → 보낸 개수
        public int PickAll()
        {
            int n = 0;
            for (int i = 0; i < G.farm.tree.slots.Count; i++) if (PickQueue(i, true)) n++;
            if (n > 0) dirty = true;
            return n;
        }

        void FruitClick(int slot)
        {
            var f = G.farm.tree.slots[slot]; var pos = tree.SlotPos(slot);
            if (!FruitRipe(f))
            {
                AddFloat(pos.x, pos.y - 30, $"{FRUITS[f.kind].n} · {Mmss((long)System.Math.Ceiling((f.at - Now()) / 1000))}", false, U.Hex("#fff6e0"));
                Snd.Ui(); return;
            }
            if (busy.Contains(slot)) return;
            if (PickQueue(slot, false)) { Snd.Ui(); dirty = true; }
        }

        // ===== buildings =====
        public bool BeginPlace(string id)
        {
            var spot = FirstSpot(BUILDING[id]);
            if (spot == null) return false;
            PlaceId = id; MoveUid = 0; grab = Vector2.zero; placeX = spot.Value.x; placeY = spot.Value.y;
            dirty = true;
            return true;
        }

        // 지은(짓는 중인) 건물 들어 올리기: at = 잡은 곳 (화면 좌표)
        public void BeginMove(SaveData.Bld b, Vector2 at)
        {
            PlaceId = b.id; MoveUid = b.uid; placeX = b.gx; placeY = b.gy;
            grab = at - BuildRect(BUILDING[b.id], b.gx, b.gy).center;
            dirty = true;
        }

        public void CancelPlace() { pressB = null; if (PlaceId == null) return; PlaceId = null; MoveUid = 0; dirty = true; }

        // 옮기는 건물을 지금 자리에 내려놓기 (못 놓으면 false, 계속 들고 있다)
        public bool ConfirmMove()
        {
            if (!Moving || !PlaceOk) return false;
            var old = G.farm.blds.FirstOrDefault(x => x.uid == MoveUid);
            if (old != null && old.gx == placeX && old.gy == placeY) { CancelPlace(); return true; }   // 제자리에 내려놓음
            if (!BuildMove(MoveUid, placeX, placeY)) return false;
            var r = BuildRect(BUILDING[PlaceId], placeX, placeY);
            PlaceId = null; MoveUid = 0; dirty = true;
            AddFloat(r.center.x, r.yMin - 20, "옮겼어요!", false, U.Hex("#fff6e0"));
            for (int i = 0; i < 10; i++) parts.Add(new Part { k = "star", x = r.center.x + U.Rand(-r.width / 2, r.width / 2), y = r.yMax - U.Rand(0, 20), vx = U.Rand(-60, 60), vy = U.Rand(-160, -60), life = U.Rand(0.4f, 0.7f) });
            return true;
        }

        // 그 자리에 건설 시작 → 맡은 꼬마가 들어 있는 건물 (못 지으면 null)
        public SaveData.Bld ConfirmPlace()
        {
            if (!PlaceOk || Moving) return null;
            var idle = crits.Where(p => p.job < 0 && BuilderSpot(p.id) == null).Select(p => p.id).ToList();
            var bld = BuildStart(PlaceId, placeX, placeY, idle);
            if (bld == null) return null;
            PlaceId = null; dirty = true;
            var c = crits.FirstOrDefault(p => p.id == bld.critter);
            if (c != null) { c.anim = null; Say(c, "내가 지을게! 뚝딱뚝딱!", null); }
            return bld;
        }

        // 마우스(손가락) 위치를 건물 가운데로 삼아 격자에 맞춘다 (옮길 때는 잡은 곳 기준)
        void MoveGhost(float x, float y)
        {
            var b = BUILDING[PlaceId];
            x -= grab.x; y -= grab.y;
            int gx = Mathf.RoundToInt((x - GRID.x0) / GRID.cell - b.w / 2f), gy = Mathf.RoundToInt((y - GRID.y0) / GRID.cell - b.h / 2f);
            gx = Mathf.Clamp(gx, 0, GRID.cols - b.w); gy = Mathf.Clamp(gy, 0, GRID.rows - b.h);
            if (gx != placeX || gy != placeY) { placeX = gx; placeY = gy; dirty = true; }
        }

        public Rect PlaceRect => Placing ? BuildRect(BUILDING[PlaceId], placeX, placeY) : default;

        void TickBuilds()
        {
            foreach (var b in BuildTick())
            {
                var B = BUILDING[b.id]; var r = BuildRect(B, b.gx, b.gy);
                AddFloat(r.center.x, r.yMin - 30, $"{B.n} 완성!", false, U.Hex("#ffe36e"));
                Stars(r.center.x, r.center.y);
                Snd.Record();
                GameFlow.I.ShowToast($"{B.n} 완성! {string.Format(B.statFmt, U.Pct(B.val))}");
                var c = crits.FirstOrDefault(p => p.id == b.critter);
                if (c != null) { c.state = "idle"; c.tool = null; Say(c, "다 지었다! 어때?", "jump"); }
                dirty = true;
            }
        }

        // ===== effects =====
        void HammerSparks(Critter p)
        {
            float R = Radius(p.id), x = p.x + p.face * R * 1.3f, y = p.y - 8;
            for (int i = 0; i < 3; i++) parts.Add(new Part { k = "star", x = x, y = y, vx = p.face * U.Rand(20, 120), vy = U.Rand(-170, -80), life = U.Rand(0.25f, 0.4f) });
        }
        void GemBurst(float x, float y, int n) { for (int i = 0; i < n; i++) parts.Add(new Part { k = "gem", x = x, y = y, vx = U.Rand(-160, 160), vy = U.Rand(-320, -120), life = U.Rand(0.6f, 1) }); }
        void AddFloat(float x, float y, string text, bool gem, Color col, float t = 0) => floats.Add(new Float { x = x, y = y, text = text, gem = gem, col = col, t = t });

        // ===== frame =====
        float buildTickT;
        void Update()
        {
            if (G?.farm == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f), now = Time.time;
            if ((buildTickT -= dt) <= 0) { buildTickT = 0.5f; TickBuilds(); }
            HandleInput();
            UpdateCritters(dt);
            foreach (var q in parts) { q.t += dt; q.x += q.vx * dt; q.y += q.vy * dt; if (q.k == "gem" || q.k == "dirt") q.vy += 700 * dt; }
            parts.RemoveAll(q => q.t >= q.life);
            if (parts.Count > 200) parts.RemoveRange(0, parts.Count - 200);
            foreach (var f in floats) { f.t += dt; f.y -= 40 * dt; }
            floats.RemoveAll(f => f.t >= 1.4f);

            grandpa.sparkle = FarmReadyCount() > 0 && Mathf.Sin(now * 2) > 0;
            tree.Draw(now, busy, hoverFruit);
            DrawBuildings();
            for (int i = 0; i < crits.Count; i++)
            {
                var p = crits[i];
                string hat = BuilderSpot(p.id) != null ? "hardhat" : p.job >= 0 ? "straw" : null;
                // 건설 중: 망치를 들어 올렸다(0) 내려친다(1). 내려치는 순간 불똥이 튄다
                int fr = p.state == "build" && p.anim == null ? ((now + p.hop) % BUILDER.beat > BUILDER.beat - BUILDER.strike ? 1 : 0) : -1;
                if (fr == 1 && p.bframe == 0) HammerSparks(p);
                p.bframe = fr;
                critPool.Get(i).Draw(p, now, hat, hover == p, FarmReady(p.id) ? FarmKindOf(p.id) : null);
            }
            critPool.Trim(crits.Count);
            DrawFx();
        }

        void DrawBuildings()
        {
            var list = G.farm.blds;
            for (int i = 0; i < list.Count; i++) buildPool.Get(i).Draw(list[i], Moving && list[i].uid == MoveUid);
            buildPool.Trim(list.Count);

            UIUtil.Show(ghost, Placing);
            if (!Placing) return;
            var b = BUILDING[PlaceId]; var r = BuildRect(b, placeX, placeY);
            bool ok = PlaceOk;
            ghost.transform.localPosition = Art.P(r.center.x, r.yMax);
            BuildingView.Fit(ghostSprite, SpriteDB.Get("Farm/Props/" + b.id), b.px);
            ghostSprite.color = ok ? new Color(1, 1, 1, 0.75f) : new Color(1, 0.65f, 0.65f, 0.6f);
            // 칸마다 초록(놓을 수 있음) · 빨강(막힘)
            int n = 0;
            for (int y = 0; y < b.h; y++)
                for (int x = 0; x < b.w; x++, n++)
                {
                    if (n >= ghostTiles.Length) break;
                    var t = ghostTiles[n];
                    t.enabled = true;
                    float cx = GRID.x0 + (placeX + x + 0.5f) * GRID.cell, cy = GRID.y0 + (placeY + y + 0.5f) * GRID.cell;
                    t.transform.localPosition = new Vector3((cx - r.center.x) / Art.PPU, -(cy - r.yMax) / Art.PPU, 0);
                    t.color = TileOk(placeX + x, placeY + y, MoveUid) ? new Color(0.35f, 1, 0.45f, 0.5f) : new Color(1, 0.25f, 0.2f, 0.55f);
                }
            for (; n < ghostTiles.Length; n++) ghostTiles[n].enabled = false;
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
                    case "spore": sp = "Icons/spore"; px = 18; break;
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

        void HandleInput()
        {
            var p = Pointer.current;
            hover = null; hoverFruit = -1;
            UIUtil.Show(buildGrid, Placing);
            if (p == null || GameFlow.I.ModalOpen) { pressB = null; return; }
            bool isMouse = p is Mouse;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(isMouse ? -1 : 0);
            var s = ToStage(p.position.ReadValue());
            if (Moving)
            {
                // 옮기기: 누른 채로 끌면(마우스는 올리기만 해도) 따라오고, 떼면 그 자리에 놓는다 (빨간 칸이면 계속 들고 있다)
                if (!overUI && (isMouse || p.press.isPressed)) MoveGhost(s.x, s.y);
                if (isMouse && Mouse.current.rightButton.wasPressedThisFrame) { GameFlow.I.OnAction("placecancel", null, null); return; }
                if (!overUI && p.press.wasReleasedThisFrame) GameFlow.I.OnAction("placeok", null, null);
                return;
            }
            if (Placing)
            {
                // 마우스: 올리면 잔상이 따라오고, 왼쪽 클릭이면 바로 짓는다 (오른쪽 클릭 = 취소)
                // 손가락: 누른 채로 끌어 옮기고, 떼면 짓는다
                if (isMouse)
                {
                    if (!overUI) MoveGhost(s.x, s.y);
                    if (Mouse.current.rightButton.wasPressedThisFrame) { GameFlow.I.OnAction("placecancel", null, null); return; }
                    if (!overUI && p.press.wasPressedThisFrame) GameFlow.I.OnAction("placeok", null, null);
                }
                else
                {
                    if (!overUI && p.press.isPressed) MoveGhost(s.x, s.y);
                    if (!overUI && p.press.wasReleasedThisFrame) GameFlow.I.OnAction("placeok", null, null);
                }
                return;
            }
            if (overUI) { pressB = null; return; }
            if (isMouse) { hover = CritterAt(s.x, s.y); if (hover == null) hoverFruit = tree.FruitAt(s.x, s.y); }

            // 누르고 있는 건물: 꾹 누르거나 끌면 들어 올리고, 그냥 떼면 누른 것으로 친다
            if (pressB != null)
            {
                if (p.press.isPressed)
                {
                    if (Time.unscaledTime - pressT >= BUILD_HOLD || (s - pressAt).magnitude >= BUILD_DRAG)
                    {
                        var held = pressB; pressB = null;
                        BeginMove(held, pressAt); MoveGhost(s.x, s.y); Snd.Ui();
                    }
                    return;
                }
                var tapped = pressB; pressB = null;
                if (p.press.wasReleasedThisFrame) BuildingClick(tapped);
                return;
            }

            if (!p.press.wasPressedThisFrame) return;
            var c = CritterAt(s.x, s.y);
            if (c != null) { ClickCritter(c); return; }
            int fruit = tree.FruitAt(s.x, s.y);
            if (fruit >= 0) { FruitClick(fruit); return; }
            // 건물은 나무 앞에 그려지므로 밑동보다 먼저 본다
            var b = G.farm.blds.OrderBy(x => x.gy + BUILDING[x.id].h).LastOrDefault(x => BuildRect(BUILDING[x.id], x.gx, x.gy).Contains(new Vector2(s.x, s.y)));
            if (b != null) { pressB = b; pressAt = s; pressT = Time.unscaledTime; return; }
            if (tree.TrunkAt(s.x, s.y)) { GameFlow.I.OnAction("treepanel", "open", null); return; }
        }

        // 건물을 짧게 눌렀다: 짓는 중이면 가속 창, 다 지었으면 능력치 · 옮기는 법
        void BuildingClick(SaveData.Bld b)
        {
            if (!b.done && Now() < b.at) { GameFlow.I.OpenAccel("build", b.uid); return; }
            var r = BuildRect(BUILDING[b.id], b.gx, b.gy);
            AddFloat(r.center.x, r.yMin - 20, string.Format(BUILDING[b.id].statFmt, U.Pct(BUILDING[b.id].val)), false, U.Hex("#fff6e0"));
            AddFloat(r.center.x, r.yMin + 10, "꾹 누르면 옮길 수 있어요", false, U.Hex("#d8f0c0"), -0.2f);
            Snd.Ui();
        }
    }
}
