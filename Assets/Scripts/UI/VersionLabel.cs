using UnityEngine;

namespace MoreMush
{
    // 버전 글자를 게임 화면(16:9로 맞춘 카메라 영역, AspectLetterbox)의 왼쪽 아래에 붙인다.
    // 버전 캔버스는 창 전체를 덮는 오버레이라, 창 비율이 16:9가 아니면 그냥 두면 검은 여백에 찍힌다
    public class VersionLabel : MonoBehaviour
    {
        public Vector2 margin = new Vector2(12, 8);
        RectTransform rt, canvasRt;
        Camera cam;

        void Awake()
        {
            rt = (RectTransform)transform;
            canvasRt = (RectTransform)GetComponentInParent<Canvas>().transform;
        }

        void LateUpdate()
        {
            if (cam == null) { var lb = FindAnyObjectByType<AspectLetterbox>(); cam = lb != null ? lb.GetComponent<Camera>() : Camera.main; }
            Rect v = cam != null ? cam.rect : new Rect(0, 0, 1, 1);
            var corner = new Vector2(v.x * Screen.width, v.y * Screen.height);   // 게임 화면 왼쪽 아래 (화면 픽셀)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, corner, null, out var local);
            rt.anchoredPosition = local - canvasRt.rect.min + margin;
        }
    }
}
