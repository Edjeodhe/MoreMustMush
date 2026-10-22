using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // 세계수 창들의 목록 팝업 하나 (꼬마 호감도·합류·부탁·진화, 세계수의 가호). 프리팹 Resources/UI/FarmListPopup (Unity MCP로 만든 것):
    //   Box / Title(Icon · Text) · Exit · Scroll / Content / Section(Text) · Row · Empty
    //   Row: Pic/Img · Arrow · Pic2/Img · Body(Name · Line1 · Line2 · Hearts(Heart0~4) · Bar(Fill · Label)) · Button_A · Button_B
    // 여는 쪽이 Fill(목록)에서 Section·Row를 채운다. 닫으면 onClose (여는 쪽 창을 다시 그린다)
    public class FarmListPopup
    {
        public readonly GameObject root;
        readonly TMP_Text title; readonly Image icon;
        readonly Transform section, row; readonly TMP_Text empty;
        readonly ScrollRect scroll;
        readonly List<Transform> sections = new List<Transform>(), rows = new List<Transform>();
        System.Action<FarmListPopup> fill;
        System.Action onClose;
        int nSec, nRow;

        public bool IsOpen => root != null && root.activeSelf;

        public FarmListPopup(GameObject root)
        {
            this.root = root;
            if (root == null) return;
            title = FarmWin.Text(SceneUI.Find(root.transform, "Title"), "Text");
            icon = FarmWin.Img(SceneUI.Find(root.transform, "Title"), "Icon");
            SceneUI.Bind(SceneUI.Find(root.transform, "Exit"), () => { Snd.Ui(); Close(); });
            section = SceneUI.Need(root.transform, "Section");
            row = SceneUI.Need(root.transform, "Row");
            empty = FarmWin.Text(root.transform, "Empty");
            scroll = root.GetComponentInChildren<ScrollRect>(true);
        }

        public void Open(string titleText, string iconName, System.Action<FarmListPopup> fill, System.Action onClose = null)
        {
            if (root == null) return;
            this.fill = fill; this.onClose = onClose;
            UIUtil.SetText(title, titleText);
            if (icon != null) { var s = FarmWin.Ui(iconName); icon.sprite = s; icon.enabled = s != null; }
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Refresh();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        public void Close() { if (!IsOpen) return; root.SetActive(false); onClose?.Invoke(); }

        public void Refresh()
        {
            if (!IsOpen || G_null()) return;
            nSec = nRow = 0; order = 0;
            fill?.Invoke(this);
            FarmWin.Grow(sections, section, nSec);
            FarmWin.Grow(rows, row, nRow);
            UIUtil.Show(empty, nRow == 0);
        }
        static bool G_null() => Game.G?.farm == null;

        public void Empty(string text) => UIUtil.SetText(empty, text);

        // 줄 순서: Section과 Row는 Content 안에서 넣은 순서대로 놓인다
        int order;
        public void Section(string text)
        {
            if (section == null) return;
            if (sections.Count == 0) sections.Add(section);
            while (sections.Count <= nSec) sections.Add(FarmWin.Clone(section, sections.Count));
            var t = sections[nSec++];
            t.gameObject.SetActive(true);
            UIUtil.SetText(FarmWin.Text(t, "Text"), text);
            t.SetSiblingIndex(order++);
        }

        public RowView Row()
        {
            if (row == null) return null;
            if (rows.Count == 0) rows.Add(row);
            while (rows.Count <= nRow) rows.Add(FarmWin.Clone(row, rows.Count));
            var t = rows[nRow++];
            t.gameObject.SetActive(true);
            t.SetSiblingIndex(order++);
            return new RowView(t);
        }

        public void Begin() => order = 0;

        // 줄 하나를 채우는 손잡이. 쓰지 않은 부분은 숨긴다 (Pic2·Arrow·Hearts·Bar·Line2·버튼)
        public class RowView
        {
            readonly Transform t;
            public RowView(Transform t)
            {
                this.t = t;
                foreach (var n in new[] { "Pic2", "Arrow", "Hearts", "Bar", "Line2", "Button_A", "Button_B" }) UIUtil.Show(SceneUI.Find(t, n)?.gameObject, false);
                var body = SceneUI.Find(t, "Body") as RectTransform;
                if (body != null) body.anchoredPosition = new Vector2(180, body.anchoredPosition.y);
                Pic(null);
            }
            public RowView Pic(Sprite s, bool dark = false)
            {
                var im = FarmWin.Img(SceneUI.Find(t, "Pic"), "Img");
                if (im != null) { im.sprite = s; im.enabled = s != null; im.preserveAspect = true; im.color = dark ? new Color(0.2f, 0.18f, 0.16f, 0.6f) : Color.white; }
                return this;
            }
            public RowView Pic2(Sprite s)
            {
                var p2 = SceneUI.Find(t, "Pic2");
                UIUtil.Show(p2?.gameObject, true); UIUtil.Show(SceneUI.Find(t, "Arrow")?.gameObject, true);
                var im = FarmWin.Img(p2, "Img");
                if (im != null) { im.sprite = s; im.enabled = s != null; im.preserveAspect = true; }
                var body = SceneUI.Find(t, "Body") as RectTransform;
                if (body != null) body.anchoredPosition = new Vector2(370, body.anchoredPosition.y);
                return this;
            }
            public RowView Text(string name, string line1, string line2 = null)
            {
                UIUtil.SetText(FarmWin.Text(t, "Name"), name);
                UIUtil.SetText(FarmWin.Text(t, "Line1"), line1 ?? "");
                var l2 = SceneUI.Find(t, "Line2");
                UIUtil.Show(l2?.gameObject, line2 != null);
                if (line2 != null) UIUtil.SetText(SceneUI.TextIn(l2), line2);
                return this;
            }
            public RowView Hearts(int lv) { var h = SceneUI.Find(t, "Hearts"); UIUtil.Show(h?.gameObject, true); FarmWin.Hearts(h, lv); return this; }
            public RowView Bar(float v, string label) { var b = SceneUI.Find(t, "Bar"); UIUtil.Show(b?.gameObject, true); FarmWin.Bar(b, v, label); return this; }
            public RowView A(string label, bool on, UnityEngine.Events.UnityAction act, string sprite = "btn_leaf") => Btn("Button_A", label, on, act, sprite);
            public RowView B(string label, bool on, UnityEngine.Events.UnityAction act, string sprite = "btn_cream") => Btn("Button_B", label, on, act, sprite);
            RowView Btn(string name, string label, bool on, UnityEngine.Events.UnityAction act, string sprite)
            {
                var b = SceneUI.Find(t, name)?.GetComponent<Button>();
                if (b == null) return this;
                b.gameObject.SetActive(true);
                b.onClick.RemoveAllListeners();
                if (act != null) b.onClick.AddListener(act);
                FarmWin.Btn(b, label, on && act != null, sprite);
                return this;
            }
        }
    }
}
