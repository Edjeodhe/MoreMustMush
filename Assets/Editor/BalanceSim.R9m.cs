using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush.EditorTools
{
    // Test_JS2 병합(새 균사 트리) 뒤 후반 기준선 재측정. 명령줄:
    // Unity.exe -batchmode -nographics -projectPath <p> -executeMethod MoreMush.EditorTools.BalanceR9m.Batch -logFile <log> -seeds 1,2,3 -skills 0.6,1.0 -out BalanceData/r9/test/T9-merge/
    // 선택: -gems 1 (봇이 균사석 노드를 산다, 기본 1) · -lagbosses 10,20,..,100 (기본 60~100) · -maxrounds 1500
    public static class BalanceR9m
    {
        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }

        public static void Batch()
        {
            int code = 0;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(root, Arg("-out", "BalanceData/r9/test/T9-merge/"));
            try
            {
                Directory.CreateDirectory(outDir);
                Defs.ApplyPreset("tuned", false);
                BalanceSim.BOSS_GATE = false; BalanceSim.GAME_ZONE = true;
                BalanceSim.BOT_GEMS = Arg("-gems", "1") != "0";
                int maxRounds = int.Parse(Arg("-maxrounds", "1500"));
                int[] lagBosses = Arg("-lagbosses", "60,70,80,90,100").Split(',').Select(int.Parse).ToArray();
                File.AppendAllText(Path.Combine(outDir, "chain_progress.txt"),
                    $"조건: preset {TUNE.id} · GAME_ZONE {BalanceSim.GAME_ZONE} · BOSS_GATE {BalanceSim.BOSS_GATE} · TUT_ROUNDS {TUT_ROUNDS} · BOT_GEMS {BalanceSim.BOT_GEMS} · maxRounds {maxRounds} · DB {Db.Data._source}\n");
                foreach (var sk in Arg("-skills", "0.6").Split(','))
                    foreach (var sd in Arg("-seeds", "1").Split(','))
                    {
                        float skill = float.Parse(sk, System.Globalization.CultureInfo.InvariantCulture);
                        string tag = "k" + ((int)Math.Round(skill * 10)).ToString("00");
                        var t0 = DateTime.Now;
                        BalanceSim.ProgressionR9(int.Parse(sd), skill, 110, RoundOfStage(100), maxRounds, outDir, tag, 8, 3, lagBosses);
                        File.AppendAllText(Path.Combine(outDir, "chain_progress.txt"), $"{tag} seed {sd} done ({(DateTime.Now - t0).TotalSeconds:F0}s)\n");
                    }
            }
            catch (Exception e) { Debug.LogError(e); File.WriteAllText(Path.Combine(outDir, "error.txt"), e.ToString()); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
