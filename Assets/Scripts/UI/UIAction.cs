using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // Button → GameFlow action, like the prototype's data-act / data-id attributes.
    // Set `act` (and optional `arg`) in the inspector; the click is routed to GameFlow.OnAction.
    [RequireComponent(typeof(Button))]
    public class UIAction : MonoBehaviour
    {
        public string act;
        public string arg;

        void Awake() => GetComponent<Button>().onClick.AddListener(() => GameFlow.I.OnAction(act, arg, this));
    }
}
