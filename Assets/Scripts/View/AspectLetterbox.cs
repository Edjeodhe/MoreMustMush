using UnityEngine;

namespace MoreMush
{
    // Keeps the camera viewport at 16:9 with black bars (prototype fit(): scale the 1920×1080 stage to the window).
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
        }
    }
}
