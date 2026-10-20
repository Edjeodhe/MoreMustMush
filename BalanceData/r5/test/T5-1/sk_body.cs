var BS = typeof(MoreMush.EditorTools.BalanceSim);
string root = "C:/Project/MoreMustMush/";
string outDir = root + "BalanceData/r5/test/T5-1/";
if (System.IO.File.Exists(outDir + "running_sk.txt") && System.IO.File.ReadAllText(outDir + "running_sk.txt").StartsWith("start")) return "already running";
var prevGate = MoreMush.EditorTools.BalanceSim.BOSS_GATE;
var prevPreset = MoreMush.Defs.TUNE.id;
MoreMush.Defs.ApplyPreset("tuned", false);
MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
System.IO.File.WriteAllText(outDir + "running_sk.txt", System.DateTime.Now.ToString("HH:mm:ss") + " start prevPreset=" + prevPreset);
// (skill, seed) 순서: 0.6 시드 1~6, 0.3 시드 1~3
var jobs = new System.Collections.Generic.List<(float, int)>();
for (int s = 1; s <= 3; s++) jobs.Add((1.0f, s));
for (int s = 1; s <= 3; s++) jobs.Add((1.5f, s));
int ji = 0, total = 0; var log = new System.Text.StringBuilder(); bool first = true;
System.Action tick = null;
tick = () =>
{
    try
    {
        if (ji >= jobs.Count)
        {
            MoreMush.EditorTools.BalanceSim.BOSS_GATE = prevGate; MoreMush.Defs.ApplyPreset(prevPreset, false);
            System.IO.File.WriteAllText(outDir + "done_sk.txt", System.DateTime.Now.ToString("HH:mm:ss") + " preset restored " + prevPreset + " now=" + MoreMush.Defs.TUNE.id);
            System.IO.File.WriteAllText(outDir + "running_sk.txt", "stopped");
            UnityEditor.EditorApplication.update -= tick.Invoke; return;
        }
        var (skill, seed) = jobs[ji];
        string tag = "k" + (skill > 1.2f ? "15" : "10") + "_s" + seed;
        MoreMush.Defs.ApplyPreset("tuned", false);
        MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
        log.Append(MoreMush.EditorTools.BalanceSim.Progression(400 - total, !first, seed, skill, 1.0, 1, 35));
        first = false;
        int n = MoreMush.EditorTools.BalanceSim.LastPlayed; total += n;
        System.IO.File.WriteAllText(outDir + "progress_sk.txt", tag + " rounds " + total + " " + System.DateTime.Now.ToString("HH:mm:ss"));
        if (MoreMush.EditorTools.BalanceSim.LastHitStop || total >= 400 || n == 0)
        {
            System.IO.File.WriteAllText(outDir + "t51sk_" + tag + ".log", log.ToString());
            System.IO.File.Copy(root + "Temp/balance_phase_s" + seed + ".json", outDir + "phasesk_" + tag + ".json", true);
            System.IO.File.Copy(root + "Temp/balance_state_s" + seed + ".json", outDir + "statesk_" + tag + ".json", true);
            ji++; total = 0; first = true; log.Clear();
        }
    }
    catch (System.Exception e)
    {
        System.IO.File.WriteAllText(outDir + "error.txt", e.ToString());
        MoreMush.EditorTools.BalanceSim.BOSS_GATE = prevGate; MoreMush.Defs.ApplyPreset(prevPreset, false);
        System.IO.File.WriteAllText(outDir + "running_sk.txt", "stopped");
        UnityEditor.EditorApplication.update -= tick.Invoke;
    }
};
UnityEditor.EditorApplication.update += tick.Invoke;
return "started; playmode=" + UnityEngine.Application.isPlaying;
