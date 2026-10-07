using UnityEditor;
using UnityEngine;

public sealed class BulletFrameMaskImporter : AssetPostprocessor
{
    private const string FrameRoot = "Assets/Resources/BulletFrames/";

    private void OnPreprocessTexture()
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        if (!normalizedPath.StartsWith(FrameRoot,
                System.StringComparison.Ordinal))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 512;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;
    }
}
