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
    // 9회차 T9-0 후반 기준선 측정기 (test 에이전트). ProgressionR8과 같은 순서이되
    //  - 지역 이동은 게임 규칙(해금되면 최신 지역, GAME_ZONE)
    //  - 판마다 지역·티어별 생긴/잡은 수·보스 판 maxHp/전투시간/총 DPS 기록
    //  - 보스 판 직전 lagN 판 전 상태로 보스 판 재측정(사람 근사)
    // 게임 로직·DB는 건드리지 않는다. 튜토리얼 판 수는 TUT_ROUNDS(표시 = 내부 − TUT_ROUNDS).
    public static partial class BalanceSim
    {
        public class R9Row
        {
            public int seed, i, stage, bossFlag, harvests, made, kill, buys, balls;
            public double net, atk, gold, playMin, dur, bossLeft, bossMaxHp, fightT, gross;
            public string zone, weather, bought, boss, bossRes;
        }

        static RoundResult PlayRoundR9(System.Random rnd, float skill, out double bossMaxHp, out double fightT, out double gross)
        {
            bossMaxHp = 0; fightT = 0; gross = 0;
            if (GAME_ZONE) { if (!THEME.TryGetValue(G.theme ?? "", out var th) || !ThemeOpen(th)) G.theme = LatestTheme().id; else G.theme = LatestTheme().id; }
            else G.theme = PickZone();
            var boss = BossRoundNext() ? BossAt(ContentNow()) : null;
            string w = RollWeather(boss != null);
            var R = RoundSim.Start(w, G.theme, boss);
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
            double clrT = 0, runT = 0;
            if (boss != null)
            {
                res.bossCleared = R.bossCleared;
                double left = 0, max = 0; foreach (var b in R.bosses) { left += Math.Max(0, b.hp); max += b.maxHp; }
                res.bossHpLeft = max > 0 ? left / max : 0; bossMaxHp = max;
                clrT = R.bossClearT; runT = R.runT;
            }
            var s = R.Finish();
            if (boss != null)
            {
                double g = 0; foreach (var kv in s.dmgBySrc) g += kv.Value.dmg;
                fightT = res.bossCleared ? clrT : runT; if (fightT <= 0) fightT = runT;
                gross = g / Math.Max(0.01, fightT);
            }
            res.net = s.net;
            res.score = s.score; res.scoreGold = s.scoreGold; res.bonus = s.bonusGold; res.value = s.value; res.harvests = s.harvests; res.spawned = R.shroomsMade; res.madeT = (int[])R.madeT.Clone(); res.killT = (int[])R.killT.Clone();
            return res;
        }

        // 균사석 노드 구매(봇): 일괄 강화(BulkBuy)는 균사석 레벨을 건너뛰므로, 사람이 손으로 사는 것처럼 살 수 있는 균사석 레벨을 가장 싼 것부터 산다 (r9m)
        public static bool BOT_GEMS = false;
        static void BotBuyGems()
        {
            for (int guard = 0; guard < 200; guard++)
            {
                Node best = null; double bc = double.PositiveInfinity;
                foreach (var n in NODES)
                {
                    if (!(n.br == "ed" || n.br == "md" || n.br == "ps")) continue;
                    var s = NodeState(n); if (s != "avail" && s != "owned") continue;
                    if (!ParentOk(n)) continue;
                    var c = NodeCost(n, Lv(n.id));
                    if (c.gem <= 0 || !CanAfford(c)) continue;
                    if (c.gem < bc) { bc = c.gem; best = n; }
                }
                if (best == null || !BuyNode(best, false)) break;
            }
        }

        // lagBosses: 표시 보스 번호(예 60,70..100) 목록, lag: 몇 판 전 상태, lagReps: 반복
        public static string ProgressionR9(int seed, float skill, int maxStage, int bossEnd, int maxRounds, string outDir, string tag, int lag, int lagReps, int[] lagBosses)
        {
            LoadPriceTable();
            var rows = new List<R9Row>();
            var snaps = new Dictionary<int, string>();
            var lagCsv = new StringBuilder("seed,skill,boss,lag,rep,bossId,weather,dur,atk,balls,maxHp,leftPct,cleared,fightT,grossDps,dpsOverMax\n");
            var wantSnap = new HashSet<int>(lagBosses.Select(b => RoundOfStage(b) - lag));
            var wantBoss = new HashSet<int>(lagBosses.Select(b => RoundOfStage(b)));
            var c = CultureInfo.InvariantCulture;
            string progPath = Path.Combine(outDir, $"progress_{tag}_s{seed}.txt");
            Directory.CreateDirectory(outDir);
            string text = Sandbox(() =>
            {
                G = new SaveData();
                var ph = new PhaseLog();
                var sb = new StringBuilder();
                botFarm = 0; botBoughtSinceFail = false;
                sb.AppendLine($"조건: 시드 {seed} · SKILL {skill} · 보스 {(BOSS_GATE ? "관문" : "실제 판(봇 조준)")} (BOSS_GATE {BOSS_GATE}) · 지역 이동 {(GAME_ZONE ? "게임" : "봇")} (GAME_ZONE {GAME_ZONE}) · 프리셋 {TUNE.id} · 튜토리얼 판 TUT_ROUNDS {TUT_ROUNDS} · 균사석 구매 {(BOT_GEMS ? "봇이 산다(가장 싼 것부터)" : "안 함")} · DB {Db.Data._source} · lag {lag}");
                for (int i = 0; i < maxRounds; i++)
                {
                    if (StageNow() > maxStage) { sb.AppendLine("멈춤: 스테이지 상한"); break; }
                    var rnd = new System.Random(seed * 1000 + G.rounds); UnityEngine.Random.InitState(seed * 1000 + G.rounds);
                    if (IsBossStage(ContentNow()) && !G.bossOn && (botBoughtSinceFail || ++botFarm >= 3)) { G.bossOn = true; botFarm = 0; botBoughtSinceFail = false; }
                    int stageNow0 = StageNow();
                    if (wantSnap.Contains(stageNow0) && !snaps.ContainsKey(stageNow0)) snaps[stageNow0] = JsonConvert.SerializeObject(G);
                    var r = PlayRoundR9(rnd, skill, out double bMax, out double fT, out double gr);
                    int before = GoldLevels();
                    var nodesBefore = new Dictionary<string, int>(G.nodes);
                    BulkBuy(new[] { "ed", "md", "ps" }, 0, false);
                    if (BOT_GEMS) BotBuyGems();
                    G.play += r.duration + OVERHEAD_SEC;
                    int bought = GoldLevels() - before;
                    if (bought > 0) botBoughtSinceFail = true;
                    if (r.boss != null && !r.bossCleared) botBoughtSinceFail = false;
                    var bl = new List<string>();
                    foreach (var kv in G.nodes) { nodesBefore.TryGetValue(kv.Key, out int b0); if (kv.Value != b0) bl.Add($"{kv.Key}:{b0}>{kv.Value}"); }
                    string bres = r.boss == null ? "" : (r.bossCleared ? "처치" : "실패");
                    int mk = r.madeT.Sum(), kl = r.killT.Sum();
                    ph.rounds++; if (bought > 0) ph.buyRounds++;
                    rows.Add(new R9Row
                    {
                        seed = seed, i = i + 1, stage = r.stage, bossFlag = r.boss != null ? (r.bossCleared ? 1 : -1) : IsBossStage(ContentStage(r.stage)) ? 2 : 0, net = r.net, atk = r.atk, balls = r.balls,
                        gold = G.gold, buys = bought, bought = string.Join(";", bl), made = mk, kill = kl, harvests = r.harvests, playMin = G.play / 60, dur = r.duration, bossLeft = r.bossHpLeft,
                        weather = r.weather, boss = r.boss ?? "", bossRes = bres, zone = r.zone, bossMaxHp = bMax, fightT = fT, gross = gr,
                    });
                    sb.AppendLine($"R{r.stage} | {G.play / 60:F1}분 | {r.duration:F0}s | {r.zone} | 공격력 {U.Fmt(r.atk)} | net {U.Fmt(r.net)} | 잡음 {kl}/{mk} | +{bought}" + (r.boss != null ? $" | 보스 {r.boss} {bres} 남음 {r.bossHpLeft:P0} DPS/max {(bMax > 0 ? gr / bMax : 0):F3}" : ""));
                    if (i % 10 == 0) File.AppendAllText(progPath, $"{DateTime.Now:HH:mm:ss} i {i} stage {r.stage} play {G.play / 60:F1}\n");
                    // 사람 근사: 이 보스 판의 lag 판 전 상태로 보스 판 재측정 (이 판 결과·난수에는 영향 없음)
                    if (r.boss != null && wantBoss.Contains(r.stage) && snaps.TryGetValue(r.stage - lag, out var js))
                    {
                        for (int rep = 0; rep < lagReps; rep++)
                        {
                            var o = BossRound(js, r.stage, seed * 1000 + r.stage * 10 + rep, 1.0f, null, false, 1.0);
                            lagCsv.AppendLine(string.Join(",", seed, skill.ToString(c), StageLabel(r.stage), lag, rep, o.boss, o.weather, o.dur.ToString("R", c), o.atk.ToString("R", c), o.balls, o.maxHp.ToString("R", c), o.left.ToString("R", c), o.cleared ? 1 : 0, o.fightT.ToString("R", c), o.gross.ToString("R", c), (o.maxHp > 0 ? o.gross / o.maxHp : 0).ToString("R", c)));
                        }
                        wantBoss.Remove(r.stage);
                        G.play += 0; // 상태 불변 확인용(Sandbox가 G를 복원한다)
                    }
                    if (r.boss != null && r.bossCleared && r.stage >= bossEnd) { sb.AppendLine($"멈춤: 보스 R{r.stage} 처치"); break; }
                }
                sb.AppendLine($"(무언가 산 판 {ph.buyRounds}/{ph.rounds})");
                return sb.ToString();
            });
            File.WriteAllText(Path.Combine(outDir, $"{tag}_s{seed}.log"), text, new UTF8Encoding(false));
            var csv = new StringBuilder("seed,i,stage,bossFlag,zone,net,atk,balls,gold,buys,made,kill,harvests,playMin,dur,bossLeft,bossMaxHp,fightT,grossDps,weather,boss,bossRes,bought\n");
            foreach (var x in rows)
                csv.Append(string.Join(",", x.seed, x.i, x.stage, x.bossFlag, x.zone, x.net.ToString("R", c), x.atk.ToString("R", c), x.balls, x.gold.ToString("R", c), x.buys, x.made, x.kill, x.harvests, x.playMin.ToString("R", c), x.dur.ToString("R", c), x.bossLeft.ToString("R", c), x.bossMaxHp.ToString("R", c), x.fightT.ToString("R", c), x.gross.ToString("R", c), x.weather, x.boss, x.bossRes, x.bought)).Append('\n');
            File.WriteAllText(Path.Combine(outDir, $"rounds_{tag}_s{seed}.csv"), csv.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDir, $"lag_{tag}_s{seed}.csv"), lagCsv.ToString(), new UTF8Encoding(false));
            return text;
        }
    }

    public static class BalanceR9d
    {
        // execute_code에서 동기 호출. tag 예: "k06"(SKILL 0.6), "k10"
        public static string Run(int seed, float skill, string tag, int lag = 8, int lagReps = 3)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(root, "BalanceData/r9/test/T9-0/");
            Defs.ApplyPreset("tuned", false);
            BalanceSim.BOSS_GATE = false; BalanceSim.GAME_ZONE = true;
            var t0 = DateTime.Now;
            BalanceSim.ProgressionR9(seed, skill, 110, RoundOfStage(100), 1500, outDir, tag, lag, lagReps, new[] { 60, 70, 80, 90, 100 });
            string line = $"{tag} seed {seed} done {DateTime.Now:HH:mm:ss} ({(DateTime.Now - t0).TotalSeconds:F0}s)\n";
            File.AppendAllText(Path.Combine(outDir, "chain_progress.txt"), line);
            return line;
        }
    }
}
