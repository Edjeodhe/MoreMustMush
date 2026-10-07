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

        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
        public void OnPointerDown(PointerEventData e) => down = true;
        public void OnPointerUp(PointerEventData e) => down = false;

        void Update()
        {
            bool on = button == null || button.interactable;
            if (content != null) content.anchoredPosition = rest + new Vector2(0, !on ? 0 : down ? -2 : hover ? 2 : 0);
            if (group != null) group.alpha = on ? 1 : 0.6f;
        }
    }
}
