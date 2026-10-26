#if UNITY_EDITOR || MMM_DEV
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Game;

namespace MoreMush
{
    // 테스트용 개발자 메뉴: 모든 화면 왼쪽 가장자리의 작은 DEV 버튼 → 골드 복사.
    // 에디터와 스크립트 정의 MMM_DEV가 있는 빌드에만 들어간다 (Player Settings → Scripting Define Symbols, 출시 빌드에서는 뺀다)
    public class DevMenu : MonoBehaviour
    {
        const double ADD_GOLD = 1e6;   // 골드가 적을 때 ×10 대신 최소로 더하는 양

        GameObject panel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<DevMenu>() != null) return;
            var go = new GameObject("DevMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<DevMenu>();
        }

        void Awake()
        {
            // 씬 UI보다 위에 그리는 따로 된 캔버스 (씬이 바뀌어도 남는다)
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var toggle = TempDialog.SkinButton(transform, "Button_Dev", "DEV", Toggle, "btn_danger", 28, out _);
            Place(toggle.GetComponent<RectTransform>(), new Vector2(70, -260), new Vector2(120, 64));
            toggle.GetComponent<Image>().color = new Color(1, 1, 1, 0.75f);

            panel = new GameObject("DevPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(transform, false);
            Place(panel.GetComponent<RectTransform>(), new Vector2(230, -260), new Vector2(360, 0), new Vector2(0, 0.5f));
            TempDialog.Skin(panel.GetComponent<Image>(), "panel_wood", TempDialog.BOX);
            var v = panel.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 24, 24); v.spacing = 10;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Row("골드 ×10", () => AddGold(G.gold * 9));
            Row("골드 +100만", () => AddGold(ADD_GOLD));
            panel.SetActive(false);
        }

        // 화면 왼쪽 가장자리 기준 (anchor 왼쪽 가운데)
        static void Place(RectTransform rt, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        void Row(string label, UnityEngine.Events.UnityAction action)
        {
            var b = TempDialog.SkinButton(panel.transform, "Btn", label, action, "btn_wood", 30, out _);
            b.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;
        }

        void Toggle() { Snd.Ui(); panel.SetActive(!panel.activeSelf); }

        static void AddGold(double amount)
        {
            if (G == null) { App.Notify("[DEV] 세이브가 아직 없어요"); return; }
            amount = System.Math.Max(amount, ADD_GOLD);
            G.gold += amount;
            SaveGame();
            Snd.Ui();
            App.Notify("[DEV] 골드 +" + U.Fmt(amount));
        }
    }
}
#endif
