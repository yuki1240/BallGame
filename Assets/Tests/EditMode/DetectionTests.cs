using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WallBurst.Depth;
using WallBurst.Detection;

namespace WallBurst.Tests
{
    /// <summary>
    /// 基準、四隅、ノイズ抑制、ヒット判定の編集モードテスト。
    /// </summary>
    public sealed class DetectionTests
    {
        /// <summary>
        /// 穴と外れ値を除いた中央値になる。
        /// </summary>
        [Test]
        public void CreateMedian_IgnoresZerosAndSpikes()
        {
            BaselineAccumulator accumulator = new BaselineAccumulator(1, 1, 5);
            ushort[] frame = new ushort[1];
            ushort[] samples = { 100, 0, 300, 110, 90 };
            for (int i = 0; i < samples.Length; i++)
            {
                frame[0] = samples[i];
                accumulator.Add(frame);
            }

            ushort[] median = accumulator.CreateMedian();
            Assert.AreEqual(110, median[0]);
        }

        /// <summary>
        /// 全部が穴の画素は無効のまま。
        /// </summary>
        [Test]
        public void CreateMedian_MarksPixelInvalidWhenMostSamplesAreHoles()
        {
            BaselineAccumulator accumulator = new BaselineAccumulator(1, 1, 4);
            ushort[] frame = new ushort[1];
            for (int i = 0; i < 4; i++)
            {
                frame[0] = 0;
                accumulator.Add(frame);
            }

            Assert.AreEqual(0, accumulator.CreateMedian()[0]);
        }

        /// <summary>
        /// 空のフレームは測定値なし。
        /// </summary>
        [Test]
        public void ContainsMeasurement_RejectsEmptyDepth()
        {
            Assert.IsFalse(BaselineAccumulator.ContainsMeasurement(new ushort[8]));
            ushort[] depth = new ushort[8];
            depth[0] = 1200;
            Assert.IsTrue(BaselineAccumulator.ContainsMeasurement(depth));
        }

        /// <summary>
        /// 傾いた四隅でも、往復変換が元の座標に戻る。
        /// </summary>
        [Test]
        public void QuadMapper_RoundTripsSkewedQuad()
        {
            Vector2 bottomLeft = new Vector2(30f, 210f);
            Vector2 bottomRight = new Vector2(300f, 200f);
            Vector2 topRight = new Vector2(290f, 25f);
            Vector2 topLeft = new Vector2(20f, 18f);
            float[] us = { 0f, 1f, 0f, 1f, 0.3f, 0.5f, 0.1f };
            float[] vs = { 0f, 0f, 1f, 1f, 0.7f, 0.5f, 0.9f };
            for (int i = 0; i < us.Length; i++)
            {
                Vector2 pixel = QuadMapper.WallToPixel(us[i], vs[i], topLeft, topRight, bottomRight, bottomLeft);
                float u;
                float v;
                bool mapped = QuadMapper.TryPixelToWall(pixel, topLeft, topRight, bottomRight, bottomLeft, out u, out v);
                Assert.IsTrue(mapped);
                Assert.AreEqual(us[i], u, 0.001f);
                Assert.AreEqual(vs[i], v, 0.001f);
                Assert.IsTrue(QuadMapper.Contains(pixel, topLeft, topRight, bottomRight, bottomLeft));
            }
        }

        /// <summary>
        /// 四隅の外は壁座標にしない。
        /// </summary>
        [Test]
        public void TryPixelToWall_RejectsOutsidePoint()
        {
            float u;
            float v;
            bool mapped = QuadMapper.TryPixelToWall(
                new Vector2(-40f, -40f),
                new Vector2(0f, 0f),
                new Vector2(100f, 0f),
                new Vector2(100f, 80f),
                new Vector2(0f, 80f),
                out u,
                out v);
            Assert.IsFalse(mapped);
        }

        /// <summary>
        /// 斜めに接するだけの画素は、4近傍では別の塊。
        /// </summary>
        [Test]
        public void Extract_SplitsDiagonalTouchAndKeepsLargest()
        {
            BlobExtractor extractor = new BlobExtractor(6, 6);
            byte[] mask = new byte[36];
            FillRect(mask, 6, 0, 0, 2, 2);
            FillRect(mask, 6, 3, 3, 2, 2);
            mask[(2 * 6) + 2] = 1;
            List<Blob> blobs = new List<Blob>();
            int count = extractor.Extract(mask, null, 1, blobs, 8);
            Assert.AreEqual(3, count);
            Assert.AreEqual(4, blobs[0].Area);
            Assert.AreEqual(4, blobs[1].Area);
            Assert.AreEqual(1, blobs[2].Area);
        }

        /// <summary>
        /// 大きい塊は1フレームでヒットし、同じ場所は冷却中に出さない。遠い場所は出す。
        /// </summary>
        [Test]
        public void Detect_EmitsStrongBlobOnceUntilCooldownExpires()
        {
            WallHitDetector detector = CreateDetector(40, 30, 2000);
            ushort[] depth = Filled(40, 30, 2000);
            PaintDisc(depth, 40, 8, 15, 5, 1850);
            List<WallHit> hits = new List<WallHit>();

            Assert.AreEqual(1, detector.Detect(depth, 0f, hits));
            Assert.AreEqual(0, detector.Detect(depth, 0.1f, hits));

            ushort[] other = Filled(40, 30, 2000);
            PaintDisc(other, 40, 32, 15, 5, 1850);
            Assert.AreEqual(1, detector.Detect(other, 0.1f, hits));
            Assert.Greater(hits[0].WallU, 0.6f);
        }

        /// <summary>
        /// 孤立点、微小な揺れ、壁から遠い人はヒットにしない。
        /// </summary>
        [Test]
        public void Detect_RejectsNoiseSpecksAndDistantPeople()
        {
            WallHitDetector detector = CreateDetector(40, 30, 2000);
            ushort[] depth = Filled(40, 30, 2000);
            depth[(5 * 40) + 5] = 1850;
            depth[(8 * 40) + 20] = 1850;
            depth[(20 * 40) + 10] = 1990;
            PaintDisc(depth, 40, 28, 22, 6, 1000);
            List<WallHit> hits = new List<WallHit>();
            Assert.AreEqual(0, detector.Detect(depth, 0f, hits));
        }

        /// <summary>
        /// 小さい塊は指定フレーム続いてから一度だけ出し、留まり続けると止める。
        /// </summary>
        [Test]
        public void Detect_ConfirmsSmallBlobThenStopsWhileItStays()
        {
            CalibrationData data = CreateData(32, 24, 2000);
            data.Tuning.MinBlobArea = 4;
            data.Tuning.StrongBlobMultiplier = 8;
            data.Tuning.ConfirmFrames = 2;
            data.Tuning.CooldownSeconds = 0.2f;
            WallHitDetector detector = new WallHitDetector(data);
            ushort[] depth = Filled(32, 24, 2000);
            PaintRect(depth, 32, 12, 10, 2, 2, 1850);
            List<WallHit> hits = new List<WallHit>();

            Assert.AreEqual(0, detector.Detect(depth, 0f, hits));
            Assert.AreEqual(1, detector.Detect(depth, 0.05f, hits));
            Assert.AreEqual(0, detector.Detect(depth, 0.1f, hits));
            for (int i = 0; i < 8; i++)
            {
                detector.Detect(depth, 10f + i, hits);
            }

            Assert.AreEqual(0, detector.Detect(depth, 30f, hits));
        }

        /// <summary>
        /// 揺れの内側だけ基準を寄せ、大きく近い画素は動かさない。
        /// </summary>
        [Test]
        public void Detect_DriftsOnlyStablePixels()
        {
            CalibrationData data = CreateData(8, 8, 2000);
            data.Tuning.EnableDriftCompensation = true;
            data.Tuning.DriftIntervalFrames = 1;
            data.Tuning.DriftBlend = 0.5f;
            data.Tuning.NoiseFloorMillimeters = 40;
            WallHitDetector detector = new WallHitDetector(data);
            ushort[] depth = Filled(8, 8, 2010);
            depth[0] = 1000;
            detector.Detect(depth, 0f, new List<WallHit>());

            Assert.AreEqual(2005, data.Baseline[20]);
            Assert.AreEqual(2000, data.Baseline[0]);
        }

        /// <summary>
        /// 模擬 Kinect の穴と飛び画素と揺れだけではヒットせず、ボールでヒットする。
        /// </summary>
        [Test]
        public void SimulatedWall_SuppressesNoiseUntilBallIsInjected()
        {
            const int width = 80;
            const int height = 60;
            SimulatedDepthGenerator generator = new SimulatedDepthGenerator(width, height);
            generator.NoiseAmplitudeMillimeters = 18;
            ushort[] frame = new ushort[width * height];
            BaselineAccumulator accumulator = new BaselineAccumulator(width, height, 12);
            for (int i = 0; i < 12; i++)
            {
                generator.Fill(frame, i);
                accumulator.Add(frame);
            }

            CalibrationData data = new CalibrationData(width, height, accumulator.CreateMedian(), new DetectionTuning());
            data.Tuning.NoiseFloorMillimeters = 40;
            data.Tuning.MaxApproachMillimeters = 400;
            data.Tuning.MinBlobArea = 20;
            data.SetCorner(0, new Vector2(2f, 2f));
            data.SetCorner(1, new Vector2(width - 3f, 2f));
            data.SetCorner(2, new Vector2(width - 3f, height - 3f));
            data.SetCorner(3, new Vector2(2f, height - 3f));
            WallHitDetector detector = new WallHitDetector(data);
            List<WallHit> hits = new List<WallHit>();

            for (int i = 0; i < 20; i++)
            {
                generator.Fill(frame, 100 + i);
                Assert.AreEqual(0, detector.Detect(frame, i * 0.05f, hits));
            }

            generator.Inject(width / 2, height / 2, 7, 160, 3);
            generator.Fill(frame, 200);
            Assert.AreEqual(1, detector.Detect(frame, 5f, hits));
            Assert.That(hits[0].WallU, Is.InRange(0.35f, 0.65f));
            Assert.That(hits[0].WallV, Is.InRange(0.35f, 0.65f));
        }

        /// <summary>
        /// 壁があった画素がボール大の穴になったら、赤外線を返さないボールとして1回出す。
        /// </summary>
        [Test]
        public void Detect_AcceptsHoleAboutTheSizeOfABall()
        {
            WallHitDetector detector = CreateDetector(40, 30, 2000);
            ushort[] depth = Filled(40, 30, 2000);
            PaintRect(depth, 40, 16, 11, 8, 8, 0);
            List<WallHit> hits = new List<WallHit>();

            Assert.AreEqual(1, detector.Detect(depth, 0f, hits));
            Assert.That(hits[0].WallU, Is.InRange(0.3f, 0.7f));
        }

        /// <summary>
        /// 人の体で隠れた大きな穴はヒットにしない。
        /// </summary>
        [Test]
        public void Detect_RejectsPersonSizedHole()
        {
            CalibrationData data = CreateData(80, 60, 2000);
            data.Tuning.MaxBlobArea = 400;
            WallHitDetector detector = new WallHitDetector(data);
            ushort[] depth = Filled(80, 60, 2000);
            PaintRect(depth, 80, 10, 10, 40, 30, 0);
            List<WallHit> hits = new List<WallHit>();

            Assert.AreEqual(0, detector.Detect(depth, 0f, hits));
        }

        /// <summary>
        /// 四隅が揃うまで範囲は空。
        /// </summary>
        [Test]
        public void RebuildRoi_IgnoresHitsUntilCornersExist()
        {
            CalibrationData data = new CalibrationData(16, 12, Filled(16, 12, 2000), new DetectionTuning());
            WallHitDetector detector = new WallHitDetector(data);
            ushort[] depth = Filled(16, 12, 1800);
            List<WallHit> hits = new List<WallHit>();
            Assert.AreEqual(0, detector.Detect(depth, 0f, hits));

            data.SetCorner(0, new Vector2(1f, 1f));
            data.SetCorner(1, new Vector2(14f, 1f));
            data.SetCorner(2, new Vector2(14f, 10f));
            data.SetCorner(3, new Vector2(1f, 10f));
            detector.RebuildRoi();
            Assert.Greater(detector.Detect(depth, 1f, hits), 0);
        }

        /// <summary>
        /// 矩形をマスクに塗る。
        /// </summary>
        private static void FillRect(byte[] mask, int width, int x, int y, int rectWidth, int rectHeight)
        {
            for (int row = y; row < y + rectHeight; row++)
            {
                for (int column = x; column < x + rectWidth; column++)
                {
                    mask[(row * width) + column] = 1;
                }
            }
        }

        /// <summary>
        /// 四隅つきの平坦な壁を作る。
        /// </summary>
        private static WallHitDetector CreateDetector(int width, int height, ushort baseline)
        {
            return new WallHitDetector(CreateData(width, height, baseline));
        }

        /// <summary>
        /// ドリフト無しの基準データ。
        /// </summary>
        private static CalibrationData CreateData(int width, int height, ushort baseline)
        {
            CalibrationData data = new CalibrationData(width, height, Filled(width, height, baseline), new DetectionTuning());
            data.Tuning.EnableDriftCompensation = false;
            data.Tuning.MinBlobArea = 10;
            data.Tuning.NoiseFloorMillimeters = 40;
            data.Tuning.MaxApproachMillimeters = 380;
            data.SetCorner(0, new Vector2(1f, 1f));
            data.SetCorner(1, new Vector2(width - 2f, 1f));
            data.SetCorner(2, new Vector2(width - 2f, height - 2f));
            data.SetCorner(3, new Vector2(1f, height - 2f));
            return data;
        }

        /// <summary>
        /// 同じ深度で埋める。
        /// </summary>
        private static ushort[] Filled(int width, int height, ushort value)
        {
            ushort[] depth = new ushort[width * height];
            for (int i = 0; i < depth.Length; i++)
            {
                depth[i] = value;
            }

            return depth;
        }

        /// <summary>
        /// 円を一定深度で塗る。
        /// </summary>
        private static void PaintDisc(ushort[] depth, int width, int centerX, int centerY, int radius, ushort value)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if ((dx * dx) + (dy * dy) > radiusSquared)
                    {
                        continue;
                    }

                    depth[(y * width) + x] = value;
                }
            }
        }

        /// <summary>
        /// 矩形を一定深度で塗る。
        /// </summary>
        private static void PaintRect(ushort[] depth, int width, int x, int y, int rectWidth, int rectHeight, ushort value)
        {
            for (int row = y; row < y + rectHeight; row++)
            {
                for (int column = x; column < x + rectWidth; column++)
                {
                    depth[(row * width) + column] = value;
                }
            }
        }
    }
}
