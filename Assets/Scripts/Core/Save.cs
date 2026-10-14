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
        public int stage = 1;                      // 지금 스테이지. 보스 스테이지(10의 배수)는 보스를 쓰러뜨려야 넘어간다
        public bool bossOn = true;                 // 보스 도전 (끄면 보스 스테이지에서 파밍만 한다. 보스에게 지면 꺼진다)
        public double play;
        public double lastNormalGold;              // 보스가 안 나온 마지막 판의 정산 골드 (보스 처치 보상 기준)
        public bool ending;

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

    public static class SaveIO
    {
        public const string SAVE_KEY = "mushroomPinball_save_v2";
        public const string BAD_KEY = SAVE_KEY + "_bad";   // 읽지 못한 세이브 원문 (새 게임이 덮어쓰기 전에 남겨 둠)

        // true면 저장하지 않는다. 밸런스 봇(BalanceSim)이 돌 때 실제 세이브 키를 건드리지 않게 한다.
        public static bool Suspended;
        // 마지막 Load()가 세이브를 읽지 못해 null을 돌려줬는지 (세이브가 아예 없을 때는 false)
        public static bool LastLoadFailed { get; private set; }

        public static void Save(SaveData g)
        {
            if (g == null || Suspended) return;
            try
            {
                PlayerPrefs.SetString(SAVE_KEY, JsonConvert.SerializeObject(g));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogError("Save failed: " + e); }   // 저장 실패가 라운드·버튼 처리 중간을 끊지 않게
        }

        public static bool HasSave() => PlayerPrefs.HasKey(SAVE_KEY);

        public static void Wipe() { PlayerPrefs.DeleteKey(SAVE_KEY); PlayerPrefs.Save(); }

        public static SaveData Load()
        {
            LastLoadFailed = false;
            string s = PlayerPrefs.GetString(SAVE_KEY, null);
            if (string.IsNullOrEmpty(s)) return null;
            try
            {
                var g = JsonConvert.DeserializeObject<SaveData>(s) ?? throw new JsonSerializationException("save root is null");
                Sanitize(g);
                return g;
            }
            catch (Exception e)
            {
                Debug.LogError("Save load failed: " + e);
                LastLoadFailed = true;
                // 다음 저장이 원래 세이브를 덮어쓰기 전에 원문을 따로 남긴다 (처음 실패한 것만)
                try { if (!PlayerPrefs.HasKey(BAD_KEY)) { PlayerPrefs.SetString(BAD_KEY, s); PlayerPrefs.Save(); } }
                catch (Exception e2) { Debug.LogError("Save backup failed: " + e2); }
                return null;
            }
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
