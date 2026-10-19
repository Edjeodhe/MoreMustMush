using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // Stage 씬 HUD. 사람이 배치한 글자에 라운드 값만 넣는다 — UI를 코드로 만들지 않는다.
    // 이름으로 찾는 것 (Stage_BG 아래, 없으면 그 칸만 건너뛴다):
    //   Cur_Score(점수)와 그 옆 Score(“점수” 라벨, 번역만) · Stage_Gold(옛 Earned_Gold) 안의 글자(예상 골드) · Stage_Timer 안의 글자(남은 시간)·Timer_Fill(남은 시간 막대)
    //   Stage_Info/WeatherText·WeatherIcon(날씨) · StageText(스테이지 번호) · Cur_Combo(지금 가장 큰 콤보) · SpeedText(배속, Speed 누르면 바뀜)
    //   Pause(누르면 일시정지) · StageGimmick/GimmickName·GimmickDescription(보스 스테이지에만 보임)
    //   Harvest_End(시간이 다 되면 화면을 어둡게 하고 "수확 끝!", 안의 첫 글자가 문구). 배치 전이면 임시로 만든다 (옛 RoundHUD endDim·endTitle)
    [RequireComponent(typeof(StageController))]
    public class StageHUD : MonoBehaviour
    {
        StageController stage;
        TMP_Text score, scoreLabel, gold, timer, info, stageNo, combo, speed, gimmickName, gimmickDesc;
        GameObject gimmick;
        Image weatherIcon, timeFill, endDim;
        TMP_Text endTitle;
        static readonly Color TimeOk = U.Hex("#8cc47a"), TimeMid = U.Hex("#e8c25e"), TimeLow = U.Hex("#dd6a52"), TimeLow2 = U.Hex("#e8946a");   // 옛 RoundHUD와 같은 수채 색
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
            var timerT = SceneUI.Need(scene, "Stage_Timer")?.transform;
            timer = SceneUI.TextIn(timerT);
            timeFill = SceneUI.Find(timerT, "Timer_Fill")?.GetComponent<Image>();
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
            var end = SceneUI.Find(scene, "Harvest_End") ?? BuildHarvestEnd(timerT);
            if (end != null) { endDim = end.GetComponent<Image>(); endTitle = SceneUI.TextIn(end); end.gameObject.SetActive(false); }
        }

        // 임시 "수확 끝!" (옛 Game 씬과 같은 모양: 화면 덮개 + 가운데 위 큰 글자, 손글씨 그림이 있으면 그림)
        static Transform BuildHarvestEnd(Transform anyHud)
        {
            var canvas = anyHud != null ? anyHud.GetComponentInParent<Canvas>()?.rootCanvas : null;
            if (canvas == null) { Debug.LogWarning("[Stage] 캔버스가 없어 수확 끝 문구를 만들 수 없어요"); return null; }
            var dim = new GameObject("Harvest_End", typeof(RectTransform), typeof(Image));
            dim.layer = canvas.gameObject.layer;
            var drt = (RectTransform)dim.transform;
            drt.SetParent(canvas.transform, false);
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            var img = dim.GetComponent<Image>(); img.color = new Color(0, 0, 0, 0); img.raycastTarget = false;
            var title = new GameObject("EndTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            title.layer = dim.layer;
            var trt = (RectTransform)title.transform;
            trt.SetParent(drt, false);
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1); trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0, -480 * canvas.GetComponent<RectTransform>().rect.height / 1080f); trt.sizeDelta = new Vector2(900, 140);
            var t = title.GetComponent<TextMeshProUGUI>();
            LocFonts.Apply(t, LocFonts.Role.Title, 0);
            t.fontSize = 110; t.color = U.Hex("#fff3b0"); t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return drt;
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
            DrawHarvestEnd(R);
            if (score != null && R.score != shownScore) { shownScore = R.score; UIUtil.SetText(score, U.Fmt(R.score)); }
            double g = R.GoldSoFar;
            if (gold != null && g != shownGold) { shownGold = g; UIUtil.SetText(gold, U.Fmt(g)); }
            int tenths = Game.DBG.infinite ? int.MaxValue : Mathf.RoundToInt(Mathf.Max(0, R.timeLeft) * 10);
            DrawTimeBar(R, now);
            if (timer != null && tenths != shownTenths) { shownTenths = tenths; UIUtil.SetText(timer, Game.DBG.infinite ? "∞" : Loc.F("{0:F1}초", Mathf.Max(0, R.timeLeft))); }
            int c = 0;
            foreach (var b in R.balls) if (b.hits > c) c = b.hits;   // 콤보 = 떨어지기 전까지 버섯 타격 수
            if (combo != null && c != shownCombo) { shownCombo = c; UIUtil.SetText(combo, c.ToString()); }
            if (speed != null && StageController.Speed != shownSpeed) { shownSpeed = StageController.Speed; UIUtil.SetText(speed, "x" + shownSpeed); }
        }

        // 시간이 다 되면(phase "end", endT 1.5초 → 0) 화면이 조금 어두워지고 "수확 끝!"이 튀어나왔다가, 정산 창이 뜨면 사라진다
        void DrawHarvestEnd(RoundSim R)
        {
            if (endDim == null) return;
            bool on = R.phase == "end" && R.endT > 0;   // endT가 다 되면 정산 창이 뜬다(StageController.OnRoundEnd) — 그때 사라진다
            if (endDim.gameObject.activeSelf != on)
            {
                endDim.gameObject.SetActive(on);
                endDim.transform.SetAsLastSibling();
                if (on && endTitle != null) UIUtil.SetText(endTitle, Loc.T("수확 끝!"));
            }
            if (!on) return;
            float k = 1.5f - R.endT;
            endDim.color = new Color(0, 0, 0, Mathf.Min(0.35f, k * 0.5f));
            if (endTitle == null) return;
            float s = k < 0.2f ? 0.5f + k * 2.5f : 1;
            endTitle.rectTransform.localScale = new Vector3(s, s, 1);
            Lettering.Apply(endTitle, "harvest_end", 1.2f);
        }

        // 남은 시간 막대: 9슬라이스 막대는 오른쪽 끝을 옮겨 둥근 끝을 지키고, 채움(Filled) 그림이면 잘라 낸다. 5초·10초 아래로 색이 바뀐다
        void DrawTimeBar(RoundSim R, float now)
        {
            if (timeFill == null) return;
            float frac = Game.DBG.infinite || R.timeMax <= 0 ? 1 : Mathf.Clamp01(R.timeLeft / R.timeMax);
            if (timeFill.type == Image.Type.Filled) timeFill.fillAmount = frac;
            else
            {
                var ft = timeFill.rectTransform;
                float fx = Mathf.Max(frac, 0.04f);
                if (ft.anchorMax.x != fx) ft.anchorMax = new Vector2(fx, 1);
                timeFill.enabled = frac > 0;
            }
            timeFill.color = Game.DBG.infinite ? TimeOk : R.timeLeft <= 5 ? (Mathf.Sin(now * 12) > 0 ? TimeLow : TimeLow2) : R.timeLeft <= 10 ? TimeMid : TimeOk;
        }
    }
}
