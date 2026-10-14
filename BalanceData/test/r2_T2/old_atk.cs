var D = MoreMush.Defs.NODE;
var keys = new string[]{"ps_atk2","ps_atk2p","ps_atk3","ps_atk3p","ps_atk4"};
var saved = new System.Collections.Generic.Dictionary<string,double[]>();
foreach (var k in keys) saved[k] = (double[])D[k].efx.Clone();
var prev = MoreMush.Defs.TUNE.id; MoreMush.Defs.ApplyPreset("tuned", false);
var bg = MoreMush.EditorTools.BalanceSim.BOSS_GATE; MoreMush.EditorTools.BalanceSim.BOSS_GATE = true;
var gh = MoreMush.EditorTools.BalanceSim.GATE_H;
try {
  D["ps_atk2"].efx = new double[]{0,10,20,30,40}; D["ps_atk2p"].efx = new double[]{0,10,20,30,0};
  D["ps_atk3"].efx = new double[]{0,100,200,300,400}; D["ps_atk3p"].efx = new double[]{0,100,200,300,0};
  D["ps_atk4"].efx = new double[]{0,1000,2000,3000,0};
  for (int sd = 1; sd <= 3; sd++) {
    for (int k = 1; k <= 230; k++) MoreMush.EditorTools.BalanceSim.Progression(1, k > 1, sd, 0.6f, 1e9, 1, 0);
    System.IO.File.Copy(@"C:\Project\MoreMustMush\Tempalance_phase_s" + sd + ".json", @"C:\Project\MoreMustMush\BalanceData	est2_T2\old_atk_s" + sd + ".json", true);
  }
}
finally {
  foreach (var k in keys) D[k].efx = saved[k];
  MoreMush.EditorTools.BalanceSim.BOSS_GATE = bg; MoreMush.EditorTools.BalanceSim.GATE_H = gh; MoreMush.Defs.ApplyPreset(prev, false);
}
return "done";
