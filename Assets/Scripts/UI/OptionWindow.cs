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
            => Build(canvas, name, title, null, btns);

        // skins = 버튼마다 그림 (UI/btn_*: btn_wood 기본 · btn_go · btn_danger · btn_orange), null이면 모두 btn_wood
        public static GameObject Build(Canvas canvas, string name, string title, string[] skins, params (string label, UnityAction action)[] btns)
        {
            var dim = Dim(canvas, name);

            // 그림은 Art/Dream/UI (세계수 탭 창과 같은 세트): 창 panel_wood · 짧은 제목은 ribbon 위, 긴 문장(확인 질문)은 창 안 글자 + 덩굴
            bool ribbon = title.Length <= 12;
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(dim.transform, false);
            box.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Max(700, 250 * btns.Length + 120), ribbon ? 330 : 380);
            Skin(box.GetComponent<Image>(), "panel_wood", BOX);

            if (ribbon) Ribbon(box.transform, title, new Vector2(0, 165), 560);
            else
            {
                var t = MakeText(box.transform, "Title", title, new Vector2(0, 80), new Vector2(Mathf.Max(580, 250 * btns.Length), 120), 42);
                t.color = INK;
                var vine = new GameObject("Vine", typeof(RectTransform), typeof(Image));
                vine.transform.SetParent(box.transform, false);
                var vr = vine.GetComponent<RectTransform>(); vr.anchoredPosition = new Vector2(0, 8); vr.sizeDelta = new Vector2(284, 60);
                var vi = vine.GetComponent<Image>(); Skin(vi, "divider", Color.clear); vi.type = Image.Type.Simple; vi.preserveAspect = true; vi.raycastTarget = false;
            }

            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(box.transform, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchoredPosition = new Vector2(0, ribbon ? -36 : -95); rr.sizeDelta = new Vector2(Mathf.Max(560, 250 * btns.Length - 20), 104);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 20; h.childForceExpandWidth = h.childForceExpandHeight = true;
            for (int i = 0; i < btns.Length; i++)
                SkinButton(row.transform, "Btn" + i, btns[i].label, btns[i].action, skins != null && i < skins.Length ? skins[i] : "btn_wood", 34, out _);
            return dim;
        }

        // ===== 임시 창 그림 (Art/Dream/UI, SpriteDB) — 그림이 없으면 예전 단색 =====
        internal static readonly Color INK = new Color(0.29f, 0.2f, 0.15f), CREAM = new Color(1f, 0.973f, 0.906f), BOX = new Color(0.98f, 0.93f, 0.8f),
            OFF_TINT = new Color(0.8f, 0.78f, 0.75f, 0.85f), DEEP_GREEN = new Color(0.12f, 0.25f, 0.09f), DEEP_ORANGE = new Color(0.38f, 0.16f, 0.04f);

        internal static void Skin(Image im, string sprite, Color fallback)
        {
            var sp = SpriteDB.Get("UI/" + sprite);
            if (sp == null) { im.color = fallback; return; }
            im.sprite = sp; im.color = Color.white;
            im.type = sp.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
        }

        // 낮은 칸(줄·버튼)에서 9-slice 둥근 끝이 칸 높이를 다 먹어 뾰족해지지 않게, 테두리를 칸 높이의 share 만큼으로 줄인다
        internal static void FitBorder(Image im, float height, float share = 0.7f)
        {
            if (im == null || im.sprite == null || im.type != Image.Type.Sliced) return;
            var b = im.sprite.border;
            im.pixelsPerUnitMultiplier = Mathf.Max(1, (b.y + b.w) / (height * share));
        }

        // 전체를 덮는 어두운 숲색 막 (뒤 클릭 막기)
        internal static GameObject Dim(Canvas canvas, string name)
        {
            var dim = new GameObject(name, typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            Stretch(dim.GetComponent<RectTransform>());
            dim.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.05f, 0.55f);
            return dim;
        }

        // 창 위 끝에 걸친 리본 + 흰 제목 (y = 리본 가운데)
        internal static TextMeshProUGUI Ribbon(Transform box, string title, Vector2 pos, float width)
        {
            var rib = new GameObject("Ribbon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rib.transform.SetParent(box, false);
            rib.GetComponent<LayoutElement>().ignoreLayout = true;
            var rr = rib.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.anchoredPosition = pos; rr.sizeDelta = new Vector2(width, width * 0.3f);
            var ri = rib.GetComponent<Image>(); Skin(ri, "ribbon", new Color(0.97f, 0.66f, 0.45f)); ri.type = Image.Type.Simple; ri.raycastTarget = false;
            // 리본 띠 가운데(아래로 처진 양 끝 빼고)에 글자
            var t = MakeText(rib.transform, "Title", title, new Vector2(0, width * 0.035f), new Vector2(width * 0.62f, width * 0.15f), 40);
            t.color = Color.white; t.enableAutoSizing = true; t.fontSizeMin = 22; t.fontSizeMax = 40;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.fontStyle = FontStyles.Bold;
            return t;
        }

        // 수채 버튼 글자색 (대비 4.5:1 이상): 나무 = 진한 갈색, 초록·주황 = 같은 계열 진한 색 (크림 글자는 1.8:1이라 흐림), 빨강·파랑 = 크림
        internal static Button SkinButton(Transform parent, string name, string label, UnityAction action, string sprite, float fontSize, out TextMeshProUGUI text)
        {
            var b = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            b.transform.SetParent(parent, false);
            var img = b.GetComponent<Image>();
            Skin(img, sprite, new Color(0.55f, 0.36f, 0.2f));
            var btn = b.GetComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors; cb.disabledColor = OFF_TINT; btn.colors = cb;
            if (action != null) btn.onClick.AddListener(action);
            text = MakeText(b.transform, "Label", label, Vector2.zero, Vector2.zero, fontSize, true);
            // 테두리(9-slice 둥근 끝) 안쪽에서 한 줄, 넘치면 줄인다
            text.rectTransform.offsetMin = new Vector2(22, 8); text.rectTransform.offsetMax = new Vector2(-22, -10);
            text.textWrappingMode = TextWrappingModes.NoWrap; text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = fontSize;
            SkinButtonText(text, sprite);
            FitBorder(img, 104 * fontSize / 34);
            return btn;
        }

        internal static void SkinButtonText(TMP_Text t, string sprite)
        {
            if (t == null) return;
            t.color = sprite == "btn_wood" ? INK : sprite == "btn_go" ? DEEP_GREEN : sprite == "btn_orange" ? DEEP_ORANGE : CREAM;
            t.fontStyle = sprite == "btn_wood" ? FontStyles.Normal : FontStyles.Bold;
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
