using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // 명령줄 배치 모드로 가격 맞추기(Run) 또는 전체 진행 측정(Measure)을 돌린다. 에디터 창이 없어도 되고 MCP 호출 시간 제한도 없다.
    // Unity.exe -batchmode -nographics -projectPath <프로젝트> -executeMethod MoreMush.EditorTools.BalanceBatch.Run -logFile <로그>
    // Unity가 꺼질 때 Temp/가 지워지므로, 끝나면 측정 파일을 BalanceData/로 복사한다.
    public static class BalanceBatch
    {
        static readonly string Temp = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
        static readonly string Keep = Path.GetFullPath(Path.Combine(Application.dataPath, "../BalanceData"));

        // 가격 맞추기: R1~R104 전체. 끝나면 Assets/Scripts/Core/Defs.Prices.cs를 다시 쓴다
        public static void Run() => Batch(() =>
        {
            // 노드에 지역 잠금이 없으므로 R1~R104 전체를 가격 곡선 하나로 맞춘다
            BalanceJob.RunBlocking(new BalanceJob.Plan(), 3);
            Reports();
        });

        // 지금 가격표로 R1~R104를 새 게임부터 그대로 돌린다 (가격은 바꾸지 않음)
        public static void Measure() => Batch(() =>
        {
            File.WriteAllText(BalanceJob.LogPath, "");
            for (int sd = 1; sd <= 3; sd++) BalanceSim.Progression(104, false, sd, BalanceSim.SKILL, 1e9, 1000);
            var (buy, done, fin) = BalanceSim.PriceStats(1, 104, 3);
            File.AppendAllText(BalanceJob.LogPath, $"R1~R104: 산 판 {buy:P0} · R104 구매율 {done:P0} · 가장 빠른 완료 R{fin}\n");
            Reports();
        });

        static void Reports()
        {
            File.AppendAllText(BalanceJob.LogPath, "=== 효율 구간 (효과 +1당 골드)\n" + BalanceSim.EfficiencyReport());
            for (int sd = 1; sd <= 3; sd++)
                File.AppendAllText(BalanceJob.LogPath, $"=== 시드 {sd}\n{BalanceSim.PhaseReport(sd)}\n");
            File.AppendAllText(BalanceJob.LogPath, "배치 끝\n");
        }

        static void Batch(Action body)
        {
            int code = 0;
            try { body(); }
            catch (Exception e) { File.AppendAllText(BalanceJob.LogPath, "오류: " + e + "\n"); code = 1; }
            try
            {
                Directory.CreateDirectory(Keep);
                foreach (var f in Directory.GetFiles(Temp))
                {
                    string n = Path.GetFileName(f);
                    if (n.StartsWith("balance") || n == "price_table.json") File.Copy(f, Path.Combine(Keep, n), true);
                }
            }
            catch (Exception e) { Debug.LogError("BalanceData 복사 실패: " + e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
