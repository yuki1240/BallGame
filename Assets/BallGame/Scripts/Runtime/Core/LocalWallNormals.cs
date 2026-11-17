using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 基準深度から、画素ごとに「その場所の壁の向き」を求めます。
    /// Kinect が壁に沿って見ると、壁の奥では視線と壁の角度が数度しかなく、
    /// 壁全体を1枚の平面とみなすと向きのわずかなずれ（レンズのゆがみや壁の反り）で
    /// 壁からの距離が大きく狂います。場所ごとの向きを使うと、その誤差が打ち消されます。
    /// </summary>
    public static class LocalWallNormals
    {
        /// <summary>向きを求めるときに見る隣の画素までの距離（画素）。</summary>
        public const int DefaultRadius = 4;

        /// <summary>全体の平面からこれ以上ずれた向きは壁でないとみなし、全体の平面を使う（度）。</summary>
        public const float MaxDeviationDegrees = 35f;

        /// <summary>
        /// 画素ごとに「壁の向き・視線」の値（法線と z=1 の視線の内積）を求めます。
        /// 深度 d の点の壁からの距離は、この値 ×（d − 基準深度）で求まります（カメラ側が正）。
        /// </summary>
        /// <param name="baseline">基準深度</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="intrinsics">内部パラメータ</param>
        /// <param name="fallback">場所の向きを求められないときに使う全体の平面</param>
        /// <param name="radius">隣の画素までの距離</param>
        /// <returns>画素ごとの値（幅×高さ）</returns>
        public static float[] Build(ushort[] baseline, int width, int height, DepthIntrinsics intrinsics, WallPlane fallback, int radius = DefaultRadius)
        {
            float[] normalDotRay = new float[width * height];
            float minDot = Mathf.Cos(MaxDeviationDegrees * Mathf.Deg2Rad);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 normal = GetNormal(baseline, width, height, x, y, radius, intrinsics, fallback, minDot);
                    normalDotRay[y * width + x] = Vector3.Dot(normal, intrinsics.GetRay(x + 0.5f, y + 0.5f));
                }
            }

            return normalDotRay;
        }

        /// <summary>
        /// 1つの画素の場所の壁の向き（カメラ側を向く単位法線）を求めます。求められないときは全体の平面の向きです。
        /// </summary>
        /// <param name="baseline">基準深度</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="x">画素の x</param>
        /// <param name="y">画素の y</param>
        /// <param name="radius">隣の画素までの距離</param>
        /// <param name="intrinsics">内部パラメータ</param>
        /// <param name="fallback">全体の平面</param>
        /// <param name="minDot">全体の平面との向きの近さの下限（cos）</param>
        /// <returns>単位法線</returns>
        public static Vector3 GetNormal(ushort[] baseline, int width, int height, int x, int y, int radius, DepthIntrinsics intrinsics, WallPlane fallback, float minDot)
        {
            Vector3 center = Vector3.zero;
            Vector3 left = Vector3.zero;
            Vector3 right = Vector3.zero;
            Vector3 up = Vector3.zero;
            Vector3 down = Vector3.zero;
            bool ok = TryPoint(baseline, width, height, x, y, intrinsics, out center)
                && TryPoint(baseline, width, height, x - radius, y, intrinsics, out left)
                && TryPoint(baseline, width, height, x + radius, y, intrinsics, out right)
                && TryPoint(baseline, width, height, x, y - radius, intrinsics, out up)
                && TryPoint(baseline, width, height, x, y + radius, intrinsics, out down);
            if (!ok)
            {
                return fallback.Normal;
            }

            Vector3 normal = Vector3.Cross(right - left, down - up);
            if (normal.sqrMagnitude < 1e-6f)
            {
                return fallback.Normal;
            }

            normal.Normalize();
            if (Vector3.Dot(normal, center) > 0f)
            {
                // カメラ側を向くようにそろえる
                normal = -normal;
            }

            return Vector3.Dot(normal, fallback.Normal) >= minDot ? normal : fallback.Normal;
        }

        private static bool TryPoint(ushort[] baseline, int width, int height, int x, int y, DepthIntrinsics intrinsics, out Vector3 point)
        {
            point = Vector3.zero;
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return false;
            }

            ushort depth = baseline[y * width + x];
            if (depth == 0)
            {
                return false;
            }

            point = intrinsics.Unproject(x + 0.5f, y + 0.5f, depth);
            return true;
        }
    }
}
