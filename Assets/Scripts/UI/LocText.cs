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

        TMP_Text tmp;
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

        public void Apply()
        {
            if (tmp == null) tmp = GetComponent<TMP_Text>();
            LocFonts.Apply(tmp, role, preset);
            if (string.IsNullOrEmpty(key)) return;
            string cur = tmp.text;
            if (cur != key && cur != authored && cur != shown) return;   // 코드가 바꾼 글자
            shown = Loc.T(key);
            if (cur != shown) tmp.text = shown;
        }
    }
}
