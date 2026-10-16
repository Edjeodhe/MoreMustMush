using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // Hand-lettered headline images for the few fixed words a font cannot carry (Dream set, sheets lettering_*):
    // UI/Lettering/<lang>/<id> for translated words, UI/Lettering/<id> for language-free ones (FEVER!, COMBO).
    // Apply() shows the image in place of a TMP text and hides the text; a language without the image keeps the font.
    public static class Lettering
    {
        // language-free words the code writes as literals → lettering id
        static readonly Dictionary<string, string> BY_TEXT = new Dictionary<string, string>
        {
            ["FEVER!"] = "fever", ["SUPER FEVER!!"] = "super_fever", ["SUPER COMBO!"] = "super_combo", ["COMBO"] = "combo", ["NEW"] = "new",
        };

        public static string IdOf(string literal) => literal != null && BY_TEXT.TryGetValue(literal, out var id) ? id : null;

        static string cacheLang;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        // per frame safe: looked up once per language
        public static Sprite Find(string id)
        {
            if (id == null) return null;
            if (cacheLang != Loc.Lang) { cacheLang = Loc.Lang; cache.Clear(); }
            if (!cache.TryGetValue(id, out var sp))
                cache[id] = sp = SpriteDB.Get("UI/Lettering/" + Loc.Lang + "/" + id) ?? SpriteDB.Get("UI/Lettering/" + id);
            return sp;
        }

        // Puts the lettering `id` over text `t` (a child image, so the text's scale pops and fades carry over) and
        // hides the text. height = the image height in text font sizes. Returns the image, or null when the font stays.
        public static Image Apply(TMP_Text t, string id, float height = 1.3f)
        {
            if (t == null) return null;
            var sp = Find(id);
            var tr = t.transform.Find("Lettering");
            var img = tr != null ? tr.GetComponent<Image>() : null;
            if (sp == null)
            {
                t.enabled = true;
                if (img != null) img.enabled = false;
                return null;
            }
            if (img == null)
            {
                var go = new GameObject("Lettering", typeof(RectTransform), typeof(Image));
                go.layer = t.gameObject.layer;
                go.transform.SetParent(t.transform, false);
                img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
            }
            t.enabled = false;
            img.enabled = true;
            if (img.sprite != sp) img.sprite = sp;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            float h = t.fontSize * height;
            rt.sizeDelta = new Vector2(h * sp.rect.width / sp.rect.height, h);
            return img;
        }
    }
}
