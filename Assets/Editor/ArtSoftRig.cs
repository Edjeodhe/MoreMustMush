using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.EditorTools
{
    // Lays out the critter and grandpa rigs for the soft watercolor parts (Tools/art/rig_soft.json, written from
    // the part compositing in Tools/art/build_grandpa_rig_soft.py and the critter layout pass):
    // CritterRig.prefab transforms, every CritterIcon (UI copy of the critter rig) and GrandpaRig.prefab.
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

            // UI critters: the same layout in UI pixels (unitPx per rig unit), parts recognised by their sprite
            int icons = 0;
            void FixIcons(GameObject go)
            {
                foreach (var ic in go.GetComponentsInChildren<CritterIcon>(true))
                {
                    Undo.RecordObject(ic, "soft rig");
                    ic.capY = layout.capY;
                    var cap = layout.critter.First(p => p.name == "CapSprite");
                    Place(ic.cap, cap.pos[0], layout.capY, cap.scale, Sp("Characters/Critters/cap_fire"), ic.unitPx);
                    foreach (var im in ic.parts)
                    {
                        if (im == null || im.sprite == null) continue;
                        string n = im.sprite.name;
                        var part = layout.critter.FirstOrDefault(p => p.sprite.EndsWith("/" + n) && (n != "foot" || (p.name == "FootL") == (im.rectTransform.anchoredPosition.x < 0)));
                        if (part == null) continue;
                        var s = Sp(part.sprite);
                        im.sprite = s;
                        Place(im, part.pos[0], part.pos[1], part.scale, s, ic.unitPx);
                    }
                    EditorUtility.SetDirty(ic);
                    icons++;
                }
            }
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                int before = icons; FixIcons(root);
                if (icons > before) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects()) FixIcons(go);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ArtSoftRig] critter + grandpa prefabs laid out, {icons} CritterIcons updated (scene {scene.path})");
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

        // anchored position = rig units × unitPx; size = sprite pixels × part scale / PPU × unitPx; mirrored parts keep the flip
        static void Place(Image im, float x, float y, float[] scale, Sprite s, float unitPx)
        {
            if (im == null || s == null) return;
            var rt = im.rectTransform;
            rt.anchoredPosition = new Vector2(x, y) * unitPx;
            rt.sizeDelta = new Vector2(s.rect.width * Mathf.Abs(scale[0]), s.rect.height * Mathf.Abs(scale[1])) / s.pixelsPerUnit * unitPx;
            rt.localScale = new Vector3(Mathf.Sign(scale[0]) * Mathf.Abs(rt.localScale.x), Mathf.Abs(rt.localScale.y), 1);
        }
    }
}
