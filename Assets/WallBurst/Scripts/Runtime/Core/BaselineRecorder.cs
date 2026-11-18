using System;

namespace WallBurst
{
    /// <summary>
    /// 人がいないときの深度を何フレームか録り、画素ごとの「基準の深度」と「揺れ幅」を作ります。
    /// <list type="bullet">
    /// <item>基準：取れたフレームの中央値（真ん中の値。ちらつきに強い）</item>
    /// <item>揺れ幅：大きい方から 1/5 と小さい方から 1/5 の差の半分</item>
    /// <item>深度が 0（取れない）になったフレームが多い画素は「不安定」として扱います</item>
    /// </list>
    /// </summary>
    public sealed class BaselineRecorder
    {
        /// <summary>録るフレーム数の初期値。</summary>
        public const int DefaultFrameCount = 30;

        /// <summary>0 のフレームがこの割合を超えた画素は、基準なし（0）にする。</summary>
        public const float MissingRatioLimit = 0.5f;

        /// <summary>0 のフレームがこの割合を超えた画素は、不安定な画素にする。</summary>
        public const float UnstableRatioLimit = 0.1f;

        private readonly int width;
        private readonly int height;
        private readonly int frameCount;
        private readonly ushort[][] frames;
        private int recorded;

        /// <summary>
        /// 録る準備をします。
        /// </summary>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="frameCount">録るフレーム数</param>
        public BaselineRecorder(int width, int height, int frameCount = DefaultFrameCount)
        {
            this.width = width;
            this.height = height;
            this.frameCount = Math.Max(3, frameCount);
            frames = new ushort[this.frameCount][];
        }

        /// <summary>録り終えたか。</summary>
        public bool IsDone
        {
            get { return recorded >= frameCount; }
        }

        /// <summary>進み具合（0〜1）。</summary>
        public float Progress
        {
            get { return recorded / (float)frameCount; }
        }

        /// <summary>
        /// 1フレーム加えます。
        /// </summary>
        /// <param name="depth">深度（幅×高さ）</param>
        public void Add(ushort[] depth)
        {
            if (IsDone || depth == null || depth.Length != width * height)
            {
                return;
            }

            frames[recorded] = (ushort[])depth.Clone();
            recorded++;
        }

        /// <summary>
        /// 録ったフレームから基準と揺れ幅を作ります。
        /// </summary>
        /// <param name="baseline">基準の深度（mm）</param>
        /// <param name="noise">揺れ幅（mm）。不安定な画素は <see cref="CalibrationData.UnstableNoise"/></param>
        public void Build(out ushort[] baseline, out ushort[] noise)
        {
            int count = width * height;
            baseline = new ushort[count];
            noise = new ushort[count];
            ushort[] values = new ushort[recorded];
            for (int i = 0; i < count; i++)
            {
                int valid = 0;
                for (int f = 0; f < recorded; f++)
                {
                    ushort d = frames[f][i];
                    if (d != 0)
                    {
                        values[valid++] = d;
                    }
                }

                int missing = recorded - valid;
                if (valid == 0 || missing > recorded * MissingRatioLimit)
                {
                    baseline[i] = 0;
                    noise[i] = CalibrationData.UnstableNoise;
                    continue;
                }

                Array.Sort(values, 0, valid);
                baseline[i] = values[valid / 2];
                if (missing > recorded * UnstableRatioLimit)
                {
                    noise[i] = CalibrationData.UnstableNoise;
                    continue;
                }

                int low = values[valid / 5];
                int high = values[valid - 1 - valid / 5];
                noise[i] = (ushort)Math.Min(1000, (high - low + 1) / 2);
            }
        }
    }
}
