using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush.EditorTools
{
    // 콤보 상한 원인 측정 (시뮬 전용, 게임·DB 불변). maxCombo·수확·수입을 잰다.
    // A = 지금 게임, C = 버섯 체력 배율 1 (Defs.SIM_HP1). 판 하나(또는 진행 한 토막)씩 EditorApplication.update로 돌려 에디터가 멈추지 않는다.
    // 호출(리플렉션): StartMax("10,30,60,90", 3, 3)  전부 최대 빌드 → BalanceData/combo_probe.log
    //                 StartBot("10,30,60,90", 3, 3)  봇이 실제로 진행한 빌드(스테이지 도달 시점 저장) → BalanceData/combo_probe_bot.log
    public static class BalanceComboProbe
    {
        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "../BalanceData"));
        static string Temp => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
        static string LogFile;
        struct Job { public bool hp1; public int stage, seed, i; public string baseFile; }
        static readonly Queue<Job> q = new Queue<Job>();
        static readonly Dictionary<string, System.Random> rnds = new Dictionary<string, System.Random>();
        // 봇 진행 단계
        static List<int> botSeeds, botTargets; static int bi, ti, botRounds; static bool botResume, botPhase;
        static string botTargetsArg;
        const string Header = "config stage seed round zone atk cols spawned harvests maxCombo scoreGold net dur";

        public static void StartMax(string stages = "10,30,60,90", int seeds = 3, int rounds = 3)
        {
            Begin("combo_probe.log", $"전부 최대(균사석 제외) · 스테이지 {stages} · 시드 {seeds} · 판 {rounds}");
            foreach (var hp1 in new[] { false, true })
                foreach (int st in stages.Split(',').Select(int.Parse))
                    for (int sd = 1; sd <= seeds; sd++)
                        for (int i = 0; i < rounds; i++) q.Enqueue(new Job { hp1 = hp1, stage = st, seed = sd, i = i });
            EditorApplication.update += Tick;
        }

        public static void StartBot(string stages = "10,30,60,90", int seeds = 3, int rounds = 3)
        {
            Begin("combo_probe_bot.log", $"봇 진행 빌드 · 도달 스테이지 {stages} · 시드 {seeds} · 판 {rounds} (보스 실제 판, 판매 안 함)");
            botSeeds = Enumerable.Range(1, seeds).ToList(); botTargets = stages.Split(',').Select(int.Parse).ToList(); botTargetsArg = stages;
            bi = 0; ti = 0; botResume = false; botPhase = true; botRounds = rounds;
            EditorApplication.update += Tick;
        }

        static void Begin(string file, string desc)
        {
            LogFile = Path.Combine(Dir, file);
            File.WriteAllText(LogFile, $"콤보 측정 {DateTime.Now:s} · {desc}\n{Header}\n");
            q.Clear(); rnds.Clear(); botPhase = false;
            EditorApplication.update -= Tick;
        }

        static void Tick()
        {
            try
            {
                if (botPhase) { BotStep(); return; }
                if (q.Count == 0) { EditorApplication.update -= Tick; File.AppendAllText(LogFile, "끝\n"); return; }
                RunJob(q.Dequeue());
            }
            catch (Exception e) { File.AppendAllText(LogFile, "오류 " + e + "\n"); SIM_HP1 = false; q.Clear(); botPhase = false; EditorApplication.update -= Tick; }
        }

        static void BotStep()
        {
            int seed = botSeeds[bi], target = botTargets[ti];
            BalanceSim.Progression(100000, botResume, seed, BalanceSim.SKILL, 8, 1, target - 1);   // 스테이지 target에 닿으면 멈춘다
            botResume = true;
            if (!BalanceSim.LastHitStop) return;
            string keep = Path.Combine(Dir, $"combo_state_s{seed}_R{target}.json");
            File.Copy(Path.Combine(Temp, $"balance_state_s{seed}.json"), keep, true);
            File.AppendAllText(LogFile, $"# 저장 {Path.GetFileName(keep)}\n");
            if (++ti >= botTargets.Count) { ti = 0; bi++; botResume = false; }
            if (bi >= botSeeds.Count)
            {
                botPhase = false;
                foreach (var hp1 in new[] { false, true })
                    foreach (int sd in botSeeds) foreach (int t in botTargets)
                        for (int i = 0; i < botRounds; i++) q.Enqueue(new Job { hp1 = hp1, stage = t, seed = sd, i = i, baseFile = Path.Combine(Dir, $"combo_state_s{sd}_R{t}.json") });
            }
        }

        static void RunJob(Job j)
        {
            string key = $"{j.hp1}{j.stage}{j.seed}{j.baseFile != null}";
            if (!rnds.TryGetValue(key, out var rnd)) { rnd = new System.Random(j.seed * 7919 + j.stage); rnds[key] = rnd; }
            var r = BalanceSim.Sandbox(() =>
            {
                SIM_HP1 = j.hp1;
                try
                {
                    UnityEngine.Random.InitState(j.seed * 31 + j.stage + j.i);
                    // 도감·씨앗 해금이 있는 후반 상태에서 시작한다 (최대 모드: balance_state_s1.json의 노드를 전부 최대로)
                    G = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(j.baseFile ?? Path.Combine(Dir, "balance_state_s1.json")));
                    G.bossOn = false;
                    if (j.baseFile == null) { G.rounds = j.stage - 1; G.stage = j.stage; G.nodes = NODES.Where(x => !x.gem).ToDictionary(x => x.id, x => x.max); }
                    G.theme = LatestTheme().id;   // 지역 = 그 스테이지의 최신 지역
                    return BalanceSim.PlayRound(rnd, BalanceSim.SKILL, false);
                }
                finally { SIM_HP1 = false; }
            });
            File.AppendAllText(LogFile, $"{(j.hp1 ? "C" : "A")} {r.stage} {j.seed} {j.i} {r.zone} {r.atk:G4} {r.cols} {r.spawned} {r.harvests} {r.maxCombo} {r.scoreGold:G4} {r.net:G4} {r.duration:F0}\n");
        }
    }
}
