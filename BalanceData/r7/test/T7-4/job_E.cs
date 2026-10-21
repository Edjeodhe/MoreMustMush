// T7-E: 같은 시드 두 번 실행 결과 동일(결정성): 봇 Progression 30판 x2 (시드 1·2), 그리고 후반 상태 판 3개 x2
string outDir = "C:/Project/MoreMustMush/BalanceData/r7/test/T7-4/";
var prevGate = MoreMush.EditorTools.BalanceSim.BOSS_GATE; var prevPreset = MoreMush.Defs.TUNE.id;
var sb = new System.Text.StringBuilder();
try
{
    MoreMush.Defs.ApplyPreset("tuned", false); MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
    for (int seed = 1; seed <= 2; seed++)
    {
        string[] logs = new string[2];
        for (int run = 0; run < 2; run++)
        {
            MoreMush.Defs.ApplyPreset("tuned", false);
            string s = MoreMush.EditorTools.BalanceSim.Progression(30, false, seed, 0.6f, 600, 1, 0);
            var lines = new System.Collections.Generic.List<string>(); foreach (var l in s.Split('\n')) if (!l.StartsWith("(무언가 산 판")) lines.Add(l.TrimEnd());
            logs[run] = string.Join("\n", lines);
        }
        bool same = logs[0] == logs[1];
        sb.AppendLine("Progression 30판 시드 " + seed + ": 로그 " + logs[0].Length + "자, 해시 " + logs[0].GetHashCode() + " / " + logs[1].GetHashCode() + " 동일=" + same);
        System.IO.File.WriteAllText(outDir + "E_prog_s" + seed + "_run0.log", logs[0]); System.IO.File.WriteAllText(outDir + "E_prog_s" + seed + "_run1.log", logs[1]);
    }
}
finally { MoreMush.EditorTools.BalanceSim.BOSS_GATE = prevGate; MoreMush.Defs.ApplyPreset(prevPreset, false); }
// 후반 상태 판 3개 x 2
string json = System.IO.File.ReadAllText(outDir + "state_k06_s1.json");
for (int k = 0; k < 3; k++)
{
    string[] hs = new string[2];
    for (int run = 0; run < 2; run++)
    {
        hs[run] = MoreMush.EditorTools.BalanceSim.Sandbox<string>(() =>
        {
            var g = Newtonsoft.Json.JsonConvert.DeserializeObject<MoreMush.SaveData>(json);
            MoreMush.Game.G = g; if (MoreMush.Game.BossRoundNext()) g.bossOn = false;
            g.theme = MoreMush.EditorTools.BalanceSim.PickZone();
            int sv = 9000 + k; var rnd = new System.Random(sv); UnityEngine.Random.InitState(sv);
            var res = MoreMush.EditorTools.BalanceSim.PlayRound(rnd, 0.6f, false);
            var R = MoreMush.RoundSim.R;
            return res.net + "|" + res.harvests + "|" + res.spawned + "|" + res.score + "|" + res.maxCombo + "|" + string.Join(",", res.madeT);
        });
    }
    sb.AppendLine("판 " + k + ": " + hs[0] + " / " + hs[1] + " 동일=" + (hs[0] == hs[1]));
}
System.IO.File.WriteAllText(outDir + "E_determ.txt", sb.ToString());
return sb.ToString();
