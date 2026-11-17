using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BallGame.Editor
{
    /// <summary>
    /// Assets/Textures/Backgrounds と同じ内容の Resources カタログ（Assets/Resources/BackgroundCatalog.asset）を作ります。
    /// ビルドの直前と、背景フォルダの中身が変わったときに自動で作り直します。
    /// </summary>
    public sealed class BackgroundCatalogBuilder : IPreprocessBuildWithReport
    {
        /// <summary>カタログの保存先。</summary>
        public const string CatalogPath = "Assets/Resources/BackgroundCatalog.asset";

        /// <summary>ビルド前処理の順番。</summary>
        public int callbackOrder
        {
            get { return 0; }
        }

        /// <summary>
        /// ビルドの直前にカタログを作り直します。
        /// </summary>
        /// <param name="report">ビルドの情報</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            Rebuild();
        }

        /// <summary>
        /// メニューからカタログを作り直します。
        /// </summary>
        [MenuItem("BallGame/背景カタログを更新")]
        public static void RebuildFromMenu()
        {
            BackgroundCatalog catalog = Rebuild();
            Debug.Log("背景カタログを更新しました（" + catalog.textures.Count + " 枚）。");
        }

        /// <summary>
        /// カタログを作り直します。
        /// </summary>
        /// <returns>作り直したカタログ</returns>
        public static BackgroundCatalog Rebuild()
        {
            BackgroundCatalog catalog = LoadOrCreateCatalog();
            List<Texture2D> textures = BackgroundLibrary.LoadFromFolder();
            if (!IsSame(catalog.textures, textures))
            {
                catalog.textures = textures;
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }

            return catalog;
        }

        private static BackgroundCatalog LoadOrCreateCatalog()
        {
            BackgroundCatalog catalog = AssetDatabase.LoadAssetAtPath<BackgroundCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            catalog = ScriptableObject.CreateInstance<BackgroundCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        private static bool IsSame(List<Texture2D> current, List<Texture2D> next)
        {
            if (current == null || current.Count != next.Count)
            {
                return false;
            }

            for (int i = 0; i < next.Count; i++)
            {
                if (current[i] != next[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
