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

        void OnEnable() { cam = GetComponent<Camera>(); lastW = 0; }

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
