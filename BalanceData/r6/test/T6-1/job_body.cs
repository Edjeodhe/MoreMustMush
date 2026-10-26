// T6-1: 사람 수준 노드 상태의 보스 판 (전/후 Hp). 결과 CSV: BalanceData/r6/test/T6-1/out.csv
var BS = typeof(MoreMush.EditorTools.BalanceSim);
string outDir = "C:/Project/MoreMustMush/BalanceData/r6/test/T6-1/";
var sb = new System.Text.StringBuilder();
sb.AppendLine("hpset,stage,durLv,skills,skill,seed,boss,weather,dur,atk,balls,maxHp,hpLeftPct,cleared,killT,fightT,netDmgPerS,grossDmgPerS,heal");
var healF = typeof(MoreMush.Defs).GetField("CLEANER_HEAL");
float healNow = MoreMush.Defs.CLEANER_HEAL;
var ids = new[] { "king", "cleaner", "angel", "phantom", "mover" };
var after = new System.Collections.Generic.Dictionary<string, double>(); foreach (var id in ids) after[id] = MoreMush.Defs.BOSS[id].hpMul;
var before = new System.Collections.Generic.Dictionary<string, double> { { "king", 1.75 }, { "cleaner", 20 }, { "angel", 740 }, { "phantom", 1650 }, { "mover", 1100 } };
bool prevGate = MoreMush.EditorTools.BalanceSim.BOSS_GATE; var prevPreset = MoreMush.Defs.TUNE.id;
try
{
    MoreMush.Defs.ApplyPreset("tuned", false);
    MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
    foreach (var hpset in new[] { "before", "after" })
    {
        var m = hpset == "before" ? before : after;
        foreach (var id in ids) MoreMush.Defs.BOSS[id].hpMul = m[id];
        healF.SetValue(null, hpset == "before" ? 0.05f : 0.03f);
        foreach (int disp in new[] { 10, 20, 30, 40, 50 })
        foreach (int dur in new[] { 0, 1 })
        foreach (int sk in new[] { 0, 1 })
        foreach (float skill in new[] { 0.6f, 1.0f })
        for (int seed = 1; seed <= 6; seed++)
        {
            string line = MoreMush.EditorTools.BalanceSim.Sandbox<string>(() =>
            {
                var rnd = new System.Random(seed * 7919 + disp); UnityEngine.Random.InitState(seed * 7919 + disp);
                var g = new MoreMush.SaveData { rounds = disp + 3, stage = disp + 4 };
                g.nodes = new System.Collections.Generic.Dictionary<string, int> { { "ps_atk", 3 }, { "power", 1 }, { "ps_ball", 1 }, { "ps_dur", dur }, { "md_map", 1 }, { "ed_score", 2 }, { "ed_cols", 2 } };
                if (sk == 1) { g.nodes["homing"] = 1; g.nodes["burst"] = 2; }
                g.bossOn = true;
                MoreMush.Game.G = g;
                g.theme = MoreMush.Game.LatestTheme().id;
                var boss = MoreMush.Defs.BossAt(MoreMush.Game.ContentNow());
                string wth = MoreMush.Game.RollWeather(true);
                var R = MoreMush.RoundSim.Start(wth, MoreMush.Game.G.theme, boss);
                double durS = R.st.duration, atk = R.st.atk; int balls = R.st.permBalls;
                var bm = R.BossMain; R.Launch(UnityEngine.Mathf.Atan2(bm.y - MoreMush.RoundSim.LAUNCH.y, bm.x - MoreMush.RoundSim.LAUNCH.x) + (float)(rnd.NextDouble() * 0.2 - 0.1));
                float h = 1 / 30f, sim = 0, noise = 0, nt = 0;
                while (!(R.phase == "end" && R.endT <= 0) && sim < 300)
                {
                    nt -= h;
                    if (nt <= 0) { noise = (float)(rnd.NextDouble() * 2 - 1) * skill * R.barLen; nt = 0.5f; }
                    MoreMush.Ball tgt = null;
                    foreach (var bb in R.balls) if (bb.perm && !bb.dead && bb.dy > 0 && (tgt == null || bb.y > tgt.y)) tgt = bb;
                    float tx = tgt != null ? tgt.x + noise : MoreMush.RoundSim.WW / 2;
                    tx = UnityEngine.Mathf.Clamp(tx, MoreMush.RoundSim.FX0 + R.barLen / 2, MoreMush.RoundSim.FX1 - R.barLen / 2);
                    R.barX = UnityEngine.Mathf.Lerp(R.barX, tx, 1 - UnityEngine.Mathf.Exp(-18 * h));
                    R.SimFrame(h); sim += h;
                }
                double maxHp = 0, left = 0; foreach (var b in R.bosses) { maxHp += b.maxHp; left += System.Math.Max(0, b.hp); }
                bool cleared = R.bossCleared; double clrT = R.bossClearT, runT = R.runT;
                var sm = R.Finish();
                double gross = 0; foreach (var kv in sm.dmgBySrc) gross += kv.Value.dmg;
                double leftPct = maxHp > 0 ? left / maxHp : 0;
                double fightT = cleared ? clrT : runT; if (fightT <= 0) fightT = runT;
                double net = maxHp * (1 - leftPct) / System.Math.Max(0.01, fightT);
                string gbid = boss.id;
                return string.Join(",", hpset, disp, dur, sk, skill, seed, gbid, wth, durS.ToString("F1"), atk.ToString("F2"), balls, maxHp.ToString("G6"), (leftPct * 100).ToString("F1"), cleared ? 1 : 0, cleared ? clrT.ToString("F1") : "-", fightT.ToString("F1"), net.ToString("F2"), (gross / System.Math.Max(0.01, fightT)).ToString("F2"), MoreMush.Defs.CLEANER_HEAL);
            });
            sb.AppendLine(line);
        }
        System.IO.File.WriteAllText(outDir + "out.csv", sb.ToString());
    }
}
finally
{
    foreach (var id in ids) MoreMush.Defs.BOSS[id].hpMul = after[id];
    healF.SetValue(null, healNow);
    MoreMush.EditorTools.BalanceSim.BOSS_GATE = prevGate; MoreMush.Defs.ApplyPreset(prevPreset, false);
    System.IO.File.WriteAllText(outDir + "done.txt", "restored heal=" + MoreMush.Defs.CLEANER_HEAL + " hp=" + string.Join(",", System.Linq.Enumerable.Select(ids, i => i + ":" + MoreMush.Defs.BOSS[i].hpMul)));
}
return "ok " + sb.Length;
