using UnityEditor;

// Images dropped into Assets/Art/Generated (e.g. by Tools/codex-image.mjs) import as ready-to-use 2D sprites.
public class GeneratedArtPostprocessor : AssetPostprocessor
{
    private const string k_GeneratedFolder = "Assets/Art/Generated/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(k_GeneratedFolder)) return;
        var importer = (TextureImporter)assetImporter;

        // FX shapes (circle, soft, ring, square) are exactly one world unit wide so code can scale them by size
        if (assetPath.Contains("/FX/"))
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
