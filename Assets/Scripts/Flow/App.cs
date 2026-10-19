using UnityEngine;
using UnityEngine.SceneManagement;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // 씬 이름. 빌드 설정 순서: MainMenu → MainGame → OutGame → Stage
    public static class Scenes
    {
        public const string MainMenu = "MainMenu";   // 시작 화면
        public const string MainGame = "MainGame";   // 업그레이드
        public const string OutGame = "OutGame";     // 세계수
        public const string Stage = "Stage";         // 라운드 (수확기로 버섯 부수기)
    }

    // 씬을 넘어 살아 있는 공용 서비스. 씬에 놓지 않는다 (첫 씬이 열리기 전에 스스로 생긴다).
    // 맡는 일: 서버 로그 시작 · 프레임 · 효과음 · 플레이 시간 · 자리 비움 · 저장(포커스·일시정지·종료) · 씬 이동.
    // 화면에 띄우는 일(알림·팝업)은 하지 않고 이벤트로만 알린다 — 각 씬의 UI가 받아서 그린다.
    public class App : MonoBehaviour
    {
        public static App I { get; private set; }

        // 알림 문장 (토스트). 받는 UI가 아직 없으면 모아 두었다가 처음 구독할 때 한꺼번에 넘긴다
        // (예: 메뉴에서 세이브 읽기 실패 → MainGame UI가 구독하는 순간 보인다).
        // 구독한 UI는 OnDisable/OnDestroy에서 꼭 -= 한다 (static 이벤트라 씬이 바뀌어도 남는다)
        static System.Action<string> notified;
        static readonly System.Collections.Generic.List<string> queued = new System.Collections.Generic.List<string>();
        const int QUEUE_MAX = 3;
        public static event System.Action<string> Notified
        {
            add { notified += value; foreach (var m in queued) value(m); queued.Clear(); }
            remove => notified -= value;
        }

        // 오래 자리를 비웠다 돌아옴 (비운 시간 ms). 게임을 껐다 켠 경우도 포함 — 받는 UI가 없으면 구독할 때 넘긴다
        static System.Action<double> returned;
        static double pendingAway;
        public static event System.Action<double> Returned
        {
            add { returned += value; if (pendingAway > 0) { var a = pendingAway; pendingAway = 0; value(a); } }
            remove => returned -= value;
        }

        // 지금 창(팝업)이 열려 있나. 열린 창 수를 센다 — 라운드·세계수 입력이 이걸 보고 멈춘다
        static int modalCount;
        public static bool ModalOpen => modalCount > 0;
        public static void ModalPush() => modalCount++;
        public static void ModalPop() { if (modalCount > 0) modalCount--; }

        const int VOICES = 16;   // Snd 동시 발음 수
        double awayFrom;         // 창이 포커스를 잃은 시각 (ms, 0 = 자리에 있음)

        // 도메인 리로드를 끈 플레이 모드에서도 static이 이전 플레이 값을 들고 있지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            I = null; notified = null; returned = null; queued.Clear(); pendingAway = 0; modalCount = 0;
            G = null;
            RoundFlow.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("App");
            DontDestroyOnLoad(go);
            var snd = go.AddComponent<Snd>();
            for (int i = 0; i < VOICES; i++) { var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; snd.voices[i] = a; }
            I = go.AddComponent<App>();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            modalCount = 0;            // 씬이 바뀌면 이전 씬의 창은 사라진다
            TextFit.ApplyScene(s);     // 글자가 배치한 칸을 넘지 않게 + 언어에 맞는 폰트로
        }

        void Awake()
        {
            Telemetry.Init();   // 개인 ID·session_start (지난 실행이 비정상 종료였으면 crash 먼저)
            Ads.Init();         // 광고 SDK 시작 + 보상형 광고 미리 불러오기 (모바일만)
#if !UNITY_WEBGL || UNITY_EDITOR
            Application.targetFrameRate = 60;   // WebGL은 브라우저 requestAnimationFrame에 맡긴다
#endif
        }

        static bool InGame => G != null && SceneManager.GetActiveScene().name != Scenes.MainMenu;

        void Update()
        {
            if (InGame) { G.play += Time.unscaledDeltaTime; if (Application.isFocused) G.seenT = Now(); }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) { Snd.muted = !Snd.muted; Notify(Snd.muted ? Loc.T("소리 끔") : Loc.T("소리 켬")); }
        }

        // WebGL은 OnApplicationPause가 오지 않고 탭을 닫을 때 Quit도 믿을 수 없어서, 포커스를 잃을 때도 저장한다
        void OnApplicationPause(bool paused) { if (paused && InGame) SaveGame(); Away(paused); }
        void OnApplicationFocus(bool focus) { if (!focus && InGame) SaveGame(); Away(!focus); }
        void OnApplicationQuit() { if (G != null) SaveGame(); }

        void Away(bool leaving)
        {
            if (leaving) { if (awayFrom <= 0) awayFrom = Now(); return; }
            if (awayFrom <= 0) return;
            double away = Now() - awayFrom;
            awayFrom = 0;
            if (G != null && SceneManager.GetActiveScene().name != Scenes.MainMenu) CheckReturn(away);
        }

        static void CheckReturn(double awayMs)
        {
            if (awayMs < HARV.returnMin * 1000) return;
            BagSettle();
            if (returned != null) returned(awayMs); else pendingAway = awayMs;
        }

        // ===== 공용 =====
        public static void Notify(string msg)
        {
            if (notified == null)
            {
                // 알림 UI가 아직 없을 때: 최근 것만 남긴다 (오래된 문장이 나중에 한꺼번에 쏟아지지 않게)
                if (queued.Count >= QUEUE_MAX) queued.RemoveAt(0);
                queued.Add(msg); Debug.Log("[App] " + msg); return;
            }
            // 한 구독자가 터져도 나머지는 받게
            foreach (System.Action<string> h in notified.GetInvocationList())
            {
                try { h(msg); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
        }

        // 저장하고 씬을 옮긴다
        public static void Go(string scene)
        {
            if (G != null) SaveGame();
            SceneManager.LoadScene(scene);
        }

        // 세이브를 불러오거나 새로 만든다. MainMenu의 Start가 쓰고,
        // 에디터에서 MainGame·OutGame·Stage를 바로 플레이할 때도 각 씬이 불러 둔다
        public static void EnsureSave()
        {
            if (G != null) return;
            double away = 0;
            if (SaveIO.HasSave())
            {
                G = SaveIO.Load();
                if (G == null)
                {
                    G = new SaveData();
                    if (SaveIO.LastLoadFailed) Notify(Loc.T("세이브를 읽지 못해 새로 시작해요 (이전 세이브는 따로 보관했어요)"));
                }
                else if (G.seenT > 0) away = Now() - G.seenT;   // 껐다 켠 동안 꼬마들이 채운 보따리
            }
            else
            {
                Telemetry.Ftue(Telemetry.FTUE_TITLE_START);
                Telemetry.ResetSnap();
                G = new SaveData();
            }
            FarmEnsure();
            if (away > 0) CheckReturn(away);   // 보따리 정산은 세계수 데이터가 준비된 뒤에
            SaveGame();
        }

        // 세이브 마이그레이션 알림이 있으면 한 번 띄운다 (게임 씬에 들어간 뒤 부른다)
        public static void FlushMigrationNotice()
        {
            if (string.IsNullOrEmpty(SaveMigration.Notice)) return;
            Notify(SaveMigration.Notice);
            SaveMigration.Notice = null;
            SaveGame();
        }

        public static void Quit()
        {
            if (G != null) SaveGame();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
