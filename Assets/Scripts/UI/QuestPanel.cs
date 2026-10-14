using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 의뢰 모달: 일일 3개 · 주간 3개 · 세계수 경험치. 줄 구성은 Editor/RevampScene.BuildQuestPanel이 만든다.
    // 0 일일 머리줄 · 1~3 일일 · 4 주간 머리줄 · 5~7 주간 · 8 경험치
    public class QuestPanel : MonoBehaviour
    {
        public TMP_Text title;
        public TMP_Text[] rowLabels = new TMP_Text[9], rowValues = new TMP_Text[9];
        public Button[] claims = new Button[6];               // UIAction questclaim, arg = d0..d2 / w0..w2
        public TMP_Text[] claimTexts = new TMP_Text[6];
        static readonly int[] QUEST_ROWS = { 1, 2, 3, 5, 6, 7 };

        public void Render()
        {
            if (G == null) return;
            QuestEnsure();
            title.text = Loc.T("의뢰");
            double now = Now();
            Set(0, Loc.T("<b>일일 의뢰</b>"), Loc.F("<size=80%>초기화까지 {0}</size>", U.FmtTime(Until(KstDay(now) + 1, now))));
            Set(4, Loc.T("<b>주간 의뢰</b>"), Loc.F("<size=80%>초기화까지 {0}</size>", U.FmtTime(Until((KstWeek(KstDay(now)) + 1) * 7 - 3, now))));
            for (int i = 0; i < QUEST_ROWS.Length; i++)
            {
                bool weekly = i >= 3;
                var list = weekly ? G.quest.weekly : G.quest.daily;
                int k = weekly ? i - 3 : i;
                int row = QUEST_ROWS[i];
                bool has = k < list.Count && QuestDefOf(list[k]) != null;
                UIUtil.Show(rowLabels[row].transform.parent, has);
                if (!has) continue;
                var p = list[k]; var d = QuestDefOf(p);
                Set(row, QuestText(p), $"{U.Fmt(p.prog)}/{U.Fmt(d.target)}");
                claims[i].GetComponent<UIAction>().arg = (weekly ? "w" : "d") + k;
                claims[i].interactable = QuestClaimable(p);
                claimTexts[i].text = p.claimed ? Loc.T("완료") : Loc.F("+{0} 경험치", d.exp);
            }
            Set(8, Loc.T("세계수 경험치"), StageMax() ? Loc.T("최대 단계") : $"{U.Fmt(System.Math.Floor(TreeExp()))}/{U.Fmt(TreeExpNeed())}");
        }

        void Set(int row, string label, string value) { rowLabels[row].text = label; rowValues[row].text = value; }

        // 그 KST 날짜(day) 0시까지 남은 초
        static double Until(long day, double now) => Math.Max(0, (day * DAY_MS - KST_OFFSET_MS - now) / 1000);
    }
}
