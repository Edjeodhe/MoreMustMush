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
        // 숫자는 그림 글자를 버리고 폰트(LocFonts.NumberFont = ONE Mobile POP)로 쓴다. 색은 글자 그림(Art/effect/text/combo·super_combo)의 색 (사용자 2026-11-11)
        public const string ComboHex = "#f4d4a3", SuperHex = "#f5aa85";
        static readonly Color32 ComboEdge = new Color32(0xdb, 0x9a, 0x4e, 255), SuperEdge = new Color32(0xbe, 0x4d, 0x26, 255);   // 글자 그림의 외곽선 색
        public static readonly Color32 LabelEdge = new Color32(0x6a, 0x37, 0x1c, 255);   // 필드 라벨 글자 테두리 (갈색 #6a371c, 사용자 2026-11-11)
        public static readonly Color32 TagEdge = new Color32(0xff, 0xf7, 0xe8, 255);     // NEW! 표 테두리 (#db4e50 글자에 밝은 크림)
        static readonly System.Collections.Generic.Dictionary<(uint, int), Material> outlineMat = new System.Collections.Generic.Dictionary<(uint, int), Material>();

        // 숫자 글꼴 + 글자 그림과 같은 색 외곽선 (super: SuperCombo 그림의 외곽선 색)
        public static void Style(TMP_Text t, bool super) => Outline(t, super ? SuperEdge : ComboEdge, 0.3f);

        // ONE Mobile POP + 테두리. 모바일 SDF 셰이더는 OUTLINE_ON을 켜야 테두리가 그려진다.
        // 재질은 (색, 두께)마다 하나를 만들어 같이 쓴다 (글자마다 재질을 만들지 않게)
        public static void Outline(TMP_Text t, Color32 edge, float width)
        {
            var f = LocFonts.NumberFont;
            if (t == null || f == null) return;
            if (t.font != f) t.font = f;
            var key = (((uint)edge.r << 24) | ((uint)edge.g << 16) | ((uint)edge.b << 8) | edge.a, Mathf.RoundToInt(width * 1000));
            if (!outlineMat.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(f.material) { name = f.name + " Outline " + key.Item1.ToString("x8"), hideFlags = HideFlags.HideAndDontSave };
                m.EnableKeyword("OUTLINE_ON");
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
                m.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.5f);   // 외곽선이 글자 속을 파먹지 않게 바깥으로 민다
                m.SetColor(ShaderUtilities.ID_OutlineColor, edge);
                outlineMat[key] = m;
            }
            if (t.fontSharedMaterial != m) t.fontSharedMaterial = m;
        }

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
            sb.Append("<color=").Append(crit ? SuperHex : ComboHex).Append('>').Append(n).Append("</color>");
            if (word) { sb.Append("<space=0.08em>"); Glyph(crit ? "combo_c" : "combo_n"); }
            return sb.ToString();
        }
    }
}
