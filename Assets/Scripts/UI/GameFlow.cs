using System.Linq;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Screen flow and button actions (prototype setScreen / onAction / beginRound / finishRound / tax).
    // Phase 1: title → (pre-round) → round → settlement → tax bill → mycelium tree → codex.
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow I;

        [Header("Screens")]
        public TitleScreen title;
        public TreeScreen tree;
        public RoundController round;
        public GameObject roundUI;              // HUD, combo and field texts on the canvas
        public SettleScreen settle;
        public TaxScreen tax;
        public GameObject titleBackdrop;        // world-space town + grandpa behind title/settle/tax
        public FarmScreen farmScreen;
        public FarmView farm;                   // world-space farm (ranch with the field inside)

        [Header("Modal")]
        public ModalHost modal;
        public CodexPanel codex;
        public PreRoundPanel preRound;
        public GameObject pausePanel;
        public RecordsPanel records;
        public BulkPanel bulk;
        public ShopPanel shop;
        public WorkshopPanel workshop;
        public SkinPanel skins;
        public BuildPanel build;
        public AutoPanel auto;
        public AccelPanel accel;
        public PetCardPanel petCard;
        public GameObject resetPanel;
        public DebugPanel debug;
        public Toast toast;

        public string Screen { get; private set; } = "title";
        public bool ModalOpen => modal != null && modal.IsOpen;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
        }

        void Start() => RenderTitle();

        void Update()
        {
            if (G != null && Screen != "title") G.play += Time.unscaledDeltaTime;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.backquoteKey.wasPressedThisFrame && debug != null) debug.Toggle();
            if (kb.mKey.wasPressedThisFrame) { Snd.muted = !Snd.muted; ShowToast(Snd.muted ? "소리 끔" : "소리 켬"); }
            if (Screen != "round" && kb.escapeKey.wasPressedThisFrame) { if (ModalOpen) CloseModal(); else if (Screen == "farm" && farm.Placing) { farm.CancelPlace(); farmScreen.Render(); } }
        }

        void OnApplicationPause(bool paused) { if (paused && G != null && Screen != "title") SaveGame(); }
        void OnApplicationQuit() { if (G != null) SaveGame(); }

        // ===== screens =====
        public void SetScreen(string s)
        {
            if (Screen == "farm" && s != "farm") farm.Flush();   // 밭일 대기열은 떠날 때 바로 끝낸다 (건설은 실제 시간으로 계속)
            Screen = s;
            UIUtil.Show(title, s == "title");
            UIUtil.Show(tree, s == "tree");
            UIUtil.Show(round, s == "round");
            UIUtil.Show(roundUI, s == "round");
            UIUtil.Show(settle, s == "settle");
            UIUtil.Show(tax, s == "tax");
            UIUtil.Show(titleBackdrop, s == "title" || s == "settle" || s == "tax");
            UIUtil.Show(farmScreen, s == "farm");
            UIUtil.Show(farm, s == "farm");
            if (tree != null) tree.SetActiveWorld(s == "tree");
        }

        public void RenderTitle() { SetScreen("title"); title.Show(SaveIO.HasSave()); }

        public void EnterTree()
        {
            SetScreen("tree");
            CloseModal();
            tree.Refresh();
        }

        public void EnterFarm(string view)
        {
            CloseModal();
            SetScreen("farm");
            farm.Enter();
            farmScreen.Render();
        }

        void OpenPetCard(string id)
        {
            petCard.id = id;
            OpenModal(petCard, () => { if (Screen == "farm") farmScreen.Render(); });
            petCard.Render();
        }

        void FarmEvolve(string id)
        {
            var k = SPC[id];
            string before = EvoName(k);
            if (!Game.FarmEvolve(id)) { Snd.Err(); ShowToast($"호감도 {FARM.hearts}칸을 모두 채우면 진화할 수 있어요"); return; }
            Snd.Record();
            farm.Evolved(id);
            ShowToast($"{U.Iga(before)} {EvoName(k)}(으)로 진화했어요! 부탁 보상 ×{U.FmtN(FARM.evoGem[EvoOf(id)])}");
            CloseModal(); farmScreen.Render();
        }

        public void OpenAccel(string kind, int uid)
        {
            accel.kind = kind; accel.uid = uid;
            Snd.Ui();
            OpenModal(accel, () => { if (Screen == "farm") farmScreen.Render(); });
            accel.Render();
        }

        public void ShowToast(string msg) { if (toast != null) toast.Show(msg); }

        // ===== modal =====
        public void OpenModal(Component panel, System.Action onClose = null) => modal.Open(panel.gameObject, onClose);
        public void OpenModal(GameObject panel, System.Action onClose = null) => modal.Open(panel, onClose);
        public void CloseModal() { if (modal != null) modal.Close(); }

        public void OpenPause()
        {
            var R = RoundSim.R;
            if (R == null || R.phase == "end") return;
            R.paused = true;
            OpenModal(pausePanel, () => { if (RoundSim.R != null) RoundSim.R.paused = false; });
        }

        // ===== shop =====
        public void OpenShop()
        {
            string from = Screen;
            shop.ResetQty();
            OpenModal(shop, () =>
            {
                shop.ResetQty();
                if (from == "tax" && Screen == "tax") tax.ShowBill();
                else if (Screen == "tree") tree.Refresh();
                else if (Screen == "farm") farmScreen.Render();
            });
            shop.Render();
        }

        void Sell(System.Collections.Generic.IEnumerable<(string id, double cnt)> ids)
        {
            var r = SellMush(ids);
            if (r.n == 0) { Snd.Err(); return; }
            foreach (var (id, _) in ids) shop.qty.Remove(id);
            Snd.Buy(); ShowToast($"버섯 {U.Fmt(r.n)}개 판매 +{U.Fmt(r.gold)}골드{(r.debt > 0 ? $" (체납 {U.Fmt(r.debt)} 차감)" : "")}");
            shop.Render();
        }

        // ===== round =====
        public void OpenPreRound()
        {
            if (!THEME.TryGetValue(G.theme, out var th) || !ThemeOpen(th)) G.theme = LatestTheme().id;
            if (THEMES.Count(ThemeOpen) <= 1) { BeginRound(); return; }
            OpenModal(preRound);
            preRound.Render();
        }

        public void BeginRound()
        {
            string w = RollWeather();
            if (!THEME.TryGetValue(G.theme, out var th) || !ThemeOpen(th)) G.theme = LatestTheme().id;
            CloseModal();
            SetScreen("round");
            round.Begin(w, G.theme);
            SaveGame();
        }

        public void FinishRound()
        {
            var sum = RoundSim.R.Finish();
            SetScreen("settle");
            settle.Show(sum);
        }

        public void AfterSettle()
        {
            if (G.tax.roundsIn >= TAX.every) { SetScreen("tax"); tax.ShowBill(); return; }
            AfterTax();
        }

        public void AfterTax()
        {
            if (CodexCount() >= SP_TOTAL && !G.ending) { G.ending = true; SaveGame(); ShowToast($"버섯 도감 {SP_TOTAL}종 완성! (엔딩 컷신은 3단계에서 추가돼요)"); }
            EnterTree();
        }

        void NextTaxCycle()
        {
            G.tax.cycle++; G.tax.roundsIn = 0; G.tax.income = 0;
            G.quests.Clear();   // 마을 의뢰는 세금 사이클마다 새로
            SaveGame();
        }

        void TaxPay()
        {
            double bill = TaxBill();
            if (G.gold < bill) { Snd.Err(); ShowToast("골드가 모자라요"); return; }
            var head = (G.tax.cycle, G.tax.income);
            G.gold -= bill; G.tax.paid++;
            var unl = CheckUnlocks();
            NextTaxCycle();
            Snd.Record();
            tax.ShowPaid(head, unl);
        }

        void TaxSkip()
        {
            var T = G.tax; double rate = TaxRate(T.unpaid), penalty = System.Math.Ceiling(T.income * rate);
            var head = (T.cycle, T.income);
            double take = System.Math.Min(G.gold, penalty);
            G.gold -= take; T.debt += penalty - take; T.unpaid++;
            NextTaxCycle();
            Snd.Err();
            tax.ShowSkipped(head, rate, penalty, take);
        }

        // ===== actions (prototype onAction) =====
        public void OnAction(string act, string arg, Component src)
        {
            switch (act)
            {
                case "close": Snd.Ui(); CloseModal(); break;
                case "newgame": G = new SaveData(); AutoEnsure(); SaveGame(); Snd.Ui(); EnterTree(); ShowToast($"균사 트리에서 강화를 사거나 바로 수확하러 가 보세요! 세금은 {TAX.every}라운드마다 나와요"); break;
                case "continue": G = SaveIO.Load() ?? new SaveData(); AutoEnsure(); Snd.Ui(); EnterTree(); break;
                case "reset": OpenModal(resetPanel); break;
                case "reset-yes": SaveIO.Wipe(); G = null; CloseModal(); RenderTitle(); ShowToast("초기화했어요"); break;
                case "title": SaveGame(); CloseModal(); RenderTitle(); break;
                case "records": Snd.Ui(); OpenModal(records); records.Render(); break;
                case "codex":
                    Snd.Ui(); codex.Open(Screen == "round");
                    if (Screen == "round" && RoundSim.R != null) RoundSim.R.paused = true;
                    break;
                case "codexsel": Snd.Ui(); codex.Select(arg); break;
                case "bulk": Snd.Ui(); OpenModal(bulk, () => tree.Refresh()); bulk.Render(); break;
                case "bulktab": Snd.Ui(); bulk.SetBranch(arg); break;
                case "bulkkeep": bulk.ToggleKeep(); break;
                case "bulkgo": bulk.Go(); break;
                case "go": Snd.Ui(); OpenPreRound(); break;
                case "pretheme": Snd.Ui(); G.theme = arg; SaveGame(); preRound.Render(); break;
                case "startround": BeginRound(); break;
                case "resume": CloseModal(); break;
                case "endnow": CloseModal(); if (RoundSim.R != null) { var R = RoundSim.R; R.timeLeft = 0; R.phase = "end"; R.endT = 0.01f; } break;
                case "mute": Snd.muted = !Snd.muted; break;
                case "pause": OpenPause(); break;
                case "speed": RoundController.NextSpeed(); Snd.Ui(); break;
                case "totree": Snd.Ui(); AfterSettle(); break;
                case "taxpay": TaxPay(); break;
                case "taxskip": TaxSkip(); break;
                case "aftertax": Snd.Ui(); AfterTax(); break;
                case "shop": Snd.Ui(); OpenShop(); break;
                case "sporeshop": Snd.Ui(); shop.mode = "spore"; OpenShop(); break;
                case "shopmode": Snd.Ui(); shop.mode = arg; shop.Render(); break;
                case "shoptab": Snd.Ui(); shop.tab = arg; shop.Render(); break;
                case "shopq": { var p = arg.Split('|'); shop.StepQty(p[0], double.Parse(p[1])); break; }
                case "shopset": { var p = arg.Split('|'); shop.SetQtyFrac(p[0], double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)); break; }
                case "shopsell": Sell(new[] { (arg, shop.Qty(arg)) }); break;
                case "shopsellall": Sell(ShopList(shop.tab).Select(sp => (sp.id, InvCount(sp.id)))); break;
                case "quest":
                {
                    var r = DoQuest(int.Parse(arg));
                    if (r == null) { Snd.Err(); break; }
                    Snd.Record(); ShowToast($"의뢰 완료! +{U.Fmt(r.Value.gold)}골드{(r.Value.debt > 0 ? $" (체납 {U.Fmt(r.Value.debt)} 차감)" : "")}"); shop.Render();
                    break;
                }
                case "questall":
                {
                    var r = QuestAll();
                    if (r.n == 0) { Snd.Err(); ShowToast("지금 전달할 수 있는 의뢰가 없어요"); break; }
                    Snd.Record(); ShowToast($"의뢰 {r.n}개 완료! +{U.Fmt(r.gold)}골드{(r.debt > 0 ? $" (체납 {U.Fmt(r.debt)} 차감)" : "")}"); shop.Render();
                    break;
                }
                case "buyspore":
                {
                    double n = arg == "max" ? SporeMax() : double.Parse(arg);
                    if (!BuySpore(n)) { Snd.Err(); ShowToast("골드가 부족해요"); break; }
                    Snd.Buy(); ShowToast($"{UIUtil.Ic("spore")} 버섯 포자 {U.Fmt(n)}개 구입 (−{U.Fmt(n * SporePrice())}골드)"); shop.Render();
                    break;
                }
                case "workshop": Snd.Ui(); OpenModal(workshop, () => tree.Refresh()); workshop.Render(); break;
                case "hvunlock":
                    if (!HvUnlock(arg)) { Snd.Err(); break; }
                    Snd.Buy(); ShowToast($"{HV[arg].n} 해금! 활성화되어 무작위로 나와요"); workshop.Render(); break;
                case "hvlevel":
                    if (!HvLevel(arg)) { Snd.Err(); break; }
                    Snd.Buy(); ShowToast($"{HV[arg].n} {U.Stars(HvStar(arg))}{(HvStar(arg) >= 5 ? " 최대! 날이 늘었어요" : "")}"); workshop.Render(); break;
                case "hvtoggle":
                    if (!HvToggle(arg)) { Snd.Err(); ShowToast("수확기는 최소 1개는 켜져 있어야 해요"); break; }
                    Snd.Ui(); workshop.Render(); break;
                case "skinshop": Snd.Ui(); OpenModal(skins, () => { if (Screen == "farm") farmScreen.Render(); else if (Screen == "tree") tree.Refresh(); }); skins.Render(); break;
                case "skintab": Snd.Ui(); skins.tab = arg; skins.Render(); break;
                case "buyskin":
                    if (!BuySkin(arg)) { Snd.Err(); ShowToast("다이아몬드가 부족해요"); break; }
                    Snd.Record(); ShowToast($"{SKIN[arg].n} 스킨을 샀어요!"); skins.Render(); break;
                case "wearskin":
                {
                    var p = arg.Split('|');
                    WearSkin(p[0]); Snd.Ui();
                    if (p.Length > 1) petCard.Render(); else skins.Render();
                    break;
                }
                // ===== 버섯 농장 =====
                case "farm": Snd.Ui(); EnterFarm(null); break;
                case "totree2": Snd.Ui(); EnterTree(); break;
                case "farmall":
                {
                    var r = farm.CareAll();
                    if (r.n == 0) { Snd.Err(); ShowToast("지금 부탁하는 꼬마가 없어요"); break; }
                    Snd.Record(); ShowToast($"꼬마 {r.n}마리를 돌봤어요! 균사석 +{U.Fmt(r.gem)}{(r.bonus > 0 ? $" (호감도 보너스 +{r.bonus})" : "")}{(r.dia > 0 ? $" · 다이아몬드 +{r.dia}" : "")}"); farmScreen.Render();
                    break;
                }
                case "farmcard": Snd.Ui(); OpenPetCard(arg); break;
                case "evolve": FarmEvolve(arg); break;
                case "starup":
                    if (!StarUp(arg)) { Snd.Err(); ShowToast("골드나 균사석이 부족해요"); break; }
                    Snd.Record(); farm.StarredUp(arg); ShowToast($"{EvoName(SPC[arg])} ★{CStarOf(arg)}! {FxText(arg)}"); petCard.Render(); break;
                // ===== 버섯 나무 =====
                case "treepanel": Snd.Ui(); farmScreen.treeOpen = arg == "open" || !farmScreen.treeOpen; farmScreen.Render(); break;
                case "treeup":
                    if (!TreeUp()) { Snd.Err(); ShowToast("골드가 부족해요"); break; }
                    Snd.Record(); farm.TreeLeveled();
                    ShowToast($"버섯 나무 Lv.{TreeLv()}! 버섯 자리 {TREE.Slots(TreeLv())}개 · 수확량 ×{U.FmtN(TreeYieldMul())}"); farmScreen.Render(); break;
                case "pickall":
                {
                    int n = farm.PickAll();
                    if (n == 0) { Snd.Err(); ShowToast(farm.Workers == 0 && farm.crits.Count > 0 ? "꼬마들이 모두 건물을 짓고 있어요" : farm.crits.Count == 0 ? "특수 버섯(꼬마)을 잡으면 버섯을 따 줘요" : "다 자란 버섯이 없어요"); break; }
                    Snd.Ui(); ShowToast($"꼬마들이 버섯 {n}개를 따러 가요!"); farmScreen.Render();
                    break;
                }
                case "treeaccel": OpenAccel("tree", 0); break;
                case "accelok":
                {
                    bool ok = accel.kind == "build" ? BuildAccel(accel.uid) : TreeAccel();
                    if (!ok) { Snd.Err(); ShowToast("균사석이 부족해요"); break; }
                    Snd.Record(); ShowToast(accel.kind == "build" ? "건설 완료!" : "버섯이 모두 다 자랐어요!");
                    CloseModal(); farmScreen.Render();
                    break;
                }
                // ===== 건축 =====
                case "buildshop":
                    Snd.Ui();
                    if (Screen != "farm") { EnterFarm(null); }
                    OpenModal(build, () => farmScreen.Render()); build.Render(); break;
                case "build":
                {
                    var fail = CanBuild(arg);
                    if (fail != BuildFail.None)
                    {
                        Snd.Err();
                        ShowToast(fail == BuildFail.Max ? "더 지을 수 없어요 (최대 개수)" : fail == BuildFail.Dia ? "다이아몬드가 부족해요" : "쉬고 있는 꼬마가 없어요 (모두 건설 중)");
                        break;
                    }
                    if (!farm.BeginPlace(arg)) { Snd.Err(); ShowToast("목장에 놓을 자리가 없어요"); break; }
                    Snd.Ui(); CloseModal(); farmScreen.Render();
                    break;
                }
                case "placeok":
                {
                    var b = farm.ConfirmPlace();
                    if (b == null) { Snd.Err(); ShowToast(farm.Placing && !farm.PlaceOk ? "여기에는 설치할 수 없어요" : "지을 수 없어요"); break; }
                    Snd.Record();
                    ShowToast($"{U.Iga(EvoName(SPC[b.critter]))} {BUILDING[b.id].n}을(를) 짓기 시작했어요! ({BuildLeftText(b)})");
                    farmScreen.Render();
                    break;
                }
                case "placecancel": Snd.Ui(); farm.CancelPlace(); farmScreen.Render(); break;
                // ===== 자동 수확 보상 =====
                case "autoharvest": Snd.Ui(); AutoEnsure(); OpenModal(auto, () => { if (Screen == "tree") tree.Refresh(); else if (Screen == "farm") farmScreen.Render(); }); auto.Render(); break;
                case "claimauto":
                {
                    var r = ClaimAuto();
                    if (r == null) { Snd.Err(); break; }
                    Snd.Record();
                    ShowToast($"자동 수확 보상! {UIUtil.Ic("gold")}+{U.Fmt(r.Value.gold)}{(r.Value.debt > 0 ? $" (체납 {U.Fmt(r.Value.debt)} 차감)" : "")} {UIUtil.Ic("gem")}+{r.Value.gem} {UIUtil.Ic("dia")}+{r.Value.dia}");
                    auto.Render();
                    break;
                }
                default:
                    if (act.StartsWith("dbg-") && debug != null) debug.Do(act, arg);
                    break;
            }
        }
    }
}
