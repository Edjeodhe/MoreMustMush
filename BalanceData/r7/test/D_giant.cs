// T7-D: 거대 버섯 자리 (FindGiantSpot) 가운데 대체 횟수: 판 시작 직후(군락 꽉 참) 필드·날씨별 40회
string outDir = "C:/Project/MoreMustMush/BalanceData/r7/test/";
var BF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
var miArea = typeof(MoreMush.RoundSim).GetMethod("AreaFree", BF, null, new System.Type[] { typeof(MoreMush.Shape), typeof(MoreMush.Colony) }, null);
var miGiant = typeof(MoreMush.RoundSim).GetMethod("FindGiantSpot", BF);
var fiNoSpawn = typeof(MoreMush.RoundSim).GetField("NO_SPAWN_Y", BF);
var sb = new System.Text.StringBuilder(); sb.AppendLine("map,cols,ii,cond,n,fallback,start_mean");
var mapN = new string[] { "L0", "L2", "L4", "md_map2" }; var mapL = new int[] { 0, 2, 4, 4 }; var mapM2 = new int[] { 0, 0, 0, 1 };
var colC = new int[] { 2, 4, 4 }; var colII = new int[] { 0, 0, 2 };
var condW = new string[] { "clear", "rain" }; var condD = new bool[] { false, true };
for (int mi = 0; mi < 4; mi++) for (int ci = 0; ci < 3; ci++) for (int wi = 0; wi < 2; wi++)
{
    int n = 0, fb = 0; double sum = 0;
    string mn = mapN[mi]; int ml = mapL[mi], m2 = mapM2[mi], c = colC[ci], ii = colII[ci]; string w = condW[wi]; bool dev = condD[wi];
    MoreMush.EditorTools.BalanceSim.Sandbox<int>(() =>
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            UnityEngine.Random.InitState(seed * 31 + 5);
            var g = new MoreMush.SaveData { rounds = 30, stage = 34, tut = 3 };
            g.nodes = new System.Collections.Generic.Dictionary<string, int> { { "md_map", ml }, { "ed_cols", c }, { "ed_cols2", ii } };
            if (m2 > 0) g.nodes["md_map2"] = 1;
            if (dev) { g.nodes["dev_stump"] = 1; g.nodes["dev_moss"] = 1; g.nodes["dev_acorn"] = 1; }
            MoreMush.Game.G = g; g.theme = MoreMush.Game.LatestTheme().id;
            var R = MoreMush.RoundSim.Start(w, g.theme, null);
            var pp = (UnityEngine.Vector2)miGiant.Invoke(R, null);
            float cx = MoreMush.RoundSim.WW / 2, cy = (float)fiNoSpawn.GetValue(null) / 2;
            n++; sum += R.colonies.Count;
            if (System.Math.Abs(pp.x - cx) < 0.01f && System.Math.Abs(pp.y - cy) < 0.01f)
            {
                bool free = (bool)miArea.Invoke(R, new object[] { MoreMush.Shape.C(cx, cy, 90), null });
                if (!free) fb++;
            }
        }
        return 0;
    });
    sb.AppendLine(string.Format("{0},{1},{2},{3}{4},{5},{6},{7:F1}", mn, c, ii, w, dev ? "+dev3" : "", n, fb, sum / n));
}
System.IO.File.WriteAllText(outDir + "D_giant.csv", sb.ToString());
return sb.ToString();
