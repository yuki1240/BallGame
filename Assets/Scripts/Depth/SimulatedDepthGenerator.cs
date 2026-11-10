using System;
using System.Collections.Generic;
using UnityEngine;

namespace WallBurst.Depth
{
    /// <summary>
    /// 壁とほぼ平行に置いた Kinect の深度を模す。
    /// 左が近く右が遠い、という軽い傾き、毎フレームの揺れ、穴、孤立した飛び画素、
    /// そして壁より手前に出るボールを生成する。乱数の種はフレーム番号なので結果は再現できる。
    /// </summary>
    public sealed class SimulatedDepthGenerator
    {
        private readonly List<Impact> _impacts = new List<Impact>();

        /// <summary>
        /// サイズを決める。
        /// </summary>
        /// <param name="width">幅。</param>
        /// <param name="height">高さ。</param>
        public SimulatedDepthGenerator(int width, int height)
        {
            if (width <= 1 || height <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "シミュレーション解像度が小さすぎます。");
            }

            Width = width;
            Height = height;
        }

        /// <summary>幅。</summary>
        public int Width { get; private set; }

        /// <summary>高さ。</summary>
        public int Height { get; private set; }

        /// <summary>画像左端の壁までの距離（mm）。センサーに近い側。</summary>
        public int NearSideMillimeters = 1700;

        /// <summary>画像右端の壁までの距離（mm）。少し開いた側。</summary>
        public int FarSideMillimeters = 2450;

        /// <summary>壁画素に足す揺れの振幅（mm）。</summary>
        public int NoiseAmplitudeMillimeters = 18;

        /// <summary>孤立した飛び画素を混ぜるか。</summary>
        public bool EnableFlyingPixels = true;

        /// <summary>ところどころ深度 0 の穴を混ぜるか。</summary>
        public bool EnableHoles = true;

        /// <summary>
        /// 1フレーム分を書き、注入したボールの寿命を1進める。
        /// </summary>
        /// <param name="destination">出力。</param>
        /// <param name="frameIndex">ノイズの種。</param>
        public void Fill(ushort[] destination, int frameIndex)
        {
            if (destination == null || destination.Length != Width * Height)
            {
                throw new ArgumentException("出力バッファのサイズが一致しません。");
            }

            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    destination[row + x] = SampleWall(x, y, frameIndex);
                }
            }

            ApplyImpacts(destination);
            DecayImpacts();
        }

        /// <summary>
        /// 壁より手前の円い塊を数フレーム出す。クリックや試験投入用。
        /// </summary>
        /// <param name="pixelX">中心 X。</param>
        /// <param name="pixelY">中心 Y。</param>
        /// <param name="radiusPixels">半径。</param>
        /// <param name="closerMillimeters">中心を壁よりどれだけ手前にするか。</param>
        /// <param name="lifeFrames">残すフレーム数。</param>
        public void Inject(int pixelX, int pixelY, int radiusPixels, int closerMillimeters, int lifeFrames)
        {
            if (lifeFrames <= 0 || radiusPixels <= 0 || closerMillimeters <= 0)
            {
                return;
            }

            Impact impact = new Impact();
            impact.X = pixelX;
            impact.Y = pixelY;
            impact.Radius = radiusPixels;
            impact.CloserMillimeters = closerMillimeters;
            impact.FramesRemaining = lifeFrames;
            _impacts.Add(impact);
        }

        /// <summary>
        /// 注入したボールを消す。
        /// </summary>
        public void ClearImpacts()
        {
            _impacts.Clear();
        }

        /// <summary>
        /// 傾いた壁に、揺れと穴と飛び画素を足した1画素。
        /// </summary>
        private ushort SampleWall(int x, int y, int frameIndex)
        {
            float across = x / (float)(Width - 1);
            float down = y / (float)(Height - 1);
            float wall = Mathf.Lerp(NearSideMillimeters, FarSideMillimeters, across);
            wall += Mathf.Lerp(-40f, 55f, down);

            uint hash = Hash(x, y, frameIndex);
            float unit = (hash & 1023u) / 1023f;
            float noise = ((unit - 0.5f) * 2f) * NoiseAmplitudeMillimeters;
            int depth = Mathf.RoundToInt(wall + noise);

            if (EnableHoles && (hash % 251u) == 0u)
            {
                return 0;
            }

            if (EnableFlyingPixels && (hash % 4000u) == 1u)
            {
                depth -= 800;
            }

            if (depth < 1)
            {
                depth = 1;
            }

            if (depth > 8000)
            {
                depth = 8000;
            }

            return (ushort)depth;
        }

        /// <summary>
        /// ボールを壁より手前の深度で上書きする。
        /// </summary>
        private void ApplyImpacts(ushort[] destination)
        {
            for (int i = 0; i < _impacts.Count; i++)
            {
                PaintImpact(destination, _impacts[i]);
            }
        }

        /// <summary>
        /// 円の中心ほど壁より近くする。
        /// </summary>
        private void PaintImpact(ushort[] destination, Impact impact)
        {
            int radius = impact.Radius;
            int radiusSquared = radius * radius;
            int minX = Math.Max(0, impact.X - radius);
            int maxX = Math.Min(Width - 1, impact.X + radius);
            int minY = Math.Max(0, impact.Y - radius);
            int maxY = Math.Min(Height - 1, impact.Y + radius);

            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - impact.Y;
                int row = y * Width;
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - impact.X;
                    int distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared > radiusSquared)
                    {
                        continue;
                    }

                    float weight = 1f - (Mathf.Sqrt(distanceSquared) / radius);
                    int closer = Mathf.RoundToInt(impact.CloserMillimeters * weight);
                    int current = destination[row + x];
                    if (current == 0)
                    {
                        current = NearSideMillimeters;
                    }

                    int next = current - closer;
                    if (next < 1)
                    {
                        next = 1;
                    }

                    destination[row + x] = (ushort)next;
                }
            }
        }

        /// <summary>
        /// 寿命が尽きた注入を捨てる。
        /// </summary>
        private void DecayImpacts()
        {
            for (int i = _impacts.Count - 1; i >= 0; i--)
            {
                Impact impact = _impacts[i];
                impact.FramesRemaining--;
                if (impact.FramesRemaining <= 0)
                {
                    _impacts.RemoveAt(i);
                    continue;
                }

                _impacts[i] = impact;
            }
        }

        /// <summary>
        /// 画素とフレームから決まるハッシュ。
        /// </summary>
        private static uint Hash(int x, int y, int frameIndex)
        {
            uint hash = (uint)((x * 374761393) + (y * 668265263) + (frameIndex * 1442695041));
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            return hash ^ (hash >> 16);
        }

        private struct Impact
        {
            public int X;
            public int Y;
            public int Radius;
            public int CloserMillimeters;
            public int FramesRemaining;
        }
    }
}
