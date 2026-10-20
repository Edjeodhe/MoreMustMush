string root = "C:/Project/MoreMustMush/";
string outDir = root + "BalanceData/r5/test/T5-3/";
if (System.IO.File.Exists(outDir + "running.txt") && System.IO.File.ReadAllText(outDir + "running.txt").StartsWith("start")) return "already running";
var prevGate = MoreMush.EditorTools.BalanceSim.BOSS_GATE;
var prevPreset = MoreMush.Defs.TUNE.id;
string[] ids = { "king", "cleaner", "angel", "phantom", "mover" };
double[] orig = new double[5]; for (int i = 0; i < 5; i++) orig[i] = MoreMush.Defs.BOSS[ids[i]].hpMul;
double[] first = { 0.35, 2.097, 84, 115.5, 147 };
double[] fin = { 1.75, 20, 740, 1650, 1100 };
System.Action<string, double[]> setHp = (pre, hp) => { MoreMush.Defs.ApplyPreset(pre, false); for (int i = 0; i < 5; i++) MoreMush.Defs.BOSS[ids[i]].hpMul = hp[i]; MoreMush.EditorTools.BalanceSim.BOSS_GATE = false; };
System.IO.File.WriteAllText(outDir + "running.txt", System.DateTime.Now.ToString("HH:mm:ss") + " start prev=" + prevPreset + " orig=" + string.Join(",", orig));
// (tag, preset, hp, seed, stopStage, cap)
var jobs = new System.Collections.Generic.List<(string, string, double[], int, int, int)>();
for (int s = 1; s <= 3; s++) jobs.Add(("spec_first", "spec", first, s, 35, 250));
double[] p1500 = (double[])fin.Clone(); p1500[3] = 1500;
for (int s = 1; s <= 3; s++) jobs.Add(("tuned_ph1500", "tuned", p1500, s, 46, 150));
int ji = 0, total = 0; var log = new System.Text.StringBuilder(); bool firstCall = true;
System.Action restore = () => { setHp("tuned", fin); };
System.Action tick = null;
tick = () =>
{
    try
    {
        if (ji >= jobs.Count)
        {
            restore();
            string chk = "preset=" + MoreMush.Defs.TUNE.id + " gate=" + MoreMush.EditorTools.BalanceSim.BOSS_GATE; for (int i = 0; i < 5; i++) chk += " " + ids[i] + "=" + MoreMush.Defs.BOSS[ids[i]].hpMul;
            System.IO.File.WriteAllText(outDir + "done.txt", System.DateTime.Now.ToString("HH:mm:ss") + " 복원 확인: " + chk + " (측정 전 값 " + string.Join(",", orig) + ")");
            System.IO.File.WriteAllText(outDir + "running.txt", "stopped");
            UnityEditor.EditorApplication.update -= tick.Invoke; return;
        }
        var (tag0, pre, hp, seed, stop, cap) = jobs[ji];
        string tag = tag0 + "_s" + seed;
        setHp(pre, hp);
        log.Append(MoreMush.EditorTools.BalanceSim.Progression(cap - total, !firstCall, seed, 0.6f, 1.0, 1, stop));
        firstCall = false;
        int n = MoreMush.EditorTools.BalanceSim.LastPlayed; total += n;
        System.IO.File.WriteAllText(outDir + "progress.txt", tag + " rounds " + total + " " + System.DateTime.Now.ToString("HH:mm:ss"));
        if (MoreMush.EditorTools.BalanceSim.LastHitStop || total >= cap || n == 0)
        {
            System.IO.File.WriteAllText(outDir + "t53_" + tag + ".log", log.ToString());
            System.IO.File.Copy(root + "Temp/balance_phase_s" + seed + ".json", outDir + "phase_" + tag + ".json", true);
            ji++; total = 0; firstCall = true; log.Clear();
        }
    }
    catch (System.Exception e)
    {
        System.IO.File.WriteAllText(outDir + "error.txt", e.ToString());
        restore();
        System.IO.File.WriteAllText(outDir + "running.txt", "stopped");
        UnityEditor.EditorApplication.update -= tick.Invoke;
    }
};
UnityEditor.EditorApplication.update += tick.Invoke;
return "started orig=" + string.Join(",", orig);
