using UnityEngine;

namespace WallBurst.Ui
{
    /// <summary>
    /// ビルドに含める背景画像の一覧。中身は Textures/Backgrounds から作る。
    /// </summary>
    public sealed class BackgroundCatalog : ScriptableObject
    {
        /// <summary>名前順の画像。</summary>
        public Texture2D[] Images = System.Array.Empty<Texture2D>();
    }
}
