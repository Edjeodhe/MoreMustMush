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

        public static void BuildAtlases()
        {
            Directory.CreateDirectory(AtlasRoot);
            var groups = new Dictionary<string, List<Object>>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { ArtRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string relative = path.Substring(ArtRoot.Length + 1);
                // WeatherMask and procedural FX use 0..1 UVs; never atlas these textures.
                if (relative.StartsWith("FX/") || relative.EndsWith("grandpa_ref.png")) continue;
                string group = relative.Split('/')[0];
                // Keep each full-screen background separate. The atlas adds compression padding
                // without resampling the NPOT source or changing the sprite's dimensions.
                if (relative.StartsWith("Backgrounds/")) group = "Background_" + Path.GetFileNameWithoutExtension(path);
                if (relative.StartsWith("Characters/")) group += "_" + relative.Split('/')[1];
                if (relative.StartsWith("Mushrooms/")) group += "_" + relative.Split('/')[1];
                if (!groups.TryGetValue(group, out var items)) groups[group] = items = new List<Object>();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) items.Add(sprite);
            }
            foreach (var pair in groups)
            {
                var atlas = new SpriteAtlasAsset();
                atlas.Add(pair.Value.ToArray());
                string path = AtlasRoot + "/" + pair.Key + ".spriteatlasv2";
                SpriteAtlasAsset.Save(atlas, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                // V2 settings belong to the importer; SpriteAtlasAsset setters are obsolete no-ops.
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                importer.includeInBuild = true;
                var packing = importer.packingSettings;
                packing.enableRotation = false; packing.enableTightPacking = false; packing.padding = 4;
                importer.packingSettings = packing;
                var texture = importer.textureSettings;
                texture.readable = false; texture.generateMipMaps = false; texture.sRGB = true; texture.filterMode = FilterMode.Bilinear;
                importer.textureSettings = texture;
                foreach (var platform in new[] { "DefaultTexturePlatform", "Standalone", "WebGL" })
                {
                    var settings = importer.GetPlatformSettings(platform);
                    settings.name = platform; settings.maxTextureSize = 2048;
                    settings.format = TextureImporterFormat.DXT5; settings.textureCompression = TextureImporterCompression.CompressedHQ;
                    settings.overridden = platform != "DefaultTexturePlatform";
                    importer.SetPlatformSettings(settings);
                }
                importer.SaveAndReimport();
            }
            Debug.Log($"[MoreMush] {groups.Count} sprite atlases created; FX excluded, backgrounds packed separately.");
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

        // 언어별 폰트 굽기는 LocTools가 번역표(strings.txt)의 언어 열로 한다
        [MenuItem("MoreMush/Optimize/Bake Static Fonts")]
        public static void BakeFonts() => Debug.Log("[MoreMush] " + LocTools.BakeFonts());

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
            var packedTextures = new HashSet<Texture2D>();
            var packedPaths = new HashSet<string>();
            foreach (var guid in AssetDatabase.IsValidFolder(AtlasRoot) ? AssetDatabase.FindAssets("t:SpriteAtlas", new[] { AtlasRoot }) : Array.Empty<string>())
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                foreach (var packable in SpriteAtlasExtensions.GetPackables(atlas)) packedPaths.Add(AssetDatabase.GetAssetPath(packable));
                var sprites = new Sprite[atlas.spriteCount]; atlas.GetSprites(sprites);
                foreach (var t in sprites.Where(s => s != null).Select(s => s.texture).Distinct())
                {
                    packedTextures.Add(t);
                    sb.AppendLine($"{path}: {t.width}x{t.height}, {t.format}, {UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t) / 1048576.0:F2} MiB");
                }
                foreach (var sprite in sprites) if (sprite != null) Object.DestroyImmediate(sprite);
            }
            // Editor memory includes extra native copies. Report GPU payload separately, counting atlas pages once.
            long Payload(Texture2D t) => (long)UnityEngine.Experimental.Rendering.GraphicsFormatUtility.ComputeMipmapSize(t.width, t.height, t.graphicsFormat);
            long atlasBytes = packedTextures.Sum(Payload);
            long looseBytes = textures.Cast<Texture2D>().Where(t => !packedPaths.Contains(AssetDatabase.GetAssetPath(t)) && !t.name.EndsWith("grandpa_ref")).Sum(Payload);
            sb.AppendLine($"GPU payload estimate (base mip, generated art): atlas {atlasBytes / 1048576.0:F2} + standalone {looseBytes / 1048576.0:F2} = {(atlasBytes + looseBytes) / 1048576.0:F2} MiB; excludes fonts, render targets, driver overhead.");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/texture-optimization.txt", sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
