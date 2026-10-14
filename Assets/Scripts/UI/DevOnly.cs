using UnityEngine;

namespace MoreMush
{
    // 개발·테스트 전용 UI(일괄 강화 버튼 등). 스크립팅 정의 MOREMUSH_DEV가 없으면(출시 빌드 기본) 처음 켜질 때 스스로 꺼진다.
    // 다시 쓰려면 Player Settings > Scripting Define Symbols에 MOREMUSH_DEV를 넣는다 (GameFlow의 디버그 모드·dbg- 액션도 같은 정의로 묶임)
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
