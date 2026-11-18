using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// Kinect が無い PC で確かめるための、模擬の深度です（512×424、1秒に30回）。
    /// <list type="bullet">
    /// <item>壁を斜めから見たような深度を作ります（画像の下が近く、上ほど遠い。遠すぎる所は 0）。</item>
    /// <item><see cref="ThrowBall"/> で、ボールが飛んできて壁で跳ね返る様子を深度に描きます。</item>
    /// <item><see cref="SetHand"/> で、壁を触る手と腕（体につながった物）を深度に描きます（見分けの確認用）。</item>
    /// </list>
    /// </summary>
    public sealed class SimulatedDepthSource : IDepthSource
    {
        /// <summary>幅。</summary>
        public const int SimWidth = 512;

        /// <summary>高さ。</summary>
        public const int SimHeight = 424;

        private const int MinDepthMm = 500;
        private const int MaxDepthMm = 4500;
        private const float WallOffsetMm = 500f;
        private const int ApproachFrames = 5;
        private const int LeaveFrames = 5;

        private sealed class Ball
        {
            public Vector3 Contact;
            public Vector3 Away;
            public float Radius;
            public int Frame;
        }

        private readonly ushort[] wall;
        private readonly DepthIntrinsics intrinsics;
        private readonly Vector3 wallNormal;
        private readonly List<Ball> balls = new List<Ball>();
        private readonly System.Random random = new System.Random(1);
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long lastFrameTicks = long.MinValue;
        private bool hasHand;
        private Vector3 handTip;
        private bool disposed;

        /// <summary>
        /// 模擬の壁を作ります。
        /// </summary>
        public SimulatedDepthSource()
        {
            intrinsics = DepthIntrinsics.ForResolution(SimWidth, SimHeight);
            wallNormal = new Vector3(0f, 1f, 0.35f).normalized;
            wall = new ushort[SimWidth * SimHeight];
            for (int y = 0; y < SimHeight; y++)
            {
                for (int x = 0; x < SimWidth; x++)
                {
                    wall[y * SimWidth + x] = WallDepth(x, y);
                }
            }
        }

        /// <summary>幅。</summary>
        public int Width
        {
            get { return SimWidth; }
        }

        /// <summary>高さ。</summary>
        public int Height
        {
            get { return SimHeight; }
        }

        /// <summary>入力源の種類。</summary>
        public DepthSourceKind Kind
        {
            get { return DepthSourceKind.Simulated; }
        }

        /// <summary>いつでもフレームを出せます。</summary>
        public bool IsAvailable
        {
            get { return !disposed; }
        }

        /// <summary>壁から手前（カメラ側）への向き。</summary>
        public Vector3 TowardCamera
        {
            get { return -wallNormal; }
        }

        /// <summary>
        /// 1秒に30回、新しいフレームを作ります。
        /// </summary>
        /// <param name="buffer">写し先</param>
        /// <returns>新しいフレームを作ったとき true</returns>
        public bool TryGetLatestFrame(ushort[] buffer)
        {
            long now = clock.ElapsedTicks;
            if (lastFrameTicks != long.MinValue && now - lastFrameTicks < Stopwatch.Frequency / 30)
            {
                return false;
            }

            lastFrameTicks = now;
            RenderFrame(buffer);
            return true;
        }

        /// <summary>
        /// 時間に関係なく、1フレーム進めて描きます（テスト用）。
        /// </summary>
        /// <param name="buffer">写し先（幅×高さ）</param>
        public void RenderFrame(ushort[] buffer)
        {
            if (buffer == null || buffer.Length != wall.Length)
            {
                return;
            }

            for (int i = 0; i < wall.Length; i++)
            {
                ushort d = wall[i];
                buffer[i] = d == 0 ? (ushort)0 : (ushort)(d + random.Next(-2, 3));
            }

            if (hasHand)
            {
                DrawHand(buffer);
            }

            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Ball ball = balls[i];
                DrawSphere(buffer, BallCenter(ball), ball.Radius);
                ball.Frame++;
                if (ball.Frame > ApproachFrames + LeaveFrames)
                {
                    balls.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 壁の上の位置（3D、mm）に向かってボールを投げます。
        /// </summary>
        /// <param name="wallPoint">壁に当たる位置</param>
        /// <param name="diameterMm">ボールの直径</param>
        public void ThrowBall(Vector3 wallPoint, float diameterMm)
        {
            float radius = diameterMm * 0.5f;
            Vector3 toward = TowardCamera;
            Ball ball = new Ball();
            ball.Radius = radius;
            ball.Contact = wallPoint + toward * radius;
            ball.Away = ball.Contact + toward * 900f + new Vector3(0f, 0f, -200f);
            balls.Add(ball);
        }

        /// <summary>
        /// 壁を触る手（腕は手前の体へつながっている）を出したり消したりします。
        /// </summary>
        /// <param name="visible">出すとき true</param>
        /// <param name="wallPoint">手で触る壁の位置</param>
        public void SetHand(bool visible, Vector3 wallPoint)
        {
            hasHand = visible;
            handTip = wallPoint + TowardCamera * 40f;
        }

        /// <summary>
        /// 閉じます。
        /// </summary>
        public void Dispose()
        {
            disposed = true;
        }

        private ushort WallDepth(int x, int y)
        {
            Vector3 ray = intrinsics.Ray(x, y);
            float denominator = Vector3.Dot(wallNormal, ray);
            if (denominator <= 1e-4f)
            {
                return 0;
            }

            float depth = WallOffsetMm / denominator;
            return depth < MinDepthMm || depth > MaxDepthMm ? (ushort)0 : (ushort)depth;
        }

        private Vector3 BallCenter(Ball ball)
        {
            if (ball.Frame <= ApproachFrames)
            {
                float t = ball.Frame / (float)ApproachFrames;
                return Vector3.Lerp(ball.Away, ball.Contact, t);
            }

            float leave = (ball.Frame - ApproachFrames) / (float)LeaveFrames;
            Vector3 back = ball.Contact + TowardCamera * 700f * leave + new Vector3(0f, 300f * leave, 0f);
            return back;
        }

        private void DrawHand(ushort[] buffer)
        {
            // 手から体の方へ、太さ 9cm の腕を 1m 描く（腕の先が手）
            Vector3 toward = TowardCamera;
            for (int step = 0; step <= 40; step++)
            {
                Vector3 center = handTip + toward * (step * 25f);
                DrawSphere(buffer, center, 45f);
            }
        }

        private void DrawSphere(ushort[] buffer, Vector3 center, float radius)
        {
            if (center.z <= radius + 10f)
            {
                return;
            }

            Vector2 pixel = intrinsics.Project(center);
            float pixelRadius = radius / center.z * intrinsics.FocalX + 2f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(pixel.x - pixelRadius));
            int maxX = Mathf.Min(SimWidth - 1, Mathf.CeilToInt(pixel.x + pixelRadius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(pixel.y - pixelRadius));
            int maxY = Mathf.Min(SimHeight - 1, Mathf.CeilToInt(pixel.y + pixelRadius));
            float cc = Vector3.Dot(center, center) - radius * radius;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 ray = intrinsics.Ray(x, y);
                    float a = Vector3.Dot(ray, ray);
                    float b = -2f * Vector3.Dot(ray, center);
                    float disc = b * b - 4f * a * cc;
                    if (disc < 0f)
                    {
                        continue;
                    }

                    float depth = (-b - (float)Math.Sqrt(disc)) / (2f * a);
                    if (depth < MinDepthMm || depth > MaxDepthMm)
                    {
                        continue;
                    }

                    int i = y * SimWidth + x;
                    if (buffer[i] == 0 || depth < buffer[i])
                    {
                        buffer[i] = (ushort)depth;
                    }
                }
            }
        }
    }
}
