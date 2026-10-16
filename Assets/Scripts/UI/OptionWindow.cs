using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush
{
    // 옵션 창 (모든 씬, Esc로 열고 닫는다). 씬에 배치된 OptionPanel이 있으면 그것을 쓴다:
    //   안의 Button_Close(또는 Exit) = 닫기, Button_Mute = 소리 켜고 끄기, 그 밖의 버튼은 Extra의 이름으로 찾아 잇는다.
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
            var btns = new (string, UnityAction)[extras.Length + 2];
            btns[0] = (MuteText(), ToggleMute);
            for (int i = 0; i < extras.Length; i++) btns[i + 1] = (extras[i].label, extras[i].action);
            btns[btns.Length - 1] = (Loc.T("닫기"), onClose);
            Root = TempDialog.Build(canvas, "OptionPanel", Loc.T("설정"), btns);
            muteLabel = Root.transform.Find("Box/Row/Btn0")?.GetComponentInChildren<TMP_Text>(true);
        }

        static string MuteText() => Snd.muted ? Loc.T("소리: 꺼짐") : Loc.T("소리: 켜짐");

        void ToggleMute() { Snd.muted = !Snd.muted; Snd.Ui(); Refresh(); }

        // 소리 글자를 지금 상태로 (M 키로도 바뀌므로 열 때마다)
        public void Refresh() { if (muteLabel != null) muteLabel.text = MuteText(); }

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
