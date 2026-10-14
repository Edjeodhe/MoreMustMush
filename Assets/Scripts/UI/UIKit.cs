using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // 코드로 만드는 목록 UI. 씬에 있는 버튼·글자를 복제해 폰트·버튼 연출·언어별 폰트(LocText)를 그대로 쓴다.
    // 글자는 코드가 넣으므로(LocText key를 비움) 언어가 바뀌면 화면이 다시 그려야 한다 (GameFlow.OnLangChanged).
    public static class UIKit
    {
        // 왼쪽 위 기준으로 (x, y)에 w×h (y는 아래로)
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static TMP_Text Text(Transform parent, TMP_Text tpl, string name)
        {
            var t = Object.Instantiate(tpl, parent);
            t.name = name;
            var lt = t.GetComponent<LocText>(); if (lt != null) { lt.key = ""; lt.role = LocFonts.Role.Body; lt.preset = -1; }
            LocFonts.Apply(t, LocFonts.Role.Body, -1);   // 밝은 종이 위에 쓰는 본문 글자 (외곽선 없음)
            t.color = U.Hex("#4a3420");
            t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow;
            t.enableAutoSizing = false; t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, Button tpl, string name)
        {
            var b = Object.Instantiate(tpl, parent);
            b.name = name;
            foreach (var lt in b.GetComponentsInChildren<LocText>(true)) lt.key = "";
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) { t.enableAutoSizing = true; t.fontSizeMin = 12; t.fontSizeMax = 22; t.enableWordWrapping = false; }
            return b;
        }

        public static void SetButton(Button b, string label, string act, string arg, bool on)
        {
            UIUtil.Show(b, true);
            var ua = b.GetComponent<UIAction>(); ua.act = act; ua.arg = arg;
            b.interactable = on;
            UIUtil.SetText(b.GetComponentInChildren<TMP_Text>(true), label);
        }

        public static Image Image(Transform parent, string name, Sprite s, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = s; img.color = c; img.raycastTarget = false;
            if (s != null && s.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        // 세로 스크롤 목록: viewport(가림) + content. 반환값은 content
        public static RectTransform Scroll(RectTransform area)
        {
            var sr = area.gameObject.AddComponent<ScrollRect>();
            var vp = Rect("Viewport", area);
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = vp.offsetMax = Vector2.zero; vp.pivot = new Vector2(0, 1);
            vp.gameObject.AddComponent<RectMask2D>();
            var hit = vp.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);   // 빈 곳도 끌어서 스크롤
            var content = Rect("Content", vp);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, 0);
            sr.viewport = vp; sr.content = content; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40; sr.inertia = true;
            return content;
        }
    }

    // 목록 한 줄: 배경 · 아이콘 · 글자 · 오른쪽 버튼(최대 4개, 2열). FarmScreen이 줄을 재사용한다
    public class UIRow
    {
        public RectTransform rt; public Image back, icon; public TMP_Text text;
        public readonly List<Button> buttons = new List<Button>();
        public const float PAD = 20, BTN_W = 150, BTN_H = 44, GAP = 6;   // PAD: 종이 테두리 그림 안쪽

        public static UIRow Make(Transform parent, TMP_Text textTpl, Button btnTpl, Sprite back)
        {
            var r = new UIRow();
            r.rt = UIKit.Rect("Row", parent);
            r.back = UIKit.Image(r.rt, "Back", back, Color.white);
            r.back.rectTransform.anchorMin = Vector2.zero; r.back.rectTransform.anchorMax = Vector2.one; r.back.rectTransform.offsetMin = r.back.rectTransform.offsetMax = Vector2.zero;
            r.icon = UIKit.Image(r.rt, "Icon", null, Color.white); r.icon.preserveAspect = true;
            r.text = UIKit.Text(r.rt, textTpl, "Text");
            r.text.alignment = TextAlignmentOptions.TopLeft; r.text.fontSize = 21;
            for (int i = 0; i < 4; i++) { var b = UIKit.Button(r.rt, btnTpl, "B" + i); r.buttons.Add(b); }
            return r;
        }

        // 글자·아이콘·버튼(라벨, 액션, 인자, 켜짐)을 넣고 줄 높이를 돌려준다. y = 목록에서의 위치
        public float Set(float y, float width, string text, Sprite icon, Color back, params (string label, string act, string arg, bool on)[] btns)
        {
            rt.gameObject.SetActive(true);
            int nb = btns?.Length ?? 0, cols = nb > 2 ? 2 : nb > 0 ? 1 : 0, rows = cols == 2 ? (nb + 1) / 2 : nb;
            float bw = cols * BTN_W + (cols > 1 ? GAP : 0);
            float ix = PAD;
            UIUtil.SetImage(this.icon, icon);
            if (icon != null) { UIKit.Place(this.icon.rectTransform, PAD, PAD, 64, 64); ix += 72; }
            float tw = width - ix - PAD - (cols > 0 ? bw + PAD : 0);
            UIUtil.SetText(this.text, text);
            float th = this.text.GetPreferredValues(text, tw, 0).y;
            float h = Mathf.Max(th + PAD * 2 + 8, rows * (BTN_H + GAP) - GAP + PAD * 2, icon != null ? 64 + PAD * 2 : 0);
            UIKit.Place(rt, 0, y, width, h);
            UIKit.Place(this.text.rectTransform, ix, PAD, tw, th + 8);
            this.back.color = back;
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (i >= nb) { UIUtil.Show(b, false); continue; }
                var (label, act, arg, on) = btns[i];
                int c = cols == 2 ? i % 2 : 0, rr = cols == 2 ? i / 2 : i;
                UIKit.Place((RectTransform)b.transform, width - PAD - bw + c * (BTN_W + GAP), PAD + rr * (BTN_H + GAP), BTN_W, BTN_H);
                UIKit.SetButton(b, label, act, arg, on);
            }
            return h;
        }
    }
}
