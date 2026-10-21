// T6-2: 표시 20에 막혀 파밍 (사람 수준 상태 = lag 8 스냅샷, 공격력 ~3.9) — 판당 공격력·보스 결과. 전/후 Hp.
string outDir = "C:/Project/MoreMustMush/BalanceData/r6/test/T6-2/";
var snaps = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("C:/Project/MoreMustMush/BalanceData/r6/test/T6-1b/snaps.json"));
var healF = typeof(MoreMush.Defs).GetField("CLEANER_HEAL"); float healNow = MoreMush.Defs.CLEANER_HEAL;
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
        for (int seed = 1; seed <= 6; seed++)
        {
            string js = null;
            foreach (var t in snaps) if ((int)t["Item1"] == seed && (int)t["Item2"] == 20 && (int)t["Item3"] == 8) js = (string)t["Item4"];
            var g = Newtonsoft.Json.JsonConvert.DeserializeObject<MoreMush.SaveData>(js);
            g.stage = 24; g.rounds = 23; g.bossOn = true;
            System.IO.File.WriteAllText("C:/Project/MoreMustMush/Temp/balance_state_s" + seed + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(g));
            var log = MoreMush.EditorTools.BalanceSim.Progression(14, true, seed, 0.6f, 30.0, 1, 0);
            System.IO.File.WriteAllText(outDir + hpset + "_s" + seed + ".log", log);
            System.IO.File.Copy("C:/Project/MoreMustMush/Temp/balance_phase_s" + seed + ".json", outDir + hpset + "_phase_s" + seed + ".json", true);
        }
    }
}
finally
{
    foreach (var id in ids) MoreMush.Defs.BOSS[id].hpMul = after[id];
    healF.SetValue(null, healNow);
    MoreMush.EditorTools.BalanceSim.BOSS_GATE = prevGate; MoreMush.Defs.ApplyPreset(prevPreset, false);
    System.IO.File.WriteAllText(outDir + "done.txt", "restored heal=" + MoreMush.Defs.CLEANER_HEAL + " hp=" + string.Join(",", System.Linq.Enumerable.Select(ids, i => i + ":" + MoreMush.Defs.BOSS[i].hpMul)));
}
return "ok";
