using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoreMush
{
    // Installed on active buttons automatically, including buttons created by popups.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform content; // Existing buttons keep their label lift/press feedback.
        const float HoverTint = 0.82f;
        const float PressScale = 0.94f;
        const float Speed = 22f;
        Button button;
        Graphic graphic;
        Vector3 restScale;
        Vector2 restContentPosition;
        Color baseColor, writtenColor;
        bool hover, down, wroteColor;
        float scale = 1f, tint = 1f;

        void Awake()
        {
            button = GetComponent<Button>();
            graphic = button.targetGraphic;
            if (graphic == null) graphic = GetComponent<Graphic>();
            restScale = transform.localScale;
            if (content != null) restContentPosition = content.anchoredPosition;
        }

        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && button.IsInteractable()) down = true;
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) down = false;
        }

        void LateUpdate()
        {
            bool active = button.isActiveAndEnabled && button.IsInteractable();
            if (!active) { ResetVisuals(); return; }
            float step = 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime);
            float targetScale = down ? PressScale : 1f;
            scale = Mathf.Lerp(scale, targetScale, step);
            if (Mathf.Abs(scale - targetScale) < 0.0001f) scale = targetScale;
            var nextScale = restScale * scale;
            if (transform.localScale != nextScale) transform.localScale = nextScale;
            if (content != null)
            {
                var position = restContentPosition + Vector2.up * (down ? -2f : hover ? 2f : 0f);
                if (content.anchoredPosition != position) content.anchoredPosition = position;
            }

            float targetTint = hover || down ? HoverTint : 1f;
            tint = Mathf.Lerp(tint, targetTint, step);
            if (Mathf.Abs(tint - targetTint) < 0.0001f) tint = targetTint;
            if (graphic == null) return;
            // Keep changes made by selection, language and card rendering code.
            if (!wroteColor || graphic.color != writtenColor) baseColor = graphic.color;
            writtenColor = new Color(baseColor.r * tint, baseColor.g * tint, baseColor.b * tint, baseColor.a);
            if (graphic.color != writtenColor) graphic.color = writtenColor;
            wroteColor = true;
        }

        void ResetVisuals()
        {
            hover = down = false;
            scale = tint = 1f;
            if (button != null && transform.localScale != restScale) transform.localScale = restScale;
            if (content != null) content.anchoredPosition = restContentPosition;
            if (graphic != null && wroteColor && graphic.color == writtenColor) graphic.color = baseColor;
            wroteColor = false;
        }
        void OnDisable() => ResetVisuals();
        void OnApplicationFocus(bool focused) { if (!focused) ResetVisuals(); }
        void OnApplicationPause(bool paused) { if (paused) ResetVisuals(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var host = new GameObject("Button Feedback");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<UIButtonFxInstaller>();
        }
    }

    // Selectable's registry avoids scene-wide searches and includes newly enabled buttons.
    // Runs before EventSystem.Update so a new button has handlers before its first click.
    [DefaultExecutionOrder(-10000)]
    internal class UIButtonFxInstaller : MonoBehaviour
    {
        Selectable[] buffer = new Selectable[128];
        void Update()
        {
            if (buffer.Length < Selectable.allSelectableCount)
                System.Array.Resize(ref buffer, Mathf.NextPowerOfTwo(Selectable.allSelectableCount));
            int count = Selectable.AllSelectablesNoAlloc(buffer);
            for (int i = 0; i < count; i++)
            {
                if (!(buffer[i] is Button b) || b.TryGetComponent<UIButtonFx>(out _)) continue;
                // Dim overlays and click-catching popup containers are not visible controls.
                if (b.name == "Dim" || b.name == "Box" || b.name == "LangPanel" || b.name == "LangPopup") continue;
                b.gameObject.AddComponent<UIButtonFx>();
            }
            System.Array.Clear(buffer, 0, count);
        }
    }
}
