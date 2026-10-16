using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // MainGame 씬 (업그레이드). 사람이 배치한 캔버스에 기능만 잇는다 — UI를 코드로 만들지 않는다.
    // 이름으로 찾는 것:
    //   Gold_UI_BG/Gold_Img·Ston_Img 안의 글자 = 골드·균사석
    //   Button_Option · Button_Lang · Button_StageStart · Stage_Text
    //   Button_MushTree(세계수 씬) · Button_MushDex(도감 팝업) · Button_Harvesters(공방 팝업)
    //   Button_Quest(의뢰 창, 지금 배치된 이름은 Quset — 둘 다 찾는다, 글자 = "의뢰" + 받을 수 있는 수)
    //   팝업: Harvesters(공방, HarvesterShop) · MushDex(도감, MushDexPopup) (각 팝업 안의 Exit = 닫기) · 옵션 팝업 OptionPanel(있으면) · 의뢰 QuestPanel(있으면, QuestWindow)
    // 팝업이 떠 있어도 아래쪽 이동 버튼(MushTree·MushDex·Harvesters)은 눌린다. 다른 곳으로 가면 열린 팝업은 닫힌다.
    // 업그레이드 트리: 임시로 옛 월드 트리(TreeWorld, TreeView)를 씬에 둔다. 트리 UI가 배치되면 바꾼다.
    // 기능 해금(Unlock): 트리는 1스테이지 클리어 뒤, 도감·세계수·의뢰 버튼은 조건을 채우면 보인다.
    public class MainGameController : MonoBehaviour
    {
        // 팝업보다 위에 그리고 눌리게 할 이동 버튼 (팝업 위 정렬 순서)
        static readonly string[] NAV = { "Button_MushTree", "Button_MushDex", "Button_Harvesters" };
        const int NAV_SORT_ABOVE = 1;   // 팝업(루트 캔버스) 바로 위 — 너무 높으면 옵션·로딩 같은 다른 UI까지 덮는다
        static readonly System.Text.RegularExpressions.Regex STAGE_MARK = new System.Text.RegularExpressions.Regex(@"\bN\b");   // Stage_Text에 적어 둔 자리 표시 ("N Stage" → "12 Stage", 낱말 N 하나만)

        CurrencyBar currency;
        HarvesterShop shop;
        MushDexPopup dex;
        GameObject option, current;   // current = 지금 열린 팝업
        OptionWindow optionWin;       // 옵션 창 (배치 전이면 임시 창)
        QuestWindow quest;            // 의뢰 창 (배치 전이면 임시 창)
        GameObject tree, dexButton, treeButton, questButton;   // 해금 전엔 숨긴다
        TMP_Text questLabel;
        int shownClaimable = -1;
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
            SceneUI.Bind(scene, "Button_MushDex", () => Toggle(dex != null ? dex.root : null));
            SceneUI.Bind(scene, "Button_Harvesters", () => Toggle(shop != null ? shop.root : null));
            SceneUI.Bind(scene, "Button_Option", () => Toggle(option));
            SceneUI.Bind(scene, "Button_Lang", () => { Snd.Ui(); LangPopup.Open(scene); });

            var shopRoot = SceneUI.Need(scene, "Harvesters");
            if (shopRoot != null) shop = new HarvesterShop(shopRoot, () => currency.Tick());
            var dexRoot = SceneUI.Need(scene, "MushDex");
            if (dexRoot != null) dex = new MushDexPopup(dexRoot);
            optionWin = OptionWindow.For(scene, () => { Snd.Ui(); CloseAll(); });   // 배치된 OptionPanel, 없으면 임시 창
            option = optionWin.Root;
            quest = QuestWindow.For(scene, () => { Snd.Ui(); CloseAll(); });
            var qb = SceneUI.Find(scene, "Button_Quest") ?? SceneUI.Find(scene, "Quset");
            if (qb == null) Debug.LogWarning("[MainGame] 의뢰 버튼(Button_Quest)이 씬에 없어요");
            else { SceneUI.Bind(qb, () => Toggle(quest.Root)); questButton = qb.gameObject; questLabel = SceneUI.TextIn(qb); }
            popups = new[] { shopRoot, dexRoot, option, quest.Root };
            foreach (var p in new[] { shopRoot, dexRoot }) BindExit(p);   // 옵션·의뢰 창은 OptionWindow·QuestWindow가 닫기를 잇는다

            foreach (var n in NAV) LiftAbovePopups(SceneUI.Find(scene, n));
            var tv = FindFirstObjectByType<TreeView>(FindObjectsInactive.Include);
            tree = tv != null ? tv.gameObject : null;
            if (tree == null) Debug.LogWarning("[MainGame] 업그레이드 트리(TreeView)가 씬에 없어요");
            dexButton = SceneUI.Find(scene, "Button_MushDex")?.gameObject;
            treeButton = SceneUI.Find(scene, "Button_MushTree")?.gameObject;
            CloseAll();
        }

        void Start()
        {
            App.EnsureSave();
            App.FlushMigrationNotice();
            Progress();
            ApplyUnlocks();
            RenderStage();
            currency.Tick();
        }

        void OnEnable() => Loc.Changed += OnLang;
        void OnDisable() { Loc.Changed -= OnLang; if (current != null) CloseAll(); }   // 열린 팝업의 모달 카운트를 남기지 않는다

        void OnLang() { RenderStage(); shop?.Render(); dex?.LangChanged(); quest.Render(); shownClaimable = -1; RenderQuestButton(); }

        void Update()
        {
            if (currency.Tick() && shop != null && shop.IsOpen) shop.Render();   // 열린 상점의 살 수 있음/없음을 재화에 맞춘다
            dex?.Tick();
            quest.Tick();
            RenderQuestButton();   // 자정에 의뢰가 바뀌면 받을 수 있는 수도 바뀐다 (같으면 바로 돌아간다)
            var kb = Keyboard.current;
            // Esc: 열린 팝업이 있으면 닫고, 없으면 옵션 창
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { if (current != null) { Snd.Ui(); CloseAll(); } else Toggle(option); }
        }

        void RenderStage()
        {
            if (stageText == null || G == null) return;
            string n = StageNow().ToString();
            UIUtil.SetText(stageText, STAGE_MARK.IsMatch(stageFormat) ? STAGE_MARK.Replace(stageFormat, n, 1) : n);
        }

        // 1스테이지(튜토리얼) 클리어 뒤 처음 오면 첫 노드를 살 골드를 보장한다 (기획서: "첫 노드만 해금 가능한 재화").
        // 새로 열린 기능은 알림으로 알린다. 지금 이 씬에 버튼이 있는 기능만 (강화는 트리가 나타나는 것으로 안다)
        static readonly string[] NOTIFY_FEATS = { Unlock.CODEX, Unlock.WORLDTREE, Unlock.QUEST };

        static void Progress()
        {
            if (G == null) return;
            bool changed = false;
            if (G.tut == 0 && G.stage > 1)
            {
                G.tut = 1;
                G.gold = System.Math.Max(G.gold, NodeCost(NODE[ROOT_NODE], Lv(ROOT_NODE)).gold);
                changed = true;
            }
            var opened = Unlock.Newly();
            var shown = opened.Where(k => NOTIFY_FEATS.Contains(k)).ToList();
            if (shown.Count > 0) App.Notify(Loc.F("새 기능이 열렸어요: {0}", string.Join(" · ", shown.Select(Unlock.Name))));
            if (changed || opened.Count > 0) SaveGame();
        }

        void ApplyUnlocks()
        {
            UIUtil.Show(tree, Unlock.On(Unlock.UPGRADE));
            UIUtil.Show(dexButton, Unlock.On(Unlock.CODEX));
            UIUtil.Show(treeButton, Unlock.On(Unlock.WORLDTREE));
            UIUtil.Show(questButton, Unlock.On(Unlock.QUEST));
            RenderQuestButton();
        }

        // 의뢰 버튼 글자: 받을 수 있는 보상이 있으면 수를 붙인다
        void RenderQuestButton()
        {
            if (questLabel == null || G == null) return;
            QuestEnsure();
            int n = QuestClaimableCount();
            if (n == shownClaimable) return;
            shownClaimable = n;
            UIUtil.SetText(questLabel, n > 0 ? $"{Loc.T("의뢰")} ({n})" : Loc.T("의뢰"));
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
            if (dex != null && popup == dex.root) dex.Open();
            if (popup == option) optionWin.Refresh();
            if (popup == quest.Root) quest.Open();
        }

        void CloseAll()
        {
            bool questWasOpen = quest != null && quest.IsOpen;
            foreach (var p in popups) UIUtil.Show(p, false);
            if (questWasOpen) RenderQuestButton();   // 받은 보상만큼 버튼 수를 줄인다
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
