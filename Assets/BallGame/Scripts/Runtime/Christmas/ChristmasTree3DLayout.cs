using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 3D のクリスマスツリーに付ける飾りの場所（高さとカメラから見た向き）と、星の形を決めます。
    /// ツリーの形は「とんがり帽子（円すい）」とみなして考えます。
    /// </summary>
    public static class ChristmasTree3DLayout
    {
        /// <summary>飾りを付ける段の高さ（葉の下 0 〜 てっぺん 1 の割合）。下の段から順に並べます。</summary>
        public static readonly float[] RowHeights01 = { 0.26f, 0.5f, 0.74f };

        /// <summary>各段に付ける飾りの数。</summary>
        public static readonly int[] RowCounts = { 4, 3, 2 };

        /// <summary>各段で飾りを左右に広げる幅（ツリーの見かけの幅の半分を 1 とした割合）。</summary>
        public static readonly float[] RowSpread01 = { 0.75f, 0.6f, 0.4f };

        /// <summary>
        /// 飾り1つ分の場所です。カメラから見たときの、ツリーの中での位置で表します。
        /// </summary>
        public struct Slot
        {
            /// <summary>高さの割合（0 〜 1）。</summary>
            public float Height01;

            /// <summary>カメラから見たツリーの左右の位置（-1 が左はし、0 が真ん中、1 が右はし）。</summary>
            public float Lateral01;
        }

        /// <summary>
        /// 飾りの場所をすべて返します（全部で 9 か所）。
        /// </summary>
        /// <returns>飾りの場所の一覧</returns>
        public static List<Slot> GetSlots()
        {
            List<Slot> slots = new List<Slot>();
            for (int row = 0; row < RowHeights01.Length; row++)
            {
                AddRow(slots, RowHeights01[row], RowCounts[row], RowSpread01[row]);
            }

            return slots;
        }

        private static void AddRow(List<Slot> slots, float height01, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                float lateral = count == 1 ? 0f : -spread + 2f * spread * i / (count - 1);
                slots.Add(new Slot { Height01 = height01, Lateral01 = lateral });
            }
        }

        /// <summary>
        /// 円すいとみなしたツリーの、その高さでの半径を返します。
        /// </summary>
        /// <param name="height01">高さの割合（0 〜 1）</param>
        /// <param name="baseRadius">いちばん下の半径</param>
        /// <returns>半径</returns>
        public static float GetConeRadius(float height01, float baseRadius)
        {
            return baseRadius * (1f - Mathf.Clamp01(height01));
        }

        /// <summary>見えなくする元の飾りのマテリアル名に含まれる言葉。</summary>
        public static readonly string[] HiddenMaterialKeywords = { "Decorations", "Lights" };

        /// <summary>
        /// 見えなくする元の飾り（飾り玉・電球）のマテリアルかどうかを返します。
        /// </summary>
        /// <param name="materialName">マテリアル名</param>
        /// <returns>見えなくするなら true</returns>
        public static bool IsHiddenMaterialName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
            {
                return false;
            }

            foreach (string keyword in HiddenMaterialKeywords)
            {
                if (materialName.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 星のふちの点を返します。先が真上を向き、外側と内側の点が交互に並びます（XY 平面）。
        /// </summary>
        /// <param name="points">星の先の数</param>
        /// <param name="outerRadius">外側の半径</param>
        /// <param name="innerRadius">内側の半径</param>
        /// <returns>ふちの点（points × 2 個）</returns>
        public static Vector3[] GetStarOutline(int points, float outerRadius, float innerRadius)
        {
            int count = points * 2;
            Vector3[] outline = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = (90f + 180f * i / points) * Mathf.Deg2Rad;
                float radius = i % 2 == 0 ? outerRadius : innerRadius;
                outline[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }

            return outline;
        }
    }
}
