using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // 명령줄: -seeds 4,5,6 (기본 1,2,3) · -skill 0.75 (바 오차, 기본 BalanceSim.SKILL)
        public static void Measure() => Batch(() =>
        {
            var seeds = (Arg("-seeds") ?? "1,2,3").Split(',').Select(int.Parse).ToArray();
            float skill = Arg("-skill") is string k ? float.Parse(k, System.Globalization.CultureInfo.InvariantCulture) : BalanceSim.SKILL;
            File.WriteAllText(BalanceJob.LogPath, $"프리셋: {Defs.TUNE.id} · 지역 램프 {Defs.ZONE_RAMP}판 · 판매 {(BalanceSim.SELL ? "전부" : "안 함")} · 지역 이동 {(BalanceSim.GAME_ZONE ? "게임(해금 즉시)" : "봇(공격력 기준)")} · 시드 {string.Join(",", seeds)} · 바 오차 {skill}\n");
            foreach (int sd in seeds) BalanceSim.Progression(104, false, sd, skill, 1e9, 1);   // 판마다 한 줄(공격력·지역·별) — verify#3
            if (seeds.SequenceEqual(new[] { 1, 2, 3 }))
            {
                var (buy, done, fin) = BalanceSim.PriceStats(1, 104, 3);
                File.AppendAllText(BalanceJob.LogPath, $"R1~R104: 산 판 {buy:P0} · R104 구매율 {done:P0} · 가장 빠른 완료 R{fin}\n");
            }
            Reports(seeds);
        });

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        static void Reports(int[] seeds = null)
        {
            File.AppendAllText(BalanceJob.LogPath, "=== 효율 구간 (효과 +1당 골드)\n" + BalanceSim.EfficiencyReport());
            foreach (int sd in seeds ?? new[] { 1, 2, 3 })
                File.AppendAllText(BalanceJob.LogPath, $"=== 시드 {sd}\n{BalanceSim.PhaseReport(sd)}\n");
            File.AppendAllText(BalanceJob.LogPath, "배치 끝\n");
        }

        // 밸런스 기준 프리셋. 에디터 PlayerPrefs에 남은 값(예전엔 spec)을 쓰지 않고 항상 이 값으로 돈다 (2026-10-08 사용자 결정: tuned = 새 플레이어 기본값).
        // 다른 프리셋으로 재려면 명령줄에 -preset spec
        static string PresetArg() => Arg("-preset") ?? BalanceSim.PRESET;

        static void Batch(Action body)
        {
            int code = 0;
            Defs.ApplyPreset(PresetArg(), false);
            if (Arg("-ramp") is string ramp) Defs.ZONE_RAMP = int.Parse(ramp);   // 구조안 S1 실험
            if (Environment.GetCommandLineArgs().Contains("-nosell")) BalanceSim.SELL = false;
            if (Environment.GetCommandLineArgs().Contains("-pickzone")) BalanceSim.GAME_ZONE = false;
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
