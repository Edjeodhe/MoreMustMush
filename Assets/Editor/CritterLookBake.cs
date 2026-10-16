using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // CritterRig 프리팹에 기본 모습(kind·form)을 미리 구워 둔다 — 플레이 전에도 계층·그림이 실행 때와 같게.
    // 실행 때 CritterRig는 구워 둔 Evo(진화 리그)·Whole(통짜 그림)을 이름으로 찾아 이어 쓰고, 다른 꼬마면 그림만 갈아 끼운다.
    // 그림·배치 표(evo_rig.json, critter_rig.json)를 바꾼 뒤 다시 누른다.
    public static class CritterLookBake
    {
        const string PREFAB = "Assets/Prefabs/Characters/CritterRig.prefab";

        [MenuItem("MoreMush/Art/Bake Critter Look")]
        public static void Bake()
        {
            var root = PrefabUtility.LoadPrefabContents(PREFAB);
            try
            {
                var rig = root.GetComponent<CritterRig>();
                if (rig == null) { Debug.LogError($"[Bake] {PREFAB}에 CritterRig가 없어요"); return; }
                rig.SetKind(rig.kind);
                PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
                Debug.Log($"[Bake] {PREFAB}: {rig.kind} ★{rig.form} (Evo={rig.EvoOn}, 통짜={rig.WholeOn})");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
