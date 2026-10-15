using System.Collections.Generic;
using System.Linq;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 단계별 기능 해금 (튜토리얼 기획서 Ver.01 "진행 흐름도"). 해금 전에는 그 기능의 버튼을 숨긴다. 공방·옵션·수확하러 가기는 처음부터 보인다.
    public static class Unlock
    {
        public const string UPGRADE = "upgrade", CODEX = "codex", RECORDS = "records", WEATHER = "weather",
                            WORLDTREE = "worldtree", SKIN = "skin", BOSS = "boss", QUEST = "quest";
        public static readonly string[] ALL = { UPGRADE, CODEX, RECORDS, WEATHER, WORLDTREE, SKIN, BOSS, QUEST };

        // 세계수·스킨을 여는 도감 수 (분류마다, 가안: 식용 2 · 약용 2 · 독 2)
        public const int CODEX_PER_CAT_FOR_TREE = 2;

        // 조건(스테이지·도감·첫 패배)은 되돌아가지 않으므로 매번 계산해도 같다. feat는 알림(Newly)과 기존 세이브(OpenAll)용
        public static bool On(string k) => G == null || (G.feat.TryGetValue(k, out var on) && on) || Ready(k);

        // 조건. 스테이지 N 클리어 = 지금 스테이지가 N보다 큼
        static bool Ready(string k) => k switch
        {
            UPGRADE => G.stage > 1,                                   // 1스테이지(튜토리얼) 클리어
            CODEX => G.stage > 3,                                     // 3스테이지 클리어
            RECORDS => G.stage > 3,                                   // 기획서에 없음 — 도감과 함께 연다
            WEATHER => G.stage > 5,                                   // 5스테이지 클리어
            WORLDTREE => CodexReady(),                                // 일정 도감 해금
            SKIN => CodexReady(),
            BOSS => G.bossFailed,                                     // 보스에게 처음 졌을 때 (보스 도전 버튼)
            QUEST => BossCleared(FIRST_BOSS_STAGE),                   // 첫 보스 클리어
            _ => true,
        };

        static bool CodexReady()
        {
            foreach (var c in CAT_KEYS)
                if (SPECIES.Count(s => s.c == c && Harvested(s.id)) < CODEX_PER_CAT_FOR_TREE) return false;
            return true;
        }

        // 아직 안 열렸다가 지금 막 조건을 채운 기능 (트리 화면에 들어올 때 알림용). 부르면 열린 것으로 남는다
        public static List<string> Newly()
        {
            var o = new List<string>();
            if (G == null) return o;
            foreach (var k in ALL)
                if (!(G.feat.TryGetValue(k, out var on) && on) && Ready(k)) { G.feat[k] = true; o.Add(k); }
            return o;
        }

        public static string Name(string k) => k switch
        {
            UPGRADE => Loc.T("균사 트리 강화"),
            CODEX => Loc.T("도감"),
            RECORDS => Loc.T("기록"),
            WEATHER => Loc.T("날씨"),
            WORLDTREE => Loc.T("세계수"),
            SKIN => Loc.T("스킨"),
            BOSS => Loc.T("보스 도전 버튼"),
            QUEST => Loc.T("의뢰"),
            _ => k,
        };

        // 기존 세이브(튜토리얼 이전부터 하던 플레이어): 전부 연다
        public static void OpenAll(SaveData g) { foreach (var k in ALL) g.feat[k] = true; }
    }
}
