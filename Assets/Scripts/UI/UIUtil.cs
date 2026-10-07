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

        // Inline icon tag for TMP text (sprite asset "Icons").
        public static string Ic(string name) => $"<sprite name=\"{name}\">";

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
