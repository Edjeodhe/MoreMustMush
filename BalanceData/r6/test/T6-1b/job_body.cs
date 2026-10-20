var BS = typeof(MoreMush.EditorTools.BalanceSim);
string outDir = "C:/Project/MoreMustMush/BalanceData/r6/test/T6-1b/";
var sb = new System.Text.StringBuilder();
sb.AppendLine("hpset,stage,lag,x,skill,seed,boss,weather,dur,atk,balls,maxHp,hpLeftPct,cleared,killT,fightT,netDmgPerS,grossDmgPerS,heal");
var healF = typeof(MoreMush.Defs).GetField("CLEANER_HEAL");
float healNow = MoreMush.Defs.CLEANER_HEAL;
var ids = new[] { "king", "cleaner", "angel", "phantom", "mover" };
var after = new System.Collections.Generic.Dictionary<string, double>(); foreach (var id in ids) after[id] = MoreMush.Defs.BOSS[id].hpMul;
var before = new System.Collections.Generic.Dictionary<string, double> { { "king", 1.75 }, { "cleaner", 20 }, { "angel", 740 }, { "phantom", 1650 }, { "mover", 1100 } };
bool prevGate = MoreMush.EditorTools.BalanceSim.BOSS_GATE; var prevPreset = MoreMush.Defs.TUNE.id;
float skill = 0.6f;
try
{
    MoreMush.Defs.ApplyPreset("tuned", false);
    MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
    // 시드마다: 봇을 표시 스테이지 (보스-lag)까지 진행시켜 노드를 저장, 그 노드로 보스 판
    var snaps = new System.Collections.Generic.List<(int seed, int disp, int lag, string json)>();
    foreach (int lag in new[] { 4, 8 })
    for (int seed = 1; seed <= 6; seed++)
    {
        // lag마다 처음부터 (단순). 보스 10·20·30·40·50 직전 (disp-lag) 스테이지까지
        bool first = true;
        foreach (int disp in new[] { 20, 30, 40, 50 })
        {
            int target = disp - lag + 4 - 1;   // 이 내부 판까지 돌리고 멈춘다 (다음 판이 표시 disp-lag)
            for (int guard = 0; guard < 400; guard++)
            {
                MoreMush.Defs.ApplyPreset("tuned", false);
                MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
                MoreMush.EditorTools.BalanceSim.Progression(300, !first, seed, skill, 1.0, 1, target); first = false;
                if (MoreMush.EditorTools.BalanceSim.LastHitStop || MoreMush.EditorTools.BalanceSim.LastPlayed == 0) break;
            }
            var st = Newtonsoft.Json.JsonConvert.DeserializeObject<MoreMush.SaveData>(System.IO.File.ReadAllText("C:/Project/MoreMustMush/Temp/balance_state_s" + seed + ".json"));
            snaps.Add((seed, disp, lag, System.IO.File.ReadAllText("C:/Project/MoreMustMush/Temp/balance_state_s" + seed + ".json")));
        }
    }
    System.IO.File.WriteAllText(outDir + "snaps.json", Newtonsoft.Json.JsonConvert.SerializeObject(snaps));
    foreach (var hpset in new[] { "before", "after" })
    {
        var m = hpset == "before" ? before : after;
        foreach (var id in ids) MoreMush.Defs.BOSS[id].hpMul = m[id];
        healF.SetValue(null, hpset == "before" ? 0.05f : 0.03f);
        foreach (var sn in snaps)
        for (int rep = 0; rep < 3; rep++)
        {
            int seed = sn.seed * 10 + rep, disp = sn.disp, lag = sn.lag;
            string line = MoreMush.EditorTools.BalanceSim.Sandbox<string>(() =>
            {
                var rnd = new System.Random(seed * 7919 + disp); UnityEngine.Random.InitState(seed * 7919 + disp);
                var g = Newtonsoft.Json.JsonConvert.DeserializeObject<MoreMush.SaveData>(sn.json);
                g.rounds = disp + 3; g.stage = disp + 4;
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
                return string.Join(",", hpset, disp, lag, 0, skill, seed, gbid, wth, durS.ToString("F1"), atk.ToString("F2"), balls, maxHp.ToString("G6"), (leftPct * 100).ToString("F1"), cleared ? 1 : 0, cleared ? clrT.ToString("F1") : "-", fightT.ToString("F1"), net.ToString("F2"), (gross / System.Math.Max(0.01, fightT)).ToString("F2"), MoreMush.Defs.CLEANER_HEAL);
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
