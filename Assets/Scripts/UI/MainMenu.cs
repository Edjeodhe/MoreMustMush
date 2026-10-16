using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Game;

namespace MoreMush
{
    // MainMenu 씬 컨트롤러. 배치는 씬에서 사람이 하고, 여기서는 버튼에 기능만 연결한다.
    // 버튼은 이름으로 찾는다 (인스펙터에 직접 끌어 넣으면 그쪽이 우선):
    //   Button_Start · Button_Reset · Button_Option · Button_Exit · Button_Lang
    // 확인 창(선택): ResetConfirm 안에 Button_Yes / Button_No, 옵션 창(선택): OptionPanel 안에 Button_Close / Button_Mute.
    // 두 창이 씬에 없으면 코드가 간단한 창을 만들어 쓴다 (TempDialog). 옵션 창은 모든 씬 공용(OptionWindow), Esc로 열고 닫는다.
    public class MainMenu : MonoBehaviour
    {
        [Header("Buttons (비우면 이름으로 찾음)")]
        public Button start, reset, option, exit, lang;

        [Header("Panels (비워도 됨)")]
        public GameObject resetConfirm;

        OptionWindow optionWin;

        void Awake()
        {
            start = Bind(start, "Button_Start", OnStart);
            reset = Bind(reset, "Button_Reset", OnReset);
            option = Bind(option, "Button_Option", OnOption);
            exit = Bind(exit, "Button_Exit", OnExit);
            lang = Bind(lang, "Button_Lang", OnLang);
            if (resetConfirm == null) resetConfirm = FindByName("ResetConfirm");
            WirePanel(resetConfirm, ("Button_Yes", OnResetYes), ("Button_No", CloseAll));
            optionWin = OptionWindow.For(gameObject.scene, () => { Snd.Ui(); CloseAll(); });
            CloseAll();
        }

        void Start() => RefreshButtons();

        void OnEnable() => Loc.Changed += OnLangChanged;
        void OnDisable() => Loc.Changed -= OnLangChanged;
        void OnLangChanged() => optionWin?.Refresh();

        // 세이브가 없으면 Reset은 의미가 없으니 숨긴다
        void RefreshButtons()
        {
            if (reset != null) reset.gameObject.SetActive(SaveIO.HasSave());
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

        void OnOption() { Snd.Ui(); CloseAll(); optionWin.Open(); }

        void OnExit() { Snd.Ui(); App.Quit(); }

        // 지구본 버튼: 다음 언어로 돌린다 (Loc.LANGS 순서)
        void OnLang() { Snd.Ui(); Loc.NextLang(); }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            // Esc: 열린 창이 있으면 닫고, 없으면 옵션 창
            bool anyOpen = optionWin.IsOpen || (resetConfirm != null && resetConfirm.activeSelf);
            if (anyOpen) { Snd.Ui(); CloseAll(); } else OnOption();
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
            optionWin?.Close();
        }

        GameObject EnsureConfirm()
        {
            if (resetConfirm != null) return resetConfirm;
            var canvas = TempDialog.RootCanvas(gameObject.scene);
            if (canvas == null) { Debug.LogWarning("[MainMenu] 캔버스가 없어 확인 창을 만들 수 없어요"); return null; }
            resetConfirm = TempDialog.Build(canvas, "ResetConfirm", Loc.T("정말 초기화하시겠습니까?"),
                (Loc.T("예"), OnResetYes), (Loc.T("아니오"), CloseAll));
            return resetConfirm;
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
    }
}
