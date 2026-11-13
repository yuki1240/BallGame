using UnityEngine;

namespace WallBurst.Detection
{
    /// <summary>
    /// 四隅の基準深度から壁の面を作り、画素が壁から何mm手前かを返す。
    /// Kinect が壁に沿っていると、奥行きの差は壁からの距離よりずっと大きくなる。
    /// </summary>
    public struct WallPlane
    {
        private const float KinectWidth = 512f;
        private const float KinectHeight = 424f;
        private const float KinectFocal = 365.456f;
        private const float KinectCenterX = 254.878f;
        private const float KinectCenterY = 205.395f;

        private float _normalX;
        private float _normalY;
        private float _normalZ;
        private float _centerX;
        private float _centerY;
        private float _invFocalX;
        private float _invFocalY;

        /// <summary>四隅から面を作れたとき true。</summary>
        public bool Valid { get; private set; }

        /// <summary>
        /// 基準の四隅で壁の面を作る。四隅に深度が無いときは無効。
        /// </summary>
        /// <param name="data">基準と四隅。</param>
        /// <returns>壁の面。作れないときは無効。</returns>
        public static WallPlane Fit(CalibrationData data)
        {
            WallPlane plane = new WallPlane();
            if (data == null || !data.HasAllCorners || data.Baseline == null)
            {
                return plane;
            }

            float centerX;
            float centerY;
            float invFocalX;
            float invFocalY;
            CameraScale(data.Width, data.Height, out centerX, out centerY, out invFocalX, out invFocalY);

            Vector3 bottomLeft;
            Vector3 bottomRight;
            Vector3 topLeft;
            if (!TryCorner(data, data.Corners[3], centerX, centerY, invFocalX, invFocalY, out bottomLeft)
                || !TryCorner(data, data.Corners[2], centerX, centerY, invFocalX, invFocalY, out bottomRight)
                || !TryCorner(data, data.Corners[0], centerX, centerY, invFocalX, invFocalY, out topLeft))
            {
                return plane;
            }

            Vector3 normal = Vector3.Cross(bottomRight - bottomLeft, topLeft - bottomLeft);
            if (normal.sqrMagnitude < 10000f)
            {
                return plane;
            }

            if (Vector3.Dot(normal, bottomLeft) > 0f)
            {
                normal = -normal;
            }

            normal.Normalize();
            plane._normalX = normal.x;
            plane._normalY = normal.y;
            plane._normalZ = normal.z;
            plane._centerX = centerX;
            plane._centerY = centerY;
            plane._invFocalX = invFocalX;
            plane._invFocalY = invFocalY;
            plane.Valid = true;
            return plane;
        }

        /// <summary>
        /// 同じ画素で、基準より現在が壁の手前へ何mm動いたか。
        /// </summary>
        /// <param name="pixelX">深度画素の中心 X。</param>
        /// <param name="pixelY">深度画素の中心 Y。画像上端が小さい。</param>
        /// <param name="baseDepth">基準の奥行き（mm）。</param>
        /// <param name="currentDepth">今の奥行き（mm）。</param>
        /// <returns>壁へ向かって手前なら正。面の裏側なら負。</returns>
        public int MillimetersCloser(float pixelX, float pixelY, int baseDepth, int currentDepth)
        {
            float dirX = (pixelX - _centerX) * _invFocalX;
            float dirY = (_centerY - pixelY) * _invFocalY;
            float towardWall = -((dirX * _normalX) + (dirY * _normalY) + _normalZ);
            float closer = (baseDepth - currentDepth) * towardWall;
            return Mathf.RoundToInt(closer);
        }

        /// <summary>
        /// 画素から伸びる視線の向き。Z 成分は 1。
        /// </summary>
        /// <param name="width">深度の幅。</param>
        /// <param name="height">深度の高さ。</param>
        /// <param name="pixelX">画素中心 X。</param>
        /// <param name="pixelY">画素中心 Y。</param>
        /// <param name="dirX">右が正。</param>
        /// <param name="dirY">上が正。</param>
        public static void CameraRay(int width, int height, float pixelX, float pixelY, out float dirX, out float dirY)
        {
            float centerX;
            float centerY;
            float invFocalX;
            float invFocalY;
            CameraScale(width, height, out centerX, out centerY, out invFocalX, out invFocalY);
            dirX = (pixelX - centerX) * invFocalX;
            dirY = (centerY - pixelY) * invFocalY;
        }

        /// <summary>
        /// Kinect v2 の深度カメラを、今の解像度へ縮める。
        /// </summary>
        private static void CameraScale(int width, int height, out float centerX, out float centerY, out float invFocalX, out float invFocalY)
        {
            float scaleX = width / KinectWidth;
            float scaleY = height / KinectHeight;
            centerX = KinectCenterX * scaleX;
            centerY = KinectCenterY * scaleY;
            float focalX = KinectFocal * scaleX;
            float focalY = KinectFocal * scaleY;
            invFocalX = focalX > 0.001f ? 1f / focalX : 0f;
            invFocalY = focalY > 0.001f ? 1f / focalY : 0f;
        }

        /// <summary>
        /// 隅の近くで深度がある画素を、カメラ座標の点にする。
        /// </summary>
        private static bool TryCorner(
            CalibrationData data,
            Vector2 corner,
            float centerX,
            float centerY,
            float invFocalX,
            float invFocalY,
            out Vector3 point)
        {
            int x;
            int y;
            int depth;
            if (!TryDepth(data, corner, out x, out y, out depth))
            {
                point = Vector3.zero;
                return false;
            }

            float pixelX = x + 0.5f;
            float pixelY = y + 0.5f;
            float dirX = (pixelX - centerX) * invFocalX;
            float dirY = (centerY - pixelY) * invFocalY;
            point = new Vector3(dirX * depth, dirY * depth, depth);
            return true;
        }

        /// <summary>
        /// 隅の周囲で、最初に見つかった有効深度。
        /// </summary>
        private static bool TryDepth(CalibrationData data, Vector2 corner, out int x, out int y, out int depth)
        {
            int originX = Mathf.RoundToInt(corner.x);
            int originY = Mathf.RoundToInt(corner.y);
            for (int radius = 0; radius <= 4; radius++)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    int sampleY = originY + offsetY;
                    if (sampleY < 0 || sampleY >= data.Height)
                    {
                        continue;
                    }

                    int row = sampleY * data.Width;
                    for (int offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        int sampleX = originX + offsetX;
                        if (sampleX < 0 || sampleX >= data.Width)
                        {
                            continue;
                        }

                        ushort sample = data.Baseline[row + sampleX];
                        if (sample == 0)
                        {
                            continue;
                        }

                        x = sampleX;
                        y = sampleY;
                        depth = sample;
                        return true;
                    }
                }
            }

            x = 0;
            y = 0;
            depth = 0;
            return false;
        }
    }
}
