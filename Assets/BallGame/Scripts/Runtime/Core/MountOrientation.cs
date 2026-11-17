using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// Kinect の設置向き。
    /// </summary>
    public enum MountOrientation
    {
        /// <summary>横に設置（初期値）。Xbox ロゴが操作者側を向き、画像の下が近く、上にいくほど遠い。</summary>
        Side = 0,

        /// <summary>真下に設置。壁の真下から上を見る。画像の上が近く、下にいくほど遠い。</summary>
        Below = 1
    }

    /// <summary>
    /// 設置向きごとの画像の上下関係をまとめたクラス。
    /// 深度データそのものは上下反転しない。プレビュー表示だけを「遠い側が画面の上」にそろえる。
    /// </summary>
    public static class MountGeometry
    {
        /// <summary>
        /// 画像の下端がセンサーに近い側かどうかを返します。
        /// </summary>
        /// <param name="mount">設置向き</param>
        /// <returns>画像の下が近いとき true</returns>
        public static bool IsNearAtImageBottom(MountOrientation mount)
        {
            return mount == MountOrientation.Side;
        }

        /// <summary>
        /// プレビュー表示で上下を入れ替えるかどうかを返します（遠い側を画面の上に出すため）。
        /// </summary>
        /// <param name="mount">設置向き</param>
        /// <returns>表示を上下反転するとき true</returns>
        public static bool IsPreviewFlippedVertically(MountOrientation mount)
        {
            return !IsNearAtImageBottom(mount);
        }

        /// <summary>
        /// プレビュー上の位置（0〜1、上端が 0）を深度画像上の位置（0〜1、上端が 0）へ変換します。
        /// </summary>
        /// <param name="previewUv">プレビュー上の位置</param>
        /// <param name="mount">設置向き</param>
        /// <returns>深度画像上の位置</returns>
        public static Vector2 PreviewToImage(Vector2 previewUv, MountOrientation mount)
        {
            if (IsPreviewFlippedVertically(mount))
            {
                return new Vector2(previewUv.x, 1f - previewUv.y);
            }

            return previewUv;
        }

        /// <summary>
        /// 深度画像上の位置（0〜1、上端が 0）をプレビュー上の位置へ変換します。
        /// </summary>
        /// <param name="imageUv">深度画像上の位置</param>
        /// <param name="mount">設置向き</param>
        /// <returns>プレビュー上の位置</returns>
        public static Vector2 ImageToPreview(Vector2 imageUv, MountOrientation mount)
        {
            return PreviewToImage(imageUv, mount);
        }

        /// <summary>
        /// 画面に出す設置向きの名前を返します。
        /// </summary>
        /// <param name="mount">設置向き</param>
        /// <returns>表示名</returns>
        public static string GetDisplayName(MountOrientation mount)
        {
            return mount == MountOrientation.Side ? "横に設置" : "真下に設置";
        }
    }
}
