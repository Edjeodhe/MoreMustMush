using UnityEngine;

namespace MoreMush
{
    // 화면 좌표 → 스테이지 픽셀 (0..1920, 0..1080, 왼쪽 위 원점, y 아래로). Stage(라운드)와 MainGame(트리)이 같이 쓴다
    public static class StageSpace
    {
        public static readonly Vector2 SIZE = new Vector2(1920, 1080);

        // rect: 1920×1080 기준 캔버스 영역. 없으면 화면 가운데에 1920×1080을 맞춰 넣은(레터박스) 것으로 본다
        public static Vector2 FromScreen(RectTransform rect, Camera cam, Vector2 screen)
        {
            if (rect != null)
            {
                var canvas = rect.GetComponentInParent<Canvas>();
                var camForUI = canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camForUI, out var local);
                var r = rect.rect;
                return new Vector2(local.x - r.xMin, r.yMax - local.y);
            }
            float s = Mathf.Min(Screen.width / SIZE.x, Screen.height / SIZE.y);
            var off = new Vector2((Screen.width - SIZE.x * s) / 2, (Screen.height - SIZE.y * s) / 2);
            return new Vector2((screen.x - off.x) / s, SIZE.y - (screen.y - off.y) / s);
        }
    }
}
