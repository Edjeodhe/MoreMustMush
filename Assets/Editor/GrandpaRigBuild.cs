using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // Rebuilds GrandpaRig.prefab's Parts from the doodle grandpa parts (Assets/ArtArchive/Doodle/Characters/Grandpa/Parts)
    // and the layout Tools/art/build_grandpa_rig_doodle.py fitted (Tools/art/grandpa_rig_doodle.json: px of the full
    // drawing, y up, origin at the feet). Joints: hips, shoulders, neck, chin (beard), hat, wrist (scythe).
    // Positions stay editable in the prefab afterwards; run again only to start over from the fitted layout.
    public static class GrandpaRigBuild
    {
        const string LayoutPath = "Tools/art/grandpa_rig_doodle.json";
        const string PrefabPath = "Assets/Prefabs/Characters/GrandpaRig.prefab";
        const string PartsDir = "Assets/ArtArchive/Doodle/Characters/Grandpa/Parts/";
        const float HEIGHT_WORLD = 3.68f;   // the previous rig's on-screen height (Parts scale 0.32 × 11.5 units)

        [Serializable] class Part { public string name; public float x, y, s, rot; public bool flip; }
        [Serializable] class Joint { public string name; public float x, y; }
        [Serializable] class Layout { public float height; public Part[] parts; public Joint[] joints; }

        [MenuItem("MoreMush/Art/Build Doodle Grandpa Rig")]
        public static void Build()
        {
            var lay = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            Vector2 J(string n) { var j = lay.joints.First(q => q.name == n); return new Vector2(j.x, j.y) / 100f; }
            Part Pt(string n) => lay.parts.FirstOrDefault(q => q.name == n);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var rig = root.GetComponent<GrandpaRig>();
                var parts = root.transform.Find("Body/Parts");
                Material mat = null;
                foreach (var sr in parts.GetComponentsInChildren<SpriteRenderer>(true)) { mat = sr.sharedMaterial; break; }
                for (int i = parts.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(parts.GetChild(i).gameObject);
                float k = HEIGHT_WORLD / (lay.height / 100f);
                parts.localScale = new Vector3(k, k, 1);

                Transform Node(string name, Transform parent, Vector2 pos)
                {
                    var t = new GameObject(name).transform; t.SetParent(parent, false);
                    t.localPosition = pos; return t;
                }
                // a part's sprite under a joint at `jointPos` (rig units)
                SpriteRenderer Sprite(string part, Transform parent, Vector2 jointPos, int order)
                {
                    var p = Pt(part);
                    var sp = AssetDatabase.LoadAssetAtPath<Sprite>(PartsDir + part + ".png");
                    if (p == null || sp == null) { Debug.LogWarning("[GrandpaRig] missing " + part); return null; }
                    var t = Node(part, parent, new Vector2(p.x, p.y) / 100f - jointPos);
                    t.localRotation = Quaternion.Euler(0, 0, p.rot);
                    t.localScale = new Vector3(p.flip ? -p.s : p.s, p.s, 1);
                    var sr = t.gameObject.AddComponent<SpriteRenderer>();
                    sr.sprite = sp; sr.sortingOrder = order; if (mat != null) sr.sharedMaterial = mat;
                    return sr;
                }

                Sprite("backpack", parts, Vector2.zero, 0);
                var armBack = Node("ArmBack", parts, J("arm_back"));
                Sprite("arm_back", armBack, J("arm_back"), 1);
                var legBack = Node("LegBack", parts, J("leg_back"));
                Sprite("leg_back", legBack, J("leg_back"), 2);
                var legFront = Node("LegFront", parts, J("leg_front"));
                Sprite("leg_front", legFront, J("leg_front"), 3);
                Sprite("torso", parts, Vector2.zero, 4);
                var neck = Node("Neck", parts, J("neck"));
                Sprite("head", neck, J("neck"), 5);
                var blink = Sprite("eyes_closed", neck, J("neck"), 6);
                var beard = Node("Beard", neck, J("beard") - J("neck"));
                Sprite("beard", beard, J("beard"), 7);
                var mouth = Sprite("mouth_open", beard, J("beard"), 8);
                var hat = Node("Hat", neck, J("hat") - J("neck"));
                Sprite("hat", hat, J("hat"), 9);
                var armFront = Node("ArmFront", parts, J("arm_front"));
                var scythe = Node("Scythe", armFront, J("scythe") - J("arm_front"));
                Sprite("scythe", scythe, J("scythe"), 10);
                Sprite("arm_front", armFront, J("arm_front"), 11);

                rig.neck = neck; rig.beard = beard; rig.hat = hat; rig.armBack = armBack; rig.armFront = armFront;
                rig.legBack = legBack; rig.legFront = legFront; rig.scythe = scythe; rig.eyes = blink; rig.mouth = mouth;
                if (blink != null) blink.enabled = false;
                if (mouth != null) mouth.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[GrandpaRig] rebuilt from {LayoutPath} ({lay.parts.Length} parts, scale {k:F3})");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
