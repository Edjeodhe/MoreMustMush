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
        public double gold, gem, spore;
        public Skins skins = new Skins();
        public Decor decor = new Decor();
        public Dictionary<string, int> nodes = new Dictionary<string, int>();
        public Dictionary<string, bool> seeds = new Dictionary<string, bool>();
        public Dictionary<string, CodexEntry> codex = new Dictionary<string, CodexEntry>();
        public Dictionary<string, double> inv = new Dictionary<string, double>();
        public Dictionary<string, bool> specials = new Dictionary<string, bool>();
        public Dictionary<string, int> hv = new Dictionary<string, int> { ["sam"] = 1 };
        public Dictionary<string, bool> hvOn = new Dictionary<string, bool> { ["sam"] = true };
        public string theme = "forest";
        public List<string> dishes = new List<string>();
        public Dictionary<string, int> dishLeft = new Dictionary<string, int>();
        public List<Quest> quests = new List<Quest>();   // 마을 의뢰 (버섯 상점)
        public Farm farm = new Farm();             // 버섯 농장 (목장·밭)
        public TaxState tax = new TaxState();
        public Records rec = new Records();
        public int rounds;
        public double play;
        public bool ending;

        [Serializable] public class Skins { public Dictionary<string, bool> own = new Dictionary<string, bool>(); public Dictionary<string, string> ch = new Dictionary<string, string>(), hv = new Dictionary<string, string>(); }
        [Serializable] public class Decor { public Dictionary<string, bool> own = new Dictionary<string, bool>(), on = new Dictionary<string, bool>(); }
        // next 다음 부탁 시각(ms, 실제 시간) · kind 부탁 종류 · love 호감도(부탁 수) · evo 진화 단계 · plots 밭 칸("행,열")
        [Serializable]
        public class Farm
        {
            public Dictionary<string, double> next = new Dictionary<string, double>();
            public Dictionary<string, string> kind = new Dictionary<string, string>();
            public Dictionary<string, int> love = new Dictionary<string, int>(), evo = new Dictionary<string, int>();
            public int size = 4, tool;
            public string crop = "ed";
            public Dictionary<string, Plot> plots = new Dictionary<string, Plot>();
        }
        // s: "till" 간 땅 · "grow" 자라는 중 (t0 심은 시각, at 다 자라는 시각, ms)
        [Serializable] public class Plot { public string s, crop; public double t0, at; }
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
                g.dishes = g.dishes?.FindAll(id => Defs.RECIPE.ContainsKey(id)) ?? new List<string>();
                g.dishLeft ??= new Dictionary<string, int>();
                g.skins ??= new SaveData.Skins();
                g.decor ??= new SaveData.Decor();
                g.quests ??= new List<SaveData.Quest>();
                g.farm ??= new SaveData.Farm();
                g.farm.next ??= new Dictionary<string, double>(); g.farm.kind ??= new Dictionary<string, string>();
                g.farm.love ??= new Dictionary<string, int>(); g.farm.evo ??= new Dictionary<string, int>();
                g.farm.plots ??= new Dictionary<string, SaveData.Plot>();
                if (!Defs.CROPS.ContainsKey(g.farm.crop ?? "")) g.farm.crop = "ed";
                g.farm.size = Math.Max(4, Math.Min(Defs.FIELD.max, g.farm.size));
                g.farm.tool = Math.Max(0, Math.Min(Defs.TOOLS.Length - 1, g.farm.tool));
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
