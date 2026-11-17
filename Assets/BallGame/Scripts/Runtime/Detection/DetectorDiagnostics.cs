using System.Text;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 「この場所でボールが反応しない」原因を調べるための表を作ります。
    /// 四隅の内側を横に10個の帯に分け、帯ごとに、基準の深度が取れているか・揺れているか・
    /// 検知のしきい値がどれくらいか・壁をどれだけかすめて見ているかを数字にします。
    /// 画面に依存しないので、テストで確かめられます。
    /// </summary>
    public static class DetectorDiagnostics
    {
        /// <summary>横に分ける帯の数。</summary>
        public const int BandCount = 10;

        /// <summary>
        /// 帯ごとの表を文章にします。
        /// </summary>
        /// <param name="detector">校正済みの検知器</param>
        /// <param name="latestDepth">いまの深度（無ければ null）。いま 0 の画素の割合を出すのに使います</param>
        /// <returns>表（検知器が使えないときはその旨）</returns>
        public static string BuildBandTable(HitDetector detector, ushort[] latestDepth)
        {
            if (detector == null || !detector.IsConfigured)
            {
                return "検知器が校正されていません。";
            }

            int width = detector.Width;
            int height = detector.Height;
            if (!TryGetInsideRange(detector, out int minX, out int maxX))
            {
                return "四隅の内側に画素がありません。";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("帯\t画素x範囲\t内側の画素\t基準なし%\t揺れ%\t基準の深度mm\t必要な高さmm(平均/最大)\t壁との角度(1=正面)\tいま0%");
            for (int band = 0; band < BandCount; band++)
            {
                int from = minX + (maxX - minX + 1) * band / BandCount;
                int to = minX + (maxX - minX + 1) * (band + 1) / BandCount - 1;
                builder.AppendLine(BuildBandRow(detector, latestDepth, band, from, to, width, height));
            }

            return builder.ToString();
        }

        private static string BuildBandRow(HitDetector detector, ushort[] latestDepth, int band, int fromX, int toX, int width, int height)
        {
            int inside = 0;
            int noBaseline = 0;
            int unstable = 0;
            int depthCount = 0;
            int nowZero = 0;
            double depthSum = 0;
            double thresholdSum = 0;
            double thresholdMax = 0;
            double dotSum = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    int index = y * width + x;
                    if (!detector.IsInside(index))
                    {
                        continue;
                    }

                    inside++;
                    ushort baselineDepth = detector.GetBaselineDepth(index);
                    noBaseline += baselineDepth == 0 ? 1 : 0;
                    unstable += detector.BlindMask[index] && baselineDepth != 0 ? 1 : 0;
                    nowZero += latestDepth != null && latestDepth.Length == width * height && latestDepth[index] == 0 ? 1 : 0;
                    if (baselineDepth == 0)
                    {
                        continue;
                    }

                    float dot = Mathf.Max(0.05f, detector.GetNormalDot(index));
                    double needed = detector.GetNoiseThreshold(index);
                    depthCount++;
                    depthSum += baselineDepth;
                    thresholdSum += needed;
                    thresholdMax = System.Math.Max(thresholdMax, needed);
                    dotSum += dot;
                }
            }

            double denominator = System.Math.Max(1, inside);
            double valid = System.Math.Max(1, depthCount);
            return string.Format(
                "{0}\t{1}-{2}\t{3}\t{4:F0}\t{5:F0}\t{6:F0}\t{7:F0}/{8:F0}\t{9:F2}\t{10:F0}",
                band + 1, fromX, toX, inside, 100.0 * noBaseline / denominator, 100.0 * unstable / denominator, depthSum / valid, thresholdSum / valid, thresholdMax, dotSum / valid, 100.0 * nowZero / denominator);
        }

        private static bool TryGetInsideRange(HitDetector detector, out int minX, out int maxX)
        {
            minX = int.MaxValue;
            maxX = -1;
            for (int y = 0; y < detector.Height; y++)
            {
                for (int x = 0; x < detector.Width; x++)
                {
                    if (detector.IsInside(y * detector.Width + x))
                    {
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                    }
                }
            }

            return maxX >= 0;
        }
    }
}
