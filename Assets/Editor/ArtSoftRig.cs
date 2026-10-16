using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // Lays out the critter and grandpa rigs for the soft watercolor parts (Tools/art/rig_soft.json, written from
    // the part compositing in Tools/art/build_grandpa_rig_soft.py and the critter layout pass):
    // CritterRig.prefab and GrandpaRig.prefab transforms. (UI CritterIcon copies went with the old UI.)
    public static class ArtSoftRig
    {
        const string LayoutPath = "Tools/art/rig_soft.json";

        [Serializable] class Part { public string name, sprite; public float[] pos, scale; public int order; }
        [Serializable] class Layout { public float capY; public Part[] critter, grandpa; }

        [MenuItem("MoreMush/Art/Apply Soft Rig Layout")]
        public static void Apply()
        {
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            var art = MoreMushSetup.ResolvedArt();
            Sprite Sp(string key) => !string.IsNullOrEmpty(key) && art.TryGetValue(key, out var p) ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;

            EditPrefab("Assets/Prefabs/Characters/CritterRig.prefab", root => ApplyParts(root.transform, layout.critter, Sp));
            EditPrefab("Assets/Prefabs/Characters/GrandpaRig.prefab", root => ApplyParts(root.transform, layout.grandpa, Sp));

            Debug.Log("[ArtSoftRig] critter + grandpa prefabs laid out");
        }

        static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void ApplyParts(Transform root, Part[] parts, Func<string, Sprite> sp)
        {
            foreach (var p in parts)
            {
                var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == p.name);
                if (t == null) { Debug.LogWarning($"[ArtSoftRig] {root.name}: no {p.name}"); continue; }
                t.localPosition = new Vector3(p.pos[0], p.pos[1], t.localPosition.z);
                t.localScale = new Vector3(p.scale[0], p.scale[1], 1);
                var sr = t.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                var s = sp(p.sprite);
                if (s != null) sr.sprite = s;
                if (p.order >= 0) sr.sortingOrder = p.order;
            }
        }
    }
}
