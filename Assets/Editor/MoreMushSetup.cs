using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace MoreMush.EditorTools
{
    // One-time project setup for generated art and fonts. Menu: MoreMush/...
    public static class MoreMushSetup
    {
        // Art sets, later ones win: Doodle (outlined cel set) > Soft (watercolor) > Generated for the same key.
        public static readonly string[] ArtRoots = { "Assets/Art/Generated", "Assets/Art/Soft", "Assets/Art/Doodle" };

        // key ("Icons/gold") → asset path of the sprite the game uses for it
        public static SortedDictionary<string, string> ResolvedArt()
        {
            var map = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var root in ArtRoots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    string key = p.Substring(root.Length + 1);
                    map[key.Substring(0, key.Length - Path.GetExtension(key).Length)] = p;
                }
            }
            return map;
        }
        const string DbPath = "Assets/Resources/SpriteDB.asset";

        [MenuItem("MoreMush/Setup All")]
        public static void SetupAll()
        {
            ConfigureSlicing();
            RebuildSpriteDB();
            BuildFontAssets();
            BuildIconSpriteAsset();
            BuildMaterials();
            Debug.Log("[MoreMush] setup done");
        }

        // ===== 9-slice borders for UI kit pieces and stretchable field patches =====
        [MenuItem("MoreMush/Configure 9-Slice Borders")]
        public static void ConfigureSlicing()
        {
            var rules = new Dictionary<string, System.Func<int, int, Vector4>>
            {
                // Vector4 = (left, bottom, right, top) in pixels
                ["UI/panel_wood"] = (w, h) => Uniform(w, h, 0.28f),
                ["UI/panel_paper"] = (w, h) => Uniform(w, h, 0.2f),
                ["UI/btn_wood"] = (w, h) => Uniform(w, h, 0.42f),
                ["UI/btn_go"] = (w, h) => Uniform(w, h, 0.42f),
                ["UI/btn_danger"] = (w, h) => Uniform(w, h, 0.42f),
                // doodle 4th-pass kit (beige pastel base + accent buttons/panels)
                ["UI/btn_orange"] = (w, h) => Uniform(w, h, 0.42f),
                ["UI/btn_blue"] = (w, h) => Uniform(w, h, 0.42f),
                ["UI/btn_round"] = (w, h) => Uniform(w, h, 0.45f),
                ["UI/panel_accent"] = (w, h) => Uniform(w, h, 0.22f),
                ["UI/panel_mint"] = (w, h) => Uniform(w, h, 0.22f),
                ["UI/window_title"] = (w, h) => Uniform(w, h, 0.4f),
                ["UI/bar_top"] = (w, h) => Uniform(w, h, 0.4f),
                ["UI/rail_item"] = (w, h) => Uniform(w, h, 0.4f),
                ["UI/rail_active"] = (w, h) => Uniform(w, h, 0.4f),
                ["UI/chip"] = (w, h) => Uniform(w, h, 0.45f),
                ["UI/tooltip"] = (w, h) => Uniform(w, h, 0.25f),
                ["UI/progress_back"] = (w, h) => Uniform(w, h, 0.45f),
                ["UI/progress_fill"] = (w, h) => Uniform(w, h, 0.45f),
                ["UI/bar_log"] = (w, h) => new Vector4(Mathf.Min(w * 0.2f, h * 1.6f), 0, Mathf.Min(w * 0.2f, h * 1.6f), 0),
                ["Props/moss"] = (w, h) => Uniform(w, h, 0.3f),
            };
            var art = ResolvedArt();
            foreach (var kv in rules)
            {
                var imp = art.TryGetValue(kv.Key, out var path) ? AssetImporter.GetAtPath(path) as TextureImporter : null;
                if (imp == null) { Debug.LogWarning("missing " + kv.Key); continue; }
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                imp.spriteBorder = kv.Value(w, h);
                // sliced SpriteRenderers (world-space frames, bubbles) need a full-rect mesh to 9-slice correctly
                var st = new TextureImporterSettings(); imp.ReadTextureSettings(st);
                st.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(st);
                imp.SaveAndReimport();
            }
        }

        static Vector4 Uniform(int w, int h, float k) { float b = Mathf.Round(Mathf.Min(w, h) * k); return new Vector4(b, b, b, b); }

        // ===== Sprite DB =====
        [MenuItem("MoreMush/Rebuild Sprite DB")]
        public static void RebuildSpriteDB()
        {
            Directory.CreateDirectory("Assets/Resources");
            var db = AssetDatabase.LoadAssetAtPath<SpriteDB>(DbPath);
            if (db == null) { db = ScriptableObject.CreateInstance<SpriteDB>(); AssetDatabase.CreateAsset(db, DbPath); }
            db.entries.Clear();
            foreach (var kv in ResolvedArt())
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(kv.Value);
                if (sp == null || kv.Value.EndsWith("/grandpa_ref.png")) continue;
                db.entries.Add(new SpriteDB.Entry { key = kv.Key, sprite = sp });
            }
            db.entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MoreMush] SpriteDB: {db.entries.Count} sprites");
        }

        // ===== Fonts: 언어별 제목·본문 폰트 세트 (LocTools) =====
        [MenuItem("MoreMush/Build Font Assets")]
        public static void BuildFontAssets()
        {
            LocTools.BuildFonts();
            LocTools.BakeFonts();
        }

        // ===== Inline icons for TMP text: <sprite name="gold"> =====
        [MenuItem("MoreMush/Build Icon Sprite Asset")]
        public static void BuildIconSpriteAsset()
        {
            const int cell = 128;
            var iconDirs = new[] { "Icons/", "Farm/Icons/", "Farm/Field/", "Farm/Tree/" };
            var paths = ResolvedArt().Where(kv => iconDirs.Any(d => kv.Key.StartsWith(d))).Select(kv => kv.Value).ToList();
            int cols = 8, rows = Mathf.CeilToInt(paths.Count / (float)cols);
            var atlas = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            var names = new List<string>();
            for (int i = 0; i < paths.Count; i++)
            {
                var src = LoadReadable(paths[i]);
                var scaled = Fit(src, cell - 8);
                int cx = (i % cols) * cell, cy = (rows - 1 - i / cols) * cell;
                atlas.SetPixels(cx + (cell - scaled.width) / 2, cy + (cell - scaled.height) / 2, scaled.width, scaled.height, scaled.GetPixels());
                names.Add(Path.GetFileNameWithoutExtension(paths[i]));
            }
            atlas.Apply();
            string atlasPath = "Assets/Fonts/IconAtlas.png";
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            AssetDatabase.ImportAsset(atlasPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            string assetPath = "Assets/Resources/Sprite Assets/Icons.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            var sa = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
            if (sa == null) { sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>(); AssetDatabase.CreateAsset(sa, assetPath); }
            // a fresh asset has no version, which makes TMP run its legacy upgrade and wipe the tables
            typeof(TMP_SpriteAsset).GetField("m_Version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(sa, "1.1.0");
            sa.spriteSheet = tex;
            sa.spriteGlyphTable.Clear(); sa.spriteCharacterTable.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                int cx = (i % cols) * cell, cy = (rows - 1 - i / cols) * cell;
                var glyph = new TMP_SpriteGlyph { index = (uint)i, metrics = new GlyphMetrics(cell, cell, 0, cell * 0.8f, cell), glyphRect = new GlyphRect(cx, cy, cell, cell), scale = 1, atlasIndex = 0 };
                sa.spriteGlyphTable.Add(glyph);
                sa.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = names[i] });
            }
            var shader = Shader.Find("TextMeshPro/Sprite");
            if (sa.material == null) { sa.material = new Material(shader) { name = "Icons Material" }; AssetDatabase.AddObjectToAsset(sa.material, sa); }
            sa.material.SetTexture(ShaderUtilities.ID_MainTex, tex);
            sa.UpdateLookupTables();
            EditorUtility.SetDirty(sa);
            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultSpriteAsset").objectReferenceValue = sa;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[MoreMush] icon sprite asset: {names.Count} icons");
        }

        static Texture2D LoadReadable(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }

        static Texture2D Fit(Texture2D src, int max)
        {
            float k = Mathf.Min(1f, (float)max / Mathf.Max(src.width, src.height));
            int w = Mathf.Max(1, Mathf.RoundToInt(src.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(src.height * k));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var o = new Texture2D(w, h, TextureFormat.RGBA32, false);
            o.ReadPixels(new Rect(0, 0, w, h), 0, 0); o.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            return o;
        }

        // ===== Materials referenced by scene objects (keeps shaders in builds) =====
        [MenuItem("MoreMush/Build Materials")]
        public static void BuildMaterials()
        {
            Directory.CreateDirectory("Assets/Materials");
            Mat("Assets/Materials/Sprite.mat", "MoreMush/Sprite", null);
            Mat("Assets/Materials/SpriteAdditive.mat", "MoreMush/Sprite", m => m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One));
            Mat("Assets/Materials/WeatherMask.mat", "MoreMush/WeatherMask", null);
            Mat("Assets/Materials/UIFx.mat", "MoreMush/UISprite", null);
            // TMP 외곽선 프리셋은 폰트마다 LocTools.BuildFonts가 만든다
            AssetDatabase.SaveAssets();
        }

        static void Mat(string path, string shader, System.Action<Material> setup)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
        }
    }
}
