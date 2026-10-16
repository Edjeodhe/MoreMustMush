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
        public FarmView farm;                   // world-space world tree (꼬마버섯 세계수)

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
        public GameObject resetPanel;
        public DebugPanel debug;
        public Toast toast;

        public string Screen { get; private set; } = "title";
        public bool ModalOpen => modal != null && modal.IsOpen;

        void Awake()
        {
            I = this;
            Loc.Changed += OnLangChanged;
            gameObject.AddComponent<Tutorial>();   // 튜토리얼 오버레이 (코드로 만든다)
            Telemetry.Init();   // 서버 로그: 개인 ID·session_start (지난 실행이 비정상 종료였으면 crash 먼저)
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
#if MOREMUSH_DEV
            if (kb.backquoteKey.wasPressedThisFrame && debug != null) debug.Toggle();   // 디버그 모드: 개발·테스트 빌드 전용 (DevOnly.cs)
#endif
            if (kb.mKey.wasPressedThisFrame) { Snd.muted = !Snd.muted; ShowToast(Snd.muted ? Loc.T("소리 끔") : Loc.T("소리 켬")); }
            if (Screen != "round" && kb.escapeKey.wasPressedThisFrame && ModalOpen) CloseModal();
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

        // 오래 자리를 비웠다 돌아오면 수확 보따리를 알린다 (트리·세계수 화면에서, 다른 창이 없을 때만)
        void CheckReturn(double awayMs)
        {
            if (G == null || awayMs < HARV.returnMin * 1000) return;
            BagSettle();
            if (!BagReady() || (Screen != "tree" && Screen != "farm") || ModalOpen || !Unlock.On(Unlock.WORLDTREE)) return;   // 세계수가 열리기 전엔 알리지 않는다
            var b = G.farm.bag;
            ShowToast(Loc.F("꼬마들이 수확 보따리를 채워 뒀어요! {0}{1}", UIUtil.Ic("gold"), U.Fmt(System.Math.Floor(b.gold))) + (b.gem >= 1 ? $" {UIUtil.Ic("gem")}{U.Fmt(System.Math.Floor(b.gem))}" : "") + "\n" + Loc.T("세계수에서 받아요"));
            if (Screen == "tree") tree.Refresh(); else farmScreen.Render();
        }

        // ===== screens =====
        public void SetScreen(string s)
        {
            if (Screen == "farm" && s != "farm") farm.Flush();   // 떠날 때까지의 수확을 보따리에 넣고 저장
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
            TutorialStep();
            tree.Refresh();
        }

        // 튜토리얼 단계 넘기기와 새로 열린 기능 알림 (트리 화면에 들어올 때마다)
        void TutorialStep()
        {
            if (G == null) return;
            if (G.tut == 0 && G.stage > 1)
            {
                // 1스테이지 클리어 → 첫 노드(시작 노드)만 살 수 있는 골드를 보장한다 (기획서: "첫 노드만 해금 가능한 재화")
                G.tut = 1;
                G.gold = System.Math.Max(G.gold, NodeCost(NODE[ROOT_NODE], Lv(ROOT_NODE)).gold);
            }
            var opened = Unlock.Newly().Where(k => k != Unlock.UPGRADE).ToList();   // 강화는 튜토리얼이 직접 안내한다
            if (opened.Count > 0) ShowToast(Loc.F("새 기능이 열렸어요: {0}", string.Join(" · ", opened.Select(Unlock.Name))));
            SaveGame();
        }

        public void EnterFarm(string view)
        {
            Telemetry.Ftue(Telemetry.FTUE_FIRST_TREE);
            CloseModal();
            SetScreen("farm");
            farm.Enter();
            farmScreen.Render();
        }

        // 세계수 화면을 열고 탭을 고른다
        public void EnterWorldTree(string tab)
        {
            EnterFarm(null);
            if (tab != null) farmScreen.SetTab(tab);
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
            var pl = Lettering.Apply(pausePanel.transform.Find("Title")?.GetComponent<TMPro.TMP_Text>(), "pause", 1.3f);
            if (pl != null) pl.color = new Color(0.72f, 0.5f, 0.32f);   // warm brown on the cream window
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
            // 날씨는 5스테이지 클리어 뒤부터 (그전엔 맑음). 보스 판은 늘 나쁜 날씨
            string w = boss != null || Unlock.On(Unlock.WEATHER) ? RollWeather(boss != null) : "clear";
            if (G.tut == 2) G.tut = 3;   // 튜토리얼 마지막 단계: "수확하러 가기"를 눌렀다
            G.theme = LatestTheme().id;   // 지역은 고르지 않는다: 스테이지가 열어 준 가장 새 지역
            CloseModal();
            SetScreen("round");
            round.Begin(w, G.theme, boss);
            SaveGame();
            Telemetry.StageStart(G);
        }

        public void FinishRound()
        {
            var sum = RoundSim.R.Finish();
            SetScreen("settle");
            settle.Show(sum);
        }

        public void AfterSettle()
        {
            bool ending = CodexCount() >= SP_TOTAL && !G.ending;
            if (ending) { G.ending = true; SaveGame(); Telemetry.GameClear(G); }
            EnterTree();
            if (ending) RuntimeOverlays.ShowEnding(SP_TOTAL);   // 엔딩 가안: 글자만 (트리 화면 위에 덮는다)
        }

        // ===== actions (prototype onAction) =====
        public void OnAction(string act, string arg, Component src)
        {
            // 꼬마 액션은 인자가 세계수 꼬마 id여야 한다
            if ((act == "wtkidlv" || act == "wtawaken" || act == "wtzone" || act == "wtgift") && (arg == null || !KID.ContainsKey(arg) || !HasSpecial(arg))) return;
            switch (act)
            {
                case "close": Snd.Ui(); CloseModal(); break;
                case "newgame": Telemetry.Ftue(Telemetry.FTUE_TITLE_START); Telemetry.ResetSnap(); G = new SaveData(); FarmEnsure(); SaveGame(); Snd.Ui(); BeginRound(); break;   // 새 게임은 바로 1스테이지(튜토리얼)로
                case "continue":
                {
                    G = SaveIO.Load();
                    if (G == null)
                    {
                        G = new SaveData();
                        if (SaveIO.LastLoadFailed) ShowToast(Loc.T("세이브를 읽지 못해 새로 시작해요 (이전 세이브는 따로 보관했어요)"));
                    }
                    double away = G.seenT > 0 ? Now() - G.seenT : 0;
                    FarmEnsure(); Snd.Ui(); EnterTree();
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
                    Snd.Record(); ShowToast(Loc.F("세계수 경험치 +{0}", exp) + (CanEvolve() == GrowFail.None ? " " + Loc.T("세계수가 진화할 수 있어요!") : ""));
                    quests.Render();
                    break;
                }
                case "codex":
                    Snd.Ui(); codex.Open(Screen == "round");
                    if (Screen == "round" && RoundSim.R != null) RoundSim.R.paused = true;
                    break;
                case "codexsel": Snd.Ui(); codex.Select(arg); break;
#if MOREMUSH_DEV
                // 일괄 강화: 개발·테스트 빌드 전용. 버튼(TreeScreen/Buttons/Right/Bulk)은 DevOnly로 숨긴다
                case "bulk": Snd.Ui(); OpenModal(bulk, () => tree.Refresh()); bulk.Render(); break;
                case "bulktab": Snd.Ui(); bulk.SetBranch(arg); break;
                case "bulkgo": bulk.Go(); break;
#endif
                // 게임 종료 (타이틀·옵션). 웹은 탭을 닫을 수 없어 버튼을 HideOnWeb으로 숨긴다
                case "quit":
                    Snd.Ui();
                    if (G != null && Screen != "title") SaveGame();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
                case "go": Snd.Ui(); BeginRound(); break;
                case "bosstoggle":
                    G.bossOn = !G.bossOn; SaveGame(); Snd.Ui();
                    ShowToast(G.bossOn ? Loc.F("보스 도전 켬 · 스테이지 {0}에서 보스와 싸워요", NextBossStage(StageNow())) : Loc.T("보스 도전 끔 · 보스 스테이지에서는 파밍만 해요"));
                    if (Screen == "tree") tree.Refresh();
                    break;
                case "resume": CloseModal(); break;
                case "endnow": if (RoundSim.R != null) { var R = RoundSim.R; R.earlyExitSec = R.timeMax - R.timeLeft; R.timeLeft = 0; R.phase = "end"; R.endT = 0.01f; } CloseModal(); break;   // 먼저 끝내야 옵션→일시정지로 되돌아가지 않는다
                case "mute": Snd.muted = !Snd.muted; if (options != null && options.gameObject.activeInHierarchy) options.Render(); break;
                case "options": Snd.Ui(); OpenOptions(); break;
                case "lang": Snd.Ui(); Loc.SetLang(arg); break;
                case "pause": OpenPause(); break;
                case "speed": RoundController.NextSpeed(); Snd.Ui(); break;
                case "totree": Snd.Ui(); AfterSettle(); break;
                case "dmgstats": Snd.Ui(); RuntimeOverlays.ShowDamageStats(settle.Sum); break;
                case "workshop": Snd.Ui(); OpenModal(workshop, () => tree.Refresh()); workshop.Render(); break;
                case "hvunlock":
                    if (!HvUnlock(arg)) { Snd.Err(); break; }
                    Snd.Buy(); ShowToast(HvOn(arg) ? Loc.F("{0} 해금! 빈 장착 칸에 들어갔어요", HV[arg].n) : Loc.F("{0} 해금! 장착 칸이 모자라 꺼 두었어요", HV[arg].n)); workshop.Render(); break;
                case "hvlevel":
                    if (!HvLevel(arg)) { Snd.Err(); break; }
                    Snd.Buy(); ShowToast(HvStar(arg) >= 5 ? Loc.F("{0} {1} 최대! 날이 늘었어요", HV[arg].n, U.Stars(HvStar(arg))) : $"{HV[arg].n} {U.Stars(HvStar(arg))}"); workshop.Render(); break;
                case "hvtoggle":
                    if (!HvToggle(arg)) { Snd.Err(); ShowToast(Loc.F("장착 칸이 다 찼어요 ({0}/{1}). 다른 수확기를 먼저 꺼 주세요", OnHvs().Count, HvSlots())); break; }
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
                    skins.Render();
                    break;
                }
                // ===== 세계수 =====
                case "farm": Snd.Ui(); EnterFarm(null); break;
                case "totree2": Snd.Ui(); EnterTree(); break;
                case "worldtree": Snd.Ui(); AfterSettle(); EnterWorldTree("grow"); break;   // 정산 화면 "세계수에 바치기" (엔딩 처리는 AfterSettle이 먼저)
                case "wtbag":
                {
                    if (Screen != "farm") { Snd.Ui(); EnterFarm(null); }
                    var r = BagClaim();
                    if (r == null) { Snd.Err(); ShowToast(Loc.T("보따리가 비어 있어요. 꼬마를 구역에 보내 나무버섯을 따게 해요")); break; }
                    Snd.Record(); farm.BagBurst(r.Value.gold, r.Value.gem);
                    ShowToast(Loc.F("수확 보따리! 나무버섯 {0}개 · {1}+{2}", r.Value.n, UIUtil.Ic("gold"), U.Fmt(r.Value.gold)) + (r.Value.gem > 0 ? $" {UIUtil.Ic("gem")}+{r.Value.gem}" : ""));
                    farmScreen.Render(); break;
                }
                case "wttab": Snd.Ui(); if (Screen != "farm") EnterFarm(null); farmScreen.SetTab(arg); break;
                case "wtring":
                    if (!RingUp()) { Snd.Err(); ShowToast(Loc.T("골드가 부족해요")); break; }
                    Snd.Buy(); ShowToast(Loc.F("나이테 Lv.{0}! 나무버섯이 더 빨리 자라요", Ring())); farmScreen.Render(); break;
                case "wtevolve":
                {
                    var why = CanEvolve();
                    if (!Evolve(out var joined))
                    {
                        Snd.Err();
                        ShowToast(why == GrowFail.Ring ? Loc.F("나이테를 Lv.{0}까지 키워야 해요", WT.ringMax) : why == GrowFail.Exp ? Loc.T("경험치가 모자라요 (창고 버섯을 바치거나 의뢰를 해요)") : why == GrowFail.Gem ? Loc.T("균사석이 부족해요") : Loc.T("골드가 부족해요"));
                        break;
                    }
                    Snd.Record(); farm.TreeGrew(Loc.F("{0}단계 {1}!", Stage(), WT.StageName(Stage())));
                    string msg = Loc.F("세계수가 {0}단계 {1}(으)로 진화했어요! 가호: {2}", Stage(), WT.StageName(Stage()), BLESS[Stage() - 1].text);
                    foreach (var k in joined) msg += "\n" + Loc.F("{0} 합류! 장식 레시피 3종이 열렸어요", k.n);
                    ShowToast(msg);
                    if (joined.Count > 0) farm.Sync(false);
                    farmScreen.Render(); break;
                }
                case "wttribute":
                {
                    double exp = TributeAll();
                    if (exp <= 0) { Snd.Err(); ShowToast(Loc.T("바칠 창고 버섯이 없어요 (라운드에서 버섯을 따 와요)")); break; }
                    Snd.Record(); farm.TreeGrew(Loc.F("경험치 +{0}", U.Fmt(System.Math.Floor(exp))));
                    ShowToast(Loc.F("세계수에 버섯을 바쳤어요! 경험치 +{0}", U.Fmt(System.Math.Floor(exp))) + (CanEvolve() == GrowFail.None ? "\n" + Loc.T("세계수가 진화할 수 있어요!") : ""));
                    farmScreen.Render(); break;
                }
                case "wtkidlv":
                    if (!KidLvUp(arg)) { Snd.Err(); ShowToast(CanKidLvUp(arg) == KidFail.Cap ? Loc.T("각성해야 더 키울 수 있어요") : Loc.T("골드가 부족해요")); break; }
                    Snd.Buy(); farmScreen.selKid = arg; farm.Cheer(arg, Loc.T("힘이 솟아나!"), $"Lv.{KLv(arg)}"); farmScreen.Render(); break;
                case "wtawaken":
                {
                    var why = CanAwaken(arg);
                    if (!KidAwaken(arg)) { Snd.Err(); ShowToast(why == KidFail.Lv ? Loc.F("Lv.{0}이 되면 각성할 수 있어요", 10 * (Awaken(arg) + 1)) : why == KidFail.Gem ? Loc.T("균사석이 부족해요") : Loc.T("골드가 부족해요")); break; }
                    Snd.Record(); farmScreen.selKid = arg; farm.Cheer(arg, Loc.T("반짝반짝… 각성했어!"), new string('★', Awaken(arg)));
                    ShowToast(Loc.F("{0} 각성 ★{1}! {2}", SPC[arg].n, Awaken(arg), KidBonusText(KID[arg], KidStep(arg)))); farmScreen.Render(); break;
                }
                case "wtzone":
                {
                    // 쉼 → 열린 구역(자리 있는 곳) 차례로 → 쉼
                    string cur = ZoneOf(arg);
                    var order = new System.Collections.Generic.List<string> { null };
                    foreach (var z in ZONES) if (ZoneOpen(z.id)) order.Add(z.id);
                    int i0 = order.IndexOf(cur);
                    for (int step = 1; step <= order.Count; step++)
                    {
                        string next = order[(i0 + step) % order.Count];
                        if (next == null || next == cur || ZoneRoom(next)) { Assign(arg, next); break; }
                    }
                    Snd.Ui(); farmScreen.selKid = arg;
                    farm.Cheer(arg, ZoneOf(arg) != null ? Loc.F("{0}(으)로 갈게!", ZONE[ZoneOf(arg)].n) : Loc.T("잠깐 쉴게~"), null);
                    farmScreen.Render(); break;
                }
                case "wtgift":
                {
                    // 좋아하는 버섯을 모두, 없으면 창고에 가장 많은 버섯 10개
                    var k = KID[arg]; string sp = null; int n = 0;
                    foreach (var name in k.fav) { var id = SP_BY_NAME[name].id; if (StoreOf(id) > n) { sp = id; n = StoreOf(id); } }
                    if (sp == null) { foreach (var kv in G.farm.store) if (kv.Value > n) { sp = kv.Key; n = kv.Value; } n = System.Math.Min(n, 10); }
                    int up = sp == null ? -1 : Gift(arg, sp, n);
                    if (up < 0) { Snd.Err(); ShowToast(Loc.T("줄 수 있는 창고 버섯이 없어요")); break; }
                    Snd.Buy(); farmScreen.selKid = arg;
                    farm.Cheer(arg, IsFav(arg, sp) ? Loc.T("이거 제일 좋아해! 고마워!") : U.Pick(FARM_LINES["thanks"]), up > 0 ? Loc.F("호감도 Lv.{0}!", AffLv(arg)) : null);
                    ShowToast(Loc.F("{0}에게 {1} {2}개를 선물했어요", SPC[arg].n, SP[sp].n, n) + (up > 0 ? " · " + Loc.F("호감도 Lv.{0}!", AffLv(arg)) : ""));
                    farmScreen.Render(); break;
                }
                case "wtreq":
                {
                    int.TryParse(arg, out int i);
                    var gold = ReqDo(i);
                    if (gold == null) { Snd.Err(); ShowToast(Loc.T("창고 버섯이 모자라요")); break; }
                    Snd.Record(); farm.Cheer(G.farm.reqs[i].kid, U.Pick(FARM_LINES["thanks"]), null);
                    ShowToast(Loc.F("부탁을 들어줬어요! {0}+{1}", UIUtil.Ic("gold"), U.Fmt(gold.Value))); farmScreen.Render(); break;
                }
                case "wtweek":
                    if (!WeekClaim()) { Snd.Err(); break; }
                    Snd.Record(); ShowToast(Loc.F("꼬마의 상자! {0}+{1}", UIUtil.Ic("gem"), REQ.weekGem)); farmScreen.Render(); break;
                case "wtcraft":
                {
                    var why = CanCraft(arg);
                    if (!CraftStart(arg)) { Snd.Err(); ShowToast(why == CraftFail.Slots ? Loc.T("제작 슬롯이 다 찼어요") : why == CraftFail.Gem ? Loc.T("균사석이 부족해요") : why == CraftFail.Closed ? Loc.T("도감을 더 모으면 공방이 열려요") : Loc.T("골드가 부족해요")); break; }
                    Snd.Buy(); ShowToast(Loc.F("{0} 제작을 시작했어요 ({1})", DECO[arg].n, TimeText(CraftSeconds(DECO[arg])))); farmScreen.Render(); break;
                }
                case "wtclaim":
                {
                    int.TryParse(arg, out int i);
                    var r = CraftClaim(i);
                    if (r == null) { Snd.Err(); break; }
                    Snd.Record();
                    ShowToast(r.Value.isNew ? Loc.F("{0} 완성! 꾸미기에서 세계수에 놓아요", r.Value.d.n) : Loc.F("{0} 완성! 강화 재료가 됐어요", r.Value.d.n));
                    farmScreen.Render(); break;
                }
                case "wtrush":
                {
                    int.TryParse(arg, out int i);
                    if (!CraftRush(i))
                    {
                        Snd.Err();
                        bool done = i >= 0 && i < G.farm.crafts.Count && RushCost(G.farm.crafts[i]) <= 0;
                        ShowToast(done ? Loc.T("이미 완성됐어요") : RushLeftToday() <= 0 ? Loc.T("오늘은 즉시 완료를 더 할 수 없어요") : Loc.T("균사석이 부족해요"));
                        farmScreen.Render(); break;
                    }
                    Snd.Buy(); farmScreen.Render(); break;
                }
                case "wtplace":
                case "wtunplace":
                {
                    if (!int.TryParse(arg, out int slot) || slot < 0 || slot >= G.farm.decoSlots.Count) break;
                    string cur = G.farm.decoSlots[slot], next = null;
                    if (act == "wtplace")
                    {
                        // 이 슬롯에 맞는 가진 장식을 차례로 (다른 슬롯에 있던 것은 옮겨 온다)
                        var cand = new System.Collections.Generic.List<string>();
                        foreach (var d in DECOS) if (d.hang == DecoSlotHang(slot) && DecoOwned(d.id)) cand.Add(d.id);
                        if (cand.Count == 0) { Snd.Err(); break; }
                        next = cand[(cand.IndexOf(cur) + 1) % cand.Count];
                    }
                    if (!PlaceDeco(slot, next, out bool first)) { Snd.Err(); break; }
                    Snd.Ui(); farmScreen.selDeco = slot;
                    if (first) { Snd.Record(); ShowToast(Loc.F("{0}을(를) 처음 놓았어요! {1}+{2} · {3} 호감도 +{4}", DECO[next].n, UIUtil.Ic("gem"), DECO_FIRST_GEM, SPC[DECO[next].of].n, AFF.placeGift)); }
                    if (next != null && KidSetDone(DECO[next].of)) ShowToast(Loc.F("{0} 장식 세트 완성! 수확량 +{1} · 특별 보너스 +1단계", SPC[DECO[next].of].n, U.Pct(DECO_SET_YIELD)));
                    farmScreen.Render(); break;
                }
                case "wtenh":
                    if (!Enhance(arg)) { Snd.Err(); ShowToast(CanEnhance(arg) == EnhFail.Dup ? Loc.T("같은 장식을 하나 더 만들어야 해요") : Loc.T("재화가 부족해요")); break; }
                    Snd.Record(); ShowToast(Loc.F("{0} ★{1}! {2}", DECO[arg].n, DecoStar(arg), DecoFxText(DECO[arg], DecoMul(arg)))); farmScreen.Render(); break;
                default:
#if MOREMUSH_DEV
                    if (act != null && act.StartsWith("dbg-")) { if (debug != null) debug.Do(act, arg); }
                    else
#endif
                    if (act != "none") Debug.LogWarning($"[GameFlow] 처리하지 않는 버튼 액션: '{act}' (arg '{arg}', {(src != null ? src.name : "코드")})");   // 오타·지운 case를 바로 알 수 있게
                    break;
            }
        }
    }
}
