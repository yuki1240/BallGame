using System;

namespace BallGame
{
    /// <summary>
    /// 人がいない状態の深度を決まった枚数録り、画素ごとの中央値を基準深度にします。
    /// 全画素が 0 のフレームは枚数に数えません。
    /// </summary>
    public sealed class BaselineRecorder
    {
        /// <summary>録る枚数の初期値。</summary>
        public const int DefaultFrameCount = 30;

        /// <summary>録った間に一度でも 0 になった（不安定な）画素の揺れ幅の値。</summary>
        public const ushort UnstableNoise = ushort.MaxValue;

        private readonly int width;
        private readonly int height;
        private readonly ushort[][] frames;
        private int recordedFrames;
        private bool isRecording;

        /// <summary>
        /// 録画器を作ります。
        /// </summary>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="frameCount">録る枚数</param>
        public BaselineRecorder(int width, int height, int frameCount = DefaultFrameCount)
        {
            this.width = width;
            this.height = height;
            frames = new ushort[Math.Max(1, frameCount)][];
        }

        /// <summary>録る枚数。</summary>
        public int TargetFrames
        {
            get { return frames.Length; }
        }

        /// <summary>録れた枚数。</summary>
        public int RecordedFrames
        {
            get { return recordedFrames; }
        }

        /// <summary>録っている途中か。</summary>
        public bool IsRecording
        {
            get { return isRecording; }
        }

        /// <summary>録り終えたか。</summary>
        public bool IsComplete
        {
            get { return recordedFrames >= frames.Length; }
        }

        /// <summary>幅。</summary>
        public int Width
        {
            get { return width; }
        }

        /// <summary>高さ。</summary>
        public int Height
        {
            get { return height; }
        }

        /// <summary>
        /// 録り始めます（前に録った分は捨てます）。
        /// </summary>
        public void Begin()
        {
            recordedFrames = 0;
            isRecording = true;
        }

        /// <summary>
        /// 録るのをやめます。
        /// </summary>
        public void Cancel()
        {
            isRecording = false;
        }

        /// <summary>
        /// フレームを1枚加えます。全ゼロのフレームは数えません。
        /// </summary>
        /// <param name="depth">深度（幅×高さ）</param>
        /// <returns>数えたとき true</returns>
        public bool AddFrame(ushort[] depth)
        {
            if (!isRecording || IsComplete || depth == null || depth.Length != width * height)
            {
                return false;
            }

            if (IsAllZero(depth))
            {
                return false;
            }

            frames[recordedFrames] = (ushort[])depth.Clone();
            recordedFrames++;
            if (IsComplete)
            {
                isRecording = false;
            }

            return true;
        }

        /// <summary>
        /// 画素ごとの中央値で基準深度を作ります。有効な値が半分未満の画素は 0（無効）にします。
        /// </summary>
        /// <returns>基準深度</returns>
        public ushort[] BuildMedian()
        {
            int pixelCount = width * height;
            ushort[] result = new ushort[pixelCount];
            ushort[] samples = new ushort[recordedFrames];
            for (int i = 0; i < pixelCount; i++)
            {
                result[i] = MedianOfPixel(i, samples);
            }

            return result;
        }

        /// <summary>
        /// 画素ごとの揺れ幅（mm、中央値からのずれの中央値×1.4826）を作ります。
        /// 録った間に一度でも 0 になった画素は <see cref="UnstableNoise"/> にします。
        /// </summary>
        /// <returns>揺れ幅</returns>
        public ushort[] BuildNoise()
        {
            int pixelCount = width * height;
            ushort[] result = new ushort[pixelCount];
            ushort[] samples = new ushort[recordedFrames];
            int[] deviations = new int[recordedFrames];
            for (int i = 0; i < pixelCount; i++)
            {
                result[i] = NoiseOfPixel(i, samples, deviations);
            }

            return result;
        }

        /// <summary>
        /// 全画素が 0 かどうかを返します。
        /// </summary>
        /// <param name="depth">深度</param>
        /// <returns>全ゼロなら true</returns>
        public static bool IsAllZero(ushort[] depth)
        {
            for (int i = 0; i < depth.Length; i++)
            {
                if (depth[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private ushort NoiseOfPixel(int index, ushort[] samples, int[] deviations)
        {
            for (int f = 0; f < recordedFrames; f++)
            {
                ushort value = frames[f][index];
                if (value == 0)
                {
                    return UnstableNoise;
                }

                samples[f] = value;
            }

            Array.Sort(samples, 0, recordedFrames);
            int median = samples[recordedFrames / 2];
            for (int f = 0; f < recordedFrames; f++)
            {
                deviations[f] = Math.Abs(samples[f] - median);
            }

            Array.Sort(deviations, 0, recordedFrames);
            double sigma = deviations[recordedFrames / 2] * 1.4826;
            return (ushort)Math.Min(UnstableNoise - 1, Math.Max(1, (int)Math.Ceiling(sigma)));
        }

        private ushort MedianOfPixel(int index, ushort[] samples)
        {
            int validCount = 0;
            for (int f = 0; f < recordedFrames; f++)
            {
                ushort value = frames[f][index];
                if (value != 0)
                {
                    samples[validCount] = value;
                    validCount++;
                }
            }

            if (validCount == 0 || validCount * 2 < recordedFrames)
            {
                return 0;
            }

            Array.Sort(samples, 0, validCount);
            return samples[validCount / 2];
        }
    }
}
