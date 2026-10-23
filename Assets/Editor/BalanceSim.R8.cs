using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush.EditorTools
{
    // 8회차 T8 기준선 측정기 (test 에이전트). 기존 BalanceSim.Progression·PlayRound와 같은 순서·같은 난수로 돌되,
    // 판마다 군락 최대치·판 시작 군락 수·산 노드를 CSV로 남긴다. 게임 로직·DB는 건드리지 않는다.
    // 명령줄: Unity.exe -batchmode -nographics -projectPath <p> -executeMethod MoreMush.EditorTools.BalanceR8.T80 -logFile <log> [-seeds 1,2,3,4,5,6] [-out BalanceData/r8/test/T8-0/] [-maxStage 64] [-bossEnd 54]
    public static partial class BalanceSim
    {
        public class R8Row
        {
            public int seed, i, stage, bossFlag, harvests, made, cols, colsStart, balls, buys;
            public double net, scoreGold, atk, gold, playMin, dur, bossLeft, colS;
            public string weather, bought, boss, bossRes, nextKey;
            public double nextCost;
        }

        // PlayRound와 같다 (난수 소비 순서 동일). 군락 계측만 추가
        static RoundResult PlayRoundR8(System.Random rnd, float skill, out int colsStart, out float colS)
        {
            G.theme = PickZone();
            var boss = BossRoundNext() ? BossAt(ContentNow()) : null;
            string w = RollWeather(boss != null);
            var R = RoundSim.Start(w, G.theme, boss);
            colsStart = R.colonies.Count; colS = R.colS;
            var res = new RoundResult { stage = StageNow(), weather = w, duration = R.st.duration, cols = R.st.maxCol, zone = G.theme, atk = R.st.atk, balls = R.st.permBalls, boss = boss?.id };
            if (boss != null) { var bm = R.BossMain; R.Launch(Mathf.Atan2(bm.y - RoundSim.LAUNCH.y, bm.x - RoundSim.LAUNCH.x) + (float)(rnd.NextDouble() * 0.2 - 0.1)); }
            else R.Launch(Mathf.Atan2(-1f, (float)(rnd.NextDouble() * 0.8 - 0.4)));
            float h = 1 / 30f, sim = 0, noise = 0, nt = 0;
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

        // 새 게임에서 시작해 표시 보스(bossEnd, 내부 번호)를 처치하거나 스테이지가 maxStage를 넘으면 멈춘다. phase/state는 Temp에 쓰고 out에 복사
        public static string ProgressionR8(int seed, float skill, int maxStage, int bossEnd, int maxRounds, string outDir)
        {
            LoadPriceTable();
            var rows = new List<R8Row>();
            string text = Sandbox(() =>
            {
                G = new SaveData();
                var ph = new PhaseLog();
                var sb = new StringBuilder();
                botFarm = 0; botBoughtSinceFail = false;
                sb.AppendLine($"조건: 시드 {seed} · 바 오차(SKILL) {skill} · 보스 {(BOSS_GATE ? "관문" : "실제 판(봇 조준)")} · 프리셋 {TUNE.id} · DB {Db.Data._source}");
                sb.AppendLine($"R = 내부 판 번호(튜토리얼 T1~T{TUT_ROUNDS} 포함). 표시 = R − {TUT_ROUNDS}. 멈춤: 보스 R{bossEnd} 처치 또는 스테이지 > {maxStage}");
                for (int i = 0; i < maxRounds; i++)
                {
                    if (StageNow() > maxStage) { sb.AppendLine("멈춤: 스테이지 상한"); break; }
                    var rnd = new System.Random(seed * 1000 + G.rounds); UnityEngine.Random.InitState(seed * 1000 + G.rounds);
                    if (IsBossStage(ContentNow()) && !G.bossOn && (botBoughtSinceFail || ++botFarm >= 3)) { G.bossOn = true; botFarm = 0; botBoughtSinceFail = false; }
                    int stageBefore = StageNow();
                    var r = PlayRoundR8(rnd, skill, out int cs, out float colS);
                    int before = GoldLevels(), atkBefore = AtkLevels();
                    var nodesBefore = new Dictionary<string, int>(G.nodes);
                    BulkBuy(new[] { "ed", "md", "ps" }, 0, false);
                    G.play += r.duration + OVERHEAD_SEC;
                    int bought = GoldLevels() - before;
                    if (bought > 0) botBoughtSinceFail = true;
                    if (r.boss != null && !r.bossCleared) botBoughtSinceFail = false;
                    var bl = new List<string>();
                    foreach (var kv in G.nodes) { nodesBefore.TryGetValue(kv.Key, out int b0); if (kv.Value != b0) bl.Add($"{kv.Key}:{b0}>{kv.Value}"); }
                    string nk = ""; double nc = double.PositiveInfinity;
                    foreach (var n in NODES)
                    {
                        if (n.gem || !(n.br == "ed" || n.br == "md" || n.br == "ps")) continue;
                        var ns = NodeState(n); if (ns != "avail" && ns != "owned") continue;
                        if (!ParentOk(n)) continue;
                        double cc = NodeCost(n, Lv(n.id)).gold; if (cc < nc) { nc = cc; nk = n.id; }
                    }
                    string bres = r.boss == null ? "" : (r.bossCleared ? "처치" : "실패");
                    if (r.boss != null) sb.AppendLine($"  보스 R{r.stage} {BOSS[r.boss].nKo}: {(r.bossCleared ? "처치" : $"실패 (남은 체력 {r.bossHpLeft:P0})")} · 표시 스테이지 {StageLabel(r.stage)}");
                    ph.rounds++; if (bought > 0) ph.buyRounds++;
                    ph.Set(r.stage, r.net, bought, r.value, r.spawned > 0 ? (double)r.harvests / r.spawned : 0, r.zone);
                    ph.incR.Add(r.net); ph.stageR.Add(r.stage); ph.playR.Add(G.play); ph.atkR.Add(r.atk); ph.buysR.Add(bought); ph.gateR.Add(r.gateT);
                    ph.bossR.Add(r.boss != null ? (r.bossCleared ? 1 : -1) : IsBossStage(ContentStage(r.stage)) ? 2 : 0);
                    ph.SetTiers(r.stage, r.madeT, r.killT);
                    rows.Add(new R8Row
                    {
                        seed = seed, i = i + 1, stage = r.stage, bossFlag = ph.bossR[ph.bossR.Count - 1], net = r.net, scoreGold = r.scoreGold, atk = r.atk, balls = r.balls,
                        gold = G.gold, buys = bought, bought = string.Join(";", bl), cols = r.cols, colsStart = cs, colS = colS, made = r.spawned, harvests = r.harvests,
                        playMin = G.play / 60, dur = r.duration, bossLeft = r.bossHpLeft, weather = r.weather, boss = r.boss ?? "", bossRes = bres, nextCost = nc, nextKey = nk,
                    });
                    sb.AppendLine($"R{r.stage} | {G.play / 60:F1}분 | {r.duration:F0}s | 핀볼 {r.balls} | 공격력 {U.Fmt(r.atk)} | net {U.Fmt(r.net)} | 수확 {r.harvests}/{r.spawned} | 군락 {cs}/{r.cols} | 보유 {U.Fmt(G.gold)} | +{bought}");
                    if (r.boss != null && r.bossCleared && r.stage >= bossEnd) { sb.AppendLine($"멈춤: 보스 R{r.stage} 처치"); break; }
                }
                File.WriteAllText(StatePath(seed), JsonConvert.SerializeObject(G));
                File.WriteAllText(PhasePath(seed), JsonConvert.SerializeObject(ph));
                sb.AppendLine($"(무언가 산 판 {ph.buyRounds}/{ph.rounds})");
                return sb.ToString();
            });
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, $"t80_s{seed}.log"), text, new UTF8Encoding(false));
            File.Copy(PhasePath(seed), Path.Combine(outDir, $"phase_s{seed}.json"), true);
            File.Copy(StatePath(seed), Path.Combine(outDir, $"state_s{seed}.json"), true);
            var c = CultureInfo.InvariantCulture;
            var csv = new StringBuilder("seed,i,stage,bossFlag,net,scoreGold,atk,balls,gold,buys,colsMax,colsStart,colS,made,harvests,playMin,dur,bossLeft,weather,boss,bossRes,bought,nextCost,nextKey\n");
            foreach (var x in rows)
                csv.Append(string.Join(",", x.seed, x.i, x.stage, x.bossFlag, x.net.ToString("R", c), x.scoreGold.ToString("R", c), x.atk.ToString("R", c), x.balls, x.gold.ToString("R", c), x.buys, x.cols, x.colsStart, x.colS.ToString("R", c), x.made, x.harvests, x.playMin.ToString("R", c), x.dur.ToString("R", c), x.bossLeft.ToString("R", c), x.weather, x.boss, x.bossRes, x.bought, x.nextCost.ToString("R", c), x.nextKey)).Append('\n');
            File.WriteAllText(Path.Combine(outDir, $"rounds_s{seed}.csv"), csv.ToString(), new UTF8Encoding(false));
            return text;
        }
    }

    public static class BalanceR8
    {
        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        public static void T80()
        {
            int code = 0;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(root, Arg("-out") ?? "BalanceData/r8/test/T8-0/");
            try
            {
                var seeds = (Arg("-seeds") ?? "1,2,3,4,5,6").Split(',').Select(int.Parse).ToArray();
                int maxStage = Arg("-maxStage") is string ms ? int.Parse(ms) : 64;
                int bossEnd = Arg("-bossEnd") is string be ? int.Parse(be) : 54;
                float skill = Arg("-skill") is string k ? float.Parse(k, CultureInfo.InvariantCulture) : 0.6f;
                Directory.CreateDirectory(outDir);
                Defs.ApplyPreset("tuned", false);
                BalanceSim.BOSS_GATE = false;
                File.WriteAllText(Path.Combine(outDir, "cond.txt"), $"프리셋 {Defs.TUNE.id} · SKILL {skill} · 시드 {string.Join(",", seeds)} · BOSS_GATE {BalanceSim.BOSS_GATE} · OFF_NODES [{string.Join(",", Defs.OFF_NODES)}] · NODE.ContainsKey(homing)={Defs.NODE.ContainsKey("homing")} · maxStage {maxStage} · bossEnd R{bossEnd}\n");
                foreach (int sd in seeds)
                {
                    Defs.ApplyPreset("tuned", false);
                    BalanceSim.BOSS_GATE = false;
                    BalanceSim.ProgressionR8(sd, skill, maxStage, bossEnd, 600, outDir);
                    File.AppendAllText(Path.Combine(outDir, "progress.txt"), $"seed {sd} done {DateTime.Now:HH:mm:ss}\n");
                }
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(outDir, "error.txt"), e.ToString()); code = 1; }
            EditorApplicationExit(code);
        }

        static void EditorApplicationExit(int code) => UnityEditor.EditorApplication.Exit(code);
    }
}
