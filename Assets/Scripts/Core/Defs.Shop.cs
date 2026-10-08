using System.Collections.Generic;
using System.Linq;

namespace MoreMush
{
    public static partial class Defs
    {
        // ===== 마을 의뢰 =====
        public static readonly double[] QUEST_N = { 30, 12, 5, 2 };   // 등급별 기본 요구 개수 (스테이지마다 ×1.1)
        public const double QUEST_MUL = 3;
        public const int QUEST_EVERY = 3;   // 마을 의뢰가 새로 바뀌는 라운드 간격

        // 의뢰 주민 8명. 초상화는 Characters/NPC/<id>. lines의 {m} = 버섯 이름, {n} = 개수
        public class Npc { public string id, n, job, bg; public string[] lines; }
        public static readonly List<Npc> NPCS = new List<Npc>
        {
            new Npc { id = "baker", n = "밀순 할머니", job = "빵집", bg = "#ffe4d6", lines = new[] { "버섯빵 반죽은 다 됐는데 속 재료가 없지 뭐냐. {m} {n}개만 구해다 주겠니?", "손주들이 놀러 온단다. {m} {n}개로 파이를 구워 주고 싶구나." } },
            new Npc { id = "herb", n = "감초 할아버지", job = "약방", bg = "#dff3dc", lines = new[] { "허허, 기침약을 달여야 하는데 {m} {n}개가 꼭 필요하다네.", "요즘 포자 감기가 돌아. 약재로 쓸 {m} {n}개를 부탁하네." } },
            new Npc { id = "smith", n = "무쇠 아저씨", job = "대장간", bg = "#ffe0c4", lines = new[] { "낫을 벼릴 때 {m} 포자를 바르면 기가 막히거든! {n}개 갖다 줘!", "용광로 불이 약해. 잘 마른 {m} {n}개면 충분해!" } },
            new Npc { id = "kid", n = "도토리", job = "꼬마 사냥꾼", bg = "#fff0c4", lines = new[] { "할아버지! 저도 사냥꾼이 될 거예요! {m} {n}개만 보여 주세요!", "친구들한테 자랑할 거예요. {m} {n}개 모아 주실 수 있어요?" } },
            new Npc { id = "inn", n = "달래 사장", job = "여관", bg = "#efe0ff", lines = new[] { "손님들이 버섯 전골만 찾아요. {m} {n}개 들여놓고 싶어요.", "오늘 밤 여관에서 잔치가 열려요! {m} {n}개만 부탁해요." } },
            new Npc { id = "dr", n = "포자린 박사", job = "연구소", bg = "#dceeff", lines = new[] { "흥미롭군! {m} 표본 {n}개가 연구에 꼭 필요하네.", "거대 버섯의 비밀을 풀 단서가 {m}에 있어. {n}개만 부탁하지." } },
            new Npc { id = "merch", n = "금복이", job = "광장 상인", bg = "#fff3c0", lines = new[] { "요즘 {m} 값이 오르고 있어요! {n}개 넘겨주시면 후하게 쳐 드리죠.", "옆 마을 손님이 {m} {n}개를 주문했지 뭐예요. 도와주세요!" } },
            new Npc { id = "ranger", n = "솔잎", job = "숲지기", bg = "#e2f4d0", lines = new[] { "숲이 버섯에 먹히고 있어요. 견본으로 {m} {n}개를 조사해야 해요.", "{m} 무리가 오솔길을 막았어요. {n}개만 뽑아다 주세요." } },
        };
        public static readonly Dictionary<string, Npc> NPC = NPCS.ToDictionary(n => n.id);
    }
}
