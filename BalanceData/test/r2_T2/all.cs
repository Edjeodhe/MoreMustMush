var D = MoreMush.Defs.NODE;
var keys = new string[] { "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4" };
var saved = new System.Collections.Generic.Dictionary<string, double[]>();
foreach (var k in keys) saved[k] = (double[])D[k].efx.Clone();
var prev = MoreMush.Defs.TUNE.id; MoreMush.Defs.ApplyPreset("tuned", false);
var bg = MoreMush.EditorTools.BalanceSim.BOSS_GATE; MoreMush.EditorTools.BalanceSim.BOSS_GATE = true;
var gh = MoreMush.EditorTools.BalanceSim.GATE_H;
string root = "C:/Project/MoreMustMush/";
string outDir = root + "BalanceData/test/r2_T2/";
System.Action<string, int, float> run = (name, rounds, skill) =>
{
    for (int sd = 1; sd <= 3; sd++)
    {
        for (int k = 1; k <= rounds; k++) MoreMush.EditorTools.BalanceSim.Progression(1, k > 1, sd, skill, 1e9, 1, 0);
        System.IO.File.Copy(root + "Temp/balance_phase_s" + sd + ".json", outDir + name + "_s" + sd + ".json", true);
    }
    System.IO.File.WriteAllText(outDir + name + ".done", System.DateTime.Now.ToString("HH:mm:ss"));
};
try
{
    D["ps_atk2"].efx = new double[] { 0, 10, 20, 30, 40 }; D["ps_atk2p"].efx = new double[] { 0, 10, 20, 30, 0 };
    D["ps_atk3"].efx = new double[] { 0, 100, 200, 300, 400 }; D["ps_atk3p"].efx = new double[] { 0, 100, 200, 300, 0 };
    D["ps_atk4"].efx = new double[] { 0, 1000, 2000, 3000, 0 };
    run("old_atk", 230, MoreMush.EditorTools.BalanceSim.SKILL);
    foreach (var k in keys) D[k].efx = (double[])saved[k].Clone();
    run("skill1.0", 300, 1.0f);
    run("skill1.5", 300, 1.5f);
    MoreMush.EditorTools.BalanceSim.GATE_H = gh * 0.8;
    run("gateH0.72", 300, MoreMush.EditorTools.BalanceSim.SKILL);
}
finally
{
    foreach (var k in keys) D[k].efx = saved[k];
    MoreMush.EditorTools.BalanceSim.BOSS_GATE = bg; MoreMush.EditorTools.BalanceSim.GATE_H = gh; MoreMush.Defs.ApplyPreset(prev, false);
    System.IO.File.WriteAllText(outDir + "all.finished", System.DateTime.Now.ToString("HH:mm:ss"));
}
return "done";
