using System;
using UnityEngine;

namespace WallBurst.Detection
{
    /// <summary>
    /// 深度画像上の四隅と、壁（投影面）の正規化座標を対応づける。
    /// Kinect が壁と完全な平行でなくても、四隅の指定で傾きを吸収する。
    /// 壁座標は左下 (0,0)、右上 (1,1)。深度の Y は画像上端を 0 とする。
    /// </summary>
    public static class QuadMapper
    {
        /// <summary>
        /// 画素が四隅の内側にあるか。
        /// </summary>
        /// <param name="pixel">深度画素。</param>
        /// <param name="topLeft">左上。</param>
        /// <param name="topRight">右上。</param>
        /// <param name="bottomRight">右下。</param>
        /// <param name="bottomLeft">左下。</param>
        /// <returns>内側なら true。</returns>
        public static bool Contains(
            Vector2 pixel,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            Vector2 bottomLeft)
        {
            if (PointInTriangle(pixel, topLeft, topRight, bottomRight))
            {
                return true;
            }

            return PointInTriangle(pixel, topLeft, bottomRight, bottomLeft);
        }

        /// <summary>
        /// 深度画素を壁の正規化座標へ変換する。
        /// </summary>
        /// <param name="pixel">深度画素。</param>
        /// <param name="topLeft">左上。</param>
        /// <param name="topRight">右上。</param>
        /// <param name="bottomRight">右下。</param>
        /// <param name="bottomLeft">左下。</param>
        /// <param name="wallU">横 0〜1。</param>
        /// <param name="wallV">縦 0〜1。下が 0。</param>
        /// <returns>四辺形の内側なら true。</returns>
        public static bool TryPixelToWall(
            Vector2 pixel,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            Vector2 bottomLeft,
            out float wallU,
            out float wallV)
        {
            if (!TryInverse(pixel, bottomLeft, bottomRight, topRight, topLeft, out wallU, out wallV))
            {
                return false;
            }

            if (wallU < -0.02f || wallU > 1.02f || wallV < -0.02f || wallV > 1.02f)
            {
                return false;
            }

            wallU = Mathf.Clamp01(wallU);
            wallV = Mathf.Clamp01(wallV);
            return true;
        }

        /// <summary>
        /// 壁の正規化座標を深度画素へ変換する。
        /// </summary>
        /// <param name="wallU">横 0〜1。</param>
        /// <param name="wallV">縦 0〜1。下が 0。</param>
        /// <param name="topLeft">左上。</param>
        /// <param name="topRight">右上。</param>
        /// <param name="bottomRight">右下。</param>
        /// <param name="bottomLeft">左下。</param>
        /// <returns>深度画素。</returns>
        public static Vector2 WallToPixel(
            float wallU,
            float wallV,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            Vector2 bottomLeft)
        {
            float u = Mathf.Clamp01(wallU);
            float v = Mathf.Clamp01(wallV);
            Vector2 bottomEdge = Vector2.Lerp(bottomLeft, bottomRight, u);
            Vector2 topEdge = Vector2.Lerp(topLeft, topRight, u);
            return Vector2.Lerp(bottomEdge, topEdge, v);
        }

        /// <summary>
        /// 四隅の内側を 1、外側を 0 で埋める。
        /// </summary>
        /// <param name="mask">幅×高さのバッファ。</param>
        /// <param name="width">幅。</param>
        /// <param name="height">高さ。</param>
        /// <param name="topLeft">左上。</param>
        /// <param name="topRight">右上。</param>
        /// <param name="bottomRight">右下。</param>
        /// <param name="bottomLeft">左下。</param>
        public static void FillMask(
            byte[] mask,
            int width,
            int height,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            Vector2 bottomLeft)
        {
            if (mask == null || mask.Length != width * height)
            {
                throw new ArgumentException("マスクのサイズが深度画像と一致しません。");
            }

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    Vector2 pixel = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside = Contains(pixel, topLeft, topRight, bottomRight, bottomLeft);
                    mask[row + x] = inside ? (byte)1 : (byte)0;
                }
            }
        }

        /// <summary>
        /// 逆双一次補間。底辺左を (u,v)=(0,0)、上辺右を (1,1) とする。
        /// </summary>
        private static bool TryInverse(
            Vector2 point,
            Vector2 bottomLeft,
            Vector2 bottomRight,
            Vector2 topRight,
            Vector2 topLeft,
            out float u,
            out float v)
        {
            Vector2 edgeU = bottomRight - bottomLeft;
            Vector2 edgeV = topLeft - bottomLeft;
            Vector2 twist = topRight - bottomRight - topLeft + bottomLeft;
            Vector2 delta = point - bottomLeft;
            float k2 = Cross(twist, edgeV);
            float k1 = Cross(edgeU, edgeV) + Cross(delta, twist);
            float k0 = Cross(delta, edgeU);

            u = 0f;
            v = 0f;
            bool found = false;
            float bestScore = float.MaxValue;

            if (Mathf.Abs(k2) < 0.000001f)
            {
                if (Mathf.Abs(k1) < 0.000001f)
                {
                    return false;
                }

                ConsiderRoot(-k0 / k1, edgeU, edgeV, twist, delta, ref u, ref v, ref found, ref bestScore);
                return found;
            }

            float discriminant = k1 * k1 - 4f * k2 * k0;
            if (discriminant < 0f)
            {
                return false;
            }

            float root = Mathf.Sqrt(discriminant);
            ConsiderRoot((-k1 - root) / (2f * k2), edgeU, edgeV, twist, delta, ref u, ref v, ref found, ref bestScore);
            ConsiderRoot((-k1 + root) / (2f * k2), edgeU, edgeV, twist, delta, ref u, ref v, ref found, ref bestScore);
            return found;
        }

        /// <summary>
        /// 解の候補のうち、四辺形の内側に最も近いものを残す。
        /// </summary>
        private static void ConsiderRoot(
            float vCandidate,
            Vector2 edgeU,
            Vector2 edgeV,
            Vector2 twist,
            Vector2 delta,
            ref float u,
            ref float v,
            ref bool found,
            ref float bestScore)
        {
            float uCandidate;
            if (!TrySolveU(edgeU, edgeV, twist, delta, vCandidate, out uCandidate))
            {
                return;
            }

            float score = Outside(uCandidate) + Outside(vCandidate);
            if (score >= bestScore)
            {
                return;
            }

            bestScore = score;
            u = uCandidate;
            v = vCandidate;
            found = true;
        }

        /// <summary>
        /// v が決まったあと、横方向 u を大きい成分で割って求める。
        /// </summary>
        private static bool TrySolveU(
            Vector2 edgeU,
            Vector2 edgeV,
            Vector2 twist,
            Vector2 delta,
            float v,
            out float u)
        {
            Vector2 denominator = edgeU + (v * twist);
            Vector2 numerator = delta - (v * edgeV);
            if (Mathf.Abs(denominator.x) >= Mathf.Abs(denominator.y))
            {
                if (Mathf.Abs(denominator.x) < 0.000001f)
                {
                    u = 0f;
                    return false;
                }

                u = numerator.x / denominator.x;
                return true;
            }

            if (Mathf.Abs(denominator.y) < 0.000001f)
            {
                u = 0f;
                return false;
            }

            u = numerator.y / denominator.y;
            return true;
        }

        /// <summary>
        /// 0〜1 からどれだけはみ出すか。
        /// </summary>
        private static float Outside(float value)
        {
            if (value < 0f)
            {
                return -value;
            }

            if (value > 1f)
            {
                return value - 1f;
            }

            return 0f;
        }

        /// <summary>
        /// 三角形の内側か。辺上も含む。
        /// </summary>
        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float crossA = Cross(b - a, point - a);
            float crossB = Cross(c - b, point - b);
            float crossC = Cross(a - c, point - c);
            bool hasNegative = crossA < 0f || crossB < 0f || crossC < 0f;
            bool hasPositive = crossA > 0f || crossB > 0f || crossC > 0f;
            return !(hasNegative && hasPositive);
        }

        /// <summary>
        /// 2次元の外積。
        /// </summary>
        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }
    }
}
