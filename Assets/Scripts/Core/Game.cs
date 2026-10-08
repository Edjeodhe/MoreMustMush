using System;
using System.Collections.Generic;
using System.Linq;
using static MoreMush.Defs;

namespace MoreMush
{
    public class SkillInfo { public bool on; public int S, P; public double p, pm; }

    // Per-round stats (prototype computeStats() result "st").
    public class RoundStats
    {
        public string wId; public Weather weather; public Theme theme; public int stage;
        public Dictionary<string, double> ab;
        public List<string> hvs; public Dictionary<string, int> hvStar;
        public double atk, spdMul, launch, minSpd, ballR, barLen, duration, crit, comboK, scoreMul;
        public int maxCol, colMax, devices, permBalls;
        public double regen, rareMul, stageMul, harvestMul, multiP, priceMul, sparkP, zoom, golden;
        public bool fire, blade;
        public NF.Meteor? meteor;
        public double comboWin, wanderP, wanderMul, poisonMul, jellySlow, skillDmg, sporeP, debuffDur;
        public double goldBonus, goldMul, giantP, specialP, specialLife;
        public double bladeR, bladeInt, bladeDmg, kidP, buffMul, tempLife, devMul, heavyMul, rushMul;
        public Dictionary<string, SkillInfo> sk = new Dictionary<string, SkillInfo>();
        public double burstR, festMul, shroomHp = 1;
        public double zoneMul = 1, scoreFlat, critMul = 3;
    }

    // Game rules on the save data (prototype "조회 · 해금 · 비용 · 판매 · 능력치" sections).
    public static partial class Game
    {
        public static SaveData G;
        public static class DBG { public static bool open, infinite, allSeeds; public static int speed = 1; }

        public static void SaveGame() => SaveIO.Save(G);

        // ===== 조회 =====
        public static int Lv(string id) => G.nodes.TryGetValue(id, out var L) ? L : 0;
        public static bool IsUnlockedSp(Species sp) => sp.init || (G.seeds.TryGetValue(sp.id, out var b) && b) || DBG.allSeeds;
        public static bool Harvested(string id) => G.codex.TryGetValue(id, out var e) && e.n > 0;
        public static int CodexCount() => SPECIES.Count(s => Harvested(s.id));
        public static int GoldenCount() => SPECIES.Count(s => G.codex.TryGetValue(s.id, out var e) && e.gold);
        public static bool SetDone(string id) => SETS.First(x => x.id == id).ids.All(Harvested);
        public static bool HasSpecial(string id) => G.specials.TryGetValue(id, out var b) && b;

        public static double GoldenSetBonus()
        {
            int g = GoldenCount(); double b = 0;
            for (int i = 0; i < GOLDEN_STEPS.Length; i++) if (g >= GOLDEN_STEPS[i][0]) b = GOLDEN_STEP_BONUS[i];
            return b;
        }

        public static int StageNow() => G != null ? G.rounds + 1 : 1;
        public static bool ThemeOpen(Theme t) => StageNow() >= t.from;
        public static Theme LatestTheme() => THEMES.Where(ThemeOpen).Last();

        public static int StarOf(string id)
        {
            double n = G.codex.TryGetValue(id, out var e) ? e.n : 0;
            int s = 0; foreach (var k in STAR_N) if (n >= k) s++;
            return s;
        }

        public static Dictionary<string, double> StarAbilities()
        {
            var o = AB.Keys.ToDictionary(k => k, k => 0.0);
            foreach (var sp in SPECIES) { int s = StarOf(sp.id); if (s > 0) o[sp.ab] += s * AbPerStar(sp); }
            return o;
        }

        // ===== 버섯 해금 =====
        public static bool UnlockReady(Species sp)
        {
            if (sp.init) return true;
            if (sp.boss > 0) return StageNow() >= sp.boss;
            if (sp.theme != null) return ThemeOpen(THEME[sp.theme]);
            if (sp.afterId != null) return G.codex.TryGetValue(sp.afterId, out var e) && e.n >= sp.cnt;
            return false;
        }

        public static string UnlockText(Species sp)
        {
            if (sp.init) return "처음부터 등장";
            if (sp.boss > 0) return $"스테이지 {sp.boss} 보스 · 스테이지 {sp.boss} 도달 시 해금 (현재 {StageNow()})";
            if (sp.theme != null) { var t = THEME[sp.theme]; return $"{t.n} 지역 전용 (스테이지 {t.from}부터, 그 지역에서만 등장)"; }
            double have = G.codex.TryGetValue(sp.afterId, out var e) ? e.n : 0;
            var pre = SP[sp.afterId];
            return $"{(Harvested(pre.id) || IsUnlockedSp(pre) ? pre.n : "???")} {Math.Min(have, sp.cnt)}/{sp.cnt}개 수확 시 해금";
        }

        public static List<string> CheckUnlocks()
        {
            var o = new List<string>();
            foreach (var sp in SPECIES)
                if (!G.seeds.ContainsKey(sp.id) && !sp.init && UnlockReady(sp)) { G.seeds[sp.id] = true; o.Add(sp.id); }
            return o;
        }

        // ===== 비용 =====
        public struct Cost { public double gold, gem; }
        public static Cost NodeCost(Node n, int L) => new Cost
        {
            // 가격표(Defs.Prices.cs)가 있으면 레벨별 가격을 쓰고, 없으면 기본가 × 증가율^레벨
            gold = Math.Ceiling((n.costs != null && L < n.costs.Length ? n.costs[L] : n.costGold * Math.Pow(n.g, L)) * COST_MUL),
            gem = n.costGem > 0 ? Math.Ceiling(n.costGem * Math.Pow(n.g, L) * COST_MUL) : 0,
        };
        public static bool CanAfford(Cost c) => G.gold + 1e-9 >= c.gold && G.gem + 1e-9 >= c.gem;
        static void Pay(Cost c) { G.gold -= c.gold; G.gem -= c.gem; }

        // 부모 조건: 보통은 1레벨 이상, 단계 노드(II·III·IV)는 부모가 최대 레벨이어야 한다
        public static bool ParentOk(Node n) => n.parent == null || Lv(n.parent) >= (n.needMax ? NODE[n.parent].max : 1);

        public static string NodeState(Node n)
        {
            int L = Lv(n.id);
            if (L >= n.max) return "max";
            if (L > 0) return "owned";
            if (ParentOk(n)) return "avail";
            // 부모를 이미 샀으면(단계 노드가 최대 레벨을 기다리는 중 포함) 잠긴 채로 보여 준다
            if (Lv(n.parent) > 0) return "locked";
            var pp = NODE[n.parent];
            return ParentOk(pp) ? "locked" : "hidden";
        }

        public static bool BuyNode(Node n)
        {
            int L = Lv(n.id);
            if (L >= n.max) return false;
            if (!ParentOk(n)) return false;
            var c = NodeCost(n, L);
            if (!CanAfford(c)) return false;
            Pay(c); G.nodes[n.id] = L + 1; SaveGame();
            return true;
        }

        public static int BuyMax(Node n) { int k = 0; while (BuyNode(n)) k++; return k; }

        // 가장 싼 강화부터 산다. dry = true면 결과만 계산하고 되돌린다.
        public static (int levels, double spent) BulkBuy(ICollection<string> brs, double reserve, bool dry)
        {
            var saveNodes = new Dictionary<string, int>(G.nodes); double saveGold = G.gold;
            int levels = 0; double spent = 0;
            for (int guard = 0; guard < 3000; guard++)
            {
                Node best = null; double bc = double.PositiveInfinity;
                foreach (var n in NODES)
                {
                    if (!brs.Contains(n.br) || n.costGem > 0) continue;
                    var s = NodeState(n); if (s != "avail" && s != "owned") continue;
                    double c = NodeCost(n, Lv(n.id)).gold; if (c < bc) { bc = c; best = n; }
                }
                if (best == null || G.gold - bc < reserve) break;
                G.gold -= bc; G.nodes[best.id] = Lv(best.id) + 1; levels++; spent += bc;
            }
            if (dry) { G.nodes = saveNodes; G.gold = saveGold; } else SaveGame();
            return (levels, spent);
        }

        // ===== 창고·판매 =====
        // 같은 능력치에 붙는 보너스(노드·코어·꼬마·별·세트)는 곱하지 않고 더한다
        public static double PriceMul() => NF.ed_price(Lv("ed_price")) + NF.ed_price2(Lv("ed_price2")) + (Lv("core_ed") > 0 ? 0.1 : 0) + StarAbilities()["price"] + GoldenSetBonus();
        public static double UnitPrice(Species sp, double pm) => TIERS[sp.t].drop * SELL_PRICE * pm;
        public static double InvCount(string id) => Math.Floor(G.inv.TryGetValue(id, out var v) ? v : 0);
        public static double InvTotal(string cat = null) { double n = 0; foreach (var sp in SPECIES) if (cat == null || sp.c == cat) n += InvCount(sp.id); return n; }
        public static double InvValue(string cat = null) { double pm = PriceMul(), v = 0; foreach (var sp in SPECIES) if (cat == null || sp.c == cat) v += InvCount(sp.id) * UnitPrice(sp, pm); return v; }

        // ===== 수확기·날씨 =====
        public static List<string> ActiveHvs()
        {
            var a = HARVESTERS.Where(h => G.hv.ContainsKey(h.id) && (!G.hvOn.TryGetValue(h.id, out var on) || on)).Select(h => h.id).ToList();
            return a.Count > 0 ? a : new List<string> { "sam" };
        }
        public static bool WeatherOpen(Weather w) => w.need == 0 || CodexCount() >= w.need;
        public static string RollWeather() => U.PickWeighted(WEATHERS.Where(WeatherOpen).ToList(), w => w.p).id;

        static SkillInfo SkillInfoOf(string id)
        {
            int S = Lv(id + "_rate"), P = Lv(id + "_pow");
            return new SkillInfo { on = Lv(id) > 0, S = S, p = SkillRate(S), P = P, pm = SkillPow(P) };
        }

        // ===== 라운드 능력치 =====
        public static RoundStats ComputeStats(string wId, string themeId)
        {
            var st = new RoundStats { wId = wId, weather = WEATHER.TryGetValue(wId, out var w) ? w : WEATHER["clear"], theme = THEME.TryGetValue(themeId, out var th) ? th : THEMES[0], stage = StageNow() };
            var ab = st.ab = StarAbilities();
            bool Core(string id) => Lv(id) > 0;
            st.hvs = ActiveHvs();
            st.hvStar = st.hvs.ToDictionary(id => id, id => G.hv.TryGetValue(id, out var s) ? s : 1);
            // 노드·코어·별 능력치·꼬마 보너스는 더하고, 날씨(라운드마다 바뀌는 조건)만 곱한다
            // 지역 배율: 버섯 체력·점수·수확 개수가 함께 커진다
            st.zoneMul = ZoneMul(st.theme.id, st.stage);
            // 공격력 = (기본 1 + 단계 노드 고정값) × (1 + 퍼센트 보너스 합)
            double atkFlat = 1 + NF.ps_atk(Lv("ps_atk")) + NF.ps_atk2(Lv("ps_atk2")) + NF.ps_atk2p(Lv("ps_atk2p")) + NF.ps_atk3(Lv("ps_atk3")) + NF.ps_atk3p(Lv("ps_atk3p")) + NF.ps_atk4(Lv("ps_atk4")) + (SetDone("poison") ? 3 : 0);
            st.atk = atkFlat * (1 + (Core("core_ps") ? 0.1 : 0) + ab["atk"]);
            double spd = (NF.ps_spd(Lv("ps_spd")) + ab["spd"]) * (wId == "storm" ? 0.8 : 1);
            st.spdMul = spd;
            st.launch = TUNE.launch * spd;
            st.minSpd = TUNE.min * spd;
            st.ballR = TUNE.ball0 * BALL_SCALE * (NF.ps_size(Lv("ps_size")) + ab["size"]);
            st.barLen = (150 * (NF.md_bar(Lv("md_bar")) + (Core("core_md") ? 0.1 : 0) + ab["bar"]) + (SetDone("shelf") ? 40 : 0)) * (wId == "cold" ? 0.75 : 1);
            st.duration = BASE_TIME + StageTime(st.stage) + NF.ps_dur(Lv("ps_dur")) + NF.ps_dur2(Lv("ps_dur2")) + ab["dur"] - (wId == "cold" ? 3 : 0);
            st.crit = NF.ps_crit(Lv("ps_crit")) + (SetDone("coral") ? 0.05 : 0) + ab["crit"];
            st.critMul = NF.ps_crit2(Lv("ps_crit2"));
            st.comboK = NF.md_combo(Lv("md_combo")) + NF.md_combo2(Lv("md_combo2")) + 0.1 * ab["combo"];
            // 타격 점수 = (등급 기본 점수 + 단계 노드 고정값) × (1 + 퍼센트 보너스 합) × 지역 배율
            st.scoreFlat = NF.ed_score(Lv("ed_score")) + NF.ed_score1p(Lv("ed_score1p")) + NF.ed_score2(Lv("ed_score2")) + NF.ed_score2p(Lv("ed_score2p")) + NF.ed_score3(Lv("ed_score3")) + NF.ed_score3p(Lv("ed_score3p"));
            st.scoreMul = (1 + (SetDone("cook") ? 0.5 : 0) + ab["score"]) * st.zoneMul;
            st.maxCol = Math.Max(3, (int)Math.Ceiling((ColCount(Lv("ed_cols")) + NF.ed_cols2(Lv("ed_cols2"))) * (1 + ab["cols"]) * (wId == "rain" ? 1.2 : 1) * (wId == "drought" ? 0.7 : 1)));
            st.regen = NF.ed_regen(Lv("ed_regen")) / (1 + ab["regen"]) / (wId == "rain" ? 2 : 1) * (wId == "storm" ? 2 : 1);
            st.rareMul = NF.ed_rare(Lv("ed_rare")) + ab["rare"];
            st.stageMul = 1 + 0.03 * (st.stage - 1);
            // 수확 개수 = (기본 1 + 단계 노드 고정값) × (1 + 퍼센트 보너스 합) × 균사석 × 지역 배율
            double harvFlat = 1 + NF.ed_bonus(Lv("ed_bonus")) + NF.ed_bonus1p(Lv("ed_bonus1p")) + NF.ed_bonus2(Lv("ed_bonus2")) + NF.ed_bonus2p(Lv("ed_bonus2p")) + NF.ed_bonus3(Lv("ed_bonus3")) + NF.ed_bonus3p(Lv("ed_bonus3p"));
            st.harvestMul = harvFlat * (1 + ab["harvest"] + BStat("harvest")) * NF.gm_harvest(Lv("gm_harvest")) * st.zoneMul;
            st.multiP = MultiRate(Lv("ed_multi"));
            st.priceMul = PriceMul();
            st.colMax = NF.ed_size(Lv("ed_size"));
            st.zoom = MAP_ZOOM[Lv("md_map")];
            double area = Math.Pow(1 / (st.zoom * st.zoom), 0.7);
            st.maxCol = Math.Max(4, (int)U.JsRound(st.maxCol * area));
            st.golden = (NF.ed_gold(Lv("ed_gold")) + (SetDone("glow") ? 0.01 : 0) + ab["golden"]) * (wId == "moon" ? 3 : 1);
            st.devices = Math.Max(1, (int)U.JsRound(NF.ed_dev(Lv("ed_dev")) * area));
            st.permBalls = 1 + Lv("ps_ball") + Lv("ps_ball2") + Lv("ps_ball3");
            st.comboWin = COMBO_WINDOW;
            st.meteor = Lv("gm_meteor") > 0 ? NF.gm_meteor(Lv("gm_meteor")) : (NF.Meteor?)null;
            st.wanderP = wId == "autumn" ? 0.3 : 0.08;
            st.wanderMul = (wId == "autumn" ? 2 : 1.5) * (SetDone("cordy") ? 1.5 : 1);
            st.poisonMul = SetDone("amanita") ? 1.3 : 1;
            st.jellySlow = SetDone("jelly") ? 0.75 : 0.55;
            st.skillDmg = 1 + (SetDone("herb") ? 0.25 : 0) + ab["skillDmg"];
            st.sporeP = 0.25 * (wId == "wind" ? 2 : 1) * NF.gm_toxin(Lv("gm_toxin"));
            st.debuffDur = 3 * (wId == "wind" ? 2 : 1) * NF.ps_resist(Lv("ps_resist")) * NF.gm_toxin(Lv("gm_toxin"));
            st.goldBonus = (1 + GoldenSetBonus()) * (wId == "rainbow" ? 1.5 : 1);
            st.harvestMul *= INCOME_MUL;
            st.goldMul = st.goldBonus * SCORE_GOLD;
            st.giantP = wId == "rainbow" ? 1 : NF.ed_giant(Lv("ed_giant"));
            st.specialP = SPECIAL_P + NF.ed_special(Lv("ed_special"));
            st.specialLife = NF.ed_special_life(Lv("ed_special"));
            st.blade = Lv("bl") > 0;
            st.bladeR = st.ballR + NF.bl_range(Lv("bl_range"));
            st.bladeInt = NF.bl_spd(Lv("bl_spd"));
            st.bladeDmg = st.atk * NF.bl_pow(Lv("bl_pow"));
            st.kidP = KidRate(Lv("md_kid"));
            st.buffMul = NF.md_buffdur(Lv("md_buffdur"));
            st.tempLife = NF.md_templife(Lv("md_templife"));
            st.devMul = NF.md_stump(Lv("md_stump"));
            st.heavyMul = NF.ps_heavy(Lv("ps_heavy"));
            st.rushMul = NF.ps_rush(Lv("ps_rush"));
            foreach (var id in SKILL_IDS) { var si = SkillInfoOf(id); si.p *= 1 + ab["skill"]; st.sk[id] = si; }
            st.burstR = NF.burst_r(st.sk["burst"].P) + (SetDone("fart") ? 30 : 0);
            st.festMul = 2 * st.sk["fest"].pm;
            st.shroomHp = st.zoneMul;
            return st;
        }
    }
}
