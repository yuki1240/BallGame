using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// ボールが壁に当たった1回ぶんの知らせ。
    /// </summary>
    public readonly struct WallHit
    {
        /// <summary>壁座標（投影範囲の左下が (0, 0)、右上が (1, 1)）。</summary>
        public readonly Vector2 Wall;

        /// <summary>深度画像の上の位置（0〜1、上端が 0）。確かめ用。</summary>
        public readonly Vector2 Image;

        /// <summary>当たった時刻（秒）。</summary>
        public readonly float Time;

        /// <summary>
        /// 作ります。
        /// </summary>
        /// <param name="wall">壁座標</param>
        /// <param name="image">深度画像の上の位置</param>
        /// <param name="time">時刻</param>
        public WallHit(Vector2 wall, Vector2 image, float time)
        {
            Wall = wall;
            Image = image;
            Time = time;
        }

        /// <summary>
        /// 画面の画素位置（左下が原点）を返します。プロジェクターの画面いっぱいが投影範囲です。
        /// </summary>
        /// <param name="screenWidth">画面の幅</param>
        /// <param name="screenHeight">画面の高さ</param>
        /// <returns>画面の位置</returns>
        public Vector2 ToScreen(float screenWidth, float screenHeight)
        {
            return new Vector2(Wall.x * screenWidth, Wall.y * screenHeight);
        }
    }
}
