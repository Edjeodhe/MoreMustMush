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
        public TaxState tax = new TaxState();
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
        [Serializable] public class TaxState { public int cycle = 1, roundsIn, paid, unpaid; public double income, debt; }

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

        public static void Save(SaveData g)
        {
            if (g == null) return;
            PlayerPrefs.SetString(SAVE_KEY, JsonConvert.SerializeObject(g));
            PlayerPrefs.Save();
        }

        public static bool HasSave() => PlayerPrefs.HasKey(SAVE_KEY);

        public static void Wipe() { PlayerPrefs.DeleteKey(SAVE_KEY); PlayerPrefs.Save(); }

        public static SaveData Load()
        {
            try
            {
                string s = PlayerPrefs.GetString(SAVE_KEY, null);
                if (string.IsNullOrEmpty(s)) return null;
                var g = JsonConvert.DeserializeObject<SaveData>(s) ?? new SaveData();
                // prototype loadSave() migrations that still apply
                foreach (var br in Defs.CAT_KEYS)
                    foreach (var n in Defs.NODES)
                        if (n.br == br && !n.core && g.nodes.ContainsKey(n.id) && g.nodes[n.id] > 0) { g.nodes["core_" + br] = 1; break; }
                foreach (var id in g.hv.Keys) if (!g.hvOn.ContainsKey(id)) g.hvOn[id] = true;
                foreach (var n in Defs.NODES) if (g.nodes.TryGetValue(n.id, out var L) && L > n.max) g.nodes[n.id] = n.max;
                foreach (var id in new List<string>(g.hv.Keys)) g.hv[id] = Math.Min(5, g.hv[id]);
                if (!Defs.THEME.ContainsKey(g.theme ?? "")) g.theme = "forest";
                g.skins ??= new SaveData.Skins();
                g.quests ??= new List<SaveData.Quest>();
                g.farm ??= new SaveData.Farm();
                g.farm.next ??= new Dictionary<string, double>(); g.farm.kind ??= new Dictionary<string, string>();
                g.farm.love ??= new Dictionary<string, int>(); g.farm.evo ??= new Dictionary<string, int>();
                g.farm.star ??= new Dictionary<string, int>();
                g.farm.blds = g.farm.blds?.FindAll(b => b != null && Defs.BUILDING.ContainsKey(b.id ?? "")) ?? new List<SaveData.Bld>();
                g.farm.tree ??= new SaveData.Tree();
                g.farm.tree.lv = Math.Max(1, Math.Min(Defs.TREE.maxLv, g.farm.tree.lv));
                g.farm.tree.slots = g.farm.tree.slots?.FindAll(f => f != null && Defs.FRUITS.ContainsKey(f.kind ?? "")) ?? new List<SaveData.Fruit>();

                return g;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save load failed: " + e.Message);
                return null;
            }
        }
    }
}
