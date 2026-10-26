using TMPro;
using UnityEngine;
using static MoreMush.Game;

namespace MoreMush
{
    // 복귀 보상 창 (MainGame·OutGame 캔버스에 배치한 `ReturnPopup`). 10분 넘게 자리를 비웠다 돌아오면(App.Returned)
    // 수확 보따리에 쌓인 것을 보여 주고 그 자리에서 받게 한다.
    // 배치: ReturnPopup(늘 켜 둠, 빈 판) > Window(꺼 둠, 딤) > Box > Title · Body · Button_Claim · Button_Close
    // 세계수가 열리기 전, 보따리가 비었을 때, 다른 창이 열려 있을 때는 띄우지 않는다 (보따리는 세계수에서 받을 수 있다).
    public class ReturnPopup : MonoBehaviour
    {
        GameObject window;
        TMP_Text title, body;
        double away;   // 자리를 비운 시간 (ms)

        void Awake()
        {
            window = SceneUI.Need(transform, "Window")?.gameObject;
            title = SceneUI.TextIn(SceneUI.Need(transform, "Title"));
            body = SceneUI.TextIn(SceneUI.Need(transform, "Body"));
            SceneUI.Bind(SceneUI.Need(transform, "Button_Claim"), Claim);
            SceneUI.Bind(SceneUI.Need(transform, "Button_Close"), Close);
            if (window != null) window.SetActive(false);
        }

        // 처음 구독은 Start에서: 쌓인 복귀가 구독 즉시 창을 여는데, OnEnable 때는 그 뒤 sceneLoaded가 모달 수를 0으로 되돌린다
        bool started;
        void Start() { started = true; App.Returned += Open; }
        void OnEnable() { if (started) App.Returned += Open; }

        void OnDisable()
        {
            App.Returned -= Open;
            if (IsOpen) Close();
        }

        bool IsOpen => window != null && window.activeSelf;

        void Open(double awayMs)
        {
            if (G == null || window == null || IsOpen || App.ModalOpen || !Unlock.On(Unlock.WORLDTREE)) return;
            BagSettle();
            if (!BagReady()) return;
            away = awayMs;
            Render();
            window.SetActive(true);
            App.ModalPush();
            Snd.Ui();
        }

        void Render()
        {
            var b = G.farm.bag;
            UIUtil.SetText(title, Loc.T("자리를 비운 동안 수확했어요!"));
            UIUtil.SetText(body, Loc.F("<color=#2a6ab8>{0}</color> 동안 자리를 비웠어요", Dur(away)) + "\n"
                + UIUtil.Ic("gold") + U.Fmt(System.Math.Floor(b.gold))
                + (b.gem >= 1 ? $"  {UIUtil.Ic("gem")}{U.Fmt(System.Math.Floor(b.gem))}" : ""));
        }

        void Claim()
        {
            var r = BagClaim();
            if (r == null) { Snd.Err(); Close(); return; }
            Snd.Record();
            App.Notify(ClaimText(r.Value));
            Close();
        }

        void Close()
        {
            if (!IsOpen) return;
            window.SetActive(false);
            App.ModalPop();
        }

        // 보따리를 받았을 때 알림 (OutGame 보따리 버튼과 같은 문구)
        public static string ClaimText((double gold, int gem, int n) r) =>
            Loc.F("수확 보따리! 나무버섯 {0}개 · {1}+{2}", r.n, UIUtil.Ic("gold"), U.Fmt(r.gold)) + (r.gem > 0 ? $" {UIUtil.Ic("gem")}+{r.gem}" : "");

        static string Dur(double ms)
        {
            long m = (long)(ms / 60000);
            return m >= 1440 ? Loc.F("{0}일 {1}시간", m / 1440, m % 1440 / 60) : m >= 60 ? Loc.F("{0}시간 {1}분", m / 60, m % 60) : Loc.F("{0}분", m);
        }
    }
}
