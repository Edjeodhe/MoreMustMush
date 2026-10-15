using UnityEngine;
using UnityEngine.InputSystem;

namespace MoreMush
{
    // PC 단축키 (균사 트리 · 세계수 화면). 키와 액션 표는 씬에 적어 두고(MoreMush/UI/Apply PC Layout),
    // 화면 아래 안내 줄도 같은 표로 쓴다. 창이 열려 있으면 받지 않는다 (Esc로 창 닫기는 GameFlow가 한다).
    public class HubShortcuts : MonoBehaviour
    {
        [System.Serializable] public struct Bind { public Key key; public string act, arg; }
        public Bind[] binds = new Bind[0];

        bool modalWas;   // 창을 닫은 그 프레임의 Esc가 화면 이동으로 이어지지 않게 한 프레임 쉰다

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || GameFlow.I == null) return;
            bool modal = GameFlow.I.ModalOpen, skip = modal || modalWas;
            modalWas = modal;
            if (skip) return;
            foreach (var b in binds)
                if (b.key != Key.None && kb[b.key].wasPressedThisFrame) { GameFlow.I.OnAction(b.act, b.arg, null); break; }
        }
    }
}
