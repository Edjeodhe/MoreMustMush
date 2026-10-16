using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush
{
    // 옵션 창 (모든 씬, Esc로 열고 닫는다). 씬에 배치된 OptionPanel이 있으면 그것을 쓴다:
    //   안의 Button_Close(또는 Exit) = 닫기, Button_Mute = 소리 켜고 끄기, Button_Quit = 게임 종료, 그 밖의 버튼은 Extra의 이름으로 찾아 잇는다.
    // 배치된 창이 없으면 임시 창(TempDialog)을 만든다 — 옵션 창이 배치되면 자동으로 그쪽을 쓴다.
    public class OptionWindow
    {
        // 씬마다 덧붙이는 버튼: 배치된 창에서는 name으로 찾고, 임시 창에는 label로 만든다
        public struct Extra
        {
            public string name, label; public UnityAction action;
            public Extra(string name, string label, UnityAction action) { this.name = name; this.label = label; this.action = action; }
        }

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        TMP_Text muteLabel;
        bool counted;   // App 모달 수에 넣었나 (Open/Close를 직접 쓰는 씬)

        public static OptionWindow For(Scene scene, UnityAction onClose, params Extra[] extras)
        {
            var w = new OptionWindow();
            var placed = SceneUI.Find(scene, "OptionPanel");
            if (placed != null) w.WirePlaced(placed.gameObject, onClose, extras);
            else w.BuildTemp(scene, onClose, extras);
            if (w.Root != null) w.Root.SetActive(false);
            return w;
        }

        void WirePlaced(GameObject root, UnityAction onClose, Extra[] extras)
        {
            Root = root;
            var close = SceneUI.Find(root.transform, "Button_Close") ?? SceneUI.Find(root.transform, "Exit");
            if (close != null) SceneUI.Bind(close, onClose);
            var quit = SceneUI.Find(root.transform, "Button_Quit");
            if (quit != null) SceneUI.Bind(quit, () => { Snd.Ui(); App.Quit(); });
            var mute = SceneUI.Find(root.transform, "Button_Mute");
            if (mute != null) { SceneUI.Bind(mute, ToggleMute); muteLabel = SceneUI.TextIn(mute); }
            foreach (var e in extras)
            {
                var t = SceneUI.Find(root.transform, e.name);
                if (t != null) SceneUI.Bind(t, e.action);
                else Debug.LogWarning($"[Option] 배치된 OptionPanel 안에 '{e.name}' 버튼이 없어요");
            }
        }

        void BuildTemp(Scene scene, UnityAction onClose, Extra[] extras)
        {
            var canvas = TempDialog.RootCanvas(scene);
            if (canvas == null) { Debug.LogWarning("[Option] 캔버스가 없어 옵션 창을 만들 수 없어요"); return; }
            // 위에서 아래로: 돌아가기 · 소리 설정(효과음·배경음·음소거) · 씬별 버튼 · 메인 메뉴 · 게임 종료
            Root = TempDialog.BuildColumn(canvas, "OptionPanel", Loc.T("설정"), out var col);
            TempDialog.ColumnButton(col, Loc.T("돌아가기"), onClose);
            TempDialog.ColumnLabel(col, Loc.T("소리 설정"));
            sfx = TempDialog.ColumnSlider(col, Loc.T("효과음"), Snd.SfxVol, v => Snd.SetSfx(v));
            bgm = TempDialog.ColumnSlider(col, Loc.T("배경음"), Snd.BgmVol, v => Snd.BgmVol = v);
            muteLabel = TempDialog.ColumnButton(col, MuteText(), ToggleMute);
            foreach (var e in extras) TempDialog.ColumnButton(col, e.label, e.action);
            if (scene.name != Scenes.MainMenu)
                TempDialog.ColumnButton(col, Loc.T("메인 메뉴"), () => { Snd.Ui(); Close(); App.Go(Scenes.MainMenu); });
#if !UNITY_WEBGL
            TempDialog.ColumnButton(col, Loc.T("게임 종료"), () => { Snd.Ui(); App.Quit(); });   // 웹은 브라우저 탭을 닫을 수 없어 뺀다 (HideOnWeb과 같은 이유)
#endif
        }

        Slider sfx, bgm;

        static string MuteText() => Snd.muted ? Loc.T("음소거: 켜짐") : Loc.T("음소거: 꺼짐");

        void ToggleMute() { Snd.muted = !Snd.muted; Snd.Ui(); Refresh(); }

        // 소리 글자·음량을 지금 상태로 (M 키로도 바뀌므로 열 때마다)
        public void Refresh()
        {
            if (muteLabel != null) muteLabel.text = MuteText();
            if (sfx != null) sfx.SetValueWithoutNotify(Snd.SfxVol);
            if (bgm != null) bgm.SetValueWithoutNotify(Snd.BgmVol);
        }

        // 직접 여닫는 씬용 (MainGame은 자기 팝업 관리로 Root만 쓴다)
        public void Open()
        {
            if (Root == null) return;
            Refresh();
            Root.SetActive(true);
            Root.transform.SetAsLastSibling();
            if (!counted) { counted = true; App.ModalPush(); }
        }

        public void Close()
        {
            if (Root != null) Root.SetActive(false);
            if (counted) { counted = false; App.ModalPop(); }
        }

        public void Toggle() { Snd.Ui(); if (IsOpen) Close(); else Open(); }
    }

    // 씬에 창이 배치되기 전까지 쓰는 임시 창 (어두운 배경 + 제목 + 버튼 줄). 배치된 창이 생기면 쓰지 않는다
    public static class TempDialog
    {
        public static Canvas RootCanvas(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<Canvas>(true);
                if (c != null) return c.rootCanvas;
            }
            return null;
        }

        public static GameObject Build(Canvas canvas, string name, string title, params (string label, UnityAction action)[] btns)
        {
            var dim = new GameObject(name, typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            Stretch(dim.GetComponent<RectTransform>());
            dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(dim.transform, false);
            box.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Max(640, 220 * btns.Length + 60), 320);
            box.GetComponent<Image>().color = new Color(0.98f, 0.93f, 0.8f);

            MakeText(box.transform, "Title", title, new Vector2(0, 60), new Vector2(580, 120), 44);

            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(box.transform, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchoredPosition = new Vector2(0, -80); rr.sizeDelta = new Vector2(Mathf.Max(580, 220 * btns.Length), 90);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 24; h.childForceExpandWidth = h.childForceExpandHeight = true;
            for (int i = 0; i < btns.Length; i++)
            {
                var b = new GameObject("Btn" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                b.transform.SetParent(row.transform, false);
                var img = b.GetComponent<Image>();
                img.color = new Color(0.55f, 0.36f, 0.2f);
                var btn = b.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(btns[i].action);
                MakeText(b.transform, "Label", btns[i].label, Vector2.zero, Vector2.zero, 36, true);
            }
            return dim;
        }

        // 세로 창: 어두운 배경 + 제목 + 위에서 아래로 쌓이는 칸 (높이는 내용에 맞춘다). col에 Column* 도우미로 칸을 더한다
        const float COL_W = 600, ROW_H = 76;
        static readonly Color BOX = new Color(0.98f, 0.93f, 0.8f), BTN = new Color(0.55f, 0.36f, 0.2f), INK = new Color(0.23f, 0.14f, 0.08f);

        public static GameObject BuildColumn(Canvas canvas, string name, string title, out Transform col)
        {
            var dim = new GameObject(name, typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            Stretch(dim.GetComponent<RectTransform>());
            dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            box.transform.SetParent(dim.transform, false);
            box.GetComponent<RectTransform>().sizeDelta = new Vector2(COL_W + 80, 0);
            box.GetComponent<Image>().color = BOX;
            var v = box.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(40, 40, 30, 40); v.spacing = 14;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            col = box.transform;

            var t = MakeText(col, "Title", title, Vector2.zero, Vector2.zero, 44);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
            return dim;
        }

        public static TMP_Text ColumnButton(Transform col, string label, UnityAction action)
        {
            var b = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            b.transform.SetParent(col, false);
            b.GetComponent<LayoutElement>().preferredHeight = ROW_H;
            var img = b.GetComponent<Image>(); img.color = BTN;
            var btn = b.GetComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(action);
            var t = MakeText(b.transform, "Label", label, Vector2.zero, Vector2.zero, 34, true);
            t.color = Color.white;
            return t;
        }

        public static void ColumnLabel(Transform col, string text)
        {
            var t = MakeText(col, "Label", text, Vector2.zero, Vector2.zero, 32);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
        }

        // 이름 + 가로 막대 (0~1). 놓을 때마다가 아니라 움직이는 동안 onChange를 부른다
        public static Slider ColumnSlider(Transform col, string label, float value, UnityAction<float> onChange)
        {
            var row = new GameObject("SliderRow", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(col, false);
            row.GetComponent<LayoutElement>().preferredHeight = 60;
            var t = MakeText(row.transform, "Name", label, Vector2.zero, Vector2.zero, 30);
            var tr = t.rectTransform; tr.anchorMin = new Vector2(0, 0); tr.anchorMax = new Vector2(0.3f, 1); tr.offsetMin = tr.offsetMax = Vector2.zero;
            t.alignment = TextAlignmentOptions.MidlineLeft; t.color = INK;

            var s = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            s.transform.SetParent(row.transform, false);
            var sr = s.GetComponent<RectTransform>(); sr.anchorMin = new Vector2(0.32f, 0.3f); sr.anchorMax = new Vector2(1, 0.7f); sr.offsetMin = sr.offsetMax = Vector2.zero;
            var back = Part(s.transform, "Background", new Color(0.78f, 0.68f, 0.52f)); Stretch(back.rectTransform); back.raycastTarget = true;   // 막대 아무 곳이나 눌러도 옮겨진다
            var area = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(s.transform, false); Stretch(area);
            var fill = Part(area, "Fill", BTN); Stretch(fill.rectTransform);
            var hArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            hArea.SetParent(s.transform, false); Stretch(hArea); hArea.offsetMin = new Vector2(16, 0); hArea.offsetMax = new Vector2(-16, 0);
            var handle = Part(hArea, "Handle", new Color(0.36f, 0.22f, 0.12f));
            handle.rectTransform.sizeDelta = new Vector2(36, 0);
            handle.rectTransform.anchorMin = new Vector2(0, -0.6f); handle.rectTransform.anchorMax = new Vector2(0, 1.6f);
            handle.raycastTarget = true;

            var slider = s.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = 0; slider.maxValue = 1; slider.value = value;
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        static Image Part(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.color = c; im.raycastTarget = false;
            return im;
        }

        internal static TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (stretch) Stretch(rt); else { rt.anchoredPosition = pos; rt.sizeDelta = size; }
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = fontSize; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.23f, 0.14f, 0.08f); t.raycastTarget = false;
            LocFonts.Apply(t, LocFonts.Role.Title, -1);
            return t;
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
