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
        const string GenRoot = "Assets/Art/Generated";
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
                ["UI/bar_log"] = (w, h) => new Vector4(Mathf.Min(w * 0.2f, h * 1.6f), 0, Mathf.Min(w * 0.2f, h * 1.6f), 0),
                ["Props/moss"] = (w, h) => Uniform(w, h, 0.3f),
                ["Props/stream"] = (w, h) => Uniform(w, h, 0.3f),
            };
            foreach (var kv in rules)
            {
                string path = $"{GenRoot}/{kv.Key}.png";
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) { Debug.LogWarning("missing " + path); continue; }
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
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { GenRoot }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sp == null) continue;
                string key = p.Substring(GenRoot.Length + 1);
                key = key.Substring(0, key.Length - Path.GetExtension(key).Length);
                db.entries.Add(new SpriteDB.Entry { key = key, sprite = sp });
            }
            db.entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MoreMush] SpriteDB: {db.entries.Count} sprites");
        }

        // ===== Fonts: Jua (display) with Noto Sans KR fallback, and Noto for body text =====
        [MenuItem("MoreMush/Build Font Assets")]
        public static void BuildFontAssets()
        {
            var noto = MakeFontAsset("Assets/Fonts/NotoSansKR-Regular.ttf", "Assets/Fonts/NotoSansKR SDF.asset");
            var jua = MakeFontAsset("Assets/Fonts/Jua-Regular.ttf", "Assets/Fonts/Jua SDF.asset");
            if (jua.fallbackFontAssetTable == null) jua.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!jua.fallbackFontAssetTable.Contains(noto)) jua.fallbackFontAssetTable.Add(noto);
            EditorUtility.SetDirty(jua);
            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultFontAsset").objectReferenceValue = jua;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
        }

        static TMP_FontAsset MakeFontAsset(string ttf, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            var fa = TMP_FontAsset.CreateFontAsset(font, 64, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            fa.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(fa, path);
            // atlas texture and material must live inside the font asset
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        // ===== Inline icons for TMP text: <sprite name="gold"> =====
        [MenuItem("MoreMush/Build Icon Sprite Asset")]
        public static void BuildIconSpriteAsset()
        {
            const int cell = 128;
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { GenRoot + "/Icons", GenRoot + "/Farm/Icons", GenRoot + "/Farm/Field" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
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
            // TMP outline presets used by field numbers, labels and titles
            var jua = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Jua SDF.asset");
            if (jua != null)
            {
                OutlinePreset(jua, "Jua Outline Dark", new Color32(0x2a, 0x1a, 0x10, 255), 0.28f);
                OutlinePreset(jua, "Jua Outline Wood", new Color32(0x3b, 0x24, 0x14, 255), 0.22f);
                OutlinePreset(jua, "Jua Outline Brown", new Color32(0x5a, 0x2a, 0x00, 255), 0.3f);
            }
            AssetDatabase.SaveAssets();
        }

        static void Mat(string path, string shader, System.Action<Material> setup)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
        }

        static void OutlinePreset(TMP_FontAsset font, string name, Color32 col, float width)
        {
            string path = $"Assets/Fonts/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(font.material); AssetDatabase.CreateAsset(m, path); }
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor(ShaderUtilities.ID_OutlineColor, col);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.5f);
            EditorUtility.SetDirty(m);
        }
    }
}
