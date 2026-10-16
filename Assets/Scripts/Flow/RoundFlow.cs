using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 라운드 하나의 앞뒤 (예전 GameFlow.BeginRound · FinishRound · AfterSettle).
    // MainGame의 출발 버튼 → Request() → Stage 씬 → Begin() … 끝나면 Finish() → 정산 → MainGame으로.
    public static class RoundFlow
    {
        public const string HV_OVER_MSG = "수확기가 많습니다. 장착 가능 수확기 이하로 활성화가 필요합니다.";

        // Stage 씬이 열리면 쓸 라운드 조건 (Request가 정한다)
        public struct Plan { public string weather, theme; public BossDef boss; }
        static Plan? pending;

        // 마지막 정산 결과 (Stage의 정산 UI·MainGame이 읽는다)
        public static RoundSim.Summary Last { get; private set; }
        // 정산을 마치고 나갈 때 엔딩을 처음 맞았나. 엔딩 연출 UI가 보여 준 뒤 false로 끈다
        public static bool EndingReached;

        // 플레이 시작 때 이전 플레이 값을 지운다 (App.ResetStatics)
        public static void Reset() { pending = null; Last = null; EndingReached = false; }

        // 시작할 수 없으면 그 이유 (번역된 문장), 시작할 수 있으면 null
        public static string CantStart() => HvOver() ? Loc.T(HV_OVER_MSG) : null;

        // 라운드 조건을 정하고 Stage 씬으로 간다. 막히면 알림을 띄우고 false
        public static bool Request()
        {
            var why = CantStart();
            if (why != null) { Snd.Err(); App.Notify(why); return false; }
            pending = MakePlan();
            App.Go(Scenes.Stage);
            return true;
        }

        static Plan MakePlan()
        {
            var boss = BossRoundNext() ? BossAt(StageNow()) : null;
            // 지역은 고르지 않는다: 스테이지가 열어 준 가장 새 지역
            return new Plan { weather = RollWeather(boss != null), theme = LatestTheme().id, boss = boss };
        }

        // Stage 씬이 부른다. 에디터에서 Stage를 바로 플레이하면 여기서 조건을 정한다
        public static RoundSim Begin()
        {
            var p = pending ?? MakePlan();
            pending = null;
            G.theme = p.theme;
            var R = RoundSim.Start(p.weather, p.theme, p.boss);
            SaveGame();
            Telemetry.StageStart(G);
            return R;
        }

        // 라운드가 끝났을 때 한 번. 정산을 확정한다
        public static RoundSim.Summary Finish()
        {
            Last = RoundSim.R.Finish();
            SaveGame();
            return Last;
        }

        // 정산 화면을 떠날 때 한 번 (예전 AfterSettle). 도감을 다 채웠으면 엔딩을 확정한다 —
        // 정산 창에서 앱을 꺼도 엔딩을 놓치지 않게, 확정은 정산 직후가 아니라 여기서 한다
        public static void AfterSettle()
        {
            if (CodexCount() < SP_TOTAL || G.ending) return;
            G.ending = true;
            EndingReached = true;
            SaveGame();
            Telemetry.GameClear(G);
        }
    }
}
