using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // Stage 씬 HUD. 사람이 배치한 글자에 라운드 값만 넣는다 — UI를 코드로 만들지 않는다.
    // 이름으로 찾는 것 (Stage_BG 아래, 없으면 그 칸만 건너뛴다):
    //   Cur_Score(점수)와 그 옆 Score(“점수” 라벨, 번역만) · Stage_Gold(옛 Earned_Gold) 안의 글자(예상 골드) · Stage_Timer 안의 글자(남은 시간)
    //   Stage_Info/WeatherText·WeatherIcon(날씨) · StageText(스테이지 번호) · Cur_Combo(지금 가장 큰 콤보) · SpeedText(배속, Speed 누르면 바뀜)
    //   Pause(누르면 일시정지) · StageGimmick/GimmickName·GimmickDescription(보스 스테이지에만 보임)
    [RequireComponent(typeof(StageController))]
    public class StageHUD : MonoBehaviour
    {
        StageController stage;
        TMP_Text score, scoreLabel, gold, timer, info, stageNo, combo, speed, gimmickName, gimmickDesc;
        GameObject gimmick;
        Image weatherIcon;
        RectTransform timerFill;
        double shownScore = -1, shownGold = -1;
        int shownTenths = -1, shownCombo = -1, shownSpeed = -1;

        void Awake()
        {
            var scene = gameObject.scene;
            stage = GetComponent<StageController>();
            var cur = SceneUI.Need(scene, "Cur_Score")?.transform;
            score = SceneUI.TextIn(cur);
            if (cur != null && cur.parent != null) scoreLabel = SceneUI.TextIn(cur.parent.Find("Score"));   // Stage 씬엔 LocText가 없어 여기서 번역한다
            gold = SceneUI.TextIn(SceneUI.Find(scene, "Stage_Gold") ?? SceneUI.Need(scene, "Earned_Gold")?.transform);
            var timerBox = SceneUI.Need(scene, "Stage_Timer")?.transform;
            timer = SceneUI.TextIn(timerBox);
            timerFill = TimerFill(timerBox);
            info = SceneUI.TextIn(SceneUI.Find(scene, "WeatherText") ?? SceneUI.Need(scene, "Stage_Info")?.transform);
            stageNo = SceneUI.TextIn(SceneUI.Find(scene, "StageText"));
            weatherIcon = SceneUI.Find(scene, "WeatherIcon")?.GetComponent<Image>();
            combo = SceneUI.TextIn(SceneUI.Find(scene, "Cur_Combo"));
            speed = SceneUI.TextIn(SceneUI.Find(scene, "SpeedText"));
            gimmick = SceneUI.Find(scene, "StageGimmick")?.gameObject;
            gimmickName = SceneUI.TextIn(SceneUI.Find(scene, "GimmickName"));
            gimmickDesc = SceneUI.TextIn(SceneUI.Find(scene, "GimmickDescription"));
            var speedBtn = SceneUI.Find(scene, "Speed");
            Click(speedBtn, () => StageController.NextSpeed());
            HotkeyHint(speedBtn, "F");
            Click(SceneUI.Find(scene, "Pause"), () => { Snd.Ui(); stage.TogglePause(); });
        }

        // 남은 시간 막대: Stage_Timer 안에 Fill(배치한 것)이 있으면 쓰고, 없으면 글자 뒤에 진행 막대 그림을 하나 깐다
        static RectTransform TimerFill(Transform box)
        {
            if (box == null) return null;
            var placed = box.Find("Fill") as RectTransform;
            if (placed != null) return placed;
            var go = new GameObject("TimerFill", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(box, false);
            go.transform.SetSiblingIndex(0);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(4, 4); rt.offsetMax = new Vector2(-4, -4);
            var im = go.GetComponent<Image>();
            im.raycastTarget = false;
            var sp = SpriteDB.Get("UI/progress_fill");
            if (sp != null) { im.sprite = sp; im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 2; }
            else im.color = new Color(0.5f, 0.75f, 0.4f);
            return rt;
        }

        // 버튼 위에 단축키 글자 (배치한 Hotkey가 있으면 그대로 둔다)
        static void HotkeyHint(Transform t, string key)
        {
            if (t == null || t.Find("Hotkey") != null) return;
            var tx = TempDialog.MakeText(t, "Hotkey", key, Vector2.zero, new Vector2(60, 36), 28);
            var rt = tx.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(6, 2);   // 시계 아이콘 바로 위
            tx.color = Color.white; tx.alignment = TMPro.TextAlignmentOptions.BottomLeft;
            tx.fontStyle = TMPro.FontStyles.Bold;
        }

        // 배치한 그림에 Button이 없으면 붙여서 누를 수 있게 한다
        static void Click(Transform t, UnityEngine.Events.UnityAction a)
        {
            if (t == null) return;
            if (!t.TryGetComponent<Button>(out var b)) { b = t.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; }
            b.onClick.AddListener(a);
        }

        void OnEnable() { stage.Started += OnStarted; stage.Drawn += OnDrawn; Loc.Changed += OnLang; }
        void OnDisable() { stage.Started -= OnStarted; stage.Drawn -= OnDrawn; Loc.Changed -= OnLang; }

        void Start() => OnLang();
        void OnLang()
        {
            shownTenths = shownSpeed = -1;
            if (scoreLabel != null) UIUtil.SetText(scoreLabel, Loc.T("점수"));
            if (RoundSim.R != null) OnStarted(RoundSim.R);
            else UIUtil.Show(gimmick, false);
        }

        void OnStarted(RoundSim R)
        {
            var st = R.st;
            UIUtil.SetText(info, st.boss != null ? Loc.F("보스 스테이지: {0}", st.boss.n) : Loc.F("오늘의 날씨: {0}", st.weather.n));
            UIUtil.SetText(stageNo, Loc.F("스테이지 {0}", st.stage));
            var ic = st.weather != null ? SpriteDB.Icon(st.weather.icon) : null;
            if (weatherIcon != null && ic != null) weatherIcon.sprite = ic;
            UIUtil.Show(gimmick, st.boss != null);
            if (st.boss != null) { UIUtil.SetText(gimmickName, st.boss.n); UIUtil.SetText(gimmickDesc, st.boss.gimmick); }
        }

        void OnDrawn(RoundSim R, float now)
        {
            if (score != null && R.score != shownScore) { shownScore = R.score; UIUtil.SetText(score, U.Fmt(R.score)); }
            double g = R.GoldSoFar;
            if (gold != null && g != shownGold) { shownGold = g; UIUtil.SetText(gold, U.Fmt(g)); }
            int tenths = Game.DBG.infinite ? int.MaxValue : Mathf.RoundToInt(Mathf.Max(0, R.timeLeft) * 10);
            if (tenths != shownTenths)
            {
                shownTenths = tenths;
                if (timer != null) UIUtil.SetText(timer, Game.DBG.infinite ? "∞" : Loc.F("{0:F1}초", Mathf.Max(0, R.timeLeft)));
                if (timerFill != null)
                {
                    float v = Game.DBG.infinite || R.timeMax <= 0 ? 1 : Mathf.Clamp01(R.timeLeft / R.timeMax);
                    timerFill.anchorMax = new Vector2(v, 1);
                    timerFill.gameObject.SetActive(v > 0.01f);
                }
            }
            int c = 0;
            foreach (var b in R.balls) if (b.hits > c) c = b.hits;   // 콤보 = 떨어지기 전까지 버섯 타격 수
            if (combo != null && c != shownCombo) { shownCombo = c; UIUtil.SetText(combo, c.ToString()); }
            if (speed != null && StageController.Speed != shownSpeed) { shownSpeed = StageController.Speed; UIUtil.SetText(speed, "x" + shownSpeed); }
        }
    }
}
