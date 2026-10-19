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
        // tierEnds(2회차, 보스 관문 모드): 단계(초반·중반·후반)별 다 사는 판 {_, 20, 88, 172}. 넣으면 스테이지가 아니라 판 단위로 맞춘다(b = tierEnds[3])
        // order: 다시 맞출 단계. 1단계는 밸런스 3회차에서 가격 사다리를 DB에 고정해 배율 1로 두고 맞추지 않는다(theory R3-20, verify#17)
        // bisect(판 단위 모드 기본): 단계별 이분. false면 예전 감쇠 다시 매기기(TunePricesTiers, 진동함 — round2_fit1). startMul: 단계별 DB 가격 배율 시작값(verify 제안 e)
        public class Plan { public int a = 1, b = 104, maxIters = 20; public double finish = 0.85, damp = 0.5; public int[] tierEnds; public bool bisect = true; public double[] startMul = { 1, 1, 0.44, 3.8 }; public int[] order = { 2, 3 }; }
        // 판 단위 모드의 단계별 허용: 가장 빠른 시드가 목표 × FINISH_MIN 이후, 가장 느린 시드가 목표 × TIER_LATE 이내에 다 산다
        public const double TIER_LATE = 1.1;

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
            if (BossTune.Running) return "BossTune이 돌고 있음 (같은 Temp 상태 파일을 씀)";
            File.WriteAllText(LogPath, "");
            Defs.ApplyPreset(BalanceSim.PRESET, false);
            BalanceSim.ClearCkpts(); ResumedRounds = PlayedRounds = 0;
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
            BalanceSim.ClearCkpts(); ResumedRounds = PlayedRounds = 0;
            var e = Run(plan, seeds);
            while (e.MoveNext()) { }
        }

        static void Tick()
        {
            if (job == null) { EditorApplication.update -= Tick; return; }
            try { if (!job.MoveNext()) { job = null; EditorApplication.update -= Tick; } }
            catch (Exception e) { Log("오류: " + e); Stop(); }
        }

        // 에디터 업데이트 한 번에 돌릴 시간(초). 한 판씩만 돌면 창이 뒤에 있을 때 업데이트가 느려져 작업이 거의 멈춘다
        public const double TICK_SEC = 1.0;
        public static int ResumedRounds, PlayedRounds;   // 이번 작업에서 체크포인트로 건너뛴 판 · 실제로 돈 판

        // 시드마다 새 게임부터 R b까지 (스테이지 모드: 스테이지 b에서 멈춤 · 판 단위 모드: b판)
        // 지금 가격으로도 결과가 같은 체크포인트가 있으면 거기서 이어 돈다 (BalanceSim.Ckpt)
        static IEnumerator RunAll(Plan p, int seeds, int rounds = 0)
        {
            if (rounds <= 0) rounds = p.b;
            int stop = p.tierEnds != null ? 0 : p.b;
            double tick = Application.isBatchMode ? 1e9 : TICK_SEC;
            for (int sd = 1; sd <= seeds; sd++)
            {
                int done = BalanceSim.ResumeFromCkpt(sd, rounds);
                ResumedRounds += done;
                while (done < rounds)
                {
                    BalanceSim.Progression(rounds - done, done > 0, sd, BalanceSim.SKILL, tick, 1, stop);
                    if (BalanceSim.LastPlayed == 0)
                    {
                        if (!BalanceSim.LastHitStop) Log($"   시드 {sd}: {done}판에서 한 판도 못 돌고 멈춤 (멈출 스테이지가 아님)");
                        break;   // 멈출 스테이지에 닿음
                    }
                    done += BalanceSim.LastPlayed; PlayedRounds += BalanceSim.LastPlayed;
                    yield return null;
                }
            }
        }

        // 판 단위 모드의 판정: (수렴, 너무 쌈, 너무 비쌈, 로그 한 줄)
        static (bool ok, bool cheap, bool dear, string log) JudgeTiers(Plan p, int seeds)
        {
            var (buy, done, fast, slow) = BalanceSim.PriceStatsR(p.b, seeds);
            var (step, nodeStep, cOk, cTotal) = BalanceSim.PriceCheck();
            bool cheap = false, dear = done < DONE_FRAC;
            for (int t = 1; t <= 3; t++)
            {
                cheap |= fast[t] < p.tierEnds[t] * FINISH_MIN;
                dear |= slow[t] > p.tierEnds[t] * TIER_LATE;
            }
            bool ok = !cheap && !dear && buy >= BUY_FRAC && step <= STEP_LIMIT && (!BalanceSim.ENFORCE_TIER_RULES || (nodeStep <= BalanceSim.NODE_STEP_MAX * 1.05 && cOk == cTotal));
            string ends = string.Join(" · ", Enumerable.Range(1, 3).Select(t => $"{t}단계 R{fast[t]}~{(slow[t] == BalanceSim.NOT_DONE ? "미완료" : "R" + slow[t])}(목표 R{p.tierEnds[t]})"));
            return (ok, cheap, dear, $"산 판 {buy:P0} · R{p.b} 구매율 {done:P0} · {ends} · 최대 가격 뜀 ×{step:F2} · 노드 안 ×{nodeStep:F2} · 효율 넘김 {cOk}/{cTotal}");
        }

        // ===== 단계별 이분 (2회차 verify 제안 a·b·d·e) =====
        // 단계 1 → 2 → 3 차례로(Plan.order), 그 단계 노드 가격 배율 하나만 이분한다(로그 공간). 줄 상한 모드(BalanceSim.LINE_CAP)를 켠다:
        // 올림 모드에서는 공격력 줄 효율 넘김 때문에 1단계 줄 끝이 3단계 가격에 묶여 1단계 배율을 0에 가깝게 내려도 R25~26에 끝났다(round2_fit2·fit3, theory R2-13).
        // 상한 모드에서는 앞 단계 배율이 뒤 단계 줄 가격을 정하므로 앞에서부터 맞추고, 앞 단계를 다시 맞추면 뒤 단계도 다시 본다(verify R2-13 조건 2).
        // 시작 m1 0.2(verify 조건 5). 3단계 공격력 III·III+는 부모 잠금(ps_heavy·ps_crit2)으로 두 번 몰려 들어오는 것을 받아들인다(verify 조건 3 (다), 사용자 결정 전) 판정은 시드 중앙값이 목표 × FINISH_MIN ~ × TIER_LATE 안,
        // 가장 느린 시드가 목표 + max(5판, 15%) 안. 구간이 한쪽만 열려 있으면 한 번에 ×/÷TIER_STEP까지만 옮긴다.
        // 가격은 EnforceShape(올리기만)를 적용한 뒤 판정하고, 단계 3까지 정한 뒤 단계 1·2를 다시 판정해 어긋나면 좁은 폭으로 한 번 더 이분한다
        public const int TIER_ITERS = 8;
        public const double TIER_STEP = 3, TIER_STEP2 = 1.5, TIER_NARROW = 1.03;
        static int SlowLimit(int target) => target + Math.Max(5, (int)Math.Ceiling(target * 0.15));
        static string Rn(int r) => r == BalanceSim.NOT_DONE ? "미완료" : "R" + r;

        // -1 너무 쌈 · 0 맞음 · 1 너무 비쌈
        static int JudgeTier(Plan p, int t, int seeds, out string log)
        {
            var (med, slow) = BalanceSim.TierDone(t, seeds);
            int target = p.tierEnds[t];
            int r = med < target * FINISH_MIN ? -1 : med > target * TIER_LATE || slow > SlowLimit(target) ? 1 : 0;
            log = $"{t}단계 중앙 {Rn(med)} · 가장 느린 {Rn(slow)} (목표 R{target}: 중앙 R{Math.Ceiling(target * FINISH_MIN)}~R{Math.Floor(target * TIER_LATE)}, 느린 시드 ≤ R{SlowLimit(target)}) → {(r < 0 ? "쌈" : r > 0 ? "비쌈" : "맞음")}";
            return r;
        }

        static string Muls(double[] m) => $"배율 {m[1]:G4} · {m[2]:G4} · {m[3]:G4}";

        // 단계 t 이분. 끝나면 ok[t]에 결과를 남긴다(맞은 배율을 찾았거나, 못 찾았으면 마지막 가운데 값을 m[t]에 둔다)
        static IEnumerator BisectTier(Plan p, int seeds, double[] m, int t, double stepMax, bool[] ok)
        {
            double lo = 0, hi = double.PositiveInfinity;
            ok[t] = false;
            for (int it = 1; it <= TIER_ITERS; it++)
            {
                BalanceSim.ApplyTierMuls(m);
                var run = RunAll(p, seeds, SlowLimit(p.tierEnds[t]) + 1);
                while (run.MoveNext()) yield return null;
                int r = JudgeTier(p, t, seeds, out var line);
                Log($"{t}단계 이분 {it}: {Muls(m)} · {line}");
                if (r == 0) { ok[t] = true; yield break; }
                if (r < 0) lo = m[t]; else hi = m[t];
                if (lo > 0 && !double.IsInfinity(hi))
                {
                    m[t] = Math.Sqrt(lo * hi);
                    if (hi / lo < TIER_NARROW) { Log($"   구간 ×{hi / lo:F3}로 좁아짐: 가운데 {m[t]:G4}를 씀"); yield break; }
                }
                else m[t] = r < 0 ? m[t] * stepMax : m[t] / stepMax;
            }
            Log($"   {t}단계 {TIER_ITERS}번 안에 못 맞춤: 마지막 가운데 {m[t]:G4}를 씀");
        }

        static IEnumerator RunBisect(Plan p, int seeds)
        {
            var m = (double[])p.startMul.Clone();
            var ok = new bool[4];
            BalanceSim.LINE_CAP = true;
            Log($"단계별 이분 시작: {Muls(m)} (DB 가격 기준) · 순서 {string.Join("→", p.order)} · 목표 R{p.tierEnds[1]}·R{p.tierEnds[2]}·R{p.tierEnds[3]}");
            foreach (int t in p.order) { var e = BisectTier(p, seeds, m, t, TIER_STEP, ok); while (e.MoveNext()) yield return null; }
            // 다시 판정: 앞 단계 가격이 바뀌면 수입이 바뀌어 뒤 단계 완료가 움직인다
            for (int pass = 1; pass <= 3; pass++)
            {
                BalanceSim.ApplyTierMuls(m);
                var run = RunAll(p, seeds, Math.Max(p.b, SlowLimit(p.tierEnds[3]) + 1));
                while (run.MoveNext()) yield return null;
                var bad = new List<int>();
                for (int t = 1; t <= 3; t++) { int r = JudgeTier(p, t, seeds, out var line); Log($"다시 판정 {pass}: {line}"); if (r != 0) bad.Add(t); }
                if (bad.Count == 0) { Log($"→ 수렴: {Muls(m)}"); break; }
                if (pass == 3) { Log($"수렴 못 함(단계 {string.Join(",", bad)}): {Muls(m)}를 씀"); break; }
                // 앞 단계를 다시 맞추면 뒤 단계 줄 가격이 바뀌므로 가장 앞의 나쁜 단계부터 끝까지 다시 본다
                int first = p.order.FirstOrDefault(bad.Contains);
                if (first == 0) { Log($"나쁜 단계({string.Join(",", bad)})가 다시 맞추는 단계({string.Join(",", p.order)})에 없음: {Muls(m)}를 씀"); break; }
                foreach (int t in p.order.SkipWhile(x => x != first)) { var e = BisectTier(p, seeds, m, t, TIER_STEP2, ok); while (e.MoveNext()) yield return null; }
            }
            BalanceSim.ApplyTierMuls(m);
            var fin = RunAll(p, seeds, Math.Max(p.b, SlowLimit(p.tierEnds[3]) + 1));
            while (fin.MoveNext()) yield return null;
            Log("최종: " + Muls(m) + " · " + JudgeTiers(p, seeds).log);
            for (int t = 1; t <= 3; t++) { JudgeTier(p, t, seeds, out var line); Log("   " + line); }
            Log($"끝 · 돈 판 {PlayedRounds} · 체크포인트로 건너뛴 판 {ResumedRounds} · " + BalanceSim.WritePriceFile());
        }

        static IEnumerator Run(Plan p, int seeds)
        {
            BalanceSim.LINE_CAP = p.tierEnds != null && p.bisect;   // 정적 값이라 같은 에디터 세션의 이전 실행 값이 남지 않게 매번 정한다
            if (p.tierEnds != null && p.bisect) { var e = RunBisect(p, seeds); while (e.MoveNext()) yield return null; yield break; }
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
                bool ok, tooCheap, tooDear;
                double step = BalanceSim.PriceCheck().maxStep;
                if (p.tierEnds != null)
                {
                    string line;
                    (ok, tooCheap, tooDear, line) = JudgeTiers(p, seeds);
                    Log($"반복 {it}: {line}{(ok ? " → 수렴" : "")}");
                }
                else
                {
                    var (buy, done, fin) = BalanceSim.PriceStats(p.a, p.b, seeds);
                    var (_, nodeStep, cOk, cTotal) = BalanceSim.PriceCheck();
                    // 가격 곡선도 조건: 한 번에 STEP_LIMIT배 넘게 뛰지 않고, 단계 노드는 효율이 다 넘어가야 한다
                    ok = buy >= BUY_FRAC && done >= DONE_FRAC && fin >= p.b * FINISH_MIN && step <= STEP_LIMIT && (!BalanceSim.ENFORCE_TIER_RULES || (nodeStep <= BalanceSim.NODE_STEP_MAX * 1.05 && cOk == cTotal));
                    Log($"반복 {it}: 산 판 {buy:P0} · R{p.b} 구매율 {done:P0} · 가장 빠른 완료 R{fin} · 최대 가격 뜀 ×{step:F2} · 노드 안 ×{nodeStep:F2} · 효율 넘김 {cOk}/{cTotal}{(ok ? " → 수렴" : "")}");
                    tooCheap = fin < p.b * FINISH_MIN; tooDear = done < DONE_FRAC || fin > p.b;
                }
                if (ok) { converged = true; break; }
                if (!tooCheap && !tooDear && step < bestStep) { bestStep = step; best = BalanceSim.SnapshotPrices(); }
                if (it > p.maxIters) break;
                // 판 단위 모드는 단계마다 따로 판정해 "초반은 쌈 + 후반은 비쌈"이 함께 나올 수 있다. 전체 기하평균 이분으로는 못 고치므로 단계별 다시 매기기만 쓴다
                if (p.tierEnds != null && (tooCheap || tooDear)) { Log("   " + BalanceSim.TunePricesTiers(p.tierEnds, p.damp, seeds)); yield return null; continue; }
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
            Log($"끝 · 돈 판 {PlayedRounds} · 체크포인트로 건너뛴 판 {ResumedRounds} · " + BalanceSim.WritePriceFile());
        }
    }
}
