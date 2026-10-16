using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // Places the separate scenery objects (Env/<name>, doodle set) that give the pale grounds their depth:
    //   World/Round/Field/Decor/<region>  border scenery per region (RoundView turns on the one matching the theme)
    //   World/Farm/Decor                  back row along the fence, side clusters and big front corners
    // Positions are in screen / field pixels at the object's base (feet); the sprite is centred on its base and
    // sorted by base y, so nearer objects draw over farther ones and critters walk behind or in front.
    // Rebuilds both groups from the tables below; move things in the scene afterwards as you like.
    public static class ArtDecor
    {
        // field slots: base x, base y, height px, mirror
        static readonly (float x, float y, float h, bool flip)[] FIELD_SLOTS =
        {
            (-10, 330, 300, false), (60, 1075, 170, false), (1930, 340, 300, true), (1850, 1075, 170, true),
            (1895, 700, 230, true), (35, 760, 130, false), (560, 45, 90, false), (840, 35, 70, false),
            (1220, 45, 90, true), (1500, 55, 70, false), (460, 1075, 80, false), (1380, 1075, 90, true),
            (270, 1000, 100, false), (1640, 1010, 80, true),
        };
        static readonly (string region, string[] objs)[] FIELD =
        {
            ("forest", new[] { "tree_big", "bush_big", "tree_big", "bush_big", "tree_pine", "rock_big", "bush_mid", "grass_tall", "bush_small", "flowers_white", "flowers_yellow", "rock_mid", "fern", "clover" }),
            ("night", new[] { "night_tree", "night_bush", "night_tree", "night_bush", "night_tree", "night_rock", "night_glowshroom", "night_flowers", "night_glowshroom", "night_lantern", "night_flowers", "night_rock", "night_bush", "night_glowshroom" }),
            ("field", new[] { "tree_mid", "field_haybale", "field_windmill", "field_haybale", "field_scarecrow", "field_wheat", "field_flowers", "field_wheat", "field_flowers", "field_sunflower", "field_flowers", "field_wheat", "field_haybale", "flowers_yellow" }),
            ("sea", new[] { "sea_palm", "sea_rock", "sea_palm", "sea_driftwood", "sea_palm", "sea_rock", "sea_shells", "sea_grass", "sea_coral", "sea_shells", "sea_coral", "sea_rock", "sea_grass", "sea_shells" }),
            ("ruins", new[] { "ruins_giantshroom", "ruins_wall", "ruins_arch", "ruins_rubble", "ruins_pillar", "ruins_rubble", "ruins_lantern", "ruins_bush", "ruins_rubble", "ruins_pillar", "ruins_bush", "ruins_wall", "ruins_lantern", "rock_mid" }),
        };
        // farm: object, base x, base y, height px, mirror (visible ground lies between the left rail and the right window)
        static readonly (string obj, float x, float y, float h, bool flip)[] FARM =
        {
            ("tree_mid", 330, 420, 190, false), ("tree_pine", 1290, 410, 200, false), ("bush_mid", 470, 430, 80, false), ("bush_small", 1150, 425, 70, true),
            ("rock_big", 320, 700, 110, false), ("stump", 1290, 690, 90, true), ("flowers_pink", 390, 820, 55, false), ("log", 1240, 860, 80, true),
            ("mush_deco", 430, 960, 55, false), ("fern", 1180, 980, 80, true),
            ("bush_big", 300, 1090, 170, false), ("bush_big", 1310, 1090, 160, true), ("flowers_white", 560, 1050, 60, false),
            ("flowers_yellow", 1060, 1060, 60, true), ("grass_tall", 700, 1080, 60, false), ("clover", 950, 1075, 50, false),
        };

        // depth layers (dream set, 2026-10-15 5차): obj, base x, base y, height px, mirror, sorting order, alpha.
        // far_* = pale hazy silhouettes behind everything, fog_* / light_patch = soft washes, near_* = big dark framing
        // pieces cut by the screen edge, drawn over everything but kept to the corners so the play area stays open.
        const int FAR = 2, WASH = 6, NEAR = 3900;
        static readonly (string obj, float x, float y, float h, bool flip, int order, float a)[] FIELD_DEPTH =
        {
            ("light_patch", 960, 470, 420, false, WASH, 0.55f), ("fog_b", 150, 560, 260, false, WASH, 0.5f), ("fog_b", 1780, 470, 240, true, WASH, 0.45f),
            ("fog_a", 960, 150, 120, false, WASH, 0.45f),
            ("near_leaves_l", 90, 1110, 360, false, NEAR, 1f), ("near_leaves_r", 1830, 1110, 360, false, NEAR, 1f),
            ("near_branch", 260, 190, 190, false, NEAR, 1f), ("near_branch", 1680, 170, 170, true, NEAR, 1f),
        };
        static readonly (string obj, float x, float y, float h, bool flip, int order, float a)[] FARM_DEPTH =
        {
            ("far_hill", 820, 400, 170, false, FAR, 1f), ("far_tree_b", 520, 395, 300, false, FAR + 1, 1f), ("far_shroom", 1110, 395, 330, false, FAR + 1, 1f),
            ("far_tree_a", 760, 390, 340, true, FAR + 1, 1f), ("far_tree_c", 1000, 392, 230, false, FAR + 2, 1f), ("far_pine", 1240, 398, 220, true, FAR + 2, 1f),
            // fog and the light wash stay under the world tree (order 880): drawn over it they washed the whole tree out
            ("fog_a", 820, 430, 110, false, 860, 0.6f), ("light_patch", 820, 760, 300, false, 861, 0.25f),   // fainter: its rim read as a pasted oval next to the tree
            ("near_leaves_l", 260, 1130, 300, false, NEAR, 1f), ("near_shroom", 1400, 1120, 340, true, NEAR, 1f),
        };

        [MenuItem("MoreMush/Art/Place Env Decor")]
        public static void Place()
        {
            var art = MoreMushSetup.ResolvedArt();
            Sprite Sp(string n) => art.TryGetValue("Env/" + n, out var p) ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;
            var scene = EditorSceneManager.GetActiveScene();
            var world = scene.GetRootGameObjects().First(g => g.name == "World").transform;
            int n = 0, missing = 0;

            var field = world.Find("Round/Field");
            var fDecor = Fresh(field, "Decor");
            fDecor.SetSiblingIndex(1);   // right after the ground
            foreach (var (region, objs) in FIELD)
            {
                var g = Fresh(fDecor, region);
                for (int i = 0; i < FIELD_SLOTS.Length && i < objs.Length; i++)
                {
                    var s = FIELD_SLOTS[i];
                    if (Make(g, objs[i], Sp(objs[i]), s.x, s.y, s.h, s.flip, 10 + Mathf.RoundToInt(s.y / 20))) n++; else missing++;
                }
                if (region == "forest")
                    foreach (var d in FIELD_DEPTH) if (Make(g, d.obj, Sp(d.obj), d.x, d.y, d.h, d.flip, d.order, d.a)) n++; else missing++;
                g.gameObject.SetActive(region == "forest");
            }

            var farm = world.Find("Farm");
            var decor = Fresh(farm, "Decor");
            decor.SetSiblingIndex(1);
            foreach (var d in FARM) if (Make(decor, d.obj, Sp(d.obj), d.x, d.y, d.h, d.flip, 1000 + Mathf.RoundToInt(d.y))) n++; else missing++;
            foreach (var d in FARM_DEPTH) if (Make(decor, d.obj, Sp(d.obj), d.x, d.y, d.h, d.flip, d.order, d.a)) n++; else missing++;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ArtDecor] {n} objects placed" + (missing > 0 ? $", {missing} sprites missing" : ""));
        }

        static Transform Fresh(Transform parent, string name)
        {
            var old = parent.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "decor");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static bool Make(Transform parent, string name, Sprite sp, float x, float y, float h, bool flip, int order, float alpha = 1f)
        {
            if (sp == null) { Debug.LogWarning("[ArtDecor] no sprite Env/" + name); return false; }
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "decor");
            go.transform.SetParent(parent, false);
            float k = h / sp.rect.height;   // sprite px → wanted px (PPU 100 = 1 unit per 100 px)
            go.transform.localPosition = new Vector3(x / Art.PPU, -(y - h / 2) / Art.PPU, 0);
            go.transform.localScale = new Vector3(flip ? -k : k, k, 1) * (sp.pixelsPerUnit / Art.PPU);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp; sr.sortingOrder = order; sr.color = new Color(1, 1, 1, alpha);
            return true;
        }
    }
}
