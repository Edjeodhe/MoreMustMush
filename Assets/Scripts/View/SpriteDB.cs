using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush
{
    // All generated art (Assets/Art/Generated/**) by path key, e.g. "Mushrooms/Single/ed0", "Icons/gold".
    // Filled by the editor menu MoreMush/Rebuild Sprite DB (runs automatically after art import).
    [CreateAssetMenu(menuName = "MoreMush/Sprite DB")]
    public class SpriteDB : ScriptableObject
    {
        [Serializable] public struct Entry { public string key; public Sprite sprite; }
        public List<Entry> entries = new List<Entry>();
        public Material[] uiFx = new Material[4];   // UI sprite effect materials: none, locked, unlocked-not-harvested, golden

        static SpriteDB inst;
        Dictionary<string, Sprite> map;

        public static SpriteDB I
        {
            get
            {
                if (inst == null) inst = Resources.Load<SpriteDB>("SpriteDB");
                return inst;
            }
        }

        public static Sprite Get(string key)
        {
            var db = I;
            if (db == null) return null;
            if (db.map == null)
            {
                db.map = new Dictionary<string, Sprite>();
                foreach (var e in db.entries) if (e.sprite != null) db.map[e.key] = e.sprite;
            }
            return db.map.TryGetValue(key, out var s) ? s : null;
        }

        // 접두어 + 이름으로 만든 키를 기억해 둔다. 프레임마다 그리는 곳에서 키 문자열을 새로 만들지 않게 쓴다
        static readonly Dictionary<string, Dictionary<string, string>> keys = new Dictionary<string, Dictionary<string, string>>();
        public static string Key(string prefix, string name)
        {
            if (name == null) return prefix;
            if (!keys.TryGetValue(prefix, out var m)) keys[prefix] = m = new Dictionary<string, string>();
            if (!m.TryGetValue(name, out var k)) m[name] = k = prefix + name;
            return k;
        }
        public static Sprite Get(string prefix, string name) => Get(Key(prefix, name));

        public static Sprite Single(string spId) => Get("Mushrooms/Single/", spId);
        public static Sprite Colony(string spId) => Get("Mushrooms/Colony/", spId);
        public static Sprite Icon(string name) => Get("Icons/", name);
    }
}
