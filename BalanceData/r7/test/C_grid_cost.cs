// T7-C: 재생 격자 보충 1회 비용 (GridCandidate, 한 군락만 빈 상태에서 8/16/24/32px), 그리고 화면 지름 표
string outDir = "C:/Project/MoreMustMush/BalanceData/r7/test/";
var BF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
var miGrid = typeof(MoreMush.RoundSim).GetMethod("GridCandidate", BF);
var sb = new System.Text.StringBuilder();
sb.AppendLine("state,map,weather,dev,step,calls,msMean,msMax,found,ptsNote");
string[] cases = new string[] { "late_clear", "late_rain_dev3", "L0_rain_dev3", "L4_cols4II2_rain_dev3" };
for (int ci = 0; ci < cases.Length; ci++)
{
    string cs = cases[ci];
    MoreMush.EditorTools.BalanceSim.Sandbox<int>(() =>
    {
        UnityEngine.Random.InitState(4242 + ci);
        MoreMush.SaveData g;
        string w = "clear"; bool dev = false;
        if (cs.StartsWith("late"))
        {
            g = Newtonsoft.Json.JsonConvert.DeserializeObject<MoreMush.SaveData>(System.IO.File.ReadAllText(outDir + "T7-3/state_k06_s1.json"));
            if (cs.Contains("rain")) { w = "rain"; dev = true; }
            if (MoreMush.Game.BossRoundNext()) g.bossOn = false;
        }
        else
        {
            g = new MoreMush.SaveData { rounds = 30, stage = 34, tut = 3 };
            g.nodes = new System.Collections.Generic.Dictionary<string, int>();
            if (cs.StartsWith("L0")) { g.nodes["md_map"] = 0; g.nodes["ed_cols"] = 4; g.nodes["ed_cols2"] = 2; }
            else { g.nodes["md_map"] = 4; g.nodes["md_map2"] = 1; g.nodes["ed_cols"] = 4; g.nodes["ed_cols2"] = 2; }
            w = "rain"; dev = true;
        }
        if (dev) { g.nodes["dev_stump"] = 1; g.nodes["dev_moss"] = 1; g.nodes["dev_acorn"] = 1; }
        MoreMush.Game.G = g; g.theme = MoreMush.Game.LatestTheme().id;
        var R = MoreMush.RoundSim.Start(w, g.theme, null);
        // 군락 하나를 비워 재생 상황을 만든다
        var removed = R.colonies[R.colonies.Count / 2]; R.colonies.Remove(removed);
        float rr = MoreMush.Defs.TIERS[3].r * R.colS;
        foreach (float step in new float[] { 8, 16, 24, 32 })
        {
            // 워밍업
            miGrid.Invoke(R, new object[] { 5, rr, step });
            int calls = 200; double sum = 0, mx = 0; int found = 0;
            for (int i = 0; i < calls; i++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var c = miGrid.Invoke(R, new object[] { 5, rr, step });
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                sum += ms; if (ms > mx) mx = ms; if (c != null) found++;
            }
            sb.AppendLine(string.Format("{0},{1:F2},{2},{3},{4},{5},{6:F3},{7:F3},{8},max{9} colonies{10} colS{11:F3}", cs, R.st.zoom, w, dev, step, calls, sum / calls, mx, found, R.st.maxCol, R.colonies.Count, R.colS));
        }
        return 0;
    });
}
// 화면 지름 표 (등급별, 지도 배율 반영): 지름 = 2 * r * colS * zoom (그림 기준 r)
var sb2 = new System.Text.StringBuilder();
sb2.AppendLine("tier,r");
for (int t = 0; t < MoreMush.Defs.TIERS.Length && t < 8; t++) sb2.AppendLine(t + "," + MoreMush.Defs.TIERS[t].r);
System.IO.File.WriteAllText(outDir + "C_grid_cost.csv", sb.ToString());
System.IO.File.WriteAllText(outDir + "C_tiers.csv", sb2.ToString());
return sb.ToString() + sb2.ToString();
