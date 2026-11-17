using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// 校正の保存のテスト。
    /// </summary>
    public sealed class CalibrationStoreTests
    {
        private static CalibrationData CreateSample()
        {
            CalibrationData data = new CalibrationData();
            data.AddCorner(new Vector2(0.1f, 0.2f));
            data.AddCorner(new Vector2(0.9f, 0.15f));
            data.AddCorner(new Vector2(0.85f, 0.8f));
            data.AddCorner(new Vector2(0.12f, 0.9f));
            data.SetBaseline(new ushort[] { 1000, 0, 1200, 1300, 1400, 1500 }, 3, 2);
            data.SetMaxDistanceCm(30);
            data.NoiseFloorMm = 50;
            data.MirrorX = false;
            data.InputSource = DepthSourceKind.Simulated;
            data.Mount = MountOrientation.Below;
            return data;
        }

        private static CalibrationData RoundTrip(CalibrationData data, bool legacy)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                if (legacy)
                {
                    CalibrationStore.WriteLegacy(stream, data);
                }
                else
                {
                    CalibrationStore.Write(stream, data);
                }

                stream.Position = 0;
                return CalibrationStore.Read(stream);
            }
        }

        [Test]
        public void 保存した内容をすべて読み戻せる()
        {
            CalibrationData original = CreateSample();
            CalibrationData loaded = RoundTrip(original, false);

            Assert.That(loaded.Corners, Is.EqualTo(original.Corners));
            Assert.That(loaded.Baseline, Is.EqualTo(original.Baseline));
            Assert.That(loaded.Width, Is.EqualTo(3));
            Assert.That(loaded.Height, Is.EqualTo(2));
            Assert.That(loaded.MaxDistanceMm, Is.EqualTo(300));
            Assert.That(loaded.NoiseFloorMm, Is.EqualTo(50));
            Assert.That(loaded.MirrorX, Is.False);
            Assert.That(loaded.InputSource, Is.EqualTo(DepthSourceKind.Simulated));
            Assert.That(loaded.Mount, Is.EqualTo(MountOrientation.Below));
        }

        [Test]
        public void 末尾が無い古いファイルも読めて残りは初期値になる()
        {
            CalibrationData loaded = RoundTrip(CreateSample(), true);

            Assert.That(loaded.Corners.Count, Is.EqualTo(4));
            Assert.That(loaded.HasBaseline, Is.True);
            Assert.That(loaded.MaxDistanceMm, Is.EqualTo(300));
            Assert.That(loaded.InputSource, Is.EqualTo(DepthSourceKind.Kinect));
            Assert.That(loaded.Mount, Is.EqualTo(MountOrientation.Side));
        }

        [Test]
        public void 基準が無い校正も保存できる()
        {
            CalibrationData data = new CalibrationData();
            data.AddCorner(new Vector2(0.3f, 0.3f));
            CalibrationData loaded = RoundTrip(data, false);

            Assert.That(loaded.Corners.Count, Is.EqualTo(1));
            Assert.That(loaded.HasBaseline, Is.False);
            Assert.That(loaded.MirrorX, Is.True);
        }

        [Test]
        public void ファイルへ保存して読み込み消せる()
        {
            string path = Path.Combine(Path.GetTempPath(), "ballgame_test_" + System.Guid.NewGuid() + ".bin");
            try
            {
                Assert.That(CalibrationStore.TrySave(path, CreateSample()), Is.True);
                Assert.That(CalibrationStore.LoadOrDefault(path).Mount, Is.EqualTo(MountOrientation.Below));
                CalibrationStore.Delete(path);
                Assert.That(File.Exists(path), Is.False);
                Assert.That(CalibrationStore.LoadOrDefault(path).IsComplete, Is.False);
            }
            finally
            {
                CalibrationStore.Delete(path);
            }
        }

        [Test]
        public void 壊れたファイルは初期値になる()
        {
            string path = Path.Combine(Path.GetTempPath(), "ballgame_broken_" + System.Guid.NewGuid() + ".bin");
            try
            {
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
                CalibrationData loaded = CalibrationStore.LoadOrDefault(path);
                Assert.That(loaded.Corners.Count, Is.EqualTo(0));
                Assert.That(loaded.Mount, Is.EqualTo(MountOrientation.Side));
            }
            finally
            {
                CalibrationStore.Delete(path);
            }
        }

        [Test]
        public void リセットで初期状態と横設置に戻る()
        {
            CalibrationData data = CreateSample();
            data.ResetAll();

            Assert.That(data.Corners.Count, Is.EqualTo(0));
            Assert.That(data.HasBaseline, Is.False);
            Assert.That(data.MaxDistanceMm, Is.EqualTo(150));
            Assert.That(data.NoiseFloorMm, Is.EqualTo(45));
            Assert.That(data.MirrorX, Is.True);
            Assert.That(data.InputSource, Is.EqualTo(DepthSourceKind.Kinect));
            Assert.That(data.Mount, Is.EqualTo(MountOrientation.Side));
        }

        [Test]
        public void 壁からの距離は5から80cmに収まる()
        {
            CalibrationData data = new CalibrationData();

            Assert.That(data.GetMaxDistanceCm(), Is.EqualTo(15));
            Assert.That(data.SetMaxDistanceCm(2), Is.EqualTo(5));
            Assert.That(data.SetMaxDistanceCm(120), Is.EqualTo(80));
            Assert.That(data.MaxDistanceMm, Is.EqualTo(800));
        }

        [Test]
        public void 左右反転で四隅と基準も左右に写る()
        {
            CalibrationData data = CreateSample();
            data.MirrorX = true;
            data.ApplyMirror(false);

            Assert.That(data.Corners[0].x, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(data.Corners[0].y, Is.EqualTo(0.15f).Within(1e-5f));
            Assert.That(data.Corners[1].x, Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(data.Baseline, Is.EqualTo(new ushort[] { 1200, 0, 1000, 1500, 1400, 1300 }));
        }

        [Test]
        public void 基準は全ゼロのフレームを数えず中央値になる()
        {
            BaselineRecorder recorder = new BaselineRecorder(2, 1, 3);
            recorder.Begin();

            Assert.That(recorder.AddFrame(new ushort[] { 0, 0 }), Is.False);
            Assert.That(recorder.AddFrame(new ushort[] { 1000, 0 }), Is.True);
            Assert.That(recorder.AddFrame(new ushort[] { 1010, 2000 }), Is.True);
            Assert.That(recorder.AddFrame(new ushort[] { 990, 2010 }), Is.True);
            Assert.That(recorder.IsComplete, Is.True);
            Assert.That(recorder.BuildMedian(), Is.EqualTo(new ushort[] { 1000, 2010 }));
        }

        [Test]
        public void 隅は1つずつ動かせて画像の外には出ない()
        {
            CalibrationData data = CreateSample();

            Assert.That(data.SetCorner(1, new Vector2(1.4f, 0.3f)), Is.True);
            Assert.That(data.Corners[1], Is.EqualTo(new Vector2(1f, 0.3f)));
            Assert.That(data.SetCorner(7, Vector2.zero), Is.False);
            Assert.That(data.HasBaseline, Is.True);
        }

        [Test]
        public void 四隅はまとめて動かせてはみ出す分は縮める()
        {
            CalibrationData data = CreateSample();
            Vector2 applied = data.TranslateCorners(new Vector2(0.5f, -0.05f));

            Assert.That(applied.x, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(applied.y, Is.EqualTo(-0.05f).Within(1e-5f));
            Assert.That(data.Corners[1].x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(data.Corners[0].x, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(data.Corners[0].y, Is.EqualTo(0.15f).Within(1e-5f));
        }

        [Test]
        public void 基準を録ると画素ごとの揺れ幅も残り0になった画素は不安定になる()
        {
            BaselineRecorder recorder = new BaselineRecorder(2, 1, 3);
            recorder.Begin();
            recorder.AddFrame(new ushort[] { 1000, 0 });
            recorder.AddFrame(new ushort[] { 1010, 2000 });
            recorder.AddFrame(new ushort[] { 990, 2010 });

            ushort[] noise = recorder.BuildNoise();

            Assert.That(noise[0], Is.EqualTo(15));
            Assert.That(noise[1], Is.EqualTo(BaselineRecorder.UnstableNoise));
        }

        [Test]
        public void 揺れ幅も保存して読み戻せる()
        {
            CalibrationData original = CreateSample();
            original.BaselineNoise = new ushort[] { 3, BaselineRecorder.UnstableNoise, 5, 6, 7, 8 };
            original.SetNoiseFloorMm(60);
            CalibrationData loaded = RoundTrip(original, false);

            Assert.That(loaded.BaselineNoise, Is.EqualTo(original.BaselineNoise));
            Assert.That(loaded.NoiseFloorMm, Is.EqualTo(60));
            Assert.That(RoundTrip(original, true).BaselineNoise, Is.Null);
        }

        [Test]
        public void ノイズ床は20から200mmに収まる()
        {
            CalibrationData data = new CalibrationData();

            Assert.That(data.SetNoiseFloorMm(5), Is.EqualTo(20));
            Assert.That(data.SetNoiseFloorMm(500), Is.EqualTo(200));
        }
    }
}
