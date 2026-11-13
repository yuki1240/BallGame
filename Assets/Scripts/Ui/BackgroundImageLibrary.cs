using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WallBurst.Ui
{
    /// <summary>
    /// Assets/Textures/Backgrounds に置いた画像を名前順で読む。
    /// エディタではそのフォルダを直接見る。ビルドでは、同じ中身を参照したカタログを使う。
    /// </summary>
    public static class BackgroundImageLibrary
    {
        /// <summary>プロジェクト上の画像フォルダ。</summary>
        public const string ProjectFolder = "Assets/Textures/Backgrounds";

        /// <summary>ビルド用カタログの Resources 名。</summary>
        public const string CatalogResource = "BackgroundCatalog";

        /// <summary>
        /// 画像を名前順で集める。
        /// </summary>
        /// <returns>名前順。無ければ空。</returns>
        public static Texture2D[] Load()
        {
#if UNITY_EDITOR
            Texture2D[] fromFolder = LoadProjectFolder();
            if (fromFolder.Length > 0)
            {
                return fromFolder;
            }
#endif
            return LoadCatalog();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Textures/Backgrounds を直接読む。スプライト設定の jpg も含む。
        /// </summary>
        /// <returns>名前順。</returns>
        private static Texture2D[] LoadProjectFolder()
        {
            if (!AssetDatabase.IsValidFolder(ProjectFolder))
            {
                return Array.Empty<Texture2D>();
            }

            Dictionary<int, Texture2D> found = new Dictionary<int, Texture2D>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ProjectFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AddOne(found, LoadTexture(path));
            }

            return ToSortedArray(found);
        }

        /// <summary>
        /// テクスチャか、スプライトの元画像を読む。
        /// </summary>
        /// <param name="path">アセットパス。</param>
        /// <returns>画像。読めなければ null。</returns>
        private static Texture2D LoadTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
            {
                return texture;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                return null;
            }

            return sprite.texture;
        }
#endif

        /// <summary>
        /// ビルドに同梱した一覧を読む。
        /// </summary>
        /// <returns>名前順。無ければ空。</returns>
        private static Texture2D[] LoadCatalog()
        {
            BackgroundCatalog catalog = Resources.Load<BackgroundCatalog>(CatalogResource);
            if (catalog == null || catalog.Images == null)
            {
                return Array.Empty<Texture2D>();
            }

            Dictionary<int, Texture2D> found = new Dictionary<int, Texture2D>();
            for (int i = 0; i < catalog.Images.Length; i++)
            {
                AddOne(found, catalog.Images[i]);
            }

            return ToSortedArray(found);
        }

        /// <summary>
        /// 同じ実体は一度だけ入れる。
        /// </summary>
        /// <param name="found">登録先。</param>
        /// <param name="texture">画像。</param>
        private static void AddOne(Dictionary<int, Texture2D> found, Texture2D texture)
        {
            if (texture == null || found.ContainsKey(texture.GetInstanceID()))
            {
                return;
            }

            found.Add(texture.GetInstanceID(), texture);
        }

        /// <summary>
        /// 名前順の配列にする。
        /// </summary>
        /// <param name="found">集めた画像。</param>
        /// <returns>名前順。</returns>
        private static Texture2D[] ToSortedArray(Dictionary<int, Texture2D> found)
        {
            List<Texture2D> textures = new List<Texture2D>(found.Values);
            textures.Sort(CompareName);
            return textures.ToArray();
        }

        /// <summary>
        /// ファイル名の順。
        /// </summary>
        /// <param name="left">左。</param>
        /// <param name="right">右。</param>
        /// <returns>比較結果。</returns>
        private static int CompareName(Texture2D left, Texture2D right)
        {
            string leftName = left == null ? string.Empty : left.name;
            string rightName = right == null ? string.Empty : right.name;
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }
    }
}
