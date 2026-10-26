MoreMush.Defs.ApplyPreset("tuned", false);
MoreMush.EditorTools.BalanceSim.BOSS_GATE = false;
double[] mul = { 1, 1.5, 2, 3, 5, 8 };
var T = new System.Collections.Generic.List<MoreMush.EditorTools.BossTune.Target>();
(string id, int st, int lo, int hi)[] defs = { ("king", 10, 2, 3), ("cleaner", 20, 2, 3), ("angel", 30, 2, 3), ("phantom", 40, 3, 5), ("mover", 50, 4, 6) };
var sb = new System.Text.StringBuilder();
foreach (var d in defs)
{
    double b = MoreMush.Defs.BOSS[d.id].hpMul; sb.Append(d.id + "=" + b + " ");
    var c = new double[mul.Length]; for (int i = 0; i < mul.Length; i++) c[i] = b * mul[i];
    T.Add(new MoreMush.EditorTools.BossTune.Target { id = d.id, stage = d.st, lo = d.lo, hi = d.hi, cands = c });
}
return sb + " -> " + MoreMush.EditorTools.BossTune.Start(T, 3, false);
