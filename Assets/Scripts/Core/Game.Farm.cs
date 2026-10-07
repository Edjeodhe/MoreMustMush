using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MoreMush.Defs;

namespace MoreMush
{
    // Mushroom farm rules: critter requests (hearts · evolution · diamonds), critter star grades and owned effects,
    // buildings (fixed footprints on a grid, built by a free critter over real time), field plots, idle harvest reward.
    // Times are real-time epoch milliseconds like Date.now(). Screen state (critters walking, job queue) is FarmView.
    public static partial class Game
    {
        public static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public static string Mmss(long s) => s >= 3600 ? $"{s / 3600}:{s % 3600 / 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";

        // ===== 꼬마 =====
        public static List<Special> FarmOwned() => SPECIALS.Where(k => HasSpecial(k.id)).ToList();

        // 잡은 꼬마는 부탁 시각이 없으면 만들어 준다 (자동 입주)
        public static void FarmEnsure()
        {
            foreach (var k in FarmOwned())
                if (!G.farm.next.ContainsKey(k.id)) { G.farm.next[k.id] = Now() + FARM.first * 1000; G.farm.kind[k.id] = "water"; }
        }

        public static bool FarmReady(string id) => G.farm.next.TryGetValue(id, out var t) && Now() >= t;
        public static int FarmPetCount() => G?.farm == null ? 0 : FarmOwned().Count(k => FarmReady(k.id));
        public static int FarmReadyCount() => FarmPetCount() + FieldRipeCount();   // 균사 트리 버튼 알림: 꼬마 부탁 + 다 자란 작물

        // 호감도·진화
        public static int EvoOf(string id) => Math.Min(FARM.evoMax, G.farm.evo.TryGetValue(id, out var e) ? e : 0);
        public static int HeartPer(string id) => FARM.per[EvoOf(id)];
        static int Love(string id) => G.farm.love.TryGetValue(id, out var l) ? l : 0;
        public static int HeartsOf(string id) => Math.Min(FARM.hearts, Love(id) / HeartPer(id));
        public static float HeartFrac(string id) => HeartsOf(id) >= FARM.hearts ? 0 : (float)(Love(id) % HeartPer(id)) / HeartPer(id);
        public static int HeartLeft(string id) => HeartsOf(id) >= FARM.hearts ? 0 : HeartPer(id) - Love(id) % HeartPer(id);
        public static bool CanEvolve(string id) => HeartsOf(id) >= FARM.hearts && EvoOf(id) < FARM.evoMax;
        public static string EvoName(Special k, int e = -1)
        {
            if (e < 0) e = EvoOf(k.id);
            return e == 0 ? k.n : e == 1 ? k.n.Replace("꼬마", "버섯") : k.n.Replace("꼬마", "대왕버섯");
        }
        public static string HeartStr(string id) => new string('♥', HeartsOf(id)) + new string('♡', FARM.hearts - HeartsOf(id));

        public static void FarmSchedule(string id)
        {
            G.farm.next[id] = Now() + U.Rand(FARM.wait[0], FARM.wait[1]) * FARM.evoWait[EvoOf(id)] * (1 - BStat("wait")) * 1000;
            G.farm.kind[id] = U.PickWeighted(FARM_KINDS, k => k.w).id;
        }

        public static FarmKind FarmKindOf(string id) => G.farm.kind.TryGetValue(id, out var k) && FARM_KIND.TryGetValue(k ?? "", out var fk) ? fk : FARM_KIND["water"];
        public static int FarmReward(string id) => Math.Max(1, (int)U.JsRound(FarmKindOf(id).gem * FARM.evoGem[EvoOf(id)]));

        public static string FarmLeftText(string id)
        {
            if (FarmReady(id)) return "부탁 있음!";
            long s = Math.Max(0, (long)Math.Ceiling(((G.farm.next.TryGetValue(id, out var t) ? t : Now()) - Now()) / 1000));
            return Mmss(s);
        }

        // 부탁 들어주기 → gem 균사석, bonus 호감도 보너스, dia 다이아몬드, heart 새 칸을 채움, full 5칸 다 참
        public struct Serve { public int gem, bonus, dia; public bool heart, full; }
        public static Serve? FarmServe(string id)
        {
            if (!FarmReady(id)) return null;
            int h0 = HeartsOf(id), gem = FarmReward(id);
            int bonus = U.Chance(FARM.bonusP * h0) ? U.RandI(1, 2 + EvoOf(id)) : 0;
            G.gem += gem + bonus;
            if (h0 < FARM.hearts) G.farm.love[id] = Love(id) + 1;
            FarmSchedule(id);
            int h1 = HeartsOf(id);
            bool heart = h1 > h0;
            int dia = (U.Chance(FARM.diaP + BStat("dia") + Fx("dia")) ? 1 : 0) + (heart ? FARM.diaHeart : 0);
            G.dia += dia;
            return new Serve { gem = gem, bonus = bonus, dia = dia, heart = heart, full = h1 >= FARM.hearts && h0 < FARM.hearts };
        }

        public static bool FarmEvolve(string id)
        {
            if (!HasSpecial(id) || !CanEvolve(id)) return false;
            G.farm.evo[id] = EvoOf(id) + 1; G.farm.love[id] = 0;
            SaveGame();
            return true;
        }

        // ===== 꼬마 별 등급 · 보유 효과 =====
        public static int CStarOf(string id) => G?.farm?.star != null && G.farm.star.TryGetValue(id, out var s) ? Math.Min(CSTAR.max, s) : 0;
        public static (double gold, int gem)? StarCost(string id) { int s = CStarOf(id); return s >= CSTAR.max ? ((double, int)?)null : (CSTAR.gold[s], CSTAR.gem[s]); }
        public static bool StarUp(string id)
        {
            var c = StarCost(id);
            if (!HasSpecial(id) || c == null || G.gold < c.Value.gold || G.gem < c.Value.gem) return false;
            G.gold -= c.Value.gold; G.gem -= c.Value.gem; G.farm.star[id] = CStarOf(id) + 1;
            SaveGame();
            return true;
        }

        // 꼬마 한 마리의 보유 효과 값 (안 잡았으면 0)
        public static float FxOf(string critter, int star = -1)
        {
            if (!CRITTER_FX.TryGetValue(critter, out var f)) return 0;
            if (star < 0) { if (G == null || !HasSpecial(critter)) return 0; star = CStarOf(critter); }
            return f.v0 + f.vs * star;
        }
        // 보유 효과 합 (key가 같은 꼬마 모두; "...Self" 효과는 그 꼬마가 일할 때만 FxOf로 따로 본다)
        public static float Fx(string key) { float v = 0; foreach (var kv in CRITTER_FX) if (kv.Value.key == key) v += FxOf(kv.Key); return v; }
        public static string FxText(string critter, int star = -1) => string.Format(CRITTER_FX[critter].fmt, U.Pct(FxOf(critter, star < 0 ? CStarOf(critter) : star)));

        // 별 등급 공통 능력치: 이 꼬마의 건설 속도 · 밭일 속도
        public static float BuildSpeed(string critter) => 1 + CSTAR.build * CStarOf(critter) + Fx("build");
        public static float WorkSpeed(string critter) => FARM.evoSpd[EvoOf(critter)] * (1 + CSTAR.work * CStarOf(critter) + Fx("work"));

        // ===== 건축 =====
        public static int BuiltCount(string id) => G.farm.blds.Count(b => b.id == id);          // 짓는 중 포함
        public static bool IsBuilding(string critter) { double now = Now(); return G.farm.blds.Any(b => !b.done && now < b.at && b.critter == critter); }
        public static List<Special> FreeCritters() => FarmOwned().Where(k => !IsBuilding(k.id)).ToList();
        // 완성된 건물 능력치 합
        public static float BStat(string stat) { if (G?.farm?.blds == null) return 0; float v = 0; foreach (var b in G.farm.blds) if (b.done && BUILDING[b.id].stat == stat) v += BUILDING[b.id].val; return v; }

        public static double BuildSeconds(Building b, string critter)
        {
            double t = b.time * (1 - BStat("build")) * NF.md_chef(Lv("md_chef"));
            if (critter != null && CRITTER_FX[critter].key == "buildSelf") t *= Math.Max(0.2, 1 - FxOf(critter));
            return t / (critter != null ? BuildSpeed(critter) : 1 + Fx("build"));
        }

        // 건물이 차지하는 화면 영역 (픽셀, 왼쪽 위 x·y와 너비·높이)
        public static Rect BuildRect(Building b, int gx, int gy) => new Rect(GRID.x0 + gx * GRID.cell, GRID.y0 + gy * GRID.cell, b.w * GRID.cell, b.h * GRID.cell);

        static bool InFence(float x, float y) { float dx = (x - GRID.ecx) / GRID.erx, dy = (y - GRID.ecy) / GRID.ery; return dx * dx + dy * dy <= 1; }

        // 그 자리에 놓을 수 있나: 울타리 안 · 밭(최대 크기) 밖 · 다른 건물과 안 겹침 (skipUid는 옮기는 중인 자기 자신)
        public static bool CanPlace(Building b, int gx, int gy, int skipUid = 0)
        {
            if (gx < 0 || gy < 0 || gx + b.w > GRID.cols || gy + b.h > GRID.rows) return false;
            var r = BuildRect(b, gx, gy);
            if (!InFence(r.xMin, r.yMin) || !InFence(r.xMax, r.yMin) || !InFence(r.xMin, r.yMax) || !InFence(r.xMax, r.yMax)) return false;
            var field = new Rect(FIELD.cx - FIELD.zone, FIELD.cy - FIELD.zone, FIELD.zone * 2, FIELD.zone * 2);
            if (r.Overlaps(field)) return false;
            foreach (var o in G.farm.blds)
                if (o.uid != skipUid && r.Overlaps(BuildRect(BUILDING[o.id], o.gx, o.gy))) return false;
            return true;
        }

        // 놓을 만한 첫 자리 (가운데에 가까운 순)
        public static Vector2Int? FirstSpot(Building b)
        {
            Vector2Int? best = null; float bd = float.MaxValue;
            for (int gy = 0; gy <= GRID.rows - b.h; gy++)
                for (int gx = 0; gx <= GRID.cols - b.w; gx++)
                {
                    if (!CanPlace(b, gx, gy)) continue;
                    var c = BuildRect(b, gx, gy).center;
                    float d = (c - new Vector2(1180, 560)).sqrMagnitude;
                    if (d < bd) { bd = d; best = new Vector2Int(gx, gy); }
                }
            return best;
        }

        public enum BuildFail { None, Max, Dia, NoCritter, Spot }
        public static BuildFail CanBuild(string id)
        {
            var b = BUILDING[id];
            if (BuiltCount(id) >= b.max) return BuildFail.Max;
            if (G.dia < b.price) return BuildFail.Dia;
            if (FreeCritters().Count == 0) return BuildFail.NoCritter;
            return BuildFail.None;
        }

        // 건설 시작: 쉬고 있는 꼬마 중 무작위 한 마리가 맡는다. 맡은 꼬마의 id를 돌려준다
        public static SaveData.Bld BuildStart(string id, int gx, int gy, IList<string> prefer = null)
        {
            var b = BUILDING[id];
            if (CanBuild(id) != BuildFail.None || !CanPlace(b, gx, gy)) return null;
            var free = FreeCritters().Select(k => k.id).ToList();
            string critter = U.Pick(prefer != null && prefer.Any(free.Contains) ? prefer.Where(free.Contains).ToList() : free);
            double now = Now();
            G.dia -= b.price;
            var bld = new SaveData.Bld { uid = G.farm.nextUid++, id = id, gx = gx, gy = gy, critter = critter, t0 = now, at = now + BuildSeconds(b, critter) * 1000 };
            G.farm.blds.Add(bld);
            SaveGame();
            return bld;
        }

        // 다 지은 건물을 완성 처리하고 돌려준다 (꼬마는 다시 쉰다)
        public static List<SaveData.Bld> BuildTick()
        {
            var done = new List<SaveData.Bld>();
            if (G?.farm?.blds == null) return done;
            double now = Now();
            foreach (var b in G.farm.blds) if (!b.done && now >= b.at) { b.done = true; done.Add(b); }
            if (done.Count > 0) SaveGame();
            return done;
        }

        public static string BuildLeftText(SaveData.Bld b) => Mmss(Math.Max(0, (long)Math.Ceiling((b.at - Now()) / 1000)));

        // ===== 버섯 밭 =====
        public static Dictionary<string, double> CropNeed(string c) => new Dictionary<string, double>
        { ["spore"] = CROPS[c].spore, [c] = Math.Ceiling(CROPS[c].need * Math.Pow(1.1, StageNow() - 1)) };
        public static double CropTime(string c) => CROPS[c].time * NF.gm_farm(Lv("gm_farm")) * (1 - BStat("grow")) * Math.Max(0.2, 1 - Fx("grow"));
        public static bool CanPlant(string c) { var n = CropNeed(c); return G.spore >= n["spore"] && InvTotal(c) >= n[c]; }
        public static int FieldOff(int n) => (FIELD.max - n) / 2;
        public static bool FieldOpen(int r, int c) { int n = G.farm.size, o = FieldOff(n); return r >= o && r < o + n && c >= o && c < o + n; }
        public static bool TileRipe(SaveData.Plot p) => p != null && p.s == "grow" && Now() >= p.at;
        public static SaveData.Plot PlotAt(string key) => G.farm.plots.TryGetValue(key, out var p) ? p : null;
        public static IEnumerable<(int r, int c, string key)> FieldKeys()
        {
            int o = FieldOff(G.farm.size);
            for (int r = o; r < o + G.farm.size; r++) for (int c = o; c < o + G.farm.size; c++) yield return (r, c, r + "," + c);
        }
        public static int FieldRipeCount() => G?.farm?.plots == null ? 0 : G.farm.plots.Values.Count(TileRipe);

        // 수확: 균사석 (보유 효과·건물·노드 반영, 소수점은 확률로) + 포자 (기본 0~1, 보너스)
        public static (int gem, int spore) HarvestYield(string crop, string critter)
        {
            double mul = 1 + BStat("fieldGem") + Fx("fieldGem") + (NF.gm_gourmet(Lv("gm_gourmet")) - 1)
                         + (critter != null && CRITTER_FX[critter].key == "fieldGemSelf" ? FxOf(critter) : 0);
            double g = CROPS[crop].gem * mul;
            int gem = (int)Math.Floor(g) + (U.Chance((float)(g - Math.Floor(g))) ? 1 : 0);
            int sp = U.RandI(0, 1) + (U.Chance((float)NF.gm_spore(Lv("gm_spore"))) ? 2 : 0) + (U.Chance(BStat("spore") + Fx("spore")) ? 1 : 0);
            return (gem, sp);
        }

        public static double ExpandCost() => FIELD.expand.TryGetValue(G.farm.size, out var c) ? c : 0;
        public static bool FieldExpand()
        {
            double c = ExpandCost();
            if (c <= 0 || G.gold < c) return false;
            G.gold -= c; G.farm.size++; SaveGame();
            return true;
        }

        public static string TimeText(double s) => s >= 3600 ? $"{(int)(s / 3600)}시간 {U.JsRound(s % 3600 / 60)}분" : s >= 60 ? $"{U.JsRound(s / 60)}분" : $"{Math.Ceiling(s)}초";

        // ===== 자동 수확 보상 =====
        public static void AutoEnsure() { if (G.autoT <= 0) G.autoT = Now(); }
        public static double AutoHours() => G == null || G.autoT <= 0 ? 0 : Math.Min(AUTO.maxHours, Math.Max(0, (Now() - G.autoT) / 3600000.0));
        public static double AutoMul() => (1 + BStat("auto")) * NF.gm_herb(Lv("gm_herb"));
        // 시간당 보상
        public static (double gold, double gem, double dia) AutoRates()
        {
            double m = AutoMul();
            double gold = Math.Max(AUTO.goldMin, TaxAmount(G.tax.cycle) * AUTO.goldTaxK) * (1 + Fx("autoGold")) * m;
            return (gold, AUTO.gemH * (1 + Fx("autoGem")) * m, AUTO.diaH * m);
        }
        public static (double gold, int gem, int dia) AutoReward()
        {
            var r = AutoRates(); double h = AutoHours();
            return (Math.Floor(r.gold * h), (int)Math.Floor(r.gem * h), (int)Math.Floor(r.dia * h));
        }
        public static bool AutoReady() => AutoHours() * 3600 >= AUTO.minClaim;

        public static (double gold, int gem, int dia, double debt)? ClaimAuto()
        {
            if (!AutoReady()) return null;
            var r = AutoReward();
            double debt = GainGold(r.gold);
            G.gem += r.gem; G.dia += r.dia; G.autoT = Now();
            SaveGame();
            return (r.gold, r.gem, r.dia, debt);
        }
    }
}
