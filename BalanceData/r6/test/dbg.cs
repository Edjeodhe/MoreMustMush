return MoreMush.EditorTools.BalanceSim.Sandbox<string>(() =>
{
    string st = "a";
    try {
    var rnd = new System.Random(1); UnityEngine.Random.InitState(1);
    var g = new MoreMush.SaveData { rounds = 23, stage = 24 };
    g.nodes = new System.Collections.Generic.Dictionary<string, int> { { "ps_atk", 3 } };
    MoreMush.Game.G = g; st="b";
    g.theme = MoreMush.Game.LatestTheme().id; st="c";
    var r = MoreMush.EditorTools.BalanceSim.PlayRound(rnd, 0.6f, false); st="d";
    var R = MoreMush.RoundSim.R; st = "e R=" + (R==null);
    return st + " boss=" + r.boss + " left=" + r.bossHpLeft + " clr=" + r.bossCleared+" "+R.runT+" "+R.dmgBySrc.Count;
    } catch (System.Exception e) { return st + " " + e.ToString(); }
});
