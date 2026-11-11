using TMPro;
using UnityEngine;

namespace MoreMush
{
    // 씬·프리팹의 TMP 글자마다 붙는다 (Editor/LocTools가 붙이고 채운다).
    // 언어가 바뀌면 그 언어의 폰트(역할·외곽선 프리셋 유지)로 바꾸고, 씬에 적어 둔 글자(key나 authored)이면 번역해 넣는다.
    // 코드가 글자를 바꿔 넣은 뒤에는 건드리지 않는다 (코드가 다시 그릴 때 Loc.T로 번역한다).
    [RequireComponent(typeof(TMP_Text))]
    public class LocText : MonoBehaviour
    {
        public LocFonts.Role role;
        public int preset = -1;                 // LocFonts.PRESETS 순번, −1 = 기본 머티리얼
        [TextArea] public string key;           // 한국어 원문 = 번역표의 키 (번역할 것이 없으면 비어 있다)
        [TextArea] public string authored;      // 씬·프리팹에 실제로 적어 둔 글자 (영어; 비어 있으면 key를 그대로 적어 둔 것)
        [Tooltip("켜면 한국어·영어에서는 프리팹·씬에 넣어 둔 폰트와 머티리얼(테두리 등)을 그대로 쓴다. 다른 언어에서는 그 언어 폰트로 바꾸되, 넣어 둔 머티리얼의 테두리(색·두께)를 그 폰트에도 입힌다 (ONEMobilePOP처럼 한·영 글자만 있는 폰트용)")]
        public bool keepPlacedFont;

        TMP_Text tmp;
        TMP_FontAsset placedFont; Material placedMat;   // keepPlacedFont: 처음 넣어 둔 폰트·머티리얼
        string shown;                           // 마지막으로 이 컴포넌트가 넣은 글자

        // 줄바꿈 없는 글자(버튼 등)는 번역이 길어져도 칸을 넘지 않게 원래 크기를 위로 두고 절반까지 줄어든다
        const float MinShrink = 0.5f;

        void Awake()
        {
            tmp = GetComponent<TMP_Text>();
            if (tmp.enableAutoSizing || tmp.textWrappingMode != TextWrappingModes.NoWrap) return;
            tmp.fontSizeMax = tmp.fontSize;
            tmp.fontSizeMin = tmp.fontSize * MinShrink;
            tmp.enableAutoSizing = true;
        }
        void OnEnable() { Loc.Changed += Apply; Apply(); }
        void OnDisable() => Loc.Changed -= Apply;

        // (폰트, 원본 테두리 머티리얼)마다 테두리를 옮겨 입힌 머티리얼 하나를 만들어 같이 쓴다 (글자마다 만들지 않게)
        static readonly System.Collections.Generic.Dictionary<(TMP_FontAsset, Material), Material> outlined = new System.Collections.Generic.Dictionary<(TMP_FontAsset, Material), Material>();
        static Material OutlinedFor(TMP_FontAsset font, Material src)
        {
            if (font == null) return src;
            if (outlined.TryGetValue((font, src), out var m) && m != null) return m;
            m = new Material(font.material) { name = font.name + " + " + src.name, hideFlags = HideFlags.DontSave };
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetColor(ShaderUtilities.ID_OutlineColor, src.GetColor(ShaderUtilities.ID_OutlineColor));
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, src.GetFloat(ShaderUtilities.ID_OutlineWidth));
            m.SetFloat(ShaderUtilities.ID_FaceDilate, src.GetFloat(ShaderUtilities.ID_FaceDilate));
            if (src.HasProperty(ShaderUtilities.ID_OutlineSoftness) && m.HasProperty(ShaderUtilities.ID_OutlineSoftness))
                m.SetFloat(ShaderUtilities.ID_OutlineSoftness, src.GetFloat(ShaderUtilities.ID_OutlineSoftness));
            outlined[(font, src)] = m;
            return m;
        }

        public void Apply()
        {
            if (tmp == null) tmp = GetComponent<TMP_Text>();
            if (keepPlacedFont && placedFont == null) { placedFont = tmp.font; placedMat = tmp.fontSharedMaterial; }
            if (keepPlacedFont && placedFont != null && LocFonts.KoEnLang(Loc.Lang))
            {
                if (tmp.font != placedFont) tmp.font = placedFont;
                if (placedMat != null && tmp.fontSharedMaterial != placedMat) tmp.fontSharedMaterial = placedMat;
            }
            else
            {
                LocFonts.Apply(tmp, role, preset);
                // 다른 언어 폰트로 바뀌어도 넣어 둔 머티리얼의 테두리(색·두께)를 그대로 입힌다 (사용자 2026-11-11)
                if (keepPlacedFont && placedMat != null && placedMat.IsKeywordEnabled(ShaderUtilities.Keyword_Outline))
                    tmp.fontSharedMaterial = OutlinedFor(tmp.font, placedMat);
            }
            if (string.IsNullOrEmpty(key)) return;
            string cur = tmp.text;
            if (cur != key && cur != authored && cur != shown) return;   // 코드가 바꾼 글자
            shown = Loc.T(key);
            if (cur != shown) tmp.text = shown;
        }
    }
}
