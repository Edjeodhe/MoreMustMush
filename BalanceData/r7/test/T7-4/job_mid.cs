var BS = typeof(MoreMush.EditorTools.BalanceSim);
string root = "C:/Project/MoreMustMush/";
string outDir = root + "BalanceData/r7/test/T7-4/";
if (System.IO.File.Exists(outDir + "mid_running.txt") && System.IO.File.ReadAllText(outDir + "mid_running.txt").StartsWith("start")) return "already running";
var prevGate = MoreMush.EditorTools.BalanceSim.BOSS_GATE;
var prevPreset = MoreMush.Defs.TUNE.id;
MoreMush.Defs.ApplyPreset("tuned", false);
MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
System.IO.File.WriteAllText(outDir + "mid_running.txt", System.DateTime.Now.ToString("HH:mm:ss") + " start prevPreset=" + prevPreset);
// (skill, seed) 순서: 0.6 시드 1~6, 0.3 시드 1~3
var jobs = new System.Collections.Generic.List<(float, int)>();
int[] stops = new int[] { 28, 32 }; string[] mids = new string[] { "mid25", "mid29" };
for (int m = 0; m < 2; m++) for (int s = 1; s <= 6; s++) jobs.Add((0.6f, s));
int ji = 0, total = 0; var log = new System.Text.StringBuilder(); bool first = true;
System.Action tick = null;
tick = () =>
{
    try
    {
        if (ji >= jobs.Count)
        {
            MoreMush.EditorTools.BalanceSim.BOSS_GATE = prevGate; MoreMush.Defs.ApplyPreset(prevPreset, false);
            System.IO.File.WriteAllText(outDir + "mid_done.txt", System.DateTime.Now.ToString("HH:mm:ss") + " preset restored " + prevPreset + " now=" + MoreMush.Defs.TUNE.id);
            System.IO.File.WriteAllText(outDir + "mid_running.txt", "stopped");
            UnityEditor.EditorApplication.update -= tick.Invoke; return;
        }
        var (skill, seed) = jobs[ji];
        int mi = ji / 6; string tag = "k06_s" + seed;
        MoreMush.Defs.ApplyPreset("tuned", false);
        MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
        log.Append(MoreMush.EditorTools.BalanceSim.Progression(120 - total, !first, seed, skill, 1.0, 1, stops[mi]));
        first = false;
        int n = MoreMush.EditorTools.BalanceSim.LastPlayed; total += n;
        System.IO.File.WriteAllText(outDir + "mid_progress.txt", tag + " rounds " + total + " " + System.DateTime.Now.ToString("HH:mm:ss"));
        if (MoreMush.EditorTools.BalanceSim.LastHitStop || total >= 200 || n == 0)
        {
            System.IO.Directory.CreateDirectory(outDir + mids[mi] + "/");
            System.IO.File.Copy(root + "Temp/balance_state_s" + seed + ".json", outDir + mids[mi] + "/state_" + tag + ".json", true);
            ji++; total = 0; first = true; log.Clear();
        }
    }
    catch (System.Exception e)
    {
        System.IO.File.WriteAllText(outDir + "mid_error.txt", e.ToString());
        MoreMush.EditorTools.BalanceSim.BOSS_GATE = prevGate; MoreMush.Defs.ApplyPreset(prevPreset, false);
        System.IO.File.WriteAllText(outDir + "mid_running.txt", "stopped");
        UnityEditor.EditorApplication.update -= tick.Invoke;
    }
};
UnityEditor.EditorApplication.update += tick.Invoke;
return "started; playmode=" + UnityEngine.Application.isPlaying;
