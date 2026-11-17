using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// クリスマスツリーの形と、飾りを付ける15か所の位置を決めます。
    /// 位置は 1920×1080 の Canvas の左下を原点にした画素です。
    /// </summary>
    public static class ChristmasTreeLayout
    {
        /// <summary>ツリーの真ん中の横位置。</summary>
        public const float CenterX = 960f;

        /// <summary>ツリーのてっぺんの高さ。</summary>
        public const float ApexY = 900f;

        /// <summary>ツリーの葉の一番下の高さ。</summary>
        public const float BaseY = 190f;

        /// <summary>ツリーの葉の一番下の半分の幅。</summary>
        public const float BaseHalfWidth = 400f;

        /// <summary>飾りを並べる段の高さ（上から）。</summary>
        public static readonly float[] RowHeights = { 700f, 530f, 340f };

        /// <summary>段ごとの飾りの数（上から 2・3・4 個で合計9か所。10個目はてっぺんの星）。</summary>
        public static readonly int[] RowCounts = { 2, 3, 4 };

        /// <summary>星を付けるてっぺんの位置。</summary>
        public static readonly Vector2 StarPosition = new Vector2(CenterX, ApexY + 25f);

        /// <summary>
        /// 高さ y でのツリーの半分の幅を返します。
        /// </summary>
        /// <param name="y">高さ</param>
        /// <returns>半分の幅</returns>
        public static float GetHalfWidthAt(float y)
        {
            float t = Mathf.Clamp01((ApexY - y) / (ApexY - BaseY));
            return BaseHalfWidth * t;
        }

        /// <summary>
        /// 飾りを付ける位置を上の段から順に返します（2, 3, 4 個で合計9か所）。
        /// </summary>
        /// <returns>位置の一覧</returns>
        public static List<Vector2> GetOrnamentSlots()
        {
            List<Vector2> slots = new List<Vector2>();
            for (int row = 0; row < RowHeights.Length; row++)
            {
                AddRow(slots, RowHeights[row], RowCounts[row]);
            }

            return slots;
        }

        /// <summary>
        /// 飾りを付ける順番を、毎回ちがう順に混ぜて返します。
        /// </summary>
        /// <param name="count">位置の数</param>
        /// <param name="random">乱数</param>
        /// <returns>位置の番号の並び</returns>
        public static int[] GetShuffledOrder(int count, System.Random random)
        {
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temp = order[i];
                order[i] = order[j];
                order[j] = temp;
            }

            return order;
        }

        /// <summary>
        /// 何個目の飾りを赤い電球にするかを返します（3個に1個）。
        /// </summary>
        /// <param name="ornamentIndex">飾りの番号（0 始まり）</param>
        /// <returns>赤い電球なら true</returns>
        public static bool IsRedBulb(int ornamentIndex)
        {
            return ornamentIndex % 3 == 2;
        }

        private static void AddRow(List<Vector2> slots, float y, int count)
        {
            float spread = GetHalfWidthAt(y) * 1.5f;
            float spacing = count > 1 ? spread / (count - 1) * 0.9f : 0f;
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * spacing;
                slots.Add(new Vector2(CenterX + offset, y));
            }
        }
    }
}
