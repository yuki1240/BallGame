using UnityEditor;

namespace BallGame.Editor
{
    /// <summary>
    /// 画像の取り込み設定をそろえ、背景フォルダが変わったら背景カタログを作り直します。
    /// Flowers の画像は Sprite に、Backgrounds の画像は縮小・ミップマップなしにします（初回の取り込みだけ）。
    /// </summary>
    public sealed class BallGameAssetPostprocessor : AssetPostprocessor
    {
        /// <summary>花の画像のフォルダ。</summary>
        public const string FlowerFolder = "Assets/Textures/Flowers";

        private void OnPreprocessTexture()
        {
            TextureImporter importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing)
            {
                return;
            }

            if (assetPath.StartsWith(FlowerFolder + "/"))
            {
                ApplyFlowerSettings(importer);
            }
            else if (assetPath.StartsWith(BackgroundLibrary.FolderPath + "/"))
            {
                ApplyBackgroundSettings(importer);
            }
        }

        private static void ApplyFlowerSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
        }

        private static void ApplyBackgroundSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (TouchesBackgrounds(importedAssets) || TouchesBackgrounds(deletedAssets) || TouchesBackgrounds(movedAssets) || TouchesBackgrounds(movedFromAssetPaths))
            {
                EditorApplication.delayCall -= RebuildCatalog;
                EditorApplication.delayCall += RebuildCatalog;
            }
        }

        private static bool TouchesBackgrounds(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.StartsWith(BackgroundLibrary.FolderPath + "/"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RebuildCatalog()
        {
            BackgroundCatalogBuilder.Rebuild();
        }
    }
}
