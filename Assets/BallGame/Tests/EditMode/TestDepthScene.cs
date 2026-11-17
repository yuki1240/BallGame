using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// テスト用に、模擬の壁と、その手前の物体を描いた深度を作ります。
    /// </summary>
    public sealed class TestDepthScene
    {
        /// <summary>四隅の内側への余白。</summary>
        public const float CornerInset = 0.06f;

        /// <summary>壁を作る模擬深度（揺れなし）。</summary>
        public readonly SimulatedDepthSource Simulator;

        /// <summary>基準深度。</summary>
        public readonly ushort[] Baseline;

        /// <summary>
        /// 揺れのない壁と基準を作ります。
        /// </summary>
        /// <param name="mount">設置向き</param>
        public TestDepthScene(MountOrientation mount = MountOrientation.Side)
        {
            Simulator = new SimulatedDepthSource(mount) { AddNoise = false };
            Baseline = NewFrame();
            Simulator.TryGetLatestFrame(Baseline);
        }

        /// <summary>幅。</summary>
        public int Width
        {
            get { return Simulator.Width; }
        }

        /// <summary>高さ。</summary>
        public int Height
        {
            get { return Simulator.Height; }
        }

        /// <summary>
        /// 四隅と基準のそろった校正データを作ります。
        /// </summary>
        /// <returns>校正データ</returns>
        public CalibrationData CreateCalibration()
        {
            CalibrationData data = new CalibrationData();
            data.AddCorner(new Vector2(CornerInset, CornerInset));
            data.AddCorner(new Vector2(1f - CornerInset, CornerInset));
            data.AddCorner(new Vector2(1f - CornerInset, 1f - CornerInset));
            data.AddCorner(new Vector2(CornerInset, 1f - CornerInset));
            data.SetBaseline((ushort[])Baseline.Clone(), Width, Height);
            data.Mount = Simulator.Mount;
            return data;
        }

        /// <summary>
        /// 校正を取り込んだ検知器を作ります。
        /// </summary>
        /// <returns>検知器</returns>
        public HitDetector CreateDetector()
        {
            HitDetector detector = new HitDetector(new DetectionSettings());
            detector.Configure(CreateCalibration());
            return detector;
        }

        /// <summary>
        /// 壁だけの深度（基準の複製）を作ります。
        /// </summary>
        /// <returns>深度</returns>
        public ushort[] EmptyFrame()
        {
            return (ushort[])Baseline.Clone();
        }

        /// <summary>
        /// 円い物体を壁から指定の距離に描きます。
        /// </summary>
        /// <param name="frame">深度</param>
        /// <param name="centerX">中心 x（画素）</param>
        /// <param name="centerY">中心 y（画素）</param>
        /// <param name="radius">半径（画素）</param>
        /// <param name="distanceMm">壁からの距離（mm）。負なら深度 0 で描く</param>
        /// <returns>描いた画素数</returns>
        public int DrawDisc(ushort[] frame, float centerX, float centerY, float radius, float distanceMm)
        {
            int count = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float dx = x + 0.5f - centerX;
                    float dy = y + 0.5f - centerY;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        frame[y * Width + x] = DepthAt(y, distanceMm);
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// 長方形の物体を壁から指定の距離に描きます。
        /// </summary>
        /// <param name="frame">深度</param>
        /// <param name="left">左</param>
        /// <param name="top">上</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="distanceMm">壁からの距離（mm）</param>
        /// <returns>描いた画素数</returns>
        public int DrawRect(ushort[] frame, int left, int top, int width, int height, float distanceMm)
        {
            int count = 0;
            for (int y = top; y < top + height; y++)
            {
                for (int x = left; x < left + width; x++)
                {
                    frame[y * Width + x] = DepthAt(y, distanceMm);
                    count++;
                }
            }

            return count;
        }

        private ushort DepthAt(int row, float distanceMm)
        {
            if (distanceMm < 0f)
            {
                return 0;
            }

            return (ushort)Mathf.RoundToInt(Simulator.GetDepthAtWallDistance(row, distanceMm));
        }

        private ushort[] NewFrame()
        {
            return new ushort[Simulator.Width * Simulator.Height];
        }
    }
}
