using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // 설정 창의 모양 (2026-11-11 시안: 왼쪽 "소리" · 오른쪽 "게임" 두 칸 + 아래 버튼 줄).
    // 손으로 고치는 원본은 프리팹 Assets/Resources/UI/OptionPanel.prefab (메뉴 MoreMush/UI/설정 창 프리팹 만들기가 이 코드로 만든다).
    // 프리팹이 없을 때만 게임이 이 코드로 직접 만든다. OptionWindow는 아래 이름으로 찾아 잇는다 — 이름만 지키면 위치·크기·그림은 마음대로 바꿔도 된다:
    //   Button_Close · Button_Continue(계속하기) · Button_Main(메인 메뉴) · Button_EndNow(지금 끝내기, 라운드에서만 보임)
    //   Slider_Bgm · Toggle_Bgm · Slider_Sfx · Toggle_Sfx · Toggle_Simple(연출 간소화)
    //   Dropdown_Lang(지금 언어 + ▼) · LangList 안의 Lang_<코드> (Lang_ko · Lang_en …)
    public static class OptionPanelBuilder
    {
        const float BOX_W = 1100, BOX_H = 540;   // 2026-11-11 사용자가 프리팹에서 줄인 크기
        static readonly Color LINE = new Color(0.29f, 0.2f, 0.15f, 0.25f);

        public static GameObject Build(Transform parent)
        {
            // 어두운 막 (전체) — 뒤 클릭을 막는다
            var dim = new GameObject("OptionPanel", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(parent, false);
            TempDialog.Stretch(dim.GetComponent<RectTransform>());
            dim.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.05f, 0.55f);

            var box = Rect("Box", dim.transform, Vector2.zero, new Vector2(BOX_W, BOX_H));
            var boxImg = box.gameObject.AddComponent<Image>();
            TempDialog.Skin(boxImg, "panel_wood", TempDialog.BOX);

            var title = TempDialog.Ribbon(box, "설정", new Vector2(0, BOX_H / 2 - 6), 560);
            TempDialog.Localize(title, "설정");
            // 제목은 ONE Mobile POP (한국어·영어). 테두리 머티리얼은 프리팹을 구울 때 UiPrefabs가 넣는다. 다른 언어에서는 그 언어 제목 폰트
            var titleLt = title.GetComponent<LocText>();
            if (LocFonts.NumberFont != null) { title.font = LocFonts.NumberFont; title.fontSharedMaterial = LocFonts.NumberFont.material; }
            if (titleLt != null) titleLt.keepPlacedFont = true;

            // 닫기 X (오른쪽 위)
            var close = TempDialog.SkinButton(box, "Button_Close", "", null, "btn_close", 30, out var closeText);
            Place(close.transform, new Vector2(BOX_W / 2, BOX_H / 2 - 20), new Vector2(84, 84));
            closeText.gameObject.SetActive(false);

            // 두 칸 머리글과 가운데 세로 선
            Header(box, "Header_Sound", "소리", new Vector2(-275, 150));
            Header(box, "Header_Game", "게임", new Vector2(275, 150));
            var line = Rect("Divider", box, new Vector2(0, 10), new Vector2(4, 230)).gameObject.AddComponent<Image>();
            line.color = LINE; line.raycastTarget = false;
            Rect("HeaderLine_Sound", box, new Vector2(-275, 116), new Vector2(480, 3)).gameObject.AddComponent<Image>().color = LINE;
            Rect("HeaderLine_Game", box, new Vector2(275, 116), new Vector2(480, 3)).gameObject.AddComponent<Image>().color = LINE;

            // 소리: 배경음 · 효과음 (아이콘 · 이름 · 막대 · 켜짐/꺼짐)
            SoundRow(box, "Bgm", "BGM", 55);   // 언어와 상관없이 BGM · SFX (사용자 2026-11-11)
            SoundRow(box, "Sfx", "SFX", -40);

            // 게임: 언어 (펼침 목록) · 연출 간소화 (켜짐/꺼짐)
            var lang = Row(box, "Row_Lang", "언어", "Icons/lang", 275, 55);
            var dd = TempDialog.SkinButton(lang, "Dropdown_Lang", "한국어 ▼", null, "btn_wood", 30, out _);
            Place(dd.transform, new Vector2(125, 0), new Vector2(220, 68));
            var simple = Row(box, "Row_Simple", "연출 간소화", "Icons/star", 275, -40);
            var tg = TempDialog.SkinButton(simple, "Toggle_Simple", "꺼짐", null, "btn_wood", 28, out var tgText);
            Place(tg.transform, new Vector2(160, 0), new Vector2(140, 62));
            TempDialog.Localize(tgText, "꺼짐");

            // 언어 목록 (Dropdown_Lang을 누르면 펼친다). 다른 칸 위에 그려지게 상자의 마지막 자식
            var list = Rect("LangList", box, new Vector2(400, 19), new Vector2(220, 64 * Loc.LANGS.Length + 24));
            list.pivot = new Vector2(0.5f, 1);
            var listImg = list.gameObject.AddComponent<Image>(); TempDialog.Skin(listImg, "panel_paper", TempDialog.CREAM);
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 12, 12); v.spacing = 0; v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            foreach (var code in Loc.LANGS)
            {
                var b = TempDialog.SkinButton(list, "Lang_" + code, Loc.LANG_NAME[code], null, "btn_wood", 28, out _);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
            }
            list.gameObject.SetActive(false);

            // 아래 버튼 줄: 메인 메뉴 · 지금 끝내기 · 계속하기 (지금 끝내기가 없으면 나머지 둘이 붙어서 가운데)
            var row = Rect("Buttons", box, new Vector2(0, -185), new Vector2(1000, 100));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 30; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            BottomButton(row, "Button_Main", "메인 메뉴", "btn_wood");
            BottomButton(row, "Button_EndNow", "지금 끝내기", "btn_danger");
            BottomButton(row, "Button_Continue", "계속하기", "btn_go");

            list.SetAsLastSibling();
            return dim;
        }

        static void BottomButton(Transform row, string name, string label, string skin)
        {
            var b = TempDialog.SkinButton(row, name, label, null, skin, 36, out var t);
            var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 290; le.preferredHeight = 96;
            TempDialog.Localize(t, label);
        }

        static void SoundRow(Transform box, string id, string label, float y)
        {
            var row = Row(box, "Row_" + id, label, "UI/icon_" + id.ToLowerInvariant() + "_on", -275, y, false);
            // 막대
            var s = TempDialog.MakeSlider(row, "Slider_" + id);
            Place(s.transform, new Vector2(55, 0), new Vector2(160, 30));
            // 켜짐/꺼짐
            var tg = TempDialog.SkinButton(row, "Toggle_" + id, "켜짐", null, "btn_go", 28, out var t);
            Place(tg.transform, new Vector2(185, 0), new Vector2(110, 58));
            TempDialog.Localize(t, "켜짐");
        }

        // 한 줄: 아이콘(왼쪽 끝) + 이름. 줄 상자는 칸 가운데(cx) 기준 너비 480, 자식 위치는 줄 가운데 기준
        static RectTransform Row(Transform box, string name, string label, string icon, float cx, float y, bool translate = true)
        {
            var row = Rect(name, box, new Vector2(cx, y), new Vector2(480, 90));
            var ic = Rect("Icon", row, new Vector2(-210, 0), new Vector2(56, 56)).gameObject.AddComponent<Image>();
            ic.preserveAspect = true; ic.raycastTarget = false;
            var sp = icon != null ? SpriteDB.Get(icon) : null;
            if (sp != null) ic.sprite = sp; else ic.enabled = false;   // 아이콘 그림이 없으면 숨긴다 (프리팹에서 그림을 넣으면 보인다)
            var t = TempDialog.MakeText(row, "Name", label, new Vector2(-100, 0), new Vector2(150, 70), 32);
            t.alignment = TextAlignmentOptions.MidlineLeft; t.color = TempDialog.INK;
            t.enableAutoSizing = true; t.fontSizeMin = 20; t.fontSizeMax = 32; t.textWrappingMode = TextWrappingModes.NoWrap;
            TempDialog.Localize(t, label);
            if (!translate)
            {
                // 번역하지 않는 글자 (BGM·SFX): 모든 언어에서 ONE Mobile POP 그대로 (영문이라 어느 언어에서도 글자가 있다)
                Object.DestroyImmediate(t.GetComponent<LocText>());
                if (LocFonts.NumberFont != null) { t.font = LocFonts.NumberFont; t.fontSharedMaterial = LocFonts.NumberFont.material; }
                t.text = label;
            }
            return row;
        }

        static void Header(Transform box, string name, string label, Vector2 pos)
        {
            var t = TempDialog.MakeText(box, name, label, pos, new Vector2(500, 64), 40);
            t.color = TempDialog.INK; t.fontStyle = FontStyles.Bold;
            TempDialog.Localize(t, label);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static void Place(Transform t, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
    }
}
