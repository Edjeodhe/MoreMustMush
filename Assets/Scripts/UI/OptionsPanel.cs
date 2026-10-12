using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // 옵션: 언어 · 소리 · (라운드 중) 끝내고 정산 · (라운드 밖) 타이틀로.
    // 언어 버튼은 Loc.LANGS 순서대로 langFirst를 복제해 만든다 — 언어를 더하면 버튼도 따라 늘어난다.
    public class OptionsPanel : MonoBehaviour
    {
        public UIAction langFirst;             // LangRow 안의 첫 버튼 (ko). 나머지 언어는 이것을 복제한다
        public TMP_Text muteText;
        public GameObject endNow, toTitle;

        static readonly Color ON = U.Hex("#2a9a74"), OFF = Color.white, TXT_ON = Color.white, TXT_OFF = U.Hex("#3b2414");
        readonly List<UIAction> langs = new List<UIAction>();

        void BuildLangs()
        {
            if (langs.Count > 0 || langFirst == null) return;
            var row = langFirst.transform.parent;
            for (int i = 0; i < Loc.LANGS.Length; i++)
            {
                var b = i == 0 ? langFirst : Instantiate(langFirst, row);
                b.name = "Lang_" + Loc.LANGS[i];
                b.act = "lang"; b.arg = Loc.LANGS[i];
                // 언어 이름은 그 언어 글자로 쓴다 (번역하지 않는다)
                var t = b.GetComponentInChildren<TMP_Text>(true);
                var lt = t != null ? t.GetComponent<LocText>() : null;
                if (lt != null) lt.key = "";
                if (t != null) t.text = Loc.LANG_NAME.TryGetValue(Loc.LANGS[i], out var n) ? n : Loc.LANGS[i];
                langs.Add(b);
            }
        }

        public void Render()
        {
            BuildLangs();
            foreach (var b in langs)
            {
                bool on = b.arg == Loc.Lang;
                var img = b.GetComponent<Image>();
                if (img != null) img.color = on ? ON : OFF;
                var t = b.GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.color = on ? TXT_ON : TXT_OFF;
            }
            UIUtil.SetText(muteText, Snd.muted ? Loc.T("소리: 꺼짐") : Loc.T("소리: 켜짐"));
            string scr = GameFlow.I.Screen;
            UIUtil.Show(endNow, scr == "round");
            UIUtil.Show(toTitle, scr != "round" && scr != "title");
        }
    }
}
