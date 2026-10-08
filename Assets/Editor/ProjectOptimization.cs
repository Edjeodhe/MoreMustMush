using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace MoreMush.EditorTools
{
    public static class ProjectOptimization
    {
        const string ArtRoot = "Assets/Art/Generated";
        public const string AtlasRoot = "Assets/Art/Atlases";

        public static int TextureLimit(string path)
        {
            if (path.Contains("/Icons/") || path.Contains("/Mushrooms/") || path.Contains("/Harvesters/")) return 256;
            if (path.Contains("/Characters/Critters/")) return 256;
            if (path.Contains("/Characters/NPC/") || path.Contains("/Farm/Props/") || path.Contains("/Farm/Tree/")) return 512;
            return 2048; // Preserve backgrounds, UI borders, and the assembled grandpa rig.
        }

        [MenuItem("MoreMush/Optimize/Textures and Atlases")]
        public static void ConfigureTextures()
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteGenerateFallbackPhysicsShape = false;
                    importer.SetTextureSettings(settings);
                    importer.maxTextureSize = TextureLimit(path);
                    importer.isReadable = false; importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            BuildAtlases();
            MoreMushSetup.RebuildSpriteDB();
            AssetDatabase.SaveAssets();
        }

        static void BuildAtlases()
        {
            Directory.CreateDirectory(AtlasRoot);
            var groups = new Dictionary<string, List<Object>>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { ArtRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string relative = path.Substring(ArtRoot.Length + 1);
                // WeatherMask and procedural FX use 0..1 UVs; never atlas these textures.
                if (relative.StartsWith("FX/") || relative.StartsWith("Backgrounds/") || relative.Contains("/Backgrounds/") || relative.EndsWith("grandpa_ref.png")) continue;
                string group = relative.Split('/')[0];
                if (relative.StartsWith("Characters/")) group += "_" + relative.Split('/')[1];
                if (relative.StartsWith("Mushrooms/")) group += "_" + relative.Split('/')[1];
                if (!groups.TryGetValue(group, out var items)) groups[group] = items = new List<Object>();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) items.Add(sprite);
            }
            foreach (var pair in groups)
            {
                var atlas = new SpriteAtlasAsset();
                atlas.SetIncludeInBuild(true);
                var packing = atlas.GetPackingSettings();
                packing.enableRotation = false; packing.enableTightPacking = false; packing.padding = 4;
                atlas.SetPackingSettings(packing);
                var texture = atlas.GetTextureSettings();
                texture.readable = false; texture.generateMipMaps = false; texture.sRGB = true; texture.filterMode = FilterMode.Bilinear;
                atlas.SetTextureSettings(texture);
                foreach (var platform in new[] { "DefaultTexturePlatform", "Standalone", "WebGL" })
                {
                    var settings = atlas.GetPlatformSettings(platform);
                    settings.name = platform; settings.maxTextureSize = 2048;
                    settings.format = TextureImporterFormat.DXT5; settings.textureCompression = TextureImporterCompression.CompressedHQ;
                    settings.overridden = platform != "DefaultTexturePlatform";
                    atlas.SetPlatformSettings(settings);
                }
                atlas.Add(pair.Value.ToArray());
                string path = AtlasRoot + "/" + pair.Key + ".spriteatlasv2";
                SpriteAtlasAsset.Save(atlas, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log($"[MoreMush] {groups.Count} sprite atlases created; FX/backgrounds excluded.");
        }

        [MenuItem("MoreMush/Optimize/Player and Render Settings")]
        public static void ConfigureSettings()
        {
            foreach (string name in new[] { "Mobile_RPAsset", "PC_RPAsset" })
            {
                var obj = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/" + name + ".asset");
                var so = new SerializedObject(obj);
                foreach (string field in new[] { "m_RequireDepthTexture", "m_RequireOpaqueTexture", "m_SupportsHDR" }) so.FindProperty(field).boolValue = false;
                so.FindProperty("m_RenderScale").floatValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var renderer = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/PC_Renderer.asset");
            var rs = new SerializedObject(renderer);
            rs.FindProperty("m_RenderingMode").intValue = 0; // Forward (URP 17)
            rs.ApplyModifiedPropertiesWithoutUndo();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/PC_Renderer.asset"))
            {
                var so = new SerializedObject(obj);
                var active = so.FindProperty("m_Active");
                if (active != null && obj.name == "ScreenSpaceAmbientOcclusion") { active.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                if (level.FindPropertyRelative("name").stringValue == "PC") level.FindPropertyRelative("vSyncCount").intValue = 1;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            AssetDatabase.SaveAssets();
        }

        public static string FontCharacters()
        {
            var chars = new SortedSet<char>(Enumerable.Range(32, 95).Select(i => (char)i));
            // Compile all shipped Korean text, including interpolated strings and serialized labels.
            foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/Prefabs", "*.prefab", SearchOption.AllDirectories)))
            {
                string text = Regex.Replace(File.ReadAllText(path), @"\\u([0-9a-fA-F]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
                foreach (char c in text) if ((c >= '\uac00' && c <= '\ud7a3') || (c >= '\u3131' && c <= '\u318e')) chars.Add(c);
            }
            foreach (char c in "★☆♥♡●○▲▼▶◀→←↑↓×÷±−–—…·≈≥≤∞①②③④⑤⑥⑦⑧⑨⑩巨") chars.Add(c);
            return new string(chars.ToArray());
        }

        [MenuItem("MoreMush/Optimize/Bake Static Fonts")]
        public static void BakeFonts()
        {
            string chars = FontCharacters();
            var fonts = new[] { "Assets/Fonts/NotoSansKR SDF.asset", "Assets/Fonts/Jua SDF.asset" }.Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).ToArray();
            foreach (var font in fonts)
            {
                if (font == null) throw new InvalidOperationException("Run Build Font Assets first.");
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                font.ClearFontAssetData();
                font.isMultiAtlasTexturesEnabled = true;
                font.TryAddCharacters(chars, out string missing);
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                font.isMultiAtlasTexturesEnabled = false;
                // Newly created atlas pages need to be persisted as subassets too.
                foreach (var texture in font.atlasTextures)
                {
                    if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
                    EditorUtility.SetDirty(texture);
                }
                EditorUtility.SetDirty(font);
                Debug.Log($"[MoreMush] {font.name}: {font.characterTable.Count} characters, {font.atlasTextures.Length} static page(s)");
            }
            string unsupported = new string(chars.Where(c => !fonts.Any(f => f.HasCharacter(c))).ToArray());
            if (unsupported.Length > 0) throw new InvalidOperationException("No font glyphs for: " + unsupported);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/font-characters.txt", chars, Encoding.UTF8);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MoreMush/Optimize/Move Unused TMP Samples")]
        public static void MoveUnusedTmpResources()
        {
            const string destination = "Assets/TextMesh Pro/Examples";
            if (!AssetDatabase.IsValidFolder(destination)) AssetDatabase.CreateFolder("Assets/TextMesh Pro", "Examples");
            Move("Assets/TextMesh Pro/Resources/Fonts & Materials", destination + "/Fonts & Materials");
            Move("Assets/TextMesh Pro/Resources/Sprite Assets", destination + "/Sprite Assets");
        }

        static void Move(string source, string destination)
        {
            if (!AssetDatabase.IsValidFolder(source)) return;
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }

        [MenuItem("MoreMush/Optimize/Write Texture Report")]
        public static void TextureReport()
        {
            var sb = new StringBuilder();
            var textures = new HashSet<Texture>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (texture != null) textures.Add(texture);
            }
            long bytes = textures.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            sb.AppendLine($"Source textures: {textures.Count}, loaded memory: {bytes / 1048576.0:F2} MiB (Editor, excludes atlas substitution)");
            foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { AtlasRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                var sprites = new Sprite[atlas.spriteCount]; atlas.GetSprites(sprites);
                foreach (var t in sprites.Where(s => s != null).Select(s => s.texture).Distinct())
                    sb.AppendLine($"{path}: {t.width}x{t.height}, {t.format}, {UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t) / 1048576.0:F2} MiB");
                foreach (var sprite in sprites) if (sprite != null) Object.DestroyImmediate(sprite);
            }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/texture-optimization.txt", sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
