using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 팝업 하나. 루트(화면 전체, CanvasGroup)에 붙인다. 닫혀 있을 때는 루트를 끈다.
    // BottomSheet: 시트가 화면 아래에서 올라온다 (의뢰·공방). FullScreen: 화면 전체가 살짝 커지며 나타난다 (도감).
    // Center: 가운데 창이 FullScreen과 같은 방식으로 나타난다 (옵션)
    [RequireComponent(typeof(CanvasGroup))]
    public class UiPopup : MonoBehaviour
    {
        public enum Style { BottomSheet, FullScreen, Center }

        [SerializeField] Style style = Style.BottomSheet;
        [SerializeField] RectTransform sheet;     // 움직이는 본체
        [SerializeField] Button dim;              // 시트 바깥(어두운 배경)을 누르면 닫힘. 없어도 된다
        [SerializeField] Button closeButton;
        [SerializeField] float duration = 0.25f;
        [SerializeField] float overhang = 160f;   // 시트 위로 튀어나온 탭 높이. 닫힐 때 이만큼 더 내려 완전히 숨긴다

        CanvasGroup group;
        Coroutine anim;

        public bool IsOpen { get; private set; }
        public event System.Action Opened;
        public event System.Action Closed;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (dim != null) dim.onClick.AddListener(Close);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Play(true);
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Play(false);
            Closed?.Invoke();
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        void Play(bool open)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Animate(open));
        }

        IEnumerator Animate(bool open)
        {
            group.blocksRaycasts = open;
            // 레이아웃이 잡힌 뒤의 높이를 써야 시트가 정확히 화면 밖에서 출발한다
            Canvas.ForceUpdateCanvases();
            float hidden = sheet != null ? -(sheet.rect.height + overhang) : 0f;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                Apply(open ? EaseOut(k) : 1f - EaseOut(k), hidden);
                yield return null;
            }
            Apply(open ? 1f : 0f, hidden);
            if (!open) gameObject.SetActive(false);
            anim = null;
        }

        // p: 0 = 닫힘, 1 = 열림
        void Apply(float p, float hidden)
        {
            group.alpha = style == Style.BottomSheet ? 1f : p;
            if (sheet == null) return;
            if (style == Style.BottomSheet)
            {
                sheet.anchoredPosition = new Vector2(sheet.anchoredPosition.x, Mathf.Lerp(hidden, 0f, p));
                if (dim != null) dim.targetGraphic.canvasRenderer.SetAlpha(p);
            }
            else
                sheet.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, p);
        }

        static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);
    }
}
