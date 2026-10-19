using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush.EditorTools
{
    // 밸런스 측정용 헤드리스 봇. RoundSim을 에디터에서 직접 돌려 라운드 수입을 잰다.
    // 실제 세이브(PlayerPrefs)와 Game.G는 실행이 끝나면 원래대로 되돌린다.
    public static partial class BalanceSim
    {
        public class RoundResult
        {
            public int stage; public string weather;
            public double score, scoreGold, bonus, value, duration;
            public int harvests, maxCombo, cols, balls, spawned;
            public int[] madeT, killT;   // 티어별 생긴 수·수확한 수
            public string zone; public double atk;
            public string boss; public bool bossCleared; public double bossHpLeft;   // 보스 라운드 결과 (남은 체력 비율)
            public double net;           // 실제로 받은 골드 = 점수골드 + 보너스 − 보스 패배 감액 + 보스 처치 보상
            public double gateT = -1;    // 보스 관문 모드: 예상 처치 시간(초). -1 = 관문 안 씀
        }

        // ===== 보스 관문 (2회차 사용자 결정: 봇 조준 대신 사람 기준 처치 시간으로 판정) =====
        // theory 공식 (이론.md 2회차, 현황/theory.md 14:43 · verify E1~E7 반영):
        //   A = 공격력 × 무더기 파쇄 × 보스 피해 배율 × 치명타 기댓값,  H = H0 × 속도 배율 × (1 + KB(영구 수확기 − 1)) × (움직이는 버섯 MOVER_H) × (임시 수확기 원천이 있으면 1 + TAU, 죽음의 천사 제외)
        //   처치 시간 t = HP / (A·H)  (청소부: HP / (A·H − 회복률·HP), 회복이 이기면 불가 · 환상: 분신 몫 ×PHANTOM_K)
        //   첫 발사·첫 타격까지 T0초 → 관문 예상 시각 = T0 + t.  처치 = T0 + t ≤ 판 시간
        // 끄면(BOSS_GATE = false) 예전처럼 봇이 실제로 보스를 맞힌다. 배치: -boss gate (-gateH 로 H0)
        public static bool BOSS_GATE = false;
        public static double GATE_H = 0.9, GATE_KB = 0.5, GATE_MOVER_H = 0.6, GATE_TAU = 0.2, GATE_T0 = 2, GATE_PHANTOM_K = 1.99;
        public static Func<RoundSim, double> BossGateTime = R =>
        {
            var st = R.st; var b = st.boss;
            double hp = st.bossHp;
            double A = st.atk * st.heavyMul * st.bossMul * (1 + Math.Min(1, st.crit) * (st.critMul - 1));
            bool temp = st.kidP > 0 || st.sk["split"].on || st.sk["clone"].on || Lv("barclone") > 0;
            double H = GATE_H * st.spdMul * (1 + GATE_KB * (st.permBalls - 1)) * (b.id == "mover" ? GATE_MOVER_H : 1) * (b.id != "angel" && temp ? 1 + GATE_TAU : 1);
            double dps = Math.Max(1e-9, A * H);
            double t;
            if (b.id == "cleaner")
            {
                double heal = CLEANER_HEAL / CLEANER_INT * hp;
                t = dps <= heal ? 1e9 : hp / (dps - heal);
            }
            else t = (b.id == "phantom" ? GATE_PHANTOM_K : 1) * hp / dps;
            return GATE_T0 + t;
        };

        // MCP 호출이 시간 초과로 끊겨도 결과가 남도록 줄마다 파일에도 쓴다 (프로젝트 Temp 폴더)
        public static readonly string LogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/balance.log"));
        static void Log(StringBuilder sb, string line) { sb.AppendLine(line); File.AppendAllText(LogPath, line + "\n"); }

        // 저장을 건드리는 코드는 전부 이 안에서 돌린다
        public static T Sandbox<T>(Func<T> f)
        {
            bool had = PlayerPrefs.HasKey(SaveIO.SAVE_KEY);
            string orig = PlayerPrefs.GetString(SaveIO.SAVE_KEY, "");
            var prevG = G;
            var prevR = RoundSim.R;
            bool prevSuspended = SaveIO.Suspended;
            SaveIO.Suspended = true;   // 봇의 SaveGame()이 실제 세이브 키에 쓰지 않게 (중간에 끊겨도 개발 세이브가 남음)
            try { return f(); }
            finally
            {
                SaveIO.Suspended = prevSuspended;
                if (had) PlayerPrefs.SetString(SaveIO.SAVE_KEY, orig); else PlayerPrefs.DeleteKey(SaveIO.SAVE_KEY);
                PlayerPrefs.Save();
                G = prevG; RoundSim.R = prevR;
            }
        }

        // 봇 한 판: 위로 발사하고, 바는 가장 낮게 내려오는 영구 핀볼을 오차를 섞어 따라간다
        // 봇의 지역 선택: 열린 지역 중 일반 버섯(체력 1 × 지역 배율)을 공격력으로 반 이상 깎을 수 있는 가장 높은 곳
        public static string PickZone()
        {
            var st = ComputeStats("clear", THEMES[0].id);
            string best = THEMES[0].id;
            foreach (var t in THEMES)
                if (ThemeOpen(t) && st.atk >= 0.5 * TIERS[0].hp * ZoneMul(t.id, StageNow())) best = t.id;
            return best;
        }

        public static RoundResult PlayRound(System.Random rnd, float skill = SKILL, bool autoZone = true)
        {
            if (autoZone) G.theme = PickZone();
            else if (!THEME.TryGetValue(G.theme ?? "", out var th) || !ThemeOpen(th)) G.theme = LatestTheme().id;
            var boss = BossRoundNext() ? BossAt(StageNow()) : null;
            string w = RollWeather(boss != null);
            var R = RoundSim.Start(w, G.theme, boss);
            var res = new RoundResult { stage = StageNow(), weather = w, duration = R.st.duration, cols = R.st.maxCol, zone = G.theme, atk = R.st.atk, balls = R.st.permBalls, boss = boss?.id };
            if (boss != null && BOSS_GATE) res.gateT = BossGateTime(R);
            bool gateHit = false;
            // 보스 라운드는 사람처럼 보스를 겨눠 쏜다
            if (boss != null) { var bm = R.BossMain; R.Launch(Mathf.Atan2(bm.y - RoundSim.LAUNCH.y, bm.x - RoundSim.LAUNCH.x) + (float)(rnd.NextDouble() * 0.2 - 0.1)); }
            else R.Launch(Mathf.Atan2(-1f, (float)(rnd.NextDouble() * 0.8 - 0.4)));
            float h = 1 / 30f, sim = 0, noise = 0, nt = 0;   // 측정 속도를 위해 30fps (충돌은 속도에 맞춰 잘게 나눠 계산된다)
            while (!(R.phase == "end" && R.endT <= 0) && sim < 300)
            {
                nt -= h;
                if (nt <= 0) { noise = (float)(rnd.NextDouble() * 2 - 1) * skill * R.barLen; nt = 0.5f; }
                Ball tgt = null;
                foreach (var b in R.balls) if (b.perm && !b.dead && b.dy > 0 && (tgt == null || b.y > tgt.y)) tgt = b;
                float tx = tgt != null ? tgt.x + noise : RoundSim.WW / 2;
                tx = Mathf.Clamp(tx, RoundSim.FX0 + R.barLen / 2, RoundSim.FX1 - R.barLen / 2);
                R.barX = Mathf.Lerp(R.barX, tx, 1 - Mathf.Exp(-18 * h));
                R.SimFrame(h); sim += h;
                // 관문: 예상 처치 시각이 판 안에 오면 그때 처치로 끝낸다 (게임도 처치하면 판을 바로 끝낸다)
                if (res.gateT >= 0 && !gateHit && R.phase == "run" && R.runT >= res.gateT) { gateHit = true; R.bossCleared = true; R.phase = "end"; R.endT = 1.5f; }
            }
            // 관문 판정이 기준: 예상 처치 시간이 판 제한 시간 안이면 처치. 봇이 그보다 먼저 실제로 쓰러뜨려 판이 끝난 경우도 같은 기준으로 본다
            if (res.gateT >= 0) R.bossCleared = gateHit || res.gateT <= R.timeMax;
            res.maxCombo = R.maxCombo;
            if (boss != null)
            {
                res.bossCleared = R.bossCleared;
                double left = 0, max = 0; foreach (var b in R.bosses) { left += Math.Max(0, b.hp); max += b.maxHp; }
                res.bossHpLeft = max > 0 ? left / max : 0;
            }
            var s = R.Finish();
            res.net = s.net;
            res.score = s.score; res.scoreGold = s.scoreGold; res.bonus = s.bonusGold; res.value = s.value; res.harvests = s.harvests; res.spawned = R.shroomsMade; res.madeT = (int[])R.madeT.Clone(); res.killT = (int[])R.killT.Clone();
            return res;
        }

        // 가격을 맞추고 단계 진행을 세는 노드: 골드로 사는 일반 노드 (균사석 노드·싱크 노드 빼고)
        static bool Priced(Node n) => !n.gem && !n.inf;
        public static int GoldLevels(int tier = 0) => NODES.Where(n => Priced(n) && (tier == 0 || n.tier == tier)).Sum(n => Lv(n.id));
        public static int GoldLevelsMax(int tier = 0) => NODES.Where(n => Priced(n) && (tier == 0 || n.tier == tier)).Sum(n => n.max);
        public static int InfLevels() => NODES.Where(n => n.inf).Sum(n => Lv(n.id));

        // 단계별 기록: 처음 산 판 · 다 산 판 · 그때의 판당 수입. 이어 돌려도 남도록 파일에 둔다
        public class PhaseLog
        {
            public int[] first = new int[4], done = new int[4];
            public double[] firstMin = new double[4], doneMin = new double[4], firstInc = new double[4], doneInc = new double[4];
            public int buyRounds, rounds;
            public List<double> inc = new List<double>();   // 스테이지별 번 골드 (인덱스 = 스테이지 - 1)
            public List<int> buys = new List<int>();        // 스테이지별 산 레벨 수
            public List<double> val = new List<double>();   // 스테이지별 수확한 버섯 가치(팔면 받을 골드)
            public List<double> kill = new List<double>();  // 스테이지별 처치율 = 수확한 버섯 ÷ 그 판에 생긴 버섯
            public List<string> zone = new List<string>();  // 스테이지별 지역
            public List<int[]> madeT = new List<int[]>(), killT = new List<int[]>();   // 스테이지별 티어별 생긴 수·수확한 수 (처치율 P13)
            // 판 단위 기록 (2회차): 보스 재도전으로 같은 스테이지를 여러 판 돌아도 판마다 한 칸
            public List<double> incR = new List<double>();   // 판 수입 (정산 net: 보스 보상·감액 포함)
            public List<int> stageR = new List<int>();       // 그 판의 스테이지
            public List<int> bossR = new List<int>();        // 0 일반 판 · 1 보스 처치 · -1 보스 패배 · 2 보스 스테이지 파밍(보스 끔)
            public List<double> playR = new List<double>();  // 판이 끝난 때 누적 플레이 시간(초, G.play)
            public List<double> atkR = new List<double>();   // 그 판 공격력
            public List<int> buysR = new List<int>();        // 그 판 뒤 산 레벨 수
            public List<double> gateR = new List<double>();  // 보스 관문 예상 처치 시간(초), 관문 안 쓰면 -1
            public int[] doneR = new int[4];                 // 단계별 다 산 판 (판 수, 스테이지가 아님)
            public double maxBuyGold;                        // 지금까지 강화를 살 때 가진 골드의 최댓값 (체크포인트 재사용 판정)
            public void Set(int stage, double income, int bought, double value = 0, double killRate = 0, string zoneId = "")
            {
                while (inc.Count < stage) { inc.Add(0); buys.Add(0); }
                while (val.Count < stage) val.Add(0);
                while (kill.Count < stage) kill.Add(0);
                while (zone.Count < stage) zone.Add("");
                inc[stage - 1] += income; buys[stage - 1] += bought;   // 보스에게 지면 같은 스테이지를 여러 판 돈다 → 더한다
                val[stage - 1] = value; kill[stage - 1] = killRate; zone[stage - 1] = zoneId;
            }
            public void SetTiers(int stage, int[] made, int[] killed)
            {
                while (madeT.Count < stage) { madeT.Add(new int[0]); killT.Add(new int[0]); }
                madeT[stage - 1] = made ?? new int[0]; killT[stage - 1] = killed ?? new int[0];
            }
        }
        static string PhasePath(int seed = 1) => Path.GetFullPath(Path.Combine(Application.dataPath, $"../Temp/balance_phase_s{seed}.json"));

        static string StatePath(int seed = 1) => Path.GetFullPath(Path.Combine(Application.dataPath, $"../Temp/balance_state_s{seed}.json"));

        // 새 게임(resume = false) 또는 이전 진행 상태에서 n판. 매 판 뒤 일괄 강화(가장 싼 것부터)로 전부 산다.
        // 한 판에 라운드 외에 드는 시간(조준·정산·트리 화면) 추정치. 플레이 시간 환산에만 쓴다
        // 보수적 기준: 평균 이하 플레이어. 바 조작 오차 ±60%, 한 판 화면 전환 30초
        public const double OVERHEAD_SEC = 30;
        public const float SKILL = 0.6f;

        // every: 몇 판마다 한 줄씩 남길지. 트리를 다 산 판은 항상 남긴다
        static int botFarm; static bool botBoughtSinceFail;
        // 보스 처치에 쓰이는 노드 레벨 합 (관문 모드 재도전 규칙: 이것이 올라야 다시 도전)
        static readonly string[] ATK_KEYS = { "ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4", "inf_atk", "ps_crit", "ps_crit2", "ps_heavy", "gm_atk", "gm_boss", "ps_spd", "ps_ball", "ps_ball2", "ps_ball3", "ps_dur", "ps_dur2" };
        static int AtkLevels() => ATK_KEYS.Where(NODE.ContainsKey).Sum(Lv);

        // 마지막 Progression 호출에서 실제로 돈 판 수 (BalanceJob이 시간 예산만큼 묶어 부를 때 남은 판을 센다)
        public static int LastPlayed;
        public static bool LastHitStop;   // 마지막 호출이 멈출 스테이지(stopStage)에 닿아 끝났는가

        public static string Progression(int rounds, bool resume = false, int seed = 1, float skill = SKILL, double maxWallSec = 20, int every = 1, int stopStage = 0)
        {
            LoadPriceTable();
            LastPlayed = 0; LastHitStop = false;
            return Sandbox(() =>
            {
                G = resume && File.Exists(StatePath(seed)) ? JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(StatePath(seed))) : new SaveData();
                var ph = resume && File.Exists(PhasePath(seed)) ? JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(seed))) : new PhaseLog();
                var sb = new StringBuilder();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                if (!resume) { botFarm = 0; botBoughtSinceFail = false; }
                // 상태·기록 파일은 판마다가 아니라 호출이 끝날 때 한 번 쓴다
                if (!resume) Log(sb, $"조건: 시드 {seed} · 바 오차 {skill} · 보스 {(BOSS_GATE ? $"관문 H0={GATE_H} KB={GATE_KB} MOVER_H={GATE_MOVER_H} TAU={GATE_TAU} T0={GATE_T0} 환상×{GATE_PHANTOM_K}" : "실제 판(봇 조준)")} · 프리셋 {TUNE.id} · DB {Db.Data._source}");
                if (!resume) Log(sb, "판 | 누적 | 지역 | 시간 | 핀볼 | 공격력 | 점수골드 | 수확 | 보유골드 | 산 레벨 | 초반/중반/후반");
                for (int i = 0; i < rounds; i++)
                {
                    if (stopStage > 0 && StageNow() > stopStage) { LastHitStop = true; break; }
                    if (sw.Elapsed.TotalSeconds > maxWallSec) break;
                    var rnd = new System.Random(seed * 1000 + G.rounds); UnityEngine.Random.InitState(seed * 1000 + G.rounds);
                    // 봇의 보스 재도전: 지고 나면 파밍하다가, 무언가 사고 나서 또는 3판마다 다시 켠다
                    // 관문 모드는 결정적이라(공격력이 그대로면 또 진다) 공격 관련 노드를 산 다음 판 또는 5판마다 (theory 재도전 규칙)
                    if (IsBossStage(StageNow()) && !G.bossOn && (botBoughtSinceFail || ++botFarm >= (BOSS_GATE ? 5 : 3))) { G.bossOn = true; botFarm = 0; botBoughtSinceFail = false; }
                    var r = PlayRound(rnd, skill, !GAME_ZONE);
                    // 버섯 판매·마을 의뢰는 v3에서 없어졌다. 판 수입은 정산 골드뿐이다 (SELL 설정은 이제 효과 없음)
                    double sold = 0;
                    int before = GoldLevels(), atkBefore = AtkLevels();
                    if (G.gold > ph.maxBuyGold) ph.maxBuyGold = G.gold;   // 체크포인트 재사용 판정용
                    BulkBuy(new[] { "ed", "md", "ps" }, 0, false);
                    // 추정 플레이 시간(초). 관문 처치 판은 처치 시각 + 끝 연출 1.5초에 끝난다
                    G.play += (r.gateT >= 0 && r.bossCleared ? Math.Min(r.duration, r.gateT + 1.5) : r.duration) + OVERHEAD_SEC;
                    int bought = GoldLevels() - before;
                    if (BOSS_GATE ? AtkLevels() > atkBefore : bought > 0) botBoughtSinceFail = true;
                    if (r.boss != null && !r.bossCleared) botBoughtSinceFail = false;   // 패배 뒤에 산 것만 센다
                    if (r.boss != null) Log(sb,$"  보스 R{r.stage} {BOSS[r.boss].nKo}: {(r.bossCleared ? "처치" : r.gateT >= 0 ? "실패" : $"실패 (남은 체력 {r.bossHpLeft:P0})")}{(r.gateT >= 0 ? $" · 관문 예상 {r.gateT:F1}s / 판 {r.duration:F0}s" : "")}");
                    ph.rounds++; if (bought > 0) ph.buyRounds++;
                    // 스테이지 수입도 정산 net(보스 처치 보상·패배 감액 포함). 2회차부터 — 1회차 기록(scoreGold + bonus)과 비교할 때 주의
                    ph.Set(r.stage, r.net + sold, bought, r.value, r.spawned > 0 ? (double)r.harvests / r.spawned : 0, r.zone);
                    ph.incR.Add(r.net); ph.stageR.Add(r.stage); ph.playR.Add(G.play); ph.atkR.Add(r.atk); ph.buysR.Add(bought); ph.gateR.Add(r.gateT);
                    ph.bossR.Add(r.boss != null ? (r.bossCleared ? 1 : -1) : IsBossStage(r.stage) ? 2 : 0);
                    ph.SetTiers(r.stage, r.madeT, r.killT);
                    string marks = "";
                    for (int t = 1; t <= 3; t++)
                    {
                        if (ph.first[t] == 0 && GoldLevels(t) > 0) { ph.first[t] = r.stage; ph.firstMin[t] = G.play / 60; ph.firstInc[t] = r.scoreGold; marks += $" ← {TIER_NAMES[t]} 시작"; }
                        if (ph.done[t] == 0 && GoldLevels(t) == GoldLevelsMax(t)) { ph.done[t] = r.stage; ph.doneR[t] = ph.rounds; ph.doneMin[t] = G.play / 60; ph.doneInc[t] = r.scoreGold; marks += $" ← {TIER_NAMES[t]} 완료"; }
                    }
                    if (r.stage % every == 0 || r.stage <= 10 || marks != "")
                        Log(sb, $"R{r.stage} | {G.play / 60:F0}분 | {THEME[r.zone].nKo} | {r.duration:F0}s | {r.balls} | {U.Fmt(r.atk)} | {U.Fmt(r.scoreGold)} | {r.harvests} | {U.Fmt(G.gold)} | +{bought} | {GoldLevels(1)}/{GoldLevelsMax(1)} {GoldLevels(2)}/{GoldLevelsMax(2)} {GoldLevels(3)}/{GoldLevelsMax(3)} | 도감 {CodexCount()} 별 {SPECIES.Sum(sp => StarOf(sp.id))} 최고등급 {SPECIES.Where(IsUnlockedSp).Max(sp => sp.t)} 황금 {GoldenCount()}{marks}");
                    LastPlayed++;
                    if (CkptEvery > 0 && G.rounds % CkptEvery == 0) SaveCkpt(seed, ph);
                }
                // 예외가 나면 반쯤 돈 판을 남기지 않도록 끝까지 돈 경우에만 쓴다 (작업은 Tick이 예외를 잡고 멈춘다)
                File.WriteAllText(StatePath(seed), JsonConvert.SerializeObject(G));
                File.WriteAllText(PhasePath(seed), JsonConvert.SerializeObject(ph));
                Log(sb, $"(무언가 산 판 {ph.buyRounds}/{ph.rounds} · 이번 호출 {LastPlayed}판 {sw.Elapsed.TotalSeconds:F1}s)");
                return sb.ToString();
            });
        }


        // ===== 가격 맞추기 =====
        // 지역 하나(그 지역에서 열리는 노드)의 레벨별 가격을, 직전 시뮬레이션에서 그 지역 기간에 번 골드에 맞춰 다시 매긴다.
        // 사는 순서는 엑셀 DB 가격 순서(OrderedLevels)를 그대로 따르고, 지역 기간의 finish 비율 안에서 고르게 사도록 나눈다.
        static readonly string PricePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/price_table.json"));

        public static void LoadPriceTable()
        {
            if (!File.Exists(PricePath)) return;
            var t = JsonConvert.DeserializeObject<Dictionary<string, double[]>>(File.ReadAllText(PricePath));
            foreach (var n in NODES) if (t.TryGetValue(n.id, out var c) && c.Length == n.max) n.costs = c;
        }

        static void SavePriceTable()
        {
            var t = NODES.Where(n => n.costs != null).ToDictionary(n => n.id, n => n.costs);
            File.WriteAllText(PricePath, JsonConvert.SerializeObject(t));
        }

        // 가격표 복사본 (골드 노드). 너무 싼 가격표와 너무 비싼 가격표 사이를 이분할 때 쓴다
        public static Dictionary<string, double[]> SnapshotPrices()
        {
            LoadPriceTable();
            return NODES.Where(Priced).ToDictionary(n => n.id, n => (double[])n.costs.Clone());
        }

        // 두 가격표의 레벨별 기하평균을 새 가격표로 쓴다. 두 쪽이 노드 안 배율·효율 넘김 조건을 지키면 평균도 지킨다
        public static void BlendPrices(Dictionary<string, double[]> lo, Dictionary<string, double[]> hi)
        {
            foreach (var kv in lo)
            {
                if (!hi.TryGetValue(kv.Key, out var h) || h.Length != kv.Value.Length) continue;
                NODE[kv.Key].costs = kv.Value.Select((v, i) => Nice(Math.Sqrt(Math.Max(1, v) * Math.Max(1, h[i])))).ToArray();
            }
            SavePriceTable();
        }

        static double Nice(double v)
        {
            if (v < 10) return Math.Max(1, Math.Round(v));
            double e = Math.Pow(10, Math.Floor(Math.Log10(v)) - 1);
            return Math.Round(v / e) * e;   // 유효숫자 2자리
        }

        // 산 순서대로 늘어놓은 가격에서 바로 앞 가격 대비 최대 배율. 레퍼런스(Bills Must Be Paid)처럼 가격이 한 번에 크게 뛰지 않게 한다.
        // 평균 배율은 약 1.1(약 250레벨로 수 골드 → 조 단위)이라, 지역이 바뀌어 수입이 10배 뛰는 곳에서만 걸린다.
        public const double PRICE_STEP_MAX = 1.4;

        // 밸런스 기준 프리셋 (Defs.PRESETS). 새 플레이어 기본값인 tuned로 맞춘다 (2026-10-08 사용자 결정, 그전 시뮬은 에디터에 남은 spec으로 돌았음)
        public const string PRESET = "tuned";
        // 판매·의뢰 수입을 넣을지 (사용자 결정 2026-10-08: 의뢰 + 전부 판매 가정)
        public static bool SELL = true;
        // 지역 이동: 게임처럼 해금 판(R20·45·75)에 자동으로 새 지역으로 옮기고 그대로 머문다(RoundSim.Finish).
        // false면 예전 봇 규칙(PickZone: 공격력이 될 때까지 옛 지역) — 2026-10-08 실제 플레이에서 해금 직후 못 잡는 버섯이 많다는 사용자 관찰로 게임 규칙으로 바꿈. 명령줄 -pickzone
        public static bool GAME_ZONE = true;

        // 단계 노드 줄과 단계별 효과 크기. 다음 단계 첫 레벨의 "효과 +1당 골드"를 앞 단계 마지막 레벨의 CROSS배 이하로 둔다.
        // 앞 단계를 다 올렸을 때 다음 단계가 확실히 더 효율적이어야 효율 구간이 단계적으로 넘어간다(레퍼런스의 "효율 구간").
        // cross: 그 줄의 효율 넘김 기준. 공격력 줄은 2026-10-13 단계적 성장(레벨당 1·2.5·6·15, theory R2-12)으로 바뀌어 단계 사이가 ×2.4~2.5뿐이라 0.66이면 % 기준 효율 비가 ×1.5에 못 미친다 → verify R2-12 판정의 0.53
        public static readonly (string[] ids, double[] per, double cross)[] TIER_LINES =
        {
            (new[] { "ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4" }, new[] { 1.0, 2.5, 2.5, 6, 6, 15 }, 0.53),
            (new[] { "ed_score", "ed_score1p", "ed_score2", "ed_score2p", "ed_score3", "ed_score3p" }, new[] { 1.0, 1, 10, 10, 100, 100 }, CROSS),
            (new[] { "ed_bonus", "ed_bonus1p", "ed_bonus2", "ed_bonus2p", "ed_bonus3", "ed_bonus3p" }, new[] { 1.0, 1, 10, 10, 100, 100 }, CROSS),
        };
        // 줄의 첫 경계(I → I+, 공격력은 I → II)는 줄 규칙에서 뺀다: 1단계 가격은 밸런스 3회차 사다리(DB)로 고정했고, I의 끝이 싸서 다음 첫 레벨이 ×4를 넘는다 (verify#12 안 a).
        // 공격력 I은 4회차 F안(2026-10-19, theory R4-2)으로 싸게 쪼개 효율 넘김(cross)을 못 지키므로 I → II 경계도 뺀다
        public static bool FreeSibling(double[] per, int t) => t == 1;
        public const double CROSS = 0.66;   // 조사 원칙 P7(다음 단계 효율 ≥ ×1.5) + 반올림 여유. 0.7(×1.43)은 I→II 세 곳이 ×1.5에 못 미쳤다(검증 2026-10-08)
        // 같은 노드 안에서 다음 레벨은 앞 레벨의 NODE_STEP_MAX배를 넘지 않는다 (한 노드 가격이 한 번에 크게 뛰지 않게)
        public const double NODE_STEP_MAX = 4;
        // 노드 안 배율·효율 넘김을 가격에 강제할지. 강제하면 값이 진동했다(2026-10-08): 효과가 단계마다 ×10인데
        // 수입은 단계 사이에 ×1000 넘게 커져서, 줄(타격 점수·수확량은 9레벨)이 게임 전체에 걸치면서 둘 다 지킬 수 없다.
        // 지금은 보고만 한다(EfficiencyReport).
        // 2026-10-08: 형제 노드(I+ · II+ · III+)로 줄을 늘려 다시 켠다
        public const bool ENFORCE_TIER_RULES = true;

        // 기본 골드 노드 전부(지역 잠금 없음)의 레벨별 가격을, 직전 시뮬레이션의 R a~b 수입에 맞춰 다시 매긴다.
        // 사는 순서는 엑셀 DB 가격 순서(OrderedLevels)를 따르고, 기간의 finish 비율 안에서 고르게 사도록 나눈다.
        // 단계(초반·중반·후반)는 잠금이 아니라 이 순서와 가격으로만 나뉜다. 다음 단계 노드(+10)가 앞 단계 마지막 레벨(+1)보다
        // 골드당 효과가 좋아지는 지점에서 효율 구간이 자연스럽게 넘어간다(EfficiencyReport로 확인).
        public static string TunePrices(int a, int b, double finish = 0.9, double damp = 0.5, int seeds = 1)
        {
            LoadPriceTable();
            // 시드별 기록에서 판마다 가장 낮은 수입을 쓴다 (보수적: 가장 느린 플레이어도 고르게 사게).
            var logs = Enumerable.Range(1, seeds).Select(sd => JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(sd)))).ToList();
            int L = b - a + 1;
            double IncAt(int st) => logs.Min(l => st - 1 < l.inc.Count ? l.inc[st - 1] : 0);
            // 기간의 누적 수입 (3판 이동 평균으로 흔들림을 줄인다)
            var raw = Enumerable.Range(a, L).Select(IncAt).ToArray();
            var inc = raw.Select((v, i) => raw.Skip(Math.Max(0, i - 1)).Take(i == 0 ? 2 : 3).Average()).ToArray();
            var cum = new double[L + 1];
            for (int i = 0; i < L; i++) cum[i + 1] = cum[i] + inc[i];
            double C(double x) { x = Math.Max(0, Math.Min(L, x)); int i = (int)Math.Floor(x); return i >= L ? cum[L] : cum[i] + (cum[i + 1] - cum[i]) * (x - i); }

            var levels = OrderedLevels();
            int N = levels.Count;
            var target = new double[N];
            double prev = 0;
            for (int k = 0; k < N; k++)
            {
                double x0 = L * finish * k / N, x1 = L * finish * (k + 1) / N;
                target[k] = prev = Math.Max(prev, C(x1) - C(x0));
            }
            // 가격이 바뀌면 수입도 바뀌어 값이 진동하므로, 이전 가격과의 로그 평균으로 일부만 옮긴다
            var p = new double[N];
            for (int k = 0; k < N; k++)
            {
                double old = NodeCost(levels[k].n, levels[k].lv).gold;
                p[k] = Math.Exp(damp * Math.Log(Math.Max(1, target[k])) + (1 - damp) * Math.Log(Math.Max(1, old)));
            }
            // 매끄럽게: 산 순서대로 오르기만 하고, 바로 앞보다 PRICE_STEP_MAX배를 넘지 않는다
            for (int k = 1; k < N; k++) p[k] = Math.Min(Math.Max(p[k], p[k - 1]), p[k - 1] * PRICE_STEP_MAX);
            var price = new Dictionary<string, double[]>();
            for (int k = 0; k < N; k++)
            {
                var (n, lv) = levels[k];
                if (!price.TryGetValue(n.id, out var arr)) price[n.id] = arr = new double[n.max];
                arr[lv] = Nice(p[k]);
            }
            foreach (var kv in price) NODE[kv.Key].costs = kv.Value;
            SavePriceTable();
            EnforceShape();

            double buyRounds = logs.Average(l => Enumerable.Range(a, L).Count(st => st - 1 < l.buys.Count && l.buys[st - 1] > 0));
            return $"R{a}~R{b}: 직전 실행에서 산 판 {buyRounds:F1}/{L} · 기간 수입 {U.Fmt(cum[L])} · 레벨 {N}개 · 새 가격 {U.Fmt(p[0])} ~ {U.Fmt(p[N - 1])} (합 {U.Fmt(p.Sum())})";
        }

        // 판 단위 가격 맞추기 (2회차, 보스 관문 모드): 보스 재도전으로 스테이지와 판이 어긋나므로 판별 수입(incR)을 쓴다.
        // ends[t] = 단계 t(초반·중반·후반)의 마지막 레벨을 사는 판 (theory R2: 20 · 88 · 172). 단계 t의 레벨은 (ends[t-1], ends[t]] 판 수입에 고르게 나눈다
        public static string TunePricesTiers(int[] ends, double damp = 0.5, int seeds = 1)
        {
            LoadPriceTable();
            var logs = Enumerable.Range(1, seeds).Select(sd => JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(sd)))).ToList();
            int L = ends[3];
            // 판까지 누적 수입의 시드별 최솟값 (가장 느린 플레이어). 판별 수입의 최솟값을 쓰면 시드마다 다른 판에 오는 보스 처치·패배가 골라 빠진다
            var cum = new double[L + 1];
            foreach (var l in logs)
            {
                double s = 0;
                for (int i = 1; i <= L; i++) { s += i - 1 < l.incR.Count ? l.incR[i - 1] : 0; cum[i] = l == logs[0] ? s : Math.Min(cum[i], s); }
            }
            for (int i = 1; i <= L; i++) cum[i] = Math.Max(cum[i], cum[i - 1]);
            double C(double x) { x = Math.Max(0, Math.Min(L, x)); int i = (int)Math.Floor(x); return i >= L ? cum[L] : cum[i] + (cum[i + 1] - cum[i]) * (x - i); }

            var levels = new List<(Node n, int lv)>();
            var target = new List<double>();
            double prev = 0;
            for (int t = 1; t <= 3; t++)
            {
                var lv = OrderedLevels().Where(x => x.n.tier == t).ToList();
                double x0 = t == 1 ? 0 : ends[t - 1], x1 = ends[t];
                for (int k = 0; k < lv.Count; k++)
                {
                    levels.Add(lv[k]);
                    target.Add(prev = Math.Max(prev, C(x0 + (x1 - x0) * (k + 1) / lv.Count) - C(x0 + (x1 - x0) * k / lv.Count)));
                }
            }
            int N = levels.Count;
            var p = new double[N];
            for (int k = 0; k < N; k++)
                p[k] = Math.Exp(damp * Math.Log(Math.Max(1, target[k])) + (1 - damp) * Math.Log(Math.Max(1, NodeCost(levels[k].n, levels[k].lv).gold)));
            for (int k = 1; k < N; k++) p[k] = Math.Min(Math.Max(p[k], p[k - 1]), p[k - 1] * PRICE_STEP_MAX);
            for (int k = 0; k < N; k++) levels[k].n.costs[levels[k].lv] = Nice(p[k]);
            SavePriceTable();
            EnforceShape();
            return $"판 R1~R{L} (단계 끝 {ends[1]}·{ends[2]}·{ends[3]}): 기간 수입 {U.Fmt(cum[L])} · 레벨 {N}개 · 새 가격 {U.Fmt(p[0])} ~ {U.Fmt(p[N - 1])}";
        }

        // 단계별 이분(2회차 verify 제안 a·e): DB 가격(DbOrder)에 단계 t 배율 m[t]를 곱한 가격표를 놓고 EnforceShape까지 적용한다.
        // 단계 안 모양은 DB 가격을 그대로 따르므로 PRICE_STEP_MAX 체인이 단계 경계를 넘어 다음 단계를 누르지 않는다
        public static void ApplyTierMuls(double[] m)
        {
            foreach (var n in NODES.Where(Priced))
                n.costs = DbOrder[n.id].Select(v => Nice(Math.Max(1, v * (n.tier >= 1 && n.tier <= 3 ? m[n.tier] : 1)))).ToArray();
            SavePriceTable();
            EnforceShape();
        }

        // 단계 t를 다 산 판의 시드 중앙값과 가장 느린 시드 (못 끝냈으면 NOT_DONE)
        public static (int med, int slow) TierDone(int t, int seeds)
        {
            var d = Enumerable.Range(1, seeds).Select(sd => JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(sd))).doneR[t])
                .Select(r => r > 0 ? r : NOT_DONE).OrderBy(r => r).ToList();
            return (d[(d.Count - 1) / 2], d[d.Count - 1]);
        }

        // 판 단위 수렴 판정: (가장 느린 시드에서 무언가 산 판 비율, 끝 구매율 최솟값, 단계별 다 산 판의 [가장 빠른 시드, 가장 느린 시드])
        // 못 끝냈으면 가장 빠른 쪽은 b + 1, 가장 느린 쪽은 NOT_DONE (b + 1은 목표 × 1.1보다 작을 수 있어 "너무 비쌈"을 놓친다)
        public const int NOT_DONE = int.MaxValue;
        public static (double buy, double done, int[] fast, int[] slow) PriceStatsR(int b, int seeds)
        {
            double buy = 1, done = 1;
            int[] fast = { 0, int.MaxValue, int.MaxValue, int.MaxValue }, slow = new int[4];
            var nodes = NODES.Where(Priced).ToList();
            int maxLv = nodes.Sum(n => n.max);
            for (int sd = 1; sd <= seeds; sd++)
            {
                var ph = JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(sd)));
                buy = Math.Min(buy, Enumerable.Range(0, Math.Min(b, ph.buysR.Count)).Count(i => ph.buysR[i] > 0) / (double)b);
                var g = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(StatePath(sd)));
                done = Math.Min(done, nodes.Sum(n => g.nodes.TryGetValue(n.id, out var L) ? Math.Min(L, n.max) : 0) / (double)maxLv);
                for (int t = 1; t <= 3; t++)
                {
                    fast[t] = Math.Min(fast[t], ph.doneR[t] > 0 ? ph.doneR[t] : b + 1);
                    slow[t] = Math.Max(slow[t], ph.doneR[t] > 0 ? ph.doneR[t] : NOT_DONE);
                }
            }
            return (buy, done, fast, slow);
        }

        // 가격 모양 조건을 올리기만으로 맞춘다 (낮추면 단계 노드 줄 전체가 싸져서 트리를 너무 빨리 끝냈다: R79 · 82분).
        // 1) 값 순으로 늘어놓았을 때 바로 앞보다 STEP_FIX배 넘게 뛰면 앞 가격을 올린다
        // 2) 노드 안에서 오르기만, 다음 레벨은 NODE_STEP_MAX배까지 (넘으면 앞 레벨을 올린다)
        // 3) 효율 넘김: 다음 단계 첫 레벨의 효과 +1당 골드가 앞 단계 마지막 레벨의 CROSS배 이하가 되게 앞 단계를 올린다
        // 한 조건을 맞추면 다른 조건이 조금 깨질 수 있어 몇 번 되풀이한다
        public const double STEP_FIX = 1.8;
        // 줄 상한 모드(theory R2-13 안 d, verify R2-13 조건부 통과): 3)을 "앞을 올림" 대신 "뒤를 내림"으로. 올림 모드는 공격력 줄(단계 사이 효과 ×2.4)에서
        // 3단계 가격이 1단계 줄 끝에 바닥을 만들어 1단계를 R20에 못 끝냈다(round2_fit2·fit3). 단계별 이분(BalanceJob.RunBisect)이 켠다
        public static bool LINE_CAP = false;
        public static void EnforceShape()
        {
            LoadPriceTable();
            var price = NODES.Where(Priced).ToDictionary(n => n.id, n => n.costs);
            void RaiseNode(double[] v)
            {
                for (int i = 1; i < v.Length; i++) v[i] = Math.Max(v[i], v[i - 1]);
                if (ENFORCE_TIER_RULES) for (int i = v.Length - 2; i >= 0; i--) v[i] = Math.Max(v[i], Nice(v[i + 1] / NODE_STEP_MAX * 1.05));
            }
            for (int pass = 0; pass < 4; pass++)
            {
                var flat = price.SelectMany(kv => kv.Value.Select((v, i) => (id: kv.Key, i))).OrderBy(x => price[x.id][x.i]).ToList();
                for (int k = flat.Count - 2; k >= 0; k--)
                {
                    var (id, i) = flat[k]; var (id1, i1) = flat[k + 1];
                    price[id][i] = Math.Max(price[id][i], Nice(price[id1][i1] / STEP_FIX * 1.03));
                }
                foreach (var kv in price) RaiseNode(kv.Value);
                if (ENFORCE_TIER_RULES && LINE_CAP) CapLines(price);
                else if (ENFORCE_TIER_RULES) foreach (var (ids, per, cross) in TIER_LINES)
                    for (int t = ids.Length - 1; t >= 1; t--)
                    {
                        if (FreeSibling(per, t)) continue;
                        var pa = price[ids[t - 1]]; var pb = price[ids[t]];
                        // 같은 단계 형제(I → I+)는 한 노드가 이어지는 것처럼 NODE_STEP_MAX배, 단계가 오를 때(I+ → II)는 효율 넘김
                        double need = per[t] == per[t - 1] ? pb[0] / NODE_STEP_MAX : pb[0] / per[t] * per[t - 1] / cross;
                        if (pa[pa.Length - 1] < need) pa[pa.Length - 1] = Nice(need * 1.06);
                        RaiseNode(pa);
                    }
            }
            if (ENFORCE_TIER_RULES && LINE_CAP) CapLines(price);
            SavePriceTable();
        }

        // 줄을 앞에서 뒤로: 다음 노드 첫 레벨 ≤ 앞 끝 × (형제 NODE_STEP_MAX · 단계 경계 per비 × cross), 노드 안 다음 레벨 ≤ 앞 × NODE_STEP_MAX.
        // 내리기만 하되 앞 끝·앞 레벨 아래로는 안 내린다(verify R2-13 하한: 없으면 m2/m1이 작을 때 줄이 거꾸로 내려갔다)
        static void CapLines(Dictionary<string, double[]> price)
        {
            foreach (var (ids, per, cross) in TIER_LINES)
                for (int t = 1; t < ids.Length; t++)
                {
                    if (FreeSibling(per, t)) continue;
                    var pa = price[ids[t - 1]]; var pb = price[ids[t]];
                    double last = pa[pa.Length - 1];
                    double cap = per[t] == per[t - 1] ? last * NODE_STEP_MAX : last * per[t] / per[t - 1] * cross;
                    pb[0] = Math.Max(last, Math.Min(pb[0], NiceDown(cap / 1.06)));
                    for (int i = 1; i < pb.Length; i++) pb[i] = Math.Max(pb[i - 1], Math.Min(pb[i], NiceDown(pb[i - 1] * NODE_STEP_MAX / 1.05)));
                }
        }

        static double NiceDown(double v)
        {
            if (v < 10) return Math.Max(1, Math.Floor(v));
            double e = Math.Pow(10, Math.Floor(Math.Log10(v)) - 1);
            return Math.Floor(v / e + 1e-9) * e;   // 유효숫자 2자리, 내림
        }

        public static void RestorePrices(Dictionary<string, double[]> snap)
        {
            foreach (var kv in snap) NODE[kv.Key].costs = (double[])kv.Value.Clone();
            SavePriceTable();
        }

        // 가격을 매기는 순서: 골드 노드의 모든 레벨을 엑셀 DB 가격(이 도구가 바꾸기 전 값, DbOrder) 순으로. 맞추는 중에 바뀐 가격으로 순서를 다시 정하지 않는다(되먹임 방지)
        static readonly Dictionary<string, double[]> DbOrder = NODES.ToDictionary(n => n.id, n => (double[])n.costs.Clone());   // BalanceSim을 처음 쓸 때(가격표를 불러오기 전)의 DB 가격

        public static List<(Node n, int lv)> OrderedLevels()
        {
            var levels = new List<(Node n, int lv, double order)>();
            foreach (var n in NODES.Where(Priced))
                for (int lv = 0; lv < n.max; lv++) levels.Add((n, lv, DbOrder[n.id][lv]));
            return levels.OrderBy(x => x.order).Select(x => (x.n, x.lv)).ToList();
        }

        // 수렴 판정: (가장 느린 시드에서 무언가 산 판의 비율, 기간 끝 골드 노드 구매율의 최솟값)
        // finish: 가장 빨리 다 산 시드가 트리(초반·중반·후반 전부)를 끝낸 판 (못 끝냈으면 b + 1)
        public static (double buy, double done, int finish) PriceStats(int a, int b, int seeds)
        {
            int finish = int.MaxValue;
            // 구매 판 비율은 기간 수입이 가장 적은(가장 느린) 시드로 본다
            double buy = 0, done = 1, slowInc = double.MaxValue;
            var nodes = NODES.Where(Priced).ToList();
            int maxLv = nodes.Sum(n => n.max);
            for (int sd = 1; sd <= seeds; sd++)
            {
                var ph = JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(sd)));
                double sum = Enumerable.Range(a, b - a + 1).Sum(st => st - 1 < ph.inc.Count ? ph.inc[st - 1] : 0);
                if (sum < slowInc) { slowInc = sum; buy = Enumerable.Range(a, b - a + 1).Count(st => st - 1 < ph.buys.Count && ph.buys[st - 1] > 0) / (double)(b - a + 1); }
                var g = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(StatePath(sd)));
                done = Math.Min(done, nodes.Sum(n => g.nodes.TryGetValue(n.id, out var L) ? Math.Min(L, n.max) : 0) / (double)maxLv);
                finish = Math.Min(finish, ph.done.Skip(1).Any(d => d == 0) ? b + 1 : ph.done.Skip(1).Max());
            }
            return (buy, done, finish);
        }

        // 효율 구간 확인: 단계 노드 줄(공격력·타격 점수·수확량)마다 레벨별 "효과 +1당 골드".
        // 다음 단계 첫 레벨이 앞 단계 마지막 레벨보다 싸야(골드당 효과가 좋아야) 효율 구간이 넘어간다.
        // 가격 점검: (가격을 오름차순으로 늘어놓았을 때 바로 앞 대비 최대 배율, 효율 넘김을 지킨 단계 수, 전체 단계 수)
        public static (double maxStep, double nodeStep, int crossOk, int crossTotal) PriceCheck()
        {
            LoadPriceTable();
            var ps = OrderedLevels().Select(x => NodeCost(x.n, x.lv).gold).OrderBy(v => v).ToList();
            double maxStep = 1, nodeStep = 1;
            for (int k = 1; k < ps.Count; k++) maxStep = Math.Max(maxStep, ps[k] / Math.Max(1, ps[k - 1]));
            foreach (var n in NODES.Where(Priced))
                for (int l = 1; l < n.max; l++) nodeStep = Math.Max(nodeStep, NodeCost(n, l).gold / Math.Max(1, NodeCost(n, l - 1).gold));
            int ok = 0, total = 0;
            foreach (var (ids, per, cross) in TIER_LINES)
                for (int t = 1; t < ids.Length; t++)
                {
                    if (FreeSibling(per, t)) continue;
                    var a = NODE[ids[t - 1]]; var b = NODE[ids[t]];
                    if (per[t] == per[t - 1]) { nodeStep = Math.Max(nodeStep, NodeCost(b, 0).gold / Math.Max(1, NodeCost(a, a.max - 1).gold)); continue; }
                    total++;
                    if (NodeCost(b, 0).gold / per[t] <= NodeCost(a, a.max - 1).gold / per[t - 1] * cross * 1.001) ok++;   // 반올림 오차만 허용 (1.05였을 때 0.66이 실제 0.693 기준이 됐다, 검증 2026-10-08)
                }
            return (maxStep, nodeStep, ok, total);
        }

        public static string EfficiencyReport()
        {
            LoadPriceTable();
            var sb = new StringBuilder();
            var (maxStep, nodeStep, cOk, cTotal) = PriceCheck();
            sb.AppendLine($"가격 곡선: 바로 앞 가격 대비 최대 ×{maxStep:F2} (기준 ×{PRICE_STEP_MAX}) · 노드 안 다음 레벨 최대 ×{nodeStep:F2} (기준 ×{NODE_STEP_MAX}) · 효율 넘김 {cOk}/{cTotal}");
            foreach (var (ids, per, cross) in TIER_LINES)
            {
                var parts = new List<string>(); int ok = 0, total = 0; double lastPer = 0;
                for (int t = 0; t < ids.Length; t++)
                {
                    var n = NODE[ids[t]];
                    var cs = Enumerable.Range(0, n.max).Select(l => NodeCost(n, l).gold / per[t]).ToList();
                    if (t > 0 && !FreeSibling(per, t)) { total++; if (cs[0] < lastPer) ok++; }
                    lastPer = cs.Last();
                    parts.Add($"{n.nKo}: " + string.Join(" → ", cs.Select(c => U.Fmt(c))));
                }
                sb.AppendLine($"{string.Join(" | ", parts)}  (효율 넘김 {ok}/{total})");
            }
            return sb.ToString();
        }

        // Temp/price_table.json(맞춘 가격표)을 BalanceData/db/tuned_prices.json으로 내보낸다.
        // 엑셀 DB에 넣기: python BalanceData/db/db_write.py prices <Assets/DB/MMM_DB_Ver.NN.xlsx> BalanceData/db/tuned_prices.json
        // (엑셀을 COM으로 고친다. 저장하면 Unity가 db.json을 다시 만든다)
        public static string WritePriceFile()
        {
            LoadPriceTable();
            var t = NODES.Where(Priced).ToDictionary(n => n.id, n => n.costs);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../BalanceData/db/tuned_prices.json"));
            File.WriteAllText(path, JsonConvert.SerializeObject(t, Formatting.Indented));
            return $"{path} (노드 {t.Count}개) → python BalanceData/db/db_write.py prices {DbImport.LatestDb()} BalanceData/db/tuned_prices.json";
        }

        // 진행 상태를 이름 붙여 저장·복원 (지역 경계에서 가격을 바꿔 가며 다시 돌리기)
        public static void SaveCheckpoint(string name, int seeds = 3)
        {
            for (int sd = 1; sd <= seeds; sd++) { File.Copy(StatePath(sd), StatePath(sd).Replace(".json", "_" + name + ".json"), true); File.Copy(PhasePath(sd), PhasePath(sd).Replace(".json", "_" + name + ".json"), true); }
        }
        public static void LoadCheckpoint(string name, int seeds = 3)
        {
            for (int sd = 1; sd <= seeds; sd++) { File.Copy(StatePath(sd).Replace(".json", "_" + name + ".json"), StatePath(sd), true); File.Copy(PhasePath(sd).Replace(".json", "_" + name + ".json"), PhasePath(sd), true); }
        }

        // 초반·중반·후반 노드를 처음 산 판과 다 산 판, 그 사이 걸린 시간과 판당 수입
        public static string PhaseReport(int seed = 1)
        {
            if (!File.Exists(PhasePath(seed))) return "기록 없음";
            var ph = JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(seed)));
            var sb = new StringBuilder();
            sb.AppendLine("단계 | 첫 구매 | 완료 | 걸린 판·시간 | 판당 점수골드 (시작 → 완료)");
            for (int t = 1; t <= 3; t++)
            {
                string done = ph.done[t] > 0 ? $"R{ph.done[t]}{(ph.doneR[t] > 0 && ph.doneR[t] != ph.done[t] ? $" · {ph.doneR[t]}판째" : "")} ({ph.doneMin[t]:F0}분)" : "미완료";   // 보스 재도전이 있으면 스테이지와 판 수가 다르다
                string span = ph.done[t] > 0 ? $"{ph.done[t] - ph.first[t]}판 · {ph.doneMin[t] - ph.firstMin[t]:F0}분" : "-";
                sb.AppendLine($"{TIER_NAMES[t]} | R{ph.first[t]} ({ph.firstMin[t]:F0}분) | {done} | {span} | {U.Fmt(ph.firstInc[t])} → {(ph.done[t] > 0 ? U.Fmt(ph.doneInc[t]) : "-")}");
            }
            sb.AppendLine($"무언가 산 판: {ph.buyRounds}/{ph.rounds}");
            return sb.ToString();
        }

        // 노드 구성을 고정하고 n판 평균. nodes == null이면 골드 노드 전부 최대.
        public static RoundResult Measure(Dictionary<string, int> nodes, int n, int stage = 1, int seed = 1)
        {
            return Sandbox(() =>
            {
                var acc = new RoundResult();
                var rnd = new System.Random(seed); UnityEngine.Random.InitState(seed);
                for (int i = 0; i < n; i++)
                {
                    G = new SaveData { rounds = stage - 1, stage = stage };
                    G.nodes = nodes != null ? new Dictionary<string, int>(nodes) : NODES.Where(x => !x.gem).ToDictionary(x => x.id, x => x.max);
                    var r = PlayRound(rnd);
                    acc.score += r.score / n; acc.scoreGold += r.scoreGold / n; acc.value += r.value / n;
                    acc.harvests += r.harvests / n; acc.maxCombo += r.maxCombo / n; acc.duration += r.duration / n; acc.cols = r.cols;
                }
                return acc;
            });
        }

        public static Dictionary<string, int> MaxGold() => NODES.Where(x => !x.gem).ToDictionary(x => x.id, x => x.max);

        // 전부 최대인 구성에서 노드 하나씩 0으로 내려 수입이 얼마나 줄어드는지 본다
        public static string Ablation(IEnumerable<string> ids, int n = 2, double maxWallSec = 60, bool withChildren = false)
        {
            var sb = new StringBuilder();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var full = Measure(null, n);
            Log(sb, $"전부 최대: 점수골드 {full.scoreGold:G3} · 버섯가치 {full.value:G3} · 수확 {full.harvests} · 콤보 {full.maxCombo}");
            foreach (var id in ids)
            {
                if (sw.Elapsed.TotalSeconds > maxWallSec) { Log(sb, "(시간 초과)"); break; }
                var cfg = MaxGold();
                // 자식 노드도 함께 0 (부모가 0이면 자식은 의미가 없다)
                var kill = new HashSet<string> { id };
                bool grew = withChildren;
                while (grew) { grew = false; foreach (var x in NODES) if (x.parent != null && kill.Contains(x.parent) && kill.Add(x.id)) grew = true; }
                foreach (var k in kill) cfg[k] = 0;
                var r = Measure(cfg, n);
                Log(sb, $"{id,-12} 0으로: 점수골드 ×{r.scoreGold / full.scoreGold:F2} · 버섯가치 ×{r.value / full.value:F2} · 수확 {r.harvests}");
            }
            return sb.ToString();
        }
    }
}
