using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static MoreMush.Game;

namespace MoreMush
{
    // MainGame 씬 (업그레이드). 사람이 배치한 캔버스에 기능만 잇는다 — UI를 코드로 만들지 않는다.
    // 이름으로 찾는 것:
    //   Gold_UI_BG/Gold_Img·Ston_Img 안의 글자 = 골드·균사석
    //   Button_Option · Button_Lang · Button_StageStart · Stage_Text
    //   Button_MushTree(세계수 씬) · Button_MushDex(도감 팝업) · Button_Harvesters(공방 팝업)
    //   팝업: Harvesters · MushDex (각 팝업 안의 Exit = 닫기) · 옵션 팝업 OptionPanel(있으면)
    // 팝업이 떠 있어도 아래쪽 이동 버튼(MushTree·MushDex·Harvesters)은 눌린다. 다른 곳으로 가면 열린 팝업은 닫힌다.
    public class MainGameController : MonoBehaviour
    {
        // 팝업보다 위에 그리고 눌리게 할 이동 버튼 (팝업 위 정렬 순서)
        static readonly string[] NAV = { "Button_MushTree", "Button_MushDex", "Button_Harvesters" };
        const int NAV_SORT_ABOVE = 1;   // 팝업(루트 캔버스) 바로 위 — 너무 높으면 옵션·로딩 같은 다른 UI까지 덮는다
        static readonly System.Text.RegularExpressions.Regex STAGE_MARK = new System.Text.RegularExpressions.Regex(@"\bN\b");   // Stage_Text에 적어 둔 자리 표시 ("N Stage" → "12 Stage", 낱말 N 하나만)

        CurrencyBar currency;
        HarvesterShop shop;
        GameObject dex, option, current;   // current = 지금 열린 팝업
        GameObject[] popups = new GameObject[0];
        TMP_Text stageText;
        string stageFormat;

        void Awake()
        {
            var scene = gameObject.scene;
            var goldImg = SceneUI.Need(scene, "Gold_Img");
            var gemImg = SceneUI.Need(scene, "Ston_Img");
            currency = new CurrencyBar(SceneUI.TextIn(goldImg?.transform), SceneUI.TextIn(gemImg?.transform));

            stageText = SceneUI.Need(scene, "Stage_Text")?.GetComponent<TMP_Text>();
            stageFormat = stageText != null ? stageText.text : null;

            SceneUI.Bind(scene, "Button_StageStart", StartRound);
            SceneUI.Bind(scene, "Button_MushTree", ToOutGame);
            SceneUI.Bind(scene, "Button_MushDex", () => Toggle(dex));
            SceneUI.Bind(scene, "Button_Harvesters", () => Toggle(shop != null ? shop.root : null));
            SceneUI.Bind(scene, "Button_Option", () => Toggle(option));
            SceneUI.Bind(scene, "Button_Lang", () => { Snd.Ui(); Loc.NextLang(); });

            var shopRoot = SceneUI.Need(scene, "Harvesters");
            if (shopRoot != null) shop = new HarvesterShop(shopRoot, () => currency.Tick());
            dex = SceneUI.Need(scene, "MushDex");
            option = SceneUI.Find(scene, "OptionPanel")?.gameObject;   // 아직 배치 전일 수 있다
            popups = new[] { shopRoot, dex, option };
            foreach (var p in popups) BindExit(p);

            foreach (var n in NAV) LiftAbovePopups(SceneUI.Find(scene, n));
            CloseAll();
        }

        void Start()
        {
            App.EnsureSave();
            App.FlushMigrationNotice();
            RenderStage();
            currency.Tick();
        }

        void OnEnable() => Loc.Changed += OnLang;
        void OnDisable() { Loc.Changed -= OnLang; if (current != null) CloseAll(); }   // 열린 팝업의 모달 카운트를 남기지 않는다

        void OnLang() { RenderStage(); shop?.Render(); }

        void Update()
        {
            if (currency.Tick() && shop != null && shop.IsOpen) shop.Render();   // 열린 상점의 살 수 있음/없음을 재화에 맞춘다
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && current != null) CloseAll();
        }

        void RenderStage()
        {
            if (stageText == null || G == null) return;
            string n = StageNow().ToString();
            UIUtil.SetText(stageText, STAGE_MARK.IsMatch(stageFormat) ? STAGE_MARK.Replace(stageFormat, n, 1) : n);
        }

        // ===== 팝업 =====
        // 같은 팝업 버튼을 다시 누르면 닫고, 다른 팝업 버튼이면 바꿔 연다
        void Toggle(GameObject popup)
        {
            Snd.Ui();
            bool wasOpen = popup != null && current == popup;
            CloseAll();   // 다른 곳으로 가면 열린 팝업은 닫는다
            if (wasOpen) return;
            if (popup == null) { Debug.LogWarning("[MainGame] 열 팝업이 씬에 없어요"); return; }
            popup.SetActive(true);
            current = popup;
            App.ModalPush();
            if (shop != null && popup == shop.root) shop.Render();
        }

        void CloseAll()
        {
            foreach (var p in popups) UIUtil.Show(p, false);
            if (current != null) App.ModalPop();
            current = null;
        }

        void BindExit(GameObject popup)
        {
            if (popup == null) return;
            var exit = SceneUI.Find(popup.transform, "Exit");
            if (exit != null) SceneUI.Bind(exit, () => { Snd.Ui(); CloseAll(); });
        }

        // 팝업이 화면을 덮어도 이 버튼은 위에 그려지고 눌리게: 버튼에만 정렬 순서를 올린 캔버스를 붙인다 (UI를 만들지는 않는다)
        static void LiftAbovePopups(Transform t)
        {
            if (t == null) return;
            var parent = t.parent != null ? t.parent.GetComponentInParent<Canvas>() : null;
            var c = t.GetComponent<Canvas>();
            if (c == null) c = t.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = (parent != null ? parent.rootCanvas.sortingOrder : 0) + NAV_SORT_ABOVE;
            // 새 캔버스는 셰이더 채널이 비어 있어 TMP 외곽선·굵기가 달라 보일 수 있다 → 부모 것을 그대로
            if (parent != null) c.additionalShaderChannels = parent.rootCanvas.additionalShaderChannels;
            if (t.GetComponent<GraphicRaycaster>() == null) t.gameObject.AddComponent<GraphicRaycaster>();
        }

        // ===== 이동 =====
        public void StartRound() { Snd.Ui(); CloseAll(); RoundFlow.Request(); }        // → Stage
        public void ToOutGame() { Snd.Ui(); CloseAll(); App.Go(Scenes.OutGame); }      // → 세계수
        public void ToMainMenu() { Snd.Ui(); CloseAll(); App.Go(Scenes.MainMenu); }
    }
}
