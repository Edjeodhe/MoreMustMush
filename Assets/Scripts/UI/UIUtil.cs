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

        // 정령 얼굴 그림 = 세계수에서 보이는 지금 진화 모습 (Characters/Evo/evo_<id>_<각성 ★>, CritterRig.Evo와 같은 그림).
        // 진화 그림이 없을 때만 예전 통짜 정령(Characters/Kids) → 옛 꼬마 참고 그림 순서로 대신 쓴다
        public static Sprite KidPortrait(string id)
            => SpriteDB.Get("Characters/Evo/evo_" + id + "_" + Mathf.Clamp(Game.Awaken(id), 0, 3))
               ?? SpriteDB.Get("Characters/Evo/evo_" + id + "_0")
               ?? SpriteDB.Get("Characters/Kids/" + id) ?? SpriteDB.Get("Characters/Critters/" + id + "_ref");

        // Inline icon tag for TMP text (sprite asset "Icons"). 같은 태그를 매번 새로 만들지 않게 기억해 둔다
        static readonly System.Collections.Generic.Dictionary<string, string> icTags = new System.Collections.Generic.Dictionary<string, string>();
        public static string Ic(string name)
        {
            if (name == null) return "<sprite name=\"\">";
            if (!icTags.TryGetValue(name, out var s)) icTags[name] = s = $"<sprite name=\"{name}\">";
            return s;
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
