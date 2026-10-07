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
            public int harvests, maxCombo, cols, balls;
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
            res.score = s.score; res.scoreGold = s.scoreGold; res.bonus = s.bonusGold; res.value = s.value; res.harvests = s.harvests;
            return res;
        }

        static void PayOrSkipTax(StringBuilder sb)
        {
            if (G.tax.roundsIn < TAX.every) return;
            double bill = TaxBill(), inc = G.tax.income;
            if (G.gold >= bill)
            {
                G.gold -= bill; G.tax.paid++; CheckUnlocks();
                sb?.AppendLine($"   세금 {G.tax.cycle}회차: 소득 {inc:G3} · 고지 {bill:G3} ({bill / Math.Max(1, inc):P0}) 납부");
            }
            else
            {
                double rate = TaxRate(G.tax.unpaid), pen = Math.Ceiling(inc * rate), take = Math.Min(G.gold, pen);
                G.gold -= take; G.tax.debt += pen - take; G.tax.unpaid++;
                sb?.AppendLine($"   세금 {G.tax.cycle}회차: 고지 {bill:G3} 못 냄 → 벌금 {pen:G3}");
            }
            G.tax.cycle++; G.tax.roundsIn = 0; G.tax.income = 0;
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
            public void Set(int stage, double income, int bought)
            {
                while (inc.Count < stage) { inc.Add(0); buys.Add(0); }
                inc[stage - 1] = income; buys[stage - 1] = bought;
            }
        }
        static string PhasePath(int seed = 1) => Path.GetFullPath(Path.Combine(Application.dataPath, $"../Temp/balance_phase_s{seed}.json"));

        static string StatePath(int seed = 1) => Path.GetFullPath(Path.Combine(Application.dataPath, $"../Temp/balance_state_s{seed}.json"));

        // 새 게임(resume = false) 또는 이전 진행 상태에서 n판. 매 판 뒤 세금 처리 후 일괄 강화(가장 싼 것부터)로 전부 산다.
        // 한 판에 라운드 외에 드는 시간(조준·정산·트리·세금 화면) 추정치. 플레이 시간 환산에만 쓴다
        // 보수적 기준: 평균 이하 플레이어. 바 조작 오차 ±60%, 한 판 화면 전환 30초
        public const double OVERHEAD_SEC = 30;
        public const float SKILL = 0.6f;

        // every: 몇 판마다 한 줄씩 남길지. 트리를 다 산 판은 항상 남긴다
        public static string Progression(int rounds, bool resume = false, int seed = 1, float skill = SKILL, double maxWallSec = 20, int every = 1, bool withTax = false, int stopStage = 0)
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
                    var r = PlayRound(rnd, skill);
                    // 세금은 제거 예정이라 기본은 빼고 계산한다 (withTax = true면 납부·추정분 비축)
                    double reserve = 0;
                    if (withTax)
                    {
                        PayOrSkipTax(null);
                        double projected = G.tax.roundsIn > 0 ? G.tax.income * TAX.every / G.tax.roundsIn : 0;
                        reserve = Math.Max(TaxAmount(G.tax.cycle), TAX.share * projected) * G.tax.roundsIn / TAX.every;
                    }
                    else { G.tax.roundsIn = 0; G.tax.income = 0; }
                    int before = GoldLevels();
                    // 구간 마지막 판(stopStage)에는 다음 지역 노드를 사지 않는다. 사 버리면 체크포인트에 이미 들어가
                    // 다음 지역 가격을 아무리 바꿔도 결과가 같아진다 (밤·들판 가격이 안 맞던 원인)
                    var masked = new List<(Node n, double[] c)>();
                    if (stopStage > 0 && r.stage >= stopStage)
                    {
                        int zNow = THEMES.FindLastIndex(t => t.from <= r.stage);
                        foreach (var n in NODES.Where(n => n.costGem <= 0 && n.zone > zNow))
                        { masked.Add((n, n.costs)); n.costs = Enumerable.Repeat(double.MaxValue, n.max).ToArray(); }
                    }
                    try { BulkBuy(new[] { "ed", "md", "ps" }, reserve, false); }
                    finally { foreach (var (n, c) in masked) n.costs = c; }
                    G.play += r.duration + OVERHEAD_SEC;   // 추정 플레이 시간(초)
                    int bought = GoldLevels() - before;
                    ph.rounds++; if (bought > 0) ph.buyRounds++;
                    ph.Set(r.stage, r.scoreGold + r.bonus, bought);
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
                Log(sb, $"({(withTax ? $"세금 납부 {G.tax.paid}회 · 미납 {G.tax.unpaid}회 · " : "세금 제외 · ")}무언가 산 판 {ph.buyRounds}/{ph.rounds} · 이번 호출 {sw.Elapsed.TotalSeconds:F1}s)");
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

        static double Nice(double v)
        {
            if (v < 10) return Math.Max(1, Math.Round(v));
            double e = Math.Pow(10, Math.Floor(Math.Log10(v)) - 1);
            return Math.Round(v / e) * e;   // 유효숫자 2자리
        }

        public static string TuneZone(int zone, int a, int b, double finish = 0.85, double damp = 0.5, int seeds = 1)
        {
            LoadPriceTable();
            // 시드별 기록에서 판마다 가장 낮은 수입을 쓴다 (보수적: 가장 느린 플레이어도 고르게 사게).
            // 기하평균은 시드끼리 수입이 25배 넘게 벌어지는 구간(숲 → 밤)에서 느린 시드가 몇 판씩 아무것도 못 샀다.
            var logs = Enumerable.Range(1, seeds).Select(sd => JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(sd)))).ToList();
            var ph = logs[0];
            int L = b - a + 1;
            double IncAt(int st) => logs.Min(l => st - 1 < l.inc.Count ? l.inc[st - 1] : 0);
            // 지역 기간의 누적 수입 (3판 이동 평균으로 흔들림을 줄인다)
            var raw = Enumerable.Range(a, L).Select(IncAt).ToArray();
            var inc = raw.Select((v, i) => raw.Skip(Math.Max(0, i - 1)).Take(i == 0 ? 2 : 3).Average()).ToArray();
            var cum = new double[L + 1];
            for (int i = 0; i < L; i++) cum[i + 1] = cum[i] + inc[i];
            double C(double x) { x = Math.Max(0, Math.Min(L, x)); int i = (int)Math.Floor(x); return i >= L ? cum[L] : cum[i] + (cum[i + 1] - cum[i]) * (x - i); }

            var levels = new List<(Node n, int lv, double order)>();
            foreach (var n in NODES.Where(n => n.costGem <= 0 && n.zone == zone))
                for (int lv = 0; lv < n.max; lv++) levels.Add((n, lv, n.costGold * Math.Pow(n.g, lv)));
            levels = levels.OrderBy(x => x.order).ToList();
            int N = levels.Count;
            var price = new Dictionary<string, double[]>();
            double prev = 0;
            for (int k = 0; k < N; k++)
            {
                double x0 = L * finish * k / N, x1 = L * finish * (k + 1) / N;
                double c = Nice(Math.Max(prev, C(x1) - C(x0)));
                prev = c;
                var (n, lv, _) = levels[k];
                if (!price.TryGetValue(n.id, out var arr)) price[n.id] = arr = new double[n.max];
                arr[lv] = c;
            }
            // 가격이 바뀌면 수입도 바뀌어 값이 진동하므로, 이전 가격과의 로그 평균으로 반씩만 옮긴다
            foreach (var kv in price)
            {
                var n = NODE[kv.Key];
                for (int i = 0; i < kv.Value.Length; i++)
                {
                    double old = NodeCost(n, i).gold;
                    kv.Value[i] = Nice(Math.Exp(damp * Math.Log(Math.Max(1, kv.Value[i])) + (1 - damp) * Math.Log(Math.Max(1, old))));
                }
            }
            // 같은 노드 안에서는 레벨이 오를수록 비싸야 한다
            foreach (var kv in price) for (int i = 1; i < kv.Value.Length; i++) kv.Value[i] = Math.Max(kv.Value[i], kv.Value[i - 1]);
            foreach (var kv in price) NODE[kv.Key].costs = kv.Value;
            SavePriceTable();

            // 직전 실행의 결과: 지역 기간 중 산 판 수, 그 지역 노드 총액 대비 기간 수입
            double buyRounds = logs.Average(l => Enumerable.Range(a, L).Count(st => st - 1 < l.buys.Count && l.buys[st - 1] > 0));
            return $"{THEMES[zone].n} R{a}~R{b}: 직전 실행에서 산 판 {buyRounds:F1}/{L} · 기간 수입 {U.Fmt(cum[L])} · 레벨 {N}개 · 새 가격 {U.Fmt(price.Values.SelectMany(v => v).Min())} ~ {U.Fmt(price.Values.SelectMany(v => v).Max())} (합 {U.Fmt(price.Values.SelectMany(v => v).Sum())})";
        }

        // 수렴 판정: (가장 느린 시드에서 무언가 산 판의 비율, 구간 끝 그 지역 노드 구매율의 최솟값)
        public static (double buy, double done) ZoneStats(int zone, int a, int b, int seeds)
        {
            // 구매 판 비율은 구간 수입이 가장 적은(가장 느린) 시드로 본다. 최솟값을 쓰면 이월 골드로
            // 첫 판에 다 사 버리고 할 일이 없어진 빠른 시드가 뽑혀 판정이 거꾸로 된다.
            double buy = 0, done = 1, slowInc = double.MaxValue;
            var zoneNodes = NODES.Where(n => n.costGem <= 0 && n.zone == zone).ToList();
            int maxLv = zoneNodes.Sum(n => n.max);
            for (int sd = 1; sd <= seeds; sd++)
            {
                var ph = JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(sd)));
                double zoneInc = Enumerable.Range(a, b - a + 1).Sum(st => st - 1 < ph.inc.Count ? ph.inc[st - 1] : 0);
                if (zoneInc < slowInc) { slowInc = zoneInc; buy = Enumerable.Range(a, b - a + 1).Count(st => st - 1 < ph.buys.Count && ph.buys[st - 1] > 0) / (double)(b - a + 1); }
                var g = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(StatePath(sd)));
                done = Math.Min(done, zoneNodes.Sum(n => g.nodes.TryGetValue(n.id, out var L) ? L : 0) / (double)maxLv);
            }
            return (buy, done);
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
            sb.AppendLine("        // 노드 레벨별 골드 가격표. Assets/Editor/BalanceSim.cs의 가격 맞추기(TuneZone)로 생성한다.");
            sb.AppendLine("        // 기준: 평균 이하 플레이어(BalanceSim.SKILL), 세금 제외, 기본 트리 약 2시간. 비어 있는 노드는 기본가 × 증가율^레벨을 쓴다.");
            sb.AppendLine("        public static readonly Dictionary<string, double[]> PRICE_TABLE = new Dictionary<string, double[]>");
            sb.AppendLine("        {");
            foreach (var z in Enumerable.Range(0, THEMES.Count))
            {
                var ns = NODES.Where(n => n.costs != null && n.zone == z).ToList();
                if (ns.Count == 0) continue;
                sb.AppendLine($"            // {THEMES[z].n} (스테이지 {THEMES[z].from}부터)");
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
