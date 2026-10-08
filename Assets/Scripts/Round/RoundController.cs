using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MoreMush
{
    // Drives a harvest round (prototype loop(), pointer handlers and keys for SCREEN === 'round').
    public class RoundController : MonoBehaviour
    {
        public RoundView view;
        [ScenePath("Canvas/RoundUI/RoundHUD")] public RoundHUD hud;
        [ScenePath("Canvas/RoundUI/ComboHUD")] public ComboHUD combo;
        [ScenePath("Canvas")] public RectTransform stage;            // 1920×1080 canvas rect used to map pointers to stage pixels
        [ScenePath("Main Camera")] public Camera cam;

        public static int SPEED = 1;
        public static bool IsTouch => Touchscreen.current != null && Mouse.current == null;
        Vector2 mouse = new Vector2(960, 540);
        RoundSim finishedR;   // 이미 정산을 넘긴 라운드. 정산 중 예외가 나도 다음 프레임에 또 정산(골드·판 수 중복)하지 않게

        public void Begin(string weatherId, string themeId)
        {
            var round = RoundSim.Start(weatherId, themeId);
            hud.BindFlyerTargets(round);
            view.Clear();
            gameObject.SetActive(true);
        }

        public static void NextSpeed() => SPEED = SPEED == 1 ? 2 : SPEED == 2 ? 4 : 1;

        // Screen point → prototype stage pixels (0..1920, 0..1080, y down)
        public Vector2 ToStage(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stage, screen, cam, out var local);
            var r = stage.rect;
            return new Vector2(local.x - r.xMin, r.yMax - local.y);
        }

        void Update()
        {
            var R = RoundSim.R;
            if (R == null) return;
            float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            HandleInput(R);

            // 바는 마우스 x를 부드럽게 따라감
            float target = U.Clamp(mouse.x / RoundSim.ZOOM, RoundSim.FX0 + R.barLen / 2, RoundSim.FX1 - R.barLen / 2);
            bool modal = GameFlow.I != null && GameFlow.I.ModalOpen;
            if (!R.paused) R.barX = U.Lerp(R.barX, target, 1 - Mathf.Exp(-18 * dt));
            bool live = !R.paused && !modal && R.hitstop <= 0;   // 이번 프레임에 시뮬레이션이 진행되는지
            if (!R.paused && !modal)
            {
                if (R.hitstop > 0) R.hitstop -= dt;
                else
                {
                    float total = dt * SPEED * Game.DBG.speed;
                    while (total > 1e-6f && RoundSim.R != null) { float h = Mathf.Min(total, 1 / 60f); R.SimFrame(h); total -= h; }
                }
            }
            if (RoundSim.R == null) return;
            float now = Time.time;
            view.Draw(R, live);
            hud.Draw(R, now, SPEED, IsTouch);
            combo.Draw(R, now);
            if (R.phase == "end" && R.endT <= 0 && finishedR != R) { finishedR = R; GameFlow.I.FinishRound(); }
        }

        void HandleInput(RoundSim R)
        {
            var p = Pointer.current;
            if (p == null) return;
            var sp = ToStage(p.position.ReadValue());
            bool isMouse = p is Mouse;
            if (isMouse || p.press.isPressed) mouse = sp;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.fKey.wasPressedThisFrame) NextSpeed();
                if (kb.escapeKey.wasPressedThisFrame) { if (GameFlow.I.ModalOpen) GameFlow.I.CloseModal(); else GameFlow.I.OpenPause(); }
            }
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) NextSpeed();
            // 조준 중에 일시정지·창이 열리면 조준을 버린다 (창을 닫는 클릭이 엉뚱한 방향 발사가 되지 않게)
            if (R.paused || GameFlow.I.ModalOpen) { R.aimStart = R.aimCur = null; return; }

            var field = new Vector2(sp.x / RoundSim.ZOOM, sp.y / RoundSim.ZOOM);
            if (p.press.wasPressedThisFrame)
            {
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();   // -1 = 지금 포인터 (터치 id는 0이 아님)
                if (!overUI && R.phase == "aim" && U.D2(field.x, field.y, RoundSim.LAUNCH.x, RoundSim.LAUNCH.y) < 160 * 160)
                    R.aimStart = R.aimCur = field;
            }
            if (R.aimStart.HasValue && p.press.isPressed) R.aimCur = field;
            if (R.aimStart.HasValue && p.press.wasReleasedThisFrame)
            {
                float dx = R.aimStart.Value.x - field.x, dy = R.aimStart.Value.y - field.y;
                R.aimStart = R.aimCur = null;
                if (Mathf.Sqrt(dx * dx + dy * dy) >= 20 && R.phase == "aim") R.Launch(Mathf.Atan2(dy, dx));
            }
        }

        void OnApplicationFocus(bool focus)
        {
            var R = RoundSim.R;
            if (!focus && R != null && !R.paused && R.phase != "end" && GameFlow.I != null) GameFlow.I.OpenPause();
        }
    }
}
