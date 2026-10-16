using TMPro;
using UnityEngine;

namespace MoreMush
{
    // Stage 씬 HUD. 사람이 배치한 글자에 라운드 값만 넣는다 — UI를 코드로 만들지 않는다.
    // 이름으로 찾는 것: Cur_Score(점수)와 그 옆 Score(“점수” 라벨, 번역만) · Earned_Gold 안의 글자(예상 골드) · Stage_Timer 안의 글자(남은 시간) · Stage_Info 안의 글자(날씨·보스)
    [RequireComponent(typeof(StageController))]
    public class StageHUD : MonoBehaviour
    {
        StageController stage;
        TMP_Text score, scoreLabel, gold, timer, info;
        double shownScore = -1, shownGold = -1;
        int shownTenths = -1;

        void Awake()
        {
            var scene = gameObject.scene;
            stage = GetComponent<StageController>();
            var cur = SceneUI.Need(scene, "Cur_Score")?.transform;
            score = SceneUI.TextIn(cur);
            if (cur != null && cur.parent != null) scoreLabel = SceneUI.TextIn(cur.parent.Find("Score"));   // Stage 씬엔 LocText가 없어 여기서 번역한다
            gold = SceneUI.TextIn(SceneUI.Need(scene, "Earned_Gold")?.transform);
            timer = SceneUI.TextIn(SceneUI.Need(scene, "Stage_Timer")?.transform);
            info = SceneUI.TextIn(SceneUI.Need(scene, "Stage_Info")?.transform);
        }

        void OnEnable() { stage.Started += OnStarted; stage.Drawn += OnDrawn; Loc.Changed += OnLang; }
        void OnDisable() { stage.Started -= OnStarted; stage.Drawn -= OnDrawn; Loc.Changed -= OnLang; }

        void Start() => OnLang();
        void OnLang()
        {
            shownTenths = -1;
            if (scoreLabel != null) UIUtil.SetText(scoreLabel, Loc.T("점수"));
            if (RoundSim.R != null) OnStarted(RoundSim.R);
        }

        void OnStarted(RoundSim R)
        {
            if (info == null) return;
            var st = R.st;
            UIUtil.SetText(info, st.boss != null ? Loc.F("보스 스테이지: {0}", st.boss.n) : Loc.F("오늘의 날씨: {0}", st.weather.n));
        }

        void OnDrawn(RoundSim R, float now)
        {
            if (score != null && R.score != shownScore) { shownScore = R.score; UIUtil.SetText(score, U.Fmt(R.score)); }
            double g = R.GoldSoFar;
            if (gold != null && g != shownGold) { shownGold = g; UIUtil.SetText(gold, U.Fmt(g)); }
            int tenths = Game.DBG.infinite ? int.MaxValue : Mathf.RoundToInt(Mathf.Max(0, R.timeLeft) * 10);
            if (timer != null && tenths != shownTenths) { shownTenths = tenths; UIUtil.SetText(timer, Game.DBG.infinite ? "∞" : Loc.F("{0:F1}초", Mathf.Max(0, R.timeLeft))); }
        }
    }
}
