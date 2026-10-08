using TMPro;
using UnityEngine;

namespace MoreMush
{
    // Short message at the top center that fades out after 1.6 s (prototype toast()).
    public class Toast : MonoBehaviour
    {
        public CanvasGroup group;
        public TMP_Text text;
        public RectTransform rect;
        float hideAt = -1;
        Vector2 origin;

        void Awake()
        {
            group.alpha = 0;
            origin = rect.anchoredPosition;
        }

        public void Show(string msg)
        {
            text.text = msg;
            hideAt = Time.unscaledTime + 1.6f;
            transform.SetAsLastSibling();
        }

        void Update()
        {
            bool on = Time.unscaledTime < hideAt;
            if (!on && group.alpha == 0) return;
            group.alpha = Mathf.MoveTowards(group.alpha, on ? 1 : 0, Time.unscaledDeltaTime / 0.25f);
            rect.anchoredPosition = origin + new Vector2(0, (1 - group.alpha) * 20);
        }
    }
}
