using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 飾りの置き場所を、ツリーの上にランダムに、重なりすぎないように決めます（画面に関係しないので、テストで確かめられます）。
    /// <para>ツリーを「円すい」とみなし、カメラから見える前側の面に点を置きます。
    /// 候補の点をたくさんランダムに置き、その中から「すでに選んだ点から十分離れた点」を選んでいきます。
    /// 決めた距離より離れた点が無くなったら、いちばん離れた点を選ぶので、必ず決めた数を置けて、できるだけ重なりません。</para>
    /// </summary>
    public static class OrnamentPlacer
    {
        /// <summary>前側として使う角度の半分（度）。0° がカメラの正面。</summary>
        public const float FrontHalfAngleDegrees = 65f;

        /// <summary>候補の点の数。</summary>
        public const int CandidateCount = 2000;

        /// <summary>
        /// ツリーの形（円すい）。
        /// </summary>
        public struct Cone
        {
            /// <summary>円すいの軸の位置（X, Z）。</summary>
            public Vector2 AxisXZ;

            /// <summary>飾りを置く範囲の下の高さ。</summary>
            public float BottomY;

            /// <summary>飾りを置く範囲の上の高さ（スターの少し下）。</summary>
            public float TopY;

            /// <summary>BottomY での半径。</summary>
            public float BottomRadius;

            /// <summary>TopY での半径。</summary>
            public float TopRadius;
        }

        /// <summary>
        /// 置き場所を決めます。カメラは -Z の側（手前）にいるとします。
        /// </summary>
        /// <param name="cone">ツリーの形</param>
        /// <param name="count">置く数</param>
        /// <param name="minDistance">点どうしを離す距離</param>
        /// <param name="random">乱数</param>
        /// <returns>置き場所（下から上の順ではなく、決めた順）</returns>
        public static List<Vector3> Generate(Cone cone, int count, float minDistance, System.Random random)
        {
            // まず、ツリーの前側に候補の点をたくさん（2000個）ランダムに置く
            List<Vector3> candidates = new List<Vector3>(CandidateCount);
            for (int i = 0; i < CandidateCount; i++)
            {
                candidates.Add(Sample(cone, random));
            }

            // 候補の中から、すでに選んだ点からいちばん遠い点を、1つずつ選んでいく（最遠点サンプリング）。
            // こうすると、点どうしがいちばん離れるように、ツリー全体に散らばる
            List<Vector3> points = new List<Vector3>(count);
            float[] nearest = new float[candidates.Count];
            for (int i = 0; i < nearest.Length; i++)
            {
                nearest[i] = float.PositiveInfinity;
            }

            int pick = random.Next(candidates.Count);
            while (points.Count < count)
            {
                Vector3 chosen = candidates[pick];
                points.Add(chosen);
                float best = -1f;
                int bestIndex = -1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float distance = Vector3.Distance(candidates[i], chosen);
                    if (distance < nearest[i])
                    {
                        nearest[i] = distance;
                    }

                    // 決めた距離より離れている候補の中からは、ランダムに選ぶ（毎回ちがう並びにする）
                    float score = nearest[i] >= minDistance ? minDistance + (float)random.NextDouble() * minDistance : nearest[i];
                    if (score > best)
                    {
                        best = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                {
                    break;
                }

                pick = bestIndex;
            }

            return points;
        }

        /// <summary>
        /// いちばん近い2点の距離を返します（確かめ用）。
        /// </summary>
        /// <param name="points">点</param>
        /// <returns>距離（2点未満のときは無限大）</returns>
        public static float MinimumSpacing(IList<Vector3> points)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    best = Mathf.Min(best, Vector3.Distance(points[i], points[j]));
                }
            }

            return best;
        }

        private static Vector3 Sample(Cone cone, System.Random random)
        {
            // 円すいの面は下ほど広いので、下を多めに選ぶ（面積に比例させる）
            float s = Mathf.Sqrt((float)random.NextDouble());
            float t = 1f - s;
            float y = Mathf.Lerp(cone.BottomY, cone.TopY, t);
            float radius = Mathf.Lerp(cone.BottomRadius, cone.TopRadius, t);
            float angle = ((float)random.NextDouble() * 2f - 1f) * FrontHalfAngleDegrees * Mathf.Deg2Rad;
            return new Vector3(cone.AxisXZ.x + Mathf.Sin(angle) * radius, y, cone.AxisXZ.y - Mathf.Cos(angle) * radius);
        }

    }
}
