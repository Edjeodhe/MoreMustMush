using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush.EditorTools
{
    // 체크포인트: 가격을 바꿔 가며 같은 시드를 1판부터 다시 돌리는 반복을 줄인다.
    // CkptEvery판마다 진행 상태(세이브·기록·봇 상태·그때 가격표)를 Temp/balance_ckpt에 저장하고,
    // 다음 실행은 "바뀐 가격이 그때까지 가진 골드보다 늘 비쌌던" 마지막 체크포인트부터 이어 돈다.
    // 봇은 가장 싼 것부터 사므로(BulkBuy) 못 사는 가격은 구매 순서를 바꾸지 않고, 판 난수는 G.rounds로 정해져 결과가 1판부터 돌린 것과 같다.
    // 코드·DB·설정이 바뀌면 맞지 않으니 작업(BalanceJob)을 시작할 때마다 지운다(ClearCkpts).
    public static partial class BalanceSim
    {
        public static int CkptEvery = 5;
        static readonly string CkptDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/balance_ckpt"));
        static string CkptPath(int seed, int round) => Path.Combine(CkptDir, $"s{seed}_r{round}.json");

        class Ckpt
        {
            public SaveData g;
            public PhaseLog ph;
            public int farm;
            public bool bought;
            public Dictionary<string, double[]> prices;
        }

        static Dictionary<string, double[]> CurPrices() => NODES.Where(n => Priced(n) && n.costs != null).ToDictionary(n => n.id, n => (double[])n.costs.Clone());

        static void SaveCkpt(int seed, PhaseLog ph)
        {
            Directory.CreateDirectory(CkptDir);
            var c = new Ckpt { g = G, ph = ph, farm = botFarm, bought = botBoughtSinceFail, prices = CurPrices() };
            File.WriteAllText(CkptPath(seed, G.rounds), JsonConvert.SerializeObject(c));
        }

        public static void ClearCkpts()
        {
            if (Directory.Exists(CkptDir)) Directory.Delete(CkptDir, true);
        }

        // 지금 가격표로 그대로 쓸 수 있는가: 바뀐 레벨 가격(옛값·새값 중 작은 쪽)이 그때까지 강화를 살 때 가진 골드보다 모두 비싸야 한다.
        // 실제 가격은 Game.NodeCost와 같이 Ceiling(costs × COST_MUL)로 본다
        static double Gold(double c) => Math.Ceiling(c * COST_MUL);
        static bool CkptValid(Ckpt c)
        {
            foreach (var n in NODES.Where(Priced))
            {
                if (n.costs == null) continue;
                if (!c.prices.TryGetValue(n.id, out var old) || old.Length != n.costs.Length) return false;
                for (int L = 0; L < old.Length; L++)
                    if (old[L] != n.costs[L] && Math.Min(Gold(old[L]), Gold(n.costs[L])) <= c.ph.maxBuyGold) return false;
            }
            return true;
        }

        static Ckpt ReadCkpt(int seed, int round)
        {
            try { return JsonConvert.DeserializeObject<Ckpt>(File.ReadAllText(CkptPath(seed, round))); }
            catch (Exception e) { Debug.LogWarning($"[BalanceSim] 체크포인트 s{seed}_r{round}을 못 읽어 건너뜀: {e.Message}"); return null; }
        }

        // maxRound 이하에서 쓸 수 있는 가장 늦은 체크포인트를 상태 파일로 되살린다. 되살린 판 수(없으면 0)를 돌려준다.
        // 그보다 늦은 체크포인트는 지금 가격과 맞지 않으므로 지운다
        public static int ResumeFromCkpt(int seed, int maxRound)
        {
            if (CkptEvery <= 0 || !Directory.Exists(CkptDir)) return 0;
            LoadPriceTable();
            var rounds = Directory.GetFiles(CkptDir, $"s{seed}_r*.json")
                .Select(f => int.TryParse(Path.GetFileNameWithoutExtension(f).Substring($"s{seed}_r".Length), out int r) ? r : -1)
                .Where(r => r > 0).OrderByDescending(r => r).ToList();
            int use = 0;
            foreach (int r in rounds)
            {
                if (r > maxRound) continue;
                var c = ReadCkpt(seed, r);
                if (c == null || c.ph == null || c.prices == null || !CkptValid(c)) continue;
                File.WriteAllText(StatePath(seed), JsonConvert.SerializeObject(c.g));
                File.WriteAllText(PhasePath(seed), JsonConvert.SerializeObject(c.ph));
                botFarm = c.farm; botBoughtSinceFail = c.bought;
                use = r;
                break;
            }
            foreach (int r in rounds) if (r > use) File.Delete(CkptPath(seed, r));
            return use;
        }

        // 초반만 빠르게: 시드마다 새 게임부터 stopStage까지 돌려 스테이지 10(첫 보스) 도착 판·보스 도전 횟수·그때 공격력을 본다
        public static string EarlyCheck(int stopStage = 15, int seeds = 3)
        {
            var sb = new StringBuilder();
            int keep = CkptEvery; CkptEvery = 0;
            try
            {
                for (int sd = 1; sd <= seeds; sd++)
                {
                    Progression(400, false, sd, SKILL, 120, 1, stopStage);
                    var ph = JsonConvert.DeserializeObject<PhaseLog>(File.ReadAllText(PhasePath(sd)));
                    int first = FIRST_BOSS_STAGE;
                    int arrive = ph.stageR.IndexOf(first) + 1;
                    int tries = 0, clearAt = 0;
                    for (int i = 0; i < ph.stageR.Count; i++)
                        if (ph.stageR[i] == first && ph.bossR[i] != 0 && ph.bossR[i] != 2) { tries++; if (ph.bossR[i] == 1) { clearAt = i + 1; break; } }
                    double atk = arrive > 0 ? ph.atkR[arrive - 1] : 0;
                    sb.AppendLine($"시드 {sd}: 스테이지 {first} 도착 {(arrive > 0 ? $"{arrive}판" : "못 감")} · 공격력 {U.Fmt(atk)} · 보스 도전 {tries}번 {(clearAt > 0 ? $"→ {clearAt}판에 처치" : "→ 못 잡음")} · 끝 스테이지 {(ph.stageR.Count > 0 ? ph.stageR[ph.stageR.Count - 1] : 0)} ({ph.rounds}판)");
                }
            }
            finally { CkptEvery = keep; }
            return sb.ToString();
        }
    }
}
