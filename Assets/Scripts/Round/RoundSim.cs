using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Harvest round simulation, ported from the prototype's "수확 라운드 시뮬레이션" section.
    // Pure state + rules; RoundView draws it and StageController drives input and timing.
    public partial class RoundSim
    {
        public static RoundSim R;

        // ===== 필드(월드) 좌표 =====
        public static float ZOOM = 1, WW = W, WH = H;
        public static float FX0 = 40, FX1 = 1880, FY0 = 40, FY1 = 1040, BAR_Y = 1000, NO_SPAWN_Y = 900;
        public static Vector2 LAUNCH = new Vector2(960, 930);

        public static void SetWorld(float z)
        {
            ZOOM = z; WW = Mathf.Round(W / z); WH = Mathf.Round(H / z);
            FX0 = 40; FX1 = WW - 40; FY0 = 40; FY1 = WH - 40;
            BAR_Y = WH - 80; NO_SPAWN_Y = WH - 180;
            LAUNCH = new Vector2(WW / 2, WH - 150);
        }

        // Visual-only destinations supplied by the Stage HUD (StageController.Started) from the actual scene layout.
        public readonly Dictionary<string, Vector2> harvestTargets = new Dictionary<string, Vector2>();

        // ===== 상태 =====
        public RoundStats st;
        public string phase = "aim";   // aim · run · end
        public float t, runT, timeLeft, timeMax;
        public float earlyExitSec = -1;   // "지금 끝내기"로 나간 시점(판 시작 후 초). -1 = 끝까지 함 (서버 로그 stage_end의 early_exit)
        public List<Ball> balls = new List<Ball>();
        public List<Colony> colonies = new List<Colony>();
        public List<Shroom> shrooms = new List<Shroom>();
        public List<Device> devices = new List<Device>();
        public List<Cloud> clouds = new List<Cloud>();
        public List<float> regen = new List<float>();
        List<Queued> queue = new List<Queued>();
        public List<Wave> waves = new List<Wave>();
        public List<Tornado> tornados = new List<Tornado>();
        public List<Bolt> bolts = new List<Bolt>();
        public List<Ring> rings = new List<Ring>();
        public List<Part> parts = new List<Part>();
        public List<Fx> fxs = new List<Fx>();
        public float barKick, barHitX;   // bar bounce: 1 -> 0 after the harvester lands, and where it landed
        public List<FloatText> texts = new List<FloatText>();
        public List<Flyer> flyers = new List<Flyer>();
        public List<Label> labels = new List<Label>();
        public double score;
        public Dictionary<string, double> gains = new Dictionary<string, double> { ["ed"] = 0, ["md"] = 0, ["ps"] = 0 };
        public int harvests, maxCombo, maxBalls, maxChain;
        public List<string> newSpecies = new List<string>(), newGolden = new List<string>();
        public List<(string id, int s)> newStars = new List<(string, int)>();
        public HashSet<string> newRec = new HashSet<string>();
        public Shroom giant;
        public string giantState;
        public GiantWarn giantWarn;
        public float acornT, hitstop, hitstopCd, shake, flash;
        public bool festOn, paused;
        public float barX, barLen, barFx;
        public Vector2? aimStart, aimCur;
        public int colId = 1, shroomsMade;   // shroomsMade: 이번 판에 생긴 버섯 수 (밸런스 시뮬의 처치율 = 수확 ÷ 이것)
        public readonly int[] madeT = new int[8], killT = new int[8];   // 티어별 생긴 수·수확한 수 (밸런스 시뮬의 티어별 처치율)
        public Dictionary<string, float> skillLabelT = new Dictionary<string, float>();
        public Dictionary<string, float> skillFlash = new Dictionary<string, float>();
        readonly List<string> flashKeys = new List<string>();   // skillFlash의 키 목록 (프레임마다 Keys.ToList()를 만들지 않게)
        void Flash(string id) { if (!skillFlash.ContainsKey(id)) flashKeys.Add(id); skillFlash[id] = 0.6f; }
        public float flashCd;
        public int flamePal; public float flameInt = 0.5f;
        public int chain; public float chainT, chainBump; public int fever; public float feverK;
        public FeverMsg feverMsg;
        public List<Ember> embers = new List<Ember>();
        public float codexShake, scoreBump, saleBump, endT;
        public Dictionary<string, float> slotBump = new Dictionary<string, float> { ["ed"] = 0, ["md"] = 0, ["ps"] = 0 };
        public Dictionary<string, double> bag = new Dictionary<string, double>();
        public readonly Dictionary<string, int> picks = new Dictionary<string, int>();   // 버섯별 수확 횟수 (의뢰 진행도)
        public double bonusGold, coinGold;
        public float? meteorT;
        public string themeId;

        // ===== 공간 격자 =====
        const int CELL = 100;
        static readonly int GW = Mathf.CeilToInt(W / (float)CELL), GH = Mathf.CeilToInt(H / (float)CELL);
        readonly List<Shroom>[] grid = Enumerable.Range(0, GW * GH).Select(_ => new List<Shroom>()).ToArray();
        int qsCounter;

        public static RoundSim Start(string wId, string themeId, BossDef boss = null)
        {
            var st = ComputeStats(wId, themeId, boss);
            SetWorld((float)st.zoom);
            var r = new RoundSim { st = st, themeId = st.theme.id };
            R = r;
            // 튜토리얼 판(1스테이지, 튜토리얼 전)만 군락 1개 고정. 그 밖의 초반 판은 보통처럼 스폰하고 종만 제한한다 (PickSpecies)
            r.tutorial = st.boss == null && st.stage == 1 && G != null && G.tut == 0;
            r.lone = r.tutorial ? 1 : 0;
            r.timeLeft = r.timeMax = (float)st.duration;
            // 보스 라운드에는 보스 말고 다른 버섯이 나오지 않는다 (군락지·재생·거대 버섯 없음). 확률은 그대로 굴려 난수 순서를 지킨다
            r.giantState = U.Chance((float)st.giantP) && st.boss == null && r.lone == 0 && !EarlyStage(st.stage) ? "pending" : "none";
            r.barX = WW / 2; r.barLen = (float)st.barLen;
            if (st.boss != null) r.SpawnBoss(st.boss);   // 군락지·장치보다 먼저 자리를 잡는다
            foreach (var d in st.devTypes) r.PlaceDevice(d, null);
            r.barLen = Mathf.Min(r.barLen, WW / 3);   // 슬라이드 바 최대 길이: 화면의 1/3
            if (st.boss == null) for (int i = 0; i < r.MaxCol; i++) r.SpawnColony(true);
            for (int i = 0; i < st.permBalls; i++) r.balls.Add(r.MakeBall(LAUNCH.x, LAUNCH.y, true, st.hvs[i % st.hvs.Count]));
            r.RebuildGrid();
            return r;
        }

        // ===== 도형 겹침 검사 =====
        static bool Overlap(Shape a, Shape b, float m)
        {
            if (a.circle && b.circle) { float rr = a.r + b.r + m; return U.D2(a.x, a.y, b.x, b.y) < rr * rr; }
            if (!a.circle && !b.circle) return a.x0 < b.x1 + m && a.x1 + m > b.x0 && a.y0 < b.y1 + m && a.y1 + m > b.y0;
            var c = a.circle ? a : b; var r = a.circle ? b : a;
            float px = U.Clamp(c.x, r.x0, r.x1), py = U.Clamp(c.y, r.y0, r.y1);
            return U.D2(c.x, c.y, px, py) < (c.r + m) * (c.r + m);
        }

        readonly Shape areaLaunch = Shape.C(0, 0, 140), areaFloor = Shape.Rect(0, 0, W, H);
        readonly Shape areaGiant = Shape.C(0, 0, 0), areaWarning = Shape.C(0, 0, 90);

        bool AreaFree(List<Shape> shapes, Colony skipCol = null)
        {
            foreach (var shape in shapes) if (!AreaFree(shape, skipCol)) return false;
            return true;
        }

        bool AreaFree(Shape shape, Colony skipCol = null)
        {
            areaLaunch.x = LAUNCH.x; areaLaunch.y = LAUNCH.y; areaFloor.y0 = NO_SPAWN_Y;
            if (Overlap(shape, areaLaunch, 0) || Overlap(shape, areaFloor, 0)) return false;
            foreach (var c in colonies) if (c != skipCol && Overlap(shape, c.shape, 10)) return false;
            foreach (var d in devices) foreach (var s in d.Shapes()) if (Overlap(shape, s, 10)) return false;
            if (giant != null)
            {
                areaGiant.x = giant.x; areaGiant.y = giant.y; areaGiant.r = giant.r;
                if (Overlap(shape, areaGiant, 10)) return false;
            }
            if (BossBlocks(shape)) return false;
            if (giantWarn != null)
            {
                areaWarning.x = giantWarn.x; areaWarning.y = giantWarn.y;
                if (Overlap(shape, areaWarning, 10)) return false;
            }
            return true;
        }

        // ===== 군락지 =====
        Species PickSpecies(bool wall)
        {
            var w = st.weather;
            var pool = EarlyStage(st.stage) && st.boss == null
                ? EARLY_SPECIES.Take(st.stage).Select(n => SP_BY_NAME[n]).ToList()   // 초반: 스테이지 수만큼의 종만
                : SPECIES.Where(sp => IsUnlockedSp(sp) && (sp.theme == null || sp.theme == st.theme.id)).ToList();
            return U.PickWeighted(pool, sp =>
            {
                double x = TIERS[sp.t].w;
                if (st.theme.favIds.Contains(sp.id)) x *= THEME_FAV_MUL;
                if (sp.t >= 1) x *= st.rareMul;
                x *= Math.Pow(st.stageMul, sp.t);
                if (wall && sp.t >= 2) x *= 3;
                if (w.favIds.Contains(sp.id) || (w.favTier >= 0 && sp.t == w.favTier)) x *= w.favMul;
                return x;
            });
        }

        static float ClusterR(float r) => Mathf.Round(r * 1.9f);

        class Cand { public float x, y, R; public List<Vector2> offs; public bool wall; public string side; public Shape shape; }

        Cand FloorCandidate(int n, float r)
        {
            float rc = ClusterR(r), ring = rc + r + 6, rot = U.Rand(0, U.TAU);
            var offs = new List<Vector2> { Vector2.zero };
            for (int i = 0; i < n; i++)
            {
                float a = rot + i * U.TAU / n + U.Rand(-0.15f, 0.15f), dd = ring + U.Rand(-4, 6);
                offs.Add(new Vector2(Mathf.Cos(a) * dd, Mathf.Sin(a) * dd));
            }
            float RR = ring + r + 6;
            if (FX0 + RR > FX1 - RR || FY0 + RR > NO_SPAWN_Y - RR) return null;
            float x = U.Rand(FX0 + RR, FX1 - RR), y = U.Rand(FY0 + RR, NO_SPAWN_Y - RR);
            return new Cand { x = x, y = y, R = RR, offs = offs, wall = false, shape = Shape.C(x, y, RR) };
        }

        Cand WallCandidate(int n, float r)
        {
            string side = U.PickWeighted(new[] { "t", "l", "r" }, s => s == "t" ? 1840 : 860);
            float rc = ClusterR(r), step = r * 2.1f;
            var offs = new List<Vector2> { Vector2.zero };
            float half = rc;
            for (int i = 0; i < n; i++)
            {
                int k = i / 2; float sgn = i % 2 == 1 ? 1 : -1;
                float a = sgn * (rc + r + 6 + k * step), inward = r * 0.35f;
                half = Mathf.Max(half, Mathf.Abs(a) + r);
                offs.Add(side == "t" ? new Vector2(a, inward) : new Vector2(side == "l" ? inward : -inward, a));
            }
            if (side == "t")
            {
                float x = U.Rand(FX0 + half + 6, FX1 - half - 6), y = FY0;
                return new Cand { x = x, y = y, R = half, offs = offs, wall = true, side = side, shape = Shape.Rect(x - half, y - rc, x + half, y + rc) };
            }
            float lo = FY0 + half + 6, hi = NO_SPAWN_Y - half - 4;
            if (lo > hi) return null;
            float yy = U.Rand(lo, hi), xx = side == "l" ? FX0 : FX1;
            return new Cand { x = xx, y = yy, R = half, offs = offs, wall = true, side = side, shape = Shape.Rect(xx - rc, yy - half, xx + rc, yy + half) };
        }

        public int lone;                       // 군락을 자리 고정으로 만드는 수 (튜토리얼 판만 1, 그 밖은 0 = 보통 스폰)
        public bool tutorial;                  // 1스테이지 튜토리얼 판: 첫 군락을 수확기 바로 위에 움직이지 않게 둔다 (Tutorial이 길을 밝혀 준다)
        public Colony TutorialColony;
        readonly List<Species> loneSp = new List<Species>();   // 자리별 종 (처음 고른 종을 기억해, 다 수확하면 같은 종으로 다시 만든다)
        int MaxCol => lone > 0 ? lone : st.maxCol;

        bool SpawnColony(bool instant)
        {
            if (colonies.Count >= MaxCol) return true;
            if (lone > 0) return SpawnLone(instant);
            bool wall = U.Chance(0.3f);
            var sp = PickSpecies(wall);
            if (sp == null) return false;
            if (sp.wander) wall = false;
            float r = TIERS[sp.t].r;
            int n = U.RandI(3, st.colMax);
            for (bool tryWall = wall; ; tryWall = false)
            {
                for (int a = 0; a < 30; a++)
                {
                    var cand = tryWall ? WallCandidate(n, r) : FloorCandidate(n, r);
                    if (cand != null && AreaFree(cand.shape)) { CreateColony(sp, r, cand, instant); return true; }
                }
                if (!tryWall) return false;
            }
        }

        // 초반 스테이지 군락: 빈 자리 하나를 그 자리의 종으로 채운다. 첫 판 튜토리얼의 첫 군락은 발사 지점 바로 위에
        bool SpawnLone(bool instant)
        {
            int slot = -1;
            for (int i = 0; i < lone && slot < 0; i++) if (!colonies.Any(c => c.slot == i)) slot = i;
            if (slot < 0) return true;
            Species sp;
            if (slot < loneSp.Count) sp = loneSp[slot];
            else
            {
                sp = PickSpecies(false);
                for (int k = 0; k < 8 && sp != null && loneSp.Contains(sp); k++) sp = PickSpecies(false);   // 두 자리는 되도록 다른 종
                if (sp == null) return false;
                loneSp.Add(sp);
            }
            float r = TIERS[sp.t].r;
            int n = U.RandI(3, st.colMax);
            bool first = tutorial && TutorialColony == null && slot == 0;
            for (int a = 0; a < 30; a++)
            {
                var cand = FloorCandidate(n, r);
                if (cand == null) break;
                if (first)
                {
                    cand.x = LAUNCH.x; cand.y = FY0 + (NO_SPAWN_Y - FY0) * 0.35f; cand.shape = Shape.C(cand.x, cand.y, cand.R);
                }
                else if (!AreaFree(cand.shape)) continue;
                CreateColony(sp, r, cand, instant, first);
                var col = colonies[colonies.Count - 1];
                col.slot = slot;
                if (first) TutorialColony = col;
                return true;
            }
            return false;
        }

        // 튜토리얼 판의 첫 발사 때 맵에 흩뿌리는 낱개 버섯 (군락에 속하지 않는다. 수확해도 다시 생기지 않는다)
        public const int TUTORIAL_LOOSE = 8;
        bool looseSpawned;

        void SpawnLoose(int count)
        {
            for (int k = 0; k < count; k++)
            {
                var sp = PickSpecies(false);
                if (sp == null) return;
                var T = TIERS[sp.t];
                float r = T.r;
                for (int a = 0; a < 30; a++)
                {
                    float x = U.Rand(FX0 + r, FX1 - r), y = U.Rand(FY0 + r, NO_SPAWN_Y - r);
                    if (!AreaFree(Shape.C(x, y, r))) continue;
                    bool hit = false;
                    foreach (var o in shrooms) { float rr = o.r + r + 10; if (!o.dead && U.D2(x, y, o.x, o.y) < rr * rr) { hit = true; break; } }
                    if (hit) continue;
                    double hp = T.hp * st.shroomHp;
                    var m = new Shroom { sp = sp, x = x, y = y, r = r, hp = hp, maxHp = hp, golden = U.Chance((float)st.golden), jelly = sp.jelly, spore = sp.spore, grow = 0, dropMul = 1 };
                    shrooms.Add(m); shroomsMade++; madeT[sp.t]++;
                    for (int i = 0; i < 6; i++) AddPart(x + U.Rand(-r, r) * 0.6f, y + U.Rand(-r, r) * 0.6f, U.Rand(-40, 40), U.Rand(-60, -10), 0.7f, U.Hex(CATS[sp.c].light), 4);
                    break;
                }
            }
        }

        void CreateColony(Species sp, float r, Cand cand, bool instant, bool still = false)
        {
            var col = new Colony
            {
                id = colId++, sp = sp, x = cand.x, y = cand.y, R = cand.R, shape = cand.shape, wall = cand.wall, side = cand.side,
                wander = !still && !cand.wall && (sp.wander || U.Chance((float)st.wanderP)), alive = cand.offs.Count, turn = U.Rand(3, 6),
            };
            if (col.wander) { float a = U.Rand(0, U.TAU); col.vx = Mathf.Cos(a) * 40; col.vy = Mathf.Sin(a) * 40; }
            var T = TIERS[sp.t];
            for (int i = 0; i < cand.offs.Count; i++)
            {
                var o = cand.offs[i];
                bool cl = i == 0;
                double hp = (cl ? T.hp * 5 : T.hp) * st.shroomHp;
                var m = new Shroom
                {
                    sp = sp, x = col.x + o.x, y = col.y + o.y, ox = o.x, oy = o.y, r = cl ? ClusterR(r) : r, hp = hp, maxHp = hp, col = col,
                    golden = U.Chance((float)st.golden), cluster = cl, solid = cl, dropMul = cl ? U.RandI(7, 10) : 1,
                    jelly = sp.jelly, spore = sp.spore, grow = instant ? 1 : 0, wall = col.wall,
                };
                col.members.Add(m); shrooms.Add(m); shroomsMade++; madeT[sp.t]++;
            }
            colonies.Add(col);
            if (!instant)
            {
                // the ground cracks open: a dust ring at the base, a few sprouts and soil chips, then each mushroom pops
                var light = U.Hex(CATS[sp.c].light);
                AddFx(FxShape.Dust, col.x, col.y + col.R * 0.35f, col.R * 2.6f, 0.55f, new Color(0.86f, 0.8f, 0.68f, 0.85f), 0, 10);
                AddFx(FxShape.Puff, col.x, col.y, col.R * 1.6f, 0.5f, new Color(light.r, light.g, light.b, 0.55f), FxR(0, 360), 30);
                for (int i = 0; i < 3; i++)
                    AddFx(FxShape.Sprout, col.x + FxR(-col.R, col.R) * 0.7f, col.y + FxR(-col.R, col.R) * 0.4f, FxR(18, 26), 0.6f, new Color(0.62f, 0.78f, 0.5f), FxR(-15, 15), 0);
                for (int i = 0; i < 6; i++)
                    AddPart(col.x + FxR(-col.R, col.R) * 0.6f, col.y + FxR(-col.R, col.R) * 0.4f, FxR(-90, 90), FxR(-220, -120), 0.6f, new Color(0.55f, 0.43f, 0.32f), 5, FxShape.Chip);
                // these 8 keep drawing from the gameplay stream as they always did, so seeded balance runs stay identical
                for (int i = 0; i < 8; i++)
                    AddPart(col.x + U.Rand(-col.R, col.R) * 0.6f, col.y + U.Rand(-col.R, col.R) * 0.6f, U.Rand(-40, 40), U.Rand(-60, -10), 0.7f, light, 4);
            }
        }

        void UpdateWander(float h)
        {
            foreach (var c in colonies)
            {
                if (!c.wander) continue;
                c.turn -= h;
                if (c.turn <= 0) { c.turn = U.Rand(3, 6); float a0 = U.Rand(0, U.TAU); c.vx = Mathf.Cos(a0) * 40; c.vy = Mathf.Sin(a0) * 40; }
                float nx = c.x + c.vx * h, ny = c.y + c.vy * h;
                if (nx - c.R < FX0 || nx + c.R > FX1) { c.vx = -c.vx; nx = c.x; }
                if (ny - c.R < FY0 || ny + c.R > NO_SPAWN_Y) { c.vy = -c.vy; ny = c.y; }
                var shape = Shape.C(nx, ny, c.R);
                Shape blocked = null;
                foreach (var o in colonies) if (o != c && Overlap(shape, o.shape, 10)) { blocked = o.shape; break; }
                if (blocked == null) foreach (var d in devices) { foreach (var s in d.Shapes()) if (Overlap(shape, s, 10)) { blocked = s; break; } if (blocked != null) break; }
                if (blocked == null && giant != null && Overlap(shape, Shape.C(giant.x, giant.y, giant.r), 10)) blocked = Shape.C(giant.x, giant.y, giant.r);
                if (blocked == null) blocked = BossBlocker(shape);
                if (blocked != null)
                {
                    float a = Mathf.Atan2(c.y - blocked.CY, c.x - blocked.CX) + U.Rand(-0.4f, 0.4f);
                    c.vx = Mathf.Cos(a) * 40; c.vy = Mathf.Sin(a) * 40;
                    continue;
                }
                c.x = nx; c.y = ny; c.shape = shape;
                foreach (var m in c.members) { m.x = c.x + m.ox; m.y = c.y + m.oy; }
            }
        }

        // ===== 숲 장치 =====
        Device PlaceDevice(string type, Vector2? near)
        {
            for (int a = 0; a < 40; a++)
            {
                var d = DeviceCandidate(type, near, a);
                if (d != null && AreaFree(d.Shapes())) { devices.Add(d); return d; }
            }
            return null;
        }

        Device DeviceCandidate(string type, Vector2? near, int attempt)
        {
            Vector2 Pos(float m)
            {
                if (near.HasValue)
                {
                    float s = 70 + attempt * 10;
                    return new Vector2(U.Clamp(near.Value.x + U.Rand(-s, s), FX0 + m, FX1 - m), U.Clamp(near.Value.y + U.Rand(-s, s), FY0 + m, NO_SPAWN_Y - m));
                }
                return new Vector2(U.Rand(FX0 + m, FX1 - m), U.Rand(FY0 + m, NO_SPAWN_Y - m));
            }
            switch (type)
            {
                case "stump": { var p = Pos(50 * DEV_SCALE); return new Device { type = type, x = p.x, y = p.y, r = 40 * DEV_SCALE }; }
                case "moss":
                {
                    var p = Pos(140 * DEV_SCALE); bool hz = U.Chance(0.5f); float w = (hz ? 260 : 50) * DEV_SCALE, hh = (hz ? 50 : 260) * DEV_SCALE;
                    return new Device { type = type, x0 = p.x - w / 2, y0 = p.y - hh / 2, x1 = p.x + w / 2, y1 = p.y + hh / 2, hz = hz };
                }
                case "acorn":
                {
                    var p = Pos(110 * DEV_SCALE); bool hz = U.Chance(0.6f); float gap = 70 * DEV_SCALE;
                    var pts = new[] { -1, 0, 1 }.Select(k => new Vector2(p.x + (hz ? k * gap : 0), p.y + (hz ? 0 : k * gap))).ToArray();
                    return new Device { type = type, pts = pts, lit = new int[3], hz = hz };
                }
            }
            return null;
        }

        // ===== 공간 격자 =====
        void RebuildGrid()
        {
            foreach (var c in grid) c.Clear();
            foreach (var m in shrooms)
            {
                if (m.dead) continue;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((m.x - m.r) / CELL), 0, GW - 1), x1 = Mathf.Clamp(Mathf.FloorToInt((m.x + m.r) / CELL), 0, GW - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((m.y - m.r) / CELL), 0, GH - 1), y1 = Mathf.Clamp(Mathf.FloorToInt((m.y + m.r) / CELL), 0, GH - 1);
                for (int gy = y0; gy <= y1; gy++) for (int gx = x0; gx <= x1; gx++) grid[gy * GW + gx].Add(m);
            }
        }

        // QueryCircle 결과 리스트 재사용. 맞힘 → 충격파 → 다시 조회처럼 중첩되므로 스택으로 빌려 주고, 다 쓰면 FreeQuery로 돌려받는다.
        // (내용·순서는 예전과 같아 결과가 바뀌지 않는다. 돌려주지 않은 리스트는 그냥 버려질 뿐)
        readonly Stack<List<Shroom>> queryPool = new Stack<List<Shroom>>();
        void FreeQuery(List<Shroom> l) { l.Clear(); queryPool.Push(l); }

        List<Shroom> QueryCircle(float x, float y, float rad)
        {
            qsCounter++;
            var o = queryPool.Count > 0 ? queryPool.Pop() : new List<Shroom>(64);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((x - rad) / CELL), 0, GW - 1), x1 = Mathf.Clamp(Mathf.FloorToInt((x + rad) / CELL), 0, GW - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((y - rad) / CELL), 0, GH - 1), y1 = Mathf.Clamp(Mathf.FloorToInt((y + rad) / CELL), 0, GH - 1);
            for (int gy = y0; gy <= y1; gy++)
                for (int gx = x0; gx <= x1; gx++)
                    foreach (var m in grid[gy * GW + gx]) { if (m.qs == qsCounter || m.dead) continue; m.qs = qsCounter; o.Add(m); }
            return o;
        }
    }
}
