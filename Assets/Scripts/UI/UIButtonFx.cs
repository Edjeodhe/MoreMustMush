using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoreMush
{
    // Prototype button feel: lift 2 px on hover, sink 2 px when pressed; dimmed when not interactable.
    public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform content;   // what moves: the label child, so the button's own layout stays intact
        bool hover, down;
        Button button;
        CanvasGroup group;
        Vector2 rest;

        void Awake()
        {
            button = GetComponent<Button>();
            if (content != null) rest = content.anchoredPosition;
            group = GetComponent<CanvasGroup>();
        }

        float shownY = float.NaN, shownA = -1;   // 마지막으로 쓴 값 (바뀔 때만 RectTransform·CanvasGroup을 건드린다)

        // 마우스를 올린 채 버튼이 꺼지면 Exit가 오지 않아, 다시 켜질 때 2px 떠 있었다
        void OnDisable() { hover = down = false; }

        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
        public void OnPointerDown(PointerEventData e) => down = true;
        public void OnPointerUp(PointerEventData e) => down = false;

        void Update()
        {
            bool on = button == null || button.interactable;
            float y = !on ? 0 : down ? -2 : hover ? 2 : 0, a = on ? 1 : 0.6f;
            if (content != null && y != shownY) { shownY = y; content.anchoredPosition = rest + new Vector2(0, y); }
            if (group != null && a != shownA) { shownA = a; group.alpha = a; }
        }
    }
}
