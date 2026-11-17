using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BallGame
{
    /// <summary>
    /// 背景画像を名前順で読み込みます。
    /// エディタでは Assets/Textures/Backgrounds を直接見て、ビルドでは Resources のカタログを使います。
    /// </summary>
    public static class BackgroundLibrary
    {
        /// <summary>背景画像のフォルダ。</summary>
        public const string FolderPath = "Assets/Textures/Backgrounds";

        /// <summary>
        /// 背景画像をすべて名前順で読み込みます。
        /// </summary>
        /// <returns>背景画像の一覧</returns>
        public static List<Texture2D> LoadAll()
        {
#if UNITY_EDITOR
            return LoadFromFolder();
#else
            return LoadFromCatalog();
#endif
        }

        /// <summary>
        /// 名前順（大文字小文字を区別しない序数比較）に並べ替えます。
        /// </summary>
        /// <param name="textures">背景画像</param>
        /// <returns>並べ替えた一覧</returns>
        public static List<Texture2D> SortByName(IEnumerable<Texture2D> textures)
        {
            List<Texture2D> sorted = new List<Texture2D>();
            foreach (Texture2D texture in textures)
            {
                if (texture != null)
                {
                    sorted.Add(texture);
                }
            }

            sorted.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return sorted;
        }

        /// <summary>
        /// Resources のカタログから読み込みます。
        /// </summary>
        /// <returns>背景画像の一覧</returns>
        public static List<Texture2D> LoadFromCatalog()
        {
            BackgroundCatalog catalog = Resources.Load<BackgroundCatalog>(BackgroundCatalog.ResourcePath);
            if (catalog == null)
            {
                Debug.LogWarning("背景カタログが見つかりません（Resources/BackgroundCatalog）。");
                return new List<Texture2D>();
            }

            return SortByName(catalog.textures);
        }

#if UNITY_EDITOR
        /// <summary>
        /// エディタで背景フォルダを直接読み込みます。
        /// </summary>
        /// <returns>背景画像の一覧</returns>
        public static List<Texture2D> LoadFromFolder()
        {
            List<Texture2D> textures = new List<Texture2D>();
            if (!AssetDatabase.IsValidFolder(FolderPath))
            {
                return textures;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FolderPath }))
            {
                textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            return SortByName(textures);
        }
#endif
    }
}
