using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 3×3 の射影変換。四隅で囲った深度画像の範囲を、壁座標（左下 (0,0)、右上 (1,1)）へ写します。
    /// </summary>
    public readonly struct Homography
    {
        private readonly double m00, m01, m02, m10, m11, m12, m20, m21, m22;

        /// <summary>
        /// 行列の要素から作ります。
        /// </summary>
        /// <param name="values">行優先の 9 要素</param>
        public Homography(double[] values)
        {
            m00 = values[0];
            m01 = values[1];
            m02 = values[2];
            m10 = values[3];
            m11 = values[4];
            m12 = values[5];
            m20 = values[6];
            m21 = values[7];
            m22 = values[8];
        }

        /// <summary>壁座標での四隅（左上・右上・右下・左下の順）。</summary>
        public static readonly Vector2[] WallCorners =
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f)
        };

        /// <summary>
        /// 深度画像上の四隅（左上・右上・右下・左下）から、壁座標への変換を作ります。
        /// </summary>
        /// <param name="imageCorners">深度画像上の四隅（0〜1、上端が 0）</param>
        /// <returns>深度画像 → 壁座標 の変換</returns>
        public static Homography FromImageCorners(IList<Vector2> imageCorners)
        {
            return FromPoints(imageCorners, WallCorners);
        }

        /// <summary>
        /// 4組の対応点から射影変換を求めます。
        /// </summary>
        /// <param name="source">変換前の4点</param>
        /// <param name="destination">変換後の4点</param>
        /// <returns>射影変換</returns>
        public static Homography FromPoints(IList<Vector2> source, IList<Vector2> destination)
        {
            if (source.Count != 4 || destination.Count != 4)
            {
                throw new ArgumentException("射影変換には4点が必要です。");
            }

            double[,] matrix = BuildSystem(source, destination);
            double[] solution = SolveLinearSystem(matrix, 8);
            return new Homography(new[]
            {
                solution[0], solution[1], solution[2],
                solution[3], solution[4], solution[5],
                solution[6], solution[7], 1.0
            });
        }

        /// <summary>
        /// 点を変換します。
        /// </summary>
        /// <param name="point">変換前の点</param>
        /// <returns>変換後の点</returns>
        public Vector2 Transform(Vector2 point)
        {
            double w = m20 * point.x + m21 * point.y + m22;
            if (Math.Abs(w) < 1e-12)
            {
                w = 1e-12;
            }

            double x = (m00 * point.x + m01 * point.y + m02) / w;
            double y = (m10 * point.x + m11 * point.y + m12) / w;
            return new Vector2((float)x, (float)y);
        }

        /// <summary>
        /// 逆変換を返します。
        /// </summary>
        /// <returns>逆変換</returns>
        public Homography Inverse()
        {
            double c00 = m11 * m22 - m12 * m21;
            double c01 = m02 * m21 - m01 * m22;
            double c02 = m01 * m12 - m02 * m11;
            double c10 = m12 * m20 - m10 * m22;
            double c11 = m00 * m22 - m02 * m20;
            double c12 = m02 * m10 - m00 * m12;
            double c20 = m10 * m21 - m11 * m20;
            double c21 = m01 * m20 - m00 * m21;
            double c22 = m00 * m11 - m01 * m10;
            double determinant = m00 * c00 + m01 * c10 + m02 * c20;
            if (Math.Abs(determinant) < 1e-15)
            {
                throw new InvalidOperationException("逆変換を作れません（四隅が一直線上にあります）。");
            }

            double inv = 1.0 / determinant;
            return new Homography(new[]
            {
                c00 * inv, c01 * inv, c02 * inv,
                c10 * inv, c11 * inv, c12 * inv,
                c20 * inv, c21 * inv, c22 * inv
            });
        }

        private static double[,] BuildSystem(IList<Vector2> source, IList<Vector2> destination)
        {
            double[,] matrix = new double[8, 9];
            for (int i = 0; i < 4; i++)
            {
                double x = source[i].x;
                double y = source[i].y;
                double u = destination[i].x;
                double v = destination[i].y;
                int row = i * 2;
                SetRow(matrix, row, x, y, 1, 0, 0, 0, -u * x, -u * y, u);
                SetRow(matrix, row + 1, 0, 0, 0, x, y, 1, -v * x, -v * y, v);
            }

            return matrix;
        }

        private static void SetRow(double[,] matrix, int row, params double[] values)
        {
            for (int column = 0; column < values.Length; column++)
            {
                matrix[row, column] = values[column];
            }
        }

        private static double[] SolveLinearSystem(double[,] matrix, int size)
        {
            for (int pivot = 0; pivot < size; pivot++)
            {
                SwapPivotRow(matrix, pivot, size);
                EliminateColumn(matrix, pivot, size);
            }

            double[] solution = new double[size];
            for (int row = 0; row < size; row++)
            {
                solution[row] = matrix[row, size] / matrix[row, row];
            }

            return solution;
        }

        private static void SwapPivotRow(double[,] matrix, int pivot, int size)
        {
            int best = pivot;
            for (int row = pivot + 1; row < size; row++)
            {
                if (Math.Abs(matrix[row, pivot]) > Math.Abs(matrix[best, pivot]))
                {
                    best = row;
                }
            }

            if (Math.Abs(matrix[best, pivot]) < 1e-12)
            {
                throw new InvalidOperationException("四隅から変換を作れません（点が重なっているか一直線上にあります）。");
            }

            for (int column = 0; column <= size; column++)
            {
                double temp = matrix[pivot, column];
                matrix[pivot, column] = matrix[best, column];
                matrix[best, column] = temp;
            }
        }

        private static void EliminateColumn(double[,] matrix, int pivot, int size)
        {
            for (int row = 0; row < size; row++)
            {
                if (row == pivot)
                {
                    continue;
                }

                double factor = matrix[row, pivot] / matrix[pivot, pivot];
                for (int column = pivot; column <= size; column++)
                {
                    matrix[row, column] -= factor * matrix[pivot, column];
                }
            }
        }
    }
}
