using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MoreMush
{
    // Stage 씬: 라운드를 시작하고, 입력(조준·발사·바)과 시간을 돌리고, 끝나면 정산을 넘긴다.
    // 화면은 RoundView(월드)가 그린다. HUD·일시정지·정산 창은 씬에 배치되면 이벤트에 붙여 연결한다.
    public class StageController : MonoBehaviour
    {
        [Tooltip("라운드를 그리는 월드 뷰 (Prefabs/Round 프리팹을 쓰는 RoundView)")]
        public RoundView view;
        [Tooltip("1920×1080 기준 캔버스 영역. 비우면 화면 전체를 1920×1080으로 본다")]
        public RectTransform stageRect;
        public Camera cam;

        public static StageController I { get; private set; }

        // 배속 (×1 · ×2 · ×4)
        public static int Speed { get; private set; } = 1;
        public static bool IsTouch => Touchscreen.current != null && Mouse.current == null;

        // UI가 붙는 곳
        // HUD 초기화 (harvestTargets 채우기 등). 라운드가 이미 시작된 뒤에 구독하면 바로 한 번 불린다 (실행 순서와 무관)
        public event System.Action<RoundSim> Started
        {
            add { started += value; if (begun != null) value(begun); }
            remove => started -= value;
        }
        public event System.Action<RoundSim, float> Drawn;            // 매 프레임 HUD 갱신 (라운드, Time.time)
        public event System.Action<bool> PauseChanged;                // 일시정지 켜기/끄기 (창은 이 컨트롤러가 옵션 창으로 연다)
        public event System.Action<RoundSim.Summary> Finished;        // 정산 창. 받는 UI가 없으면 바로 MainGame으로

        const float MAX_DT = 0.05f, SIM_STEP = 1 / 60f;
        // 한 프레임의 시뮬 시간 상한(ms): 넘으면 남은 시간을 버린다. 느린 프레임 다음에 시뮬을 몰아서 돌려 더 느려지는 되먹임을 끊는다
        // (평소 x4에서도 한 프레임 1ms 안팎이라 걸리지 않는다)
        const double SIM_BUDGET_MS = 8;
        readonly System.Diagnostics.Stopwatch simWatch = new System.Diagnostics.Stopwatch();
        const float GRAB_R = 160, MIN_PULL = 20;   // 수확기를 잡는 반경 · 발사로 치는 최소 당김 (필드 px)

        Vector2 mouse = StageSpace.SIZE / 2;
        OptionWindow optionWin;   // 일시정지 창 = 옵션 창. 일시정지 버튼·Esc·포커스 잃음이 모두 이 창을 열고 게임을 멈춘다
        RoundSim finishedR;   // 이미 정산을 넘긴 라운드 (정산 중 예외가 나도 다시 정산하지 않게)
        RoundSim begun;       // 이 씬에서 시작한 라운드
        public RoundSim Round => begun;   // 개발자 패널이 읽는다
        System.Action<RoundSim> started;

        void Awake()
        {
            I = this;
            optionWin = OptionWindow.For(gameObject.scene, () => { Snd.Ui(); CloseOption(); },
                new OptionWindow.Extra("Button_EndNow", "지금 끝내기", () => { Snd.Ui(); optionWin.Close(); EndNow(); }));
        }
        // 씬을 떠날 때 이전 씬은 먼저 꺼지고 나중에 비동기로 내려진다 → 끌 때 배속·I를 돌려놓는다 (다음 씬이 x4로 움직이지 않게)
        void OnDisable() { if (I == this) I = null; Time.timeScale = 1; }

        // 플레이 시작 때 이전 플레이의 배속을 지운다 (도메인 리로드를 끈 플레이 모드 대비)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; Speed = 1; }

        void Start()
        {
            App.EnsureSave();
            if (cam == null) cam = Camera.main;
            if (view == null) { Debug.LogError("[Stage] RoundView가 연결되지 않았어요"); enabled = false; return; }
            begun = RoundFlow.Begin();
            view.Clear();
            started?.Invoke(begun);
            Tutorial.ForStage(this);   // 1스테이지 첫 판: 조준 안내
            Snd.WarmRound();
        }

        // ===== 버튼에서 부르는 것 =====
        public static void NextSpeed() { Speed = Speed == 1 ? 2 : Speed == 2 ? 4 : 1; Snd.Ui(); }

        // 일시정지 = 옵션 창을 열고 게임 시간을 멈춘다 (라운드 중에만). 끝난 뒤에는 창만 연다
        public void SetPaused(bool on)
        {
            var R = RoundSim.R;
            if (R == null || R.phase == "end") { if (!on) optionWin.Close(); return; }
            SetPausedRaw(R, on);
        }

        // 일시정지 버튼·Esc: 라운드 중이면 일시정지 켜고 끄기, 끝났으면 옵션 창만 열고 닫기
        public void TogglePause()
        {
            var R = RoundSim.R;
            if (R != null && R.phase != "end") SetPaused(!R.paused);
            else if (optionWin.IsOpen) optionWin.Close(); else optionWin.Open();
        }

        void CloseOption() { if (RoundSim.R != null && RoundSim.R.paused) SetPaused(false); else optionWin.Close(); }

        // 라운드를 지금 끝내고 정산 (스테이지는 오르지 않는다)
        public void EndNow()
        {
            var R = RoundSim.R;
            if (R == null || R.phase == "end") return;
            R.earlyExitSec = R.timeMax - R.timeLeft; R.timeLeft = 0; R.phase = "end"; R.endT = 0.01f;
            SetPausedRaw(R, false);
        }

        // 정산을 보고 업그레이드 화면으로
        public void ToMainGame() { Snd.Ui(); LeaveSettle(); }

        static void LeaveSettle() { RoundFlow.AfterSettle(); App.Go(Scenes.MainGame); }

        // 시뮬은 R.paused로, 화면 연출(Time.time 흔들림·비·대기 동작)은 timeScale 0으로 멈춘다. 라운드 루프는 unscaledDeltaTime이라 영향 없다
        void SetPausedRaw(RoundSim R, bool on)
        {
            if (R.paused == on) return;
            R.paused = on;
            Time.timeScale = on ? 0 : 1;
            if (on) optionWin.Open(); else optionWin.Close();
            PauseChanged?.Invoke(on);
        }

        // ===== 루프 =====
        void Update()
        {
            var R = RoundSim.R;
            if (R == null) return;
            float dt = Mathf.Min(MAX_DT, Time.unscaledDeltaTime);
            HandleInput(R);

            bool hold = R.paused || App.ModalOpen;
            // 바는 마우스 속도를 100% 따라온다 (전투 기획서 Ver05)
            if (!R.paused) R.barX = U.Clamp(mouse.x / RoundSim.ZOOM, RoundSim.FX0 + R.barLen / 2, RoundSim.FX1 - R.barLen / 2);
            bool live = !hold && R.hitstop <= 0;
            if (!hold)
            {
                if (R.hitstop > 0) R.hitstop -= dt;
                else
                {
                    float total = dt * Speed * Game.DBG.speed;
                    simWatch.Restart();
                    while (total > 1e-6f && RoundSim.R != null)
                    {
                        float h = Mathf.Min(total, SIM_STEP); R.SimFrame(h); total -= h;
                        if (simWatch.Elapsed.TotalMilliseconds > SIM_BUDGET_MS) break;
                    }
                }
            }
            if (RoundSim.R == null) return;
            view.Draw(R, live);
            Drawn?.Invoke(R, Time.time);
            if (R.phase == "end" && R.endT <= 0 && finishedR != R) { finishedR = R; OnRoundEnd(); }
        }

        void OnRoundEnd()
        {
            var sum = RoundFlow.Finish();
            if (Finished != null) Finished(sum);
            else StageSettle.Show(gameObject.scene, sum, ToMainGame);   // 씬에 정산 UI가 없으면 임시 정산 창
        }

        void HandleInput(RoundSim R)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.fKey.wasPressedThisFrame) NextSpeed();
                if (kb.escapeKey.wasPressedThisFrame && (optionWin.IsOpen || !App.ModalOpen)) { Snd.Ui(); TogglePause(); }
            }
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) NextSpeed();

            var p = Pointer.current;
            if (p == null) return;
            var sp = ToStage(p.position.ReadValue());
            if (p is Mouse || p.press.isPressed) mouse = sp;
            // 조준 중에 일시정지·창이 열리면 조준을 버린다 (창을 닫는 클릭이 엉뚱한 방향 발사가 되지 않게)
            if (R.paused || App.ModalOpen) { R.aimStart = R.aimCur = null; return; }

            var field = sp / RoundSim.ZOOM;
            if (p.press.wasPressedThisFrame)
            {
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!overUI && R.phase == "aim" && U.D2(field.x, field.y, RoundSim.LAUNCH.x, RoundSim.LAUNCH.y) < GRAB_R * GRAB_R)
                    R.aimStart = R.aimCur = field;
            }
            if (R.aimStart.HasValue && p.press.isPressed) R.aimCur = field;
            if (R.aimStart.HasValue && p.press.wasReleasedThisFrame)
            {
                float dx = R.aimStart.Value.x - field.x, dy = R.aimStart.Value.y - field.y;
                R.aimStart = R.aimCur = null;
                if (Mathf.Sqrt(dx * dx + dy * dy) >= MIN_PULL && R.phase == "aim") R.Launch(Mathf.Atan2(dy, dx));
            }
        }

        // 화면 좌표 → 스테이지 픽셀 (0..1920, 0..1080, y 아래로)
        public Vector2 ToStage(Vector2 screen) => StageSpace.FromScreen(stageRect, cam, screen);

        void OnApplicationFocus(bool focus)
        {
            var R = RoundSim.R;
            // 창을 벗어나면 일시정지 창을 띄우고 멈춘다
            if (!focus && R != null && R.phase != "end") SetPaused(true);
        }
    }
}
