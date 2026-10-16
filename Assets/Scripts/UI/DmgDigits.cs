using System.Text;
using TMPro;
using UnityEngine;

namespace MoreMush
{
    // Painted damage-number glyphs (sheet "digits", sprite asset Resources/Sprite Assets/DmgDigits built by
    // MoreMush/Build Damage Digit Sprite Asset). Spell() rewrites a number string as TMP sprite tags tinted by the
    // text colour; characters without a glyph (Qa, Sx...) stay in the font. Without the asset it returns null.
    public static class DmgDigits
    {
        static bool loaded;
        static TMP_SpriteAsset asset;
        static readonly StringBuilder sb = new StringBuilder();

        public static bool Ready
        {
            get
            {
                if (!loaded) { loaded = true; asset = Resources.Load<TMP_SpriteAsset>("Sprite Assets/DmgDigits"); }
                return asset != null;
            }
        }

        // glyph ids: u* = capital, l* = lowercase (the art lives in files, and Windows file names ignore case)
        static string Glyph(char c) => c switch
        {
            >= '0' and <= '9' => "d" + c,
            '+' => "plus", '.' => "dot", ',' => "comma", '!' => "excl",
            >= 'A' and <= 'Z' => "u" + char.ToLowerInvariant(c),
            >= 'a' and <= 'z' => "l" + c,
            _ => null,
        };

        public static string Spell(string s)
        {
            if (s == null || !Ready) return null;
            sb.Clear();
            foreach (char c in s)
            {
                var g = Glyph(c);
                if (g != null && asset.GetSpriteIndexFromName(g) >= 0) sb.Append("<sprite=\"DmgDigits\" name=\"").Append(g).Append("\" tint=1>");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
