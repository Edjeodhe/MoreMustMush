using TMPro;
using UnityEngine;
using static MoreMush.Defs;

namespace MoreMush
{
    // 엔딩 가안 (MainGame·OutGame 캔버스에 배치한 `EndingScreen`). 도감을 다 채운 정산 뒤 들어온 씬에서 한 번 글자로 알린다.
    // 배치: EndingScreen(늘 켜 둠, 빈 판) > Window(꺼 둠, 검은 판 + Button) > Text. 아무 데나 누르면 닫힌다.
    // 컷신이 생기면 이 창을 바꾼다.
    public class EndingScreen : MonoBehaviour
    {
        GameObject window;

        void Awake()
        {
            window = SceneUI.Need(transform, "Window")?.gameObject;
            SceneUI.Bind(window?.transform, Close);
            if (window != null) window.SetActive(false);
        }

        // Start에서: sceneLoaded가 모달 수를 0으로 되돌린 뒤라야 ModalPush가 남는다
        void Start()
        {
            if (!RoundFlow.EndingReached || window == null) return;
            RoundFlow.EndingReached = false;
            UIUtil.SetText(SceneUI.TextIn(window.transform),
                $"{Loc.T("엔딩")}\n<size=35%>{Loc.F("버섯 도감 {0}종 완성!", SP_TOTAL)}</size>\n\n<size=22%><color=#c8c8c8>{Loc.T("화면을 누르면 계속해요")}</color></size>");
            window.SetActive(true);
            App.ModalPush();
            Snd.Rare();
        }

        void OnDisable() => Close();

        void Close()
        {
            if (window == null || !window.activeSelf) return;
            window.SetActive(false);
            App.ModalPop();
        }
    }
}
