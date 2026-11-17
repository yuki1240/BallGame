using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// 設置の上下関係のテスト。
    /// </summary>
    public sealed class MountOrientationTests
    {
        [Test]
        public void 横設置は画像の下が近く上ほど遠い()
        {
            SimulatedDepthSource simulator = new SimulatedDepthSource(MountOrientation.Side) { AddNoise = false };

            Assert.That(MountGeometry.IsNearAtImageBottom(MountOrientation.Side), Is.True);
            Assert.That(simulator.GetWallDepthMm(SimulatedDepthSource.SimulatedHeight - 1), Is.LessThan(simulator.GetWallDepthMm(0)));
            Assert.That(simulator.GetWallDepthMm(100), Is.LessThan(simulator.GetWallDepthMm(50)));
        }

        [Test]
        public void 真下設置は画像の上が近く下ほど遠い()
        {
            SimulatedDepthSource simulator = new SimulatedDepthSource(MountOrientation.Below) { AddNoise = false };

            Assert.That(MountGeometry.IsNearAtImageBottom(MountOrientation.Below), Is.False);
            Assert.That(simulator.GetWallDepthMm(0), Is.LessThan(simulator.GetWallDepthMm(SimulatedDepthSource.SimulatedHeight - 1)));
        }

        [Test]
        public void 模擬の壁はKinectの最短距離より遠い()
        {
            SimulatedDepthSource simulator = new SimulatedDepthSource(MountOrientation.Side) { AddNoise = false };
            ushort[] frame = new ushort[simulator.Width * simulator.Height];
            simulator.TryGetLatestFrame(frame);

            foreach (ushort depth in frame)
            {
                Assert.That(depth, Is.GreaterThanOrEqualTo(SimulatedDepthSource.MinimumDepthMm));
            }
        }

        [Test]
        public void プレビューは真下設置のときだけ上下を入れ替える()
        {
            Vector2 tap = new Vector2(0.25f, 0.2f);

            Assert.That(MountGeometry.PreviewToImage(tap, MountOrientation.Side), Is.EqualTo(tap));
            Assert.That(MountGeometry.PreviewToImage(tap, MountOrientation.Below).y, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(MountGeometry.ImageToPreview(MountGeometry.PreviewToImage(tap, MountOrientation.Below), MountOrientation.Below).y, Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void 同じ設置を選び直しても四隅と基準は消えない()
        {
            CalibrationData data = new TestDepthScene().CreateCalibration();

            Assert.That(data.SelectMount(MountOrientation.Side), Is.False);
            Assert.That(data.IsComplete, Is.True);
        }

        [Test]
        public void 別の設置へ切り替えると四隅と基準が消える()
        {
            CalibrationData data = new TestDepthScene().CreateCalibration();

            Assert.That(data.SelectMount(MountOrientation.Below), Is.True);
            Assert.That(data.Mount, Is.EqualTo(MountOrientation.Below));
            Assert.That(data.Corners.Count, Is.EqualTo(0));
            Assert.That(data.HasBaseline, Is.False);
        }

        [Test]
        public void 四隅は左上が壁の左上で左下が壁の原点になる()
        {
            CalibrationData data = new TestDepthScene().CreateCalibration();
            Homography toWall = Homography.FromImageCorners(data.Corners);

            AssertNear(toWall.Transform(data.Corners[0]), new Vector2(0f, 1f));
            AssertNear(toWall.Transform(data.Corners[1]), new Vector2(1f, 1f));
            AssertNear(toWall.Transform(data.Corners[2]), new Vector2(1f, 0f));
            AssertNear(toWall.Transform(data.Corners[3]), new Vector2(0f, 0f));
            AssertNear(toWall.Inverse().Transform(new Vector2(0.5f, 0.5f)), new Vector2(0.5f, 0.5f));
        }

        private static void AssertNear(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f));
        }
    }
}
