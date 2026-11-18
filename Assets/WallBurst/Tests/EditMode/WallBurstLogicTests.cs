using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WallBurst.Tests
{
    /// <summary>
    /// 計算の部分（射影変換・壁の面・ツリーの大きさ・飾りの置き場所・シーンの選び方・保存）のテストです。
    /// Unity の Test Runner（Window → General → Test Runner → EditMode）で実行します。
    /// </summary>
    public sealed class WallBurstLogicTests
    {
        [Test]
        public void Homography_MapsCornersToWallCorners()
        {
            Vector2[] corners = { new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.25f), new Vector2(0.9f, 0.9f), new Vector2(0.1f, 0.85f) };
            Homography h = Homography.FromImageCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                Vector2 mapped = h.Transform(corners[i]);
                Assert.That(Vector2.Distance(mapped, Homography.WallCorners[i]), Is.LessThan(1e-4f));
            }

            Vector2 back = h.Inverse().Transform(new Vector2(0.5f, 0.5f));
            Assert.That(Vector2.Distance(h.Transform(back), new Vector2(0.5f, 0.5f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void WallPlane_FitsTiltedPlaneAndPointsTowardCamera()
        {
            Vector3 normal = new Vector3(0.2f, 0.9f, 0.4f).normalized;
            List<Vector3> points = new List<Vector3>();
            System.Random random = new System.Random(3);
            Vector3 u = Vector3.Cross(normal, Vector3.right).normalized;
            Vector3 v = Vector3.Cross(normal, u).normalized;
            for (int i = 0; i < 400; i++)
            {
                float a = (float)random.NextDouble() * 2000f - 1000f;
                float b = (float)random.NextDouble() * 2000f - 1000f;
                points.Add(normal * 1500f + u * a + v * b);
            }

            WallPlane plane = WallPlane.Fit(points);
            Assert.That(plane.IsValid);
            Assert.That(Mathf.Abs(Vector3.Dot(plane.Normal, normal)), Is.GreaterThan(0.999f));
            Assert.That(plane.DistanceTo(Vector3.zero), Is.GreaterThan(0f), "カメラ（原点）側が正");
            Assert.That(Mathf.Abs(plane.DistanceTo(points[10])), Is.LessThan(1f));
        }

        [TestCase(16f / 9f)]
        [TestCase(16f / 10f)]
        [TestCase(4f / 3f)]
        [TestCase(1f)]
        [TestCase(9f / 16f)]
        public void TreeLayout_FitsWithinMarginsForAnyAspect(float aspect)
        {
            Vector3 size = new Vector3(2.2f, 4.6f, 2.2f);
            bool byWidth;
            float distance = TreeLayout.ComputeCameraDistance(size, 30f, aspect, out byWidth);
            Assert.That(TreeLayout.HeightFraction(size, distance, 30f), Is.LessThanOrEqualTo(0.92f + 1e-4f));
            Assert.That(TreeLayout.WidthFraction(size, distance, 30f, aspect), Is.LessThanOrEqualTo(0.90f + 1e-4f));
            if (!byWidth)
            {
                Assert.That(TreeLayout.HeightFraction(size, distance, 30f), Is.EqualTo(0.92f).Within(1e-3f), "高さで決まるときは画面の高さいっぱい");
            }
        }

        [Test]
        public void OrnamentPlacer_PlacesAllWithoutOverlap()
        {
            OrnamentPlacer.Cone cone = new OrnamentPlacer.Cone();
            cone.AxisXZ = Vector2.zero;
            cone.BottomY = 0.5f;
            cone.TopY = 4f;
            cone.BottomRadius = 1.6f;
            cone.TopRadius = 0.2f;
            float diameter = 4.6f * 0.06f;
            List<Vector3> points = OrnamentPlacer.Generate(cone, 29, diameter * 1.2f, new System.Random(5));
            Assert.That(points.Count, Is.EqualTo(29));
            Assert.That(OrnamentPlacer.MinimumSpacing(points), Is.GreaterThanOrEqualTo(diameter), "飾りどうしが重ならない");
            foreach (Vector3 p in points)
            {
                Assert.That(p.y, Is.InRange(cone.BottomY, cone.TopY));
                Assert.That(p.z, Is.LessThanOrEqualTo(0f), "カメラから見える前側");
            }
        }

        [Test]
        public void SceneRotation_NeverRepeatsWithTwoScenes()
        {
            List<string> scenes = new List<string> { "A", "B" };
            System.Random random = new System.Random(1);
            string current = SceneRotation.PickNext(scenes, null, random);
            for (int i = 0; i < 20; i++)
            {
                string next = SceneRotation.PickNext(scenes, current, random);
                Assert.That(next, Is.Not.EqualTo(current));
                current = next;
            }

            Assert.That(SceneRotation.PickNext(new List<string> { "A" }, "A", random), Is.EqualTo("A"));
            Assert.That(SceneRotation.PickNext(new List<string>(), "A", random), Is.Null);
        }

        [Test]
        public void CalibrationStore_RoundTrips()
        {
            CalibrationData data = new CalibrationData();
            data.Width = 4;
            data.Height = 2;
            data.Baseline = new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            data.Noise = new ushort[] { 0, 1, 2, 3, CalibrationData.UnstableNoise, 5, 6, 7 };
            data.Corners = new[] { new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.2f), new Vector2(0.9f, 0.8f), new Vector2(0.1f, 0.8f) };
            data.BallDiameterMm = 150;
            data.PreviewRotation = 3;
            using (MemoryStream stream = new MemoryStream())
            {
                CalibrationStore.Write(data, stream);
                stream.Position = 0;
                CalibrationData read = CalibrationStore.Read(stream);
                Assert.That(read.Baseline, Is.EqualTo(data.Baseline));
                Assert.That(read.Noise, Is.EqualTo(data.Noise));
                Assert.That(read.Corners[2], Is.EqualTo(data.Corners[2]));
                Assert.That(read.BallDiameterMm, Is.EqualTo(150));
                Assert.That(read.PreviewRotation, Is.EqualTo(3));
            }
        }
    }
}
