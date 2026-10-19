using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // OutGame 씬 (세계수). 사람이 배치한 캔버스에 기능만 잇는다 — UI를 코드로 만들지 않는다.
    // 이름으로 찾는 것:
    //   UP_Panel/Gold·수확석 안의 글자 = 골드·균사석 · UP_Panel/Text (TMP) (1) = 보따리 보관 시간
    //   Button_Bag = 수확 보따리 받기 (쌓인 게 있을 때만 눌린다)
    //   Down_Panel(접힘) ↔ Down_hidden_Panel(펼침) = 누르면 서로 바뀐다. 펼침 안의 칸 = 정령 (CRITTER_JOIN 순서, 칸 하나 = 정령 하나)
    //     있는 정령 = 그림 · 열렸지만 아직 안 데려온 정령 = 검은 실루엣 · 안 열린 정령 = 배치한 빈칸 그림.
    //     누르면 (정령 창이 배치되기 전 임시): 안 데려온 정령 = 균사석으로 데려오기, 있는 정령 = 골드로 Lv 업, Lv 상한이면 균사석으로 진화(각성).
    //     균사석을 쓰는 일은 한 번 눌러 값을 보여 주고, 3초 안에 다시 누르면 한다
    //   Button_Group 안의 버튼 = 성장 · 꼬마 · 공방 · 꾸미기 탭 → 세계수 탭 창(FarmTabWindow, 배치된 FarmTabPanel 또는 임시 창). 같은 탭을 다시 누르면 닫힘
    //   Workshop_Panel = 꼬마 공방 (WorkshopPanel 참고). 배치돼 있으면 공방 탭은 세계수 탭 창 대신 이 창을 연다
    //   Button_Back = MainGame으로 돌아가기. 배치돼 있지 않으면 왼쪽 아래에 임시 나무 화살표 팻말(UI/sign_arrow)을 만든다
    // 세계수 월드(FarmView)는 씬에 있으면 연다. Esc = MainGame으로.
    public class OutGameController : MonoBehaviour
    {
        [Tooltip("세계수 월드 뷰. 비우면 씬에서 찾는다")]
        public FarmView farm;

        const float TICK = 1;   // 보따리·보관 시간 갱신 간격 (초)
        const int TAB_CRAFT = 2;   // TAB_NAMES의 공방
        static readonly string[] TAB_NAMES = { "성장", "꼬마", "공방", "꾸미기" };

        CurrencyBar currency;
        WorkshopPanel workshop;
        TMP_Text storeText, bagText;
        Button bag;
        GameObject fold, unfold;           // Down_Panel · Down_hidden_Panel
        readonly List<Image> kidSlots = new List<Image>();
        readonly List<Sprite> kidBlank = new List<Sprite>();   // 배치한 빈칸 그림 (아직 없는 꼬마)
        readonly List<Button> tabs = new List<Button>();
        int tab;
        FarmTabWindow tabWin;
        int armed = -1; float armedT;      // 균사석을 쓰기 전 확인: 한 번 누른 칸과 그 시각
        const float ARM_SEC = 3;
        static readonly Color SILHOUETTE = new Color(0.08f, 0.09f, 0.14f, 0.85f);
        OptionWindow optionWin;   // Esc = 옵션 창 (돌아가기 버튼 포함). MainGame으로 돌아가는 버튼이 배치되기 전까지 여기서 돌아간다
        float tickT;
        TMP_Text backLabel;       // 돌아가기 팻말 글자 (언어가 바뀌면 다시 쓴다)

        void Awake()
        {
            var scene = gameObject.scene;
            var up = SceneUI.Need(scene, "UP_Panel");
            currency = new CurrencyBar(SceneUI.TextIn(SceneUI.Need(up?.transform, "Gold")), SceneUI.TextIn(SceneUI.Need(up?.transform, "수확석")));
            storeText = up != null ? up.transform.Find("Text (TMP) (1)")?.GetComponent<TMP_Text>() : null;   // Gold·수확석 안의 같은 이름 글자와 헷갈리지 않게 바로 아래 것만

            var bagT = SceneUI.Need(scene, "Button_Bag")?.transform;
            bag = SceneUI.Bind(bagT, ClaimBag);
            bagText = SceneUI.TextIn(bagT);

            fold = SceneUI.Need(scene, "Down_Panel");
            unfold = SceneUI.Need(scene, "Down_hidden_Panel");
            Clickable(fold, () => SetFolded(false));
            Clickable(unfold, () => SetFolded(true));
            if (unfold != null)
            {
                foreach (Transform c in unfold.transform)
                    if (c.TryGetComponent<Image>(out var img)) { img.preserveAspect = true; kidSlots.Add(img); }   // 정령 그림은 가로세로 비가 제각각(0.6~1.2) → 칸에 늘리지 않고 비율대로
                kidSlots.Sort((a, b) => Order(a.transform, b.transform, true));
                foreach (var s in kidSlots) kidBlank.Add(s.sprite);
                for (int i = 0; i < kidSlots.Count; i++) { int k = i; Clickable(kidSlots[i].gameObject, () => KidSlot(k)); }
                FoldButton(unfold.transform);   // 칸을 다 모은 뒤에 (닫기 버튼이 정령 칸으로 세어지지 않게)
            }

            var group = SceneUI.Need(scene, "Button_Group");
            if (group != null)
            {
                foreach (Transform c in group.transform)
                    if (c.TryGetComponent<Button>(out var b)) tabs.Add(b);
                tabs.Sort((a, b) => Order(a.transform, b.transform, false));   // 위 → 아래
                for (int i = 0; i < tabs.Count; i++) { int k = i; tabs[i].onClick.AddListener(() => OpenTab(k)); }
            }
            var ws = SceneUI.Find(scene, "Workshop_Panel");
            if (ws != null) { workshop = new WorkshopPanel(ws.gameObject, () => currency.Tick()); UIUtil.Show(workshop.root, false); }
            SetFolded(true, false);
            tabWin = FarmTabWindow.For(scene, null, () => { currency.Tick(); RenderBag(); RenderKids(); RenderTabs(); });
            optionWin = OptionWindow.For(scene, () => { Snd.Ui(); optionWin.Close(); },
                new OptionWindow.Extra("Button_Back", "돌아가기", ToMainGame));
            BackButton(scene);
        }

        // 화면에 보이는 돌아가기 버튼. 배치된 Button_Back(옵션 창 밖)이 있으면 그것을, 없으면 왼쪽 아래 나무 화살표 팻말을 만든다
        void BackButton(UnityEngine.SceneManagement.Scene scene)
        {
            var canvas = TempDialog.RootCanvas(scene);
            if (canvas == null) return;
            foreach (var b in canvas.GetComponentsInChildren<Button>(true))
                if (b.name == "Button_Back" && (optionWin?.Root == null || !b.transform.IsChildOf(optionWin.Root.transform)))
                { SceneUI.Bind(b.transform, ToMainGame); backLabel = SceneUI.TextIn(b.transform); return; }

            var go = new GameObject("Button_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.SetSiblingIndex(0);   // 창·팝업보다 뒤 (열린 창이 덮는다)
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(36, 32); rt.sizeDelta = new Vector2(250, 168);   // sign_arrow 220×148 비율
            // 팻말 그림은 왼쪽을 가리키게 뒤집은 자식에 (글자는 뒤집지 않는다)
            go.GetComponent<Image>().color = Color.clear;
            var sign = new GameObject("Sign", typeof(RectTransform), typeof(Image));
            var sr = (RectTransform)sign.transform; sr.SetParent(rt, false); TempDialog.Stretch(sr);
            sr.localScale = new Vector3(-1, 1, 1);
            var si = sign.GetComponent<Image>(); TempDialog.Skin(si, "sign_arrow", new Color(0.55f, 0.36f, 0.2f)); si.type = Image.Type.Simple; si.raycastTarget = false;
            var btn = go.GetComponent<Button>(); btn.targetGraphic = si;
            btn.onClick.AddListener(ToMainGame);
            // 글자는 화살표 머리(왼쪽)를 빼고 판자 몸통 위에. 진한 나무 위 크림 글자 (대비 5:1 이상)
            backLabel = TempDialog.MakeText(rt, "Label", Loc.T("돌아가기"), Vector2.zero, Vector2.zero, 34, true);
            var lr = backLabel.rectTransform; lr.offsetMin = new Vector2(76, 46); lr.offsetMax = new Vector2(-22, -40);
            backLabel.color = TempDialog.CREAM; backLabel.fontStyle = FontStyles.Bold;
            backLabel.enableAutoSizing = true; backLabel.fontSizeMin = 18; backLabel.fontSizeMax = 34;
            backLabel.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void Start()
        {
            App.EnsureSave();
            if (farm == null) farm = FindFirstObjectByType<FarmView>();
            if (farm != null) farm.Enter();
            else Debug.LogWarning("[OutGame] FarmView가 씬에 없어요 (세계수 월드를 배치하면 연결돼요)");
            tabWin?.SetFarm(farm);
            Render();
        }

        void OnEnable() => Loc.Changed += Render;
        void OnDisable() => Loc.Changed -= Render;

        // 떠날 때까지의 수확을 보따리에 넣고 저장
        void OnDestroy() { if (farm != null) farm.Flush(); }

        void Update()
        {
            if (currency.Tick()) workshop?.Render();   // 재화가 바뀌면 제작 가능 여부도
            if ((tickT -= Time.unscaledDeltaTime) <= 0) { tickT = TICK; BagSettle(); RenderBag(); workshop?.Tick(); }
            var kb = Keyboard.current;
            tabWin?.Tick();
            // Esc: 열린 탭 창 → 닫기, 옵션 창 → 닫기, 아무것도 없으면 업그레이드 화면(MainGame)으로
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (TabOpen()) { Snd.Ui(); tabWin?.Close(); UIUtil.Show(workshop?.root, false); RenderTabs(); }
                else if (optionWin.IsOpen) optionWin.Toggle();
                else ToMainGame();
            }
        }

        void Render()
        {
            if (G == null) return;
            currency.Tick();
            RenderBag();
            RenderKids();
            RenderTabs();
            tabWin?.Refresh();
            UIUtil.SetText(backLabel, Loc.T("돌아가기"));
        }

        void RenderBag()
        {
            if (G?.farm == null) return;
            var b = G.farm.bag;
            UIUtil.SetText(storeText, Loc.F("보관 {0} / {1}", Mmss((long)b.sec), Mmss((long)BagCapSec())) + (BagFull() ? " " + Loc.T("가득!") : ""));
            if (bag != null) bag.interactable = BagReady();
            UIUtil.SetText(bagText, Loc.F("수확 보따리 받기  <size=80%>{0}{1}</size>", UIUtil.Ic("gold"), U.Fmt(System.Math.Floor(b.gold))));
        }

        // 정령 칸: CRITTER_JOIN 순서로 칸 하나에 정령 하나. 있는 정령 = 그림 · 열린 정령 = 실루엣 · 안 열린 정령 = 빈칸 그림
        void RenderKids()
        {
            for (int i = 0; i < kidSlots.Count; i++)
            {
                string id = i < CRITTER_JOIN.Length ? CRITTER_JOIN[i] : null;
                bool shown = id != null && (HasSpecial(id) || KidOpen(id));
                var s = shown ? UIUtil.KidPortrait(id) : null;
                kidSlots[i].sprite = s != null ? s : kidBlank[i];
                kidSlots[i].color = s != null && !HasSpecial(id) ? SILHOUETTE : Color.white;
            }
        }

        // 정령 칸을 눌렀을 때 (정령 창이 배치되기 전 임시 동작)
        void KidSlot(int i)
        {
            if (G?.farm == null || i >= CRITTER_JOIN.Length) return;
            string id = CRITTER_JOIN[i], name = SPC[id].n;
            bool confirm = armed == i && Time.unscaledTime - armedT < ARM_SEC;
            armed = -1;
            if (!HasSpecial(id))
            {
                switch (CanUnlockKid(id))
                {
                    case KidFail.Stage: Snd.Err(); App.Notify(Loc.F("세계수가 {0}단계({1})가 되면 열려요", JoinLv(id), WT.StageName(JoinLv(id)))); return;
                    case KidFail.Gem: Snd.Err(); App.Notify(Loc.F("{0}을(를) 데려오려면 {1}{2}이 필요해요 (지금 {3})", name, UIUtil.Ic("gem"), UnlockGem(id), G.gem)); return;
                    case KidFail.None:
                        if (!confirm) { Arm(i); App.Notify(Loc.F("한 번 더 누르면 {0}{1}으로 {2}을(를) 데려와요", UIUtil.Ic("gem"), UnlockGem(id), name)); return; }
                        UnlockKid(id);
                        Snd.Record(); App.Notify(Loc.F("{0}이(가) 세계수에 왔어요!", name));
                        break;
                    default: return;
                }
            }
            else if (CanKidLvUp(id) != KidFail.Cap && KLv(id) < KLV.max)
            {
                if (CanKidLvUp(id) == KidFail.Gold) { Snd.Err(); App.Notify(Loc.F("Lv 업에 {0}{1}이 필요해요", UIUtil.Ic("gold"), U.Fmt(KLvCost(id)))); return; }
                KidLvUp(id);
                Snd.Ui(); App.Notify(Loc.F("{0} Lv.{1}", name, KLv(id)));
            }
            else
            {
                int a = Awaken(id);
                switch (CanAwaken(id))
                {
                    case KidFail.Max: App.Notify(Loc.F("{0}은(는) 마지막 진화까지 마쳤어요 {1}", name, new string('★', a))); return;
                    case KidFail.Stage: Snd.Err(); App.Notify(Loc.F("세계수가 {0}단계({1})가 되면 ★{2} 진화가 열려요", AwakenStage(id), WT.StageName(AwakenStage(id)), a + 1)); return;
                    case KidFail.Lv: Snd.Err(); App.Notify(Loc.F("Lv.{0}이 되면 진화할 수 있어요 (지금 Lv.{1})", 10 * (a + 1), KLv(id))); return;
                    case KidFail.Gem: Snd.Err(); App.Notify(Loc.F("★{0} 진화에 {1}{2}이 필요해요 (지금 {3})", a + 1, UIUtil.Ic("gem"), KLV.awakenGem[a], G.gem)); return;
                    case KidFail.None:
                        if (!confirm) { Arm(i); App.Notify(Loc.F("한 번 더 누르면 {0}{1}으로 ★{2} 진화해요", UIUtil.Ic("gem"), KLV.awakenGem[a], a + 1)); return; }
                        KidAwaken(id);
                        Snd.Record(); App.Notify(Loc.F("{0} 진화! {1}", name, new string('★', a + 1)));
                        break;
                    default: return;
                }
            }
            if (farm != null) farm.Sync(false);
            currency.Tick();
            RenderKids();
        }

        void Arm(int i) { armed = i; armedT = Time.unscaledTime; Snd.Ui(); }

        // 탭 버튼: 세계수 탭 창을 연다 (열린 탭을 다시 누르면 닫는다)
        void OpenTab(int k)
        {
            Snd.Ui();
            bool wasOpen = TabOpen() && tab == k;
            tabWin?.Close();
            UIUtil.Show(workshop?.root, false);
            if (!wasOpen)
            {
                tab = k;
                if (k == TAB_CRAFT && workshop != null) UIUtil.Show(workshop.root, true);
                else tabWin?.Open((FarmTabWindow.Tab)Mathf.Clamp(k, 0, 3));
            }
            RenderTabs();
        }

        // 열린 탭만 눌린 모양으로
        void RenderTabs()
        {
            bool open = TabOpen();
            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].interactable = !(open && i == tab);
                if (i < TAB_NAMES.Length) UIUtil.SetText(SceneUI.TextIn(tabs[i].transform), Loc.T(TAB_NAMES[i]));
            }
            workshop?.Render();
        }

        bool TabOpen() => (tabWin != null && tabWin.IsOpen) || (workshop != null && workshop.IsOpen);

        // 펼친 정령 목록의 닫기 버튼. 배치된 Button_Close가 있으면 그것을, 없으면 오른쪽 위 모서리에 걸친 btn_close를 만든다
        // (목록 바탕을 눌러도 접히지만 보이는 닫기가 없어 닫는 법을 알기 어려웠다)
        void FoldButton(Transform panel)
        {
            var placed = SceneUI.Find(panel, "Button_Close");
            if (placed != null) { SceneUI.Bind(placed, () => SetFolded(true)); return; }
            var go = new GameObject("Button_Close", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(panel, false);
            rt.SetAsLastSibling();
            rt.anchorMin = rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-14, -14); rt.sizeDelta = new Vector2(80, 77);   // btn_close 96×92 비율
            var img = go.GetComponent<Image>(); TempDialog.Skin(img, "btn_close", new Color(0.9f, 0.4f, 0.35f)); img.type = Image.Type.Simple;
            var btn = go.GetComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(() => SetFolded(true));
        }

        void SetFolded(bool folded, bool sound = true)
        {
            if (sound) Snd.Ui();
            UIUtil.Show(fold, folded);
            UIUtil.Show(unfold, !folded);
        }

        void ClaimBag()
        {
            var r = BagClaim();
            if (r == null) { Snd.Err(); App.Notify(Loc.T("보따리가 비어 있어요. 꼬마를 구역에 보내 나무버섯을 따게 해요")); return; }
            Snd.Record();
            if (farm != null) farm.BagBurst(r.Value.gold, r.Value.gem);
            App.Notify(ReturnPopup.ClaimText(r.Value));
            currency.Tick();   // BagClaim이 이미 저장했다
            RenderBag();
        }

        // 화면에 놓인 순서: 가로면 왼쪽 → 오른쪽, 세로면 위 → 아래. 같은 자리면 하이어라키 순서
        static int Order(Transform a, Transform b, bool horizontal)
        {
            var pa = ((RectTransform)a).anchoredPosition; var pb = ((RectTransform)b).anchoredPosition;
            float d = horizontal ? pa.x - pb.x : pb.y - pa.y;
            return Mathf.Abs(d) > 1 ? (d > 0 ? 1 : -1) : a.GetSiblingIndex().CompareTo(b.GetSiblingIndex());
        }

        // 배치한 패널은 버튼이 아니라서 누름만 받게 버튼 컴포넌트를 붙인다 (모양은 그대로)
        static void Clickable(GameObject go, UnityEngine.Events.UnityAction action)
        {
            if (go == null) return;
            if (!go.TryGetComponent<Button>(out var b)) { b = go.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.navigation = new Navigation { mode = Navigation.Mode.None }; }
            b.onClick.AddListener(action);
        }

        public void ToMainGame() { Snd.Ui(); App.Go(Scenes.MainGame); }
        public void StartRound() { Snd.Ui(); RoundFlow.Request(); }
    }
}
