using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // Keeps the camera viewport at 16:9 with black bars (prototype fit(): scale the 1920×1080 stage to the window).
    // CanvasScaler measures the whole window, not the letterboxed viewport, so its match value is set here:
    // wide window → viewport height = window height (match 1), tall window → viewport width = window width (match 0).
    // That keeps the canvas exactly 1920×1080 at any window size or resolution.
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public class AspectLetterbox : MonoBehaviour
    {
        public float aspect = 16f / 9f;
        Camera cam;
        int lastW, lastH;

        void OnEnable() { cam = GetComponent<Camera>(); lastW = 0; if (Application.isPlaying) EnsureBars(); }

        // 16:9 밖 여백(검은 띠)을 매 프레임 지우는 카메라. 게임 카메라는 자기 영역만 지워서, 없으면 여백에 지난 프레임이 남는다
        // (창 크기를 바꾸거나 화면 밖에 걸친 오버레이 글자 — 예: 버전 표시 — 의 잔상이 겹쳐 보였다)
        static Camera bars;
        void EnsureBars()
        {
            if (bars != null) return;
            var go = new GameObject("LetterboxBars");
            DontDestroyOnLoad(go);
            bars = go.AddComponent<Camera>();
            bars.clearFlags = CameraClearFlags.SolidColor; bars.backgroundColor = Color.black;
            bars.cullingMask = 0; bars.rect = new Rect(0, 0, 1, 1); bars.depth = cam.depth - 10;
        }

        void Update()
        {
            if (Screen.width == lastW && Screen.height == lastH) return;
            lastW = Screen.width; lastH = Screen.height;
            float win = (float)Screen.width / Mathf.Max(1, Screen.height);
            cam.rect = win > aspect
                ? new Rect((1 - aspect / win) / 2, 0, aspect / win, 1)
                : new Rect(0, (1 - win / aspect) / 2, 1, win / aspect);
            MatchCanvases(win > aspect ? 1f : 0f);
        }

        void MatchCanvases(float match)
        {
            foreach (var scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var canvas = scaler.GetComponent<Canvas>();
                if (canvas == null || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != cam) continue;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = match;
            }
        }
    }
}
