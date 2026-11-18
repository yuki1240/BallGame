using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 深度カメラの内部パラメータ（焦点距離と画像の中心）。
    /// 深度画像の画素と深度（mm）から、カメラから見た3Dの位置（mm）を求めるのに使います。
    /// <para>座標の向き：X は画像の右、Y は画像の下、Z はカメラの前（奥）が正です。</para>
    /// </summary>
    public readonly struct DepthIntrinsics
    {
        /// <summary>Kinect v2（512×424）の焦点距離（画素）。</summary>
        public const float KinectFocal512 = 365.5f;

        /// <summary>横の焦点距離（画素）。</summary>
        public readonly float FocalX;

        /// <summary>縦の焦点距離（画素）。</summary>
        public readonly float FocalY;

        /// <summary>画像の中心の X（画素）。</summary>
        public readonly float CenterX;

        /// <summary>画像の中心の Y（画素）。</summary>
        public readonly float CenterY;

        /// <summary>
        /// 値を指定して作ります。
        /// </summary>
        /// <param name="focalX">横の焦点距離</param>
        /// <param name="focalY">縦の焦点距離</param>
        /// <param name="centerX">中心の X</param>
        /// <param name="centerY">中心の Y</param>
        public DepthIntrinsics(float focalX, float focalY, float centerX, float centerY)
        {
            FocalX = focalX;
            FocalY = focalY;
            CenterX = centerX;
            CenterY = centerY;
        }

        /// <summary>
        /// 画像の大きさに合わせた内部パラメータを返します（Kinect v2 の画角を、大きさに合わせて縮めたもの）。
        /// </summary>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <returns>内部パラメータ</returns>
        public static DepthIntrinsics ForResolution(int width, int height)
        {
            float scale = width / 512f;
            return new DepthIntrinsics(KinectFocal512 * scale, KinectFocal512 * scale, (width - 1) * 0.5f, (height - 1) * 0.5f);
        }

        /// <summary>
        /// 画素（u, v）の視線の向き（Z を 1 にしたもの）を返します。
        /// </summary>
        /// <param name="u">横の画素位置</param>
        /// <param name="v">縦の画素位置</param>
        /// <returns>視線の向き</returns>
        public Vector3 Ray(float u, float v)
        {
            return new Vector3((u - CenterX) / FocalX, (v - CenterY) / FocalY, 1f);
        }

        /// <summary>
        /// 画素と深度から3Dの位置（mm）を返します。
        /// </summary>
        /// <param name="u">横の画素位置</param>
        /// <param name="v">縦の画素位置</param>
        /// <param name="depthMm">深度（mm）</param>
        /// <returns>3Dの位置</returns>
        public Vector3 ToPoint(float u, float v, float depthMm)
        {
            return Ray(u, v) * depthMm;
        }

        /// <summary>
        /// 3Dの位置を画像の画素位置に写します。
        /// </summary>
        /// <param name="point">3Dの位置（Z は正）</param>
        /// <returns>画素位置</returns>
        public Vector2 Project(Vector3 point)
        {
            float z = Mathf.Max(1f, point.z);
            return new Vector2(point.x / z * FocalX + CenterX, point.y / z * FocalY + CenterY);
        }

        /// <summary>
        /// 深度 depthMm の所での1画素の大きさ（mm）を返します。
        /// </summary>
        /// <param name="depthMm">深度（mm）</param>
        /// <returns>1画素の大きさ（mm）</returns>
        public float PixelSizeMm(float depthMm)
        {
            return depthMm / FocalX;
        }
    }
}
