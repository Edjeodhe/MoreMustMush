using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // MainGame 씬 (업그레이드). 사람이 배치한 캔버스에 기능만 잇는다 — UI를 코드로 만들지 않는다.
    // 이름으로 찾는 것:
    //   Gold_UI_BG/Gold_Img·Ston_Img 안의 글자 = 골드·균사석
    //   Button_Option · Button_Lang(언어 선택 창 LangPanel, LangWindow) · Button_StageStart · Stage_Text
    //   Button_MushTree(세계수 씬) · Button_MushDex(도감 팝업) · Button_Harvesters(공방 팝업)
    //   Button_Quest(의뢰 창, 지금 배치된 이름은 Quset — 둘 다 찾는다, 글자 = "의뢰" + 받을 수 있는 수)
    //   Button_Boss(보스 도전 켜고 끄기, 보스에게 처음 지면 보인다). 배치 전이면 Button_StageStart를 복제해 스테이지 패널 위에 임시로 둔다
    //   팝업: Harvesters(공방, HarvesterShop) · MushDex(도감, MushDexPopup) (각 팝업 안의 Exit = 닫기) · 옵션 팝업 OptionPanel(있으면) · 의뢰 QuestPanel(있으면, QuestWindow)
    // 팝업이 떠 있어도 아래쪽 이동 버튼(MushTree·MushDex·Harvesters)은 눌린다. 다른 곳으로 가면 열린 팝업은 닫힌다.
    // 업그레이드 트리: 임시로 옛 월드 트리(TreeWorld, TreeView)를 씬에 둔다. 트리 UI가 배치되면 바꾼다.
    // 기능 해금(Unlock): 트리는 1스테이지 클리어 뒤, 도감·세계수·의뢰 버튼은 조건을 채우면 보인다.
    public class MainGameController : MonoBehaviour
    {
        // 팝업보다 위에 그리고 눌리게 할 이동 버튼 (팝업 위 정렬 순서)
        static readonly string[] NAV = { "Button_MushTree", "Button_MushDex", "Button_Harvesters" };
        const int NAV_SORT_ABOVE = 1;   // 팝업(루트 캔버스) 바로 위 — 너무 높으면 옵션·로딩 같은 다른 UI까지 덮는다

        CurrencyBar currency;
        HarvesterShop shop;
        MushDexPopup dex;
        GameObject option, current;   // current = 지금 열린 팝업
        OptionWindow optionWin;       // 옵션 창 (배치 전이면 임시 창)
        QuestWindow quest;            // 의뢰 창 (배치 전이면 임시 창)
        LangWindow langWin;           // 언어 선택 창 (배치 전이면 임시 창)
        GameObject tree, dexButton, treeButton, questButton, bossButton;   // 해금 전엔 숨긴다
        TMP_Text questLabel, bossLabel;
        Image bossFrame;
        Transform startButton;
        TreeView treeView;
        UnlockGuide guide;            // 새 기능 안내 (배치 전이면 알림만)
        int shownClaimable = -1;
        GameObject[] popups = new GameObject[0];
        TMP_Text stageText;

        void Awake()
        {
            var scene = gameObject.scene;
            var goldImg = SceneUI.Need(scene, "Gold_Img");
            var gemImg = SceneUI.Need(scene, "Ston_Img");
            currency = new CurrencyBar(SceneUI.TextIn(goldImg?.transform), SceneUI.TextIn(gemImg?.transform));

            stageText = SceneUI.Need(scene, "Stage_Text")?.GetComponent<TMP_Text>();

            startButton = SceneUI.Need(scene, "Button_StageStart")?.transform;
            WireBossButton(scene);
            SceneUI.Bind(startButton, StartRound);
            SceneUI.Bind(scene, "Button_MushTree", ToOutGame);
            SceneUI.Bind(scene, "Button_MushDex", () => Toggle(dex != null ? dex.root : null));
            SceneUI.Bind(scene, "Button_Harvesters", () => Toggle(shop != null ? shop.root : null));
            SceneUI.Bind(scene, "Button_Option", () => Toggle(option));
            SceneUI.Bind(scene, "Button_Lang", () => Toggle(langWin != null ? langWin.Root : null));

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
            langWin = LangWindow.For(scene, CloseAll);   // 고르거나 바깥을 누르면 닫힌다
            popups = new[] { shopRoot, dexRoot, option, quest.Root, langWin.Root };
            foreach (var p in new[] { shopRoot, dexRoot }) BindExit(p);   // 옵션·의뢰 창은 OptionWindow·QuestWindow가 닫기를 잇는다

            foreach (var n in NAV) LiftAbovePopups(SceneUI.Find(scene, n));
            treeView = FindFirstObjectByType<TreeView>(FindObjectsInactive.Include);
            tree = treeView != null ? treeView.gameObject : null;
            if (tree == null) Debug.LogWarning("[MainGame] 업그레이드 트리(TreeView)가 씬에 없어요");
            guide = SceneUI.Find(scene, "UnlockGuide")?.GetComponent<UnlockGuide>();
            dexButton = SceneUI.Find(scene, "Button_MushDex")?.gameObject;
            treeButton = SceneUI.Find(scene, "Button_MushTree")?.gameObject;
            CloseAll();
        }

        void Start()
        {
            App.EnsureSave();
            App.FlushMigrationNotice();
            var opened = Progress();
            ApplyUnlocks();
            RenderStage();
            currency.Tick();
            Tutorial.ForMain(gameObject.scene, treeView, startButton);
            AnnounceUnlocks(opened);
        }

        void OnEnable() => Loc.Changed += OnLang;
        void OnDisable() { Loc.Changed -= OnLang; if (current != null) CloseAll(); }   // 열린 팝업의 모달 카운트를 남기지 않는다

        void OnLang() { RenderStage(); shop?.Render(); dex?.LangChanged(); quest.Render(); shownClaimable = -1; RenderQuestButton(); RenderBoss(); }

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
            string n = StageLabel(StageNow());   // 튜토리얼 판 T1~T2, 그 뒤 1부터 (5회차 P-2)
            UIUtil.SetText(stageText, Loc.F("스테이지 {0}", n));
        }

        // 1스테이지(튜토리얼) 클리어 뒤 처음 오면 첫 노드를 살 골드를 보장한다 (기획서: "첫 노드만 해금 가능한 재화").
        // 새로 열린 기능은 알림으로 알린다. 지금 이 씬에 버튼이 있는 기능만 (강화는 트리가 나타나는 것으로 안다)
        static readonly string[] NOTIFY_FEATS = { Unlock.CODEX, Unlock.WORLDTREE, Unlock.QUEST };

        // 새로 열린 기능 (알릴 것만) 을 돌려준다
        static System.Collections.Generic.List<string> Progress()
        {
            var shown = new System.Collections.Generic.List<string>();
            if (G == null) return shown;
            bool changed = false;
            if (G.tut == 0 && G.stage > 1)
            {
                G.tut = 1;
                G.gold = System.Math.Max(G.gold, NodeCost(NODE[ROOT_NODE], Lv(ROOT_NODE)).gold);
                changed = true;
            }
            var opened = Unlock.Newly();
            shown = opened.Where(k => NOTIFY_FEATS.Contains(k)).ToList();
            if (changed || opened.Count > 0) SaveGame();
            return shown;
        }

        // 새 기능은 버튼을 밝혀 안내한다 (UnlockGuide가 씬에 없으면 알림 문구만)
        void AnnounceUnlocks(System.Collections.Generic.List<string> feats)
        {
            if (feats.Count == 0) return;
            if (guide == null) { App.Notify(Loc.F("새 기능이 열렸어요: {0}", string.Join(" · ", feats.Select(Unlock.Name)))); return; }
            foreach (var k in feats)
            {
                var button = k == Unlock.CODEX ? dexButton : k == Unlock.WORLDTREE ? treeButton : k == Unlock.QUEST ? questButton : null;
                guide.Show(k, button != null ? button.transform : null);
            }
        }

        void ApplyUnlocks()
        {
            UIUtil.Show(tree, Unlock.On(Unlock.UPGRADE));
            UIUtil.Show(dexButton, Unlock.On(Unlock.CODEX));
            UIUtil.Show(treeButton, Unlock.On(Unlock.WORLDTREE));
            UIUtil.Show(questButton, Unlock.On(Unlock.QUEST));
            UIUtil.Show(bossButton, Unlock.On(Unlock.BOSS));
            RenderQuestButton();
            RenderBoss();
        }

        // ===== 보스 도전 =====
        // 임시 보스 버튼과 스테이지 패널 사이: 눈에 보이는 테두리끼리 6px.
        // 그림 가장자리의 투명한 여백(btn_go 아래 8px + panel_wood 위 6px)만큼 사각형끼리는 겹친다
        const float BOSS_GAP = 6, BOSS_SPRITE_PAD = 8 + 6;
        // 보스에게 지면 보스 도전이 꺼지고(그 스테이지에서 파밍) 이 버튼으로 다시 켠다. 켜짐이면 보스 스테이지에서 보스와 싸운다
        void WireBossButton(Scene scene)
        {
            var b = SceneUI.Find(scene, "Button_Boss");
            if (b == null && startButton != null)
            {
                // 임시: 출발 버튼을 복제해 바로 위에 둔다 (복제는 코드로 건 클릭을 가져가지 않는다)
                b = Instantiate(startButton.gameObject, startButton.parent).transform;
                b.name = "Button_Boss";
                b.SetSiblingIndex(startButton.GetSiblingIndex());
                var srt = (RectTransform)startButton; var brt = (RectTransform)b;
                // 출발 버튼이 놓인 스테이지 패널(부모)의 위쪽 가장자리에서 BOSS_GAP만큼 띄운다 (패널 안의 "N Stage" 글자와 겹치지 않게)
                var prt = (RectTransform)startButton.parent;
                float anchorY = Mathf.Lerp(prt.rect.yMin, prt.rect.yMax, brt.anchorMin.y);
                brt.anchoredPosition = new Vector2(srt.anchoredPosition.x, prt.rect.yMax + BOSS_GAP - BOSS_SPRITE_PAD + brt.rect.height * brt.pivot.y - anchorY);
                var lbl = SceneUI.TextIn(b);
                // 두 줄이 알약 모양 둥근 끝·아래 투명 여백 밖으로 나가지 않게: 좌우 60, 위 12·아래 20 비우고 출발 버튼 글자(40)보다 커지지 않게
                if (lbl != null)
                {
                    lbl.enableAutoSizing = true; lbl.fontSizeMin = Mathf.Min(lbl.fontSizeMin, 18); lbl.fontSizeMax = 40;
                    lbl.margin = new Vector4(60, 12, 60, 20); lbl.lineSpacing = -8;
                }
            }
            if (b == null) { Debug.LogWarning("[MainGame] 보스 도전 버튼(Button_Boss)을 만들 수 없어요"); return; }
            SceneUI.Bind(b, ToggleBoss);
            bossButton = b.gameObject;
            bossLabel = SceneUI.TextIn(b);
            bossFrame = b.GetComponent<Image>();
        }

        void ToggleBoss()
        {
            if (G == null) return;
            G.bossOn = !G.bossOn; SaveGame(); Snd.Ui();
            App.Notify(G.bossOn ? Loc.F("보스 도전 켬 · 스테이지 {0}에서 보스와 싸워요", NextBossStage(ContentNow())) : Loc.T("보스 도전 끔 · 보스 스테이지에서는 파밍만 해요"));
            RenderBoss();
        }

        void RenderBoss()
        {
            if (bossLabel == null || G == null) return;
            int bs = NextBossStage(ContentNow());   // 콘텐츠(표시) 스테이지
            var b = BossAt(bs);
            string where = b == null ? "" : bs == ContentNow() ? Loc.F("지금 스테이지 {0} · {1}", bs, b.n) : Loc.F("스테이지 {0} · {1}", bs, b.n);
            string state = G.bossOn ? "<color=#b8321e>ON</color>" : "<color=#2a5a9a>OFF</color>";
            UIUtil.SetText(bossLabel, $"{Loc.T("보스 도전 버튼")} {state}\n<size=70%>{where}</size>");
            if (bossFrame != null) bossFrame.color = G.bossOn ? U.Hex("#fff0e6") : U.Hex("#eef7ff");
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
            if (popup == langWin.Root) langWin.Refresh();
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
