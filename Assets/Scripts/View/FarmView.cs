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
    // Mushroom farm world (prototype FARM_RT + drawFarm): three scenes side by side (field | ranch | kitchen)
    // that slide past the camera. The caught critters live in all three: wandering ranch pets with requests,
    // field workers that walk to queued plots, kitchen cooks that carry orders through counter → stove → table.
    // Every fixed object (backgrounds, decorations, 64 plot slots, dish slots) is placed in the hierarchy;
    // critters, particles and floating texts are clones of hidden template slots.
    public class FarmView : MonoBehaviour
    {
        public static FarmView I;
        static readonly Dictionary<string, int> SCENE_X = new Dictionary<string, int> { ["field"] = -1, ["pets"] = 0, ["kitchen"] = 1 };

        [Header("Scenes")]
        public Transform content;                 // zoom pivot for the pan
        public Transform field, pets, kitchen;    // scene roots, one screen apart
        public SpriteRenderer panShade;           // darkens the edges while panning

        [Header("Ranch")]
        public GrandpaRig petsGrandpa;
        public GameObject[] decor = new GameObject[8];   // DECOR order

        [Header("Field")]
        public GrandpaRig fieldGrandpa;
        public FieldTile[] tiles = new FieldTile[64];     // 8×8, index r * 8 + c
        public SpriteRenderer fieldFrame;                 // sliced wood frame around the open plots
        public SpriteRenderer[] nextEdges = new SpriteRenderer[4];   // outline of the next size (top, bottom, left, right)
        public GameObject tileHint; public TMP_Text tileHintText; public SpriteRenderer tileHintBack;

        [Header("Kitchen")]
        public SpriteRenderer[] dishes = new SpriteRenderer[10];
        public SpriteRenderer[] steam = new SpriteRenderer[6];
        public SpriteRenderer stoveGlow;

        [Header("Templates (hidden slots)")]
        public FarmCritter critterTemplate;
        public SpriteRenderer partTemplate;
        public FarmFloat floatTemplate;
        public Transform fx;                      // particles and floating texts (screen coordinates, drawn when not panning)

        // ===== screen state (not saved) =====
        public class Critter
        {
            public string id; public Special kind;
            public float x, y, tx, ty, t, hop, animT, sayT, slot;
            public int face = 1, step;
            public string state = "idle", anim, say, tool;
            public Job job; public string order;
        }
        public class Job { public int r, c; public string key, mode, crop; }
        class Part { public string k; public float x, y, vx, vy, t, life; }
        class Float { public float x, y, t; public string text; public bool gem; public Color col; }
        class Cam { public float from, to, t, dur; }

        public string View { get; private set; } = "pets";
        public bool Moving => cam != null;
        public readonly List<Critter> petList = new List<Critter>(), workers = new List<Critter>(), cooks = new List<Critter>();
        public readonly List<Job> jobs = new List<Job>();
        public readonly Dictionary<string, string> busy = new Dictionary<string, string>();
        public readonly List<string> orders = new List<string>();
        public readonly HashSet<string> cooking = new HashSet<string>();
        readonly List<Part> parts = new List<Part>();
        readonly List<Float> floats = new List<Float>();
        Cam cam;
        Critter hover; (int r, int c, string key)? hoverTile;
        public bool dirty;                        // HUD needs a redraw (FarmScreen polls it)

        Pool<FarmCritter> petPool, workerPool, cookPool;
        Pool<SpriteRenderer> partPool;
        Pool<FarmFloat> floatPool;

        void Awake()
        {
            I = this;
            petPool = new Pool<FarmCritter>(critterTemplate, pets);
            workerPool = new Pool<FarmCritter>(critterTemplate, field);
            cookPool = new Pool<FarmCritter>(critterTemplate, kitchen);
            partPool = new Pool<SpriteRenderer>(partTemplate, fx);
            floatPool = new Pool<FarmFloat>(floatTemplate, fx);
        }

        // ===== enter · leave · move =====
        public void Enter(string view)
        {
            FarmEnsure(); SaveGame();
            if (view != null) View = view;
            cam = null;
            Sync(true);
            dirty = true;
        }

        // Leaving the farm: queued field work and orders finish at once (as the critters would have done)
        public void Flush()
        {
            if (G?.farm == null) return;
            foreach (var w in workers) if (w.job != null) { FinishJob(w.job, true); w.job = null; w.state = "idle"; }
            foreach (var j in jobs) FinishJob(j, true);
            jobs.Clear();
            foreach (var c in cooks) if (c.order != null) { FinishOrder(c.order, true); c.order = null; c.state = "idle"; }
            foreach (var o in orders) FinishOrder(o, true);
            orders.Clear();
            SaveGame();
        }

        static Critter NewCritter(Special k, float x, float y) => new Critter
        { id = k.id, kind = k, x = x, y = y, tx = x, ty = y, t = U.Rand(0.2f, 2), face = U.Chance(0.5f) ? 1 : -1, hop = U.Rand(0, 6) };

        // Caught critters become characters in all three scenes. A newcomer walks in from the ranch gate.
        public void Sync(bool initial)
        {
            foreach (var k in FarmOwned())
            {
                if (!petList.Any(p => p.id == k.id)) petList.Add(NewCritter(k, initial ? U.Rand(FARM.x0, FARM.x1) : 960, initial ? U.Rand(FARM.y0, FARM.y1) : FARM.y1));
                if (!workers.Any(p => p.id == k.id)) { var s = FieldIdleSpot(); workers.Add(NewCritter(k, s.x, s.y)); }
                if (!cooks.Any(p => p.id == k.id)) { var s = KitIdleSpot(); cooks.Add(NewCritter(k, s.x, s.y)); }
            }
            petList.RemoveAll(p => !HasSpecial(p.id)); workers.RemoveAll(p => !HasSpecial(p.id)); cooks.RemoveAll(p => !HasSpecial(p.id));
        }

        public void Go(string v)
        {
            if (cam != null || v == View || !SCENE_X.ContainsKey(v)) return;
            int d = Mathf.Abs(SCENE_X[v] - SCENE_X[View]);
            cam = new Cam { from = SCENE_X[View], to = SCENE_X[v], t = 0, dur = FARM.pan * (d > 1 ? 1.35f : 1) };
            View = v; hover = null; hoverTile = null; parts.Clear(); floats.Clear();
            dirty = true;
            Snd.Tone(220, 0.35f, Snd.Wave.Sine, 0.35f, 2.4f, "whoosh", 0.06f);
        }

        // ===== ranch =====
        public void Say(Critter p, string line, string anim)
        {
            p.say = line; p.sayT = 2.8f;
            if (anim != null) { p.anim = anim; p.animT = 0; if (p.job == null && p.order == null) { p.state = "idle"; p.t = Mathf.Max(p.t, FARM_ANIMS[anim] + 0.3f); } }
        }

        void ClickPet(Critter p)
        {
            if (FarmReady(p.id))
            {
                var r = FarmServe(p.id).Value; SaveGame(); Snd.Buy();
                Say(p, r.full ? "호감도 가득! 진화할 수 있어!" : U.Pick(FARM_LINES["thanks"]), "heart");
                AddFloat(p.x, p.y - 200, $"+{r.gem}", true, U.Hex("#a8f5d4"));
                if (r.bonus > 0) AddFloat(p.x + 60, p.y - 240, $"보너스 +{r.bonus}", true, U.Hex("#ffe36e"), -0.15f);
                GemBurst(p.x, p.y - 40, 12);
                if (r.heart) AddFloat(p.x, p.y - 280, $"호감도 {HeartStr(p.id)}", false, U.Hex("#ff7aa8"), -0.3f);
                dirty = true;
            }
            else
            {
                Snd.Ui();
                string anim = U.Pick(FARM_ANIMS.Keys.ToList());
                Say(p, anim == "sleep" ? "쿨쿨... 음냐..." : U.Pick(FARM_LINES[p.id].Concat(FARM_LINES["all"]).ToList()), anim);
            }
        }

        // 모두 돌보기 → (돌본 꼬마 수, 균사석, 보너스)
        public (int n, int gem, int bonus) CareAll()
        {
            int n = 0, gem = 0, bonus = 0;
            foreach (var p in petList)
            {
                var rr = FarmServe(p.id); if (rr == null) continue;
                var r = rr.Value;
                n++; gem += r.gem; bonus += r.bonus;
                Say(p, r.full ? "호감도 가득! 진화할 수 있어!" : U.Pick(FARM_LINES["thanks"]), "jump");
                AddFloat(p.x, p.y - 200, $"+{r.gem + r.bonus}", true, U.Hex("#a8f5d4"));
                GemBurst(p.x, p.y - 40, 5);
            }
            if (n > 0) { SaveGame(); dirty = true; }
            return (n, gem, bonus);
        }

        public void Evolved(string id)
        {
            var p = petList.FirstOrDefault(q => q.id == id);
            if (p == null) return;
            Say(p, "반짝반짝… 몸이 커졌어!", "spin");
            AddFloat(p.x, p.y - 200, "진화!", false, U.Hex("#ffe36e"));
            for (int i = 0; i < 30; i++) { float a = U.Rand(0, U.TAU), v = U.Rand(80, 300); parts.Add(new Part { k = "star", x = p.x, y = p.y - 50, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, life = U.Rand(0.6f, 1.1f) }); }
        }

        static float Radius(string id) => 44 * (1 + 0.15f * EvoOf(id));

        // 누를 수 있는 꼬마 (목장: 부탁 말풍선 포함)
        static Critter CritterAt(List<Critter> list, float x, float y, bool bubble)
        {
            Critter best = null; float bd = 1e9f;
            foreach (var p in list)
            {
                float R = Radius(p.id), d = U.D2(x, y, p.x, p.y - R * 0.9f);
                bool bub = bubble && FarmReady(p.id) && p.sayT <= 0 && x > p.x - 40 && x < p.x + 40 && y > p.y - 170 && y < p.y - 90;
                if ((d < R * 1.25f * R * 1.25f || bub) && d < bd) { best = p; bd = d; }
            }
            return best;
        }

        // 꼬마가 쉬러 갈 곳: 놓아 둔 꾸미기 근처를 좋아한다
        static Vector2 PetWanderTarget(Critter p)
        {
            var on = DECOR.Where(d => DecoOn(d.id)).ToList();
            if (on.Count > 0 && U.Chance(0.3f)) { var d = U.Pick(on); return new Vector2(U.Clamp(d.x + U.Rand(-90, 90), FARM.x0, FARM.x1), U.Clamp(d.y + U.Rand(20, 70), FARM.y0, FARM.y1)); }
            float a = U.Rand(0, U.TAU), r = U.Rand(120, 380);
            return new Vector2(U.Clamp(p.x + Mathf.Cos(a) * r, FARM.x0, FARM.x1), U.Clamp(p.y + Mathf.Sin(a) * r * 0.6f, FARM.y0, FARM.y1));
        }

        // 목표까지 걷기. 도착하면 true
        static bool WalkTo(Critter p, float x, float y, float spd, float dt)
        {
            float dx = x - p.x, dy = y - p.y, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 4) { p.x = x; p.y = y; return true; }
            float s = Mathf.Min(d, spd * dt);
            p.x += dx / d * s; p.y += dy / d * s; p.hop += dt * 9;
            if (Mathf.Abs(dx) > 2) p.face = dx > 0 ? 1 : -1;
            return false;
        }

        bool TickCritter(Critter p, float dt)
        {
            if (p.sayT > 0) p.sayT = Mathf.Max(0, p.sayT - dt);
            if (p.anim == null) return false;
            p.animT += dt;
            if (p.anim == "heart" && Random.value < dt * 6) parts.Add(new Part { k = "heart", x = p.x + U.Rand(-20, 20), y = p.y - 70, vx = U.Rand(-20, 20), vy = U.Rand(-90, -60), life = 1.1f });
            if (p.anim == "sleep" && Random.value < dt * 2) parts.Add(new Part { k = "z", x = p.x + 24, y = p.y - 80, vx = 18, vy = -40, life = 1.4f });
            if (p.animT >= FARM_ANIMS[p.anim]) p.anim = null;
            return true;   // 동작 중에는 걷지 않는다
        }

        void UpdatePets(float dt)
        {
            foreach (var p in petList)
            {
                p.t -= dt;
                if (TickCritter(p, dt) && View == "pets") continue;
                if (p.state == "walk")
                {
                    if (WalkTo(p, p.tx, p.ty, 70, dt) || p.t <= 0) { p.state = "idle"; p.t = U.Rand(1, 3.5f); }
                }
                else if (p.t <= 0)
                {
                    var t = PetWanderTarget(p); p.tx = t.x; p.ty = t.y; p.state = "walk"; p.t = 8;
                    if (View == "pets" && Random.value < 0.08f && !petList.Any(q => q.sayT > 0)) { p.say = U.Pick(FARM_LINES[p.id]); p.sayT = 2.4f; }
                }
            }
        }

        // ===== field =====
        public static float TileSize() => Mathf.Min(112, FIELD.span / G.farm.size);
        // top-left of plot (r, c) in stage pixels
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

        // 쉴 때 서 있는 자리: 밭 둘레
        static Vector2 FieldIdleSpot()
        {
            float half = FIELD.span / 2 + 40, a = U.Rand(0, U.TAU);
            return new Vector2(U.Clamp(FIELD.cx + Mathf.Cos(a) * (half + U.Rand(10, 60)), 300, 1160), U.Clamp(FIELD.cy + Mathf.Sin(a) * (half * 0.8f + U.Rand(0, 40)), 220, 1000));
        }

        // 칸에 할 일 맡기기. mode 없으면 칸 상태에 맞게. 심을 때 재료는 맡기는 순간 쓴다. 맡겼으면 true
        public bool FieldQueue(int r, int c, string mode, bool quiet)
        {
            string key = r + "," + c; var p = PlotAt(key);
            if (busy.ContainsKey(key)) return false;
            if (workers.Count == 0) { if (!quiet) { Snd.Err(); GameFlow.I.ShowToast("특수 버섯(꼬마)을 잡으면 밭일을 도와줘요"); } return false; }
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

        // 일이 끝났을 때 (quiet = 농장을 떠나며 한꺼번에 끝낼 때)
        void FinishJob(Job j, bool quiet)
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
                var tool = TOOLS[G.farm.tool]; int gem = CROPS[p.crop].gem;
                int sp = U.RandI(tool.dropMin, tool.dropMax) + (U.Chance((float)NF.gm_spore(Lv("gm_spore"))) ? 2 : 0);
                G.gem += gem; G.spore += sp;
                P.Remove(j.key);   // 수확하면 다시 안 간 땅
                if (!quiet) { AddFloat(mx, my - 50, $"+{gem}{(sp > 0 ? $"  포자 +{sp}" : "")}", true, U.Hex("#a8f5d4")); GemBurst(mx, my - 20, 6); }
            }
            else if (j.mode == "plant") G.spore += CROPS[j.crop].spore;   // 심을 수 없게 됐으면 포자는 돌려준다
            if (!quiet) { SaveGame(); dirty = true; }
        }

        void UpdateField(float dt)
        {
            const float spd = 380;
            foreach (var w in workers)
            {
                w.t -= dt;
                if (TickCritter(w, dt) && w.job == null) continue;
                float ev = FARM.evoSpd[EvoOf(w.id)];
                if (w.job != null)
                {
                    if (w.state == "walk") { if (WalkTo(w, w.tx, w.ty, spd * ev, dt)) { w.state = "work"; w.t = FIELD.work / ev; } }
                    else if (w.state == "work" && w.t <= 0)
                    {
                        var j = w.job; w.job = null; w.tool = null; w.state = "idle"; w.t = U.Rand(0.2f, 0.6f);
                        FinishJob(j, false);
                        if (j.mode == "harvest") Snd.Buy(); else Snd.Tone(j.mode == "till" ? 180 : 520, 0.06f, Snd.Wave.Triangle, 0.4f, 1.2f, "farm", 0.04f);
                    }
                    continue;
                }
                if (jobs.Count > 0)
                {
                    int bi = 0; float bd = 1e12f;
                    for (int i = 0; i < jobs.Count; i++) { var tt = TileXY(jobs[i].r, jobs[i].c); float d = U.D2(w.x, w.y, tt.x, tt.y); if (d < bd) { bd = d; bi = i; } }
                    var jb = jobs[bi]; jobs.RemoveAt(bi);
                    var t = TileXY(jb.r, jb.c); float T = TileSize();
                    w.job = jb; w.tx = t.x + T / 2; w.ty = t.y + T * 0.78f; w.state = "walk"; w.anim = null;
                    w.tool = jb.mode == "till" ? "hoe" : jb.mode == "plant" ? "seed" : "basket";
                    continue;
                }
                if (w.state == "walk") { if (WalkTo(w, w.tx, w.ty, 90, dt) || w.t <= 0) { w.state = "idle"; w.t = U.Rand(1.5f, 4); } }
                else if (w.t <= 0)
                {
                    var s = FieldIdleSpot(); w.tx = s.x; w.ty = s.y; w.state = "walk"; w.t = 8;
                    if (View == "field" && U.Chance(0.06f)) { w.say = U.Pick(FARM_LINES["field"]); w.sayT = 2.4f; }
                }
            }
        }

        // 일괄: 모두 갈기 · 모두 심기 · 모두 수확 (꼬마들이 나눠서 한다) → 맡긴 칸 수
        public int FieldAll(string mode)
        {
            int n = 0;
            foreach (var (r, c, _) in FieldKeys().ToList()) if (FieldQueue(r, c, mode, true)) n++;
            if (n > 0) dirty = true;
            return n;
        }

        public int Working => jobs.Count + workers.Count(w => w.job != null);

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

        // ===== kitchen =====
        static Vector2 KitIdleSpot() => new Vector2(U.Rand(300, 1150), U.Rand(880, 1020));

        public bool CanCook(Recipe rc) => !DishOn(rc.id) && !cooking.Contains(rc.id) && HasIngredients(rc);

        public bool CookOrder(string id, bool quiet)
        {
            var rc = RECIPE[id];
            if (cooks.Count == 0) { if (!quiet) { Snd.Err(); GameFlow.I.ShowToast("특수 버섯(꼬마)을 잡으면 요리를 해 줘요"); } return false; }
            if (!CanCook(rc)) { if (!quiet) Snd.Err(); return false; }
            PayIngredients(RecipeNeed(rc));
            orders.Add(id); cooking.Add(id);
            SaveGame();
            if (!quiet) { Snd.Ui(); GameFlow.I.ShowToast($"{UIUtil.Ic(rc.icon)} {rc.n} 주문! 꼬마 요리사가 만들어요"); dirty = true; }
            return true;
        }

        public List<Recipe> CookAll()
        {
            var made = RECIPES.Where(rc => CookOrder(rc.id, true)).ToList();
            if (made.Count > 0) dirty = true;
            return made;
        }

        void FinishOrder(string rid, bool quiet)
        {
            var rc = RECIPE[rid];
            cooking.Remove(rid);
            ServeDish(rid);
            if (!quiet)
            {
                SaveGame(); Snd.Record(); GameFlow.I.ShowToast($"{UIUtil.Ic(rc.icon)} {rc.n} 완성! 다음 라운드에 먹어요");
                AddFloat(KIT.table.x, KIT.table.y - 150, $"{UIUtil.Ic(rc.icon)} 완성!", false, U.Hex("#ffe36e"));
                dirty = true;
            }
        }

        static Vector2 KitAt(string where) => where == "counter" ? KIT.counter : where == "stove" ? KIT.stove : KIT.table;

        void UpdateKitchen(float dt)
        {
            foreach (var c in cooks)
            {
                c.t -= dt;
                if (TickCritter(c, dt) && c.order == null) continue;
                float ev = FARM.evoSpd[EvoOf(c.id)];
                if (c.order != null)
                {
                    var (where, dur, tool) = KIT.steps[c.step]; var at = KitAt(where);
                    if (c.state == "walk") { if (WalkTo(c, at.x + c.slot, at.y, 340 * ev, dt)) { c.state = "work"; c.t = dur / ev; c.tool = tool; } }
                    else if (c.state == "work" && c.t <= 0)
                    {
                        if (c.step < KIT.steps.Length - 1) { c.step++; c.state = "walk"; }
                        else { var o = c.order; c.order = null; c.tool = null; c.state = "idle"; c.t = U.Rand(0.5f, 1.5f); FinishOrder(o, false); }
                    }
                    continue;
                }
                if (orders.Count > 0)
                {
                    c.order = orders[0]; orders.RemoveAt(0); c.step = 0; c.state = "walk"; c.anim = null; c.slot = U.Rand(-50, 50); c.tool = null;
                    if (View == "kitchen") { c.say = $"{RECIPE[c.order].n} 만들게!"; c.sayT = 1.8f; }
                    continue;
                }
                if (c.state == "walk") { if (WalkTo(c, c.tx, c.ty, 80, dt) || c.t <= 0) { c.state = "idle"; c.t = U.Rand(1.5f, 4); } }
                else if (c.t <= 0)
                {
                    var s = KitIdleSpot(); c.tx = s.x; c.ty = s.y; c.state = "walk"; c.t = 8;
                    if (View == "kitchen" && U.Chance(0.06f)) { c.say = U.Pick(FARM_LINES["kitchen"]); c.sayT = 2.4f; }
                }
            }
        }

        // ===== effects =====
        void GemBurst(float x, float y, int n) { for (int i = 0; i < n; i++) parts.Add(new Part { k = "gem", x = x, y = y, vx = U.Rand(-160, 160), vy = U.Rand(-320, -120), life = U.Rand(0.6f, 1) }); }
        void AddFloat(float x, float y, string text, bool gem, Color col, float t = 0) => floats.Add(new Float { x = x, y = y, text = text, gem = gem, col = col, t = t });

        // ===== frame =====
        void Update()
        {
            if (G?.farm == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f), now = Time.time;
            HandleInput();
            UpdatePets(dt); UpdateField(dt); UpdateKitchen(dt);
            foreach (var q in parts) { q.t += dt; q.x += q.vx * dt; q.y += q.vy * dt; if (q.k == "gem" || q.k == "dirt") q.vy += 700 * dt; }
            parts.RemoveAll(q => q.t >= q.life);
            if (parts.Count > 200) parts.RemoveRange(0, parts.Count - 200);
            foreach (var f in floats) { f.t += dt; f.y -= 40 * dt; }
            floats.RemoveAll(f => f.t >= 1.4f);

            // camera: slides between scenes, pulling back a little halfway
            float x = SCENE_X[View], z = 1, shade = 0;
            if (cam != null)
            {
                cam.t = Mathf.Min(1, cam.t + dt / cam.dur);
                float u = cam.t < 0.5f ? 4 * cam.t * cam.t * cam.t : 1 - Mathf.Pow(-2 * cam.t + 2, 3) / 2;
                x = U.Lerp(cam.from, cam.to, u); z = 1 - 0.12f * Mathf.Sin(Mathf.PI * cam.t);
                shade = 0.45f * Mathf.Sin(Mathf.PI * cam.t);
                if (cam.t >= 1) { cam = null; dirty = true; }
            }
            var c0 = Art.P(960, 540);
            content.localScale = new Vector3(z, z, 1);
            content.localPosition = new Vector3(c0.x * (1 - z) - x * W / Art.PPU * z, c0.y * (1 - z), 0);
            field.gameObject.SetActive(Mathf.Abs(-1 - x) < 1);
            pets.gameObject.SetActive(Mathf.Abs(0 - x) < 1);
            kitchen.gameObject.SetActive(Mathf.Abs(1 - x) < 1);
            var sc = panShade.color; sc.a = shade; panShade.color = sc;
            panShade.enabled = shade > 0.001f;

            if (field.gameObject.activeSelf) DrawField(now);
            if (pets.gameObject.activeSelf) DrawPets(now);
            if (kitchen.gameObject.activeSelf) DrawKitchen(now);
            DrawFx();
        }

        void DrawPets(float now)
        {
            petsGrandpa.sparkle = FarmPetCount() > 0 && Mathf.Sin(now * 2) > 0;
            for (int i = 0; i < DECOR.Length; i++) UIUtil.Show(decor[i], DecoOn(DECOR[i].id));
            for (int i = 0; i < petList.Count; i++)
            {
                var p = petList[i];
                petPool.Get(i).Draw(p, now, null, hover == p, FarmReady(p.id) ? FarmKindOf(p.id) : null);
            }
            petPool.Trim(petList.Count);
        }

        void DrawField(float now)
        {
            fieldGrandpa.sparkle = FieldRipeCount() > 0 && Mathf.Sin(now * 2) > 0;
            int n = G.farm.size, o = FieldOff(n); float T = TileSize();
            double t0 = Now();
            var a = TileXY(o, o);
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
                    tile.Draw(p.x + T / 2, p.y + T / 2, T, PlotAt(key), t0, now, bz, hov, G.farm.tool);
                }
            for (int i = 0; i < workers.Count; i++) workerPool.Get(i).Draw(workers[i], now, "straw", hover == workers[i], null);
            workerPool.Trim(workers.Count);

            // 마우스를 올린 칸 안내
            UIUtil.Show(tileHint, hoverTile.HasValue && cam == null);
            if (hoverTile.HasValue && cam == null)
            {
                var (r, c, key) = hoverTile.Value; var q = PlotAt(key); var p = TileXY(r, c);
                string label = busy.ContainsKey(key) ? "꼬마가 일하러 가는 중…"
                    : q == null ? $"{UIUtil.Ic("tool_" + G.farm.tool)} 갈기 ({TOOLS[G.farm.tool].n})"
                    : q.s == "till" ? $"{UIUtil.Ic(G.farm.crop)} {CROPS[G.farm.crop].n} 심기"
                    : TileRipe(q) ? $"{UIUtil.Ic("crop_" + q.crop)} 수확 (균사석 +{CROPS[q.crop].gem})"
                    : $"{CROPS[q.crop].n} · {Mmss((long)System.Math.Ceiling((q.at - t0) / 1000))} 남음";
                UIUtil.SetText(tileHintText, label);
                tileHintText.ForceMeshUpdate();
                float w = tileHintText.preferredWidth * Art.PPU + 24;
                tileHint.transform.localPosition = Art.P(p.x + T / 2, p.y - 18);
                Art.SlicedPx(tileHintBack, w, 32);
            }
        }

        void DrawKitchen(float now)
        {
            bool cook = cooks.Any(c => c.order != null && c.step == 1 && c.state == "work");
            var gc = stoveGlow.color; gc.a = (cook ? 0.55f : 0.3f) + 0.1f * Mathf.Sin(now * 9); stoveGlow.color = gc;
            for (int i = 0; i < steam.Length; i++)
            {
                bool on = i < (cook ? 6 : 3);
                steam[i].enabled = on;
                if (!on) continue;
                float t = (now * 0.5f + i / 6f) % 1;
                steam[i].transform.localPosition = Art.P(380 + i * 16 + Mathf.Sin(now * 2 + i) * 10, KIT.stove.y - 330 - t * 140);
                float s = (14 + t * 18) * 2 / Art.PPU;
                steam[i].transform.localScale = new Vector3(s, s, 1);
                steam[i].color = new Color(1, 1, 1, 0.5f * (1 - t));
            }
            var ds = G.dishes.Where(id => RECIPE.ContainsKey(id)).ToList();
            for (int i = 0; i < dishes.Length; i++)
            {
                bool on = i < ds.Count;
                dishes[i].enabled = on;
                if (on) dishes[i].sprite = SpriteDB.Get("Farm/Icons/" + RECIPE[ds[i]].icon);
            }
            for (int i = 0; i < cooks.Count; i++) cookPool.Get(i).Draw(cooks[i], now, "chef", hover == cooks[i], null);
            cookPool.Trim(cooks.Count);
        }

        void DrawFx()
        {
            bool show = cam == null;
            int n = 0;
            if (show)
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
            if (show)
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
            if (p == null || cam != null || GameFlow.I.ModalOpen) return;
            bool isMouse = p is Mouse;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(isMouse ? -1 : 0);
            if (overUI) return;
            var s = ToStage(p.position.ReadValue());
            if (isMouse) Hover(s.x, s.y);
            if (p.press.wasPressedThisFrame) Click(s.x, s.y);
        }

        void Hover(float x, float y)
        {
            if (View == "field") { hover = CritterAt(workers, x, y, false); if (hover == null) hoverTile = FieldTileAt(x, y); return; }
            hover = CritterAt(View == "kitchen" ? cooks : petList, x, y, View == "pets");
        }

        void Click(float x, float y)
        {
            if (View == "field")
            {
                var w = CritterAt(workers, x, y, false);
                if (w != null && w.job == null) { Snd.Ui(); Say(w, U.Pick(FARM_LINES["field"].Concat(FARM_LINES[w.id]).ToList()), U.Pick(new[] { "jump", "dance", "heart" })); return; }
                FieldClick(x, y); return;
            }
            if (View == "kitchen")
            {
                var c = CritterAt(cooks, x, y, false);
                if (c != null) { Snd.Ui(); Say(c, c.order != null ? $"{RECIPE[c.order].n} 만드는 중이야!" : U.Pick(FARM_LINES["kitchen"].Concat(FARM_LINES[c.id]).ToList()), c.order != null ? null : U.Pick(new[] { "jump", "dance", "spin" })); }
                return;
            }
            var pt = CritterAt(petList, x, y, true); if (pt != null) ClickPet(pt);
        }
    }
}
