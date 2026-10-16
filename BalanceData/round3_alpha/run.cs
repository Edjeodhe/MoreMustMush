// theory#4 alpha 측정. SEED를 바꿔 시드마다 한 번씩 실행. 결과: round3_alpha/alpha_s{SEED}.jsonl
int SEED = 1;
var BS = MoreMush.EditorTools.BalanceSim.BOSS_GATE; var prev = MoreMush.Defs.TUNE.id;
var origMul = (double[])MoreMush.Defs.EARLY_COL_MUL.Clone();
MoreMush.Defs.ApplyPreset("tuned", false);
MoreMush.EditorTools.BalanceSim.BOSS_GATE = true;
string outF = "C:/Project/MoreMustMush/BalanceData/round3_alpha/alpha_s" + SEED + ".jsonl";
var sb = new System.Text.StringBuilder();
try
{
    int[] cs = { 1, 2, 3, 4, 6, 8, 12 };
    // (stage, c, stump)
    var cfgs = new System.Collections.Generic.List<int[]>();
    foreach (int st in new[] { 3, 5 }) foreach (int c in cs) cfgs.Add(new[] { st, c, 0 });
    foreach (int st in new[] { 3, 5 }) cfgs.Add(new[] { st, 3, 1 });
    foreach (var cf in cfgs)
    {
        int stage = cf[0], c = cf[1], stump = cf[2];
        MoreMush.EditorTools.BalanceSim.Sandbox<int>(() =>
        {
            var rnd = new System.Random(SEED); UnityEngine.Random.InitState(SEED);
            for (int i = 0; i < 10; i++)
            {
                MoreMush.Game.G = new MoreMush.SaveData { rounds = stage - 1, stage = stage };
                var nodes = new System.Collections.Generic.Dictionary<string, int> { { "power", 1 }, { "ed_score", 1 } };
                if (stump == 1) nodes["dev_stump"] = 1;
                MoreMush.Game.G.nodes = nodes;
                // 군락 수 고정: 기준값(배율 1) 대비 배율
                MoreMush.Defs.EARLY_COL_MUL[stage - 1] = 1.0;
                var st0 = MoreMush.Game.ComputeStats("clear", MoreMush.Defs.THEMES[0].id);
                double baseCols = st0.maxCol;
                MoreMush.Defs.EARLY_COL_MUL[stage - 1] = c / baseCols;
                var r = MoreMush.EditorTools.BalanceSim.PlayRound(rnd);
                sb.AppendLine("{\"seed\":" + SEED + ",\"stage\":" + stage + ",\"c\":" + c + ",\"stump\":" + stump + ",\"i\":" + i + ",\"base\":" + baseCols + ",\"weather\":\"" + r.weather + "\",\"cols\":" + r.cols + ",\"dur\":" + r.duration + ",\"net\":" + r.net.ToString("R") + ",\"scoreGold\":" + r.scoreGold.ToString("R") + ",\"bonus\":" + r.bonus.ToString("R") + ",\"score\":" + r.score.ToString("R") + ",\"harv\":" + r.harvests + ",\"made\":" + r.spawned + ",\"kill\":[" + string.Join(",", r.killT) + "],\"madeT\":[" + string.Join(",", r.madeT) + "],\"combo\":" + r.maxCombo + ",\"atk\":" + r.atk.ToString("R") + "}");
            }
            return 0;
        });
    }
}
finally
{
    for (int k = 0; k < origMul.Length; k++) MoreMush.Defs.EARLY_COL_MUL[k] = origMul[k];
    MoreMush.EditorTools.BalanceSim.BOSS_GATE = BS; MoreMush.Defs.ApplyPreset(prev, false);
    System.IO.File.WriteAllText(outF, sb.ToString());
    System.IO.File.WriteAllText("C:/Project/MoreMustMush/BalanceData/round3_alpha/done_s" + SEED + ".txt", System.DateTime.Now.ToString("HH:mm:ss") + " preset " + prev + " mul " + string.Join(",", MoreMush.Defs.EARLY_COL_MUL));
}
return "done s" + SEED;
