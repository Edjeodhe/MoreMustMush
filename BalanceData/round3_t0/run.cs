var BS = MoreMush.EditorTools.BalanceSim.BOSS_GATE;
var prev = MoreMush.Defs.TUNE.id; MoreMush.Defs.ApplyPreset("tuned", false);
MoreMush.EditorTools.BalanceSim.BOSS_GATE = true;
string root = "C:/Project/MoreMustMush/";
string outDir = root + "BalanceData/round3_t0/";
var sbAll = new System.Text.StringBuilder();
try
{
    for (int sd = 1; sd <= 3; sd++)
    {
        var log = new System.Text.StringBuilder();
        var states = new System.Text.StringBuilder();
        for (int k = 1; k <= 400; k++)
        {
            log.Append(MoreMush.EditorTools.BalanceSim.Progression(1, k > 1, sd, MoreMush.EditorTools.BalanceSim.SKILL, 1e9, 1, 0));
            states.AppendLine(System.IO.File.ReadAllText(root + "Temp/balance_state_s" + sd + ".json"));
            var ph = System.IO.File.ReadAllText(root + "Temp/balance_phase_s" + sd + ".json");
            int i = ph.IndexOf("\"stageR\":[");
            var tail = ph.Substring(i + 10, ph.IndexOf("]", i) - i - 10).Split(',');
            if (int.Parse(tail[tail.Length - 1]) > 15) break;
        }
        System.IO.File.WriteAllText(outDir + "t0_s" + sd + ".log", log.ToString());
        System.IO.File.WriteAllText(outDir + "states_s" + sd + ".jsonl", states.ToString());
        System.IO.File.Copy(root + "Temp/balance_phase_s" + sd + ".json", outDir + "phase_s" + sd + ".json", true);
    }
}
finally
{
    MoreMush.EditorTools.BalanceSim.BOSS_GATE = BS; MoreMush.Defs.ApplyPreset(prev, false);
    System.IO.File.WriteAllText(outDir + "done.txt", System.DateTime.Now.ToString("HH:mm:ss") + " preset restored " + prev);
}
return "done";
