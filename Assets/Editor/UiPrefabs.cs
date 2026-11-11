using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // 코드로 짠 창 모양을 손으로 고칠 수 있는 프리팹으로 굽는다.
    // 설정 창: Assets/Resources/UI/OptionPanel.prefab (OptionPanelBuilder). 게임은 이 프리팹이 있으면 그것을 쓴다 (OptionWindow).
    // 이미 있으면 덮어쓰지 않는다 — 손으로 고친 것을 지우지 않게. 처음부터 다시 만들려면 "다시 만들기(덮어쓰기)".
    public static class UiPrefabs
    {
        const string OptionPath = "Assets/Resources/UI/OptionPanel.prefab";
        // 설정 제목 테두리 머티리얼 (ONE Mobile POP + 테두리 #ab4c27). 색·두께는 이 머티리얼에서 고친다
        public const string TitleMat = "Assets/Fonts/ONEMobilePOP SDF Title Outline.mat";

        [MenuItem("MoreMush/UI/설정 창 프리팹 만들기")]
        public static void BuildOptionPanel() => BuildOption(false);

        [MenuItem("MoreMush/UI/설정 창 프리팹 다시 만들기 (덮어쓰기)")]
        public static void RebuildOptionPanel() => BuildOption(true);

        static void BuildOption(bool overwrite)
        {
            if (File.Exists(OptionPath) && !overwrite) { Debug.LogWarning("[UI] 이미 있어요 (덮어쓰지 않음): " + OptionPath); Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(OptionPath); return; }
            var holder = new GameObject("PrefabHolder", typeof(RectTransform), typeof(Canvas));
            try
            {
                var go = OptionPanelBuilder.Build(holder.transform);
                go.name = "OptionPanel";
                var title = go.transform.Find("Box/Ribbon/Title")?.GetComponent<TMPro.TMP_Text>();
                var mat = AssetDatabase.LoadAssetAtPath<Material>(TitleMat);
                if (title != null && mat != null) title.fontSharedMaterial = mat;
                Directory.CreateDirectory(Path.GetDirectoryName(OptionPath));
                var saved = PrefabUtility.SaveAsPrefabAsset(go, OptionPath);
                Debug.Log("[UI] 설정 창 프리팹: " + OptionPath);
                Selection.activeObject = saved;
            }
            finally { Object.DestroyImmediate(holder); }
        }
    }
}
