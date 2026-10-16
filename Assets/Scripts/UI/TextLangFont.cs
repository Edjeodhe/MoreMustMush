using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MoreMush
{
    // 배치한 글자(LocText가 없는 TMP)도 언어에 맞는 폰트로 바꾼다. TextFit.ApplyScene이 씬마다 등록한다.
    // 대상은 LocFonts 세트에 들어 있는 폰트로 배치한 글자뿐 — 세트 밖 폰트(장식용 등)는 배치한 그대로 둔다.
    //   그 언어 세트의 폰트가 배치한 폰트와 같으면 배치한 폰트·머티리얼 그대로
    //   다르면 그 언어 폰트로 바꾸고, 머티리얼은 LocFonts 프리셋 순번을 이어받는다 (프리셋이 아니면 그 폰트 기본)
    public static class TextLangFont
    {
        struct Entry
        {
            public TMP_Text text;
            public TMP_FontAsset font;     // 배치한 폰트
            public Material mat;           // 배치한 머티리얼
            public LocFonts.Role role;     // 배치한 폰트가 세트에서 맡은 역할 (제목·본문)
            public int preset;             // 배치한 머티리얼의 프리셋 순번 (−1 = 프리셋 아님)
        }

        static readonly List<Entry> entries = new List<Entry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { entries.Clear(); Loc.Changed -= ApplyAll; }

        // 내려간 씬의 글자를 비운다 (씬이 바뀔 때 등록 전에)
        public static void Prune() => entries.RemoveAll(e => e.text == null);

        // 글자 하나를 등록한다. 지금 언어로 바로 맞추려면 apply = true (씬 등록은 끝에 ApplyAll 한 번)
        public static void Register(TMP_Text t, bool apply = true)
        {
            Loc.Changed -= ApplyAll;   // 같은 구독이 쌓이지 않게
            Loc.Changed += ApplyAll;
            if (t == null || t.font == null || t.GetComponent<LocText>() != null) return;   // LocText가 알아서 바꾼다
            if (!TryDescribe(t, out var e)) return;
            entries.Add(e);
            if (apply) Apply(e, LocFonts.I);
        }

        static bool TryDescribe(TMP_Text t, out Entry e)
        {
            e = default;
            var lf = LocFonts.I;
            if (lf == null) return false;
            foreach (var set in lf.sets)
            {
                var role = set.title.font == t.font ? LocFonts.Role.Title : set.body.font == t.font ? LocFonts.Role.Body : (LocFonts.Role?)null;
                if (role == null) continue;
                var face = role == LocFonts.Role.Title ? set.title : set.body;
                e = new Entry { text = t, font = t.font, mat = t.fontSharedMaterial, role = role.Value, preset = System.Array.IndexOf(face.presets, t.fontSharedMaterial) };
                return true;
            }
            return false;   // 세트 밖 폰트
        }

        public static void ApplyAll()
        {
            var lf = LocFonts.I;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].text == null) { entries.RemoveAt(i); continue; }
                Apply(entries[i], lf);
            }
        }

        static void Apply(Entry e, LocFonts lf)
        {
            var face = lf != null ? lf.FaceFor(Loc.Lang, e.role) : null;
            bool keep = face == null || face.font == null || face.font == e.font;
            var font = keep ? e.font : face.font;
            var mat = keep ? e.mat
                : e.preset >= 0 && e.preset < face.presets.Length && face.presets[e.preset] != null ? face.presets[e.preset] : face.font.material;
            if (e.text.font != font) e.text.font = font;
            if (e.text.fontSharedMaterial != mat) e.text.fontSharedMaterial = mat;
        }
    }
}
