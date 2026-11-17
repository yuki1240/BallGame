using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// Kinect が無いときの 320×240 の模擬深度。
    /// 壁に沿って見たときと同じく、画像の下が近く上が遠い勾配の壁を作ります（真下設置では上下が逆）。
    /// Game 画面のクリックをボールとして入れられます。
    /// </summary>
    public sealed class SimulatedDepthSource : IDepthSource
    {
        /// <summary>模擬深度の幅。</summary>
        public const int SimulatedWidth = 320;

        /// <summary>模擬深度の高さ。</summary>
        public const int SimulatedHeight = 240;

        /// <summary>壁の面の傾き（y − Tilt・z = Height の Tilt）。</summary>
        public const float WallTilt = -0.75f;

        /// <summary>壁の面の位置（mm）。</summary>
        public const float WallHeightMm = 780f;

        /// <summary>揺れの大きさ（±mm）。ノイズ床より十分小さくします。</summary>
        public const int NoiseAmplitudeMm = 4;

        /// <summary>Kinect の最短距離（mm）。これより近い画素は 0 にします。</summary>
        public const int MinimumDepthMm = 500;

        private sealed class SimulatedBall
        {
            public Vector2 Pixel;
            public float RadiusPixels;
            public float DistanceFromWallMm;
            public int RemainingFrames;
        }

        private readonly List<SimulatedBall> balls = new List<SimulatedBall>();
        private readonly DepthIntrinsics intrinsics = DepthIntrinsics.ForResolution(SimulatedWidth, SimulatedHeight);
        private readonly System.Random random;
        private MountOrientation mount;
        private readonly float wallTilt;
        private readonly float wallHeightMm;

        /// <summary>
        /// 模擬深度を作ります。
        /// </summary>
        /// <param name="mount">設置向き</param>
        /// <param name="seed">揺れの乱数の種</param>
        /// <param name="tilt">壁の面の傾き（テストで壁の向きを変えるとき用）</param>
        /// <param name="heightMm">壁の面の位置（mm）</param>
        public SimulatedDepthSource(MountOrientation mount, int seed = 12345, float tilt = WallTilt, float heightMm = WallHeightMm)
        {
            this.mount = mount;
            wallTilt = tilt;
            wallHeightMm = heightMm;
            random = new System.Random(seed);
        }

        /// <summary>幅。</summary>
        public int Width
        {
            get { return SimulatedWidth; }
        }

        /// <summary>高さ。</summary>
        public int Height
        {
            get { return SimulatedHeight; }
        }

        /// <summary>入力源の種類。</summary>
        public DepthSourceKind Kind
        {
            get { return DepthSourceKind.Simulated; }
        }

        /// <summary>常に使えます。</summary>
        public bool IsAvailable
        {
            get { return true; }
        }

        /// <summary>設置向き（壁の勾配の向き）。</summary>
        public MountOrientation Mount
        {
            get { return mount; }
            set { mount = value; }
        }

        /// <summary>揺れを入れるか。テストでは切れます。</summary>
        public bool AddNoise { get; set; } = true;

        /// <summary>
        /// ボールを入れます。指定したフレーム数だけ壁の手前に現れます。
        /// </summary>
        /// <param name="imageUv">深度画像上の 0〜1（上端が 0）</param>
        /// <param name="frames">見えているフレーム数</param>
        /// <param name="distanceFromWallMm">壁からの距離（mm）</param>
        /// <param name="radiusPixels">半径（画素）</param>
        public void InjectBall(Vector2 imageUv, int frames = 6, float distanceFromWallMm = 70f, float radiusPixels = 4.5f)
        {
            balls.Add(new SimulatedBall
            {
                Pixel = new Vector2(imageUv.x * SimulatedWidth, imageUv.y * SimulatedHeight),
                RadiusPixels = radiusPixels,
                DistanceFromWallMm = distanceFromWallMm,
                RemainingFrames = Mathf.Max(1, frames)
            });
        }

        /// <summary>
        /// 新しい模擬フレームを作って buffer へ写します。
        /// </summary>
        /// <param name="buffer">写し先（320×240）</param>
        /// <returns>写したとき true</returns>
        public bool TryGetLatestFrame(ushort[] buffer)
        {
            if (buffer == null || buffer.Length != SimulatedWidth * SimulatedHeight)
            {
                return false;
            }

            FillWall(buffer);
            DrawBalls(buffer);
            AgeBalls();
            return true;
        }

        /// <summary>
        /// 指定した行の壁までの深度（mm）を返します。
        /// </summary>
        /// <param name="row">行（上端が 0）</param>
        /// <returns>深度（mm）</returns>
        public float GetWallDepthMm(int row)
        {
            return GetDepthAtWallDistance(row, 0f);
        }

        /// <summary>
        /// 指定した行で、壁から distanceMm だけ手前にある点の深度（mm）を返します。
        /// </summary>
        /// <param name="row">行（上端が 0）</param>
        /// <param name="distanceMm">壁からの距離（mm、手前が正）</param>
        /// <returns>深度（mm）</returns>
        public float GetDepthAtWallDistance(int row, float distanceMm)
        {
            float sensorRow = MountGeometry.IsNearAtImageBottom(mount) ? row : SimulatedHeight - 1 - row;
            float rayY = intrinsics.GetRay(0f, sensorRow + 0.5f).y;
            float normalLength = Mathf.Sqrt(1f + wallTilt * wallTilt);
            return (wallHeightMm - distanceMm * normalLength) / (rayY - wallTilt);
        }

        /// <summary>
        /// 入れたボールをすべて消します。
        /// </summary>
        public void ClearBalls()
        {
            balls.Clear();
        }

        /// <summary>
        /// 何もしません（模擬深度は解放するものがありません）。
        /// </summary>
        public void Dispose()
        {
            balls.Clear();
        }

        private void FillWall(ushort[] buffer)
        {
            for (int y = 0; y < SimulatedHeight; y++)
            {
                float wallDepth = GetWallDepthMm(y);
                for (int x = 0; x < SimulatedWidth; x++)
                {
                    buffer[y * SimulatedWidth + x] = ToDepth(wallDepth + NextNoise());
                }
            }
        }

        private void DrawBalls(ushort[] buffer)
        {
            foreach (SimulatedBall ball in balls)
            {
                DrawBall(buffer, ball);
            }
        }

        private void DrawBall(ushort[] buffer, SimulatedBall ball)
        {
            int radius = Mathf.CeilToInt(ball.RadiusPixels);
            int centerX = Mathf.RoundToInt(ball.Pixel.x);
            int centerY = Mathf.RoundToInt(ball.Pixel.y);
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    DrawBallPixel(buffer, ball, x, y);
                }
            }
        }

        private void DrawBallPixel(ushort[] buffer, SimulatedBall ball, int x, int y)
        {
            if (x < 0 || y < 0 || x >= SimulatedWidth || y >= SimulatedHeight)
            {
                return;
            }

            float dx = x + 0.5f - ball.Pixel.x;
            float dy = y + 0.5f - ball.Pixel.y;
            if (dx * dx + dy * dy > ball.RadiusPixels * ball.RadiusPixels)
            {
                return;
            }

            buffer[y * SimulatedWidth + x] = ToDepth(GetDepthAtWallDistance(y, ball.DistanceFromWallMm));
        }

        private void AgeBalls()
        {
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                balls[i].RemainingFrames--;
                if (balls[i].RemainingFrames <= 0)
                {
                    balls.RemoveAt(i);
                }
            }
        }

        private float NextNoise()
        {
            return AddNoise ? random.Next(-NoiseAmplitudeMm, NoiseAmplitudeMm + 1) : 0f;
        }

        private static ushort ToDepth(float depthMm)
        {
            if (depthMm < MinimumDepthMm || depthMm > ushort.MaxValue)
            {
                return 0;
            }

            return (ushort)Mathf.RoundToInt(depthMm);
        }
    }
}
