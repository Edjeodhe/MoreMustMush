using UnityEditor;

// Images dropped into the art folder (Resources/Art, Tools/art/build-art.mjs) import as ready-to-use 2D sprites.
public class GeneratedArtPostprocessor : AssetPostprocessor
{
    private static readonly string[] k_ArtFolders = { "Assets/Resources/Art/" };

    private void OnPreprocessTexture()
    {
        if (!System.Array.Exists(k_ArtFolders, f => assetPath.StartsWith(f))) return;
        var importer = (TextureImporter)assetImporter;

        // FX shapes (circle, soft, ring, square) are exactly one world unit wide so code can scale them by size.
        // 하위 폴더(FX/Impact·Boss·Wind·Growth: 플립북·시트)는 코드가 그림 폭으로 크기를 맞추므로 건드리지 않는다
        if (System.IO.Path.GetDirectoryName(assetPath).Replace(System.IO.Path.DirectorySeparatorChar, '/') == "Assets/Resources/Art/FX")
        {
            importer.GetSourceTextureWidthAndHeight(out int fw, out _);
            importer.spritePixelsPerUnit = fw;
        }

        // Only set defaults on first import so manual tweaks in the Inspector stick.
        if (!assetImporter.importSettingsMissing) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = MoreMush.EditorTools.ProjectOptimization.TextureLimit(assetPath);
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
    }
}
