using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // BalanceSim 가격 맞추기를 에디터 업데이트마다 한 단계(한 판)씩 돌리는 백그라운드 작업.
    // MCP 호출 시간 제한에 걸리지 않고 지역 여러 개를 이어서 맞춘다. 진행은 Temp/balance_job.log에 남는다.
    // 스크립트를 다시 컴파일하면 작업이 끊긴다(정적 상태가 사라짐). 끊기면 마지막 체크포인트부터 다시 시작한다.
    public static class BalanceJob
    {
        public class ZonePlan { public int zone, a, b, maxIters = 8; public double finish = 0.85, damp = 0.7; }

        // 수렴 조건: 구간 판의 BUY_FRAC 이상에서 무언가를 사고, 구간 끝에 그 지역 노드를 DONE_FRAC 이상 샀다
        public const double BUY_FRAC = 0.6, DONE_FRAC = 0.95;

        static IEnumerator job;
        public static readonly string LogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/balance_job.log"));
        public static bool Running => job != null;

        static void Log(string s) => File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {s}\n");

        // fromCheckpoint: 첫 지역을 시작할 체크포인트 이름 (그 지역 시작 스테이지 직전 상태)
        public static string Start(List<ZonePlan> plan, string fromCheckpoint, int seeds = 3)
        {
            if (Running) return "이미 돌고 있음";
            File.WriteAllText(LogPath, "");
            job = Run(plan, fromCheckpoint, seeds);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Log($"시작: 지역 {plan.Count}개 · 시드 {seeds}개");
            return "시작";
        }

        public static void Stop() { job = null; EditorApplication.update -= Tick; Log("중지"); }

        static void Tick()
        {
            if (job == null) { EditorApplication.update -= Tick; return; }
            try { if (!job.MoveNext()) { job = null; EditorApplication.update -= Tick; } }
            catch (Exception e) { Log("오류: " + e); Stop(); }
        }

        // 지역 하나를 시드마다 끝까지 돌린다 (한 판 = 한 단계). checkpoint가 null이면 새 게임부터
        static IEnumerator RunZone(ZonePlan z, string checkpoint, int seeds)
        {
            if (checkpoint != null) BalanceSim.LoadCheckpoint(checkpoint, seeds);
            for (int sd = 1; sd <= seeds; sd++)
                for (int st = z.a; st <= z.b; st++)
                {
                    BalanceSim.Progression(1, checkpoint != null || st > z.a, sd, BalanceSim.SKILL, 1e9, 1000, false, z.b);
                    yield return null;
                }
        }

        static IEnumerator Run(List<ZonePlan> plan, string cp, int seeds)
        {
            foreach (var z in plan)
            {
                string name = Defs.THEMES[z.zone].n;
                for (int it = 1; ; it++)
                {
                    var run = RunZone(z, cp, seeds);
                    while (run.MoveNext()) yield return null;
                    var (buy, done) = BalanceSim.ZoneStats(z.zone, z.a, z.b, seeds);
                    bool ok = buy >= BUY_FRAC && done >= DONE_FRAC;
                    Log($"{name} 반복 {it}: 산 판 {buy:P0} · 구간 끝 구매율 {done:P0}{(ok ? " → 수렴" : "")}");
                    if (ok || it > z.maxIters) break;
                    Log("   " + BalanceSim.TuneZone(z.zone, z.a, z.b, z.finish, z.damp, seeds));
                    yield return null;
                }
                cp = "z" + (z.zone + 1);
                BalanceSim.SaveCheckpoint(cp, seeds);
                Log($"{name} 완료 → 체크포인트 {cp}\n" + BalanceSim.PhaseReport(1));
                yield return null;
            }
            Log("끝 · " + BalanceSim.WritePriceFile());
        }
    }
}
