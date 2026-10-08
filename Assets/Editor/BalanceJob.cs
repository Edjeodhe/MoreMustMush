using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // BalanceSim 가격 맞추기를 돌리는 작업. 에디터에서는 업데이트마다 한 판씩(Start), 배치 모드에서는 한 번에(RunBlocking).
    // 노드에 지역 잠금이 없으므로 R1~R104 전체를 한 구간으로 보고 가격 곡선 하나를 맞춘다. 진행은 Temp/balance_job.log에 남는다.
    // 스크립트를 다시 컴파일하면 에디터 작업이 끊긴다(정적 상태가 사라짐).
    public static class BalanceJob
    {
        // finish: 기간(R104)의 몇 % 지점에서 다 사게 할지 (2시간 ≈ R104)
        // finish 0.85 ≈ R88: 판매 전부 가정이라 덜 파는 플레이어 몫으로 널널하게(사용자 2026-10-08)
        public class Plan { public int a = 1, b = 104, maxIters = 20; public double finish = 0.85, damp = 0.5; }

        // 수렴 조건: 기간 판의 BUY_FRAC 이상에서 무언가를 사고, 기간 끝에 골드 노드를 DONE_FRAC 이상 샀다
        public const double BUY_FRAC = 0.6, DONE_FRAC = 0.95;
        // 가격을 오름차순으로 늘어놓았을 때 바로 앞 대비 허용 배율. 곡선은 PRICE_STEP_MAX(1.4)로 맞추지만,
        // 효율 넘김으로 낮춘 가격이 빠진 자리와 유효숫자 반올림 때문에 약간 넘을 수 있어 2배까지 허용한다
        public const double STEP_LIMIT = 2.0;
        // 가장 빨리 끝낸 시드도 R(b × FINISH_MIN) 이후에 트리를 끝내야 한다. 2시간(R104)은 기준선이고 조금 빠른 건 괜찮다(사용자, 2026-10-08) → R84(약 90분)까지 허용
        public const double FINISH_MIN = 0.8;

        static IEnumerator job;
        public static readonly string LogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/balance_job.log"));
        public static bool Running => job != null;

        static void Log(string s) => File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {s}\n");

        public static string Start(Plan plan, int seeds = 3)
        {
            if (Running) return "이미 돌고 있음";
            File.WriteAllText(LogPath, "");
            Defs.ApplyPreset(BalanceSim.PRESET);
            job = Run(plan, seeds);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Log($"시작: R{plan.a}~R{plan.b} · 시드 {seeds}개");
            return "시작";
        }

        public static void Stop() { job = null; EditorApplication.update -= Tick; Log("중지"); }

        // 배치 모드용: 에디터 업데이트 없이 끝까지 한 번에 돌린다
        public static void RunBlocking(Plan plan, int seeds = 3)
        {
            File.WriteAllText(LogPath, "");
            Log($"시작(배치): R{plan.a}~R{plan.b} · 시드 {seeds}개 · 프리셋 {Defs.TUNE.id} · 지역 램프 {Defs.ZONE_RAMP}판 · 판매 {(BalanceSim.SELL ? "전부" : "안 함")} · 지역 이동 {(BalanceSim.GAME_ZONE ? "게임(해금 즉시)" : "봇(공격력 기준)")}");
            var e = Run(plan, seeds);
            while (e.MoveNext()) { }
        }

        static void Tick()
        {
            if (job == null) { EditorApplication.update -= Tick; return; }
            try { if (!job.MoveNext()) { job = null; EditorApplication.update -= Tick; } }
            catch (Exception e) { Log("오류: " + e); Stop(); }
        }

        // 시드마다 새 게임부터 R b까지 (한 판 = 한 단계)
        static IEnumerator RunAll(Plan p, int seeds)
        {
            for (int sd = 1; sd <= seeds; sd++)
                for (int st = 1; st <= p.b; st++)
                {
                    BalanceSim.Progression(1, st > 1, sd, BalanceSim.SKILL, 1e9, 1, false, p.b);
                    yield return null;
                }
        }

        static IEnumerator Run(Plan p, int seeds)
        {
            // 가격을 바꾸면 수입이 크게 바뀌어 "너무 쌈 ↔ 너무 비쌈"을 오갈 수 있다(R38 완료 ↔ 구매율 36%).
            // 양쪽 가격표를 한 번씩 보면 그 사이를 기하평균으로 이분한다
            // 완료 시점이 맞으면(R b×FINISH_MIN ~ R b) 가격 모양만 올리기로 다듬는다(EnforceShape). 끝까지 못 맞추면 시점이 맞은 가격표 중 가격 뜀이 가장 작은 것을 쓴다
            Dictionary<string, double[]> cheap = null, dear = null, best = null;
            double bestStep = double.MaxValue;
            bool converged = false;
            for (int it = 1; ; it++)
            {
                var run = RunAll(p, seeds);
                while (run.MoveNext()) yield return null;
                var (buy, done, fin) = BalanceSim.PriceStats(p.a, p.b, seeds);
                var (step, nodeStep, cOk, cTotal) = BalanceSim.PriceCheck();
                // 가격 곡선도 조건: 한 번에 STEP_LIMIT배 넘게 뛰지 않고, 단계 노드는 효율이 다 넘어가야 한다
                bool ok = buy >= BUY_FRAC && done >= DONE_FRAC && fin >= p.b * FINISH_MIN && step <= STEP_LIMIT && (!BalanceSim.ENFORCE_TIER_RULES || (nodeStep <= BalanceSim.NODE_STEP_MAX * 1.05 && cOk == cTotal));
                Log($"반복 {it}: 산 판 {buy:P0} · R{p.b} 구매율 {done:P0} · 가장 빠른 완료 R{fin} · 최대 가격 뜀 ×{step:F2} · 노드 안 ×{nodeStep:F2} · 효율 넘김 {cOk}/{cTotal}{(ok ? " → 수렴" : "")}");
                if (ok) { converged = true; break; }
                bool tooCheap = fin < p.b * FINISH_MIN, tooDear = done < DONE_FRAC || fin > p.b;
                if (!tooCheap && !tooDear && step < bestStep) { bestStep = step; best = BalanceSim.SnapshotPrices(); }
                if (it > p.maxIters) break;
                if (tooCheap) cheap = BalanceSim.SnapshotPrices();
                else if (tooDear) dear = BalanceSim.SnapshotPrices();
                if (!tooCheap && !tooDear)
                {
                    BalanceSim.EnforceShape();
                    Log("   완료 시점은 맞음: 가격 모양만 다듬기");
                }
                else if (cheap != null && dear != null)
                {
                    BalanceSim.BlendPrices(cheap, dear);
                    BalanceSim.EnforceShape();
                    Log("   이분: 너무 싼 가격표와 너무 비싼 가격표의 기하평균");
                }
                else Log("   " + BalanceSim.TunePrices(p.a, p.b, p.finish, p.damp, seeds));
                yield return null;
            }
            if (!converged && best != null) { BalanceSim.RestorePrices(best); Log($"수렴 못 함: 완료 시점이 맞은 가격표 중 가격 뜀이 가장 작은 것(×{bestStep:F2})을 씀"); var again = RunAll(p, seeds); while (again.MoveNext()) yield return null; }
            Log("끝 · " + BalanceSim.WritePriceFile());
        }
    }
}
