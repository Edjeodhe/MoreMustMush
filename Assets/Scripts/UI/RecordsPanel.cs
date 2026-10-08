using System.Linq;
using TMPro;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Records modal (prototype openRecords): one Label/Value text pair per row.
    public class RecordsPanel : MonoBehaviour
    {
        public TMP_Text[] rowLabels = new TMP_Text[9], rowValues = new TMP_Text[9];

        public void Render()
        {
            var rows = RECORD_KEYS.Select(k => (RECORD_NAMES[k], U.Fmt(G.rec.Get(k)))).ToList();
            var th = LatestTheme();
            rows.Add(("스테이지", $"{StageNow()} · {UIUtil.Ic(th.icon)} {th.n}"));
            rows.Add(("플레이 시간", U.FmtTime(G.play)));
            rows.Add(("도감", $"{CodexCount()}/{SP_TOTAL} · 황금 {GoldenCount()}/{SP_TOTAL} · 특수 {SPECIALS.Count(s => HasSpecial(s.id))}/{SPECIALS.Length}"));
            // 씬의 줄 수가 기록 수보다 많으면 남는 줄은 숨긴다 (세금 줄을 지운 뒤 Row8이 "기록 / 0"으로 남아 있었다)
            for (int i = 0; i < rowLabels.Length; i++)
            {
                bool on = i < rows.Count;
                UIUtil.Show(rowLabels[i].transform.parent, on);
                if (!on) continue;
                rowLabels[i].text = rows[i].Item1;
                rowValues[i].text = rows[i].Item2;
            }
        }
    }
}
