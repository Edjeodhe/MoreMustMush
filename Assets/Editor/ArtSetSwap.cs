using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // Points every Sprite reference in the scenes and prefabs at the sprite its key resolves to
    // (MoreMushSetup.ArtRoots, later sets win), e.g. Assets/ArtArchive/Generated/Icons/gold → Assets/Resources/Art/Dream/Icons/gold.
    // Code-loaded art goes through SpriteDB and needs no swap; this covers sprites wired in the inspector.
    public static class ArtSetSwap
    {
        [MenuItem("MoreMush/Art/Use Resolved Art In Scenes and Prefabs")]
        public static void SwapAll()
        {
            var resolved = MoreMushSetup.ResolvedArt();
            // path of any art-set sprite → key, so an old reference finds its key
            var keyOf = new Dictionary<string, string>();
            foreach (var root in MoreMushSetup.ArtRoots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    string key = p.Substring(root.Length + 1);
                    keyOf[p] = key.Substring(0, key.Length - Path.GetExtension(key).Length);
                }
            }

            int changed = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                int n = root.GetComponentsInChildren<Component>(true).Sum(c => Swap(c, keyOf, resolved));
                if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                if (n > 0) Debug.Log($"[ArtSetSwap] {path}: {n}");
                changed += n;
            }

            var opened = EditorSceneManager.GetActiveScene().path;
            foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = 0;
                foreach (var go in scene.GetRootGameObjects())
                    n += go.GetComponentsInChildren<Component>(true).Sum(c => Swap(c, keyOf, resolved));
                if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                if (n > 0) Debug.Log($"[ArtSetSwap] {path}: {n}");
                changed += n;
            }
            if (!string.IsNullOrEmpty(opened)) EditorSceneManager.OpenScene(opened, OpenSceneMode.Single);
            Debug.Log($"[ArtSetSwap] {changed} sprite references now point at the resolved art set");
        }

        static int Swap(Component c, Dictionary<string, string> keyOf, SortedDictionary<string, string> resolved)
        {
            if (c == null) return 0;
            int n = 0;
            var so = new SerializedObject(c);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || !(it.objectReferenceValue is Sprite sp)) continue;
                string p = AssetDatabase.GetAssetPath(sp);
                if (!keyOf.TryGetValue(p, out var key) || !resolved.TryGetValue(key, out var target) || target == p) continue;
                var next = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                if (next == null) continue;
                it.objectReferenceValue = next;
                n++;
            }
            if (n > 0) so.ApplyModifiedPropertiesWithoutUndo();
            return n;
        }
    }
}
