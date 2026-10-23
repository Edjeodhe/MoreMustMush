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
    // 8회차 T8-1 측정기 (test 에이전트). 게임 로직·DB는 건드리지 않는다. 유도(homing)는 측정용으로만 OFF_NODES에서 잠시 빼서 켠다(끝나면 되돌림).
    // 명령줄: Unity.exe -batchmode -nographics -projectPath <p> -executeMethod MoreMush.EditorTools.BalanceR8b.T81 -logFile <log> -out BalanceData/r8/test/T8-1/ -items snap,ball,cols,eff,c24,m,legacy [-seeds 1,2,3,4,5,6] [-reps 6]
    public static partial class BalanceSim
    {
        static readonly int[] SNAP_ROUNDS = { 10, 13, 14, 16, 20, 23, 24, 26, 30, 33, 34, 36, 40, 43, 44, 46, 50, 53, 54 };

        // 봇(tuned, SKILL skill)을 새 게임부터 돌리며 SNAP_ROUNDS 판을 시작하기 직전의 세이브(JSON)를 남긴다. 표시 54(보스 처치)까지
        public static Dictionary<int, string> CaptureSnaps(int seed, float skill)
        {
            LoadPriceTable();
            return Sandbox(() =>
            {
                var snaps = new Dictionary<int, string>();
                G = new SaveData();
                botFarm = 0; botBoughtSinceFail = false;
                for (int i = 0; i < 400; i++)
                {
                    if (StageNow() > 64) break;
                    if (SNAP_ROUNDS.Contains(StageNow()) && !snaps.ContainsKey(StageNow())) snaps[StageNow()] = JsonConvert.SerializeObject(G);
                    var rnd = new System.Random(seed * 1000 + G.rounds); UnityEngine.Random.InitState(seed * 1000 + G.rounds);
                    if (IsBossStage(ContentNow()) && !G.bossOn && (botBoughtSinceFail || ++botFarm >= 3)) { G.bossOn = true; botFarm = 0; botBoughtSinceFail = false; }
                    var r = PlayRoundR8(rnd, skill, out int cs, out float colS);
                    int before = GoldLevels();
                    BulkBuy(new[] { "ed", "md", "ps" }, 0, false);
                    G.play += r.duration + OVERHEAD_SEC;
                    if (GoldLevels() - before > 0) botBoughtSinceFail = true;
                    if (r.boss != null && !r.bossCleared) botBoughtSinceFail = false;
                    if (r.boss != null && r.bossCleared && r.stage >= 54) break;
                }
                return snaps;
            });
        }

        public class BossOut { public string boss, weather; public double dur, atk, maxHp, left, fightT, gross, netDps; public int balls; public bool cleared; }

        // 저장(json)에서 보스 판 하나. over = 노드 레벨 덮어쓰기(replaceAll이면 노드를 통째로 바꿈), hpScale = 보스 체력 배율(DPS 재기용)
        public static BossOut BossRound(string json, int stage, int rndSeed, float skill, Dictionary<string, int> over, bool replaceAll, double hpScale, double? gold = null)
        {
            double origLoop = BOSS_LOOP_HP_MUL; var origMul = BOSSES.ToDictionary(b => b.id, b => b.hpMul);
            try
            {
                if (hpScale != 1) { BOSS_LOOP_HP_MUL *= hpScale; foreach (var b in BOSSES) b.hpMul *= hpScale; }
                return Sandbox(() =>
                {
                    var rnd = new System.Random(rndSeed); UnityEngine.Random.InitState(rndSeed);
                    var g = JsonConvert.DeserializeObject<SaveData>(json);
                    if (replaceAll) g.nodes = new Dictionary<string, int>();
                    if (over != null) foreach (var kv in over) g.nodes[kv.Key] = kv.Value;
                    g.stage = stage; g.rounds = stage - 1; g.bossOn = true;
                    if (gold.HasValue) g.gold = gold.Value;
                    G = g; g.theme = LatestTheme().id;
                    var boss = BossAt(ContentNow());
                    string w = RollWeather(true);
                    var R = RoundSim.Start(w, G.theme, boss);
                    double durS = R.st.duration, atk = R.st.atk; int balls = R.st.permBalls;
                    var bm = R.BossMain; R.Launch(Mathf.Atan2(bm.y - RoundSim.LAUNCH.y, bm.x - RoundSim.LAUNCH.x) + (float)(rnd.NextDouble() * 0.2 - 0.1));
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
                    double maxHp = 0, left = 0; foreach (var b in R.bosses) { maxHp += b.maxHp; left += Math.Max(0, b.hp); }
                    bool cleared = R.bossCleared; double clrT = R.bossClearT, runT = R.runT;
                    var sm = R.Finish();
                    double gross = 0; foreach (var kv in sm.dmgBySrc) gross += kv.Value.dmg;
                    double fightT = cleared ? clrT : runT; if (fightT <= 0) fightT = runT;
                    double leftPct = maxHp > 0 ? left / maxHp : 0;
                    return new BossOut { boss = boss.id, weather = w, dur = durS, atk = atk, balls = balls, maxHp = maxHp, left = leftPct, cleared = cleared, fightT = fightT, gross = gross / Math.Max(0.01, fightT), netDps = maxHp * (1 - leftPct) / Math.Max(0.01, fightT) };
                });
            }
            finally { BOSS_LOOP_HP_MUL = origLoop; foreach (var b in BOSSES) b.hpMul = origMul[b.id]; }
        }

        // 일반 판 하나(보스 아님). 군락 계측 포함
        public static (double net, int cols, int colsStart, float colS, int balls, int harvests, double atk) NormalRound(string json, int stage, int rndSeed, float skill, Dictionary<string, int> over)
        {
            return Sandbox(() =>
            {
                var rnd = new System.Random(rndSeed); UnityEngine.Random.InitState(rndSeed);
                var g = JsonConvert.DeserializeObject<SaveData>(json);
                if (over != null) foreach (var kv in over) g.nodes[kv.Key] = kv.Value;
                g.stage = stage; g.rounds = stage - 1;
                G = g;
                var r = PlayRoundR8(rnd, skill, out int cs, out float colS);
                return (r.net, r.cols, cs, colS, r.balls, r.harvests, r.atk);
            });
        }
    }

    public static class BalanceR8b
    {
        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static string F(double v) => v.ToString("R", C);

        // theory#12 ③ 상태 (design8_out.json E5 사람: 판 13 직전·판 23 직전, 14 상태는 theory#12 목록 그대로)
        static readonly Dictionary<string, int> HUMAN14 = new Dictionary<string, int> { ["power"] = 4, ["ed_score"] = 3, ["ed_cols"] = 3, ["ed_price"] = 3, ["md_bar"] = 3, ["md_map"] = 2, ["ps_atk"] = 2, ["ps_ball"] = 1, ["ps_dur"] = 3, ["ps_size"] = 1, ["dev_stump"] = 1, ["dev_moss"] = 1, ["ed_rare0"] = 1, ["ed_regen"] = 1, ["md_combo"] = 1 };
        static readonly Dictionary<string, int> HUMAN24 = new Dictionary<string, int> { ["power"] = 4, ["ed_score"] = 3, ["ed_cols"] = 4, ["ps_dur"] = 4, ["ed_price"] = 3, ["ps_atk"] = 4, ["md_bar"] = 3, ["ps_ball"] = 2, ["dev_stump"] = 1, ["md_map"] = 4, ["md_combo"] = 3, ["ed_regen"] = 3, ["ed_rare0"] = 3, ["dev_moss"] = 1, ["ps_size"] = 3, ["ps_spd"] = 3, ["md_kid"] = 3, ["ed_bonus"] = 3, ["accel"] = 4, ["dev_acorn"] = 1, ["pierce"] = 4, ["burst"] = 3, ["md_map2"] = 1, ["ed_score1p"] = 1, ["ed_bonus1p"] = 1, ["ps_bar"] = 1 };

        static Dictionary<int, string> LoadSnaps(string outDir, int seed)
        {
            string p = Path.Combine(outDir, $"snaps_s{seed}.json");
            if (File.Exists(p)) return JsonConvert.DeserializeObject<Dictionary<int, string>>(File.ReadAllText(p));
            Defs.ApplyPreset("tuned", false); BalanceSim.BOSS_GATE = false;
            var s = BalanceSim.CaptureSnaps(seed, 0.6f);
            File.WriteAllText(p, JsonConvert.SerializeObject(s));
            return s;
        }

        static void Prog(string outDir, string line) => File.AppendAllText(Path.Combine(outDir, "progress81.txt"), $"[{DateTime.Now:HH:mm:ss}] {line}\n");

        public static void T81()
        {
            int code = 0;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(root, Arg("-out") ?? "BalanceData/r8/test/T8-1/");
            Directory.CreateDirectory(outDir);
            var items = (Arg("-items") ?? "snap").Split(',');
            var seeds = (Arg("-seeds") ?? "1,2,3,4,5,6").Split(',').Select(int.Parse).ToArray();
            int reps = Arg("-reps") is string rp ? int.Parse(rp) : 6;
            Prog(outDir, "start items=" + string.Join(",", items) + " DB " + Db.Data._source);
            foreach (var it in items)
            {
                try
                {
                    Defs.ApplyPreset("tuned", false); BalanceSim.BOSS_GATE = false;
                    switch (it)
                    {
                        case "snap": foreach (int sd in seeds) { LoadSnaps(outDir, sd); Prog(outDir, "snap s" + sd); } break;
                        case "ball": ItemBall(outDir, seeds, reps); break;
                        case "cols": ItemCols(outDir, seeds, reps); break;
                        case "eff": ItemEff(outDir); break;
                        case "c24": ItemC24(outDir, seeds, reps); break;
                        case "m": ItemM(outDir, seeds, reps); break;
                        case "c24b": ItemC24b(outDir, seeds, reps); break;
                        case "legacy": ItemLegacy(outDir, seeds, reps); break;
                    }
                    Prog(outDir, "done " + it);
                }
                catch (Exception e) { File.WriteAllText(Path.Combine(outDir, $"error_{it}.txt"), e.ToString()); Prog(outDir, "ERROR " + it); code = 1; }
            }
            UnityEditor.EditorApplication.Exit(code);
        }

        // ② 내부 10 시작 상태에서 ps_ball 0 vs 1 (같은 시드 난수 짝)
        static void ItemBall(string outDir, int[] seeds, int reps)
        {
            var sb = new StringBuilder("seed,rep,psBall,net,balls,harvests,colsStart,atk\n");
            foreach (int sd in seeds)
            {
                var sn = LoadSnaps(outDir, sd);
                foreach (int bl in new[] { 0, 1 })
                    for (int rep = 0; rep < Math.Max(reps, 4); rep++)
                    {
                        var r = BalanceSim.NormalRound(sn[10], 10, sd * 100 + rep, 0.6f, new Dictionary<string, int> { ["ps_ball"] = bl, ["ps_ball2"] = 0, ["ps_ball3"] = 0 });
                        sb.AppendLine(string.Join(",", sd, rep, bl, F(r.net), r.balls, r.harvests, r.colsStart, F(r.atk)));
                    }
                Prog(outDir, "ball s" + sd);
            }
            File.WriteAllText(Path.Combine(outDir, "ball.csv"), sb.ToString());
        }

        // ⑤ ed_cols L4(12)에서 표시 30·50 판(내부 34·54 직전 일반 판 상태: 스냅 33·53, 스테이지는 34·54로 둔 일반 판 시작만)
        static void ItemCols(string outDir, int[] seeds, int reps)
        {
            var sb = new StringBuilder("seed,snap,stage,rep,weather,zoom,maxCol,colsStart,unplaced,colS,colSMin,atMin,edCols,mdMap,mdMap2\n");
            foreach (int sd in seeds)
            {
                var sn = LoadSnaps(outDir, sd);
                foreach (int snapR in new[] { 33, 53 })
                {
                    for (int rep = 0; rep < Math.Max(reps, 20); rep++)
                    {
                        string line = BalanceSim.Sandbox(() =>
                        {
                            UnityEngine.Random.InitState(sd * 313 + snapR * 7 + rep);
                            var g = JsonConvert.DeserializeObject<SaveData>(sn[snapR]);
                            g.nodes["ed_cols"] = 4;
                            g.stage = snapR; g.rounds = snapR - 1; g.bossOn = false; G = g; g.theme = BalanceSim.PickZone();
                            string w = RollWeather(false);
                            var R = RoundSim.Start(w, g.theme, null);
                            float sMin = ColSMin(RoundSim.ZOOM);
                            return string.Join(",", sd, snapR, snapR, rep, w, F(RoundSim.ZOOM), R.st.maxCol, R.colonies.Count, R.st.maxCol - R.colonies.Count, F(R.colS), F(sMin), R.colS <= sMin + 1e-4f ? 1 : 0, Lv("ed_cols"), Lv("md_map"), Lv("md_map2"));
                        });
                        sb.AppendLine(line);
                    }
                }
                Prog(outDir, "cols s" + sd);
            }
            File.WriteAllText(Path.Combine(outDir, "cols.csv"), sb.ToString());
        }

        // ⑥ 가격 효율 보고 + SinkChecks
        static void ItemEff(string outDir)
        {
            var sb = new StringBuilder();
            sb.AppendLine(BalanceSim.EfficiencyReport());
            File.WriteAllText(Path.Combine(outDir, "efficiency.txt"), sb.ToString());
            string sink;
            try { sink = SinkChecks.Run(); } catch (Exception e) { sink = "실패: " + e.Message; }
            File.WriteAllText(Path.Combine(outDir, "sinkchecks.txt"), sink);
        }

        // ④ 보스 14(king) 사람 상태 · 24(cleaner) 사람 23 상태 / ps_atk2 1레벨 추가 상태 · 봇 그대로(lag 0)
        static void ItemC24(string outDir, int[] seeds, int reps)
        {
            var sb = new StringBuilder("state,skill,seed,rep,boss,weather,dur,atk,balls,maxHp,leftPct,cleared,fightT,grossDps,netDps\n");
            var plus = new Dictionary<string, int>(HUMAN24) { ["ps_atk2"] = 1 };
            // 각 상태: (이름, 스냅 판, 보스 판, 노드 덮어쓰기)
            var states = new List<(string name, int snap, int stage, Dictionary<string, int> nodes, bool all)>
            {
                ("h14", 13, 14, HUMAN14, true), ("h24", 23, 24, HUMAN24, true), ("h24+atk2", 23, 24, plus, true), ("bot24", 24, 24, null, false), ("bot14", 14, 14, null, false),
            };
            foreach (int sd in seeds)
            {
                var sn = LoadSnaps(outDir, sd);
                foreach (var st in states)
                    foreach (float sk in new[] { 0.6f, 1.0f })
                        for (int rep = 0; rep < Math.Max(reps, 6) * 2; rep++)
                        {
                            var o = BalanceSim.BossRound(sn[st.snap], st.stage, sd * 1000 + rep, sk, st.nodes, st.all, 1);
                            sb.AppendLine(string.Join(",", st.name, sk.ToString(C), sd, rep, o.boss, o.weather, F(o.dur), F(o.atk), o.balls, F(o.maxHp), F(o.left), o.cleared ? 1 : 0, F(o.fightT), F(o.gross), F(o.netDps)));
                        }
                Prog(outDir, "c24 s" + sd);
            }
            File.WriteAllText(Path.Combine(outDir, "c24.csv"), sb.ToString());
        }

        // T8-3b 보스 24 cleaner: 사람 상태 h24(ps_atk2 0·1레벨, 스냅 23) + 봇 lag 0·4·8(스냅 24-lag), SKILL 0.6·1.0, 체력 배율 1(지금 DB) / CLEAN_OLD(=0.20/0.41, 옛 체력 환산)
        const double CLEAN_OLD = 0.20 / 0.41;
        static void ItemC24b(string outDir, int[] seeds, int reps)
        {
            var sb = new StringBuilder("state,hpScale,skill,seed,rep,boss,weather,atk,balls,maxHp,leftPct,cleared,fightT,grossDps\n");
            var plus = new Dictionary<string, int>(HUMAN24) { ["ps_atk2"] = 1 };
            var states = new List<(string name, int snap, Dictionary<string, int> nodes, bool all)>
            {
                ("h24", 23, HUMAN24, true), ("h24+atk2", 23, plus, true), ("bot lag0", 24, null, false), ("bot lag4", 20, null, false), ("bot lag8", 16, null, false),
            };
            foreach (int sd in seeds)
            {
                var sn = LoadSnaps(outDir, sd);
                foreach (var st in states)
                    foreach (double hs in new[] { 1.0, CLEAN_OLD })
                        foreach (float sk in new[] { 0.6f, 1.0f })
                            for (int rep = 0; rep < reps; rep++)
                            {
                                if (!sn.ContainsKey(st.snap)) { Prog(outDir, $"c24b 스냅 없음 s{sd} {st.snap}"); break; }
                                var o = BalanceSim.BossRound(sn[st.snap], 24, sd * 1000 + 240 + rep, sk, st.nodes, st.all, hs);
                                sb.AppendLine(string.Join(",", st.name, F(hs), sk.ToString(C), sd, rep, o.boss, o.weather, F(o.atk), o.balls, F(o.maxHp), F(o.left), o.cleared ? 1 : 0, F(o.fightT), F(o.gross)));
                            }
                Prog(outDir, "c24b s" + sd);
            }
            File.WriteAllText(Path.Combine(outDir, "c24b.csv"), sb.ToString());
        }

        // ③ 유도 배율 M: 보스 34·44·54, lag 0·4·8 봇 상태, SKILL 1.0, homing 0 vs 2·3·4 (같은 난수 짝), 보스 체력 그대로 / ×50(끝까지 살려 DPS)
        static void ItemM(string outDir, int[] seeds, int reps)
        {
            var sb = new StringBuilder("seed,boss,lag,hpScale,homing,rep,bossId,weather,dur,atk,balls,maxHp,leftPct,cleared,fightT,grossDps,netDps\n");
            bool removed = OFF_NODES.Remove("homing");
            try
            {
                foreach (int sd in seeds)
                {
                    var sn = LoadSnaps(outDir, sd);
                    foreach (int boss in new[] { 34, 44, 54 })
                        foreach (int lag in new[] { 0, 4, 8 })
                        {
                            int snapR = boss - lag;
                            if (!sn.ContainsKey(snapR)) { Prog(outDir, $"m 스냅 없음 s{sd} {snapR}"); continue; }
                            foreach (double hs in new[] { 1.0, 50.0 })
                                foreach (int hm in new[] { 0, 2, 3, 4 })
                                    for (int rep = 0; rep < reps; rep++)
                                    {
                                        var o = BalanceSim.BossRound(sn[snapR], boss, sd * 1000 + boss * 10 + rep, 1.0f, new Dictionary<string, int> { ["homing"] = hm }, false, hs);
                                        sb.AppendLine(string.Join(",", sd, boss, lag, F(hs), hm, rep, o.boss, o.weather, F(o.dur), F(o.atk), o.balls, F(o.maxHp), F(o.left), o.cleared ? 1 : 0, F(o.fightT), F(o.gross), F(o.netDps)));
                                    }
                            File.WriteAllText(Path.Combine(outDir, "m.csv"), sb.ToString());
                            Prog(outDir, $"m s{sd} boss{boss} lag{lag}");
                        }
                }
            }
            finally { if (removed) OFF_NODES.Add("homing"); }
            File.WriteAllText(Path.Combine(outDir, "m.csv"), sb.ToString());
        }

        // ⑦ 옛 세이브: inf_atk 129 · inf_gold 117 + 골드 트리 최대 → 표시 60(내부 64)·70(내부 74) 보스 판을 여러 번 (도전 수 = 1 / 처치율)
        static void ItemLegacy(string outDir, int[] seeds, int reps)
        {
            var sb = new StringBuilder("variant,skill,seed,stage,rep,boss,atk,maxHp,leftPct,cleared,fightT,grossDps\n");
            foreach (int sd in seeds)
            {
                string json = File.ReadAllText(Path.Combine(outDir, $"state_s{sd}.json"));
                var tree = NODES.Where(n => !n.gem && !n.inf).ToDictionary(n => n.id, n => n.max);
                var gem = NODES.Where(n => n.gem).ToDictionary(n => n.id, n => n.max);
                var variants = new List<(string name, Dictionary<string, int> o)>();
                var a = new Dictionary<string, int>(tree) { ["inf_atk"] = 129, ["inf_gold"] = 117 };
                var b = new Dictionary<string, int>(a); foreach (var kv in gem) b[kv.Key] = kv.Value;
                var c = new Dictionary<string, int>(tree) { ["inf_atk"] = 0, ["inf_gold"] = 0 };
                variants.Add(("tree+inf129", a)); variants.Add(("tree+gem+inf129", b)); variants.Add(("tree+inf0", c));
                foreach (var v in variants)
                    foreach (int stage in new[] { 64, 74 })
                        foreach (float sk in new[] { 0.6f, 1.0f })
                            for (int rep = 0; rep < Math.Max(reps, 6); rep++)
                            {
                                var o = BalanceSim.BossRound(json, stage, sd * 1000 + stage + rep, sk, v.o, false, 1);
                                sb.AppendLine(string.Join(",", v.name, sk.ToString(C), sd, stage, rep, o.boss, F(o.atk), F(o.maxHp), F(o.left), o.cleared ? 1 : 0, F(o.fightT), F(o.gross)));
                            }
                Prog(outDir, "legacy s" + sd);
            }
            File.WriteAllText(Path.Combine(outDir, "legacy.csv"), sb.ToString());
        }
    }
}
