using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    public static class UIUtil
    {
        // Prototype stage pixels (top-left origin, y down) → anchoredPosition for a top-left anchored rect.
        public static void Place(RectTransform rt, float x, float y) => rt.anchoredPosition = new Vector2(x, -y);

        public static void SetText(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

        public static void Show(Component c, bool on) { if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on); }
        public static void Show(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }

        public static Color Col(string hex) => U.Hex(hex);

        // Inline icon tag for TMP text (sprite asset "Icons"). 같은 태그를 매번 새로 만들지 않게 기억해 둔다
        static readonly System.Collections.Generic.Dictionary<string, string> icTags = new System.Collections.Generic.Dictionary<string, string>();
        public static string Ic(string name)
        {
            if (name == null) return "<sprite name=\"\">";
            if (!icTags.TryGetValue(name, out var s)) icTags[name] = s = $"<sprite name=\"{name}\">";
            return s;
        }

        // 매 프레임 움직이는 UI 층(필드 글자, 피버 불꽃)에 하위 Canvas를 붙인다. 바뀐 층만 다시 배칭하고 나머지 UI는 그대로 둔다.
        // 클릭은 받지 않는 층이라 GraphicRaycaster는 붙이지 않는다. (씬에 Canvas를 직접 달아 두면 이 함수는 아무것도 안 한다)
        public static void SubCanvas(Component layer)
        {
            if (layer == null || layer.GetComponent<Canvas>() != null) return;
            var parent = layer.GetComponentInParent<Canvas>();
            var cv = layer.gameObject.AddComponent<Canvas>();
            if (parent != null) cv.additionalShaderChannels = parent.additionalShaderChannels;
            cv.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;   // TMP SDF
        }

        // Shared UI materials for codex states (assets on the SpriteDB): 0 normal, 1 locked silhouette,
        // 2 unlocked-not-harvested, 3 golden
        public static Material Fx(int state) => SpriteDB.I.uiFx[state];

        public static void SetImage(Image img, Sprite s, int fxState = 0)
        {
            if (img == null) return;
            img.sprite = s;
            img.material = fxState == 0 ? null : Fx(fxState);
            img.enabled = s != null;
        }
    }
}
