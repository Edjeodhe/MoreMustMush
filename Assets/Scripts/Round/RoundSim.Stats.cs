using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static MoreMush.Defs;

namespace MoreMush
{
    // 데미지 통계: 출처(직접 타격·스킬·능력)별 · 수확기별로 한 판 동안 준 피해·타격 수·처치 수를 모은다.
    // 피해는 버섯에 실제로 들어간 양(남은 체력을 넘는 초과분은 빼고). 정산 화면의 "데미지 통계"와 판 끝 로그가 쓴다
    public class DmgStat { public double dmg; public int hits, kills; }

    public partial class RoundSim
    {
        public readonly Dictionary<string, DmgStat> dmgBySrc = new Dictionary<string, DmgStat>();
        public readonly Dictionary<string, DmgStat> dmgByHv = new Dictionary<string, DmgStat>();

        // 수확기 칸: 영구 수확기는 그 종류, 분열형이 낳은 소형 수확기·분신(분열·분신 수확기 스킬)은 따로
        static string HvKey(Ball b) => b == null ? "none" : b.perm ? b.hv : b.hitsLeft > 0 || b.noSplit ? "sub_split" : "sub_clone";

        void RecordDamage(string src, Ball b, double dealt, bool kill)
        {
            Add(dmgBySrc, src, dealt, kill);
            Add(dmgByHv, HvKey(b), dealt, kill);
        }

        static void Add(Dictionary<string, DmgStat> d, string k, double dealt, bool kill)
        {
            if (!d.TryGetValue(k, out var s)) d[k] = s = new DmgStat();
            s.dmg += Math.Max(0, dealt); s.hits++; if (kill) s.kills++;
        }

        public static string DmgSrcName(string src)
        {
            switch (src)
            {
                case "direct": return Loc.T("직접 타격");
                case "blade": return Loc.T("회전 칼날");
                case "mill": return Loc.F("{0} 칼날", HV["mill"].n);
                case "jelly": return Loc.T("젤리 버섯 안");
                case "cleave": return Loc.F("{0} 베기", HV["sam"].n);
                case "bell": return Loc.F("{0} 울림", HV["bell"].n);
                case "spark": return Loc.T("연쇄 번개");
                case "meteor": return Loc.T("균사석 낙하");
            }
            return SKILLS.TryGetValue(src, out var sk) ? sk.n : src;
        }

        public static string DmgHvName(string key) =>
            key == "sub_split" ? Loc.T("분열로 생긴 소형 수확기") : key == "sub_clone" ? Loc.T("분신 수확기 (스킬)") : key == "none" ? Loc.T("기타") : HV.TryGetValue(key, out var h) ? h.n : key;

        static Dictionary<string, DmgStat> Copy(Dictionary<string, DmgStat> d) => d.ToDictionary(kv => kv.Key, kv => new DmgStat { dmg = kv.Value.dmg, hits = kv.Value.hits, kills = kv.Value.kills });

        // 판 끝 로그 한 줄씩 (에디터 콘솔 · 빌드는 Player.log)
        void LogDamage(int stage)
        {
            double total = dmgBySrc.Values.Sum(s => s.dmg);
            if (total <= 0) return;
            var sb = new StringBuilder($"[DMG] stage {stage} total {U.Fmt(total)} (atk {U.Fmt(st.atk)})");
            void Rows(string title, Dictionary<string, DmgStat> d)
            {
                sb.Append($"\n  {title}");
                foreach (var kv in d.OrderByDescending(kv => kv.Value.dmg))
                    sb.Append($"\n    {kv.Key,-10} {kv.Value.dmg / total * 100,5:0.0}%  dmg {U.Fmt(kv.Value.dmg)}  hits {kv.Value.hits}  kills {kv.Value.kills}");
            }
            Rows("by source", dmgBySrc); Rows("by harvester", dmgByHv);
            if (!SaveIO.Suspended) Debug.Log(sb.ToString());   // 밸런스 시뮬(수천 판)은 콘솔을 채우지 않게
        }
    }
}
