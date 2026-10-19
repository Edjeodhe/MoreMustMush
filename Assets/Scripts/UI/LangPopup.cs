using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // 언어 고르기 창 (지구본 버튼). 언어마다 버튼 하나, 누르면 그 언어로 바꾸고 닫는다. 바깥(어두운 곳)을 눌러도 닫힌다.
    // 씬에 배치한 창이 없어 TempDialog로 만든다. 지금 언어 버튼은 밝게 칠한다. 열린 동안 App.ModalOpen (뒤의 트리·세계수가 클릭을 받지 않게)
    public static class LangPopup
    {
        static readonly Color Current = new Color(0.86f, 0.62f, 0.3f);
        static GameObject open;

        public static void Open(UnityEngine.SceneManagement.Scene scene)
        {
            if (open != null) return;   // 이미 열려 있다
            var canvas = TempDialog.RootCanvas(scene);
            if (canvas == null) { Debug.LogWarning("[LangPopup] 캔버스가 없어 언어 창을 만들 수 없어요"); return; }
            var btns = new (string, UnityEngine.Events.UnityAction)[Loc.LANGS.Length];
            for (int i = 0; i < Loc.LANGS.Length; i++)
            {
                string code = Loc.LANGS[i];
                btns[i] = (Loc.LANG_NAME[code], () => { Close(); Loc.SetLang(code); });
            }
            open = TempDialog.Build(canvas, "LangPopup", Loc.T("언어"), btns);
            open.AddComponent<ModalHold>();

            var box = open.transform.Find("Box");
            var row = box.Find("Row");
            for (int i = 0; i < Loc.LANGS.Length; i++)
                if (Loc.LANGS[i] == Loc.Lang) row.GetChild(i).GetComponent<Image>().color = Current;

            // 어두운 바깥을 누르면 닫는다. 상자 안 빈 곳 클릭은 Box의 빈 버튼이 받아 바깥으로 올라가지 않게 한다
            Clickable(open, Close);
            Clickable(box.gameObject, null);
        }

        static void Close()
        {
            if (open == null) return;
            Snd.Ui();
            Object.Destroy(open);
            open = null;
        }

        static void Clickable(GameObject go, UnityEngine.Events.UnityAction a)
        {
            var b = go.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            if (a != null) b.onClick.AddListener(a);
        }

        // 창이 켜져 있는 동안 모달 한 칸 (씬이 바뀌면 이전 씬은 새 씬이 모달 수를 0으로 되돌리기 전에 꺼지므로 OnDisable로 돌려놓는다.
        // OnDestroy는 비동기로 씬을 내릴 때 늦게 와서, 새 씬에서 연 창의 칸을 빼 버린다)
        class ModalHold : MonoBehaviour
        {
            void OnEnable() => App.ModalPush();
            void OnDisable() => App.ModalPop();
        }
    }
}
