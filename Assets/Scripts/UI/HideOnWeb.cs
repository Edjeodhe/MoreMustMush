using UnityEngine;

namespace MoreMush
{
    // 웹(WebGL)에서는 쓸 수 없는 UI(게임 종료 버튼 — 브라우저 탭은 게임이 닫을 수 없다)를 숨긴다
    public class HideOnWeb : MonoBehaviour
    {
        void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            gameObject.SetActive(false);
#endif
        }
    }
}
