using UnityEngine;

namespace MoreMush
{
    // 개발·테스트 전용 UI(개발자 패널 등). 에디터 · Development Build · 스크립팅 정의 MOREMUSH_DEV 중 하나일 때만 켜지고,
    // 출시 빌드(MoreMush/Build 메뉴의 WebGL·Windows)에서는 처음 켜질 때 스스로 꺼진다. 개발용 빌드는 MoreMush/Build/WebGL (개발용)
    public class DevOnly : MonoBehaviour
    {
#if MOREMUSH_DEV || DEVELOPMENT_BUILD || UNITY_EDITOR
        public const bool ON = true;
#else
        public const bool ON = false;
#endif
        void Awake() { if (!ON) gameObject.SetActive(false); }
    }
}
