using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Harvest round simulation, ported from the prototype's "수확 라운드 시뮬레이션" section.
    // Pure state + rules; RoundView draws it and RoundController drives input and timing.
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

        // HUD slots that harvested mushrooms fly to (screen coords)
        public static readonly Dictionary<string, Vector2> HUD_SLOTS = new Dictionary<string, Vector2>
        {
            ["ed"] = new Vector2(66, 186), ["md"] = new Vector2(66, 244), ["ps"] = new Vector2(66, 302),
        };

        // ===== 상태 =====
        public RoundStats st;
        public string phase = "aim";   // aim · run · end
        public float t, runT, timeLeft, timeMax, tsporeUsed, sipUsed;
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
        public float acornT, lightningT, hitstop, hitstopCd, shake, flash;
        public bool festOn, paused;
        public float barX, barLen, barFx;
        public Vector2? aimStart, aimCur;
        public int deviceSpawned, colId = 1;
        public Dictionary<string, float> skillLabelT = new Dictionary<string, float>();
        public Dictionary<string, float> skillFlash = new Dictionary<string, float>();
        public float flashCd;
        public int flamePal; public float flameInt = 0.5f;
        public int chain; public float chainT, chainBump; public int fever; public float feverK;
        public FeverMsg feverMsg;
        public List<Ember> embers = new List<Ember>();
        public float codexShake, scoreBump, saleBump, endT;
        public Dictionary<string, float> slotBump = new Dictionary<string, float> { ["ed"] = 0, ["md"] = 0, ["ps"] = 0 };
        public Dictionary<string, double> bag = new Dictionary<string, double>();
        public double value, bonusGold, coinGold;
        public SpecialMush special;
        public float specialAt;
        public string specialGot;
        public float? meteorT;
        public string themeId;

        // ===== 공간 격자 =====
        const int CELL = 100;
        static readonly int GW = Mathf.CeilToInt(W / (float)CELL), GH = Mathf.CeilToInt(H / (float)CELL);
        readonly List<Shroom>[] grid = Enumerable.Range(0, GW * GH).Select(_ => new List<Shroom>()).ToArray();
        int qsCounter;

        public static RoundSim Start(string wId, string themeId)
        {
            var st = ComputeStats(wId, themeId);
            SetWorld((float)st.zoom);
            var r = new RoundSim { st = st, themeId = st.theme.id };
            R = r;
            r.timeLeft = r.timeMax = (float)st.duration;
            r.giantState = U.Chance((float)st.giantP) ? "pending" : "none";
            r.lightningT = U.Rand(2, 4);
            r.barX = WW / 2; r.barLen = (float)st.barLen;
            r.specialAt = U.Chance((float)st.specialP) ? U.Rand(3, Mathf.Max(4, (float)(st.duration - st.specialLife - 1))) : -1;
            for (int i = 0; i < st.devices; i++) r.PlaceDevice(U.Pick(DEVICE_TYPES), null);
            for (int i = 0; i < st.maxCol; i++) r.SpawnColony(true);
            for (int i = 0; i < st.permBalls; i++) r.balls.Add(r.MakeBall(LAUNCH.x, LAUNCH.y, true));
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

        bool AreaFree(List<Shape> shapes, Colony skipCol = null)
        {
            var ex = new List<(Shape s, float m)> { (Shape.C(LAUNCH.x, LAUNCH.y, 140), 0), (Shape.Rect(0, NO_SPAWN_Y, W, H), 0) };
            foreach (var c in colonies) if (c != skipCol) ex.Add((c.shape, 10));
            foreach (var d in devices) foreach (var s in d.Shapes()) ex.Add((s, 10));
            if (giant != null) ex.Add((Shape.C(giant.x, giant.y, giant.r), 10));
            if (giantWarn != null) ex.Add((Shape.C(giantWarn.x, giantWarn.y, 90), 10));
            foreach (var s in shapes) foreach (var e in ex) if (Overlap(s, e.s, e.m)) return false;
            return true;
        }

        // ===== 군락지 =====
        Species PickSpecies(bool wall)
        {
            var w = st.weather;
            var pool = SPECIES.Where(sp => IsUnlockedSp(sp) && (sp.theme == null || sp.theme == st.theme.id)).ToList();
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

        bool SpawnColony(bool instant)
        {
            if (colonies.Count >= st.maxCol) return true;
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
                    if (cand != null && AreaFree(new List<Shape> { cand.shape })) { CreateColony(sp, r, cand, instant); return true; }
                }
                if (!tryWall) return false;
            }
        }

        void CreateColony(Species sp, float r, Cand cand, bool instant)
        {
            var col = new Colony
            {
                id = colId++, sp = sp, x = cand.x, y = cand.y, R = cand.R, shape = cand.shape, wall = cand.wall, side = cand.side,
                wander = !cand.wall && (sp.wander || U.Chance((float)st.wanderP)), alive = cand.offs.Count, turn = U.Rand(3, 6),
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
                col.members.Add(m); shrooms.Add(m);
            }
            colonies.Add(col);
            if (!instant)
                for (int i = 0; i < 8; i++)
                    AddPart(col.x + U.Rand(-col.R, col.R) * 0.6f, col.y + U.Rand(-col.R, col.R) * 0.6f, U.Rand(-40, 40), U.Rand(-60, -10), 0.7f, U.Hex(CATS[sp.c].light), 4);
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
                case "stump": { var p = Pos(50); return new Device { type = type, x = p.x, y = p.y, r = 40 }; }
                case "moss":
                {
                    var p = Pos(140); bool hz = U.Chance(0.5f); float w = hz ? 260 : 50, hh = hz ? 50 : 260;
                    return new Device { type = type, x0 = p.x - w / 2, y0 = p.y - hh / 2, x1 = p.x + w / 2, y1 = p.y + hh / 2, hz = hz };
                }
                case "mole":
                {
                    var a = Pos(40);
                    for (int k = 0; k < 30; k++)
                    {
                        var b = new Vector2(U.Rand(FX0 + 40, FX1 - 40), U.Rand(FY0 + 40, NO_SPAWN_Y - 40));
                        if (U.D2(a.x, a.y, b.x, b.y) >= 600 * 600) return new Device { type = type, a = a, b = b, r = 30 };
                    }
                    return null;
                }
                case "stream":
                    if (U.Chance(0.5f))
                    {
                        float y = near.HasValue ? U.Clamp(near.Value.y + U.Rand(-160, 160), FY0 + 70, NO_SPAWN_Y - 70) : U.Rand(FY0 + 70, NO_SPAWN_Y - 70);
                        return new Device { type = type, x0 = FX0, x1 = FX1, y0 = y - 35, y1 = y + 35, fx = U.Chance(0.5f) ? 1 : -1, fy = 0 };
                    }
                    else
                    {
                        float x = near.HasValue ? U.Clamp(near.Value.x + U.Rand(-160, 160), FX0 + 70, FX1 - 70) : U.Rand(FX0 + 70, FX1 - 70);
                        return new Device { type = type, x0 = x - 35, x1 = x + 35, y0 = FY0, y1 = NO_SPAWN_Y, fx = 0, fy = U.Chance(0.5f) ? 1 : -1 };
                    }
                case "acorn":
                {
                    var p = Pos(110); bool hz = U.Chance(0.6f);
                    var pts = new[] { -1, 0, 1 }.Select(k => new Vector2(p.x + (hz ? k * 70 : 0), p.y + (hz ? 0 : k * 70))).ToArray();
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

        List<Shroom> QueryCircle(float x, float y, float rad)
        {
            qsCounter++;
            var o = new List<Shroom>();
            int x0 = Mathf.Clamp(Mathf.FloorToInt((x - rad) / CELL), 0, GW - 1), x1 = Mathf.Clamp(Mathf.FloorToInt((x + rad) / CELL), 0, GW - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((y - rad) / CELL), 0, GH - 1), y1 = Mathf.Clamp(Mathf.FloorToInt((y + rad) / CELL), 0, GH - 1);
            for (int gy = y0; gy <= y1; gy++)
                for (int gx = x0; gx <= x1; gx++)
                    foreach (var m in grid[gy * GW + gx]) { if (m.qs == qsCounter || m.dead) continue; m.qs = qsCounter; o.Add(m); }
            return o;
        }
    }
}
