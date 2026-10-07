using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoreMush.EditorTools
{
    public static class AutoWireMenu
    {
        // Wire every MoreMush component in the open scene and in Assets/Prefabs, then save.
        [MenuItem("MoreMush/Auto Wire")]
        public static void WireAll()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                int k = 0;
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true).Where(IsOurs)) k += AutoWire.Fill(mb);
                if (k > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                n += k;
            }
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true).Where(IsOurs))
                {
                    int k = AutoWire.Fill(mb);
                    if (mb is GameFlow gf) k += WireFlow(gf);
                    if (k > 0) { EditorUtility.SetDirty(mb); n += k; }
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MoreMush] auto-wired {n} references");
        }

        static bool IsOurs(MonoBehaviour mb) => mb != null && mb.GetType().Namespace == "MoreMush";

        // GameFlow points at screens anywhere in the scene: match by component type.
        static int WireFlow(GameFlow gf)
        {
            int n = 0;
            var all = gf.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            foreach (var f in typeof(GameFlow).GetFields())
            {
                if (f.FieldType == typeof(GameObject))
                {
                    if (f.GetValue(gf) is GameObject g && g != null) continue;
                    string want = char.ToUpperInvariant(f.Name[0]) + f.Name.Substring(1);
                    var t = all.FirstOrDefault(x => x.name == want);
                    if (t != null) { f.SetValue(gf, t.gameObject); n++; }
                    continue;
                }
                if (!typeof(Component).IsAssignableFrom(f.FieldType)) continue;
                if (f.GetValue(gf) is Object o && o != null) continue;
                var c = Object.FindFirstObjectByType(f.FieldType, FindObjectsInactive.Include);
                if (c != null) { f.SetValue(gf, c); n++; }
            }
            return n;
        }
    }
}
