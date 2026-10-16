using System;
using TMPro;
using UnityEngine;

namespace MoreMush
{
    // 언어별 폰트 세트. Assets/Resources/Loc/LocFonts.asset 하나를 Editor/LocTools가 만든다.
    // 역할: Title(제목·버튼·숫자, 예전 Jua) · Body(설명 글, 예전 Noto Sans KR).
    // 외곽선 프리셋: 같은 순서(Outline Dark, Wood, Brown)로 폰트마다 머티리얼을 하나씩 둔다.
    [CreateAssetMenu(menuName = "MoreMush/Loc Fonts")]
    public class LocFonts : ScriptableObject
    {
        public enum Role { Title, Body }
        public static readonly string[] PRESETS = { "Outline Dark", "Wood", "Brown" };

        [Serializable]
        public class Face { public TMP_FontAsset font; public Material[] presets = new Material[PRESETS.Length]; }
        [Serializable]
        public class Set { public string lang; public Face title = new Face(), body = new Face(); }

        public Set[] sets = new Set[0];

        [Tooltip("세트 밖이지만 배치한 UI가 쓰는 한·영 전용 폰트 (ONEMobilePOP 등). 한국어·영어에서는 그대로 두고, 그 밖의 언어에서는 그 언어 폰트(koEnTitleFonts면 제목, 아니면 본문)로 바꾼다 (TextLangFont)")]
        public TMP_FontAsset[] koEnFonts = new TMP_FontAsset[0];
        [Tooltip("koEnFonts 가운데 다른 언어에서 제목 폰트(둥글고 굵은 글꼴)로 바꿀 것 (ONEMobilePOP). 나머지는 본문 폰트로")]
        public TMP_FontAsset[] koEnTitleFonts = new TMP_FontAsset[0];
        public static bool KoEnLang(string lang) => lang == "ko" || lang == "en";
        public Role KoEnRole(TMP_FontAsset f) => Array.IndexOf(koEnTitleFonts, f) >= 0 ? Role.Title : Role.Body;
        // 머티리얼 이름 끝(… Outline Dark 등)으로 본 프리셋 순번 (−1 = 프리셋 아님)
        public static int PresetOfName(Material m) => m == null ? -1 : Array.FindIndex(PRESETS, p => m.name.EndsWith(p));

        static LocFonts inst;
        public static LocFonts I => inst != null ? inst : inst = Resources.Load<LocFonts>("Loc/LocFonts");

        public Face FaceFor(string lang, Role role)
        {
            Set s = null;
            foreach (var x in sets) if (x.lang == lang) { s = x; break; }
            if (s == null && sets.Length > 0) s = sets[0];
            return s == null ? null : role == Role.Title ? s.title : s.body;
        }

        // preset −1 = 폰트 기본 머티리얼
        public static void Apply(TMP_Text t, Role role, int preset)
        {
            var face = I?.FaceFor(Loc.Lang, role);
            if (t == null || face?.font == null) return;
            if (t.font != face.font) t.font = face.font;
            var mat = preset >= 0 && preset < face.presets.Length && face.presets[preset] != null ? face.presets[preset] : face.font.material;
            if (t.fontSharedMaterial != mat) t.fontSharedMaterial = mat;
        }
    }
}
