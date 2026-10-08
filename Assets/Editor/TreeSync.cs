using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // Defs.NODES와 씬의 트리 노드(TreeNodeView)를 맞춘다: 없는 노드는 TreeNode 프리팹으로 만들고, 정의에서 빠진 노드는 지우고,
    // 전부 TreeLayout.Compute() 위치로 다시 놓는다. 노드를 추가·삭제한 뒤 메뉴 또는 배치 모드로 실행한다.
    // Unity.exe -batchmode -nographics -projectPath <프로젝트> -executeMethod MoreMush.EditorTools.TreeSync.Batch -logFile <로그>
    public static class TreeSync
    {
        const string ScenePath = "Assets/Scenes/Game.unity", PrefabPath = "Assets/Prefabs/Tree/TreeNode.prefab";

        [MenuItem("MoreMush/트리 노드 맞추기")]
        public static string Sync()
        {
            var parent = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "Nodes" && t.parent != null && t.parent.name == "Content" && t.gameObject.scene.IsValid());
            if (parent == null) return "Nodes 부모를 못 찾음";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var views = parent.GetComponentsInChildren<TreeNodeView>(true);
            var have = views.Select(v => v.nodeId).ToHashSet();
            var made = Defs.NODES.Where(n => !n.core && !have.Contains(n.id)).Select(n =>
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = n.id;
                var so = new SerializedObject(go.GetComponent<TreeNodeView>());
                so.FindProperty("nodeId").stringValue = n.id; so.ApplyModifiedPropertiesWithoutUndo();
                return n.id;
            }).ToList();
            var removed = views.Where(v => !Defs.NODE.ContainsKey(v.nodeId ?? "")).Select(v => { string id = v.nodeId; UnityEngine.Object.DestroyImmediate(v.gameObject); return id; }).ToList();
            var layout = TreeLayout.Compute();
            int moved = 0;
            foreach (var v in parent.GetComponentsInChildren<TreeNodeView>(true))
            {
                if (!layout.TryGetValue(v.nodeId, out var pd)) continue;
                v.transform.localPosition = Art.P(pd.pos.x, pd.pos.y, v.transform.localPosition.z);
                moved++;
            }
            EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
            return $"추가 {made.Count}개({string.Join(", ", made)}) · 삭제 {removed.Count}개({string.Join(", ", removed)}) · 재배치 {moved}개";
        }

        public static void Batch()
        {
            int code = 0; string log = Path.GetFullPath(Path.Combine(Application.dataPath, "../BalanceData/tree_sync.txt"));
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                string r = Sync();
                EditorSceneManager.SaveScene(scene);
                Directory.CreateDirectory(Path.GetDirectoryName(log));
                File.WriteAllText(log, r + "\n");
            }
            catch (Exception e) { File.WriteAllText(log, "오류: " + e + "\n"); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
