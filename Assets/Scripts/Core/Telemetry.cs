using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreMush
{
    // 데이터 수집용 서버 로그 (log.md 표). 웹·exe 공통.
    // 로그는 먼저 기기(PlayerPrefs)의 큐에 쌓고, ENDPOINT가 있으면 오래된 것부터 하나씩 POST한다. 실패하면 큐에 남겨 두고 나중에 다시 보낸다.
    // ENDPOINT가 비어 있으면 보내지 않고 쌓기만 한다(QUEUE_MAX까지, 넘치면 오래된 것부터 버림).
    // 보내는 형식 (JSON 한 건): { uid, name, ver, plat, sid, t, ts, data } — data는 JSON 문자열(DATA_MAX자까지)
    public static class Telemetry
    {
        // Cloudflare 서버 주소. 예: "https://xxx.workers.dev/log"
        public static string ENDPOINT = "";
        public const int DATA_MAX = 4000;        // 서버 data 칸 한도 (game_clear 때문에 4,000자)
        public const int QUEUE_MAX = 300;        // 못 보낸 로그를 기기에 남겨 두는 최대 건수
        public const int ERROR_MAX = 3;          // 세션당 error 로그 최대
        public const float RETRY_SEC = 30;       // 전송 실패 뒤 다시 시도할 때까지

        const string K_UID = "tlm_uid", K_QUEUE = "tlm_queue", K_RUN = "tlm_running", K_LAST = "tlm_last_stage",
                     K_SESS = "tlm_sessions", K_NETFAIL = "tlm_netfail", K_FTUE = "tlm_ftue", K_CLEAR = "tlm_clear_sent", K_SNAP = "tlm_snap";

        public static string Uid { get; private set; }
        public static bool FirstRun { get; private set; }
        public static string SessionId { get; private set; }
        static float t0;
        static bool inited;
        static int errors;
        static List<string> queue = new List<string>();

        public static string Platform =>
            Application.platform == RuntimePlatform.WebGLPlayer ? "web" :
            Application.platform == RuntimePlatform.WindowsPlayer ? "exe" :
            Application.isEditor ? "editor" : Application.platform.ToString();

        // 세션 시작 후 경과 초
        public static double T => Math.Round(Time.realtimeSinceStartup - t0, 1);

        // 게임 접속: 개인 ID를 만들고(처음이면) 지난 세션이 비정상 종료였으면 crash를 먼저 보낸다
        public static void Init()
        {
            if (inited) return;
            inited = true;
            t0 = Time.realtimeSinceStartup;
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            Uid = PlayerPrefs.GetString(K_UID, "");
            FirstRun = string.IsNullOrEmpty(Uid);
            if (FirstRun) { Uid = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(K_UID, Uid); }
            try { queue = JsonConvert.DeserializeObject<List<string>>(PlayerPrefs.GetString(K_QUEUE, "[]")) ?? new List<string>(); }
            catch { queue = new List<string>(); }
            int sessions = PlayerPrefs.GetInt(K_SESS, 0) + 1;
            PlayerPrefs.SetInt(K_SESS, sessions);

            // 지난 실행이 "실행 중" 표시를 지우지 못하고 끝났으면 비정상 종료로 본다 (죽는 순간에는 못 보내므로 사후 전송)
            if (PlayerPrefs.GetInt(K_RUN, 0) == 1)
                Send("crash", new { stage = PlayerPrefs.GetInt(K_LAST, 0), ver = PlayerPrefs.GetString(K_RUN + "_ver", Application.version) });
            MarkRunning(true);

            Send("session_start", new { first = FirstRun, sessions });
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            TelemetryRunner.Ensure();
        }

        // "실행 중" 표시. 앱이 뒤로 가거나 꺼질 때 지우고, 돌아오면 다시 켠다 (탭 닫기·강제 종료를 크래시로 오인하지 않게)
        public static void MarkRunning(bool on)
        {
            PlayerPrefs.SetInt(K_RUN, on ? 1 : 0);
            if (on) PlayerPrefs.SetString(K_RUN + "_ver", Application.version);
            PlayerPrefs.Save();
        }

        // 에디터 플레이는 콘솔에만 찍고 서버 큐에는 넣지 않는다 (개발 중 데이터가 섞이지 않게). 켜려면 SEND_IN_EDITOR
        public static bool SEND_IN_EDITOR = false;

        public static void Send(string name, object data)
        {
            if (SaveIO.Suspended) return;   // 밸런스 시뮬(BalanceSim 샌드박스)은 보내지 않는다
            if (!inited && name != "crash" && name != "session_start") Init();
            string d = data == null ? "" : JsonConvert.SerializeObject(data, Formatting.None);
            if (d.Length > DATA_MAX) d = d.Substring(0, DATA_MAX);
            var entry = new Dictionary<string, object>
            {
                ["uid"] = Uid, ["name"] = name, ["ver"] = Application.version, ["plat"] = Platform, ["sid"] = SessionId,
                ["t"] = T, ["ts"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), ["data"] = d,
            };
            if (Application.isEditor) { Debug.Log($"[Telemetry] {name} {d}"); if (!SEND_IN_EDITOR) return; }
            queue.Add(JsonConvert.SerializeObject(entry));
            while (queue.Count > QUEUE_MAX) queue.RemoveAt(0);
            SaveQueue();
        }

        static void SaveQueue() { PlayerPrefs.SetString(K_QUEUE, JsonConvert.SerializeObject(queue)); PlayerPrefs.Save(); }

        // ===== 스테이지 =====
        // 직전 stage_end 때의 업그레이드 레벨. 그 뒤에 산 업그레이드(변경분·소모 재화)를 여기서 뺀 값으로 구한다
        class Snap { public Dictionary<string, int> nodes = new Dictionary<string, int>(); }

        static Snap LoadSnap()
        {
            try { var s = JsonConvert.DeserializeObject<Snap>(PlayerPrefs.GetString(K_SNAP, "")); if (s != null) return s; } catch { }
            return null;
        }

        static Snap TakeSnap(SaveData g) => new Snap { nodes = new Dictionary<string, int>(g.nodes) };

        // 스테이지 시작: 서버로는 보내지 않는다. 비정상 종료 때 보낼 마지막 스테이지만 남긴다
        public static void StageStart(SaveData g)
        {
            if (SaveIO.Suspended) return;   // 밸런스 시뮬은 Init()(DontDestroyOnLoad)을 거치지 않게
            if (!inited) Init();
            PlayerPrefs.SetInt(K_LAST, g.stage);
            PlayerPrefs.Save();
        }

        // 새 게임: 업그레이드 변경분 기준을 0레벨로 되돌린다
        public static void ResetSnap() { PlayerPrefs.DeleteKey(K_SNAP); PlayerPrefs.Save(); }

        // 스테이지 종료. result: clear / fail / early_exit. sec는 일반·보스 중도 이탈 때만, bossHp(0~100)는 보스 fail·early_exit 때만 싣는다
        public static void StageEnd(SaveData g, int stage, string result, bool boss, double score, double gained, double sec, double bossHpPct, List<string> newCodex)
        {
            if (SaveIO.Suspended) return;
            if (!inited) Init();
            var snap = LoadSnap() ?? new Snap();   // 기준이 없으면(첫 판·로그 도입 전 세이브) 0레벨부터 센다
            var ups = new List<object[]>();
            double spentGold = 0, spentGem = 0;
            foreach (var kv in g.nodes)
            {
                int before = snap.nodes.TryGetValue(kv.Key, out var b) ? b : 0;
                if (kv.Value == before) continue;
                ups.Add(new object[] { kv.Key, kv.Value });
                if (kv.Value > before && Defs.NODE.TryGetValue(kv.Key, out var n))
                    for (int L = before; L < kv.Value; L++) { var c = Game.NodeCost(n, L); spentGold += c.gold; spentGem += c.gem; }
            }
            var data = new Dictionary<string, object>
            {
                ["stage"] = stage, ["result"] = result, ["boss"] = boss, ["score"] = N(score),
                ["gain"] = N(gained), ["spent"] = N(spentGold), ["gold"] = N(g.gold),
                ["ups"] = ups, ["codex"] = newCodex,
            };
            if (spentGem > 0 || g.gem > 0) { data["spent_gem"] = N(spentGem); data["gem"] = N(g.gem); }
            if (result == "early_exit") data["sec"] = Math.Round(sec, 1);
            if (boss && result != "clear") data["boss_hp"] = Math.Round(bossHpPct, 1);
            Send("stage_end", data);
            PlayerPrefs.SetString(K_SNAP, JsonConvert.SerializeObject(TakeSnap(g)));
            PlayerPrefs.Save();
        }

        // ===== 세계수 · 의뢰 =====
        public static void TreeUp(string id, int level, double gold, double gem) => Send("tree_up", Cost(new Dictionary<string, object> { ["id"] = id, ["lv"] = level }, gold, gem));
        public static void TreeBuild(string id, double gold, double gem) => Send("tree_build", Cost(new Dictionary<string, object> { ["id"] = id }, gold, gem));
        // 의뢰 보상은 세계수 경험치뿐이다
        public static void QuestClear(string id, string kind, int exp) =>
            Send("quest_clear", new Dictionary<string, object> { ["id"] = id, ["kind"] = kind, ["exp"] = exp });

        static Dictionary<string, object> Cost(Dictionary<string, object> d, double gold, double gem)
        {
            if (gold > 0) d["gold"] = N(gold);
            if (gem > 0) d["gem"] = N(gem);
            return d;
        }

        // 재화 숫자: long에 들어가면 정수로(9876), 아주 크면 double 그대로(1.2E+20)
        static object N(double v) { v = Math.Round(v); return Math.Abs(v) < 9e15 ? (object)(long)v : v; }

        // ===== 초반 구간 (FTUE) =====
        // 단계마다 한 번만 보낸다. 번호는 FTUE_* 상수
        public const int FTUE_TITLE_START = 1, FTUE_FIRST_LAUNCH = 2, FTUE_FIRST_ROUND_END = 3, FTUE_FIRST_UPGRADE = 4, FTUE_FIRST_TREE = 5;
        public static void Ftue(int step)
        {
            if (SaveIO.Suspended) return;
            string done = PlayerPrefs.GetString(K_FTUE, ",");
            if (done.Contains("," + step + ",")) return;
            PlayerPrefs.SetString(K_FTUE, done + step + ",");
            Send("ftue", new { step });
        }

        // ===== 게임 클리어 (엔딩) — 한 번만 =====
        public static void GameClear(SaveData g)
        {
            if (PlayerPrefs.GetInt(K_CLEAR, 0) == 1) return;
            PlayerPrefs.SetInt(K_CLEAR, 1);
            Send("game_clear", new Dictionary<string, object>
            {
                ["play"] = N(g.play), ["sessions"] = PlayerPrefs.GetInt(K_SESS, 1), ["stage"] = g.stage, ["rounds"] = g.rounds,
                ["nodes"] = g.nodes.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value),
            });
        }

        // ===== 에러 =====
        static void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            if (msg != null && msg.StartsWith("[Telemetry]")) return;
            if (errors >= ERROR_MAX) return;
            errors++;
            // 스택 첫 줄은 Debug.LogError 자신이라 그 다음 줄(실제로 부른 곳)을 쓴다
            string where = string.IsNullOrEmpty(stack) ? "" : stack.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("UnityEngine.Debug")) ?? "";
            Send("error", new { msg = msg != null && msg.Length > 300 ? msg.Substring(0, 300) : msg, at = where, type = type.ToString() });
        }

        // ===== 전송 =====
        internal static IEnumerator Pump()
        {
            while (true)
            {
                if (string.IsNullOrEmpty(ENDPOINT) || queue.Count == 0) { yield return new WaitForSecondsRealtime(2); continue; }
                string body = queue[0];
                using (var req = new UnityWebRequest(ENDPOINT, "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.timeout = 10;
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        queue.RemoveAt(0); SaveQueue();
                        // 연결 실패는 횟수만 모아 두었다가 연결되면 한 번에 보낸다 (세션당 error 한도와 별개)
                        int fails = PlayerPrefs.GetInt(K_NETFAIL, 0);
                        if (fails > 0) { PlayerPrefs.SetInt(K_NETFAIL, 0); Send("error", new { msg = "net_fail", count = fails }); }
                    }
                    else
                    {
                        PlayerPrefs.SetInt(K_NETFAIL, PlayerPrefs.GetInt(K_NETFAIL, 0) + 1); PlayerPrefs.Save();
                        // 4xx(형식 오류 등)는 다시 보내도 같으니 버린다
                        if (req.responseCode >= 400 && req.responseCode < 500) { queue.RemoveAt(0); SaveQueue(); }
                        else yield return new WaitForSecondsRealtime(RETRY_SEC);
                    }
                }
            }
        }
    }

    // 전송 코루틴과 앱 상태 콜백을 맡는 숨은 오브젝트 (씬에 따로 놓지 않는다)
    public class TelemetryRunner : MonoBehaviour
    {
        static TelemetryRunner inst;

        public static void Ensure()
        {
            if (inst != null) return;
            var go = new GameObject("Telemetry");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            inst = go.AddComponent<TelemetryRunner>();
            inst.StartCoroutine(Telemetry.Pump());
        }

        void OnApplicationPause(bool paused) => Telemetry.MarkRunning(!paused);
        void OnApplicationFocus(bool focus) => Telemetry.MarkRunning(focus);
        void OnApplicationQuit() => Telemetry.MarkRunning(false);
    }
}
