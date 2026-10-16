using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Game;

namespace MoreMush
{
    // MainMenu 씬 컨트롤러. 배치는 씬에서 사람이 하고, 여기서는 버튼에 기능만 연결한다.
    // 버튼은 이름으로 찾는다 (인스펙터에 직접 끌어 넣으면 그쪽이 우선):
    //   Button_Start · Button_Reset · Button_Option · Button_Exit · Button_Lang
    // 확인 창(선택): ResetConfirm 안에 Button_Yes / Button_No, 옵션 창(선택): OptionPanel 안에 Button_Close / Button_Mute.
    // 두 창이 씬에 없으면 코드가 간단한 창을 만들어 쓴다.
    public class MainMenu : MonoBehaviour
    {
        [Header("Buttons (비우면 이름으로 찾음)")]
        public Button start, reset, option, exit, lang;

        [Header("Panels (비워도 됨)")]
        public GameObject resetConfirm;
        public GameObject optionPanel;

        TMP_Text muteLabel;

        void Awake()
        {
            start = Bind(start, "Button_Start", OnStart);
            reset = Bind(reset, "Button_Reset", OnReset);
            option = Bind(option, "Button_Option", OnOption);
            exit = Bind(exit, "Button_Exit", OnExit);
            lang = Bind(lang, "Button_Lang", OnLang);
            if (resetConfirm == null) resetConfirm = FindByName("ResetConfirm");
            if (optionPanel == null) optionPanel = FindByName("OptionPanel");
            WirePanel(resetConfirm, ("Button_Yes", OnResetYes), ("Button_No", CloseAll));
            WirePanel(optionPanel, ("Button_Close", CloseAll), ("Button_Mute", OnMute));
            if (optionPanel != null) muteLabel = FindIn(optionPanel, "Button_Mute")?.GetComponentInChildren<TMP_Text>(true);
            CloseAll();
        }

        void Start() => RefreshButtons();

        void OnEnable() => Loc.Changed += RefreshMute;
        void OnDisable() => Loc.Changed -= RefreshMute;

        // 세이브가 없으면 Reset은 의미가 없으니 숨긴다
        void RefreshButtons()
        {
            if (reset != null) reset.gameObject.SetActive(SaveIO.HasSave());
            RefreshMute();
        }

        // ===== 버튼 =====
        // 세이브가 있으면 불러와 이어서, 없으면 새로 만들어 시작 (App.EnsureSave)
        void OnStart()
        {
            Snd.Ui();
            G = null;   // 메뉴로 돌아왔다 다시 시작해도 저장된 것을 새로 읽는다
            App.EnsureSave();
            App.Go(Scenes.MainGame);
        }

        void OnReset() { Snd.Ui(); Open(EnsureConfirm()); }

        void OnResetYes()
        {
            SaveIO.Wipe();
            G = null;
            Snd.Ui();
            CloseAll();
            RefreshButtons();
        }

        void OnOption() { Snd.Ui(); Open(EnsureOption()); RefreshMute(); }

        void OnExit() { Snd.Ui(); App.Quit(); }

        // 지구본 버튼: 다음 언어로 돌린다 (Loc.LANGS 순서)
        void OnLang() { Snd.Ui(); Loc.NextLang(); }

        void OnMute() { Snd.muted = !Snd.muted; RefreshMute(); }

        void RefreshMute()
        {
            if (muteLabel != null) muteLabel.text = Snd.muted ? Loc.T("소리: 꺼짐") : Loc.T("소리: 켜짐");
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) CloseAll();
        }

        // ===== 창 =====
        void Open(GameObject panel)
        {
            if (panel == null) return;
            CloseAll();
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        void CloseAll()
        {
            if (resetConfirm != null) resetConfirm.SetActive(false);
            if (optionPanel != null) optionPanel.SetActive(false);
        }

        GameObject EnsureConfirm()
        {
            if (resetConfirm != null) return resetConfirm;
            resetConfirm = BuildDialog("ResetConfirm", Loc.T("정말 초기화하시겠습니까?"),
                (Loc.T("예"), OnResetYes), (Loc.T("아니오"), CloseAll));
            return resetConfirm;
        }

        GameObject EnsureOption()
        {
            if (optionPanel != null) return optionPanel;
            optionPanel = BuildDialog("OptionPanel", Loc.T("설정"),
                (Snd.muted ? Loc.T("소리: 꺼짐") : Loc.T("소리: 켜짐"), OnMute), (Loc.T("닫기"), CloseAll));
            muteLabel = optionPanel != null ? optionPanel.transform.Find("Box/Row/Btn0")?.GetComponentInChildren<TMP_Text>(true) : null;
            return optionPanel;
        }

        // ===== 연결 도우미 =====
        Button Bind(Button b, string name, UnityEngine.Events.UnityAction action)
        {
            if (b == null) b = FindIn(gameObject.scene, name)?.GetComponent<Button>();
            if (b == null) { Debug.LogWarning($"[MainMenu] 버튼 '{name}'을(를) 씬에서 찾지 못했어요"); return null; }
            b.onClick.RemoveListener(action);
            b.onClick.AddListener(action);
            return b;
        }

        static void WirePanel(GameObject panel, params (string name, UnityEngine.Events.UnityAction action)[] btns)
        {
            if (panel == null) return;
            foreach (var (name, action) in btns)
            {
                var b = FindIn(panel, name)?.GetComponent<Button>();
                if (b == null) { Debug.LogWarning($"[MainMenu] '{panel.name}' 안에 버튼 '{name}'이(가) 없어요"); continue; }
                b.onClick.RemoveListener(action);
                b.onClick.AddListener(action);
            }
        }

        // 비활성 오브젝트까지 이름으로 찾는다 (씬 루트부터 한 번만 훑는다)
        static Transform FindIn(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindIn(root, name);
                if (t != null) return t;
            }
            return null;
        }

        static Transform FindIn(GameObject root, string name)
        {
            if (root.name == name) return root.transform;
            foreach (Transform c in root.transform)
            {
                var t = FindIn(c.gameObject, name);
                if (t != null) return t;
            }
            return null;
        }

        GameObject FindByName(string name) => FindIn(gameObject.scene, name)?.gameObject;

        // 씬에 확인·옵션 창이 없을 때만 쓰는 간단한 창 (어두운 배경 + 글 + 버튼 줄)
        GameObject BuildDialog(string name, string title, params (string label, UnityEngine.Events.UnityAction action)[] btns)
        {
            var canvas = start != null ? start.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
            if (canvas == null) { Debug.LogWarning($"[MainMenu] 캔버스가 없어 '{name}' 창을 만들 수 없어요"); return null; }
            canvas = canvas.rootCanvas;
            var dim = new GameObject(name, typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            Stretch(dim.GetComponent<RectTransform>());
            dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(dim.transform, false);
            var br = box.GetComponent<RectTransform>();
            br.sizeDelta = new Vector2(640, 320);
            box.GetComponent<Image>().color = new Color(0.98f, 0.93f, 0.8f);

            MakeText(box.transform, "Title", title, new Vector2(0, 60), new Vector2(580, 120), 44);

            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(box.transform, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchoredPosition = new Vector2(0, -80); rr.sizeDelta = new Vector2(580, 90);
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

        static void MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (stretch) Stretch(rt); else { rt.anchoredPosition = pos; rt.sizeDelta = size; }
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = fontSize; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.23f, 0.14f, 0.08f); t.raycastTarget = false;
            LocFonts.Apply(t, LocFonts.Role.Title, -1);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
