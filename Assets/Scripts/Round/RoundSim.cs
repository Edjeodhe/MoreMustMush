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
            r.sporeSlowMul = SporeSlowMulNow();
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
            if (st.boss == null)
            {
                if (r.lone > 0) for (int i = 0; i < r.MaxCol; i++) r.SpawnColony(true);   // 튜토리얼 판: 크기 그대로
                else r.PlanColonies();
            }
            if (st.boss == null && !r.tutorial && EarlyStage(st.stage)) { r.looseSpawned = true; r.SpawnLoose(EARLY_LOOSE[st.stage - 1]); }   // 초반 낱개 버섯 (판 시작 때 한 번)
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
            foreach (var p in planned) if (Overlap(shape, p, 10)) return false;   // 판 시작 계획 중인 군락 자리
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
            // 후보 목록은 예전 LINQ(Take/Select · Where)와 같은 순서로 재사용 버퍼에 채운다 (군락마다 새 리스트를 만들지 않게)
            var pool = spPool; pool.Clear();
            if (EarlyStage(st.stage) && st.boss == null)
            {
                for (int i = 0; i < EARLY_SPECIES.Length && i < st.stage; i++) pool.Add(SP_BY_NAME[EARLY_SPECIES[i]]);   // 초반: 스테이지 수만큼의 종만
            }
            else foreach (var sp in SPECIES) if (IsUnlockedSp(sp) && (sp.theme == null || sp.theme == st.theme.id)) pool.Add(sp);
            pickWall = wall;
            return U.PickWeighted(pool, speciesWeight ??= SpeciesWeight);
        }

        // 종 가중치 (PickSpecies). 예전 람다와 같은 계산·순서. 대리자는 한 번만 만들어 둔다 (군락을 만들 때마다 클로저가 생기지 않게)
        bool pickWall;
        Func<Species, double> speciesWeight;
        double SpeciesWeight(Species sp)
        {
            var w = st.weather;
            double x = TIERS[sp.t].w;
            if (st.theme.favIds.Contains(sp.id)) x *= THEME_FAV_MUL;
            if (sp.t >= 1) x *= st.rareMul;
            x *= Math.Pow(st.stageMul, sp.t);
            if (pickWall && sp.t >= 2) x *= 3;
            if (w.favIds.Contains(sp.id) || (w.favTier >= 0 && sp.t == w.favTier)) x *= w.favMul;
            return x;
        }

        static float ClusterR(float r) => Mathf.Round(r * 1.9f);

        class Cand { public float x, y, R; public List<Vector2> offs; public bool wall; public string side; public Shape shape; }
        readonly List<Species> spPool = new List<Species>();
        // 바닥 군락 후보는 자리를 찾을 때까지 최대 30번 만들어 본다: 그릇(후보·오프셋·모양)은 하나를 다시 쓰고, 고른 뒤 Own으로 모양만 새로 만든다
        readonly Cand floorCand = new Cand { offs = new List<Vector2>() };
        readonly Shape floorShape = new Shape { circle = true };
        // 군락이 계속 들고 있을 모양을 후보의 재사용 그릇에서 떼어 낸다
        static Cand Own(Cand c)
        {
            var s = c.shape;
            c.shape = new Shape { circle = s.circle, x = s.x, y = s.y, r = s.r, x0 = s.x0, y0 = s.y0, x1 = s.x1, y1 = s.y1 };
            return c;
        }

        Cand FloorCandidate(int n, float r)
        {
            float rc = ClusterR(r), ring = rc + r + 6, rot = U.Rand(0, U.TAU);
            var offs = floorCand.offs; offs.Clear(); offs.Add(Vector2.zero);
            for (int i = 0; i < n; i++)
            {
                float a = rot + i * U.TAU / n + U.Rand(-0.15f, 0.15f), dd = ring + U.Rand(-4, 6);
                offs.Add(new Vector2(Mathf.Cos(a) * dd, Mathf.Sin(a) * dd));
            }
            float RR = ring + r + 6;
            if (FX0 + RR > FX1 - RR || FY0 + RR > NO_SPAWN_Y - RR) return null;
            float x = U.Rand(FX0 + RR, FX1 - RR), y = U.Rand(FY0 + RR, NO_SPAWN_Y - RR);
            floorShape.circle = true; floorShape.x = x; floorShape.y = y; floorShape.r = RR;
            var c = floorCand; c.x = x; c.y = y; c.R = RR; c.wall = false; c.side = null; c.shape = floorShape;
            return c;
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

        // 벽(위·좌·우 가장자리)에 길게 늘어선 군락. 화면 바깥 띠에 버섯이 줄지어 보여서 끈다 (사용자 지시 2026-10-19). 낱개 버섯은 그대로 가장자리에도 나온다
        const bool WALL_COLONIES = false;

        public int lone;                       // 군락을 자리 고정으로 만드는 수 (튜토리얼 판만 1, 그 밖은 0 = 보통 스폰)
        public bool tutorial;                  // 1스테이지 튜토리얼 판: 첫 군락을 수확기 바로 위에 움직이지 않게 둔다 (Tutorial이 길을 밝혀 준다)
        public Colony TutorialColony;
        readonly List<Species> loneSp = new List<Species>();   // 자리별 종 (처음 고른 종을 기억해, 다 수확하면 같은 종으로 다시 만든다)
        int MaxCol => lone > 0 ? lone : st.maxCol;

        bool SpawnColony(bool instant)
        {
            if (colonies.Count >= MaxCol) return true;
            if (lone > 0) return SpawnLone(instant);
            bool wall = U.Chance(0.3f) && WALL_COLONIES;   // 난수 순서는 그대로 두고 벽 군락만 끈다
            var sp = PickSpecies(wall);
            if (sp == null) return false;
            if (sp.wander) wall = false;
            float r = TIERS[sp.t].r * colS;   // 7회차: 판마다 정한 군락 크기 배율 (재생도 같은 크기)
            int n = U.RandI(3, st.colMax);
            for (bool tryWall = wall; ; tryWall = false)
            {
                for (int a = 0; a < COL_TRIES; a++)
                {
                    var cand = tryWall ? WallCandidate(n, r) : FloorCandidate(n, r);
                    if (cand != null && AreaFree(cand.shape)) { CreateColony(sp, r, Own(cand), instant); return true; }
                }
                if (!tryWall)
                {
                    var g = GridCandidate(n, r, COL_GRID_REGEN);   // 무작위로 못 찾으면 빈 자리 격자에서 고른다
                    if (g != null) { CreateColony(sp, r, g, instant); return true; }
                    return false;
                }
            }
        }

        // ===== 판 시작 군락 계획 (밸런스 7회차) =====
        // 최대치만큼 군락이 실제로 놓이게 한다: 종·둘레 수를 먼저 다 뽑고(난수 순서는 예전 SpawnColony와 같다: 벽 → 종 → 둘레 수),
        // 등급이 섞인 계획으로 시작 크기 s0를 푼 뒤 자리만 계획한다(무작위 30번 → 격자 보충). 하나라도 못 놓으면 크기를 줄여 처음부터.
        // 크기 하한 ColSMin(zoom)은 한 번 들르고 COL_S_ABS에서 멈춘다(그때는 놓인 만큼만). 정한 크기는 판 안의 재생에도 쓴다
        public float colS = 1;
        readonly List<Shape> planned = new List<Shape>();
        readonly struct PlanSlot { public readonly Species sp; public readonly int n; public PlanSlot(Species sp, int n) { this.sp = sp; this.n = n; } }

        void PlanColonies()
        {
            var slots = new List<PlanSlot>();
            for (int i = 0; i < MaxCol; i++)
            {
                bool wall = U.Chance(0.3f) && WALL_COLONIES;   // 예전 SpawnColony와 같은 난수 순서 (벽 군락은 꺼져 있다)
                var sp = PickSpecies(wall);
                if (sp == null) continue;
                slots.Add(new PlanSlot(sp, U.RandI(3, st.colMax)));
            }
            if (slots.Count == 0) return;
            float sMin = ColSMin(ZOOM), s = StartScale(slots);
            var cands = new List<Cand>(slots.Count);
            while (true)
            {
                planned.Clear(); cands.Clear();
                foreach (var sl in slots)
                {
                    float r = TIERS[sl.sp.t].r * s;
                    Cand c = null;
                    for (int a = 0; a < COL_TRIES && c == null; a++)
                    {
                        var f = FloorCandidate(sl.n, r);
                        if (f != null && AreaFree(f.shape)) c = Copy(f);
                    }
                    c ??= GridCandidate(sl.n, r, COL_GRID);
                    if (c == null) break;
                    cands.Add(c); planned.Add(c.shape);
                }
                if (cands.Count == slots.Count || s <= COL_S_ABS) break;
                float next = s * COL_SHRINK;
                if (s > sMin && next < sMin) next = sMin;   // 하한은 건너뛰지 않고 한 번 들른다
                s = Mathf.Max(next, COL_S_ABS);
            }
            planned.Clear();
            colS = s;
            for (int i = 0; i < cands.Count; i++) CreateColony(slots[i].sp, TIERS[slots[i].sp.t].r * s, cands[i], true);
        }

        // 계획한 종으로 시작 크기를 푼다: Σ π(3.9·r_i·s + 17)² = COL_PACK · 빈 넓이 (검증_7 M1)
        float StartScale(List<PlanSlot> slots)
        {
            double free = (FX1 - FX0) * (NO_SPAWN_Y - FY0) - Math.PI * 140 * 140 / 2;
            foreach (var d in devices) foreach (var sh in d.Shapes()) free -= sh.circle ? Math.PI * sh.r * sh.r : (sh.x1 - sh.x0) * (sh.y1 - sh.y0);
            double a = 0, b = 0, c = 289.0 * slots.Count - COL_PACK * Math.Max(0, free) / Math.PI;
            foreach (var sl in slots) { double k = 3.9 * TIERS[sl.sp.t].r; a += k * k; b += 34 * k; }
            double disc = b * b - 4 * a * c;
            double s0 = disc < 0 ? COL_S_ABS : (-b + Math.Sqrt(disc)) / (2 * a);
            return Mathf.Clamp((float)s0, COL_S_ABS, 1);
        }

        // 후보를 계획 목록에 남길 수 있게 떼어 낸다 (FloorCandidate는 그릇 하나를 다시 쓴다)
        static Cand Copy(Cand c)
        {
            var o = Own(c);
            return new Cand { x = o.x, y = o.y, R = o.R, offs = new List<Vector2>(o.offs), wall = false, side = null, shape = o.shape };
        }

        // 빈 자리 격자에서 하나: 군락 원(RR)이 들어가는 격자점을 모두 모아 U.RandI로 하나 고른 뒤 둘레를 뽑는다 (위치 → 회전·둘레 순, 검증_7 M2)
        readonly List<Vector2> gridPts = new List<Vector2>();
        readonly Shape gridProbe = new Shape { circle = true };
        Cand GridCandidate(int n, float r, float step)
        {
            float RR = ClusterR(r) + r + 6 + r + 6;
            if (FX0 + RR > FX1 - RR || FY0 + RR > NO_SPAWN_Y - RR) return null;
            gridPts.Clear();
            gridProbe.r = RR;
            for (float y = FY0 + RR; y <= NO_SPAWN_Y - RR; y += step)
                for (float x = FX0 + RR; x <= FX1 - RR; x += step)
                {
                    gridProbe.x = x; gridProbe.y = y;
                    if (AreaFree(gridProbe)) gridPts.Add(new Vector2(x, y));
                }
            if (gridPts.Count == 0) return null;
            var p = gridPts[Mathf.Min(gridPts.Count - 1, U.RandI(0, gridPts.Count - 1))];
            float rc = ClusterR(r), ring = rc + r + 6, rot = U.Rand(0, U.TAU);
            var offs = new List<Vector2> { Vector2.zero };
            for (int i = 0; i < n; i++)
            {
                float a = rot + i * U.TAU / n + U.Rand(-0.15f, 0.15f), dd = ring + U.Rand(-4, 6);
                offs.Add(new Vector2(Mathf.Cos(a) * dd, Mathf.Sin(a) * dd));
            }
            return new Cand { x = p.x, y = p.y, R = RR, offs = offs, wall = false, side = null, shape = Shape.C(p.x, p.y, RR) };
        }

        // 초반 스테이지 군락: 빈 자리 하나를 그 자리의 종으로 채운다. 첫 판 튜토리얼의 첫 군락은 발사 지점 바로 위에
        bool SpawnLone(bool instant)
        {
            int slot = -1;
            for (int i = 0; i < lone && slot < 0; i++)
            {
                bool used = false;   // colonies.Any(c => c.slot == i)와 같다 (반복마다 클로저를 만들지 않게)
                for (int k = 0; k < colonies.Count; k++) if (colonies[k].slot == i) { used = true; break; }
                if (!used) slot = i;
            }
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
                CreateColony(sp, r, Own(cand), instant, first);
                var col = colonies[colonies.Count - 1];
                col.slot = slot;
                if (first) TutorialColony = col;
                return true;
            }
            return false;
        }

        // 낱개 버섯 (군락에 속하지 않는다. 수확해도 다시 생기지 않는다): 튜토리얼 판은 첫 발사 때 TUTORIAL_LOOSE개, 초반 판은 시작 때 EARLY_LOOSE
        public const int TUTORIAL_LOOSE = 12;   // 첫 판 수입을 늘려 첫 강화까지 이어지게 (8회차 2026-10-23, 이전 8)
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

        readonly Shape wanderShape = new Shape { circle = true };
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
                var shape = wanderShape; shape.circle = true; shape.x = nx; shape.y = ny; shape.r = c.R;   // 시험용 그릇 (매 프레임 새 모양을 만들지 않게)
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
                c.x = nx; c.y = ny;
                if (c.shape != null && c.shape.circle && c.shape != wanderShape) { c.shape.x = nx; c.shape.y = ny; c.shape.r = c.R; }   // 군락의 모양을 제자리에서 옮긴다
                else c.shape = Shape.C(nx, ny, c.R);
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
