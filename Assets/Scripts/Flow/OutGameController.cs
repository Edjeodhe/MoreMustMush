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
    //   Down_Panel(접힘) ↔ Down_hidden_Panel(펼침) = 누르면 서로 바뀐다. 펼침 안의 칸 = 꼬마 (합류 순서대로)
    //   Button_Group 안의 버튼 = 성장 · 꼬마 · 공방 · 꾸미기 탭 (공방 탭 = Workshop_Panel, 나머지 내용은 아직 없음)
    //   Workshop_Panel = 꼬마 공방 (WorkshopPanel 참고, 배치 전이면 건너뛴다)
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
        float tickT;

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
                    if (c.TryGetComponent<Image>(out var img)) kidSlots.Add(img);
                kidSlots.Sort((a, b) => Order(a.transform, b.transform, true));
                foreach (var s in kidSlots) kidBlank.Add(s.sprite);
            }

            var group = SceneUI.Need(scene, "Button_Group");
            if (group != null)
            {
                foreach (Transform c in group.transform)
                    if (c.TryGetComponent<Button>(out var b)) tabs.Add(b);
                tabs.Sort((a, b) => Order(a.transform, b.transform, false));   // 위 → 아래
                for (int i = 0; i < tabs.Count; i++) { int k = i; tabs[i].onClick.AddListener(() => { Snd.Ui(); tab = k; RenderTabs(); }); }
            }
            var ws = SceneUI.Find(scene, "Workshop_Panel");
            if (ws != null) workshop = new WorkshopPanel(ws.gameObject, () => currency.Tick());
            SetFolded(true, false);
        }

        void Start()
        {
            App.EnsureSave();
            if (farm == null) farm = FindFirstObjectByType<FarmView>();
            if (farm != null) farm.Enter();
            else Debug.LogWarning("[OutGame] FarmView가 씬에 없어요 (세계수 월드를 배치하면 연결돼요)");
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
            if (kb != null && kb.escapeKey.wasPressedThisFrame) ToMainGame();
        }

        void Render()
        {
            if (G == null) return;
            currency.Tick();
            RenderBag();
            RenderKids();
            RenderTabs();
        }

        void RenderBag()
        {
            if (G?.farm == null) return;
            var b = G.farm.bag;
            UIUtil.SetText(storeText, Loc.F("보관 {0} / {1}", Mmss((long)b.sec), Mmss((long)BagCapSec())) + (BagFull() ? " " + Loc.T("가득!") : ""));
            if (bag != null) bag.interactable = BagReady();
            UIUtil.SetText(bagText, Loc.F("수확 보따리 받기  <size=80%>{0}{1}</size>", UIUtil.Ic("gold"), U.Fmt(System.Math.Floor(b.gold))));
        }

        // 꼬마 칸: 합류한 꼬마를 합류 순서대로 앞 칸부터, 나머지는 배치한 빈칸 그림 그대로
        void RenderKids()
        {
            var owned = new List<string>();
            foreach (var id in CRITTER_JOIN) if (HasSpecial(id)) owned.Add(id);
            for (int i = 0; i < kidSlots.Count; i++)
            {
                var s = i < owned.Count ? SpriteDB.Get($"Characters/Critters/{owned[i]}_ref") : null;
                kidSlots[i].sprite = s != null ? s : kidBlank[i];
            }
        }

        // 임시 탭: 고른 탭만 눌린 모양으로 (탭 내용 창은 배치되면 연결)
        void RenderTabs()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].interactable = i != tab;
                if (i < TAB_NAMES.Length) UIUtil.SetText(SceneUI.TextIn(tabs[i].transform), Loc.T(TAB_NAMES[i]));
            }
            if (workshop != null) { UIUtil.Show(workshop.root, tab == TAB_CRAFT); workshop.Render(); }
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
            App.Notify(Loc.F("수확 보따리! 나무버섯 {0}개 · {1}+{2}", r.Value.n, UIUtil.Ic("gold"), U.Fmt(r.Value.gold)) + (r.Value.gem > 0 ? $" {UIUtil.Ic("gem")}+{r.Value.gem}" : ""));
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
