using UnityEngine;

namespace MoreMush
{
    // 버전 글자를 화면의 오른쪽 아래에 붙인다.
    // 왼쪽 아래는 허브 화면의 단축키 안내 줄이 쓴다.
    public class VersionLabel : MonoBehaviour
    {
        public Vector2 margin = new Vector2(-12, 8);
        RectTransform rt, canvasRt;

        void Awake()
        {
            rt = (RectTransform)transform;
            canvasRt = (RectTransform)GetComponentInParent<Canvas>().transform;
        }

        void LateUpdate()
        {
            var corner = new Vector2(Screen.width, 0);   // 화면 오른쪽 아래 (화면 픽셀)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, corner, null, out var local);
            rt.anchoredPosition = local - canvasRt.rect.min + margin;
        }
    }
}
