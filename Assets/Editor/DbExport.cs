using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush.EditorTools
{
    // 노드 정의·레벨별 효과·가격을 JSON으로 내보낸다. 관리자가 이 파일로 엑셀 DB(Upgrade·Upgrade_Cost)를 갱신한다.
    // Unity.exe -batchmode -nographics -projectPath <프로젝트> -executeMethod MoreMush.EditorTools.DbExport.Batch -logFile <로그>
    // 결과: BalanceData/db/nodes_export.json
    public static class DbExport
    {
        public static string Export()
        {
            Defs.ApplyPreset(BalanceSim.PRESET, false);
            BalanceSim.LoadPriceTable();
            var nf = typeof(NF);
            var rows = NODES.Select(n =>
            {
                var m = nf.GetMethod(n.id, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int) }, null);
                bool numeric = m != null && (m.ReturnType == typeof(double) || m.ReturnType == typeof(int));
                return new
                {
                    n.id, name = n.n, n.br, n.parent, n.max, n.tier, n.needMax, n.core, n.sub, n.skill, gem = n.gem || n.costGem > 0,
                    baseGold = n.costGold, n.g, priced = n.costs != null,
                    levels = Enumerable.Range(1, n.max).Select(L => new
                    {
                        L,
                        value = numeric ? Convert.ToDouble(m.Invoke(null, new object[] { L })) : (double?)null,
                        text = n.eff?.Invoke(L),
                        gold = NodeCost(n, L - 1).gold,
                        gemCost = NodeCost(n, L - 1).gem,
                    }).ToArray(),
                };
            }).ToList();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../BalanceData/db/nodes_export.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(new { preset = TUNE.id, time = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), nodes = rows }, Formatting.Indented));
            return $"노드 {rows.Count}개 → {path}";
        }

        public static void Batch()
        {
            int code = 0; string log = Path.GetFullPath(Path.Combine(Application.dataPath, "../BalanceData/db/export.txt"));
            try { Directory.CreateDirectory(Path.GetDirectoryName(log)); File.WriteAllText(log, Export() + "\n"); }
            catch (Exception e) { File.WriteAllText(log, "오류: " + e + "\n"); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
