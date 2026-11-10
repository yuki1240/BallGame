using System;

namespace WallBurst.Detection
{
    /// <summary>
    /// 設置時に複数フレームを貯め、画素ごとの中央値を壁の基準深度にする。
    /// 0（穴）と、通りがかりの数フレームは中央値から外れる。
    /// </summary>
    public sealed class BaselineAccumulator
    {
        private readonly int _pixelCount;
        private readonly ushort[][] _frames;
        private int _collected;

        /// <summary>
        /// 蓄積バッファを確保する。
        /// </summary>
        /// <param name="width">深度幅。</param>
        /// <param name="height">深度高さ。</param>
        /// <param name="frameCount">貯めるフレーム数。奇数に近いほど中央値が安定する。</param>
        public BaselineAccumulator(int width, int height, int frameCount)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "深度画像のサイズが不正です。");
            }

            if (frameCount < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount), "基準フレームは 3 枚以上必要です。");
            }

            Width = width;
            Height = height;
            TargetFrames = frameCount;
            _pixelCount = width * height;
            _frames = new ushort[frameCount][];
        }

        /// <summary>深度幅。</summary>
        public int Width { get; private set; }

        /// <summary>深度高さ。</summary>
        public int Height { get; private set; }

        /// <summary>目標枚数。</summary>
        public int TargetFrames { get; private set; }

        /// <summary>すでに貯めた枚数。</summary>
        public int CollectedFrames
        {
            get { return _collected; }
        }

        /// <summary>目標まで貯まったか。</summary>
        public bool IsComplete
        {
            get { return _collected >= TargetFrames; }
        }

        /// <summary>
        /// 測定値らしい深度が1つでもあるか。全ゼロの起動直後フレームを基準に混ぜない。
        /// </summary>
        /// <param name="depth">ミリメートル深度。</param>
        /// <returns>有効そうなら true。</returns>
        public static bool ContainsMeasurement(ushort[] depth)
        {
            if (depth == null || depth.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < depth.Length; i += 16)
            {
                if (depth[i] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// フレームをコピーして貯める。入力バッファは次フレームで上書きされてよい。
        /// </summary>
        /// <param name="depth">ミリメートル深度。</param>
        /// <returns>この追加で目標枚数に達したら true。</returns>
        public bool Add(ushort[] depth)
        {
            if (depth == null)
            {
                throw new ArgumentNullException(nameof(depth));
            }

            if (depth.Length != _pixelCount)
            {
                throw new ArgumentException("深度フレームのサイズが一致しません。");
            }

            if (_collected >= TargetFrames)
            {
                return true;
            }

            ushort[] copy = new ushort[_pixelCount];
            Buffer.BlockCopy(depth, 0, copy, 0, _pixelCount * sizeof(ushort));
            _frames[_collected] = copy;
            _collected++;
            return _collected >= TargetFrames;
        }

        /// <summary>
        /// 画素ごとの中央値を作る。有効サンプルが半分未満の画素は 0（無効）。
        /// </summary>
        /// <returns>基準深度。</returns>
        public ushort[] CreateMedian()
        {
            if (_collected < TargetFrames)
            {
                throw new InvalidOperationException("基準フレームがまだ揃っていません。");
            }

            ushort[] baseline = new ushort[_pixelCount];
            ushort[] scratch = new ushort[_collected];
            int minimumValid = _collected / 2;

            for (int pixel = 0; pixel < _pixelCount; pixel++)
            {
                baseline[pixel] = MedianAt(pixel, scratch, minimumValid);
            }

            return baseline;
        }

        /// <summary>
        /// 1画素の中央値。0 は穴として除外する。
        /// </summary>
        /// <param name="pixel">画素インデックス。</param>
        /// <param name="scratch">並べ替え用の作業配列。</param>
        /// <param name="minimumValid">これ未満なら無効。</param>
        /// <returns>中央値。無効なら 0。</returns>
        private ushort MedianAt(int pixel, ushort[] scratch, int minimumValid)
        {
            int count = 0;
            for (int frame = 0; frame < _collected; frame++)
            {
                ushort sample = _frames[frame][pixel];
                if (sample == 0)
                {
                    continue;
                }

                scratch[count] = sample;
                count++;
            }

            if (count < minimumValid)
            {
                return 0;
            }

            Array.Sort(scratch, 0, count);
            return scratch[count / 2];
        }
    }
}
