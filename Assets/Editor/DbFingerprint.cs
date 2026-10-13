using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush.EditorTools
{
    // 게임 데이터 지문: 노드(구조·가격·효과 문구)·스킬·수확기·날씨·보스·라운드 능력치를 글로 남긴다.
    // 엑셀 DB로 옮기기 전후에 찍어 비교한다 (같아야 한다). 결과: BalanceData/db/fingerprint_<이름>.txt
    public static class DbFingerprint
    {
        [MenuItem("MoreMush/DB/지문 남기기 (before)")] static void Before() => Debug.Log(Write("before"));
        [MenuItem("MoreMush/DB/지문 남기기 (after)")] static void After() => Debug.Log(Write("after"));

        public static string Write(string name)
        {
            var sb = new StringBuilder();
            var oldG = G;
            try
            {
                foreach (var n in NODES)
                {
                    sb.Append($"N {n.id} br={n.br} p={n.parent} req={n.req} max={n.max} tier={n.tier} sub={n.sub} skill={n.skill} icon={n.icon} col={n.col} gem={n.gem} name={n.nKo}\n");
                    sb.Append($"  d={n.d}\n");
                    for (int L = 0; L <= n.max; L++)
                    {
                        var c = L < n.max ? NodeCost(n, L) : default;
                        sb.Append($"  L{L} gold={c.gold} gem={c.gem} eff={n.eff(L)}\n");
                    }
                }
                foreach (var s in SKILL_IDS.Select(i => SKILLS[i])) sb.Append($"S {s.id} {s.nKo} kind={s.kind} col={s.col} icon={s.icon} noRate={s.noRate} eff0={s.eff(0)} eff3={s.eff(3)}\n");
                foreach (var h in HARVESTERS) sb.Append($"H {h.id} {h.n} {h.type} unlock={h.unlockGold} lv={h.lvGold} atk={h.atkMul} spd={h.spdMul} size={h.sizeMul} crit={h.crit} text1={h.text(1)}\n");
                foreach (var w in WEATHERS) sb.Append($"W {w.id} {w.n} good={w.good} icon={w.icon} d={w.d} fav={string.Join(",", w.fav ?? new string[0])}\n");
                foreach (var b in BOSSES) sb.Append($"B {b.id} {b.n} cat={b.cat} look={b.look} col={b.col} hp={b.hpMul} g={b.gimmick}\n");
                // 라운드 능력치: 모든 노드 최대·스테이지 50·수확기 전부
                G = new SaveData { stage = 50, gem = 1e9, gold = 1e30 };
                foreach (var n in NODES) G.nodes[n.id] = n.max;
                foreach (var h in HARVESTERS) { G.hv[h.id] = 3; G.hvOn[h.id] = true; }
                foreach (var wid in WEATHERS.Select(w => w.id))
                {
                    var st = ComputeStats(wid, "night", wid == "cold" ? BOSSES[0] : null);
                    sb.Append($"R {wid}");
                    foreach (var f in typeof(RoundStats).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name))
                    {
                        var v = f.GetValue(st);
                        if (v is double || v is float || v is int || v is bool || v is string) sb.Append($" {f.Name}={v}");
                    }
                    foreach (var k in SKILL_IDS) sb.Append($" sk.{k}={st.sk[k].p:0.####}/{st.sk[k].pm:0.####}");
                    sb.Append('\n');
                }
            }
            finally { G = oldG; }
            string path = $"BalanceData/db/fingerprint_{name}.txt";
            System.IO.File.WriteAllText(path, sb.ToString());
            return $"{path} ({sb.Length} chars, {NODES.Count} nodes)";
        }
    }
}
