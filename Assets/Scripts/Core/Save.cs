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
        public int v = 2;
        public double gold, gem, spore, dia;       // dia 다이아몬드: 스킨·건물에 쓰는 코스메틱 재화
        public double autoT;                       // 자동 수확 보상을 마지막으로 받은 시각 (ms, 0 = 아직 시작 안 함)
        public double seenT;                       // 마지막으로 게임 안에 있던 시각 (ms, 복귀 팝업용, 0 = 기록 없음)
        public Skins skins = new Skins();
        public Dictionary<string, int> nodes = new Dictionary<string, int>();
        public Dictionary<string, bool> seeds = new Dictionary<string, bool>();
        public Dictionary<string, CodexEntry> codex = new Dictionary<string, CodexEntry>();
        public Dictionary<string, double> inv = new Dictionary<string, double>();
        public Dictionary<string, bool> specials = new Dictionary<string, bool>();
        public Dictionary<string, int> hv = new Dictionary<string, int> { ["sam"] = 1 };
        public Dictionary<string, bool> hvOn = new Dictionary<string, bool> { ["sam"] = true };
        public string theme = "forest";
        public List<Quest> quests = new List<Quest>();   // 마을 의뢰 (버섯 상점)
        public Farm farm = new Farm();             // 버섯 농장 (목장·밭)
        public Records rec = new Records();
        public int rounds;
        public double play;
        public bool ending;

        [Serializable] public class Skins { public Dictionary<string, bool> own = new Dictionary<string, bool>(); public Dictionary<string, string> ch = new Dictionary<string, string>(), hv = new Dictionary<string, string>(); }
        // next 다음 부탁 시각(ms, 실제 시간) · kind 부탁 종류 · love 호감도(부탁 수) · evo 진화 단계 · star 별 등급
        // tree 버섯 나무 · blds 지은(짓는) 건물
        [Serializable]
        public class Farm
        {
            public Dictionary<string, double> next = new Dictionary<string, double>();
            public Dictionary<string, string> kind = new Dictionary<string, string>();
            public Dictionary<string, int> love = new Dictionary<string, int>(), evo = new Dictionary<string, int>(), star = new Dictionary<string, int>();
            public int nextUid = 1;
            public Tree tree = new Tree();
            public List<Bld> blds = new List<Bld>();
        }
        // 버섯 나무: lv 레벨 · slots 버섯 자리마다 지금 열리는 버섯 (kind, t0 열리기 시작한 시각, at 다 자라는 시각, ms)
        [Serializable] public class Tree { public int lv = 1; public List<Fruit> slots = new List<Fruit>(); }
        [Serializable] public class Fruit { public string kind; public double t0, at; }
        // 건물 하나: 격자 칸(gx, gy)이 왼쪽 위 · critter 짓는 꼬마 · at 다 지어지는 시각(ms) · done 완성
        [Serializable] public class Bld { public int uid, gx, gy; public string id, critter; public double t0, at; public bool done; }
        [Serializable] public class Quest { public string id, npc; public double cnt; public int line; }
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
            g.codex ??= new Dictionary<string, SaveData.CodexEntry>(); g.inv ??= new Dictionary<string, double>();
            g.specials ??= new Dictionary<string, bool>();
            g.hv ??= new Dictionary<string, int> { ["sam"] = 1 }; g.hvOn ??= new Dictionary<string, bool>();
            g.rec ??= new SaveData.Records();
            g.skins ??= new SaveData.Skins();
            g.skins.own ??= new Dictionary<string, bool>(); g.skins.ch ??= new Dictionary<string, string>(); g.skins.hv ??= new Dictionary<string, string>();
            foreach (var k in new List<string>(g.codex.Keys)) if (g.codex[k] == null) g.codex.Remove(k);
            g.quests = g.quests?.FindAll(q => q != null && q.id != null && Defs.SP.ContainsKey(q.id)) ?? new List<SaveData.Quest>();
            g.rounds = Math.Max(0, g.rounds);
            if (!IsFinite(g.gold)) g.gold = 0;
            if (!IsFinite(g.gem)) g.gem = 0;
            if (!IsFinite(g.spore)) g.spore = 0;
            if (!IsFinite(g.dia)) g.dia = 0;

            // prototype loadSave() migrations that still apply
            foreach (var br in Defs.CAT_KEYS)
                foreach (var n in Defs.NODES)
                    if (n.br == br && !n.core && g.nodes.ContainsKey(n.id) && g.nodes[n.id] > 0) { g.nodes["core_" + br] = 1; break; }
            foreach (var id in g.hv.Keys) if (!g.hvOn.ContainsKey(id)) g.hvOn[id] = true;
            foreach (var n in Defs.NODES) if (g.nodes.TryGetValue(n.id, out var L) && (L > n.max || L < 0)) g.nodes[n.id] = Math.Max(0, Math.Min(n.max, L));
            foreach (var id in new List<string>(g.hv.Keys)) g.hv[id] = Math.Max(1, Math.Min(5, g.hv[id]));
            if (!Defs.THEME.ContainsKey(g.theme ?? "")) g.theme = "forest";
            g.farm ??= new SaveData.Farm();
            g.farm.next ??= new Dictionary<string, double>(); g.farm.kind ??= new Dictionary<string, string>();
            g.farm.love ??= new Dictionary<string, int>(); g.farm.evo ??= new Dictionary<string, int>();
            g.farm.star ??= new Dictionary<string, int>();
            g.farm.blds = g.farm.blds?.FindAll(b => b != null && Defs.BUILDING.ContainsKey(b.id ?? "")) ?? new List<SaveData.Bld>();
            foreach (var b in g.farm.blds) if (b.uid >= g.farm.nextUid) g.farm.nextUid = b.uid + 1;   // 새 건물 uid가 기존 것과 겹치지 않게
            g.farm.tree ??= new SaveData.Tree();
            g.farm.tree.lv = Math.Max(1, Math.Min(Defs.TREE.maxLv, g.farm.tree.lv));
            g.farm.tree.slots = g.farm.tree.slots?.FindAll(f => f != null && Defs.FRUITS.ContainsKey(f.kind ?? "")) ?? new List<SaveData.Fruit>();
        }

        static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
