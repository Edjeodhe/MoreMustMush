string outDir = "C:/Project/MoreMustMush/BalanceData/r7/test/T7-5/";
var sb = new System.Text.StringBuilder("mid,seed,stage,atk,dur,shroomHp\n");
foreach (var m in new string[] { "mid21", "mid23" }) for (int sd = 1; sd <= 6; sd++)
{
    string json = System.IO.File.ReadAllText(outDir + m + "/state_k06_s" + sd + ".json");
    MoreMush.EditorTools.BalanceSim.Sandbox<int>(() =>
    {
        var g = Newtonsoft.Json.JsonConvert.DeserializeObject<MoreMush.SaveData>(json);
        MoreMush.Game.G = g; if (MoreMush.Game.BossRoundNext()) g.bossOn = false;
        g.theme = MoreMush.EditorTools.BalanceSim.PickZone();
        UnityEngine.Random.InitState(sd);
        var R = MoreMush.RoundSim.Start("clear", g.theme, null);
        sb.AppendLine(m + "," + sd + "," + R.st.stage + "," + R.st.atk + "," + R.st.duration + "," + R.st.shroomHp);
        return 0;
    });
}
System.IO.File.WriteAllText(outDir + "atk_check.csv", sb.ToString());
return sb.ToString();
