using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using AOT;

namespace MoreMush
{
    // 분석용 로그 (로그 서버 인수인계 HANDOFF.md 4·5절). 웹·exe 공통. 게임 데이터는 서버에 올리지 않는다.
    //  · 로그는 먼저 로컬 큐(persistentDataPath/logq.dat, SecureStore 암호화)에 쌓고, 접속 직후·첫 단계(FTUE 1·2)·스테이지 종료·세계수를 나갈 때 묶어서 보낸다.
    //    앱이 뒤로 갈 때(모바일 홈·웹 탭 숨김·닫기)는 다음 프레임이 안 올 수 있어 그 자리에서 보낸다 (Background · SendNow, 웹은 sendBeacon)
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
                     K_FTUE = "tlm_ftue", K_CLEAR = "tlm_clear_sent", K_SNAP = "tlm_snap", K_SEQ = "tlm_seq", K_STAGE_PEND = "tlm_stage_pend", K_STAGE_POST = "tlm_stage_post";

        public static string Uid => DeviceId.Get();
        public static bool FirstRun { get; private set; }
        public static string SessionId { get; private set; }
        static string url, key;
        static float t0;
        static bool inited, flushWanted, forbidden, background;
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
            LoadTree(); TreeCommit();   // 세계수에서 꺼져 못 보낸 묶음 → 이번 접속 전송에 함께
            int sessions = PlayerPrefs.GetInt(K_SESS, 0) + 1;
            PlayerPrefs.SetInt(K_SESS, sessions);

            // 지난 실행이 "실행 중" 표시를 지우지 못하고 끝났으면 비정상 종료로 본다 (죽는 순간에는 못 보내므로 사후 전송)
            if (RunFlag)
                Send("crash", new Dictionary<string, object> { ["stage"] = PlayerPrefs.GetInt(K_LAST, 0), ["ver"] = PlayerPrefs.GetString(K_RUN + "_ver", BuildTag.Version) });
            MarkRunning(true);

            var d = new Dictionary<string, object> { ["first"] = FirstRun, ["sessions"] = sessions };
            var bal = SaveIO.PeekGold();   // 아직 세이브를 불러오기 전(타이틀)이라 파일에서 잔액만 읽는다
            if (bal.HasValue) d["bal"] = N(bal.Value);
            Send("session_start", d);
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            StageCommitAll();   // 지난 실행에서 보내지 못한 스테이지 묶음
            TelemetryRunner.Ensure();
#if UNITY_WEBGL && !UNITY_EDITOR
            MMM_HookVisibility(OnPageHidden, OnPageShown);
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
            MMM_RunSet(on ? 1 : 0);   // 웹 PlayerPrefs(IndexedDB) 저장은 탭이 닫히면 끝나지 못해 localStorage에 바로 쓴다
#endif
            PlayerPrefs.SetInt(K_RUN, on ? 1 : 0);
            if (on) PlayerPrefs.SetString(K_RUN + "_ver", BuildTag.Version);
            PlayerPrefs.Save();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static bool RunFlag => MMM_RunGet() == 1;
#else
        static bool RunFlag => PlayerPrefs.GetInt(K_RUN, 0) == 1;
#endif

        // 다음 프레임에 보낸다 (전송 시점: 접속 직후 · 첫 단계 · 스테이지 종료 · 세계수를 나갈 때)
        public static void Flush() => flushWanted = true;

        // ===== 앱이 뒤로 갈 때 · 돌아올 때 =====
        // 모바일 Pause·Focus, PC Focus, 웹 visibilitychange·pagehide가 겹쳐 오므로 상태가 바뀔 때 한 번만 처리한다
        public static void Background()
        {
            if (!inited || background) return;
            background = true;
            TreeCommit();    // 세계수 묶음 (세계수 화면은 그대로라 돌아오면 다시 모은다)
            StageCommitAll();   // 모으던 스테이지 구간 · 엔딩 뒤 합친 줄
            MarkRunning(false);
            SendNow();
        }

        public static void Foreground()
        {
            if (!inited || !background) return;
            background = false;
            MarkRunning(true);
        }

        public static void Send(string name, Dictionary<string, object> data)
        {
            if (SaveIO.Suspended) return;   // 밸런스 시뮬(BalanceSim 샌드박스)은 보내지 않는다
            if (!inited && name != "crash" && name != "session_start") Init();
            string d = data == null ? "{}" : JsonConvert.SerializeObject(data, Formatting.None);
            Enqueue(name, d, NowMs, T, SessionId, NextEid(), BuildTag.Version, true);
        }

        static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        static string NextEid()
        {
            long seq = PlayerPrefs.GetInt(K_SEQ, 0) + 1; PlayerPrefs.SetInt(K_SEQ, (int)(seq % int.MaxValue));
            return SessionId + "-" + seq;
        }

        // 큐에 한 건 넣는다. ts·t·session·eid·version은 로그가 생긴 때의 값 (세계수 묶음은 나중에 넣으므로 따로 받는다.
        // 같은 eid가 두 번 들어가도 서버가 INSERT OR IGNORE로 한 번만 남긴다)
        static void Enqueue(string name, string d, long ts, double t, string session, string eid, string version, bool save)
        {
            if (d.Length > DATA_MAX) { Debug.LogWarning($"[Telemetry] {name} data {d.Length}자 > {DATA_MAX} — 잘라서 JSON이 깨지지 않게 표시만 남긴다"); d = JsonConvert.SerializeObject(new { truncated = d.Length }); }
            var entry = new JObject
            {
                ["game"] = GAME, ["version"] = version ?? BuildTag.Version, ["platform"] = Platform, ["uid"] = Uid, ["session"] = session ?? SessionId,
                ["event"] = name, ["t"] = t, ["eid"] = eid ?? NextEid(),
                ["ts"] = ts,   // 기기 시각 (큐에 오래 있다 가면 서버 시각과 다르다)
                ["data"] = JObject.Parse(d),
            };
            if (Application.isEditor) { Debug.Log($"[Telemetry] {name} {d}"); if (!SEND_IN_EDITOR) return; }
            queue.Add(entry.ToString(Formatting.None));
            while (queue.Count > QUEUE_MAX) queue.RemoveAt(0);
            if (save) SaveQueue();
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
            StageSend(stage, result, boss, g.ending, data);
            PlayerPrefs.SetString(K_SNAP, JsonConvert.SerializeObject(new Snap { nodes = new Dictionary<string, int>(g.nodes), gold = g.gold, hasGold = true }));
            PlayerPrefs.Save();
            Flush();
        }

        // ===== 스테이지 구간 묶음 =====
        // STAGE_SOLO_MAX 스테이지까지는 판마다 보낸다. 그 뒤로는 보스 구간(21~30, 31~40 …)의 클리어를 모아 두었다가
        // 보스를 클리어하면 구간을 한 줄로 보낸다 (D1 행 절약). 보스 실패·중도 포기(early_exit)는 그 판만 바로 보낸다 (모으던 것은 계속 기다린다).
        // 합친 줄: stage·result·boss·bal·gem·score = 마지막 판, stage0·score0 = 첫 판, in·out·etc·out_gem = 합계, up = 나중 레벨,
        //   codex = 이어 붙임, c = 합친 판 수. 직전 잔액 + in − out + etc = bal 은 합친 줄에서도 맞는다.
        // 모으는 중인 줄은 PlayerPrefs에 두고, 앱이 뒤로 갈 때·다음 접속 때 모은 만큼 보낸다. 시각(ts·t)은 마지막 판 기준.
        // 엔딩을 본 유저(ending)는 판마다·구간마다 보내지 않고 세션 동안 한 줄에 모두 합쳐(post = 1, nf = 실패·중도 포기 수)
        //   앱이 뒤로 갈 때·다음 접속 때만 보낸다 → 최종 스테이지와 그동안의 합계만 남는다
        public const int STAGE_SOLO_MAX = 20;
        static readonly string[] STAGE_SUM = { "in", "out", "etc", "out_gem" };

        static void StageSend(int stage, string result, bool boss, bool ending, Dictionary<string, object> data)
        {
            var d = JObject.FromObject(data);
            if (ending)
            {
                StageCommit();   // 엔딩 직전까지 모으던 구간
                var post = LoadPend(K_STAGE_POST);
                var m = post == null ? StageStart(d) : StageMerge(post, d);
                int nf = (int)(post?["nf"] ?? 0) + (result == "clear" ? 0 : 1);
                if (nf > 0) m["nf"] = nf;
                m["post"] = 1; m["_ts"] = NowMs; m["_t"] = T;
                PlayerPrefs.SetString(K_STAGE_POST, m.ToString(Formatting.None));
                return;
            }
            // 보스 실패·중도 포기 전에 모으던 구간을 먼저 보낸다: 그 줄의 bal에는 모은 판이 이미 들어 있어서, 기기 시각 순서로 잔액 검사가 맞으려면 앞서야 한다
            if (stage <= STAGE_SOLO_MAX || result != "clear") { StageCommit(); Enqueue("stage_end", d); Flush(); return; }
            var pend = LoadPend(K_STAGE_PEND);
            if (pend != null && Block((int)pend["stage"]) != Block(stage)) { StageCommit(); pend = null; }   // 구간이 바뀌었는데 남은 것 (새 게임 등)
            var o = pend == null ? StageStart(d) : StageMerge(pend, d);
            o["_ts"] = NowMs; o["_t"] = T;
            if (!boss) { PlayerPrefs.SetString(K_STAGE_PEND, o.ToString(Formatting.None)); return; }
            PlayerPrefs.DeleteKey(K_STAGE_PEND);
            EnqueueStage(o);
            Flush();
        }

        static int Block(int stage) => (stage - 1) / Math.Max(1, Defs.BOSS_EVERY);

        // 모으기 시작: eid·세션·버전을 이때 정해 둔다 (탭이 닫혀 저장이 덜 끝나 다음 접속에 다시 보내도 같은 eid라 서버가 한 줄만 남긴다)
        static JObject StageStart(JObject d)
        {
            d["_eid"] = NextEid(); d["_s"] = SessionId; d["_v"] = BuildTag.Version;
            return d;
        }

        static JObject StageMerge(JObject a, JObject b)
        {
            var o = (JObject)b.DeepClone();
            o["_eid"] = a["_eid"]; o["_s"] = a["_s"]; o["_v"] = a["_v"];
            o["stage0"] = a["stage0"] ?? a["stage"]; o["score0"] = a["score0"] ?? a["score"];
            o["c"] = (int)(a["c"] ?? 1) + 1;
            foreach (var f in STAGE_SUM)
                if (a[f] != null || b[f] != null) o[f] = JToken.FromObject(N((double)(a[f] ?? 0) + (double)(b[f] ?? 0)));
            var up = new Dictionary<int, int>();
            foreach (var u in ((JArray)a["up"] ?? new JArray()).Concat((JArray)b["up"] ?? new JArray())) up[(int)u[0]] = (int)u[1];
            o["up"] = new JArray(up.Select(kv => new JArray(kv.Key, kv.Value)));
            o["codex"] = new JArray(((JArray)a["codex"] ?? new JArray()).Concat((JArray)b["codex"] ?? new JArray()));
            return o;
        }

        static JObject LoadPend(string key)
        {
            string js = PlayerPrefs.GetString(key, "");
            if (js.Length == 0) return null;
            try { return JObject.Parse(js); }
            catch (Exception e) { Debug.LogWarning("[Telemetry] 스테이지 묶음을 읽지 못함: " + e.Message); PlayerPrefs.DeleteKey(key); return null; }
        }

        // 모으던 구간을 모은 만큼 보낸다
        static void StageCommit() => StageCommit(K_STAGE_PEND);

        // 앱이 뒤로 갈 때·다음 접속 때: 모으던 구간과 엔딩 뒤 합친 줄
        static void StageCommitAll() { StageCommit(K_STAGE_PEND); StageCommit(K_STAGE_POST); }

        static void StageCommit(string key)
        {
            var pend = LoadPend(key);
            if (pend == null) return;
            PlayerPrefs.DeleteKey(key);
            EnqueueStage(pend);
        }

        static void Enqueue(string name, JObject d) => Enqueue(name, d.ToString(Formatting.None), NowMs, T, SessionId, NextEid(), BuildTag.Version, true);

        // 모은 줄은 마지막 판의 시각, 모으기 시작할 때 정한 eid·세션·버전으로 넣는다
        static void EnqueueStage(JObject o)
        {
            long ts = (long?)o["_ts"] ?? NowMs; double t = (double?)o["_t"] ?? T;
            string eid = (string)o["_eid"], session = (string)o["_s"], version = (string)o["_v"];
            foreach (var k in new[] { "_ts", "_t", "_eid", "_s", "_v" }) o.Remove(k);
            Enqueue("stage_end", o.ToString(Formatting.None), ts, t, session ?? SessionId, eid ?? NextEid(), version ?? BuildTag.Version, true);
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
        //   반복되는 레벨업(나이테 ring · 꼬마 klv_ · 장식 deco_)은 세계수 묶음에서 한 줄로 합친다 (TreeBegin 참고)
        public static void TreeUp(string id, int level, double gold, double gem)
        {
            bool repeat = id == "ring" || id.StartsWith("klv_") || id.StartsWith("deco_");
            Tree("tree_up", repeat ? "up:" + id : null, Cost(new Dictionary<string, object> { ["id"] = id, ["lv"] = level }, gold, gem));
        }
        // 세계수 건축(장식 제작)
        public static void TreeBuild(string id, double gold, double gem) =>
            Tree("tree_build", null, Cost(new Dictionary<string, object> { ["id"] = id }, gold, gem));
        // 재화 소모가 없는 세계수 활동: 공물(in=경험치) · 선물(in=호감도 점수, n=개수) · 공방 즉시 완료(out_gem=균사석). 같은 활동·대상은 한 줄로 합친다
        public static void TreeAct(string act, string id, double gain, int n, double gem)
        {
            var d = new Dictionary<string, object> { ["act"] = act, ["in"] = N(gain), ["n"] = n };
            if (id != null) d["id"] = id;
            Tree("tree_act", "act:" + act + ":" + id, Cost(d, 0, gem));
        }
        // 의뢰 보상은 세계수 경험치 (cur = exp, bal = 세계수 경험치)
        public static void QuestClear(string id, string kind, int exp) =>
            Tree("quest_clear", null, new Dictionary<string, object> { ["id"] = id, ["kind"] = kind, ["cur"] = "exp", ["in"] = exp, ["bal"] = N(Game.G?.farm?.exp ?? 0) });

        // ===== 세계수 묶음 =====
        // 세계수 화면에 있는 동안(TreeBegin ~ TreeEnd) 세계수 로그는 큐에 넣지 않고 모아 둔다. 같은 key는 한 줄로 합친다:
        //   합계 out·out_gem·in·n, 마지막 값 lv·bal·gem, 첫 레벨 lv0, 합친 횟수 c. 세계수 화면을 벗어날 때 큐에 넣고 한 번에 보낸다.
        //   모으는 동안 기기 파일(logtree.dat)에도 저장해, 세계수에서 앱이 꺼지면 다음 접속 때 큐에 넣어 접속 전송에 함께 보낸다
        class Pending { public string key, ev, session, eid, version; public long ts; public double t; public JObject d; }
        static readonly string[] SUM_FIELDS = { "out", "out_gem", "in", "n" }, LAST_FIELDS = { "lv", "bal", "gem" };
        static List<Pending> tree = new List<Pending>();
        static bool treeOpen;

        public static void TreeBegin()
        {
            if (SaveIO.Suspended) return;
            if (!inited) Init();
            TreeCommit();
            treeOpen = true;
        }

        public static void TreeEnd()
        {
            if (!treeOpen) return;
            treeOpen = false;
            TreeCommit();
            Flush();
        }

        static void Tree(string ev, string key, Dictionary<string, object> data)
        {
            if (!treeOpen) { Send(ev, data); return; }
            if (SaveIO.Suspended) return;
            var d = JObject.FromObject(data);
            var p = key == null ? null : tree.Find(x => x.key == key);
            if (p == null)
            {
                tree.Add(new Pending { key = key, ev = ev, session = SessionId, eid = NextEid(), version = BuildTag.Version, ts = NowMs, t = T, d = d });
                while (tree.Count > QUEUE_MAX) tree.RemoveAt(0);
            }
            else Merge(p, d);
            SaveTree();
        }

        static void Merge(Pending p, JObject d)
        {
            var o = p.d;
            if (o["lv"] != null && o["lv0"] == null) o["lv0"] = o["lv"];
            foreach (var f in SUM_FIELDS)
                if (d[f] != null) o[f] = JToken.FromObject(N((double)(o[f] ?? 0) + (double)d[f]));
            foreach (var f in LAST_FIELDS)
                if (d[f] != null) o[f] = d[f];
            o["c"] = (int)(o["c"] ?? 1) + 1;
            p.ts = NowMs; p.t = T;
        }

        static void TreeCommit()
        {
            if (tree.Count > 0)
            {
                foreach (var p in tree) Enqueue(p.ev, p.d.ToString(Formatting.None), p.ts, p.t, p.session, p.eid, p.version, false);
                tree.Clear();
                SaveQueue();
            }
            SaveTree();
        }

        static string TreePath => Path.Combine(Application.persistentDataPath, "logtree.dat");

        static void SaveTree()
        {
            try
            {
                if (tree.Count == 0) { if (File.Exists(TreePath)) File.Delete(TreePath); }
                else SecureStore.Save(TreePath, Uid, JsonConvert.SerializeObject(tree));
            }
            catch (Exception e) { Debug.LogWarning("[Telemetry] 세계수 묶음을 저장하지 못함: " + e.Message); }
        }

        // 지난 실행에서 세계수 화면에 있다가 꺼져 못 보낸 묶음
        static void LoadTree()
        {
            tree = new List<Pending>();
            try
            {
                if (SecureStore.Load(TreePath, Uid, out _, out var json))
                    tree = JsonConvert.DeserializeObject<List<Pending>>(json) ?? new List<Pending>();
            }
            catch (Exception e) { Debug.LogWarning("[Telemetry] 세계수 묶음을 읽지 못함: " + e.Message); }
        }

        static Dictionary<string, object> Cost(Dictionary<string, object> d, double gold, double gem)
        {
            d["out"] = N(gold); d["bal"] = N(Game.G?.gold ?? 0);
            if (gem > 0) { d["out_gem"] = N(gem); d["gem"] = N(Game.G?.gem ?? 0); }
            return d;
        }

        // ===== 초반 구간 (FTUE) =====
        // 단계마다 기기당 한 번만 보낸다. 번호는 FTUE_* 상수.
        // 2단계부터는 이 기기에서 1단계(새 게임)를 보낸 경우만: 로그 도입 전부터 하던 유저가 퍼널 중간에 끼지 않게.
        // 1·2단계는 바로 보낸다: 첫 판이 끝나기 전에 떠나는 유저도 퍼널에 잡히게 (3단계는 stage_end와 함께 간다)
        public const int FTUE_TITLE_START = 1, FTUE_FIRST_LAUNCH = 2, FTUE_FIRST_ROUND_END = 3, FTUE_FIRST_UPGRADE = 4, FTUE_FIRST_TREE = 5;
        public static void Ftue(int step)
        {
            if (SaveIO.Suspended) return;
            string done = PlayerPrefs.GetString(K_FTUE, ",");
            if (done.Contains("," + step + ",")) return;
            if (step != FTUE_TITLE_START && !done.Contains("," + FTUE_TITLE_START + ",")) return;
            PlayerPrefs.SetString(K_FTUE, done + step + ",");
            PlayerPrefs.Save();   // 저장 전에 꺼지면 다음 실행에서 같은 단계를 또 보낸다
            Send("ftue", new Dictionary<string, object> { ["step"] = step });
            if (step <= FTUE_FIRST_LAUNCH) Flush();
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
                    var sent = Batch(out string body);
                    int n = sent.Count;
                    using (var req = new UnityWebRequest(Target, "POST"))
                    {
                        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                        req.downloadHandler = new DownloadHandlerBuffer();
                        req.SetRequestHeader("Content-Type", "application/json");
                        req.timeout = 15;
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success && req.downloadHandler?.text?.Trim() == "ok")
                        {
                            Remove(sent); attempt = 0;
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

        static string Target => url + (url.Contains("?") ? "&" : "?") + "k=" + UnityWebRequest.EscapeURL(key);

        // 앞에서부터 50건·20KB(바이트) 안으로 묶는다. 묶은 항목을 돌려준다
        static List<string> Batch(out string body)
        {
            var sb = new StringBuilder("[");
            var items = new List<string>();
            int bytes = 2;
            for (int i = 0; i < queue.Count && i < BATCH_MAX; i++)
            {
                int len = Encoding.UTF8.GetByteCount(queue[i]) + 1;
                if (i > 0 && bytes + len > BATCH_BYTES) break;
                if (i > 0) sb.Append(',');
                sb.Append(queue[i]); bytes += len; items.Add(queue[i]);
            }
            body = sb.Append(']').ToString();
            return items;
        }

        // 보낸 항목을 위치가 아니라 값으로 지운다 (보내는 사이 큐가 200을 넘어 앞이 밀려도 안 보낸 것을 지우지 않게)
        static void Remove(List<string> sent)
        {
            var set = new HashSet<string>(sent);
            queue.RemoveAll(set.Contains);
            SaveQueue();
        }

        // 그 자리에서 한 묶음 보낸다 (뒤로 갈 때). 웹은 탭이 닫혀도 가는 sendBeacon이라 결과를 모르므로 큐에 남긴다
        // (다음 전송 때 같은 eid로 다시 가도 서버가 한 줄만 남긴다). 그 밖에는 응답이 오면 큐에서 지운다
        static void SendNow()
        {
            if (!CanSend || queue.Count == 0) return;
            var sent = Batch(out string body);
#if UNITY_WEBGL && !UNITY_EDITOR
            MMM_Beacon(Target, body);
#else
            var req = new UnityWebRequest(Target, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 15,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SendWebRequest().completed += _ =>
            {
                if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.text.Trim() == "ok") Remove(sent);
                req.Dispose();
            };
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Assets/Plugins/WebGL/MMMTelemetry.jslib
        [DllImport("__Internal")] static extern int MMM_Beacon(string url, string body);
        [DllImport("__Internal")] static extern void MMM_HookVisibility(Action onHidden, Action onShown);
        [DllImport("__Internal")] static extern void MMM_RunSet(int on);
        [DllImport("__Internal")] static extern int MMM_RunGet();
        [MonoPInvokeCallback(typeof(Action))] static void OnPageHidden() => Background();
        [MonoPInvokeCallback(typeof(Action))] static void OnPageShown() => Foreground();
#endif

        // 묶음이 400·413이면 앞의 n건을 한 건씩 보낸다 (깨진 한 건만 버리고 나머지는 살린다)
        static IEnumerator SendOneByOne(int n)
        {
            for (int i = 0; i < n && queue.Count > 0; i++)
            {
                using (var req = new UnityWebRequest(Target, "POST"))
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

        // 앱이 뒤로 가거나 꺼질 때 그 자리에서 보낸다 (웹 탭 숨김·닫기는 jslib의 visibilitychange·pagehide가 따로 부른다)
        void OnApplicationPause(bool paused) { if (paused) Telemetry.Background(); else Telemetry.Foreground(); }
        void OnApplicationFocus(bool focus) { if (focus) Telemetry.Foreground(); else Telemetry.Background(); }
        void OnApplicationQuit() => Telemetry.Background();
    }
}
