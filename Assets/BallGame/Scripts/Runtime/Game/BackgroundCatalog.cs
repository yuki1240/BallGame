using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// ビルドで使う背景画像の一覧（Resources に置く）。
    /// 中身は Assets/Textures/Backgrounds と同じで、エディタが自動で作り直します。
    /// </summary>
    public sealed class BackgroundCatalog : ScriptableObject
    {
        /// <summary>Resources からの読み込み名。</summary>
        public const string ResourcePath = "BackgroundCatalog";

        /// <summary>背景画像（名前順）。</summary>
        public List<Texture2D> textures = new List<Texture2D>();
    }
}
