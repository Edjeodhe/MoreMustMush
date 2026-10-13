using System.Linq;
using UnityEngine;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Screen flow and button actions (prototype setScreen / onAction / beginRound / finishRound).
    // Phase 1: title → (pre-round) → round → settlement → mycelium tree → codex.
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow I;

        [Header("Screens")]
        public TitleScreen title;
        public TreeScreen tree;
        public RoundController round;
        public GameObject roundUI;              // HUD, combo and field texts on the canvas
        public SettleScreen settle;
        public GameObject titleBackdrop;        // world-space town + grandpa behind title/settle
        public FarmScreen farmScreen;
        public FarmView farm;                   // world-space farm (ranch with the field inside)

        [Header("Modal")]
        public ModalHost modal;
        public CodexPanel codex;
        public GameObject pausePanel;
        public RecordsPanel records;
        public QuestPanel quests;
        public OptionsPanel options;
        public BulkPanel bulk;
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
            Loc.Changed += OnLangChanged;
#if !UNITY_WEBGL || UNITY_EDITOR
            Application.targetFrameRate = 60;   // WebGL은 브라우저 requestAnimationFrame에 맡긴다 (값을 주면 타이머 루프로 바뀌어 덜 부드럽다)
#endif
        }

        void OnDestroy() => Loc.Changed -= OnLangChanged;

        void Start() => RenderTitle();

        // 언어를 바꾸면 씬 글자(LocText)는 스스로 바뀌고, 코드가 그리는 화면은 여기서 다시 그린다
        void OnLangChanged()
        {
            switch (Screen)
            {
                case "title": title.Show(SaveIO.HasSave()); break;
                case "tree": tree.Refresh(); break;
                case "farm": farmScreen.Render(); break;
            }
            if (options != null && options.gameObject.activeInHierarchy) options.Render();
        }

        public void OpenOptions()
        {
            bool fromPause = pausePanel != null && pausePanel.activeInHierarchy;
            if (fromPause) CloseModal();   // 일시정지 창을 닫으면 라운드가 풀리므로, 옵션을 닫을 때 다시 멈춘다
            OpenModal(options, () => { if (fromPause) OpenPause(); });
            options.Render();
        }

        void Update()
        {
            if (G != null && Screen != "title") { G.play += Time.unscaledDeltaTime; if (Application.isFocused) G.seenT = Now(); }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.backquoteKey.wasPressedThisFrame && debug != null) debug.Toggle();
            if (kb.mKey.wasPressedThisFrame) { Snd.muted = !Snd.muted; ShowToast(Snd.muted ? Loc.T("소리 끔") : Loc.T("소리 켬")); }
            if (Screen != "round" && kb.escapeKey.wasPressedThisFrame) { if (ModalOpen) CloseModal(); else if (Screen == "farm" && farm.Placing) { farm.CancelPlace(); farmScreen.Render(); } }
        }

        void OnApplicationPause(bool paused) { if (paused && G != null && Screen != "title") SaveGame(); Away(paused); }
        // WebGL에서는 OnApplicationPause가 오지 않고 탭을 닫을 때 OnApplicationQuit도 믿을 수 없어서, 포커스를 잃을 때도 저장한다
        void OnApplicationFocus(bool focus) { if (!focus && G != null && Screen != "title") SaveGame(); Away(!focus); }
        void OnApplicationQuit() { if (G != null) SaveGame(); }

        // ===== 복귀 팝업 (방치 보상) =====
        double awayFrom;   // 창이 포커스를 잃은 시각 (ms, 0 = 자리에 있음)

        void Away(bool leaving)
        {
            if (leaving) { if (awayFrom <= 0) awayFrom = Now(); return; }
            if (awayFrom <= 0) return;
            double away = Now() - awayFrom;
            awayFrom = 0;
            CheckReturn(away);
        }

        // 오래 자리를 비웠다 돌아오면 자동 수확 보상 창을 띄운다 (트리·농장 화면에서, 다른 창이 없을 때만).
        void CheckReturn(double awayMs)
        {
            if (G == null || awayMs < AUTO.returnMin * 1000 || !AutoReady()) return;
            if ((Screen != "tree" && Screen != "farm") || ModalOpen || (Screen == "farm" && farm.Placing)) return;
            OpenAuto(awayMs);
        }

        void OpenAuto(double awayMs)
        {
            AutoEnsure();
            auto.away = awayMs;
            OpenModal(auto, () => { auto.away = 0; if (Screen == "tree") tree.Refresh(); else if (Screen == "farm") farmScreen.Render(); });
            auto.Render();
        }

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
            UIUtil.Show(titleBackdrop, s == "title" || s == "settle");
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
            if (!SPC.TryGetValue(id ?? "", out var k)) return;
            string before = EvoName(k);
            if (!Game.FarmEvolve(id)) { Snd.Err(); ShowToast(Loc.F("호감도 {0}칸을 모두 채우면 진화할 수 있어요", FARM.hearts)); return; }
            Snd.Record();
            farm.Evolved(id);
            ShowToast(Loc.F("{0} {1}(으)로 진화했어요! 부탁 보상 ×{2}", U.Iga(before), EvoName(k), U.FmtN(FARM.evoGem[EvoOf(id)])));
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

        // ===== round =====
        public const string HV_OVER_MSG = "수확기가 많습니다. 장착 가능 수확기 이하로 활성화가 필요합니다.";

        // 켠 수확기가 장착 칸보다 많으면 시작하지 않고 알린다
        bool BlockedByHarvesters()
        {
            if (!HvOver()) return false;
            Snd.Err(); ShowToast(Loc.T(HV_OVER_MSG));
            return true;
        }

        public void BeginRound()
        {
            if (BlockedByHarvesters()) return;
            var boss = BossRoundNext() ? BossAt(StageNow()) : null;
            string w = RollWeather(boss != null);
            G.theme = LatestTheme().id;   // 지역은 고르지 않는다: 스테이지가 열어 준 가장 새 지역
            CloseModal();
            SetScreen("round");
            round.Begin(w, G.theme, boss);
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
            if (CodexCount() >= SP_TOTAL && !G.ending) { G.ending = true; SaveGame(); ShowToast(Loc.F("버섯 도감 {0}종 완성! (엔딩 컷신은 3단계에서 추가돼요)", SP_TOTAL)); }
            EnterTree();
        }

        // ===== actions (prototype onAction) =====
        public void OnAction(string act, string arg, Component src)
        {
            switch (act)
            {
                case "close": Snd.Ui(); CloseModal(); break;
                case "newgame": G = new SaveData(); AutoEnsure(); SaveGame(); Snd.Ui(); EnterTree(); ShowToast(Loc.T("균사 트리에서 강화를 사거나 바로 수확하러 가 보세요!")); break;
                case "continue":
                {
                    G = SaveIO.Load();
                    if (G == null)
                    {
                        G = new SaveData();
                        if (SaveIO.LastLoadFailed) ShowToast(Loc.T("세이브를 읽지 못해 새로 시작해요 (이전 세이브는 따로 보관했어요)"));
                    }
                    double away = G.seenT > 0 ? Now() - G.seenT : 0;
                    AutoEnsure(); Snd.Ui(); EnterTree();
                    CheckReturn(away);
                    if (!string.IsNullOrEmpty(SaveMigration.Notice)) { ShowToast(SaveMigration.Notice); SaveMigration.Notice = null; SaveGame(); }
                    break;
                }
                case "reset": OpenModal(resetPanel); break;
                case "reset-yes": SaveIO.Wipe(); G = null; CloseModal(); RenderTitle(); ShowToast(Loc.T("초기화했어요")); break;
                case "title": SaveGame(); CloseModal(); RenderTitle(); break;
                case "records": Snd.Ui(); OpenModal(records); records.Render(); break;
                case "quests": Snd.Ui(); OpenModal(quests, () => { if (Screen == "tree") tree.Refresh(); else if (Screen == "farm") farmScreen.Render(); }); quests.Render(); break;
                case "questclaim":
                {
                    var exp = QuestClaim(arg);
                    if (exp == null) { Snd.Err(); break; }
                    Snd.Record(); ShowToast(TreeExpFull() && TreeLv() < TREE.maxLv ? Loc.F("세계수 경험치 +{0}! 세계수 나무를 키울 수 있어요", exp) : Loc.F("세계수 경험치 +{0}", exp));
                    quests.Render();
                    break;
                }
                case "codex":
                    Snd.Ui(); codex.Open(Screen == "round");
                    if (Screen == "round" && RoundSim.R != null) RoundSim.R.paused = true;
                    break;
                case "codexsel": Snd.Ui(); codex.Select(arg); break;
                case "bulk": Snd.Ui(); OpenModal(bulk, () => tree.Refresh()); bulk.Render(); break;
                case "bulktab": Snd.Ui(); bulk.SetBranch(arg); break;
                case "bulkgo": bulk.Go(); break;
                case "go": Snd.Ui(); BeginRound(); break;
                case "bosstoggle":
                    G.bossOn = !G.bossOn; SaveGame(); Snd.Ui();
                    ShowToast(G.bossOn ? Loc.F("보스 도전 켬 · 스테이지 {0}에서 보스와 싸워요", NextBossStage(StageNow())) : Loc.T("보스 도전 끔 · 보스 스테이지에서는 파밍만 해요"));
                    if (Screen == "tree") tree.Refresh();
                    break;
                case "resume": CloseModal(); break;
                case "endnow": if (RoundSim.R != null) { var R = RoundSim.R; R.timeLeft = 0; R.phase = "end"; R.endT = 0.01f; } CloseModal(); break;   // 먼저 끝내야 옵션→일시정지로 되돌아가지 않는다
                case "mute": Snd.muted = !Snd.muted; if (options != null && options.gameObject.activeInHierarchy) options.Render(); break;
                case "options": Snd.Ui(); OpenOptions(); break;
                case "lang": Snd.Ui(); Loc.SetLang(arg); break;
                case "pause": OpenPause(); break;
                case "speed": RoundController.NextSpeed(); Snd.Ui(); break;
                case "totree": Snd.Ui(); AfterSettle(); break;
                case "workshop": Snd.Ui(); OpenModal(workshop, () => tree.Refresh()); workshop.Render(); break;
                case "hvunlock":
                    if (!HvUnlock(arg)) { Snd.Err(); break; }
                    Snd.Buy(); ShowToast(HvOn(arg) ? Loc.F("{0} 해금! 빈 장착 칸에 들어갔어요", HV[arg].n) : Loc.F("{0} 해금! 장착 칸이 모자라 꺼 두었어요", HV[arg].n)); workshop.Render(); break;
                case "hvlevel":
                    if (!HvLevel(arg)) { Snd.Err(); break; }
                    Snd.Buy(); ShowToast(HvStar(arg) >= 5 ? Loc.F("{0} {1} 최대! 날이 늘었어요", HV[arg].n, U.Stars(HvStar(arg))) : $"{HV[arg].n} {U.Stars(HvStar(arg))}"); workshop.Render(); break;
                case "hvtoggle":
                    HvToggle(arg);
                    Snd.Ui(); workshop.Render();
                    if (HvOver()) ShowToast(Loc.T(HV_OVER_MSG));
                    break;
                case "skinshop": Snd.Ui(); OpenModal(skins, () => { if (Screen == "farm") farmScreen.Render(); else if (Screen == "tree") tree.Refresh(); }); skins.Render(); break;
                case "skintab": Snd.Ui(); skins.tab = arg; skins.Render(); break;
                case "buyskin":
                    if (!BuySkin(arg)) { Snd.Err(); ShowToast(Loc.T("균사석이 부족해요")); break; }
                    Snd.Record(); ShowToast(Loc.F("{0} 스킨을 샀어요!", SKIN[arg].n)); skins.Render(); break;
                case "wearskin":
                {
                    var p = arg.Split('|');
                    WearSkin(p[0]); Snd.Ui();
                    if (p.Length > 1) petCard.Render(); else skins.Render();
                    break;
                }
                // ===== 세계수 =====
                case "farm": Snd.Ui(); EnterFarm(null); break;
                case "totree2": Snd.Ui(); EnterTree(); break;
                case "farmall":
                {
                    var r = farm.CareAll();
                    if (r.n == 0) { Snd.Err(); ShowToast(Loc.T("지금 부탁하는 꼬마가 없어요")); break; }
                    Snd.Record(); ShowToast(Loc.F("꼬마 {0}마리를 돌봤어요! 균사석 +{1}", r.n, U.Fmt(r.gem)) + (r.bonus > 0 ? " " + Loc.F("(호감도 보너스 +{0})", r.bonus) : "") + (r.extra > 0 ? " · " + Loc.F("추가 +{0}", r.extra) : "")); farmScreen.Render();
                    break;
                }
                case "farmcard": Snd.Ui(); OpenPetCard(arg); break;
                // 꼬마 카드의 진화·별 버튼은 씬에 arg가 없다 → 지금 열린 카드의 꼬마
                case "evolve": FarmEvolve(string.IsNullOrEmpty(arg) ? petCard.id : arg); break;
                case "starup":
                {
                    string id = string.IsNullOrEmpty(arg) ? petCard.id : arg;
                    if (!SPC.ContainsKey(id ?? "")) break;
                    if (!StarUp(id)) { Snd.Err(); ShowToast(Loc.T("골드나 균사석이 부족해요")); break; }
                    Snd.Record(); farm.StarredUp(id); ShowToast($"{EvoName(SPC[id])} ★{CStarOf(id)}! {FxText(id)}"); petCard.Render(); break;
                }
                // ===== 세계수 나무 =====
                case "treepanel": Snd.Ui(); farmScreen.treeOpen = arg == "open" || !farmScreen.treeOpen; farmScreen.Render(); break;
                case "treeup":
                {
                    if (!TreeUp(out var joined)) { Snd.Err(); ShowToast(CanTreeUp() == TreeUpFail.Exp ? Loc.T("세계수 경험치가 모자라요 (의뢰로 모아요)") : Loc.T("골드가 부족해요")); break; }
                    Snd.Record(); farm.TreeLeveled();
                    string msg = Loc.F("세계수 나무 Lv.{0}! 버섯 자리 {1}개 · 수확량 ×{2}", TreeLv(), TREE.Slots(TreeLv()), U.FmtN(TreeYieldMul()));
                    foreach (var k in joined) msg += "\n" + Loc.F("{0} 합류! 세계수에 새 꼬마가 들어와요", k.n);
                    ShowToast(msg);
                    if (joined.Count > 0) { farm.Sync(false); farm.dirty = true; }
                    farmScreen.Render(); break;
                }
                case "pickall":
                {
                    int n = farm.PickAll();
                    if (n == 0) { Snd.Err(); ShowToast(farm.Workers == 0 && farm.crits.Count > 0 ? Loc.T("꼬마들이 모두 건물을 짓고 있어요") : farm.crits.Count == 0 ? Loc.T("세계수 나무 레벨이 오르면 꼬마가 합류해 버섯을 따 줘요") : Loc.T("다 자란 버섯이 없어요")); break; }
                    Snd.Ui(); ShowToast(Loc.F("꼬마들이 버섯 {0}개를 따러 가요!", n)); farmScreen.Render();
                    break;
                }
                case "treeaccel": OpenAccel("tree", 0); break;
                case "accelok":
                {
                    bool ok = accel.kind == "build" ? BuildAccel(accel.uid) : TreeAccel();
                    if (!ok) { Snd.Err(); ShowToast(Loc.T("균사석이 부족해요")); break; }
                    Snd.Record(); ShowToast(accel.kind == "build" ? Loc.T("건설 완료!") : Loc.T("버섯이 모두 다 자랐어요!"));
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
                        ShowToast(fail == BuildFail.Max ? Loc.T("더 지을 수 없어요 (최대 개수)") : fail == BuildFail.Gem ? Loc.T("균사석이 부족해요") : Loc.T("쉬고 있는 꼬마가 없어요 (모두 건설 중)"));
                        break;
                    }
                    if (!farm.BeginPlace(arg)) { Snd.Err(); ShowToast(Loc.T("목장에 놓을 자리가 없어요")); break; }
                    Snd.Ui(); CloseModal(); farmScreen.Render();
                    break;
                }
                case "placeok":
                {
                    if (farm.Moving)
                    {
                        if (!farm.ConfirmMove()) { Snd.Err(); ShowToast(Loc.T("여기에는 설치할 수 없어요")); break; }
                        Snd.Buy(); farmScreen.Render();
                        break;
                    }
                    var b = farm.ConfirmPlace();
                    if (b == null) { Snd.Err(); ShowToast(farm.Placing && !farm.PlaceOk ? Loc.T("여기에는 설치할 수 없어요") : Loc.T("지을 수 없어요")); break; }
                    Snd.Record();
                    ShowToast(Loc.F("{0} {1}을(를) 짓기 시작했어요! ({2})", U.Iga(EvoName(SPC[b.critter])), BUILDING[b.id].n, BuildLeftText(b)));
                    farmScreen.Render();
                    break;
                }
                case "placecancel": Snd.Ui(); farm.CancelPlace(); farmScreen.Render(); break;
                // ===== 자동 수확 보상 =====
                case "autoharvest": Snd.Ui(); OpenAuto(0); break;
                case "claimauto":
                {
                    var r = ClaimAuto();
                    if (r == null) { Snd.Err(); break; }
                    Snd.Record();
                    ShowToast(Loc.F("자동 수확 보상! {0}+{1} {2}+{3}", UIUtil.Ic("gold"), U.Fmt(r.Value.gold), UIUtil.Ic("gem"), r.Value.gem));
                    if (auto.away > 0) CloseModal(); else auto.Render();   // 복귀 팝업은 받으면 닫는다
                    break;
                }
                default:
                    if (act != null && act.StartsWith("dbg-")) { if (debug != null) debug.Do(act, arg); }
                    else if (act != "none") Debug.LogWarning($"[GameFlow] 처리하지 않는 버튼 액션: '{act}' (arg '{arg}', {(src != null ? src.name : "코드")})");   // 오타·지운 case를 바로 알 수 있게
                    break;
            }
        }
    }
}
