using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush
{
    // 설정 창 (모든 씬, Esc로 열고 닫는다). 2026-11-11 시안: 왼쪽 "소리"(배경음·효과음) · 오른쪽 "게임"(언어·연출 간소화) + 아래 버튼 줄.
    // 창 찾는 순서: ① 씬에 배치된 OptionPanel ② 프리팹 Resources/UI/OptionPanel (손으로 고치는 원본) ③ 없으면 OptionPanelBuilder가 코드로 만든다.
    // 이름으로 찾아 잇는다 (OptionPanelBuilder 맨 위 목록). 아래 버튼: 메인 메뉴 · 지금 끝내기(라운드만) · 계속하기 —
    // 지금 끝내기가 없는 씬에서는 나머지 둘이 붙어서 가운데 (버튼 줄의 HorizontalLayoutGroup).
    public class OptionWindow
    {
        // 씬마다 덧붙이는 버튼: 창 안에서 name으로 찾는다 (지금 끝내기 = Button_EndNow)
        public struct Extra
        {
            public string name, label; public UnityAction action;
            public Extra(string name, string label, UnityAction action) { this.name = name; this.label = label; this.action = action; }
        }

        public const string PrefabPath = "UI/OptionPanel";   // Resources 안 경로

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        Slider sfx, bgm;
        Button tSfx, tBgm, tSimple, langBtn;
        Image iconBgm, iconSfx;   // 켜짐 = 색, 꺼짐 = 회색 (UI/icon_bgm_on·off, icon_sfx_on·off)
        GameObject langList;
        readonly Button[] langItems = new Button[Loc.LANGS.Length];
        TMP_Text muteLabel;   // 옛 배치 창(Button_Mute)용
        bool counted;   // App 모달 수에 넣었나 (Open/Close를 직접 쓰는 씬)

        public static OptionWindow For(Scene scene, UnityAction onClose, params Extra[] extras)
        {
            var w = new OptionWindow();
            var placed = SceneUI.Find(scene, "OptionPanel");
            GameObject root = placed != null ? placed.gameObject : null;
            if (root == null)
            {
                var canvas = TempDialog.RootCanvas(scene);
                if (canvas == null) { Debug.LogWarning("[Option] 캔버스가 없어 설정 창을 만들 수 없어요"); return w; }
                var prefab = Resources.Load<GameObject>(PrefabPath);
                root = prefab != null ? Object.Instantiate(prefab, canvas.transform, false) : OptionPanelBuilder.Build(canvas.transform);
                root.name = "OptionPanel";
            }
            w.Wire(root, scene, onClose, extras);
            w.Root.SetActive(false);
            return w;
        }

        void Wire(GameObject root, Scene scene, UnityAction onClose, Extra[] extras)
        {
            Root = root;
            var t = root.transform;
            var close = SceneUI.Find(t, "Button_Close") ?? SceneUI.Find(t, "Exit");
            if (close != null) SceneUI.Bind(close, onClose);
            var cont = SceneUI.Find(t, "Button_Continue");
            if (cont != null) SceneUI.Bind(cont, onClose);

            var main = SceneUI.Find(t, "Button_Main");
            if (main != null)
            {
                if (scene.name == Scenes.MainMenu) main.gameObject.SetActive(false);   // 메인 메뉴에서는 갈 곳이 없다
                else SceneUI.Bind(main, () => { Snd.Ui(); Close(); App.Go(Scenes.MainMenu); });
            }
            // 지금 끝내기 등 씬별 버튼: 넘겨받은 것만 보이고, 넘겨받지 않은 Button_EndNow는 숨긴다 (메인 메뉴·계속하기가 가운데로 붙는다)
            var endNow = SceneUI.Find(t, "Button_EndNow");
            if (endNow != null) endNow.gameObject.SetActive(false);
            foreach (var e in extras)
            {
                var b = SceneUI.Find(t, e.name);
                if (b == null) continue;   // 이 창에 없는 버튼 (예: 세계수의 Button_Back)은 넘어간다
                b.gameObject.SetActive(true);
                SceneUI.Bind(b, e.action);
            }

            bgm = SliderIn(t, "Slider_Bgm", Snd.BgmVol, v => Snd.BgmVol = v);
            sfx = SliderIn(t, "Slider_Sfx", Snd.SfxVol, v => Snd.SetSfx(v));
            tBgm = BindBtn(t, "Toggle_Bgm", () => { Snd.BgmOn = !Snd.BgmOn; Snd.Ui(); Refresh(); });
            tSfx = BindBtn(t, "Toggle_Sfx", () => { Snd.SfxOn = !Snd.SfxOn; Snd.Ui(); Refresh(); });
            iconBgm = SceneUI.Find(t, "Row_Bgm")?.Find("Icon")?.GetComponent<Image>();
            iconSfx = SceneUI.Find(t, "Row_Sfx")?.Find("Icon")?.GetComponent<Image>();
            tSimple = BindBtn(t, "Toggle_Simple", () => { FxPrefs.Simple = !FxPrefs.Simple; Snd.Ui(); Refresh(); });

            // 언어: 지금 언어 버튼을 누르면 목록을 펼치고, 고르면 바꾸고 접는다
            langList = SceneUI.Find(t, "LangList")?.gameObject;
            langBtn = BindBtn(t, "Dropdown_Lang", () => { Snd.Ui(); if (langList != null) { langList.SetActive(!langList.activeSelf); langList.transform.SetAsLastSibling(); } });
            for (int i = 0; i < Loc.LANGS.Length; i++)
            {
                string code = Loc.LANGS[i];
                langItems[i] = BindBtn(t, "Lang_" + code, () => { Snd.Ui(); Loc.SetLang(code); if (langList != null) langList.SetActive(false); Refresh(); });
            }
            if (langList != null) langList.SetActive(false);
            // 다른 곳에서 언어가 바뀌어도 열려 있는 창의 언어 이름·켜짐/꺼짐 글자를 맞춘다 (씬이 내려가 Root가 없어지면 아무것도 안 한다)
            Loc.Changed += () => { if (Root != null && Root.activeSelf) Refresh(); };

            // 옛 배치 창 호환
            var quit = SceneUI.Find(t, "Button_Quit");
            if (quit != null) SceneUI.Bind(quit, () => { Snd.Ui(); App.Quit(); });
            var mute = SceneUI.Find(t, "Button_Mute");
            if (mute != null) { SceneUI.Bind(mute, () => { Snd.muted = !Snd.muted; Snd.Ui(); Refresh(); }); muteLabel = SceneUI.TextIn(mute); }
        }

        static Button BindBtn(Transform root, string name, UnityAction action)
        {
            var t = SceneUI.Find(root, name);
            return t != null ? SceneUI.Bind(t, action) : null;
        }

        static Slider SliderIn(Transform root, string name, float value, UnityAction<float> onChange)
        {
            var t = SceneUI.Find(root, name);
            if (t == null || !t.TryGetComponent<Slider>(out var s)) return null;
            s.onValueChanged.RemoveAllListeners();
            s.minValue = 0; s.maxValue = 1; s.SetValueWithoutNotify(value);
            s.onValueChanged.AddListener(onChange);
            return s;
        }

        static void SetKey(TMP_Text label, string key)
        {
            if (label == null) return;
            if (label.TryGetComponent<LocText>(out var lt)) { lt.key = key; lt.Apply(); }
            else label.text = Loc.T(key);
        }

        // 소리 아이콘: 켜짐 = 색 그림(…on), 꺼짐·음소거 = 회색 그림(…off)
        static void SetIcon(Image im, string key, bool on)
        {
            if (im == null) return;
            var sp = SpriteDB.Get(key + (on ? "on" : "off"));
            if (sp == null) return;
            if (im.sprite != sp) im.sprite = sp;
            im.enabled = true;
        }

        // 켜짐 = 초록(btn_go), 꺼짐 = 나무(btn_wood)
        static void SetToggle(Button b, bool on)
        {
            if (b == null) return;
            var img = b.GetComponent<Image>();
            var sp = SpriteDB.Get(on ? "UI/btn_go" : "UI/btn_wood");
            if (img != null && sp != null) img.sprite = sp;
            var t = SceneUI.TextIn(b.transform);
            SetKey(t, on ? "켜짐" : "꺼짐");
            TempDialog.SkinButtonText(t, on ? "btn_go" : "btn_wood");
        }

        // 지금 상태로 (M 키·언어 변경으로도 바뀌므로 열 때마다)
        public void Refresh()
        {
            SetToggle(tBgm, Snd.BgmOn);
            SetToggle(tSfx, Snd.SfxOn);
            SetIcon(iconBgm, "UI/icon_bgm_", Snd.BgmOn && !Snd.muted);
            SetIcon(iconSfx, "UI/icon_sfx_", Snd.SfxOn && !Snd.muted);
            SetToggle(tSimple, FxPrefs.Simple);
            if (sfx != null) sfx.SetValueWithoutNotify(Snd.SfxVol);
            if (bgm != null) bgm.SetValueWithoutNotify(Snd.BgmVol);
            if (muteLabel != null) SetKey(muteLabel, Snd.muted ? "음소거: 켜짐" : "음소거: 꺼짐");
            // 언어: 지금 언어 이름 + ▼, 목록은 지금 언어만 초록. 언어 이름은 그 언어의 제목 폰트로 (없는 글자가 대체 폰트로 벌어지지 않게)
            if (langBtn != null) LangLabel(SceneUI.TextIn(langBtn.transform), Loc.Lang, Loc.LANG_NAME[Loc.Lang] + " ▼", "btn_wood");
            for (int i = 0; i < langItems.Length; i++)
            {
                if (langItems[i] == null) continue;
                string code = Loc.LANGS[i]; bool cur = code == Loc.Lang;
                var img = langItems[i].GetComponent<Image>();
                var sp = SpriteDB.Get(cur ? "UI/btn_go" : "UI/btn_wood");
                if (img != null && sp != null) img.sprite = sp;
                LangLabel(SceneUI.TextIn(langItems[i].transform), code, Loc.LANG_NAME[code], cur ? "btn_go" : "btn_wood");
            }
        }

        static void LangLabel(TMP_Text t, string code, string text, string skin)
        {
            if (t == null) return;
            if (t.TryGetComponent<LocText>(out var lt)) Object.Destroy(lt);   // 언어 이름은 번역하지 않는다
            UIUtil.SetText(t, text);
            var face = LocFonts.I?.FaceFor(code, LocFonts.Role.Title);
            if (face?.font != null && t.font != face.font) { t.font = face.font; t.fontSharedMaterial = face.font.material; }
            TempDialog.SkinButtonText(t, skin);
        }

        // 직접 여닫는 씬용 (MainGame은 자기 팝업 관리로 Root만 쓴다)
        public void Open()
        {
            if (Root == null) return;
            Refresh();
            if (langList != null) langList.SetActive(false);
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

            // 그림은 Art/UI (세계수 탭 창과 같은 세트): 창 panel_wood · 짧은 제목은 ribbon 위, 긴 문장(확인 질문)은 창 안 글자 + 덩굴
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

        // 세로 창: 어두운 막 + 리본 제목 + 위에서 아래로 쌓이는 칸 (높이는 내용에 맞춘다). col에 Column* 도우미로 칸을 더한다
        const float COL_W = 600, ROW_H = 84;

        public static GameObject BuildColumn(Canvas canvas, string name, string title, out Transform col)
        {
            var dim = Dim(canvas, name);
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            box.transform.SetParent(dim.transform, false);
            box.GetComponent<RectTransform>().sizeDelta = new Vector2(COL_W + 100, 0);
            Skin(box.GetComponent<Image>(), "panel_wood", BOX);
            var v = box.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(50, 50, 110, 50); v.spacing = 14;   // 위 110 = 리본이 걸치는 자리
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            col = box.transform;
            // 리본은 창 위 끝에 걸친다 (창 높이가 내용에 따라 달라 위쪽 기준으로 놓는다)
            var rib = Ribbon(col, title, Vector2.zero, 560);
            var rr = rib.transform.parent.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 1); rr.anchoredPosition = new Vector2(0, -20);
            return dim;
        }

        public static TMP_Text ColumnButton(Transform col, string label, UnityAction action)
        {
            var btn = SkinButton(col, "Btn", label, action, "btn_wood", 34, out var text);
            btn.gameObject.AddComponent<LayoutElement>().preferredHeight = ROW_H;
            return text;
        }

        public static TMP_Text ColumnLabel(Transform col, string text)
        {
            var t = MakeText(col, "Label", text, Vector2.zero, Vector2.zero, 32);
            t.alignment = TextAlignmentOptions.MidlineLeft; t.color = INK;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
            return t;
        }

        // 임시 창 글자를 번역 키(한국어 원문)에 묶는다: LocText가 지금 언어로 넣고, 언어가 바뀌면(Loc.Changed) 다시 넣는다.
        // 임시 창 글자는 모두 MakeText(제목 역할, 기본 머티리얼)로 만든다
        internal static void Localize(TMP_Text t, string key)
        {
            if (t == null) return;
            if (!t.TryGetComponent<LocText>(out var lt)) lt = t.gameObject.AddComponent<LocText>();
            lt.role = LocFonts.Role.Title; lt.preset = -1;
            t.text = key; lt.key = key;
            lt.Apply();
        }

        // 이름 + 가로 막대 (0~1). 움직이는 동안 onChange를 부른다
        public static Slider ColumnSlider(Transform col, string label, float value, UnityAction<float> onChange)
        {
            var row = new GameObject("SliderRow", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(col, false);
            row.GetComponent<LayoutElement>().preferredHeight = 60;
            var t = MakeText(row.transform, "Name", label, Vector2.zero, Vector2.zero, 30);
            var tr = t.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = new Vector2(0.3f, 1); tr.offsetMin = tr.offsetMax = Vector2.zero;
            t.alignment = TextAlignmentOptions.MidlineLeft; t.color = INK;

            var s = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            s.transform.SetParent(row.transform, false);
            var sr = s.GetComponent<RectTransform>(); sr.anchorMin = new Vector2(0.32f, 0.3f); sr.anchorMax = new Vector2(1, 0.7f); sr.offsetMin = sr.offsetMax = Vector2.zero;
            var back = Part(s.transform, "Background", new Color(0.78f, 0.68f, 0.52f)); Stretch(back.rectTransform); back.raycastTarget = true;   // 막대 아무 곳이나 눌러도 옮겨진다
            var area = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(s.transform, false); Stretch(area);
            var fill = Part(area, "Fill", new Color(0.55f, 0.36f, 0.2f)); Stretch(fill.rectTransform);
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

        // 가로 막대 하나 (0~1). 이름 칸 없이 막대만 — 설정 창(OptionPanelBuilder)이 쓴다
        internal static Slider MakeSlider(Transform parent, string name)
        {
            var s = new GameObject(name, typeof(RectTransform), typeof(Slider));
            s.transform.SetParent(parent, false);
            // 그림: 바탕 UI/progress_back · 채움 UI/progress_fill2 (9-slice) · 손잡이 UI/badge_round (사용자 2026-11-11). 그림이 없으면 단색
            var back = Part(s.transform, "Background", new Color(0.78f, 0.68f, 0.52f)); Stretch(back.rectTransform); back.raycastTarget = true;
            Skin(back, "progress_back", new Color(0.78f, 0.68f, 0.52f)); FitBorder(back, 30, 1f);
            var area = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(s.transform, false); Stretch(area);
            var fill = Part(area, "Fill", new Color(0.55f, 0.36f, 0.2f)); Stretch(fill.rectTransform);
            Skin(fill, "progress_fill2", new Color(0.55f, 0.36f, 0.2f)); FitBorder(fill, 30, 1f);
            var hArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            hArea.SetParent(s.transform, false); Stretch(hArea); hArea.offsetMin = new Vector2(16, 0); hArea.offsetMax = new Vector2(-16, 0);
            var handle = Part(hArea, "Handle", new Color(0.36f, 0.22f, 0.12f));
            Skin(handle, "badge_round", new Color(0.36f, 0.22f, 0.12f)); handle.type = Image.Type.Simple; handle.preserveAspect = true;
            handle.rectTransform.sizeDelta = new Vector2(44, 0);
            handle.rectTransform.anchorMin = new Vector2(0, -0.35f); handle.rectTransform.anchorMax = new Vector2(0, 1.35f);
            handle.raycastTarget = true;
            var slider = s.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            return slider;
        }

        static Image Part(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.color = c; im.raycastTarget = false;
            return im;
        }

        // ===== 임시 창 그림 (Art/UI, SpriteDB) — 그림이 없으면 예전 단색 =====
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
