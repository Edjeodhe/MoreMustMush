using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MoreMush
{
    // Field lives elsewhere in the scene: AutoWire looks it up by hierarchy path from the scene root.
    [AttributeUsage(AttributeTargets.Field)]
    public class ScenePathAttribute : Attribute { public readonly string path; public ScenePathAttribute(string p) => path = p; }

    // Field is a prefab/asset: AutoWire loads it from this asset path (editor only).
    [AttributeUsage(AttributeTargets.Field)]
    public class AssetPathAttribute : Attribute { public readonly string path; public AssetPathAttribute(string p) => path = p; }

    // Fills empty public Component/GameObject fields from descendants named like the field
    // (case-insensitive, "_"/spaces ignored; array element i matches "Name" + i).
    // Run by the editor menu MoreMush/Auto Wire after the hierarchy is built, so the links are saved and
    // visible in the inspector; anything already assigned is left alone.
    public static class AutoWire
    {
        static string Key(string s) => s.Replace("_", "").Replace(" ", "").ToLowerInvariant();

        public static int Fill(MonoBehaviour mb)
        {
            var byName = new Dictionary<string, List<Transform>>();
            foreach (var t in mb.GetComponentsInChildren<Transform>(true))
            {
                var k = Key(t.name);
                if (!byName.TryGetValue(k, out var l)) byName[k] = l = new List<Transform>();
                l.Add(t);
            }
            int n = 0;
            foreach (var f in mb.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var ft = f.FieldType;
                if (ft.IsArray)
                {
                    var et = ft.GetElementType();
                    if (!IsRef(et)) continue;
                    var arr = (Array)f.GetValue(mb);
                    if (arr == null) continue;
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr.GetValue(i) is UnityEngine.Object o && o != null) continue;
                        var v = Lookup(byName, f.Name + i, et, mb.transform);
                        if (v != null) { arr.SetValue(v, i); n++; }
                    }
                    continue;
                }
                if (!IsRef(ft)) continue;
                if (f.GetValue(mb) is UnityEngine.Object cur && cur != null) continue;
                var val = Special(mb, f) ?? Lookup(byName, f.Name, ft, mb.transform) ?? Own(mb, ft);
                if (val != null) { f.SetValue(mb, val); n++; }
            }
            return n;
        }

        static UnityEngine.Object Special(MonoBehaviour mb, FieldInfo f)
        {
            var ft = f.FieldType;
            var sp = f.GetCustomAttribute<ScenePathAttribute>();
            if (sp != null)
            {
                string[] parts = sp.path.Split(new[] { '/' }, 2);
                foreach (var root in mb.gameObject.scene.GetRootGameObjects())
                {
                    if (root.name != parts[0]) continue;
                    var t = parts.Length > 1 ? root.transform.Find(parts[1]) : root.transform;
                    if (t == null) continue;
                    return ft == typeof(GameObject) ? t.gameObject : (UnityEngine.Object)t.GetComponent(ft);
                }
                return null;
            }
#if UNITY_EDITOR
            var ap = f.GetCustomAttribute<AssetPathAttribute>();
            if (ap != null)
            {
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ap.path);
                if (go == null) return null;
                return ft == typeof(GameObject) ? go : (UnityEngine.Object)go.GetComponent(ft);
            }
#endif
            return null;
        }

        // another script of that type on the same object (e.g. RoundController.view on the Round object)
        static UnityEngine.Object Own(MonoBehaviour mb, Type ft)
        {
            if (!typeof(MonoBehaviour).IsAssignableFrom(ft)) return null;
            var own = mb.GetComponent(ft);
            return own != null && !ReferenceEquals(own, mb) ? own : null;
        }

        static bool IsRef(Type t) => typeof(Component).IsAssignableFrom(t) || t == typeof(GameObject);

        static UnityEngine.Object Lookup(Dictionary<string, List<Transform>> byName, string field, Type type, Transform self)
        {
            if (!byName.TryGetValue(Key(field), out var list)) return null;
            foreach (var t in list)
            {
                if (t == self) continue;
                if (type == typeof(GameObject)) return t.gameObject;
                var c = t.GetComponent(type);
                if (c != null) return c;
            }
            return null;
        }
    }
}
