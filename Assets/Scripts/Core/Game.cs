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
        public int maxCol, colMax, permBalls;
        public List<string> devTypes;   // 해금한 숲 장치 (라운드마다 하나씩)
        public double regen, rareMul, stageMul, harvestMul, multiP, priceMul, sparkP, zoom, golden;
        public bool fire, blade;
        public NF.Meteor? meteor;
        public double comboWin, wanderP, wanderMul, poisonMul, jellySlow, skillDmg, sporeP, debuffDur;
        public double goldBonus, goldMul, giantP;
        public double bladeR, bladeInt, bladeDmg, kidP, buffMul, tempLife, devMul, heavyMul, rushMul;
        public double barSpd = 1, barDur, barRate, powAtk, powSkill, bossMul = 1, bossGemAdd;   // 슬라이드 바 수치 강화(독 가지)·균사석 노드
        public Dictionary<string, SkillInfo> sk = new Dictionary<string, SkillInfo>();
        public double burstR, festMul, shroomHp = 1;
        public double zoneMul = 1, scoreFlat, critMul = 3;
        public BossDef boss; public double bossHp;   // 보스 라운드면 보스와 그 체력 (아니면 null)
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
        // 라운드 HUD가 프레임마다 부르므로 LINQ 대신 루프로 센다
        public static int CodexCount() { int c = 0; for (int i = 0; i < SPECIES.Count; i++) if (Harvested(SPECIES[i].id)) c++; return c; }
        public static int GoldenCount() { int c = 0; for (int i = 0; i < SPECIES.Count; i++) if (G.codex.TryGetValue(SPECIES[i].id, out var e) && e.gold) c++; return c; }
        public static bool SetDone(string id) => SETS.First(x => x.id == id).ids.All(Harvested);
        public static bool HasSpecial(string id) => G.specials.TryGetValue(id, out var b) && b;

        public static double GoldenSetBonus()
        {
            int g = GoldenCount(); double b = 0;
            for (int i = 0; i < GOLDEN_STEPS.Length; i++) if (g >= GOLDEN_STEPS[i][0]) b = GOLDEN_STEP_BONUS[i];
            return b;
        }

        public static int StageNow() => G != null ? G.stage : 1;
        // 이번 라운드가 보스 라운드인가: 보스 스테이지에서 보스 도전이 켜져 있을 때
        public static bool BossRoundNext() => G != null && G.bossOn && IsBossStage(StageNow());
        // 보스 스테이지는 보스를 쓰러뜨려야 넘어가므로, 지나온 보스 스테이지는 모두 쓰러뜨린 것이다
        public static bool BossCleared(int stage) => StageNow() > stage;
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
            if (sp.boss > 0) return BossCleared(sp.boss);
            if (sp.theme != null) return ThemeOpen(THEME[sp.theme]);
            if (sp.afterId != null) return G.codex.TryGetValue(sp.afterId, out var e) && e.n >= sp.cnt;
            return false;
        }

        public static string UnlockText(Species sp)
        {
            if (sp.init) return Loc.T("처음부터 등장");
            if (sp.boss > 0) return Loc.F("스테이지 {0} 보스({1})를 쓰러뜨리면 해금 (현재 스테이지 {2})", sp.boss, BossAt(sp.boss).n, StageNow());
            if (sp.theme != null) { var t = THEME[sp.theme]; return Loc.F("{0} 지역 전용 (스테이지 {1}부터, 그 지역에서만 등장)", t.n, t.from); }
            double have = G.codex.TryGetValue(sp.afterId, out var e) ? e.n : 0;
            var pre = SP[sp.afterId];
            return Loc.F("{0} {1}/{2}개 수확 시 해금", (Harvested(pre.id) || IsUnlockedSp(pre) ? pre.n : "???"), Math.Min(have, sp.cnt), sp.cnt);
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
        // 더 살 수 없는 레벨인가. 싱크 노드는 가격이 넘치기 직전(infCap)까지 산다
        public static bool AtMax(Node n, int L) => n.inf ? L >= n.infCap : L >= n.max;

        public static Cost NodeCost(Node n, int L) => n.inf
            ? new Cost { gold = Math.Ceiling(n.infCost0 * Math.Pow(n.infCostK, L) * COST_MUL), gem = 0 }   // 싱크: 레벨마다 ×infCostK
            : new Cost
        {
            // 레벨별 가격 (엑셀 DB Upgrade_Cost: Cost_Lv = 골드, Gem_Lv = 균사석)
            gold = L < n.costs.Length ? Math.Ceiling(n.costs[L] * COST_MUL) : 0,
            gem = L < n.gems.Length && n.gems[L] > 0 ? Math.Ceiling(n.gems[L] * COST_MUL) : 0,
        };
        public static bool CanAfford(Cost c) => !double.IsInfinity(c.gold) && !double.IsNaN(c.gold) && G.gold + 1e-9 >= c.gold && G.gem + 1e-9 >= c.gem;
        static void Pay(Cost c) { G.gold -= c.gold; G.gem -= c.gem; }

        // 부모 조건: 보통은 1레벨 이상, 단계 노드(II·III·IV)는 부모가 최대 레벨이어야 한다
        public static bool ParentOk(Node n) => (n.parent == null || Lv(n.parent) >= 1) && (n.req == null || Lv(n.req) >= NODE[n.req].max);

        public static string NodeState(Node n)
        {
            int L = Lv(n.id);
            if (AtMax(n, L)) return "max";
            if (L > 0) return "owned";
            if (ParentOk(n)) return "avail";
            // 부모를 이미 샀으면(단계 노드가 최대 레벨을 기다리는 중 포함) 잠긴 채로 보여 준다
            if (n.parent == null || Lv(n.parent) > 0) return "locked";
            var pp = NODE[n.parent];
            return ParentOk(pp) ? "locked" : "hidden";
        }

        public static bool BuyNode(Node n, bool save = true)
        {
            int L = Lv(n.id);
            if (AtMax(n, L)) return false;
            if (!ParentOk(n)) return false;
            var c = NodeCost(n, L);
            if (!CanAfford(c)) return false;
            Pay(c); G.nodes[n.id] = L + 1;
            Telemetry.Ftue(Telemetry.FTUE_FIRST_UPGRADE);
            if (save) SaveGame();
            return true;
        }

        // 여러 레벨을 한 번에 사도 저장(JSON 직렬화 + PlayerPrefs 쓰기)은 한 번만 한다
        public static int BuyMax(Node n) { int k = 0; while (k < 10000 && BuyNode(n, false)) k++; if (k > 0) SaveGame(); return k; }   // 싱크는 상한이 없어 반복 상한을 둔다

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
                    if (!brs.Contains(n.br) || n.gem) continue;
                    var s = NodeState(n); if (s != "avail" && s != "owned") continue;
                    if (!ParentOk(n)) continue;   // 산 노드라도 선행(부모·최대 레벨 선행)이 안 맞으면 더 못 올린다
                    double c = NodeCost(n, Lv(n.id)).gold; if (c < bc) { bc = c; best = n; }
                }
                if (best == null || G.gold - bc < reserve) break;
                G.gold -= bc; G.nodes[best.id] = Lv(best.id) + 1; levels++; spent += bc;
            }
            if (dry) { G.nodes = saveNodes; G.gold = saveGold; } else { SaveGame(); if (levels > 0) Telemetry.Ftue(Telemetry.FTUE_FIRST_UPGRADE); }
            return (levels, spent);
        }

        // ===== 정산 골드 =====
        // 버섯 판매가 없어진 뒤, 예전 판매가 보너스(노드·코어·별 능력치)는 정산 골드 +%로 쓴다. 보너스끼리는 더한다
        public static double SettleBonus(Dictionary<string, double> ab) =>
            (NF.ed_price(Lv("ed_price")) - 1) + NF.ed_price2(Lv("ed_price2")) + (NF.ed_trade(Lv("ed_trade")) - 1) + 0.1 /* 예전 식용 코어 보너스, 코어가 없어져 기본으로 */ + ab["price"];
        // 버섯 한 개의 골드 값어치 (엽전 수확기가 쓴다): 등급 기본가 × (1 + 정산 보너스 + 황금 세트)
        public static double PriceMul(Dictionary<string, double> ab) => 1 + SettleBonus(ab) + GoldenSetBonus();

        // ===== 수확기·날씨 =====
        // 장착 칸 = 영구 핀볼 수. 수확기는 종류마다 한 칸씩, 남은 칸은 기본 수확기로 채운다
        public static int HvSlots() => 1 + Lv("ps_ball") + Lv("ps_ball2") + Lv("ps_ball3");
        public static List<string> OnHvs() => HARVESTERS.Where(h => HvOn(h.id)).Select(h => h.id).ToList();
        // 켠 수확기가 장착 칸보다 많으면 라운드를 시작할 수 없다
        public static bool HvOver() => OnHvs().Count > HvSlots();
        // 이번 라운드의 장착 목록 (길이 = 장착 칸, i번째 영구 핀볼이 i번째 수확기)
        public static List<string> ActiveHvs()
        {
            int slots = HvSlots();
            var a = OnHvs().Take(slots).ToList();
            while (a.Count < slots) a.Add(BASE_HV);
            return a;
        }
        // 일반 채집은 좋은 날씨만, 보스 채집은 나쁜 날씨만 (전투 기획서 Ver05 · 날씨 변경점). 같은 유형 안에서는 DB Wea_Rate 가중치로
        public static List<Weather> WeatherPool(bool boss) => WEATHERS.Where(w => w.good != boss).ToList();
        public static string RollWeather(bool boss)
        {
            var pool = WeatherPool(boss);
            // 가중치가 모두 같으면 예전처럼 고르게 뽑는다 (난수 소비가 같아 봇 시뮬 결과가 그대로)
            return (pool.All(w => w.rate == pool[0].rate) ? U.Pick(pool) : (U.PickWeighted(pool, w => w.rate) ?? U.Pick(pool))).id;
        }

        static SkillInfo SkillInfoOf(string id)
        {
            var (S, P) = SkillSP(SKILLS[id], Lv(id));
            return new SkillInfo { on = Lv(id) > 0, S = S, p = SkillRate(S), P = P, pm = SkillPow(P) };
        }

        // ===== 라운드 능력치 =====
        public static RoundStats ComputeStats(string wId, string themeId, BossDef boss = null)
        {
            var st = new RoundStats { wId = wId, weather = WEATHER.TryGetValue(wId, out var w) ? w : WEATHER["clear"], theme = THEME.TryGetValue(themeId, out var th) ? th : THEMES[0], stage = StageNow() };
            var ab = st.ab = StarAbilities();
            st.hvs = ActiveHvs();
            st.hvStar = st.hvs.Distinct().ToDictionary(id => id, id => G.hv.TryGetValue(id, out var s) ? s : 1);
            // 노드·코어·별 능력치·꼬마 보너스는 더하고, 날씨(라운드마다 바뀌는 조건)만 곱한다
            // 지역 배율: 버섯 체력·점수·수확 개수가 함께 커진다
            st.zoneMul = ZoneMul(st.theme.id, st.stage);
            // 공격력 = (기본 1 + 단계 노드 고정값) × (1 + 퍼센트 보너스 합)
            double atkFlat = 1 + NF.ps_atk(Lv("ps_atk")) + NF.ps_atk2(Lv("ps_atk2")) + NF.ps_atk2p(Lv("ps_atk2p")) + NF.ps_atk3(Lv("ps_atk3")) + NF.ps_atk3p(Lv("ps_atk3p")) + NF.ps_atk4(Lv("ps_atk4")) + (SetDone("poison") ? 3 : 0);
            st.atk = atkFlat * (1 + 0.1 + ab["atk"]) * NF.gm_atk(Lv("gm_atk")) * NF.inf_atk(Lv("inf_atk"));
            double spd = (NF.ps_spd(Lv("ps_spd")) + ab["spd"]) * (wId == "storm" ? 0.8 : 1);
            st.spdMul = spd;
            st.launch = TUNE.launch * spd;
            st.minSpd = TUNE.min * spd;
            st.ballR = TUNE.ball0 * BALL_SCALE * (NF.ps_size(Lv("ps_size")) + ab["size"]);
            st.barLen = (150 * (NF.md_bar(Lv("md_bar")) + NF.ps_bar(Lv("ps_bar")) + 0.1 + ab["bar"]) + (SetDone("shelf") ? 40 : 0)) * NF.gm_bar(Lv("gm_bar")) * (wId == "cold" ? 0.75 : 1);   // 최대 필드 폭의 1/3 (RoundSim.Start)
            st.duration = BASE_TIME + StageTime(st.stage) + NF.ps_dur(Lv("ps_dur")) + NF.ps_dur2(Lv("ps_dur2")) + ab["dur"] - (wId == "cold" ? 3 : 0);
            st.crit = NF.ps_crit(Lv("ps_crit")) + (SetDone("coral") ? 0.05 : 0) + ab["crit"];
            st.critMul = NF.ps_crit2(Lv("ps_crit2"));
            st.comboK = NF.md_combo(Lv("md_combo")) + NF.md_combo2(Lv("md_combo2")) + 0.1 * ab["combo"];
            // 타격 점수 = (등급 기본 점수 + 단계 노드 고정값) × (1 + 퍼센트 보너스 합) × 지역 배율
            st.scoreFlat = NF.ed_score(Lv("ed_score")) + NF.ed_score1p(Lv("ed_score1p")) + NF.ed_score2(Lv("ed_score2")) + NF.ed_score2p(Lv("ed_score2p")) + NF.ed_score3(Lv("ed_score3")) + NF.ed_score3p(Lv("ed_score3p"));
            st.scoreMul = (1 + (SetDone("cook") ? 0.5 : 0) + ab["score"]) * st.zoneMul;
            st.maxCol = Math.Max(3, (int)Math.Ceiling((ColCount(Lv("ed_cols")) + NF.ed_cols2(Lv("ed_cols2"))) * (1 + ab["cols"]) * (wId == "rain" ? 1.2 : 1)));
            st.regen = NF.ed_regen(Lv("ed_regen")) / (1 + ab["regen"]) / (wId == "rain" ? 2 : 1) * (wId == "storm" ? 2 : 1);
            st.rareMul = NF.ed_rare(Lv("ed_rare")) + ab["rare"];
            st.stageMul = 1 + 0.03 * (st.stage - 1);
            // 수확 개수 = (기본 1 + 단계 노드 고정값) × (1 + 퍼센트 보너스 합) × 균사석 × 지역 배율
            double harvFlat = 1 + NF.ed_bonus(Lv("ed_bonus")) + NF.ed_bonus1p(Lv("ed_bonus1p")) + NF.ed_bonus2(Lv("ed_bonus2")) + NF.ed_bonus2p(Lv("ed_bonus2p")) + NF.ed_bonus3(Lv("ed_bonus3")) + NF.ed_bonus3p(Lv("ed_bonus3p"));
            st.harvestMul = harvFlat * (1 + ab["harvest"] + BStat("harvest")) * NF.gm_harvest(Lv("gm_harvest")) * st.zoneMul;
            st.multiP = MultiRate(Lv("ed_multi"));
            st.priceMul = PriceMul(ab);
            st.colMax = NF.ed_size(Lv("ed_size"));
            st.zoom = MAP_ZOOM[Lv("md_map")];
            double area = Math.Pow(1 / (st.zoom * st.zoom), 0.7);
            st.maxCol = Math.Max(4, (int)U.JsRound(st.maxCol * area));
            st.golden = (NF.ed_gold(Lv("ed_gold")) + (SetDone("glow") ? 0.01 : 0) + ab["golden"]) * (wId == "moon" ? 3 : 1);
            st.devTypes = DEVICE_TYPES.Where(d => Lv("dev_" + d) > 0).ToList();
            st.permBalls = st.hvs.Count;
            st.comboWin = COMBO_WINDOW;
            st.meteor = Lv("gm_meteor") > 0 ? NF.gm_meteor(Lv("gm_meteor")) : (NF.Meteor?)null;
            st.wanderP = 0.08;
            st.wanderMul = 1.5 * (SetDone("cordy") ? 1.5 : 1);
            st.poisonMul = SetDone("amanita") ? 1.3 : 1;
            st.jellySlow = SetDone("jelly") ? 0.75 : 0.55;
            st.skillDmg = 1 + (SetDone("herb") ? 0.25 : 0) + ab["skillDmg"];
            st.sporeP = 0.25 * (wId == "wind" ? 2 : 1) * NF.gm_toxin(Lv("gm_toxin"));
            st.debuffDur = 3 * (wId == "wind" ? 2 : 1) * NF.ps_resist(Lv("ps_resist")) * NF.gm_toxin(Lv("gm_toxin"));
            st.goldBonus = 1 + GoldenSetBonus();
            st.harvestMul *= INCOME_MUL;
            st.goldMul = st.goldBonus * SCORE_GOLD * (1 + SettleBonus(ab)) * NF.gm_score(Lv("gm_score")) * NF.inf_gold(Lv("inf_gold"));   // 끝없는 풍년은 점수→골드에만 (사용자 결정 2026-10-13)
            st.giantP = NF.ed_giant(Lv("ed_giant"));
            st.blade = Lv("bl") > 0;
            var (bRange, bSpd, bPow) = BladeLv(Lv("bl"));
            st.bladeR = st.ballR + NF.bl_range(bRange);
            st.bladeInt = NF.bl_spd(bSpd);
            st.bladeDmg = st.atk * NF.bl_pow(bPow);
            st.kidP = KidRate(Lv("md_kid"));
            st.buffMul = NF.md_buffdur(Lv("md_buffdur"));
            st.tempLife = NF.md_templife(Lv("md_templife"));
            st.devMul = NF.md_stump(Lv("md_stump"));
            st.heavyMul = NF.ps_heavy(Lv("ps_heavy"));
            st.rushMul = NF.ps_rush(Lv("ps_rush"));
            st.barSpd = NF.ps_barspd(Lv("ps_barspd"));
            st.barDur = NF.ps_bardur(Lv("ps_bardur"));
            st.barRate = NF.ps_barrate(Lv("ps_barrate"));
            st.powAtk = NF.ps_power(Lv("ps_power")); st.powSkill = NF.ps_power(Lv("ps_power")) * POWER_SKILL / POWER_ATK;
            st.bossMul = NF.gm_boss(Lv("gm_boss")); st.bossGemAdd = NF.gm_bossgem(Lv("gm_bossgem"));
            foreach (var id in SKILL_IDS) { var si = SkillInfoOf(id); si.p *= (1 + ab["skill"]) * NF.gm_skill(Lv("gm_skill")); st.sk[id] = si; }
            st.burstR = NF.burst_r(st.sk["burst"].P) + (SetDone("fart") ? 30 : 0);
            st.festMul = 2 * st.sk["fest"].pm;
            st.shroomHp = st.zoneMul;
            // 보스 체력은 고른 지역이 아니라 그 스테이지의 최신 지역 기준 (예전 지역을 골라 쉽게 잡지 못하게)
            st.boss = boss;
            if (boss != null) st.bossHp = BossHp(boss, LatestTheme().id, st.stage);
            return st;
        }
    }
}
