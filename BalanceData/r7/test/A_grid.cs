// T7-A: 판 시작 군락 수·colS·계획 ms (표 칸: md_map L0/L2/L4/md_map2 x cols 0/2/3/4/4+II2, 맑음·장치없음 / 비·장치3), 시드당 4판
var sb = new System.Text.StringBuilder();
sb.AppendLine("map,cols,ii,cond,seed,max,placed,colS,ms,zoom");
string outDir = "C:/Project/MoreMustMush/BalanceData/r7/test/";
var mapN = new[] { "L0", "L2", "L4", "md_map2" }; var mapL = new[] { 0, 2, 4, 4 }; var mapM2 = new[] { 0, 0, 0, 1 };
var colC = new[] { 0, 2, 3, 4, 4 }; var colII = new[] { 0, 0, 0, 0, 2 };
var condW = new[] { "clear", "rain" }; var condD = new[] { false, true };
MoreMush.EditorTools.BalanceSim.Sandbox<int>(() => { var g = new MoreMush.SaveData { rounds = 30, stage = 34, tut = 3 }; MoreMush.Game.G = g; g.theme = MoreMush.Game.LatestTheme().id; for (int i = 0; i < 3; i++) MoreMush.RoundSim.Start("clear", g.theme, null); return 0; });
for (int mi = 0; mi < 4; mi++)
for (int ci = 0; ci < 5; ci++)
for (int wi = 0; wi < 2; wi++)
for (int seed = 1; seed <= 4; seed++)
{
    string mn = mapN[mi]; int ml = mapL[mi], m2 = mapM2[mi], c = colC[ci], ii = colII[ci]; string w = condW[wi]; bool dev = condD[wi];
    string line = MoreMush.EditorTools.BalanceSim.Sandbox<string>(() =>
    {
        UnityEngine.Random.InitState(seed * 100 + 7);
        var g = new MoreMush.SaveData { rounds = 30, stage = 34, tut = 3 };
        g.nodes = new System.Collections.Generic.Dictionary<string, int> { { "md_map", ml }, { "ed_cols", c }, { "ed_cols2", ii } };
        if (m2 > 0) g.nodes["md_map2"] = 1;
        if (dev) { g.nodes["dev_stump"] = 1; g.nodes["dev_moss"] = 1; g.nodes["dev_acorn"] = 1; }
        MoreMush.Game.G = g; g.theme = MoreMush.Game.LatestTheme().id;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var R = MoreMush.RoundSim.Start(w, g.theme, null);
        sw.Stop();
        return $"{mn},{c},{ii},{w}{(dev ? "+dev3" : "")},{seed},{R.st.maxCol},{R.colonies.Count},{R.colS:F3},{sw.Elapsed.TotalMilliseconds:F2},{R.st.zoom:F2}";
    });
    sb.AppendLine(line);
}
System.IO.File.WriteAllText(outDir + "A_grid.csv", sb.ToString());
return "ok " + sb.Length;
