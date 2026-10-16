using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush
{
    // 사람이 배치한 씬 UI를 이름으로 찾아 코드에 잇는 도우미. UI를 만들지는 않는다.
    // 찾기는 Awake에서 한 번만 하고, 찾은 참조를 들고 쓴다 (매 프레임 찾지 않는다).
    public static class SceneUI
    {
        // 씬 전체에서 이름으로 (비활성 포함, 처음 찾은 것)
        public static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = Find(root.transform, name);
                if (t != null) return t;
            }
            return null;
        }

        // root 아래에서 이름으로 (root 자신 포함, 비활성 포함)
        public static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var t = Find(c, name);
                if (t != null) return t;
            }
            return null;
        }

        public static GameObject Need(Scene scene, string name)
        {
            var t = Find(scene, name);
            if (t == null) Debug.LogWarning($"[UI] 씬에서 '{name}'을(를) 찾지 못했어요");
            return t != null ? t.gameObject : null;
        }

        public static Transform Need(Transform root, string name)
        {
            var t = Find(root, name);
            if (t == null && root != null) Debug.LogWarning($"[UI] '{root.name}' 안에서 '{name}'을(를) 찾지 못했어요");
            return t;
        }

        // 버튼에 동작을 건다. 이름 있는 메서드는 두 번 걸리지 않게 먼저 뺀다 (람다는 매번 새것이라 빠지지 않으니 Awake에서 한 번만 부른다)
        public static Button Bind(Transform t, UnityAction action)
        {
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b == null) { if (t != null) Debug.LogWarning($"[UI] '{t.name}'에 Button이 없어요"); return null; }
            b.onClick.RemoveListener(action);
            b.onClick.AddListener(action);
            return b;
        }

        public static Button Bind(Scene scene, string name, UnityAction action) => Bind(Need(scene, name)?.transform, action);

        // 버튼·패널 안의 첫 글자 (배치한 TMP)
        public static TMP_Text TextIn(Transform t) => t != null ? t.GetComponentInChildren<TMP_Text>(true) : null;
    }
}
