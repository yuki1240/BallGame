using System;
using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 壁の面（カメラから見た3Dの平面）。
    /// <para>面の向き（Normal）は、カメラがある側を正にそろえます。
    /// そのため <see cref="DistanceTo"/> は「壁から手前（カメラ側）へ何 mm 出ているか」になります。</para>
    /// </summary>
    public readonly struct WallPlane
    {
        /// <summary>面の向き（長さ1、カメラ側が正）。</summary>
        public readonly Vector3 Normal;

        /// <summary>面の位置（Normal・点 = Offset が面の上）。</summary>
        public readonly float Offset;

        /// <summary>面として使えるか。</summary>
        public readonly bool IsValid;

        private WallPlane(Vector3 normal, float offset)
        {
            Normal = normal;
            Offset = offset;
            IsValid = true;
        }

        /// <summary>
        /// 向きと位置から面を作ります。カメラ（原点）側が正になるよう向きをそろえます。
        /// </summary>
        /// <param name="normal">向き</param>
        /// <param name="offset">位置</param>
        /// <returns>面</returns>
        public static WallPlane FromNormalAndOffset(Vector3 normal, float offset)
        {
            float length = normal.magnitude;
            if (length < 1e-6f)
            {
                return default;
            }

            Vector3 n = normal / length;
            float d = offset / length;

            // 原点（カメラ）の距離 = -d。これが正になるよう向きをそろえる
            if (-d < 0f)
            {
                n = -n;
                d = -d;
            }

            return new WallPlane(n, d);
        }

        /// <summary>
        /// 点が壁から手前へ何 mm 出ているかを返します（壁の奥は負）。
        /// </summary>
        /// <param name="point">3Dの位置（mm）</param>
        /// <returns>距離（mm）</returns>
        public float DistanceTo(Vector3 point)
        {
            return Vector3.Dot(Normal, point) - Offset;
        }

        /// <summary>
        /// 点を壁の面の上に下ろした位置を返します。
        /// </summary>
        /// <param name="point">3Dの位置（mm）</param>
        /// <returns>面の上の位置</returns>
        public Vector3 ProjectOnto(Vector3 point)
        {
            return point - Normal * DistanceTo(point);
        }

        /// <summary>
        /// たくさんの点に、いちばん合う面を当てはめます（主成分分析：点の広がりがいちばん小さい向きを面の向きにする）。
        /// </summary>
        /// <param name="points">点（3つ以上）</param>
        /// <returns>面（作れないときは IsValid が false）</returns>
        public static WallPlane Fit(IList<Vector3> points)
        {
            if (points == null || points.Count < 3)
            {
                return default;
            }

            double cx = 0, cy = 0, cz = 0;
            for (int i = 0; i < points.Count; i++)
            {
                cx += points[i].x;
                cy += points[i].y;
                cz += points[i].z;
            }

            cx /= points.Count;
            cy /= points.Count;
            cz /= points.Count;
            double[,] c = new double[3, 3];
            for (int i = 0; i < points.Count; i++)
            {
                double dx = points[i].x - cx;
                double dy = points[i].y - cy;
                double dz = points[i].z - cz;
                c[0, 0] += dx * dx;
                c[0, 1] += dx * dy;
                c[0, 2] += dx * dz;
                c[1, 1] += dy * dy;
                c[1, 2] += dy * dz;
                c[2, 2] += dz * dz;
            }

            c[1, 0] = c[0, 1];
            c[2, 0] = c[0, 2];
            c[2, 1] = c[1, 2];
            double[] normal = SmallestEigenVector(c);
            if (normal == null)
            {
                return default;
            }

            Vector3 n = new Vector3((float)normal[0], (float)normal[1], (float)normal[2]);
            float offset = (float)(normal[0] * cx + normal[1] * cy + normal[2] * cz);
            return FromNormalAndOffset(n, offset);
        }

        /// <summary>
        /// 面から大きく外れた点（壁でない物）を除きながら、3回当てはめ直します。
        /// </summary>
        /// <param name="points">点</param>
        /// <param name="minimumInlierMm">外れたとみなす距離の下限（mm）</param>
        /// <returns>面</returns>
        public static WallPlane FitRobust(IList<Vector3> points, float minimumInlierMm)
        {
            WallPlane plane = Fit(points);
            List<Vector3> inliers = new List<Vector3>(points.Count);
            for (int round = 0; round < 3 && plane.IsValid; round++)
            {
                List<float> distances = new List<float>(points.Count);
                for (int i = 0; i < points.Count; i++)
                {
                    distances.Add(Mathf.Abs(plane.DistanceTo(points[i])));
                }

                distances.Sort();
                float limit = Mathf.Max(minimumInlierMm, distances[distances.Count / 2] * 3f);
                inliers.Clear();
                for (int i = 0; i < points.Count; i++)
                {
                    if (Mathf.Abs(plane.DistanceTo(points[i])) <= limit)
                    {
                        inliers.Add(points[i]);
                    }
                }

                if (inliers.Count < 3)
                {
                    break;
                }

                plane = Fit(inliers);
            }

            return plane;
        }

        // 3×3 の対称行列の、いちばん小さい固有値の固有ベクトル（ヤコビ法）
        private static double[] SmallestEigenVector(double[,] matrix)
        {
            double[,] a = (double[,])matrix.Clone();
            double[,] v = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            for (int sweep = 0; sweep < 50; sweep++)
            {
                double off = Math.Abs(a[0, 1]) + Math.Abs(a[0, 2]) + Math.Abs(a[1, 2]);
                if (off < 1e-12)
                {
                    break;
                }

                for (int p = 0; p < 2; p++)
                {
                    for (int q = p + 1; q < 3; q++)
                    {
                        if (Math.Abs(a[p, q]) < 1e-15)
                        {
                            continue;
                        }

                        double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                        double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                        if (theta == 0)
                        {
                            t = 1;
                        }

                        double cos = 1 / Math.Sqrt(t * t + 1);
                        double sin = t * cos;
                        Rotate(a, v, p, q, cos, sin);
                    }
                }
            }

            int smallest = 0;
            for (int i = 1; i < 3; i++)
            {
                if (a[i, i] < a[smallest, smallest])
                {
                    smallest = i;
                }
            }

            double[] result = { v[0, smallest], v[1, smallest], v[2, smallest] };
            double length = Math.Sqrt(result[0] * result[0] + result[1] * result[1] + result[2] * result[2]);
            if (length < 1e-12 || double.IsNaN(length))
            {
                return null;
            }

            return new[] { result[0] / length, result[1] / length, result[2] / length };
        }

        private static void Rotate(double[,] a, double[,] v, int p, int q, double cos, double sin)
        {
            for (int k = 0; k < 3; k++)
            {
                double akp = a[k, p];
                double akq = a[k, q];
                a[k, p] = cos * akp - sin * akq;
                a[k, q] = sin * akp + cos * akq;
            }

            for (int k = 0; k < 3; k++)
            {
                double apk = a[p, k];
                double aqk = a[q, k];
                a[p, k] = cos * apk - sin * aqk;
                a[q, k] = sin * apk + cos * aqk;
            }

            for (int k = 0; k < 3; k++)
            {
                double vkp = v[k, p];
                double vkq = v[k, q];
                v[k, p] = cos * vkp - sin * vkq;
                v[k, q] = sin * vkp + cos * vkq;
            }
        }
    }
}
