var BSim = typeof(MoreMush.EditorTools.BalanceSim);
string root = "C:/Project/MoreMustMush/";
string outDir = root + "BalanceData/r5/test/T5-0/";
if (System.IO.File.Exists(outDir + "running.txt") && System.IO.File.ReadAllText(outDir + "running.txt").StartsWith("start")) return "already running";
var prevGate = MoreMush.EditorTools.BalanceSim.BOSS_GATE;
var prevPreset = MoreMush.Defs.TUNE.id;
MoreMush.Defs.ApplyPreset("tuned", false);
MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
System.IO.File.WriteAllText(outDir + "running.txt", System.DateTime.Now.ToString("HH:mm:ss") + " start prevPreset=" + prevPreset);
int seed = 1, total = 0; var log = new System.Text.StringBuilder(); bool first = true;
System.Action finish = null;
System.Action tick = null;
tick = () =>
{
    try
    {
        if (seed > 6)
        {
            MoreMush.EditorTools.BalanceSim.BOSS_GATE = prevGate; MoreMush.Defs.ApplyPreset(prevPreset, false);
            System.IO.File.WriteAllText(outDir + "done.txt", System.DateTime.Now.ToString("HH:mm:ss") + " preset restored " + prevPreset);
            System.IO.File.WriteAllText(outDir + "running.txt", "stopped");
            UnityEditor.EditorApplication.update -= tick.Invoke; return;
        }
        MoreMush.Defs.ApplyPreset("tuned", false);
        MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
        log.Append(MoreMush.EditorTools.BalanceSim.Progression(400 - total, !first, seed, MoreMush.EditorTools.BalanceSim.SKILL, 1.0, 1, 55));
        first = false;
        int n = MoreMush.EditorTools.BalanceSim.LastPlayed; total += n;
        System.IO.File.WriteAllText(outDir + "progress.txt", "seed " + seed + " rounds " + total + " " + System.DateTime.Now.ToString("HH:mm:ss"));
        if (MoreMush.EditorTools.BalanceSim.LastHitStop || total >= 400 || n == 0)
        {
            System.IO.File.WriteAllText(outDir + "t50_s" + seed + ".log", log.ToString());
            System.IO.File.Copy(root + "Temp/balance_phase_s" + seed + ".json", outDir + "phase_s" + seed + ".json", true);
            System.IO.File.Copy(root + "Temp/balance_state_s" + seed + ".json", outDir + "state_s" + seed + ".json", true);
            seed++; total = 0; first = true; log.Clear();
        }
    }
    catch (System.Exception e)
    {
        System.IO.File.WriteAllText(outDir + "error.txt", e.ToString());
        MoreMush.EditorTools.BalanceSim.BOSS_GATE = prevGate; MoreMush.Defs.ApplyPreset(prevPreset, false);
        System.IO.File.WriteAllText(outDir + "running.txt", "stopped");
        UnityEditor.EditorApplication.update -= tick.Invoke;
    }
};
UnityEditor.EditorApplication.update += tick.Invoke;
return "started; playmode=" + UnityEngine.Application.isPlaying;
