using System.Collections.Generic;
using System.Linq;

namespace MoreMush
{
    public static partial class Defs
    {
        // ===== 의뢰 (일일 · 주간) =====
        // 매일·매주 00:00 KST(GMT+9)에 새로 받는다. 주는 월요일에 시작한다. 보상은 세계수 나무 경험치.
        // kind: rounds 라운드 판 수 · harvest 버섯 수확 횟수 · species 지정한 버섯 수확 횟수 · combo 한 판 최고 콤보 · daily 일일 의뢰 완료 수
        // text의 {n} = 목표 수, {m} = 지정한 버섯 이름
        public class QuestDef
        {
            public string id, kind; public int target, exp;
            public string textKo;   // 한국어 틀 원문
            public string text { get => Loc.T(textKo); set => textKo = value; }   // 번역된 틀 ({n}·{m}은 그대로)
        }

        public const int DAILY_COUNT = 3;
        public static readonly QuestDef[] DAILY_QUESTS =
        {
            new QuestDef { id = "d_rounds", kind = "rounds", target = 3, exp = 10, text = "수확 라운드 {n}판 하기" },
            new QuestDef { id = "d_harvest", kind = "harvest", target = 150, exp = 10, text = "버섯 {n}번 수확하기" },
            new QuestDef { id = "d_species", kind = "species", target = 15, exp = 10, text = "{m} {n}번 수확하기" },
            new QuestDef { id = "d_combo", kind = "combo", target = 30, exp = 10, text = "한 판에 콤보 {n} 달성하기" },
        };
        public static readonly QuestDef[] WEEKLY_QUESTS =
        {
            new QuestDef { id = "w_daily", kind = "daily", target = 7, exp = 40, text = "일일 의뢰 {n}개 완료하기" },
            new QuestDef { id = "w_rounds", kind = "rounds", target = 25, exp = 30, text = "수확 라운드 {n}판 하기" },
            new QuestDef { id = "w_harvest", kind = "harvest", target = 1500, exp = 30, text = "버섯 {n}번 수확하기" },
        };
        public static readonly Dictionary<string, QuestDef> QUEST = DAILY_QUESTS.Concat(WEEKLY_QUESTS).ToDictionary(q => q.id);

        public const double KST_OFFSET_MS = 9 * 3600000.0, DAY_MS = 86400000.0;
    }
}
