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
        public BossDef boss; public double bossHp;
        // 균사 트리 A안 (2026-10-19): 슬라이드 바 버프 지속(초, 0 = 없음)·확률 스킬·종류별 스폰·수확
        public double powFlat, springDur, pierceDur, homingDur, healDur, splitDur, feverDur, cloneDur, wisdomP;
        public int cloneMax = 1;
        public double poisonP, burstP, boltP, tornadoP, tornadoDur, moltP, legendP, flexP;
        public double epicMul = 1, uniqueMul = 1;
        public double[] handMul = { 1, 1, 1, 1 };   // 등급별 수확량 배율 (일반·에픽·유니크·레전더리)
        public bool fest;                            // 할아버지의 전설: 마지막 5초 festMul
        public Game.TreeBonus tree = new Game.TreeBonus();   // 세계수 보너스 (harvest: 창고에 넣는 버섯 수 +%)   // 보스 라운드면 보스와 그 체력 (아니면 null)
    }

    // Game rules on the save data (prototype "조회 · 해금 · 비용 · 판매 · 능력치" sections).
    public static partial class Game
    {
        public static SaveData G;
        public static class DBG { public static bool open, infinite, allSeeds, allNodes; public static int speed = 1; }   // allNodes: 개발자 치트 — 업그레이드 노드 선행 조건 무시

        public static void SaveGame() => SaveIO.Save(G);

        // ===== 조회 =====
        public static int Lv(string id) => G.nodes.TryGetValue(id, out var L) ? L : 0;
        // 줄(같은 스킬) 레벨 = 행 레벨의 합. 줄 값 = 마지막으로 산 행의 DB Efx (아무것도 안 샀으면 첫 행의 Efx_Lv0)
        public static int LineLv(string line) { int s = 0; foreach (var n in LINES[line]) s += Lv(n.id); return s; }
        public static double LineV(string line)
        {
            var rows = LINES[line];
            for (int i = rows.Count - 1; i >= 0; i--) { int L = Lv(rows[i].id); if (L > 0) return rows[i].efx[Math.Min(L, rows[i].max)]; }
            return rows[0].efx[0];
        }
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

        // 내부 판 번호 (튜토리얼 T1~T2 포함, 1부터). 화면 글자는 Defs.StageLabel
        public static int StageNow() => G != null ? G.stage : 1;
        // 콘텐츠(표시) 스테이지: 보스·지역·체력 성장은 이것으로 판정한다 (튜토리얼 판은 1, Defs.ContentStage)
        public static int ContentNow() => ContentStage(StageNow());
        // 이번 라운드가 보스 라운드인가: 보스 스테이지에서 보스 도전이 켜져 있을 때
        public static bool BossRoundNext() => G != null && G.bossOn && IsBossStage(ContentNow());
        // 보스 스테이지는 보스를 쓰러뜨려야 넘어가므로, 지나온 보스 스테이지는 모두 쓰러뜨린 것이다 (stage = 콘텐츠 스테이지)
        public static bool BossCleared(int stage) => ContentNow() > stage;
        public static bool ThemeOpen(Theme t) => ContentNow() >= t.from;
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
            if (sp.boss > 0) return Loc.F("스테이지 {0} 보스({1})를 쓰러뜨리면 해금 (현재 스테이지 {2})", sp.boss, BossAt(sp.boss).n, StageLabel(StageNow()));
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
            gem = L < n.gems.Length && n.gems[L] > 0 ? Math.Ceiling(n.gems[L]) : 0,   // 균사석은 COST_MUL을 곱하지 않는다
        };
        public static bool CanAfford(Cost c) => !double.IsInfinity(c.gold) && !double.IsNaN(c.gold) && G.gold + 1e-9 >= c.gold && G.gem + 1e-9 >= c.gem;
        static void Pay(Cost c) { G.gold -= c.gold; G.gem -= c.gem; }

        // 부모 조건: 보통은 1레벨 이상, 단계 노드(II·III·IV)는 부모가 최대 레벨이어야 한다
        public static bool ParentOk(Node n) => DBG.allNodes || (n.parent == null || Lv(n.parent) >= 1) && (n.req == null || Lv(n.req) >= NODE[n.req].max);

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
            BagSettle();   // 세계수 수확률을 바꾸는 노드(gm_herb 등)가 지난 시간에 소급되지 않게
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
        // 흥정 기술·단골 손님(점수 → 골드 전환율)은 더한다
        public static double SettleBonus(Dictionary<string, double> ab) =>
            LineV("ed_haggle") + LineV("ed_regular") + 0.1 /* 예전 식용 코어 보너스, 코어가 없어져 기본으로 */ + ab["price"];
        // 버섯 한 개의 골드 값어치 (엽전 수확기가 쓴다): 등급 기본가 × (1 + 정산 보너스 + 황금 세트)
        public static double PriceMul(Dictionary<string, double> ab) => 1 + SettleBonus(ab) + GoldenSetBonus();

        // ===== 수확기·날씨 =====
        // 장착 칸 = 영구 핀볼 수. 수확기는 종류마다 한 칸씩, 남은 칸은 기본 수확기로 채운다
        public static int HvSlots() => 1 + (int)Math.Round(LineV("ps_tools"));   // 장인의 도구 (균사석)
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
            int L = OFF_NODES.Contains(id) ? 0 : Lv(id);   // 뺀 노드는 옛 세이브에 레벨이 남아 있어도 끈다
            var (S, P) = SkillSP(SKILLS[id], L);
            return new SkillInfo { on = L > 0, S = S, p = SkillRate(S), P = P, pm = SkillPow(P) };
        }

        // ===== 라운드 능력치 =====
        public static RoundStats ComputeStats(string wId, string themeId, BossDef boss = null)
        {
            var st = new RoundStats { wId = wId, weather = WEATHER.TryGetValue(wId, out var w) ? w : WEATHER["clear"], theme = THEME.TryGetValue(themeId, out var th) ? th : THEMES[0], stage = StageNow() };
            var ab = st.ab = StarAbilities();
            var tb = st.tree = RoundBonus();   // 세계수: 가호 · 꼬마 특별 보너스 · 장식 · 아름다움 (보너스끼리 더한다)
            st.hvs = ActiveHvs();
            st.hvStar = st.hvs.Distinct().ToDictionary(id => id, id => G.hv.TryGetValue(id, out var s) ? s : 1);
            // 노드·코어·별 능력치·꼬마 보너스는 더하고, 날씨(라운드마다 바뀌는 조건)만 곱한다
            // 지역 배율: 버섯 체력·점수·수확 개수가 함께 커진다
            int cst = ContentStage(st.stage);   // 콘텐츠 스테이지 (튜토리얼 판 오프셋). st.stage는 내부 판 번호
            st.zoneMul = ZoneMul(st.theme.id, cst);
            // 공격력 = 날 갈기(기본 1 포함) × (1 + 퍼센트 보너스 합)
            double atkFlat = LineV("ps_blade") + (SetDone("poison") ? 3 : 0);
            st.atk = atkFlat * (1 + 0.1 + ab["atk"] + tb.atk);
            double spd = (1 + LineV("ps_wind") + ab["spd"] + tb.spd) * (wId == "storm" ? 0.8 : 1);
            st.spdMul = spd;
            st.launch = TUNE.launch * spd;
            st.minSpd = TUNE.min * spd;
            st.ballR = TUNE.ball0 * BALL_SCALE * (1 + LineV("ps_size") + ab["size"]);
            st.barLen = (150 * (1 + LineV("ps_hug") + 0.1 + ab["bar"] + tb.bar) + (SetDone("shelf") ? 40 : 0)) * (wId == "cold" ? 0.75 : 1);   // 최대 필드 폭의 1/3 (RoundSim.Start)
            // 라운드 시간: 기본 10초 + 노장의 체력 (스테이지로는 늘지 않는다)
            st.duration = ROUND_SEC + LineV("ps_stamina") + ab["dur"] + tb.dur - (wId == "cold" ? 3 : 0);
            st.crit = LineV("ps_crit") + (SetDone("coral") ? 0.05 : 0) + ab["crit"] + tb.crit;   // 100%를 넘으면 극한 치명타 (RoundSim.Damage)
            st.critMul = 1 + LineV("ps_critdmg");   // 기본 150%
            st.comboK = (0.1 + 0.1 * ab["combo"]) * (1 + LineV("ps_combo"));   // [피버] 버프는 따로 곱한다 (RoundSim.ScoreMulFor)
            // 타격 점수 = 등급 기본 점수 × (1 + 퍼센트 보너스 합) × 지역 배율
            st.scoreFlat = 0;
            st.scoreMul = (1 + (SetDone("cook") ? 0.5 : 0) + ab["score"] + LineV("ed_hunt")) * st.zoneMul;
            // 군락지 수 = 기본 3 + 넓은 시야·하늘의 시야·신의 눈. 군락지가 늘면 필드가 넓어진다 (필드 넓히기 노드 없음)
            int cols = COL_BASE + (int)Math.Round(LineV("ed_view") + LineV("ed_sky") + LineV("ed_god")) + (int)Math.Round(tb.colsN);
            st.zoom = ZoomForCols(cols);
            st.maxCol = Math.Max(1, (int)Math.Ceiling(cols * (1 + ab["cols"] + tb.cols) * (wId == "rain" ? 1.2 : 1)));
            st.regen = LineV("ed_regen") / (1 + ab["regen"]) / (wId == "rain" ? 2 : 1) * (wId == "storm" ? 2 : 1);
            // 레전더리는 보스 전용 (필드에 안 나온다). 에픽·유니크는 소문·전설 노드로 따로
            st.rareMul = 1 + ab["rare"];
            st.epicMul = 1 + LineV("ed_epic");
            st.uniqueMul = 1 + LineV("ed_unique");
            st.stageMul = 1 + 0.03 * (cst - 1);
            // 수확 개수 = 1 × (1 + 퍼센트 보너스 합) × 지역 배율. 등급별 손길은 따로 곱한다 (RoundSim.Harvest)
            st.harvestMul = (1 + ab["harvest"]) * st.zoneMul;
            st.handMul = new[] { 1 + LineV("ed_hand1"), 1 + LineV("ed_hand2"), 1 + LineV("ed_hand3"), 1 + LineV("ed_hand4") };
            st.multiP = 0;
            st.priceMul = PriceMul(ab);
            st.colMax = (int)Math.Round(LineV("ed_more"));   // 군락지 둘레 단일 버섯 수 (고정)
            st.golden = (0.01 + LineV("ed_gold") + (SetDone("glow") ? 0.01 : 0) + ab["golden"] + tb.goldenP) * tb.goldenMul * (wId == "moon" ? 3 : 1);
            st.sparkP = tb.spark;   // 연쇄 번개 (꼬마·장식)
            // 환경 복원: 1레벨 그루터기 · 2레벨 꽃밭(moss) · 3레벨 도토리
            int env = (int)Math.Round(LineV("md_env"));
            st.devTypes = DEVICE_TYPES.Take(Math.Max(0, Math.Min(env, DEVICE_TYPES.Length))).ToList();
            st.permBalls = st.hvs.Count;
            st.comboWin = COMBO_WINDOW;
            st.meteor = null;
            st.wanderP = 0.08;
            st.wanderMul = 1.5 * (SetDone("cordy") ? 1.5 : 1);
            st.poisonMul = SetDone("amanita") ? 1.3 : 1;
            st.jellySlow = SetDone("jelly") ? 0.75 : 0.55;
            st.skillDmg = 1 + (SetDone("herb") ? 0.25 : 0) + ab["skillDmg"];
            st.sporeP = 0.25 * (wId == "wind" ? 2 : 1);
            st.debuffDur = 3 * (wId == "wind" ? 2 : 1);
            st.goldBonus = 1 + GoldenSetBonus();
            st.harvestMul *= INCOME_MUL;
            st.goldMul = st.goldBonus * SCORE_GOLD * (1 + SettleBonus(ab) + tb.gold) * WtGoldMul();
            st.giantP = 0;   // 거대 버섯 삭제 (A안)
            // 회전 칼날: 수확기 둘레를 도는 꼬마 수확기 n개 = 칼날이 n배 자주 벤다. 피해 = 공격력 × (10% + 회전 회오리)
            int kids = (int)Math.Round(LineV("ps_kid"));
            st.blade = kids > 0;
            st.bladeR = st.ballR + 45;
            st.bladeInt = kids > 0 ? 0.6 / kids : 0.6;
            st.bladeDmg = st.atk * (KID_STAT + LineV("ps_kidatk"));
            st.kidP = 0;
            st.buffMul = 1; st.tempLife = 1; st.devMul = 1; st.heavyMul = 1; st.rushMul = 1;
            st.barSpd = 1; st.barDur = 0; st.barRate = 0; st.powAtk = 0; st.powSkill = 0;
            st.bossMul = boss != null && GritOn(cst) ? GritMul(G.bossGrit) : 1; st.bossGemAdd = 0;   // 9회차 끈기: 반복 보스만
            // 슬라이드 바 버프 (바에 맞을 때. 분열·분신술은 확률) · 수확기 스킬 확률 · 시작 버프
            st.powFlat = LineV("power");
            st.springDur = LineV("md_spring"); st.pierceDur = LineV("md_pierce"); st.homingDur = LineV("md_homing"); st.healDur = LineV("md_heal");
            st.splitDur = LineV("md_split"); st.feverDur = LineV("md_fever"); st.cloneDur = LineV("ps_clone"); st.wisdomP = LineV("md_wisdom");
            st.cloneMax = Math.Max(1, (int)Math.Round(LineV("ps_mirror")));
            st.poisonP = LineV("md_poison"); st.burstP = LineV("md_burst"); st.boltP = LineV("md_bolt");
            st.tornadoP = LineV("md_tornado"); st.tornadoDur = 2 + LineLv("md_tornado");   // 3·4·5초
            st.moltP = LineV("md_molt"); st.legendP = LineV("md_grand"); st.flexP = LineV("ed_flex");
            st.fest = LineV("ed_legend") > 0;
            st.festMul = 1 + LineV("ed_legend");
            st.shroomHp = st.zoneMul * EarlyHpMul(st.stage);   // 초반 판은 버섯 체력을 낮춘다 (Defs.EarlyHpMul, 내부 판 번호)
            if (SIM_HP1) st.shroomHp = 1;   // 시뮬 전용 (기본 꺼짐)
            // 보스 체력은 고른 지역이 아니라 그 스테이지의 최신 지역 기준 (예전 지역을 골라 쉽게 잡지 못하게)
            st.boss = boss;
            if (boss != null) st.bossHp = BossHp(boss, LatestTheme().id, cst);
            return st;
        }
    }
}
