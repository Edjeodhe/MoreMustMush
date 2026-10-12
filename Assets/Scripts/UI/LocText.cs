using TMPro;
using UnityEngine;

namespace MoreMush
{
    // 씬·프리팹의 TMP 글자마다 붙는다 (Editor/LocTools가 붙이고 채운다).
    // 언어가 바뀌면 그 언어의 폰트(역할·외곽선 프리셋 유지)로 바꾸고, 씬에 적어 둔 원문(key)이면 번역해 넣는다.
    // 코드가 글자를 바꿔 넣은 뒤에는 건드리지 않는다 (코드가 다시 그릴 때 Loc.T로 번역한다).
    [RequireComponent(typeof(TMP_Text))]
    public class LocText : MonoBehaviour
    {
        public LocFonts.Role role;
        public int preset = -1;                 // LocFonts.PRESETS 순번, −1 = 기본 머티리얼
        [TextArea] public string key;           // 씬에 적힌 한국어 원문 (번역할 것이 없으면 비어 있다)

        TMP_Text tmp;
        string shown;                           // 마지막으로 이 컴포넌트가 넣은 글자

        void Awake() => tmp = GetComponent<TMP_Text>();
        void OnEnable() { Loc.Changed += Apply; Apply(); }
        void OnDisable() => Loc.Changed -= Apply;

        public void Apply()
        {
            if (tmp == null) tmp = GetComponent<TMP_Text>();
            LocFonts.Apply(tmp, role, preset);
            if (string.IsNullOrEmpty(key)) return;
            string cur = tmp.text;
            if (cur != key && cur != shown) return;   // 코드가 바꾼 글자
            shown = Loc.T(key);
            if (cur != shown) tmp.text = shown;
        }
    }
}
