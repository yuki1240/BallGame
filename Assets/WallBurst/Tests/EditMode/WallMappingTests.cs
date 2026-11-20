using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WallBurst.Tests
{
    /// <summary>
    /// 深度を使った壁座標の対応付け（<see cref="WallMapping"/>）と、壁グリッド（<see cref="RectifiedDepthGrid"/>）のテストです。
    /// <para>実際の置き方と同じく、Kinect を投影面の横に置いて、壁に沿って見ている場面を作ります。
    /// カメラの右 300mm に壁があり、壁は奥行き 1000〜3000mm（幅 2m）、高さ 1m です。</para>
    /// </summary>
    public sealed class WallMappingTests
    {
        private const int Width = 512;
        private const int Height = 424;
        private const float WallX = 300f;
        private const float NearZ = 1000f;
        private const float FarZ = 3000f;
        private const float HalfHeight = 500f;

        private DepthIntrinsics intrinsics;
        private ushort[] baseline;

        // 壁の四隅（3D）。Kinect が左にあるとき、画面の左上・右上・右下・左下になる順
        private Vector3 nearTop;
        private Vector3 farTop;
        private Vector3 farBottom;
        private Vector3 nearBottom;

        [SetUp]
        public void SetUp()
        {
            intrinsics = DepthIntrinsics.ForResolution(Width, Height);
            nearTop = new Vector3(WallX, -HalfHeight, NearZ);
            farTop = new Vector3(WallX, -HalfHeight, FarZ);
            farBottom = new Vector3(WallX, HalfHeight, FarZ);
            nearBottom = new Vector3(WallX, HalfHeight, NearZ);
            baseline = new ushort[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector3 ray = intrinsics.Ray(x, y);
                    if (ray.x <= 0f)
                    {
                        continue;
                    }

                    // 視線が壁（x = 300）と交わる所の深度
                    float depth = WallX / ray.x;
                    baseline[y * Width + x] = depth < 8000f ? (ushort)Mathf.RoundToInt(depth) : (ushort)0;
                }
            }
        }

        [Test]
        public void CornersMapToWallCorners()
        {
            WallMapping mapping = Create(false, nearTop, farTop, farBottom, nearBottom);
            AssertNear(mapping.PointToWall(nearTop), new Vector2(0f, 1f));
            AssertNear(mapping.PointToWall(farTop), new Vector2(1f, 1f));
            AssertNear(mapping.PointToWall(farBottom), new Vector2(1f, 0f));
            AssertNear(mapping.PointToWall(nearBottom), new Vector2(0f, 0f));
            Assert.That(mapping.NearSideDetected, Is.True);
            Assert.That(mapping.WidthMm, Is.EqualTo(2000f).Within(30f));
            Assert.That(mapping.HeightMm, Is.EqualTo(1000f).Within(30f));
        }

        [Test]
        public void SameLengthOnWallIsSameLengthOnScreen_NearAndFar()
        {
            WallMapping mapping = Create(false, nearTop, farTop, farBottom, nearBottom);

            // 壁の上の 20cm を、Kinect に近い所と遠い所で比べる
            float near = mapping.PointToWall(new Vector3(WallX, 0f, 1400f)).x - mapping.PointToWall(new Vector3(WallX, 0f, 1200f)).x;
            float far = mapping.PointToWall(new Vector3(WallX, 0f, 2800f)).x - mapping.PointToWall(new Vector3(WallX, 0f, 2600f)).x;
            Assert.That(near, Is.EqualTo(0.1f).Within(0.005f));
            Assert.That(far, Is.EqualTo(0.1f).Within(0.005f));
        }

        [Test]
        public void KinectOnRight_SwapsLeftAndRight_WithoutRetapping()
        {
            WallMapping mapping = Create(true, nearTop, farTop, farBottom, nearBottom);
            AssertNear(mapping.PointToWall(nearTop), new Vector2(1f, 1f));
            AssertNear(mapping.PointToWall(farBottom), new Vector2(0f, 0f));
        }

        [Test]
        public void TappingLeftAndRightReversed_IsCorrectedByDepth()
        {
            // 左右を逆にタップしても、Kinect に近い側が左（設定どおり）になる
            WallMapping mapping = Create(false, farTop, nearTop, nearBottom, farBottom);
            AssertNear(mapping.PointToWall(nearTop), new Vector2(0f, 1f));
            AssertNear(mapping.PointToWall(farBottom), new Vector2(1f, 0f));
        }

        [Test]
        public void WallToPointAndBack_RoundTrips()
        {
            WallMapping mapping = Create(false, nearTop, farTop, farBottom, nearBottom);
            Vector2 wall = new Vector2(0.3f, 0.7f);
            Vector3 point = mapping.WallToPoint(wall);
            Assert.That(mapping.Plane.DistanceTo(point), Is.EqualTo(0f).Within(1f));
            AssertNear(mapping.PointToWall(point), wall);
            AssertNear(mapping.ImageToWall(mapping.WallToImage(wall)), wall);
        }

        [Test]
        public void Grid_IsOneCentimeterPerCell_AndFarSideIsFilled()
        {
            CalibrationData data = CreateData(false, nearTop, farTop, farBottom, nearBottom);
            bool[] inside = QuadMask.Build(data.Corners, Width, Height);
            string error;
            WallMapping mapping = WallMapping.Create(data, inside, out error);
            Assert.That(mapping, Is.Not.Null, error);

            RectifiedDepthGrid grid = new RectifiedDepthGrid(mapping, inside, Width, Height);
            Assert.That(grid.Width, Is.EqualTo(200).Within(3));
            Assert.That(grid.Height, Is.EqualTo(100).Within(3));

            grid.Build(baseline);
            int far = 0;
            int farFilled = 0;
            for (int y = 5; y < grid.Height - 5; y++)
            {
                for (int x = grid.Width * 3 / 4; x < grid.Width - 5; x++)
                {
                    far++;
                    farFilled += grid.HasValue(x, y) ? 1 : 0;
                    if (grid.HasValue(x, y))
                    {
                        Assert.That(Mathf.Abs(grid.GetHeightMm(x, y)), Is.LessThan(15f), "壁だけのときは、壁からの距離はほぼ 0");
                    }
                }
            }

            Assert.That(farFilled / (float)far, Is.GreaterThan(0.95f), "Kinect から遠い側も穴が埋まる");
        }

        [Test]
        public void Grid_PlacesObjectAtItsWallPosition_AndSkipsZeroDepth()
        {
            CalibrationData data = CreateData(false, nearTop, farTop, farBottom, nearBottom);
            bool[] inside = QuadMask.Build(data.Corners, Width, Height);
            string error;
            WallMapping mapping = WallMapping.Create(data, inside, out error);
            RectifiedDepthGrid grid = new RectifiedDepthGrid(mapping, inside, Width, Height);

            // 壁の上の (0.6, 0.5) の所に、壁から 50mm 手前へ出た物を置く
            Vector3 onWall = mapping.WallToPoint(new Vector2(0.6f, 0.5f));
            Vector3 front = onWall + mapping.Plane.Normal * 50f;
            Vector2 pixel = intrinsics.Project(front);
            ushort[] frame = (ushort[])baseline.Clone();
            int px = Mathf.RoundToInt(pixel.x);
            int py = Mathf.RoundToInt(pixel.y);
            frame[py * Width + px] = (ushort)Mathf.RoundToInt(front.z);

            // 深度が取れない画素（0）は置かない
            Vector2 other = intrinsics.Project(mapping.WallToPoint(new Vector2(0.2f, 0.5f)));
            frame[Mathf.RoundToInt(other.y) * Width + Mathf.RoundToInt(other.x)] = 0;

            grid.Build(frame);
            float best = 0f;
            int bestX = -1;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.GetHeightMm(x, y) > best)
                    {
                        best = grid.GetHeightMm(x, y);
                        bestX = x;
                    }
                }
            }

            Assert.That(best, Is.EqualTo(50f).Within(15f));
            Assert.That(bestX / (float)grid.Width, Is.EqualTo(0.6f).Within(0.03f));
        }

        [Test]
        public void CalibrationStore_KeepsKinectSide_AndReadsVersion1()
        {
            CalibrationData data = CreateData(true, nearTop, farTop, farBottom, nearBottom);
            using (MemoryStream stream = new MemoryStream())
            {
                CalibrationStore.Write(data, stream);
                stream.Position = 0;
                Assert.That(CalibrationStore.Read(stream).KinectOnRight, Is.True);
            }

            // Version 1（Kinect を置いた側がない）のファイル：最後の1バイトを除き、版の番号を 1 にしたもの
            using (MemoryStream stream = new MemoryStream())
            {
                CalibrationStore.Write(data, stream);
                byte[] bytes = stream.ToArray();
                byte[] old = new byte[bytes.Length - 1];
                System.Array.Copy(bytes, old, old.Length);
                old[4] = 1;
                CalibrationData read = CalibrationStore.Read(new MemoryStream(old));
                Assert.That(read, Is.Not.Null);
                Assert.That(read.KinectOnRight, Is.False);
                Assert.That(read.IsComplete, Is.True);
            }
        }

        private WallMapping Create(bool kinectOnRight, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, Vector3 bottomLeft)
        {
            CalibrationData data = CreateData(kinectOnRight, topLeft, topRight, bottomRight, bottomLeft);
            string error;
            WallMapping mapping = WallMapping.Create(data, QuadMask.Build(data.Corners, Width, Height), out error);
            Assert.That(mapping, Is.Not.Null, error);
            return mapping;
        }

        private CalibrationData CreateData(bool kinectOnRight, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, Vector3 bottomLeft)
        {
            CalibrationData data = new CalibrationData();
            data.Width = Width;
            data.Height = Height;
            data.Baseline = baseline;
            data.Noise = new ushort[Width * Height];
            data.Corners = new[] { ToImage(topLeft), ToImage(topRight), ToImage(bottomRight), ToImage(bottomLeft) };
            data.KinectOnRight = kinectOnRight;
            return data;
        }

        // 3Dの位置を、深度画像の位置（0〜1、画素の中心が (i + 0.5) / 幅）にする
        private Vector2 ToImage(Vector3 point)
        {
            Vector2 pixel = intrinsics.Project(point);
            return new Vector2((pixel.x + 0.5f) / Width, (pixel.y + 0.5f) / Height);
        }

        private static void AssertNear(Vector2 actual, Vector2 expected)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.01f), "actual " + actual + " expected " + expected);
        }
    }
}
