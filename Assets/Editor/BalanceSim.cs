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
    public static class BalanceSim
    {
        public class RoundResult
        {
            public int stage; public string weather;
            public double score, scoreGold, bonus, value, duration;
            public int harvests, maxCombo, cols, balls, spawned;
            public int[] madeT, killT;   // 티어별 생긴 수·수확한 수
            public string zone; public double atk;
        }

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
            try { return f(); }
            finally
            {
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
            string w = RollWeather();
            var R = RoundSim.Start(w, G.theme);
            var res = new RoundResult { stage = StageNow(), weather = w, duration = R.st.duration, cols = R.st.maxCol, zone = G.theme, atk = R.st.atk, balls = R.st.permBalls };
            R.Launch(Mathf.Atan2(-1f, (float)(rnd.NextDouble() * 0.8 - 0.4)));
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
            }
            res.maxCombo = R.maxCombo;
            var s = R.Finish();
            res.score = s.score; res.scoreGold = s.scoreGold; res.bonus = s.bonusGold; res.value = s.value; res.harvests = s.harvests; res.spawned = R.shroomsMade; res.madeT = (int[])R.madeT.Clone(); res.killT = (int[])R.killT.Clone();
            return res;
        }

        public static int GoldLevels(int tier = 0) => NODES.Where(n => n.costGem <= 0 && (tier == 0 || n.tier == tier)).Sum(n => Lv(n.id));
        public static int GoldLevelsMax(int tier = 0) => NODES.Where(n => n.costGem <= 0 && (tier == 0 || n.tier == tier)).Sum(n => n.max);

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
            public void Set(int stage, double income, int bought, double value = 0, double killRate = 0, string zoneId = "")
            {
                while (inc.Count < stage) { inc.Add(0); buys.Add(0); }
                while (val.Count < stage) val.Add(0);
                while (kill.Count < stage) kill.Add(0);
                while (zone.Count < stage) zone.Add("");
                inc[stage - 1] = income; buys[stage - 1] = bought; val[stage - 1] = value; kill[stage - 1] = killRate; zone[stage - 1] = zoneId;
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
        public static string Progression(int rounds, bool resume = false, int seed = 1, float skill = SKILL, double maxWallSec = 20, int every = 1, int stopStage = 0)
        {
            LoadPriceTable();
            return Sandbox(() =>
            {
                G = resume && File.Exists(StatePath(seed)) ? JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(StatePath(seed))) : new SaveData();
                var ph = resume && File.Exists(PhasePath(seed)) ? JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(seed))) : new PhaseLog();
                var sb = new StringBuilder();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                if (!resume) Log(sb, "판 | 누적 | 지역 | 시간 | 핀볼 | 공격력 | 점수골드 | 수확 | 보유골드 | 산 레벨 | 초반/중반/후반");
                for (int i = 0; i < rounds; i++)
                {
                    if (sw.Elapsed.TotalSeconds > maxWallSec || (stopStage > 0 && StageNow() > stopStage)) break;
                    var rnd = new System.Random(seed * 1000 + G.rounds); UnityEngine.Random.InitState(seed * 1000 + G.rounds);
                    var r = PlayRound(rnd, skill, !GAME_ZONE);
                    // 판매·의뢰: 매 판 끝에 의뢰를 전부 하고 창고 버섯을 전부 판다(사용자 결정 2026-10-08 "의뢰+전부 판매 가정, 널널하게").
                    // 판 수입(ph.inc)에 들어가서 가격 맞추기도 이 수입을 기준으로 한다. 끄려면 SELL = false(명령줄 -nosell)
                    double sold = 0;
                    if (SELL)
                    {
                        RefreshQuests();
                        sold += QuestAll().gold;
                        sold += SellMush(G.inv.Select(kv => (kv.Key, kv.Value)).ToList()).gold;
                    }
                    int before = GoldLevels();
                    BulkBuy(new[] { "ed", "md", "ps" }, 0, false);
                    G.play += r.duration + OVERHEAD_SEC;   // 추정 플레이 시간(초)
                    int bought = GoldLevels() - before;
                    ph.rounds++; if (bought > 0) ph.buyRounds++;
                    ph.Set(r.stage, r.scoreGold + r.bonus + sold, bought, r.value, r.spawned > 0 ? (double)r.harvests / r.spawned : 0, r.zone);
                    ph.SetTiers(r.stage, r.madeT, r.killT);
                    string marks = "";
                    for (int t = 1; t <= 3; t++)
                    {
                        if (ph.first[t] == 0 && GoldLevels(t) > 0) { ph.first[t] = r.stage; ph.firstMin[t] = G.play / 60; ph.firstInc[t] = r.scoreGold; marks += $" ← {TIER_NAMES[t]} 시작"; }
                        if (ph.done[t] == 0 && GoldLevels(t) == GoldLevelsMax(t)) { ph.done[t] = r.stage; ph.doneMin[t] = G.play / 60; ph.doneInc[t] = r.scoreGold; marks += $" ← {TIER_NAMES[t]} 완료"; }
                    }
                    if (r.stage % every == 0 || r.stage <= 10 || marks != "")
                        Log(sb, $"R{r.stage} | {G.play / 60:F0}분 | {THEME[r.zone].n} | {r.duration:F0}s | {r.balls} | {U.Fmt(r.atk)} | {U.Fmt(r.scoreGold)} | {r.harvests} | {U.Fmt(G.gold)} | +{bought} | {GoldLevels(1)}/{GoldLevelsMax(1)} {GoldLevels(2)}/{GoldLevelsMax(2)} {GoldLevels(3)}/{GoldLevelsMax(3)} | 도감 {CodexCount()} 별 {SPECIES.Sum(sp => StarOf(sp.id))} 최고등급 {SPECIES.Where(IsUnlockedSp).Max(sp => sp.t)} 황금 {GoldenCount()}{marks}");
                    File.WriteAllText(StatePath(seed), JsonConvert.SerializeObject(G));
                    File.WriteAllText(PhasePath(seed), JsonConvert.SerializeObject(ph));
                }
                Log(sb, $"(무언가 산 판 {ph.buyRounds}/{ph.rounds} · 이번 호출 {sw.Elapsed.TotalSeconds:F1}s)");
                return sb.ToString();
            });
        }


        // ===== 가격 맞추기 =====
        // 지역 하나(그 지역에서 열리는 노드)의 레벨별 가격을, 직전 시뮬레이션에서 그 지역 기간에 번 골드에 맞춰 다시 매긴다.
        // 사는 순서는 노드의 기본가 × 증가율^레벨 순서를 그대로 따르고, 지역 기간의 finish 비율 안에서 고르게 사도록 나눈다.
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
            return NODES.Where(n => n.costs != null && n.costGem <= 0 && !n.core).ToDictionary(n => n.id, n => (double[])n.costs.Clone());
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
        public static readonly (string[] ids, double[] per)[] TIER_LINES =
        {
            (new[] { "ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4" }, new[] { 1.0, 10, 10, 100, 100, 1000 }),
            (new[] { "ed_score", "ed_score1p", "ed_score2", "ed_score2p", "ed_score3", "ed_score3p" }, new[] { 1.0, 1, 10, 10, 100, 100 }),
            (new[] { "ed_bonus", "ed_bonus1p", "ed_bonus2", "ed_bonus2p", "ed_bonus3", "ed_bonus3p" }, new[] { 1.0, 1, 10, 10, 100, 100 }),
        };
        public const double CROSS = 0.66;   // 조사 원칙 P7(다음 단계 효율 ≥ ×1.5) + 반올림 여유. 0.7(×1.43)은 I→II 세 곳이 ×1.5에 못 미쳤다(검증 2026-10-08)
        // 같은 노드 안에서 다음 레벨은 앞 레벨의 NODE_STEP_MAX배를 넘지 않는다 (한 노드 가격이 한 번에 크게 뛰지 않게)
        public const double NODE_STEP_MAX = 4;
        // 노드 안 배율·효율 넘김을 가격에 강제할지. 강제하면 값이 진동했다(2026-10-08): 효과가 단계마다 ×10인데
        // 수입은 단계 사이에 ×1000 넘게 커져서, 줄(타격 점수·수확량은 9레벨)이 게임 전체에 걸치면서 둘 다 지킬 수 없다.
        // 지금은 보고만 한다(EfficiencyReport).
        // 2026-10-08: 형제 노드(I+ · II+ · III+)로 줄을 늘려 다시 켠다
        public const bool ENFORCE_TIER_RULES = true;

        // 기본 골드 노드 전부(지역 잠금 없음)의 레벨별 가격을, 직전 시뮬레이션의 R a~b 수입에 맞춰 다시 매긴다.
        // 사는 순서는 노드의 기본가 × 증가율^레벨 순서를 따르고, 기간의 finish 비율 안에서 고르게 사도록 나눈다.
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

        // 가격 모양 조건을 올리기만으로 맞춘다 (낮추면 단계 노드 줄 전체가 싸져서 트리를 너무 빨리 끝냈다: R79 · 82분).
        // 1) 값 순으로 늘어놓았을 때 바로 앞보다 STEP_FIX배 넘게 뛰면 앞 가격을 올린다
        // 2) 노드 안에서 오르기만, 다음 레벨은 NODE_STEP_MAX배까지 (넘으면 앞 레벨을 올린다)
        // 3) 효율 넘김: 다음 단계 첫 레벨의 효과 +1당 골드가 앞 단계 마지막 레벨의 CROSS배 이하가 되게 앞 단계를 올린다
        // 한 조건을 맞추면 다른 조건이 조금 깨질 수 있어 몇 번 되풀이한다
        public const double STEP_FIX = 1.8;
        public static void EnforceShape()
        {
            LoadPriceTable();
            var price = NODES.Where(n => n.costs != null && n.costGem <= 0 && !n.core).ToDictionary(n => n.id, n => n.costs);
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
                if (ENFORCE_TIER_RULES) foreach (var (ids, per) in TIER_LINES)
                    for (int t = ids.Length - 1; t >= 1; t--)
                    {
                        var pa = price[ids[t - 1]]; var pb = price[ids[t]];
                        // 같은 단계 형제(I → I+)는 한 노드가 이어지는 것처럼 NODE_STEP_MAX배, 단계가 오를 때(I+ → II)는 효율 넘김
                        double need = per[t] == per[t - 1] ? pb[0] / NODE_STEP_MAX : pb[0] / per[t] * per[t - 1] / CROSS;
                        if (pa[pa.Length - 1] < need) pa[pa.Length - 1] = Nice(need * 1.06);
                        RaiseNode(pa);
                    }
            }
            SavePriceTable();
        }

        public static void RestorePrices(Dictionary<string, double[]> snap)
        {
            foreach (var kv in snap) NODE[kv.Key].costs = (double[])kv.Value.Clone();
            SavePriceTable();
        }

        // 가격을 매기는 순서: 골드 노드의 모든 레벨을 기본가 × 증가율^레벨 순으로 (코어 노드는 고정가라 뺀다)
        public static List<(Node n, int lv)> OrderedLevels()
        {
            var levels = new List<(Node n, int lv, double order)>();
            foreach (var n in NODES.Where(n => n.costGem <= 0 && !n.core))
                for (int lv = 0; lv < n.max; lv++) levels.Add((n, lv, n.costGold * Math.Pow(n.g, lv)));
            return levels.OrderBy(x => x.order).Select(x => (x.n, x.lv)).ToList();
        }

        // 수렴 판정: (가장 느린 시드에서 무언가 산 판의 비율, 기간 끝 골드 노드 구매율의 최솟값)
        // finish: 가장 빨리 다 산 시드가 트리(초반·중반·후반 전부)를 끝낸 판 (못 끝냈으면 b + 1)
        public static (double buy, double done, int finish) PriceStats(int a, int b, int seeds)
        {
            int finish = int.MaxValue;
            // 구매 판 비율은 기간 수입이 가장 적은(가장 느린) 시드로 본다
            double buy = 0, done = 1, slowInc = double.MaxValue;
            var nodes = NODES.Where(n => n.costGem <= 0 && !n.core).ToList();
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
            foreach (var n in NODES.Where(n => n.costGem <= 0 && !n.core))
                for (int l = 1; l < n.max; l++) nodeStep = Math.Max(nodeStep, NodeCost(n, l).gold / Math.Max(1, NodeCost(n, l - 1).gold));
            int ok = 0, total = 0;
            foreach (var (ids, per) in TIER_LINES)
                for (int t = 1; t < ids.Length; t++)
                {
                    var a = NODE[ids[t - 1]]; var b = NODE[ids[t]];
                    if (per[t] == per[t - 1]) { nodeStep = Math.Max(nodeStep, NodeCost(b, 0).gold / Math.Max(1, NodeCost(a, a.max - 1).gold)); continue; }
                    total++;
                    if (NodeCost(b, 0).gold / per[t] <= NodeCost(a, a.max - 1).gold / per[t - 1] * CROSS * 1.001) ok++;   // 반올림 오차만 허용 (1.05였을 때 0.66이 실제 0.693 기준이 됐다, 검증 2026-10-08)
                }
            return (maxStep, nodeStep, ok, total);
        }

        public static string EfficiencyReport()
        {
            LoadPriceTable();
            var sb = new StringBuilder();
            var (maxStep, nodeStep, cOk, cTotal) = PriceCheck();
            sb.AppendLine($"가격 곡선: 바로 앞 가격 대비 최대 ×{maxStep:F2} (기준 ×{PRICE_STEP_MAX}) · 노드 안 다음 레벨 최대 ×{nodeStep:F2} (기준 ×{NODE_STEP_MAX}) · 효율 넘김 {cOk}/{cTotal}");
            foreach (var (ids, per) in TIER_LINES)
            {
                var parts = new List<string>(); int ok = 0, total = 0; double lastPer = 0;
                for (int t = 0; t < ids.Length; t++)
                {
                    var n = NODE[ids[t]];
                    var cs = Enumerable.Range(0, n.max).Select(l => NodeCost(n, l).gold / per[t]).ToList();
                    if (t > 0) { total++; if (cs[0] < lastPer) ok++; }
                    lastPer = cs.Last();
                    parts.Add($"{n.n}: " + string.Join(" → ", cs.Select(c => U.Fmt(c))));
                }
                sb.AppendLine($"{string.Join(" | ", parts)}  (효율 넘김 {ok}/{total})");
            }
            return sb.ToString();
        }

        // Temp/price_table.json을 Assets/Scripts/Core/Defs.Prices.cs로 쓴다
        public static string WritePriceFile()
        {
            LoadPriceTable();
            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace MoreMush");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class Defs");
            sb.AppendLine("    {");
            sb.AppendLine("        // 노드 레벨별 골드 가격표. Assets/Editor/BalanceSim.cs의 가격 맞추기(TunePrices)로 생성한다.");
            sb.AppendLine("        // 기준: 평균 이하 플레이어(BalanceSim.SKILL), 기본 트리 약 2시간, 지역 잠금 없이 골드로만. 비어 있는 노드는 기본가 × 증가율^레벨을 쓴다.");
            sb.AppendLine("        public static readonly Dictionary<string, double[]> PRICE_TABLE = new Dictionary<string, double[]>");
            sb.AppendLine("        {");
            for (int t = 1; t <= 3; t++)
            {
                var ns = NODES.Where(n => n.costs != null && n.tier == t).ToList();
                if (ns.Count == 0) continue;
                sb.AppendLine($"            // {TIER_NAMES[t]}");
                foreach (var n in ns) sb.AppendLine($"            [\"{n.id}\"] = new double[] {{ {string.Join(", ", n.costs.Select(c => c.ToString("0")))} }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(Application.dataPath, "Scripts/Core/Defs.Prices.cs"), sb.ToString());
            return "written";
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
                string done = ph.done[t] > 0 ? $"R{ph.done[t]} ({ph.doneMin[t]:F0}분)" : "미완료";
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
                    G = new SaveData { rounds = stage - 1 };
                    G.nodes = nodes != null ? new Dictionary<string, int>(nodes) : NODES.Where(x => x.costGem <= 0).ToDictionary(x => x.id, x => x.max);
                    var r = PlayRound(rnd);
                    acc.score += r.score / n; acc.scoreGold += r.scoreGold / n; acc.value += r.value / n;
                    acc.harvests += r.harvests / n; acc.maxCombo += r.maxCombo / n; acc.duration += r.duration / n; acc.cols = r.cols;
                }
                return acc;
            });
        }

        public static Dictionary<string, int> MaxGold() => NODES.Where(x => x.costGem <= 0).ToDictionary(x => x.id, x => x.max);

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
