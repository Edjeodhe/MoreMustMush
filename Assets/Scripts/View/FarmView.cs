using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Mushroom farm world: one ranch with the field inside. Caught critters wander and ask for things, walk to queued
    // field plots, and build buildings (a builder is busy until the building is done, real time). Buildings sit on a grid
    // with fixed footprints; placing one shows a ghost that snaps to the grid until it is confirmed.
    // Fixed objects (background, 64 plot slots, ghost) are placed in the hierarchy; critters, buildings, particles and
    // floating texts are clones of hidden template slots.
    public class FarmView : MonoBehaviour
    {
        public static FarmView I;

        [Header("Scene")]
        public GrandpaRig grandpa;
        public SpriteRenderer fieldFrame;                 // sliced wood frame around the open plots
        public SpriteRenderer[] nextEdges = new SpriteRenderer[4];   // outline of the next field size
        public FieldTile[] tiles = new FieldTile[64];     // 8×8, index r * 8 + c
        public GameObject tileHint; public TMP_Text tileHintText; public SpriteRenderer tileHintBack;

        [Header("Buildings")]
        public Transform buildLayer;
        public BuildingView buildingTemplate;             // hidden slot, one clone per building
        public GameObject ghost;                          // placement preview
        public SpriteRenderer ghostSprite, ghostFoot;

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
            public string state = "idle", anim, say, tool;   // state: idle · walk · work (field) · build
            public Job job;
        }
        public class Job { public int r, c; public string key, mode, crop; }
        class Part { public string k; public float x, y, vx, vy, t, life; }
        class Float { public float x, y, t; public string text; public bool gem; public Color col; }

        public readonly List<Critter> crits = new List<Critter>();
        public readonly List<Job> jobs = new List<Job>();
        public readonly Dictionary<string, string> busy = new Dictionary<string, string>();
        readonly List<Part> parts = new List<Part>();
        readonly List<Float> floats = new List<Float>();
        Critter hover; (int r, int c, string key)? hoverTile;
        public bool dirty;                                // HUD needs a redraw (FarmScreen polls it)

        // placement (건물 배치)
        public string PlaceId { get; private set; }
        public int placeX, placeY;
        public bool Placing => PlaceId != null;
        public bool PlaceOk => Placing && CanPlace(BUILDING[PlaceId], placeX, placeY);

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

        // Leaving the farm: queued field work finishes at once (as the critters would have done). Buildings keep their real-time timers.
        public void Flush()
        {
            if (G?.farm == null) return;
            CancelPlace();
            foreach (var w in crits) if (w.job != null) { FinishJob(w.job, w.id, true); w.job = null; w.state = "idle"; }
            foreach (var j in jobs) FinishJob(j, null, true);
            jobs.Clear();
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
            if (anim != null) { p.anim = anim; p.animT = 0; if (p.job == null && p.state != "build") { p.state = "idle"; p.t = Mathf.Max(p.t, FARM_ANIMS[anim] + 0.3f); } }
        }

        void ClickCritter(Critter p)
        {
            bool free = p.job == null && p.state != "build";
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
            if (p.job != null) { Say(p, U.Pick(FARM_LINES["field"]), null); return; }
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
                Say(p, r.full ? "호감도 가득! 진화할 수 있어!" : U.Pick(FARM_LINES["thanks"]), p.job == null && p.state != "build" ? "jump" : null);
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
            Say(p, "힘이 솟아나! 더 열심히 할게!", p.job == null && p.state != "build" ? "jump" : null);
            AddFloat(p.x, p.y - 200, $"★{CStarOf(id)}", false, U.Hex("#ffe36e"));
            Stars(p.x, p.y - 50);
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

                // 건설: 현장으로 가서 다 지을 때까지 망치질 (그동안 밭일은 못 한다)
                var site = BuilderSpot(p.id);
                if (site != null)
                {
                    if (p.job != null) { jobs.Add(p.job); p.job = null; }
                    if (p.state != "build")
                    {
                        p.state = "walk"; p.tool = null;
                        if (WalkTo(p, site.Value.x, site.Value.y, 240, dt)) { p.state = "build"; p.tool = "hammer"; p.face = site.Value.x < BuildCenterX(p.id) ? 1 : -1; }
                    }
                    continue;
                }
                if (p.state == "build") { p.state = "idle"; p.tool = null; p.t = U.Rand(0.5f, 1.5f); }

                // 밭일
                if (p.job != null)
                {
                    float ws = WorkSpeed(p.id);
                    if (p.state == "walk") { if (WalkTo(p, p.tx, p.ty, 380 * ws, dt)) { p.state = "work"; p.t = FIELD.work / ws; } }
                    else if (p.state == "work" && p.t <= 0)
                    {
                        var j = p.job; p.job = null; p.tool = null; p.state = "idle"; p.t = U.Rand(0.2f, 0.6f);
                        FinishJob(j, p.id, false);
                        if (j.mode == "harvest") Snd.Buy(); else Snd.Tone(j.mode == "till" ? 180 : 520, 0.06f, Snd.Wave.Triangle, 0.4f, 1.2f, "farm", 0.04f);
                    }
                    continue;
                }
                if (anim) continue;
                if (jobs.Count > 0)
                {
                    int bi = 0; float bd = 1e12f;
                    for (int i = 0; i < jobs.Count; i++) { var tt = TileXY(jobs[i].r, jobs[i].c); float d = U.D2(p.x, p.y, tt.x, tt.y); if (d < bd) { bd = d; bi = i; } }
                    var jb = jobs[bi]; jobs.RemoveAt(bi);
                    var t = TileXY(jb.r, jb.c); float T = TileSize();
                    p.job = jb; p.tx = t.x + T / 2; p.ty = t.y + T * 0.78f; p.state = "walk"; p.anim = null;
                    p.tool = jb.mode == "till" ? "till" : jb.mode == "plant" ? "seed" : "basket";
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

        // ===== field =====
        public static float TileSize() => Mathf.Min(112, FIELD.span / G.farm.size);
        public static Vector2 TileXY(int r, int c)
        {
            int n = G.farm.size, o = FieldOff(n); float T = TileSize();
            return new Vector2(FIELD.cx + (c - o - n / 2f) * T, FIELD.cy + (r - o - n / 2f) * T);
        }

        static (int r, int c, string key)? FieldTileAt(float x, float y)
        {
            int n = G.farm.size, o = FieldOff(n); float T = TileSize();
            int c = o + Mathf.FloorToInt((x - (FIELD.cx - n / 2f * T)) / T), r = o + Mathf.FloorToInt((y - (FIELD.cy - n / 2f * T)) / T);
            return FieldOpen(r, c) ? (r, c, r + "," + c) : ((int, int, string)?)null;
        }

        public int Workers => crits.Count(p => BuilderSpot(p.id) == null);

        // 칸에 할 일 맡기기. mode 없으면 칸 상태에 맞게. 심을 때 재료는 맡기는 순간 쓴다. 맡겼으면 true
        public bool FieldQueue(int r, int c, string mode, bool quiet)
        {
            string key = r + "," + c; var p = PlotAt(key);
            if (busy.ContainsKey(key)) return false;
            if (Workers == 0)
            {
                if (!quiet) { Snd.Err(); GameFlow.I.ShowToast(crits.Count == 0 ? "특수 버섯(꼬마)을 잡으면 밭일을 도와줘요" : "꼬마들이 모두 건물을 짓고 있어요"); }
                return false;
            }
            string m = mode ?? (p == null ? "till" : p.s == "till" ? "plant" : TileRipe(p) ? "harvest" : null);
            if (m == null) return false;
            if (m == "till" && p != null) return false;
            if (m == "harvest" && !TileRipe(p)) return false;
            string crop = null;
            if (m == "plant")
            {
                if (p == null || p.s != "till") return false;
                crop = G.farm.crop;
                var n = CropNeed(crop);
                if (!CanPlant(crop))
                {
                    if (!quiet) { Snd.Err(); GameFlow.I.ShowToast(G.spore < n["spore"] ? $"버섯 포자가 {n["spore"]}개 필요해요 (포자 상점)" : $"{CATS[crop].name} 버섯이 {U.Fmt(n[crop])}개 필요해요"); }
                    return false;
                }
                G.spore -= n["spore"]; ConsumeCat(crop, n[crop]);
            }
            jobs.Add(new Job { r = r, c = c, key = key, mode = m, crop = crop });
            busy[key] = m;
            return true;
        }

        // 분류 재료는 판매가가 싼 버섯부터 쓴다
        static void ConsumeCat(string cat, double n)
        {
            var list = SPECIES.Where(sp => sp.c == cat && InvCount(sp.id) > 0).OrderBy(sp => sp.t).ThenByDescending(sp => InvCount(sp.id)).ToList();
            foreach (var sp in list)
            {
                if (n <= 0) break;
                double k = System.Math.Min(n, InvCount(sp.id));
                G.inv[sp.id] -= k; n -= k;
            }
        }

        // 일이 끝났을 때 (worker = 일한 꼬마, quiet = 농장을 떠나며 한꺼번에 끝낼 때)
        void FinishJob(Job j, string worker, bool quiet)
        {
            var P = G.farm.plots; var p = PlotAt(j.key);
            busy.Remove(j.key);
            var t = TileXY(j.r, j.c); float T = TileSize(), mx = t.x + T / 2, my = t.y + T / 2;
            if (j.mode == "till" && p == null)
            {
                P[j.key] = new SaveData.Plot { s = "till" };
                if (!quiet) for (int i = 0; i < 6; i++) parts.Add(new Part { k = "dirt", x = mx, y = my, vx = U.Rand(-120, 120), vy = U.Rand(-260, -100), life = 0.5f });
            }
            else if (j.mode == "plant" && p != null && p.s == "till")
            {
                double now = Now();
                P[j.key] = new SaveData.Plot { s = "grow", crop = j.crop, t0 = now, at = now + CropTime(j.crop) * 1000 };
                if (!quiet) for (int i = 0; i < 6; i++) parts.Add(new Part { k = "spore", x = mx, y = my - 10, vx = U.Rand(-60, 60), vy = U.Rand(-160, -60), life = 0.6f });
            }
            else if (j.mode == "harvest" && TileRipe(p))
            {
                var (gem, sp) = HarvestYield(p.crop, worker);
                G.gem += gem; G.spore += sp;
                P.Remove(j.key);   // 수확하면 다시 안 간 땅
                if (!quiet) { AddFloat(mx, my - 50, $"+{gem}{(sp > 0 ? $"  포자 +{sp}" : "")}", true, U.Hex("#a8f5d4")); GemBurst(mx, my - 20, 6); }
            }
            else if (j.mode == "plant") G.spore += CROPS[j.crop].spore;   // 심을 수 없게 됐으면 포자는 돌려준다
            if (!quiet) { SaveGame(); dirty = true; }
        }

        // 일괄: 모두 갈기 · 모두 심기 · 모두 수확 → 맡긴 칸 수
        public int FieldAll(string mode)
        {
            int n = 0;
            foreach (var (r, c, _) in FieldKeys().ToList()) if (FieldQueue(r, c, mode, true)) n++;
            if (n > 0) dirty = true;
            return n;
        }

        public int Working => jobs.Count + crits.Count(w => w.job != null);

        void FieldClick(float x, float y)
        {
            var t = FieldTileAt(x, y); if (t == null) return;
            var (r, c, key) = t.Value; var p = PlotAt(key);
            if (p != null && p.s == "grow" && !TileRipe(p) && !busy.ContainsKey(key))
            {
                var tl = TileXY(r, c); float T = TileSize();
                AddFloat(tl.x + T / 2, tl.y + T / 2 - 40, Mmss((long)System.Math.Ceiling((p.at - Now()) / 1000)), false, U.Hex("#fff6e0"));
                Snd.Ui(); return;
            }
            if (FieldQueue(r, c, null, false)) { Snd.Ui(); dirty = true; }
        }

        // ===== buildings =====
        public bool BeginPlace(string id)
        {
            var spot = FirstSpot(BUILDING[id]);
            if (spot == null) return false;
            PlaceId = id; placeX = spot.Value.x; placeY = spot.Value.y;
            dirty = true;
            return true;
        }

        public void CancelPlace() { if (PlaceId == null) return; PlaceId = null; dirty = true; }

        // 확인: 그 자리에 건설 시작 → 맡은 꼬마가 들어 있는 건물 (실패하면 null)
        public SaveData.Bld ConfirmPlace()
        {
            if (!PlaceOk) return null;
            var idle = crits.Where(p => p.job == null && BuilderSpot(p.id) == null).Select(p => p.id).ToList();
            var bld = BuildStart(PlaceId, placeX, placeY, idle);
            if (bld == null) return null;
            PlaceId = null; dirty = true;
            var c = crits.FirstOrDefault(p => p.id == bld.critter);
            if (c != null) { c.anim = null; Say(c, "내가 지을게! 뚝딱뚝딱!", null); }
            return bld;
        }

        // 누른 위치를 건물 가운데로 삼아 격자에 맞춘다
        void MoveGhost(float x, float y)
        {
            var b = BUILDING[PlaceId];
            int gx = Mathf.RoundToInt((x - GRID.x0) / GRID.cell - b.w / 2f), gy = Mathf.RoundToInt((y - GRID.y0) / GRID.cell - b.h / 2f);
            gx = Mathf.Clamp(gx, 0, GRID.cols - b.w); gy = Mathf.Clamp(gy, 0, GRID.rows - b.h);
            if (gx != placeX || gy != placeY) { placeX = gx; placeY = gy; dirty = true; }
        }

        // 배치 중인 건물의 화면 영역 (HUD가 확인·취소 버튼을 옆에 붙인다)
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
            DrawField(now);
            DrawBuildings();
            for (int i = 0; i < crits.Count; i++)
            {
                var p = crits[i];
                string hat = BuilderSpot(p.id) != null ? "hardhat" : p.job != null ? "straw" : null;
                critPool.Get(i).Draw(p, now, hat, hover == p, FarmReady(p.id) ? FarmKindOf(p.id) : null);
            }
            critPool.Trim(crits.Count);
            DrawFx();
        }

        void DrawField(float now)
        {
            int n = G.farm.size; float T = TileSize();
            double t0 = Now();
            fieldFrame.transform.localPosition = Art.P(FIELD.cx, FIELD.cy);
            Art.SlicedPx(fieldFrame, n * T + 28, n * T + 28);
            bool more = n < FIELD.max;
            float S = more ? (n + 1) * Mathf.Min(112, FIELD.span / (n + 1)) + 28 : 0;
            for (int i = 0; i < 4; i++)
            {
                var e = nextEdges[i];
                e.enabled = more;
                if (!more) continue;
                bool h = i < 2;
                float ex = h ? FIELD.cx : FIELD.cx + (i == 2 ? -S / 2 : S / 2), ey = h ? FIELD.cy + (i == 0 ? -S / 2 : S / 2) : FIELD.cy;
                e.transform.localPosition = Art.P(ex, ey);
                e.transform.localScale = new Vector3((h ? S + 3 : 3) / Art.PPU, (h ? 3 : S + 3) / Art.PPU, 1);
            }
            for (int r = 0; r < FIELD.max; r++)
                for (int c = 0; c < FIELD.max; c++)
                {
                    var tile = tiles[r * FIELD.max + c];
                    bool open = FieldOpen(r, c);
                    UIUtil.Show(tile, open);
                    if (!open) continue;
                    string key = r + "," + c;
                    var p = TileXY(r, c);
                    busy.TryGetValue(key, out var bz);
                    bool hov = hoverTile.HasValue && hoverTile.Value.key == key;
                    tile.Draw(p.x + T / 2, p.y + T / 2, T, PlotAt(key), t0, now, bz, hov);
                }

            // 마우스를 올린 칸 안내
            UIUtil.Show(tileHint, hoverTile.HasValue);
            if (hoverTile.HasValue)
            {
                var (r, c, key) = hoverTile.Value; var q = PlotAt(key); var p = TileXY(r, c);
                string label = busy.ContainsKey(key) ? "꼬마가 일하러 가는 중…"
                    : q == null ? "갈기"
                    : q.s == "till" ? $"{UIUtil.Ic(G.farm.crop)} {CROPS[G.farm.crop].n} 심기"
                    : TileRipe(q) ? $"{UIUtil.Ic("crop_" + q.crop)} 수확 (균사석 +{CROPS[q.crop].gem})"
                    : $"{CROPS[q.crop].n} · {Mmss((long)System.Math.Ceiling((q.at - t0) / 1000))} 남음";
                UIUtil.SetText(tileHintText, label);
                tileHintText.ForceMeshUpdate();
                tileHint.transform.localPosition = Art.P(p.x + T / 2, p.y - 18);
                Art.SlicedPx(tileHintBack, tileHintText.preferredWidth * Art.PPU + 24, 32);
            }
        }

        void DrawBuildings()
        {
            var list = G.farm.blds;
            for (int i = 0; i < list.Count; i++) buildPool.Get(i).Draw(list[i]);
            buildPool.Trim(list.Count);

            UIUtil.Show(ghost, Placing);
            if (!Placing) return;
            var b = BUILDING[PlaceId]; var r = BuildRect(b, placeX, placeY);
            bool ok = PlaceOk;
            ghost.transform.localPosition = Art.P(r.center.x, r.yMax);
            ghostFoot.transform.localPosition = new Vector3(0, r.height / 2 / Art.PPU, 0);
            ghostFoot.transform.localScale = new Vector3(r.width / Art.PPU, r.height / Art.PPU, 1);
            ghostFoot.color = ok ? new Color(0.45f, 1, 0.5f, 0.38f) : new Color(1, 0.35f, 0.3f, 0.42f);
            BuildingView.Fit(ghostSprite, SpriteDB.Get("Farm/Props/" + b.id), b.px);
            ghostSprite.color = ok ? new Color(1, 1, 1, 0.7f) : new Color(1, 0.6f, 0.6f, 0.6f);
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
                    case "dirt": sp = "FX/circle"; px = 10; c = new Color(0.48f, 0.31f, 0.18f, c.a); break;
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
            hover = null; hoverTile = null;
            if (p == null || GameFlow.I.ModalOpen) return;
            bool isMouse = p is Mouse;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(isMouse ? -1 : 0);
            var s = ToStage(p.position.ReadValue());
            if (Placing)
            {
                // 배치 중: 누르거나 끌면 건물 잔상이 따라온다 (확인·취소는 HUD 버튼)
                if (!overUI && p.press.isPressed) MoveGhost(s.x, s.y);
                return;
            }
            if (overUI) return;
            if (isMouse) { hover = CritterAt(s.x, s.y); if (hover == null) hoverTile = FieldTileAt(s.x, s.y); }
            if (!p.press.wasPressedThisFrame) return;
            var c = CritterAt(s.x, s.y);
            if (c != null) { ClickCritter(c); return; }
            if (FieldTileAt(s.x, s.y) != null) { FieldClick(s.x, s.y); return; }
            var b = G.farm.blds.LastOrDefault(x => BuildRect(BUILDING[x.id], x.gx, x.gy).Contains(new Vector2(s.x, s.y)));
            if (b != null)
            {
                var r = BuildRect(BUILDING[b.id], b.gx, b.gy);
                AddFloat(r.center.x, r.yMin - 20, b.done ? string.Format(BUILDING[b.id].statFmt, U.Pct(BUILDING[b.id].val)) : $"{UIUtil.Ic("hammer")} {BuildLeftText(b)} 남음", false, U.Hex("#fff6e0"));
                Snd.Ui();
            }
        }
    }
}
