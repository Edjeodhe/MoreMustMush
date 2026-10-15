using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreMush
{
    // 분석용 로그 (로그 서버 인수인계 HANDOFF.md 4·5절). 웹·exe 공통. 게임 데이터는 서버에 올리지 않는다.
    //  · 로그는 먼저 로컬 큐(persistentDataPath/logq.dat, SecureStore 암호화)에 쌓고, 접속 직후·스테이지 종료·창이 뒤로 갈 때 묶어서 보낸다
    //  · 한 번에 최대 50건·20KB. 서버가 "ok"를 돌려준 것만 큐에서 지운다. 실패하면 15·30·60초 간격으로 3번 더, 그래도 안 되면 다음 전송 때 함께
    //  · 큐는 200건까지, 넘치면 오래된 것부터 버린다. 같은 로그가 두 번 갈 수 있어 eid(고유 번호)를 붙인다 (서버가 INSERT OR IGNORE)
    // 보내는 한 건: { game, version, platform, uid, session, event, t, eid, ts, data{...} }
    // 서버 주소·키는 커밋하지 않는 설정 파일 Assets/Resources/LogConfig.json ({"url": "...", "key": "..."}). 없거나 키가 비면 쌓기만 하고 보내지 않는다
    public static class Telemetry
    {
        public const string GAME = "MMM";
        public const string DEFAULT_URL = "https://moremustmush.nonggle.workers.dev/";
        public const int DATA_MAX = 4000;            // 서버 data 칸 한도 (game_clear 때문에 4,000자)
        public const int QUEUE_MAX = 200;            // 못 보낸 로그를 기기에 남겨 두는 최대 건수
        public const int BATCH_MAX = 50, BATCH_BYTES = 19000;   // 서버 한도 50건·20KB (여유를 둔다)
        public const int ERROR_MAX = 3;              // 세션당 error 로그 최대
        static readonly float[] RETRY = { 15, 30, 60 };   // 실패 뒤 다시 보내기까지(초)

        // 에디터 플레이는 콘솔에만 찍고 서버 큐에는 넣지 않는다 (개발 중 데이터가 섞이지 않게). 켜려면 SEND_IN_EDITOR
        public static bool SEND_IN_EDITOR = false;

        const string K_RUN = "tlm_running", K_LAST = "tlm_last_stage", K_SESS = "tlm_sessions", K_NETFAIL = "tlm_netfail",
                     K_FTUE = "tlm_ftue", K_CLEAR = "tlm_clear_sent", K_SNAP = "tlm_snap", K_SEQ = "tlm_seq";

        public static string Uid => DeviceId.Get();
        public static bool FirstRun { get; private set; }
        public static string SessionId { get; private set; }
        static string url, key;
        static float t0;
        static bool inited, flushWanted, forbidden;
        static int errors;
        static List<string> queue = new List<string>();

        public static string Platform =>
            Application.platform == RuntimePlatform.WebGLPlayer ? "web" :
            Application.platform == RuntimePlatform.WindowsPlayer ? "exe" :
            Application.isEditor ? "editor" : Application.platform.ToString();

        static string QueuePath => Path.Combine(Application.persistentDataPath, "logq.dat");
        public static bool CanSend => !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(key) && !forbidden && (!Application.isEditor || SEND_IN_EDITOR);

        // 세션 시작 후 경과 초
        public static double T => Math.Round(Time.realtimeSinceStartup - t0, 1);

        // 게임 접속: 기기 ID(처음이면 생성) · 지난 실행이 비정상 종료였으면 crash · session_start, 그리고 곧바로 한 번 보낸다
        public static void Init()
        {
            if (inited || SaveIO.Suspended) return;
            inited = true;
            t0 = Time.realtimeSinceStartup;
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            DeviceId.Get();
            FirstRun = DeviceId.Created;
            LoadConfig();
            LoadQueue();
            int sessions = PlayerPrefs.GetInt(K_SESS, 0) + 1;
            PlayerPrefs.SetInt(K_SESS, sessions);

            // 지난 실행이 "실행 중" 표시를 지우지 못하고 끝났으면 비정상 종료로 본다 (죽는 순간에는 못 보내므로 사후 전송)
            if (PlayerPrefs.GetInt(K_RUN, 0) == 1)
                Send("crash", new Dictionary<string, object> { ["stage"] = PlayerPrefs.GetInt(K_LAST, 0), ["ver"] = PlayerPrefs.GetString(K_RUN + "_ver", Application.version) });
            MarkRunning(true);

            var d = new Dictionary<string, object> { ["first"] = FirstRun, ["sessions"] = sessions };
            var bal = SaveIO.PeekGold();   // 아직 세이브를 불러오기 전(타이틀)이라 파일에서 잔액만 읽는다
            if (bal.HasValue) d["bal"] = N(bal.Value);
            Send("session_start", d);
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            TelemetryRunner.Ensure();
            Flush();
        }

        static void LoadConfig()
        {
            url = DEFAULT_URL; key = null;
            try
            {
                var ta = Resources.Load<TextAsset>("LogConfig");
                if (ta == null) { Debug.Log("[Telemetry] LogConfig 없음 — 로그를 쌓기만 하고 보내지 않는다"); return; }
                var o = JObject.Parse(ta.text);
                if (!string.IsNullOrWhiteSpace((string)o["url"])) url = ((string)o["url"]).Trim();
                key = ((string)o["key"])?.Trim();
            }
            catch (Exception e) { Debug.LogWarning("[Telemetry] LogConfig를 읽지 못함: " + e.Message); }
        }

        // "실행 중" 표시. 앱이 뒤로 가거나 꺼질 때 지우고, 돌아오면 다시 켠다 (탭 닫기·강제 종료를 크래시로 오인하지 않게)
        public static void MarkRunning(bool on)
        {
            PlayerPrefs.SetInt(K_RUN, on ? 1 : 0);
            if (on) PlayerPrefs.SetString(K_RUN + "_ver", Application.version);
            PlayerPrefs.Save();
        }

        // 다음 프레임에 보낸다 (전송 시점: 접속 직후 · 스테이지 종료 · 창이 뒤로 갈 때)
        public static void Flush() => flushWanted = true;

        public static void Send(string name, Dictionary<string, object> data)
        {
            if (SaveIO.Suspended) return;   // 밸런스 시뮬(BalanceSim 샌드박스)은 보내지 않는다
            if (!inited && name != "crash" && name != "session_start") Init();
            string d = data == null ? "{}" : JsonConvert.SerializeObject(data, Formatting.None);
            if (d.Length > DATA_MAX) { Debug.LogWarning($"[Telemetry] {name} data {d.Length}자 > {DATA_MAX} — 잘라서 JSON이 깨지지 않게 표시만 남긴다"); d = JsonConvert.SerializeObject(new { truncated = d.Length }); }
            long seq = PlayerPrefs.GetInt(K_SEQ, 0) + 1; PlayerPrefs.SetInt(K_SEQ, (int)(seq % int.MaxValue));
            var entry = new JObject
            {
                ["game"] = GAME, ["version"] = Application.version, ["platform"] = Platform, ["uid"] = Uid, ["session"] = SessionId,
                ["event"] = name, ["t"] = T, ["eid"] = SessionId + "-" + seq,
                ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),   // 기기 시각 (큐에 오래 있다 가면 서버 시각과 다르다)
                ["data"] = JObject.Parse(d),
            };
            if (Application.isEditor) { Debug.Log($"[Telemetry] {name} {d}"); if (!SEND_IN_EDITOR) return; }
            queue.Add(entry.ToString(Formatting.None));
            while (queue.Count > QUEUE_MAX) queue.RemoveAt(0);
            SaveQueue();
        }

        static void LoadQueue()
        {
            queue = new List<string>();
            try
            {
                if (SecureStore.Load(QueuePath, Uid, out _, out var json))
                    queue = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch (Exception e) { Debug.LogWarning("[Telemetry] 큐를 읽지 못함: " + e.Message); }
        }

        static void SaveQueue()
        {
            try { SecureStore.Save(QueuePath, Uid, JsonConvert.SerializeObject(queue)); }
            catch (Exception e) { Debug.LogWarning("[Telemetry] 큐를 저장하지 못함: " + e.Message); }
        }

        // ===== 스테이지 =====
        // 직전 stage_end 때의 업그레이드 레벨·골드. 그 뒤 업그레이드 변경분(up)·소모 합계(out)·그 밖의 골드 변화(etc)를 여기서 뺀 값으로 구한다
        class Snap { public Dictionary<string, int> nodes = new Dictionary<string, int>(); public double gold; public bool hasGold; }

        static Snap LoadSnap()
        {
            try { var s = JsonConvert.DeserializeObject<Snap>(PlayerPrefs.GetString(K_SNAP, "")); if (s != null) return s; } catch { }
            return null;
        }

        // 스테이지 시작: 서버로는 보내지 않는다. 비정상 종료 때 보낼 마지막 스테이지만 남긴다
        public static void StageStart(SaveData g)
        {
            if (SaveIO.Suspended) return;
            if (!inited) Init();
            PlayerPrefs.SetInt(K_LAST, g.stage);
            PlayerPrefs.Save();
        }

        // 새 게임: 업그레이드 변경분 기준을 0레벨로 되돌린다
        public static void ResetSnap() { PlayerPrefs.DeleteKey(K_SNAP); PlayerPrefs.Save(); }

        // 스테이지 종료. result: clear / fail / early_exit. sec는 early_exit 때만, bossHp(0~100)는 보스 fail·early_exit 때만 싣는다
        public static void StageEnd(SaveData g, int stage, string result, bool boss, double score, double gained, double sec, double bossHpPct, List<string> newCodex)
        {
            if (SaveIO.Suspended) return;
            if (!inited) Init();
            var snap = LoadSnap() ?? new Snap();   // 기준이 없으면(첫 판·로그 도입 전 세이브) 0레벨부터 센다
            var up = new List<int[]>();
            double outGold = 0, outGem = 0;
            foreach (var kv in g.nodes)
            {
                int before = snap.nodes.TryGetValue(kv.Key, out var b) ? b : 0;
                if (kv.Value == before) continue;
                up.Add(new[] { UpgId(kv.Key), kv.Value });
                if (kv.Value > before && Defs.NODE.TryGetValue(kv.Key, out var n))
                    for (int L = before; L < kv.Value; L++) { var c = Game.NodeCost(n, L); outGold += c.gold; outGem += c.gem; }
            }
            var data = new Dictionary<string, object>
            {
                ["stage"] = stage, ["result"] = result, ["boss"] = boss, ["score"] = N(score),
                ["in"] = N(gained), ["out"] = N(outGold), ["bal"] = N(g.gold),
                ["up"] = up, ["codex"] = newCodex.Select(SpeciesNum).ToList(),
            };
            // 업그레이드·이번 판 말고 골드가 바뀐 몫(세계수 레벨 업·수확 보따리·공방 등 합계). 직전 잔액 + in − out + etc = bal
            if (snap.hasGold) { double etc = g.gold - (snap.gold + gained - outGold); if (Math.Abs(etc) >= 0.5) data["etc"] = N(etc); }
            if (outGem > 0) data["out_gem"] = N(outGem);
            data["gem"] = N(g.gem);
            if (result == "early_exit") data["sec"] = Math.Round(sec, 1);
            if (boss && result != "clear") data["boss_hp"] = Math.Round(bossHpPct, 1);
            Send("stage_end", data);
            PlayerPrefs.SetString(K_SNAP, JsonConvert.SerializeObject(new Snap { nodes = new Dictionary<string, int>(g.nodes), gold = g.gold, hasGold = true }));
            PlayerPrefs.Save();
            Flush();
        }

        // 노드 키 → DB 숫자 ID (Upgrade 시트 Upg_ID). 없으면 0
        static Dictionary<string, int> upgIds;
        static int UpgId(string key)
        {
            if (upgIds == null) { upgIds = new Dictionary<string, int>(); foreach (var u in Db.Data.Upgrade) upgIds[u.Upg_Key] = u.Upg_ID; }
            return upgIds.TryGetValue(key, out var id) ? id : 0;
        }

        // 버섯 종 id → 숫자 (식용 1·약용 2·독 3 × 100 + 번호. 예: ed12 → 112, ps0 → 300)
        public static int SpeciesNum(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 3) return 0;
            int cat = id.StartsWith("ed") ? 1 : id.StartsWith("md") ? 2 : id.StartsWith("ps") ? 3 : 9;
            return int.TryParse(id.Substring(2), out var i) ? cat * 100 + i : cat * 100 + 99;
        }

        // ===== 세계수 · 의뢰 =====
        // 세계수 성장(나이테·진화·꼬마 각성): 골드 out·bal, 균사석을 쓰면 out_gem·gem
        public static void TreeUp(string id, int level, double gold, double gem) =>
            Send("tree_up", Cost(new Dictionary<string, object> { ["id"] = id, ["lv"] = level }, gold, gem));
        // 세계수 건축(장식 제작)
        public static void TreeBuild(string id, double gold, double gem) =>
            Send("tree_build", Cost(new Dictionary<string, object> { ["id"] = id }, gold, gem));
        // 의뢰 보상은 세계수 경험치 (cur = exp, bal = 세계수 경험치)
        public static void QuestClear(string id, string kind, int exp) =>
            Send("quest_clear", new Dictionary<string, object> { ["id"] = id, ["kind"] = kind, ["cur"] = "exp", ["in"] = exp, ["bal"] = N(Game.G?.farm?.exp ?? 0) });

        static Dictionary<string, object> Cost(Dictionary<string, object> d, double gold, double gem)
        {
            d["out"] = N(gold); d["bal"] = N(Game.G?.gold ?? 0);
            if (gem > 0) { d["out_gem"] = N(gem); d["gem"] = N(Game.G?.gem ?? 0); }
            return d;
        }

        // ===== 초반 구간 (FTUE) =====
        // 단계마다 기기당 한 번만 보낸다. 번호는 FTUE_* 상수
        public const int FTUE_TITLE_START = 1, FTUE_FIRST_LAUNCH = 2, FTUE_FIRST_ROUND_END = 3, FTUE_FIRST_UPGRADE = 4, FTUE_FIRST_TREE = 5;
        public static void Ftue(int step)
        {
            if (SaveIO.Suspended) return;
            string done = PlayerPrefs.GetString(K_FTUE, ",");
            if (done.Contains("," + step + ",")) return;
            PlayerPrefs.SetString(K_FTUE, done + step + ",");
            Send("ftue", new Dictionary<string, object> { ["step"] = step });
        }

        // ===== 게임 클리어 (엔딩) — 기기당 한 번 =====
        public static void GameClear(SaveData g)
        {
            if (SaveIO.Suspended || PlayerPrefs.GetInt(K_CLEAR, 0) == 1) return;
            PlayerPrefs.SetInt(K_CLEAR, 1);
            Send("game_clear", new Dictionary<string, object>
            {
                ["play"] = N(g.play), ["sessions"] = PlayerPrefs.GetInt(K_SESS, 1), ["stage"] = g.stage, ["rounds"] = g.rounds, ["bal"] = N(g.gold),
                ["up"] = g.nodes.Where(kv => kv.Value > 0).Select(kv => new[] { UpgId(kv.Key), kv.Value }).ToList(),
            });
            Flush();
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
            Send("error", new Dictionary<string, object> { ["msg"] = msg != null && msg.Length > 300 ? msg.Substring(0, 300) : msg, ["at"] = where.Length > 200 ? where.Substring(0, 200) : where, ["type"] = type.ToString() });
        }

        // 재화 숫자: long에 들어가면 정수로(9876), 아주 크면 double 그대로(1.2E+20)
        static object N(double v) { v = Math.Round(v); return Math.Abs(v) < 9e15 ? (object)(long)v : v; }

        // ===== 전송 =====
        internal static IEnumerator Pump()
        {
            while (true)
            {
                if (!flushWanted || !CanSend || queue.Count == 0) { yield return null; continue; }
                flushWanted = false;
                int attempt = 0;
                while (queue.Count > 0 && CanSend)
                {
                    // 앞에서부터 50건·20KB 안으로 묶는다
                    var sb = new StringBuilder("[");
                    int n = 0;
                    for (; n < queue.Count && n < BATCH_MAX; n++)
                    {
                        int len = Encoding.UTF8.GetByteCount(queue[n]) + 1;
                        if (n > 0 && sb.Length + len + 1 > BATCH_BYTES) break;
                        if (n > 0) sb.Append(',');
                        sb.Append(queue[n]);
                    }
                    sb.Append(']');
                    string target = url + (url.Contains("?") ? "&" : "?") + "k=" + UnityWebRequest.EscapeURL(key);
                    using (var req = new UnityWebRequest(target, "POST"))
                    {
                        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sb.ToString()));
                        req.downloadHandler = new DownloadHandlerBuffer();
                        req.SetRequestHeader("Content-Type", "application/json");
                        req.timeout = 15;
                        yield return req.SendWebRequest();
                        string body = req.downloadHandler?.text?.Trim();
                        if (req.result == UnityWebRequest.Result.Success && body == "ok")
                        {
                            queue.RemoveRange(0, n); SaveQueue(); attempt = 0;
                            // 연결 실패는 횟수만 모아 두었다가 연결되면 한 번 보낸다 (세션당 error 한도와 별개)
                            int fails = PlayerPrefs.GetInt(K_NETFAIL, 0);
                            if (fails > 0) { PlayerPrefs.SetInt(K_NETFAIL, 0); Send("error", new Dictionary<string, object> { ["msg"] = "net_fail", ["count"] = fails }); }
                            continue;
                        }
                        long code = req.responseCode;
                        if (code == 403) { forbidden = true; Debug.LogWarning("[Telemetry] 서버가 키를 거부함(403) — LogConfig의 key 확인. 이번 실행에서는 더 보내지 않는다"); break; }
                        if (code == 400 || code == 413)
                        {
                            // 형식·크기 문제: 묶음이면 한 건씩 다시 보내고, 한 건짜리면 그 로그만 버린다
                            if (n == 1) { queue.RemoveAt(0); SaveQueue(); continue; }
                            yield return SendOneByOne(n);
                            continue;
                        }
                        PlayerPrefs.SetInt(K_NETFAIL, PlayerPrefs.GetInt(K_NETFAIL, 0) + 1); PlayerPrefs.Save();
                        if (attempt >= RETRY.Length) break;   // 3번 다시 해도 안 되면 다음 전송 때 함께
                        yield return new WaitForSecondsRealtime(RETRY[attempt++]);
                    }
                }
            }
        }

        // 묶음이 400·413이면 앞의 n건을 한 건씩 보낸다 (깨진 한 건만 버리고 나머지는 살린다)
        static IEnumerator SendOneByOne(int n)
        {
            for (int i = 0; i < n && queue.Count > 0; i++)
            {
                string target = url + (url.Contains("?") ? "&" : "?") + "k=" + UnityWebRequest.EscapeURL(key);
                using (var req = new UnityWebRequest(target, "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(queue[0]));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.timeout = 15;
                    yield return req.SendWebRequest();
                    long code = req.responseCode;
                    if ((req.result == UnityWebRequest.Result.Success && req.downloadHandler.text.Trim() == "ok") || code == 400 || code == 413) { queue.RemoveAt(0); SaveQueue(); }
                    else yield break;   // 연결 문제면 나머지는 다음에
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

        // 창이 뒤로 갈 때(웹은 탭 숨김·포커스 잃음) 보낸다
        void OnApplicationPause(bool paused) { Telemetry.MarkRunning(!paused); if (paused) Telemetry.Flush(); }
        void OnApplicationFocus(bool focus) { Telemetry.MarkRunning(focus); if (!focus) Telemetry.Flush(); }
        void OnApplicationQuit() => Telemetry.MarkRunning(false);
    }
}
