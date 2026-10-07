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
        public List<string> dishes;
    }

    // Game rules on the save data (prototype "조회 · 해금 · 비용 · 세금 · 판매 · 능력치" sections).
    public static class Game
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
        public static bool WorkshopOpen() => CodexCount() >= 10;
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
            if (sp.tax > 0) return G.tax.paid >= sp.tax;
            if (sp.theme != null) return ThemeOpen(THEME[sp.theme]);
            if (sp.afterId != null) return G.codex.TryGetValue(sp.afterId, out var e) && e.n >= sp.cnt;
            return false;
        }

        public static string UnlockText(Species sp)
        {
            if (sp.init) return "처음부터 등장";
            if (sp.tax > 0) return $"세금 {sp.tax}회 납부 시 해금 (현재 {G.tax.paid}회)";
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
            gold = Math.Ceiling(n.costGold * Math.Pow(n.g, L) * COST_MUL),
            gem = n.costGem > 0 ? Math.Ceiling(n.costGem * Math.Pow(n.g, L) * COST_MUL) : 0,
        };
        public static bool CanAfford(Cost c) => G.gold + 1e-9 >= c.gold && G.gem + 1e-9 >= c.gem;
        static void Pay(Cost c) { G.gold -= c.gold; G.gem -= c.gem; }

        public static string NodeState(Node n)
        {
            int L = Lv(n.id);
            if (L >= n.max) return "max";
            if (L > 0) return "owned";
            if (n.parent == null || Lv(n.parent) > 0) return "avail";
            var pp = NODE[n.parent];
            return pp.parent == null || Lv(pp.parent) > 0 ? "locked" : "hidden";
        }

        public static bool BuyNode(Node n)
        {
            int L = Lv(n.id);
            if (L >= n.max) return false;
            if (n.parent != null && Lv(n.parent) < 1) return false;
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

        // ===== 세금 =====
        public static double TaxBill()
        {
            double b = Math.Max(TaxAmount(G.tax.cycle), TAX.share * G.tax.income);
            return Math.Ceiling(b * (SetDone("tax") ? 0.8 : 1) * NF.ed_tax(Lv("ed_tax")) / 10) * 10;
        }
        public static int TaxRoundsLeft() => TAX.every - G.tax.roundsIn;
        public static Species NextTaxUnlock() => SPECIES.Where(s => s.tax > 0 && s.tax > G.tax.paid).OrderBy(s => s.tax).FirstOrDefault();

        // ===== 창고·판매 =====
        public static double PriceMul() => NF.ed_price(Lv("ed_price")) * (Lv("core_ed") > 0 ? 1.1 : 1) * (HasSpecial("coin") ? 1.15 : 1) * (1 + StarAbilities()["price"]) * (1 + GoldenSetBonus());
        public static double UnitPrice(Species sp, double pm) => TIERS[sp.t].drop * SELL_PRICE * pm;
        public static double InvCount(string id) => Math.Floor(G.inv.TryGetValue(id, out var v) ? v : 0);
        public static double InvTotal(string cat = null) { double n = 0; foreach (var sp in SPECIES) if (cat == null || sp.c == cat) n += InvCount(sp.id); return n; }
        public static double InvValue(string cat = null) { double pm = PriceMul(), v = 0; foreach (var sp in SPECIES) if (cat == null || sp.c == cat) v += InvCount(sp.id) * UnitPrice(sp, pm); return v; }

        // 골드가 들어올 때는 체납금부터 갚는다. 갚은 금액을 돌려준다.
        public static double GainGold(double x) { double take = Math.Min(G.tax.debt, x); G.tax.debt -= take; G.gold += x - take; return take; }

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
            return new SkillInfo { on = Lv(id) > 0, S = S, p = SkillRate(S), P = P, pm = Math.Pow(2, P) };
        }

        // ===== 라운드 능력치 =====
        public static RoundStats ComputeStats(string wId, string themeId)
        {
            var st = new RoundStats { wId = wId, weather = WEATHER.TryGetValue(wId, out var w) ? w : WEATHER["clear"], theme = THEME.TryGetValue(themeId, out var th) ? th : THEMES[0], stage = StageNow() };
            var ab = st.ab = StarAbilities();
            bool Core(string id) => Lv(id) > 0;
            st.hvs = ActiveHvs();
            st.hvStar = st.hvs.ToDictionary(id => id, id => G.hv.TryGetValue(id, out var s) ? s : 1);
            st.atk = (NF.ps_atk(Lv("ps_atk")) + (SetDone("poison") ? 3 : 0)) * (Core("core_ps") ? 1.1 : 1) * (1 + ab["atk"]);
            double spd = NF.ps_spd(Lv("ps_spd")) * (1 + ab["spd"]) * (wId == "storm" ? 0.8 : 1) * (HasSpecial("wind") ? 1.1 : 1);
            st.spdMul = spd;
            st.launch = TUNE.launch * spd;
            st.minSpd = TUNE.min * spd;
            st.ballR = TUNE.ball0 * NF.ps_size(Lv("ps_size")) * (1 + ab["size"]);
            st.barLen = (150 * NF.md_bar(Lv("md_bar")) + (SetDone("shelf") ? 40 : 0)) * (Core("core_md") ? 1.1 : 1) * (1 + ab["bar"]) * (wId == "cold" ? 0.75 : 1) * (HasSpecial("rock") ? 1.12 : 1);
            st.duration = BASE_TIME * NF.ps_dur(Lv("ps_dur")) + (HasSpecial("dew") ? 2 : 0) + ab["dur"] - (wId == "cold" ? 3 : 0);
            st.crit = NF.ps_crit(Lv("ps_crit")) + (SetDone("coral") ? 0.05 : 0) + ab["crit"];
            st.comboK = NF.md_combo(Lv("md_combo")) * (1 + ab["combo"]);
            st.scoreMul = NF.ed_score(Lv("ed_score")) * (SetDone("cook") ? 1.5 : 1) * (1 + ab["score"]);
            st.maxCol = Math.Max(3, (int)Math.Ceiling(ColCount(Lv("ed_cols")) * (wId == "rain" ? 1.2 : 1) * (wId == "drought" ? 0.7 : 1) * (1 + ab["cols"]) * (HasSpecial("leaf") ? 1.15 : 1)));
            st.regen = NF.ed_regen(Lv("ed_regen")) / (wId == "rain" ? 2 : 1) * (wId == "storm" ? 2 : 1) / (1 + ab["regen"]);
            st.rareMul = NF.ed_rare(Lv("ed_rare")) * (HasSpecial("star") ? 1.25 : 1) * (1 + ab["rare"]);
            st.stageMul = 1 + 0.03 * (st.stage - 1);
            st.harvestMul = NF.ed_bonus(Lv("ed_bonus")) * (1 + ab["harvest"]) * (HasSpecial("chest") ? 1.15 : 1) * NF.gm_harvest(Lv("gm_harvest"));
            st.multiP = MultiRate(Lv("ed_multi"));
            st.priceMul = PriceMul();
            st.fire = HasSpecial("fire");
            st.sparkP = HasSpecial("spark") ? 0.06 : 0;
            st.colMax = 5 + Lv("ed_size");
            st.zoom = MAP_ZOOM[Lv("md_map")];
            double area = Math.Pow(1 / (st.zoom * st.zoom), 0.7);
            st.maxCol = Math.Max(4, (int)U.JsRound(st.maxCol * area));
            st.golden = (NF.ed_gold(Lv("ed_gold")) + (SetDone("glow") ? 0.01 : 0)) * (wId == "moon" ? 3 : 1) * (HasSpecial("moon") ? 1.5 : 1) + ab["golden"];
            st.devices = Math.Max(1, (int)U.JsRound((2 + Lv("ed_dev")) * area));
            st.permBalls = 1 + Lv("ps_ball");
            st.comboWin = COMBO_WINDOW;
            st.meteor = Lv("gm_meteor") > 0 ? NF.gm_meteor(Lv("gm_meteor")) : (NF.Meteor?)null;
            st.wanderP = wId == "autumn" ? 0.3 : 0.08;
            st.wanderMul = (wId == "autumn" ? 2 : 1.5) * (SetDone("cordy") ? 1.5 : 1);
            st.poisonMul = SetDone("amanita") ? 1.3 : 1;
            st.jellySlow = SetDone("jelly") ? 0.75 : 0.55;
            st.skillDmg = (SetDone("herb") ? 1.25 : 1) * (1 + ab["skillDmg"]);
            st.sporeP = 0.25 * (wId == "wind" ? 2 : 1) * NF.gm_toxin(Lv("gm_toxin"));
            st.debuffDur = 3 * (wId == "wind" ? 2 : 1) * NF.ps_resist(Lv("ps_resist")) * NF.gm_toxin(Lv("gm_toxin"));
            st.goldBonus = (1 + GoldenSetBonus()) * (wId == "rainbow" ? 1.5 : 1);
            st.harvestMul *= INCOME_MUL;
            st.goldMul = st.goldBonus * SCORE_GOLD;
            st.giantP = wId == "rainbow" ? 1 : NF.ed_giant(Lv("ed_giant"));
            st.specialP = SPECIAL_P + NF.ed_special(Lv("ed_special"));
            st.specialLife = SPECIAL_LIFE + Lv("ed_special");
            st.blade = Lv("bl") > 0;
            st.bladeR = st.ballR + NF.bl_range(Lv("bl_range"));
            st.bladeInt = NF.bl_spd(Lv("bl_spd"));
            st.bladeDmg = st.atk * 0.4 * Math.Pow(2, Lv("bl_pow"));
            st.kidP = KidRate(Lv("md_kid"));
            st.buffMul = NF.md_buffdur(Lv("md_buffdur"));
            st.tempLife = NF.md_templife(Lv("md_templife"));
            st.devMul = NF.md_stump(Lv("md_stump"));
            st.heavyMul = NF.ps_heavy(Lv("ps_heavy"));
            st.rushMul = NF.ps_rush(Lv("ps_rush"));
            foreach (var id in SKILL_IDS) { var si = SkillInfoOf(id); si.p *= 1 + ab["skill"]; st.sk[id] = si; }
            st.burstR = 80 * Math.Pow(1.2, st.sk["burst"].P) + (SetDone("fart") ? 30 : 0);
            st.festMul = 2 * st.sk["fest"].pm;
            st.dishes = G.dishes.Where(id => RECIPE.ContainsKey(id)).ToList();
            st.shroomHp = 1;
            double k = NF.md_chef(Lv("md_chef"));
            foreach (var id in st.dishes) RECIPE[id].apply(st, k * (RECIPE[id].cat == "ed" ? NF.gm_gourmet(Lv("gm_gourmet")) : 1));
            return st;
        }
    }
}
