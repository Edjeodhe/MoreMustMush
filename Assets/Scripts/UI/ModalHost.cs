using UnityEngine;
using UnityEngine.EventSystems;

namespace MoreMush
{
    // Dimmed modal layer (prototype #modal): shows one child panel at a time; clicking the dim closes it.
    public class ModalHost : MonoBehaviour, IPointerClickHandler
    {
        GameObject current;
        System.Action onClose;

        public bool IsOpen => gameObject.activeSelf;

        public void Open(GameObject panel, System.Action closeCb = null)
        {
            if (current != null && current != panel) current.SetActive(false);
            gameObject.SetActive(true);
            current = panel;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            onClose = closeCb;
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            if (current != null) current.SetActive(false);
            current = null;
            gameObject.SetActive(false);
            var f = onClose; onClose = null;
            f?.Invoke();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.pointerCurrentRaycast.gameObject == gameObject) Close();
        }
    }
}
