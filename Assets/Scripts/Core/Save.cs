using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace MoreMush
{
    // Save data (prototype newSave()). Stored as JSON in PlayerPrefs, which maps to IndexedDB on WebGL.
    [Serializable]
    public class SaveData
    {
        public int v = SaveMigration.CURRENT;
        public double gold, gem;                   // gem 균사석: 노드·꼬마·건물·스킨에 쓴다
        // v2 세이브의 다이아몬드. 읽기만 하고(SaveMigration이 균사석으로 바꿈) 다시 저장하지 않는다
        [JsonProperty("dia")] public double legacyDia;
        public bool ShouldSerializelegacyDia() => false;
        // v5 세이브의 자동 수확 보상 시각 (ms). 읽기만 하고(SaveMigration이 수확 보따리 시계로 옮김) 다시 저장하지 않는다
        [JsonProperty("autoT")] public double legacyAutoT;
        public bool ShouldSerializelegacyAutoT() => false;
        public double seenT;                       // 마지막으로 게임 안에 있던 시각 (ms, 복귀 팝업용, 0 = 기록 없음)
        public Skins skins = new Skins();
        public Dictionary<string, int> nodes = new Dictionary<string, int>();
        public Dictionary<string, bool> seeds = new Dictionary<string, bool>();
        public Dictionary<string, CodexEntry> codex = new Dictionary<string, CodexEntry>();
        public Dictionary<string, bool> specials = new Dictionary<string, bool>();
        public Dictionary<string, int> hv = new Dictionary<string, int> { ["sam"] = 1 };
        public Dictionary<string, bool> hvOn = new Dictionary<string, bool> { ["sam"] = true };
        public string theme = "forest";
        public Farm farm = new Farm();             // 세계수 (목장·밭)
        public Quests quest = new Quests();        // 일일·주간 의뢰 (예전 마을 의뢰 "quests"와는 다른 필드)
        public Records rec = new Records();
        public int rounds;
        public int stage = 1;                      // 지금 판 번호 (내부, 튜토리얼 T1~T4 포함). 표시 스테이지 = stage − Defs.TUT_ROUNDS. 보스 스테이지(표시 10의 배수)는 보스를 쓰러뜨려야 넘어간다
        public bool bossOn = true;                 // 보스 도전 (끄면 보스 스테이지에서 파밍만 한다. 보스에게 지면 꺼진다)
        public double play;
        public double lastNormalGold;              // 보스가 안 나온 마지막 판의 정산 골드 (보스 처치 보상 기준)
        public double bagGoldPerSec;               // 일반 판 골드/초 평활값 (수확 보따리 골드 기준, Game.BagBaseUpdate)
        public bool ending;
        // 튜토리얼 (기획서 Ver.01): 0 첫 판(1스테이지) 전 · 1 1스테이지 클리어, 첫 노드 사기 · 2 첫 노드 산 뒤 "수확하러 가기" 유도 · 3 끝
        public int tut;
        public Dictionary<string, bool> feat = new Dictionary<string, bool>();   // 열린 기능 (Unlock). 한 번 열리면 남는다
        public bool bossFailed;                    // 보스에게 한 번이라도 졌다 (보스 도전 버튼 해금)

        [Serializable] public class Skins { public Dictionary<string, bool> own = new Dictionary<string, bool>(); public Dictionary<string, string> ch = new Dictionary<string, string>(), hv = new Dictionary<string, string>(); }
        // 꼬마버섯 세계수 (Defs.WT·Game.Farm). stage 성장 단계 1~8 · ring 나이테 Lv.1~10 · exp 경험치(공물·의뢰)
        // zone 꼬마가 일하는 구역(없으면 쉼) · klv 꼬마 레벨 · awaken 각성 ★ · aff 호감도 점수(누적) · store 창고 버섯(종 id → 개수)
        // bag 수확 보따리 · crafts 공방 제작 중 · decoStar 가진 장식의 강화 ★(1~5) · decoDup 강화 재료로 쌓인 같은 장식 · decoSlots 장식 슬롯마다 놓은 장식
        // decoFirst 처음 배치 보상을 받은 장식 · reqs 오늘의 부탁 · weekDone 이번 주 완료한 부탁 수 · rushUsed 오늘 즉시 완료 횟수
        [Serializable]
        public class Farm
        {
            public int stage = 1, ring = 1;
            public double exp;
            public Dictionary<string, string> zone = new Dictionary<string, string>();
            public Dictionary<string, int> klv = new Dictionary<string, int>(), awaken = new Dictionary<string, int>();
            public Dictionary<string, double> aff = new Dictionary<string, double>();
            public Dictionary<string, int> store = new Dictionary<string, int>();
            public Bag bag = new Bag();
            public List<Craft> crafts = new List<Craft>();
            public Dictionary<string, int> decoStar = new Dictionary<string, int>(), decoDup = new Dictionary<string, int>();
            public List<string> decoSlots = new List<string>();
            public Dictionary<string, bool> decoFirst = new Dictionary<string, bool>();
            public List<Req> reqs = new List<Req>();
            public long reqDay = -1, reqWeek = -1, rushDay = -1;
            public int weekDone, rushUsed;
            public bool weekClaimed;

            // v5 세이브의 농장 (나무 레벨·건물·꼬마 별). SaveMigration이 읽고 다시 저장하지 않는다
            [JsonProperty("tree")] public LegacyTree legacyTree;
            [JsonProperty("blds")] public List<Bld> legacyBlds;
            [JsonProperty("star")] public Dictionary<string, int> legacyStar;
            public bool ShouldSerializelegacyTree() => false;
            public bool ShouldSerializelegacyBlds() => false;
            public bool ShouldSerializelegacyStar() => false;
        }
        // 수확 보따리: gold·gem 쌓인 양 · sec 쌓인 시간(초, 최대 보관 시간까지) · t 마지막으로 쌓은 시각(ms, 0 = 아직) · n 수확한 나무버섯 개수
        [Serializable] public class Bag { public double gold, gem, sec, t, n; }
        // 제작: deco 장식 id · t0 시작 · at 완성 시각(ms)
        [Serializable] public class Craft { public string deco; public double t0, at; }
        // 꼬마 부탁: kid 꼬마 · sp 버섯 종 · n 개수 · done 들어줌
        [Serializable] public class Req { public string kid, sp; public int n; public bool done; }
        [Serializable] public class LegacyTree { public int lv = 1; public double exp; }
        // 의뢰: day·week 마지막으로 뽑은 KST 날짜·주 번호 (-1 = 아직) · 의뢰마다 진행도
        [Serializable] public class Quests { public long day = -1, week = -1; public List<QuestProg> daily = new List<QuestProg>(), weekly = new List<QuestProg>(); }
        // id 의뢰 정의 · arg 지정한 버섯 id (species) · prog 진행도 · claimed 보상 받음
        [Serializable] public class QuestProg { public string id, arg; public int prog; public bool claimed; }
        // v5 세이브의 건물 (환불용으로 id만 읽는다)
        [Serializable] public class Bld { public string id; }
        [Serializable] public class CodexEntry { public double n; public int first; public bool gold, giant; }

        [Serializable]
        public class Records
        {
            public double score, combo, harvest, balls, chain;
            public double Get(string k) => k switch { "score" => score, "combo" => combo, "harvest" => harvest, "balls" => balls, _ => chain };
            public void Set(string k, double v)
            {
                switch (k) { case "score": score = v; break; case "combo": combo = v; break; case "harvest": harvest = v; break; case "balls": balls = v; break; default: chain = v; break; }
            }
        }
    }

    // 세이브: persistentDataPath/save.dat 에 암호화해 둔다 (SecureStore: AES + HMAC, 직전 정상본 .bak).
    // 웹은 persistentDataPath가 브라우저 IndexedDB라 템플릿의 autoSyncPersistentDataPath로 바로 반영된다.
    // 예전 세이브(PlayerPrefs 평문 JSON, SAVE_KEY)는 읽을 수 있으면 읽고, 다음 저장 때 파일로 옮긴 뒤 지운다.
    // 웹은 persistentDataPath가 페이지 URL마다 달라(itch.io는 올릴 때마다 주소가 바뀜) 새 버전에서 세이브가 안 보였다 (2026-10-20):
    //   저장할 때 같은 암호 묶음을 브라우저 localStorage(BrowserStore)에도 두고, 불러올 때 파일·사본 가운데 더 진행한(플레이 시간) 것을 쓴다.
    //   이 수정 전 버전에서 넘어온 경우를 위해 처음 한 번은 같은 브라우저 저장소의 다른 주소 폴더(예전 버전)에서 세이브를 찾는다
    public static class SaveIO
    {
        public const string SAVE_KEY = "mushroomPinball_save_v2";   // 예전 평문 세이브 (옮기기 전)
        public const string BAD_KEY = SAVE_KEY + "_bad";            // 읽지 못한 예전 세이브 원문
        public static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "save.dat");

        // true면 저장하지 않는다. 밸런스 봇(BalanceSim)이 돌 때 실제 세이브를 건드리지 않게 한다.
        public static bool Suspended;
        // 마지막 Load()가 세이브를 읽지 못해 null을 돌려줬는지 (세이브가 아예 없을 때는 false)
        public static bool LastLoadFailed { get; private set; }

        public static void Save(SaveData g)
        {
            if (g == null || Suspended) return;
            try
            {
                string uid = DeviceId.Get(), json = JsonConvert.SerializeObject(g);
                SecureStore.Save(SavePath, uid, json);
                if (BrowserStore.Available && BrowserStore.Set(BrowserStore.SAVE, uid + ":" + Convert.ToBase64String(SecureStore.Pack(uid, json))))
                    BrowserStore.Set(BrowserStore.LEGACY_SCANNED, "1");   // 사본이 생겼으니 예전 폴더는 더 찾지 않는다
                if (PlayerPrefs.HasKey(SAVE_KEY)) { PlayerPrefs.DeleteKey(SAVE_KEY); PlayerPrefs.Save(); }   // 예전 평문 세이브는 옮겼으니 지운다
            }
            catch (Exception e) { Debug.LogError("Save failed: " + e); }   // 저장 실패가 라운드·버튼 처리 중간을 끊지 않게
        }

        public static bool HasSave() => SecureStore.Exists(SavePath) || PlayerPrefs.HasKey(SAVE_KEY) || BrowserStore.Get(BrowserStore.SAVE) != null || LegacySaves().Count > 0;

        public static void Wipe()
        {
            try { SecureStore.Delete(SavePath); } catch (Exception e) { Debug.LogError("Save wipe failed: " + e); }
            PlayerPrefs.DeleteKey(SAVE_KEY); PlayerPrefs.Save();
            BrowserStore.Delete(BrowserStore.SAVE);
            BrowserStore.Set(BrowserStore.LEGACY_SCANNED, "1");   // 새로 시작했으니 예전 버전 세이브를 되살리지 않는다
        }

        // 브라우저 사본 ("uid:base64 암호 묶음") → (json, uid). 없거나 못 읽으면 null
        static (string json, string uid)? BrowserCopy()
        {
            var v = BrowserStore.Get(BrowserStore.SAVE);
            int c = v?.IndexOf(':') ?? -1;
            if (c <= 0) return null;
            try
            {
                string uid = v.Substring(0, c);
                if (SecureStore.Unpack(Convert.FromBase64String(v.Substring(c + 1)), uid, out _, out var json)) return (json, uid);
            }
            catch (Exception e) { Debug.LogWarning("browser save copy unreadable: " + e.Message); }
            return null;
        }

        // 같은 브라우저 저장소의 다른 주소 폴더(예전 버전)에 남은 세이브: (세이브 경로, 그 폴더의 uid). 웹에서 처음 한 번만 찾는다
        static List<(string path, string uid)> LegacySaves()
        {
            var o = new List<(string, string)>();
            if (!BrowserStore.Available || BrowserStore.Get(BrowserStore.LEGACY_SCANNED) == "1") return o;
            try
            {
                string mine = System.IO.Path.GetFullPath(Application.persistentDataPath).TrimEnd('/', '\\');
                string root = System.IO.Path.GetDirectoryName(mine);
                if (string.IsNullOrEmpty(root) || !System.IO.Directory.Exists(root)) return o;
                foreach (var dir in System.IO.Directory.GetDirectories(root))
                {
                    if (System.IO.Path.GetFullPath(dir).TrimEnd('/', '\\') == mine) continue;
                    string save = System.IO.Path.Combine(dir, "save.dat"), uidFile = System.IO.Path.Combine(dir, "uid.txt");
                    if (!SecureStore.Exists(save) || !System.IO.File.Exists(uidFile)) continue;
                    string uid = System.IO.File.ReadAllText(uidFile).Trim();
                    if (DeviceId.Valid(uid)) o.Add((save, uid));
                }
            }
            catch (Exception e) { Debug.LogWarning("legacy save scan failed: " + e.Message); }
            return o;
        }

        // 더 진행한 세이브 (플레이 시간 → 판 수 → 마지막 접속 시각)
        static bool Ahead(SaveData a, SaveData b) => b == null || a.play > b.play || (a.play == b.play && (a.rounds > b.rounds || (a.rounds == b.rounds && a.seenT > b.seenT)));

        public static SaveData Load()
        {
            LastLoadFailed = false;
            // 이 주소의 파일보다 더 진행한 브라우저 사본·예전 버전 세이브가 있으면 그것을 쓴다 (웹에서만 있다)
            SaveData other = null; string otherUid = null, from = null;
            if (BrowserCopy() is var bc && bc != null && FromJson(bc.Value.json) is SaveData b) { other = b; from = "browser copy"; }
            foreach (var (path, uid) in LegacySaves())
                if (SecureStore.Load(path, uid, out _, out var lj) && FromJson(lj) is SaveData l && Ahead(l, other)) { other = l; otherUid = uid; from = path; }
            if (BrowserStore.Available) BrowserStore.Set(BrowserStore.LEGACY_SCANNED, "1");
            if (otherUid != null) DeviceId.Adopt(otherUid);   // 처음 들어온 새 주소면 예전 ID를 이어 쓴다 (로그 uid가 끊기지 않게)

            if (SecureStore.Exists(SavePath))
            {
                if (SecureStore.Load(SavePath, DeviceId.Get(), out _, out var json) && FromJson(json) is SaveData g)
                {
                    if (other != null && Ahead(other, g)) { Debug.Log("Save restored from " + from); return other; }
                    return g;
                }
                // 셋(.tmp·본·.bak) 다 못 읽음: 다음 저장이 덮기 전에 본 파일을 .bad로 남긴다 (처음 실패한 것만)
                try { var bad = SavePath + ".bad"; if (!System.IO.File.Exists(bad) && System.IO.File.Exists(SavePath)) System.IO.File.Copy(SavePath, bad); }
                catch (Exception e) { Debug.LogError("Save backup failed: " + e); }
                if (other != null) { Debug.Log("Save restored from " + from); return other; }
                LastLoadFailed = true;
                return null;
            }
            if (other != null) { Debug.Log("Save restored from " + from); return other; }
            string s = PlayerPrefs.GetString(SAVE_KEY, null);   // 예전 평문 세이브
            if (string.IsNullOrEmpty(s)) return null;
            var old = FromJson(s);
            if (old == null)
            {
                LastLoadFailed = true;
                try { if (!PlayerPrefs.HasKey(BAD_KEY)) { PlayerPrefs.SetString(BAD_KEY, s); PlayerPrefs.Save(); } }
                catch (Exception e) { Debug.LogError("Save backup failed: " + e); }
            }
            return old;
        }

        // 세이브를 불러오지 않고 골드 잔액만 읽는다 (접속 로그 session_start의 bal). 없거나 못 읽으면 null
        public static double? PeekGold()
        {
            try
            {
                string j = null;
                if (SecureStore.Exists(SavePath)) { if (!SecureStore.Load(SavePath, DeviceId.Get(), out _, out j)) return null; }
                else j = BrowserCopy()?.json ?? PlayerPrefs.GetString(SAVE_KEY, null);
                return string.IsNullOrEmpty(j) ? null : (double?)Newtonsoft.Json.Linq.JObject.Parse(j)["gold"];
            }
            catch { return null; }
        }

        // JSON → 세이브 (정리·버전 올리기까지). 못 읽으면 null
        public static SaveData FromJson(string json)
        {
            try
            {
                var g = JsonConvert.DeserializeObject<SaveData>(json) ?? throw new JsonSerializationException("save root is null");
                Sanitize(g);
                return g;
            }
            catch (Exception e) { Debug.LogError("Save load failed: " + e); return null; }
        }

        // 빈 칸·범위 밖 값을 채우고 자른다. 올바른 세이브에는 아무것도 바꾸지 않는다.
        static void Sanitize(SaveData g)
        {
            g.nodes ??= new Dictionary<string, int>(); g.seeds ??= new Dictionary<string, bool>();
            g.codex ??= new Dictionary<string, SaveData.CodexEntry>();
            g.specials ??= new Dictionary<string, bool>();
            g.hv ??= new Dictionary<string, int> { ["sam"] = 1 }; g.hvOn ??= new Dictionary<string, bool>();
            g.rec ??= new SaveData.Records();
            g.skins ??= new SaveData.Skins();
            g.skins.own ??= new Dictionary<string, bool>(); g.skins.ch ??= new Dictionary<string, string>(); g.skins.hv ??= new Dictionary<string, string>();
            foreach (var k in new List<string>(g.codex.Keys)) if (g.codex[k] == null) g.codex.Remove(k);
            g.rounds = Math.Max(0, g.rounds);
            g.stage = Math.Max(1, g.stage);
            if (!IsFinite(g.gold)) g.gold = 0;
            if (!IsFinite(g.gem)) g.gem = 0;
            if (!IsFinite(g.legacyDia) || g.legacyDia < 0) g.legacyDia = 0;
            if (!IsFinite(g.lastNormalGold) || g.lastNormalGold < 0) g.lastNormalGold = 0;
            if (!IsFinite(g.bagGoldPerSec) || g.bagGoldPerSec < 0) g.bagGoldPerSec = 0;

            // prototype loadSave() migrations that still apply
            foreach (var id in g.hv.Keys) if (!g.hvOn.ContainsKey(id)) g.hvOn[id] = true;
            foreach (var n in Defs.NODES)
            {
                int hi = n.inf ? n.infCap : n.max;   // 싱크 노드는 레벨 상한이 없다 (가격이 넘치기 직전 레벨까지)
                if (g.nodes.TryGetValue(n.id, out var L) && (L > hi || L < 0)) g.nodes[n.id] = Math.Max(0, Math.Min(hi, L));
            }
            foreach (var id in new List<string>(g.hv.Keys)) g.hv[id] = Math.Max(1, Math.Min(5, g.hv[id]));
            if (!Defs.THEME.ContainsKey(g.theme ?? "")) g.theme = "forest";
            SanitizeFarm(g.farm ??= new SaveData.Farm());
            g.quest ??= new SaveData.Quests();
            g.feat ??= new Dictionary<string, bool>();
            g.tut = Math.Max(0, Math.Min(3, g.tut));
            g.quest.daily = g.quest.daily?.FindAll(QuestOk) ?? new List<SaveData.QuestProg>();
            g.quest.weekly = g.quest.weekly?.FindAll(QuestOk) ?? new List<SaveData.QuestProg>();
            SaveMigration.Run(g);
        }

        static void SanitizeFarm(SaveData.Farm f)
        {
            f.stage = Math.Max(1, Math.Min(Defs.WT.stages, f.stage));
            f.ring = Math.Max(1, Math.Min(Defs.WT.ringMax, f.ring));
            if (!IsFinite(f.exp) || f.exp < 0) f.exp = 0;
            f.zone = Keep(f.zone, (k, v) => Defs.KID.ContainsKey(k) && Defs.ZONE.ContainsKey(v ?? ""));
            f.klv = Keep(f.klv, (k, v) => Defs.KID.ContainsKey(k));
            foreach (var k in new List<string>(f.klv.Keys)) f.klv[k] = Math.Max(1, Math.Min(Defs.KLV.max, f.klv[k]));
            f.awaken = Keep(f.awaken, (k, v) => Defs.KID.ContainsKey(k));
            foreach (var k in new List<string>(f.awaken.Keys)) f.awaken[k] = Math.Max(0, Math.Min(Defs.KLV.awakenMax, f.awaken[k]));
            f.aff = Keep(f.aff, (k, v) => Defs.KID.ContainsKey(k) && IsFinite(v) && v >= 0);
            f.store = Keep(f.store, (k, v) => Defs.SP.ContainsKey(k) && v > 0);
            f.bag ??= new SaveData.Bag();
            if (!IsFinite(f.bag.gold) || f.bag.gold < 0) f.bag.gold = 0;
            if (!IsFinite(f.bag.gem) || f.bag.gem < 0) f.bag.gem = 0;
            if (!IsFinite(f.bag.n) || f.bag.n < 0) f.bag.n = 0;
            if (!IsFinite(f.bag.sec) || f.bag.sec < 0) f.bag.sec = 0;
            if (!IsFinite(f.bag.t) || f.bag.t < 0) f.bag.t = 0;
            f.crafts = f.crafts?.FindAll(c => c != null && Defs.DECO.ContainsKey(c.deco ?? "") && IsFinite(c.at) && IsFinite(c.t0)) ?? new List<SaveData.Craft>();
            f.decoStar = Keep(f.decoStar, (k, v) => Defs.DECO.ContainsKey(k) && v > 0);
            foreach (var k in new List<string>(f.decoStar.Keys)) f.decoStar[k] = Math.Min(Defs.DECO_STAR_MAX, f.decoStar[k]);
            f.decoDup = Keep(f.decoDup, (k, v) => Defs.DECO.ContainsKey(k) && v > 0);
            f.decoFirst = Keep(f.decoFirst, (k, v) => Defs.DECO.ContainsKey(k));
            f.decoSlots ??= new List<string>();
            var seen = new HashSet<string>();
            for (int i = 0; i < f.decoSlots.Count; i++)   // 가진 장식만, 한 번씩만 놓는다
            {
                var d = f.decoSlots[i];
                if (d != null && (!f.decoStar.ContainsKey(d) || !seen.Add(d))) f.decoSlots[i] = null;
            }
            f.reqs = f.reqs?.FindAll(r => r != null && Defs.KID.ContainsKey(r.kid ?? "") && Defs.SP.ContainsKey(r.sp ?? "") && r.n > 0) ?? new List<SaveData.Req>();
            f.weekDone = Math.Max(0, f.weekDone); f.rushUsed = Math.Max(0, f.rushUsed);
        }

        static Dictionary<string, T> Keep<T>(Dictionary<string, T> d, Func<string, T, bool> ok)
        {
            var o = new Dictionary<string, T>();
            if (d != null) foreach (var kv in d) if (kv.Key != null && ok(kv.Key, kv.Value)) o[kv.Key] = kv.Value;
            return o;
        }

        static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        // 정의에 있는 의뢰이고, 지정 버섯이 있다면 도감에 있는 버섯이어야 한다
        static bool QuestOk(SaveData.QuestProg p) => p != null && Defs.QUEST.ContainsKey(p.id ?? "") && (p.arg == null || Defs.SP.ContainsKey(p.arg));
    }
}
