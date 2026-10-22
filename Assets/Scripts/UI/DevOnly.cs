using UnityEngine;

namespace MoreMush
{
    // 개발·테스트 전용 UI(개발자 패널 등). 스크립팅 정의 MOREMUSH_DEV가 있을 때만 켜진다 (에디터 포함).
    // 그 밖에는 처음 켜질 때 스스로 꺼진다. 모바일 재구축 중이라 에디터·Development Build에서도 기본으로 끈다
    public class DevOnly : MonoBehaviour
    {
#if MOREMUSH_DEV
        public const bool ON = true;
#else
        public const bool ON = false;
#endif
        void Awake() { if (!ON) gameObject.SetActive(false); }
    }
}
