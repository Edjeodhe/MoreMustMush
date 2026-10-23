using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using MoreMush;

namespace MoreMush.EditorTools
{
    // T8-4 (test 에이전트): 에디터가 열린 채로 시드 하나씩 delayCall 체인으로 돌린다. 게임 로직·DB는 건드리지 않는다.
    // 작업 파일 BalanceData/r8/test/T8-4/jobs.txt 한 줄 = "seed skill maxStage bossEnd maxRounds outDir"
    public static class BalanceR8d
    {
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string JobsPath => Path.Combine(Root, "BalanceData/r8/test/T8-4/jobs.txt");
        static string ProgPath => Path.Combine(Root, "BalanceData/r8/test/T8-4/chain_progress.txt");

        public static void StartChain() { File.WriteAllText(ProgPath, ""); EditorApplication.delayCall += () => Step(0); }

        static void Step(int idx)
        {
            var lines = File.ReadAllLines(JobsPath).Where(l => l.Trim().Length > 0).ToArray();
            if (idx >= lines.Length) { File.AppendAllText(ProgPath, $"ALLDONE {DateTime.Now:HH:mm:ss}\n"); return; }
            var p = lines[idx].Split(' ');
            int seed = int.Parse(p[0]); float skill = float.Parse(p[1], CultureInfo.InvariantCulture);
            int maxStage = int.Parse(p[2]), bossEnd = int.Parse(p[3]), maxRounds = int.Parse(p[4]);
            string outDir = Path.Combine(Root, p[5]);
            try
            {
                Defs.ApplyPreset("tuned", false);
                BalanceSim.BOSS_GATE = false;
                var t0 = DateTime.Now;
                BalanceSim.ProgressionR8(seed, skill, maxStage, bossEnd, maxRounds, outDir);
                File.AppendAllText(ProgPath, $"job {idx} seed {seed} skill {skill} done {DateTime.Now:HH:mm:ss} ({(DateTime.Now - t0).TotalSeconds:F0}s)\n");
            }
            catch (Exception e) { File.AppendAllText(ProgPath, $"job {idx} ERROR {e}\n"); }
            EditorApplication.delayCall += () => Step(idx + 1);
        }

        public static string MigCheck()
        {
            var sb = new StringBuilder();
            bool prev = SaveIO.Suspended; SaveIO.Suspended = true;
            try
            {
                foreach (var (v, st, tut) in new[] { (9, 3, 3), (9, 4, 4), (9, 14, 4), (9, 50, 3), (8, 10, 4), (9, 1, 0) })
                {
                    string json = $"{{\"v\":{v},\"stage\":{st},\"tut\":{tut}}}";
                    var g = SaveIO.FromJson(json);
                    sb.AppendLine($"v{v} stage {st} tut {tut} -> v{g?.v} stage {g?.stage}");
                }
            }
            finally { SaveIO.Suspended = prev; }
            return sb.ToString();
        }
        public static void MigCheckToFile() { File.WriteAllText(Path.Combine(Root, "BalanceData/r8/test/T8-4/mig.txt"), MigCheck()); }
    }
}
