using System.Text;
using TMPro;
using UnityEngine;

namespace MoreMush
{
    // Combo counter skin (sheet "combo_skin", sprite asset Resources/Sprite Assets/ComboDigits built by
    // MoreMush/Build Combo Digit Sprite Asset): "12 COMBO" spelled in the gold set, or the critical set, and the
    // CRITICAL! word over critical damage numbers. The glyphs keep
    // their painted colours (no tint). Without the asset it returns null and the caller keeps the font.
    public static class ComboDigits
    {
        static bool loaded;
        static TMP_SpriteAsset asset;
        static readonly StringBuilder sb = new StringBuilder();

        public static bool Ready
        {
            get
            {
                if (!loaded) { loaded = true; asset = Resources.Load<TMP_SpriteAsset>("Sprite Assets/ComboDigits"); }
                return asset != null;
            }
        }

        // the CRITICAL! word shown over critical damage numbers (null without the asset or the glyph)
        public static string Critical => Ready && asset.GetSpriteIndexFromName("critical") >= 0 ? "<sprite=\"ComboDigits\" name=\"critical\">" : null;

        static void Glyph(string g) => sb.Append("<sprite=\"ComboDigits\" name=\"").Append(g).Append("\">");

        // digits only ("12"), or with the word after them ("12 COMBO")
        public static string Spell(int n, bool crit, bool word)
        {
            if (!Ready) return null;
            sb.Clear();
            char set = crit ? 'c' : 'n';
            foreach (char d in n.ToString()) Glyph(set.ToString() + d);
            if (word) { sb.Append("<space=0.08em>"); Glyph(crit ? "combo_c" : "combo_n"); }
            return sb.ToString();
        }
    }
}
