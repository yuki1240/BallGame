using System;

namespace WallBurst.Effects
{
    /// <summary>
    /// 背景画像ごとに、重ならない3種類の演出を割り当てる。
    /// 画像は名前順で、0 が最初の画像。4枚目以降は先頭の組に戻る。
    /// </summary>
    public static class EffectSets
    {
        /// <summary>1枚の画像に付ける演出の数。</summary>
        public const int PerImage = 3;

        /// <summary>用意した組の数。</summary>
        public const int SetCount = 3;

        /// <summary>
        /// いまの画像の3種類から1つ選ぶ。
        /// </summary>
        /// <param name="imageIndex">名前順の画像番号。画像が無いときは 0。</param>
        /// <param name="roll">0 以上 3 未満を返す。</param>
        /// <returns>WallBurstEffect の種類。</returns>
        public static int Pick(int imageIndex, Func<int, int> roll)
        {
            int set = 0;
            if (imageIndex > 0)
            {
                set = imageIndex % SetCount;
            }

            int offset = 0;
            if (roll != null)
            {
                offset = roll(PerImage);
            }

            if (offset < 0 || offset >= PerImage)
            {
                offset = 0;
            }

            return (set * PerImage) + offset;
        }
    }
}
