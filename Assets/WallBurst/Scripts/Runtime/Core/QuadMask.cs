using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 四隅で囲った台形の内側かどうかを調べる道具。
    /// </summary>
    public static class QuadMask
    {
        /// <summary>
        /// 画素ごとに、台形の内側なら true の表を作ります。
        /// </summary>
        /// <param name="corners">四隅（画像の 0〜1、上端が 0）</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <returns>内側の表（幅×高さ）</returns>
        public static bool[] Build(IList<Vector2> corners, int width, int height)
        {
            bool[] mask = new bool[width * height];
            if (corners == null || corners.Count != 4)
            {
                return mask;
            }

            Vector2[] pixels = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                pixels[i] = new Vector2(corners[i].x * width, corners[i].y * height);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    mask[y * width + x] = IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), pixels);
                }
            }

            return mask;
        }

        /// <summary>
        /// 点が多角形の内側かどうかを返します。
        /// </summary>
        /// <param name="point">点</param>
        /// <param name="polygon">多角形の頂点</param>
        /// <returns>内側なら true</returns>
        public static bool IsPointInPolygon(Vector2 point, IList<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// 四隅がねじれていない（となり合う辺が交差しない）凸の四角形かどうかを返します。
        /// </summary>
        /// <param name="corners">四隅</param>
        /// <returns>凸の四角形なら true</returns>
        public static bool IsConvex(IList<Vector2> corners)
        {
            if (corners == null || corners.Count != 4)
            {
                return false;
            }

            int sign = 0;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i];
                Vector2 b = corners[(i + 1) % 4];
                Vector2 c = corners[(i + 2) % 4];
                float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                if (Mathf.Abs(cross) < 1e-6f)
                {
                    return false;
                }

                int s = cross > 0 ? 1 : -1;
                if (sign == 0)
                {
                    sign = s;
                }
                else if (s != sign)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
