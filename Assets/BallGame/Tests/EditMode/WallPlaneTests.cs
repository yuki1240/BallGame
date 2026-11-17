using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// 壁の面までの距離のテスト。
    /// </summary>
    public sealed class WallPlaneTests
    {
        [Test]
        public void 四点から面を作りカメラ側を正の距離にする()
        {
            WallPlane plane = WallPlane.FromCornerPoints(new[]
            {
                new Vector3(-500f, -400f, 2000f),
                new Vector3(500f, -400f, 2000f),
                new Vector3(500f, 400f, 2000f),
                new Vector3(-500f, 400f, 2000f)
            });

            Assert.That(plane.IsValid, Is.True);
            Assert.That(plane.DistanceTo(new Vector3(0f, 0f, 1900f)), Is.EqualTo(100f).Within(0.01f));
            Assert.That(plane.DistanceTo(new Vector3(300f, 100f, 2100f)), Is.EqualTo(-100f).Within(0.01f));
            Assert.That(plane.DistanceTo(Vector3.zero), Is.GreaterThan(0f));
        }

        [Test]
        public void 傾いた面でも垂直距離になる()
        {
            WallPlane plane = WallPlane.FromNormalAndOffset(new Vector3(0f, 1f, 0.75f), -780f);
            Vector3 onPlane = new Vector3(0f, 30f, 1000f);
            Vector3 normal = plane.Normal;

            Assert.That(plane.DistanceTo(onPlane), Is.EqualTo(0f).Within(0.01f));
            Assert.That(plane.DistanceTo(onPlane + normal * 120f), Is.EqualTo(120f).Within(0.01f));
        }

        [Test]
        public void 一直線の点からは面を作れない()
        {
            WallPlane plane = WallPlane.FromCornerPoints(new[]
            {
                new Vector3(0f, 0f, 1000f),
                new Vector3(0f, 0f, 1000f),
                new Vector3(0f, 0f, 1000f),
                new Vector3(0f, 0f, 1000f)
            });

            Assert.That(plane.IsValid, Is.False);
        }

        [Test]
        public void 壁に沿って見ても奥行きでなく壁からの距離で測る()
        {
            TestDepthScene scene = new TestDepthScene();
            HitDetector detector = scene.CreateDetector();
            Assert.That(detector.IsConfigured, Is.True);

            foreach (int row in new[] { 30, 120, 200 })
            {
                int index = row * scene.Width + 160;
                ushort depth = (ushort)Mathf.RoundToInt(scene.Simulator.GetDepthAtWallDistance(row, 100f));
                Assert.That(detector.GetDistanceFromWall(index, depth), Is.EqualTo(100f).Within(8f), "行 " + row);
                Assert.That(detector.GetDistanceFromWall(index, scene.Baseline[index]), Is.EqualTo(0f).Within(0.01f));
            }
        }

        [Test]
        public void 同じ壁からの距離でも遠い行ほど奥行きの差が大きい()
        {
            TestDepthScene scene = new TestDepthScene();
            float nearDifference = scene.Simulator.GetWallDepthMm(220) - scene.Simulator.GetDepthAtWallDistance(220, 100f);
            float farDifference = scene.Simulator.GetWallDepthMm(20) - scene.Simulator.GetDepthAtWallDistance(20, 100f);

            Assert.That(farDifference, Is.GreaterThan(nearDifference * 2f));
        }

        [Test]
        public void 基準から壁の面を作れる()
        {
            TestDepthScene scene = new TestDepthScene();
            CalibrationData data = scene.CreateCalibration();
            WallPlane plane = WallPlaneBuilder.Build(data.Baseline, data.Width, data.Height, data.Corners, DepthIntrinsics.ForResolution(data.Width, data.Height));
            Vector3 expectedNormal = new Vector3(0f, -1f, -0.75f).normalized;

            Assert.That(plane.IsValid, Is.True);
            Assert.That(Vector3.Dot(plane.Normal, expectedNormal), Is.GreaterThan(0.999f));
        }

        [Test]
        public void センサーに近い隅の深度が0でも壁の面を作れる()
        {
            TestDepthScene scene = new TestDepthScene();
            CalibrationData data = scene.CreateCalibration();
            for (int y = scene.Height - 60; y < scene.Height; y++)
            {
                for (int x = 0; x < scene.Width; x++)
                {
                    data.Baseline[y * scene.Width + x] = 0;
                }
            }

            WallPlane plane = WallPlaneBuilder.Build(data.Baseline, data.Width, data.Height, data.Corners, DepthIntrinsics.ForResolution(data.Width, data.Height));
            Vector3 expectedNormal = new Vector3(0f, -1f, -0.75f).normalized;

            Assert.That(plane.IsValid, Is.True);
            Assert.That(Vector3.Dot(plane.Normal, expectedNormal), Is.GreaterThan(0.999f));
            Assert.That(new HitDetector(new DetectionSettings()).Configure(data), Is.True);
        }

        [Test]
        public void 壁でない物が混ざっても面は壁に合う()
        {
            TestDepthScene scene = new TestDepthScene();
            CalibrationData data = scene.CreateCalibration();
            scene.DrawRect(data.Baseline, 40, 40, 60, 60, 300f);

            WallPlane plane = WallPlaneBuilder.Build(data.Baseline, data.Width, data.Height, data.Corners, DepthIntrinsics.ForResolution(data.Width, data.Height));
            Vector3 expectedNormal = new Vector3(0f, -1f, -0.75f).normalized;

            Assert.That(Vector3.Dot(plane.Normal, expectedNormal), Is.GreaterThan(0.999f));
        }

        [Test]
        public void 有効な基準が無いと面を作れない()
        {
            TestDepthScene scene = new TestDepthScene();
            CalibrationData data = scene.CreateCalibration();
            System.Array.Clear(data.Baseline, 0, data.Baseline.Length);

            Assert.That(WallPlaneBuilder.Build(data.Baseline, data.Width, data.Height, data.Corners, DepthIntrinsics.ForResolution(data.Width, data.Height)).IsValid, Is.False);
            Assert.That(new HitDetector(new DetectionSettings()).Configure(data), Is.False);
        }

        [Test]
        public void 壁が少し曲がっていても場所ごとの向きで距離を測れる()
        {
            const int kinkRow = 120;
            TestDepthScene scene = new TestDepthScene();
            SimulatedDepthSource bent = new SimulatedDepthSource(MountOrientation.Side, 1, -0.65f, 760f) { AddNoise = false };
            CalibrationData data = scene.CreateCalibration();
            for (int y = kinkRow; y < scene.Height; y++)
            {
                ushort depth = (ushort)Mathf.RoundToInt(bent.GetWallDepthMm(y));
                for (int x = 0; x < scene.Width; x++)
                {
                    data.Baseline[y * scene.Width + x] = depth;
                }
            }

            HitDetector detector = new HitDetector(new DetectionSettings());
            Assert.That(detector.Configure(data), Is.True);

            foreach (int row in new[] { 140, 180, 215 })
            {
                int index = row * scene.Width + 160;
                ushort ball = (ushort)Mathf.RoundToInt(bent.GetDepthAtWallDistance(row, 70f));
                Assert.That(detector.GetDistanceFromWall(index, ball), Is.EqualTo(70f).Within(8f), "行 " + row);
            }

            foreach (int row in new[] { 30, 80 })
            {
                int index = row * scene.Width + 160;
                ushort ball = (ushort)Mathf.RoundToInt(scene.Simulator.GetDepthAtWallDistance(row, 70f));
                Assert.That(detector.GetDistanceFromWall(index, ball), Is.EqualTo(70f).Within(8f), "行 " + row);
            }
        }

        [Test]
        public void 場所ごとの向きは平らな壁なら全体の平面と同じ()
        {
            TestDepthScene scene = new TestDepthScene();
            CalibrationData data = scene.CreateCalibration();
            DepthIntrinsics intrinsics = DepthIntrinsics.ForResolution(data.Width, data.Height);
            WallPlane plane = WallPlaneBuilder.Build(data.Baseline, data.Width, data.Height, data.Corners, intrinsics);
            Vector3 local = LocalWallNormals.GetNormal(data.Baseline, data.Width, data.Height, 160, 120, LocalWallNormals.DefaultRadius, intrinsics, plane, 0.8f);

            Assert.That(Vector3.Dot(local, plane.Normal), Is.GreaterThan(0.999f));
        }
    }
}
