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

        [Header("Modal")]
        public ModalHost modal;
        public CodexPanel codex;
        public PreRoundPanel preRound;
        public GameObject pausePanel;
        public RecordsPanel records;
        public BulkPanel bulk;
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
            if (Screen != "round" && kb.escapeKey.wasPressedThisFrame && ModalOpen) CloseModal();
        }

        void OnApplicationPause(bool paused) { if (paused && G != null && Screen != "title") SaveGame(); }
        void OnApplicationQuit() { if (G != null) SaveGame(); }

        // ===== screens =====
        public void SetScreen(string s)
        {
            Screen = s;
            UIUtil.Show(title, s == "title");
            UIUtil.Show(tree, s == "tree");
            UIUtil.Show(round, s == "round");
            UIUtil.Show(roundUI, s == "round");
            UIUtil.Show(settle, s == "settle");
            UIUtil.Show(tax, s == "tax");
            UIUtil.Show(titleBackdrop, s == "title" || s == "settle" || s == "tax");
            if (tree != null) tree.SetActiveWorld(s == "tree");
        }

        public void RenderTitle() { SetScreen("title"); title.Show(SaveIO.HasSave()); }

        public void EnterTree()
        {
            SetScreen("tree");
            CloseModal();
            tree.Refresh();
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

        // ===== round =====
        public void OpenPreRound()
        {
            if (!THEME.TryGetValue(G.theme, out var th) || !ThemeOpen(th)) G.theme = LatestTheme().id;
            if (THEMES.Count(ThemeOpen) <= 1 && G.dishes.Count == 0) { BeginRound(); return; }
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
            // 요리는 이번 라운드에 먹었다. 남은 라운드가 있으면 다음 라운드에도 남는다
            G.dishes = G.dishes.Where(id =>
            {
                int n = (G.dishLeft.TryGetValue(id, out var l) ? l : 1) - 1;
                if (n > 0) { G.dishLeft[id] = n; return true; }
                G.dishLeft.Remove(id); return false;
            }).ToList();
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
            G.quests = new Newtonsoft.Json.Linq.JArray();   // 마을 의뢰는 사이클마다 새로 (2단계)
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
                case "newgame": G = new SaveData(); SaveGame(); Snd.Ui(); EnterTree(); ShowToast($"균사 트리에서 강화를 사거나 바로 수확하러 가 보세요! 세금은 {TAX.every}라운드마다 나와요"); break;
                case "continue": G = SaveIO.Load() ?? new SaveData(); Snd.Ui(); EnterTree(); break;
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
                case "shop": case "farm": case "skinshop": case "workshop": case "sporeshop":
                    Snd.Err(); ShowToast("2·3단계에서 열리는 기능이에요"); break;
                default:
                    if (act.StartsWith("dbg-") && debug != null) debug.Do(act, arg);
                    break;
            }
        }
    }
}
