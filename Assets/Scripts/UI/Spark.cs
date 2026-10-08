using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // Little dot flying out of a purchase (prototype .spark CSS animation).
    public class Spark : MonoBehaviour
    {
        public Vector2 vel;
        float t;
        Vector2 start;
        Image img;
        void Start() { start = ((RectTransform)transform).anchoredPosition; img = GetComponent<Image>(); }
        void Update()
        {
            t += Time.unscaledDeltaTime / 0.6f;
            var rt = (RectTransform)transform;
            rt.anchoredPosition = start + vel * t;
            rt.localScale = Vector3.one * Mathf.Lerp(1, 0.2f, t);
            var c = img.color; c.a = 1 - t; img.color = c;
            if (t >= 1) Destroy(gameObject);
        }
    }
}
