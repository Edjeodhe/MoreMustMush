using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // 버튼에 붙는 액션 이름표 (프로토타입 data-act / data-id). 클릭 처리는 이 버튼을 가진 화면이 act를 보고 붙인다
    // (지금은 Stage의 정산 창 SettleScreen이 totree · worldtree · dmgstats를 받는다).
    [RequireComponent(typeof(Button))]
    public class UIAction : MonoBehaviour
    {
        public string act;
        public string arg;
    }
}
