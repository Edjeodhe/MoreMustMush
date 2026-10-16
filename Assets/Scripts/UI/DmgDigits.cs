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
            '+' => "plus", '.' => "dot", ',' => "comma", '!' => "excl", '×' => "lx",
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

        static readonly StringBuilder nb = new StringBuilder();
        static readonly string[] Units2 = { "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

        // Mixed text ("+12 버섯", "점수 ×2", "보스 45%"): only the numbers turn into glyphs, words stay in the font.
        // A number = optional + / × / x right before a digit, digits with . or , between them, then a unit (K… Dc, G)
        // when no letter follows it. Rich-text tags (<sprite …>) are copied as they are. Null without the asset.
        public static string Numbers(string s)
        {
            if (s == null || !Ready) return null;
            nb.Clear();
            int i = 0, n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (c == '<') { int e = s.IndexOf('>', i); if (e < 0) e = n - 1; nb.Append(s, i, e - i + 1); i = e + 1; continue; }
                bool prefix = (c == '+' || c == '×' || c == 'x') && i + 1 < n && char.IsDigit(s[i + 1]) && (c != 'x' || i == 0 || !char.IsLetter(s[i - 1]));
                if (!char.IsDigit(c) && !prefix) { nb.Append(c); i++; continue; }
                int j = prefix ? i + 1 : i;
                while (j < n && (char.IsDigit(s[j]) || ((s[j] == '.' || s[j] == ',') && j + 1 < n && char.IsDigit(s[j + 1])))) j++;
                int u = 0;
                if (j + 1 < n) foreach (var w in Units2) if (string.CompareOrdinal(s, j, w, 0, 2) == 0) { u = 2; break; }
                if (u == 0 && j < n && "KMBTG".IndexOf(s[j]) >= 0) u = 1;
                if (u > 0 && j + u < n && char.IsLetter(s[j + u])) u = 0;
                nb.Append(Spell(s.Substring(i, j + u - i)));
                i = j + u;
            }
            return nb.ToString();
        }
    }
}
