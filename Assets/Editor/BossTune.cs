using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // 보스 체력 배율 맞추기 (봇 기준 도전 횟수). 보스를 앞에서부터 하나씩, 후보 배율마다 시드 여러 개로 새 게임부터 그 보스를 잡을 때까지 돌려
    // 도전 횟수가 [lo, hi] 안인 시드가 가장 많은 배율을 고른다(같으면 중앙값이 가운데에 가까운 쪽). 뒤 보스는 앞 보스를 고른 값으로 둔 채 맞춘다.
    // 고른 값은 로그에만 남기고, 끝나거나 멈추면 원래 값으로 되돌린다 — DB에는 사람이 넣는다(keepResult = true면 메모리에 남김, 도메인 리로드 때 사라짐).
    // 에디터 업데이트마다 한 시드씩 돈다. 진행은 Temp/boss_tune.log
    // BalanceJob과 같은 Temp 상태 파일(시드 1~)을 쓰므로 함께 돌리지 않는다
    public static class BossTune
    {
        public class Target { public string id; public int stage, lo, hi; public double[] cands; }   // id "loop" = BOSS_LOOP_HP_MUL (보스가 한 바퀴 돈 뒤)

        public const double SEED_WALL_SEC = 60;   // 시드 한 번의 시간 상한. 넘으면 "미완료"로 따로 센다
        const int NOT_DONE = int.MinValue;

        public static readonly string LogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/boss_tune.log"));
        static IEnumerator job;
        static Dictionary<string, double> origVals;
        static bool keep;
        public static bool Running => job != null;
        static void Log(string s) => File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {s}\n");

        public static string Start(List<Target> targets, int seeds = 6, bool keepResult = false)
        {
            if (Running) return "이미 돌고 있음";
            if (BalanceJob.Running) return "BalanceJob이 돌고 있음 (같은 Temp 상태 파일을 씀)";
            if (seeds < 1) return "시드는 1개 이상";
            foreach (var t in targets)
            {
                if (t.cands == null || t.cands.Length == 0 || t.lo > t.hi) return $"{t.id}@{t.stage}: 후보나 목표 범위가 잘못됨";
                if (!Defs.IsBossStage(t.stage)) return $"{t.stage}는 보스 스테이지가 아님";
                bool loop = t.stage / Defs.BOSS_EVERY > Defs.BOSSES.Count;
                if (t.id == "loop" ? !loop : (loop || !Defs.BOSS.ContainsKey(t.id) || Defs.BossAt(t.stage).id != t.id))
                    return $"{t.id}@{t.stage}: 그 스테이지 보스 체력에 쓰이는 값이 아님";
            }
            File.WriteAllText(LogPath, "");
            origVals = targets.Select(t => t.id).Distinct().ToDictionary(id => id, Get);
            keep = keepResult;
            job = Run(targets, seeds);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return "시작";
        }

        public static void Stop() { Finish("중지"); }

        // 끝·중지·오류 모두 여기서: keep이 아니거나 끝까지 못 갔으면 원래 값으로 되돌린다
        static void Finish(string why, bool completed = false)
        {
            job = null; EditorApplication.update -= Tick;
            if (origVals != null && !(completed && keep))
            {
                foreach (var kv in origVals) Set(kv.Key, kv.Value);
                Log($"{why} · 보스 배율을 원래 값으로 되돌림");
            }
            else if (origVals != null) { Log($"{why} · 고른 값을 메모리에 남김 (도메인 리로드 때 사라짐)"); Debug.LogWarning("[BossTune] 고른 보스 배율이 메모리에만 남아 있음"); }
            origVals = null;
        }

        static void Tick()
        {
            if (job == null) { EditorApplication.update -= Tick; return; }
            try { if (!job.MoveNext()) Finish("끝", true); }
            catch (Exception e) { Log("오류: " + e); Finish("오류로 멈춤"); }
        }

        static double Get(string id) => id == "loop" ? Defs.BOSS_LOOP_HP_MUL : Defs.BOSS[id].hpMul;
        static void Set(string id, double v) { if (id == "loop") Defs.BOSS_LOOP_HP_MUL = v; else Defs.BOSS[id].hpMul = v; }

        // 새 게임부터 그 보스를 잡을 때까지 돌려 도전 횟수. 시간 상한에 걸려 보스를 못 넘었으면 NOT_DONE
        public static int Tries(int seed, int stage)
        {
            int keepCk = BalanceSim.CkptEvery; BalanceSim.CkptEvery = 0;
            try { BalanceSim.Progression(5000, false, seed, BalanceSim.SKILL, SEED_WALL_SEC, 1, stage); }
            finally { BalanceSim.CkptEvery = keepCk; }
            if (!BalanceSim.LastHitStop) return NOT_DONE;
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, $"../Temp/balance_phase_s{seed}.json"));
            var ph = JsonConvert.DeserializeObject<BalanceSim.PhaseLog>(File.ReadAllText(path));
            int tries = 0;
            for (int i = 0; i < ph.stageR.Count; i++)
                if (ph.stageR[i] == stage && (ph.bossR[i] == 1 || ph.bossR[i] == -1)) { tries++; if (ph.bossR[i] == 1) return tries; }
            return NOT_DONE;
        }

        static IEnumerator Run(List<Target> targets, int seeds)
        {
            Log($"시작: {string.Join(" · ", targets.Select(t => $"{t.id}@{t.stage} 목표 {t.lo}~{t.hi}"))} · 시드 {seeds}개");
            foreach (var t in targets)
            {
                double best = Get(t.id); int bestIn = -1; double bestDist = double.MaxValue;
                foreach (double v in t.cands)
                {
                    Set(t.id, v);
                    var r = new List<int>();
                    for (int sd = 1; sd <= seeds; sd++) { r.Add(Tries(sd, t.stage)); yield return null; }
                    int inRange = r.Count(x => x >= t.lo && x <= t.hi);
                    var sorted = r.Select(x => x == NOT_DONE ? int.MaxValue : x).OrderBy(x => x).ToList();
                    double med = sorted[sorted.Count / 2], dist = med == int.MaxValue ? double.MaxValue : Math.Abs(med - (t.lo + t.hi) / 2.0);
                    Log($"{t.id}@{t.stage} 배율 {v:G4}: [{string.Join(",", r.Select(x => x == NOT_DONE ? "미완료" : x.ToString()))}] 목표 안 {inRange}/{seeds}");
                    if (inRange > bestIn || (inRange == bestIn && dist < bestDist)) { bestIn = inRange; bestDist = dist; best = v; }
                }
                Set(t.id, best);
                Log($"→ {t.id} 고름 {best:G4} (원래 {origVals[t.id]:G4}, 목표 안 {bestIn}/{seeds})");
            }
            Log("고른 값 · " + string.Join(" · ", targets.Select(t => t.id).Distinct().Select(id => $"{id} {Get(id):G4}")));
        }
    }
}
