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
        static string[] ArtRoots => MoreMushSetup.ArtRoots.Where(AssetDatabase.IsValidFolder).ToArray();
        public const string AtlasRoot = "Assets/Resources/Atlases";

        public static int TextureLimit(string path)
        {
            if (path.Contains("/Icons/") || path.Contains("/Mushrooms/") || path.Contains("/Harvesters/")) return 256;
            if (path.Contains("/Characters/Critters/")) return 256;
            if (path.Contains("/Characters/NPC/") || path.Contains("/Farm/Props/") || path.Contains("/Farm/Tree/") || path.Contains("/Bosses/")) return 512;
            return 2048; // Preserve backgrounds, UI borders, and the assembled grandpa rig.
        }

        [MenuItem("MoreMush/Optimize/Textures and Atlases")]
        public static void ConfigureTextures()
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", ArtRoots))
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

        static readonly string[] UvBound = { "FX/brush", "FX/square", "FX/tree_soil" };

        public static void BuildAtlases()
        {
            Directory.CreateDirectory(AtlasRoot);
            var groups = new Dictionary<string, List<Object>>();
            // Only the sprite each key resolves to (Soft over Generated) is packed; replaced sprites stay out of the build.
            foreach (var kv in MoreMushSetup.ResolvedArt())
            {
                string path = kv.Value;
                string relative = kv.Key + Path.GetExtension(path);
                // 0..1 UV에 기대는 것(번개 선의 붓 결 FX/brush, 날씨 가림막 FX/square — WeatherMask 셰이더가 UV로 구멍 위치를 잡는다)과
                // 큰 농장 흙은 빼고, 나머지 FX는 한 장에 묶는다: 라운드 조각·타격 효과·그림자가 같은 텍스처를 써야 SpriteRenderer끼리
                // 묶여 그려진다 (모바일 그리기 호출)
                if (UvBound.Any(relative.StartsWith) || relative.EndsWith("grandpa_ref.png")) continue;
                string group = relative.Split('/')[0];
                // Keep each full-screen background separate. The atlas adds compression padding
                // without resampling the NPOT source or changing the sprite's dimensions.
                if (relative.StartsWith("Backgrounds/")) group = "Background_" + Path.GetFileNameWithoutExtension(path);
                if (relative.StartsWith("Characters/")) group += "_" + relative.Split('/')[1];
                // 낱개·군락 버섯은 한 아틀라스로: 필드에서 y 순서로 섞여 그려져 텍스처가 갈리면 묶음이 끊긴다
                if (relative.StartsWith("Mushrooms/")) group = "Mushrooms";
                if (!groups.TryGetValue(group, out var items)) groups[group] = items = new List<Object>();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) items.Add(sprite);
            }
            // 예전 그룹(버섯 낱개·군락 따로)이 남아 있으면 같은 그림이 두 아틀라스에 들어가니 지운다
            foreach (string stale in new[] { "Mushrooms_Single", "Mushrooms_Colony" })
                AssetDatabase.DeleteAsset(AtlasRoot + "/" + stale + ".spriteatlasv2");
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
                    settings.name = platform; settings.maxTextureSize = AtlasMaxSize(pair.Key);
                    settings.format = TextureImporterFormat.DXT5; settings.textureCompression = TextureImporterCompression.CompressedHQ;
                    settings.overridden = platform != "DefaultTexturePlatform";
                    importer.SetPlatformSettings(settings);
                }
                foreach (string platform in MOBILE) importer.SetPlatformSettings(MobileSettings(importer.GetPlatformSettings(platform), platform, AtlasMaxSize(pair.Key)));
                importer.SaveAndReimport();
            }
            Debug.Log($"[MoreMush] {groups.Count} sprite atlases created; FX excluded, backgrounds packed separately.");
        }

        // 모바일 GPU는 DXT5를 못 읽어 실행 중에 RGBA32로 풀어 올린다 (메모리 4배, 불러올 때 끊김).
        // 안드로이드·iOS는 ASTC 6x6 (픽셀당 3.56비트, DXT5의 절반 아래)로 따로 굽는다 (2026-10-21 모바일 버벅임)
        static readonly string[] MOBILE = { "Android", "iPhone" };

        static TextureImporterPlatformSettings MobileSettings(TextureImporterPlatformSettings s, string platform, int maxSize)
        {
            s.name = platform; s.overridden = true; s.maxTextureSize = maxSize;
            s.format = TextureImporterFormat.ASTC_6x6; s.textureCompression = TextureImporterCompression.CompressedHQ;
            return s;
        }

        // 아틀라스를 다시 묶지 않고 모바일 압축만 건다: 지금 아틀라스와 아틀라스 밖 그림(FX/brush·square·tree_soil)
        [MenuItem("MoreMush/Optimize/Mobile Texture Compression")]
        public static void ConfigureMobileCompression()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { AtlasRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is SpriteAtlasImporter importer)) continue;
                int max = AtlasMaxSize(Path.GetFileNameWithoutExtension(path));
                foreach (string platform in MOBILE) importer.SetPlatformSettings(MobileSettings(importer.GetPlatformSettings(platform), platform, max));
                importer.SaveAndReimport(); n++;
            }
            foreach (var kv in MoreMushSetup.ResolvedArt())
            {
                string relative = kv.Key + Path.GetExtension(kv.Value);
                if (!UvBound.Any(relative.StartsWith)) continue;
                if (!(AssetImporter.GetAtPath(kv.Value) is TextureImporter importer)) continue;
                foreach (string platform in MOBILE) importer.SetPlatformTextureSettings(MobileSettings(importer.GetPlatformTextureSettings(platform), platform, TextureLimit(kv.Value)));
                importer.SaveAndReimport(); n++;
            }
            Debug.Log($"[MoreMush] mobile ASTC compression set on {n} textures/atlases.");
        }

        // 버섯 150장(256px)은 2048 한 장에 안 들어가 여러 장으로 나뉘면 묶음이 끊긴다 → 4096 한 장
        static int AtlasMaxSize(string group) => group == "Mushrooms" ? 4096 : 2048;

        [MenuItem("MoreMush/Optimize/Player and Render Settings")]
        public static void ConfigureSettings()
        {
            foreach (string name in new[] { "Mobile_RPAsset", "PC_RPAsset" })
            {
                var obj = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/" + name + ".asset");
                var so = new SerializedObject(obj);
                foreach (string field in new[] { "m_RequireDepthTexture", "m_RequireOpaqueTexture", "m_SupportsHDR" }) so.FindProperty(field).boolValue = false;
                // MoreMush/Sprite 셰이더는 SRP Batcher 대상이 아니라, SpriteRenderer는 동적 배칭이 켜져 있어야 같은 재질·텍스처끼리 묶인다
                so.FindProperty("m_SupportsDynamicBatching").boolValue = true;
                so.FindProperty("m_RenderScale").floatValue = 1;
                // 2D 스프라이트 게임이라 그림자를 쓰지 않는다 (켜 두면 2048 그림자 맵을 잡고 매 프레임 그림자 패스를 준비한다)
                foreach (string field in new[] { "m_MainLightShadowsSupported", "m_AdditionalLightShadowsSupported" }) so.FindProperty(field).boolValue = false;
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

        // 언어별 폰트 굽기는 LocTools가 번역표(DB String 시트)의 언어 열로 한다
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
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", ArtRoots))
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
