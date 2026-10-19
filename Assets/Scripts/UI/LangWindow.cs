using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush
{
    // 언어 선택 창 (지구본 버튼 Button_Lang). 씬에 배치된 LangPanel이 있으면 그것을 쓴다:
    //   안의 Lang_<코드> 버튼(Lang_ko · Lang_en …) = 그 언어로, Button_Close(또는 Exit) = 닫기.
    // 배치된 창이 없으면 임시 창(TempDialog)을 만든다. 언어를 고르면 바로 바꾸고 닫는다. 바깥(어두운 막)을 눌러도 닫힌다
    public class LangWindow
    {
        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        readonly TMP_Text[] labels = new TMP_Text[Loc.LANGS.Length];
        readonly Image[] frames = new Image[Loc.LANGS.Length];
        bool counted;     // App 모달 수에 넣었나 (Open/Close를 직접 쓰는 씬)
        UnityAction onClose;

        // onClose: 닫기(버튼·바깥·고른 뒤). 비우면 Close()
        public static LangWindow For(Scene scene, UnityAction onClose = null)
        {
            var w = new LangWindow();
            w.onClose = onClose ?? w.Close;
            var placed = SceneUI.Find(scene, "LangPanel");
            if (placed != null) w.WirePlaced(placed.gameObject);
            else w.BuildTemp(scene);
            if (w.Root != null) w.Root.SetActive(false);
            return w;
        }

        void WirePlaced(GameObject root)
        {
            Root = root;
            var close = SceneUI.Find(root.transform, "Button_Close") ?? SceneUI.Find(root.transform, "Exit");
            if (close != null) SceneUI.Bind(close, () => { Snd.Ui(); onClose(); });
            for (int i = 0; i < Loc.LANGS.Length; i++)
            {
                var t = SceneUI.Find(root.transform, "Lang_" + Loc.LANGS[i]);
                if (t == null) { Debug.LogWarning($"[Lang] 배치된 LangPanel 안에 'Lang_{Loc.LANGS[i]}' 버튼이 없어요"); continue; }
                string code = Loc.LANGS[i];
                SceneUI.Bind(t, () => Pick(code));
                labels[i] = SceneUI.TextIn(t);
                frames[i] = t.GetComponent<Image>();
            }
        }

        void BuildTemp(Scene scene)
        {
            var canvas = TempDialog.RootCanvas(scene);
            if (canvas == null) { Debug.LogWarning("[Lang] 캔버스가 없어 언어 창을 만들 수 없어요"); return; }
            var btns = new (string, UnityAction)[Loc.LANGS.Length];
            for (int i = 0; i < btns.Length; i++) { string code = Loc.LANGS[i]; btns[i] = (Loc.LANG_NAME[code], () => Pick(code)); }
            Root = TempDialog.Build(canvas, "LangPanel", "Language", btns);
            for (int i = 0; i < btns.Length; i++)
            {
                var b = Root.transform.Find("Box/Row/Btn" + i);
                if (b == null) continue;
                labels[i] = b.GetComponentInChildren<TMP_Text>(true);
                frames[i] = b.GetComponent<Image>();
            }
            // 바깥(어두운 막)을 누르면 닫는다. 창 안 클릭은 창(Box)이 받는다
            var dimBtn = Root.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => { Snd.Ui(); onClose(); });
            var box = Root.transform.Find("Box");
            if (box != null && box.TryGetComponent<Image>(out var bi)) bi.raycastTarget = true;
        }

        void Pick(string code)
        {
            Snd.Ui();
            Loc.SetLang(code);
            onClose();
        }

        // 지금 언어 버튼만 초록(btn_go), 나머지는 나무. 언어 이름은 그 언어의 글자로 쓰므로 버튼마다 그 언어의 제목 폰트를 쓴다
        // (지금 언어 폰트로 쓰면 없는 글자(한국어 폰트의 키릴 문자 등)가 대체 폰트로 그려져 간격이 벌어진다)
        public void Refresh()
        {
            for (int i = 0; i < Loc.LANGS.Length; i++)
            {
                string code = Loc.LANGS[i];
                bool cur = code == Loc.Lang;
                if (frames[i] != null)
                {
                    var sp = SpriteDB.Get(cur ? "UI/btn_go" : "UI/btn_wood");
                    if (sp != null) frames[i].sprite = sp;
                }
                var t = labels[i];
                if (t == null) continue;
                UIUtil.SetText(t, Loc.LANG_NAME[code]);
                var face = LocFonts.I?.FaceFor(code, LocFonts.Role.Title);
                if (face?.font != null && t.font != face.font) { t.font = face.font; t.fontSharedMaterial = face.font.material; }
                TempDialog.SkinButtonText(t, cur ? "btn_go" : "btn_wood");
            }
        }

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
}
