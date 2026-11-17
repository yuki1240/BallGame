using NUnit.Framework;

namespace BallGame.Tests
{
    /// <summary>
    /// ボール検知と人サイズの除外のテスト。
    /// </summary>
    public sealed class HitDetectorTests
    {
        private TestDepthScene scene;
        private HitDetector detector;

        [SetUp]
        public void SetUp()
        {
            scene = new TestDepthScene();
            detector = scene.CreateDetector();
        }

        [Test]
        public void 何も無ければヒットしない()
        {
            DetectionResult result = detector.Process(scene.EmptyFrame(), 0f);

            Assert.That(result.Hits, Is.Empty);
            Assert.That(result.ChangedPixelCount, Is.EqualTo(0));
        }

        [Test]
        public void 大きめのボールは1フレームで重心にヒットする()
        {
            ushort[] frame = scene.EmptyFrame();
            Assert.That(scene.DrawDisc(frame, 100f, 80f, 4.5f, 70f), Is.GreaterThanOrEqualTo(24));

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.Hits.Count, Is.EqualTo(1));
            Assert.That(result.Hits[0].Pixel.x, Is.EqualTo(99.5f).Within(0.6f));
            Assert.That(result.Hits[0].Pixel.y, Is.EqualTo(79.5f).Within(0.6f));
            Assert.That(result.Hits[0].ImageUv.x, Is.EqualTo(100f / scene.Width).Within(0.01f));
        }

        [Test]
        public void 小さい塊は2フレーム続いてから採用する()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 200f, 80f, 2.3f, 70f);

            Assert.That(detector.Process(frame, 0f).Hits, Is.Empty);
            Assert.That(detector.Process(frame, 0.033f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 面積12未満は捨てる()
        {
            ushort[] frame = scene.EmptyFrame();
            Assert.That(scene.DrawDisc(frame, 200f, 120f, 1.5f, 70f), Is.LessThan(12));

            for (int i = 0; i < 4; i++)
            {
                Assert.That(detector.Process(frame, i * 0.033f).Hits, Is.Empty);
            }
        }

        [Test]
        public void 孤立した点は捨てる()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawRect(frame, 150, 100, 1, 1, 70f);

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.ChangedPixelCount, Is.EqualTo(1));
            Assert.That(result.Blobs, Is.Empty);
        }

        [Test]
        public void ノイズ床未満と上限より遠いものは捨てる()
        {
            ushort[] near = scene.EmptyFrame();
            scene.DrawDisc(near, 100f, 100f, 5f, 30f);
            Assert.That(detector.Process(near, 0f).Hits, Is.Empty);

            ushort[] far = scene.EmptyFrame();
            scene.DrawDisc(far, 220f, 100f, 5f, 300f);
            DetectionResult result = detector.Process(far, 1f);
            Assert.That(result.Hits, Is.Empty);
            Assert.That(result.ChangedPixelCount, Is.GreaterThan(0));
        }

        [Test]
        public void 壁からの距離の上限を広げると遠いものも拾う()
        {
            detector.Settings.MaxDistanceMm = 400;
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 220f, 100f, 5f, 300f);

            Assert.That(detector.Process(frame, 0f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 深度0になった物体も候補にする()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 160f, 120f, 4.5f, -1f);

            int total = 0;
            for (int i = 0; i < 12; i++)
            {
                total += detector.Process(frame, i * 0.033f).Hits.Count;
            }

            Assert.That(total, Is.EqualTo(1));
        }

        [Test]
        public void 四隅の外側は見ない()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 6f, 6f, 4.5f, 70f);

            Assert.That(detector.Process(frame, 0f).Hits, Is.Empty);
        }

        [Test]
        public void ボールとして検知できた塊だけが赤い表示の対象になる()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 100f, 80f, 4.5f, 70f);

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.HasBall, Is.True);
            Assert.That(result.BallMask[80 * scene.Width + 100], Is.True, "ボールの中心");
            Assert.That(result.BallMask[200 * scene.Width + 300], Is.False, "ボールの無い所");
        }

        [Test]
        public void ボールの重心が画像の割合で分かる()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 100f, 80f, 4.5f, 70f);

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.BallImageUvs.Count, Is.EqualTo(1));
            Assert.That(result.BallImageUvs[0].x, Is.EqualTo(100f / scene.Width).Within(0.01f));
            Assert.That(result.BallImageUvs[0].y, Is.EqualTo(80f / scene.Height).Within(0.01f));
            Assert.That(detector.Process(scene.EmptyFrame(), 0.033f).BallImageUvs.Count, Is.EqualTo(0));
        }

        [Test]
        public void きれいな壁なら検知できない画素はない()
        {
            Assert.That(detector.BlindMask.Length, Is.EqualTo(scene.Width * scene.Height));
            Assert.That(detector.BlindRatio, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void 診断の表は帯の数だけ行が出る()
        {
            string table = DetectorDiagnostics.BuildBandTable(detector, scene.EmptyFrame());
            string[] lines = table.Split('\n');
            int rows = 0;
            foreach (string line in lines)
            {
                rows += line.Length > 0 && char.IsDigit(line[0]) ? 1 : 0;
            }

            Assert.That(rows, Is.EqualTo(DetectorDiagnostics.BandCount));
        }

        [Test]
        public void 何も無ければ赤い表示はない()
        {
            DetectionResult result = detector.Process(scene.EmptyFrame(), 0f);

            Assert.That(result.HasBall, Is.False);
        }

        [Test]
        public void 人のフレームでは赤い表示を出さない()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawRect(frame, 60, 40, 30, 90, 80f);
            scene.DrawDisc(frame, 250f, 60f, 4.5f, 70f);

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.PersonDetected, Is.True);
            Assert.That(result.HasBall, Is.False);
        }

        [Test]
        public void 縦横が80画素を超える塊は人として除外する()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawRect(frame, 60, 40, 30, 90, 80f);
            scene.DrawDisc(frame, 250f, 60f, 4.5f, 70f);

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.PersonDetected, Is.True);
            Assert.That(result.Hits, Is.Empty);
        }

        [Test]
        public void 変化した面積が大きいと壁から遠くても人として除外する()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawRect(frame, 20, 15, 200, 90, 400f);
            scene.DrawDisc(frame, 270f, 150f, 4.5f, 70f);

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.ChangedArea, Is.GreaterThan(4500f));
            Assert.That(result.PersonDetected, Is.True);
            Assert.That(result.Hits, Is.Empty);
        }

        [Test]
        public void 人が去ってから0点5秒はボールを出さない()
        {
            ushort[] person = scene.EmptyFrame();
            scene.DrawRect(person, 60, 40, 30, 90, 80f);
            detector.Process(person, 0f);

            ushort[] ball = scene.EmptyFrame();
            scene.DrawDisc(ball, 250f, 60f, 4.5f, 70f);
            DetectionResult during = detector.Process(ball, 0.3f);
            Assert.That(during.InCooldown, Is.True);
            Assert.That(during.Hits, Is.Empty);

            ushort[] later = scene.EmptyFrame();
            scene.DrawDisc(later, 150f, 180f, 8f, 70f);
            Assert.That(detector.Process(later, 0.6f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 同じ場所に留まった物体は出し続けない()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 100f, 100f, 4.5f, 70f);
            int total = 0;
            for (int i = 0; i < 30; i++)
            {
                total += detector.Process(frame, i * 0.033f).Hits.Count;
            }

            Assert.That(total, Is.EqualTo(1));
        }

        [Test]
        public void 別の場所に当たればまたヒットする()
        {
            ushort[] first = scene.EmptyFrame();
            scene.DrawDisc(first, 100f, 100f, 4.5f, 70f);
            ushort[] second = scene.EmptyFrame();
            scene.DrawDisc(second, 220f, 150f, 7f, 70f);

            Assert.That(detector.Process(first, 0f).Hits.Count, Is.EqualTo(1));
            Assert.That(detector.Process(scene.EmptyFrame(), 0.033f).Hits, Is.Empty);
            Assert.That(detector.Process(second, 0.066f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 壁を伝って落ちる軌跡は1球として1回だけ出す()
        {
            int total = 0;
            for (int i = 0; i < 18; i++)
            {
                ushort[] frame = scene.EmptyFrame();
                scene.DrawDisc(frame, 100f + i * 2f, 60f + i * 7f, 4.5f, 70f);
                total += detector.Process(frame, i * 0.033f).Hits.Count;
            }

            Assert.That(total, Is.EqualTo(1));
        }

        [Test]
        public void 遠くから飛んできたボールは壁に着いた点で出す()
        {
            float[] heights = { 140f, 140f, 110f, 70f, 110f, 140f, 140f, 140f };
            float[] xs = { 60f, 100f, 140f, 180f, 170f, 160f, 150f, 140f };
            int firstHitFrame = -1;
            int total = 0;
            float hitX = 0f;
            for (int i = 0; i < heights.Length; i++)
            {
                ushort[] frame = scene.EmptyFrame();
                scene.DrawDisc(frame, xs[i], 100f + i * 6f, 4.5f, heights[i]);
                DetectionResult result = detector.Process(frame, i * 0.033f);
                total += result.Hits.Count;
                if (result.Hits.Count > 0)
                {
                    firstHitFrame = i;
                    hitX = result.Hits[0].Pixel.x;
                }
            }

            Assert.That(total, Is.EqualTo(1));
            Assert.That(firstHitFrame, Is.GreaterThanOrEqualTo(3), "着く前には出さない");
            Assert.That(hitX, Is.EqualTo(180f).Within(2f), "最初に見えた所ではなく、壁に着いた所");
        }

        [Test]
        public void 深度0から見え始めた軌跡でも壁に着いた点で出す()
        {
            float[] heights = { -1f, -1f, 100f, 140f, 140f, 110f, 70f, 110f, 140f, 140f };
            float[] xs = { 40f, 60f, 80f, 100f, 120f, 150f, 180f, 170f, 160f, 150f };
            int total = 0;
            float hitX = 0f;
            for (int i = 0; i < heights.Length; i++)
            {
                ushort[] frame = scene.EmptyFrame();
                scene.DrawDisc(frame, xs[i], 100f + i * 6f, 4.5f, heights[i]);
                DetectionResult result = detector.Process(frame, i * 0.033f);
                total += result.Hits.Count;
                if (result.Hits.Count > 0)
                {
                    hitX = result.Hits[0].Pixel.x;
                }
            }

            Assert.That(total, Is.EqualTo(1));
            Assert.That(hitX, Is.EqualTo(180f).Within(2f));
        }

        [Test]
        public void 飛んでいる間は赤い印を付けず当たりの瞬間だけ付ける()
        {
            float[] heights = { 140f, 140f, 110f, 70f, 110f, 140f, 140f };
            bool markedBeforeHit = false;
            bool markedAtHit = false;
            for (int i = 0; i < heights.Length; i++)
            {
                ushort[] frame = scene.EmptyFrame();
                scene.DrawDisc(frame, 60f + i * 20f, 100f, 4.5f, heights[i]);
                DetectionResult result = detector.Process(frame, i * 0.033f);
                if (result.Hits.Count > 0)
                {
                    markedAtHit = result.HasBall && result.BallImageUvs.Count == 1;
                }
                else
                {
                    markedBeforeHit |= result.HasBall;
                }
            }

            Assert.That(markedBeforeHit, Is.False);
            Assert.That(markedAtHit, Is.True);
        }

        [Test]
        public void 軌跡が途切れたあとの次の1球はまた出す()
        {
            ushort[] ball = scene.EmptyFrame();
            scene.DrawDisc(ball, 100f, 100f, 4.5f, 70f);
            Assert.That(detector.Process(ball, 0f).Hits.Count, Is.EqualTo(1));

            int total = 0;
            for (int i = 1; i <= 10; i++)
            {
                total += detector.Process(scene.EmptyFrame(), i * 0.033f).Hits.Count;
            }

            ushort[] next = scene.EmptyFrame();
            scene.DrawDisc(next, 110f, 105f, 4.5f, 70f);
            total += detector.Process(next, 0.4f).Hits.Count;
            Assert.That(total, Is.EqualTo(1));
        }

        [Test]
        public void 模擬深度のクリックも揺れの中で検知できる()
        {
            SimulatedDepthSource simulator = new SimulatedDepthSource(MountOrientation.Side, 42);
            BaselineRecorder recorder = new BaselineRecorder(simulator.Width, simulator.Height);
            ushort[] frame = new ushort[simulator.Width * simulator.Height];
            recorder.Begin();
            while (!recorder.IsComplete)
            {
                simulator.TryGetLatestFrame(frame);
                recorder.AddFrame(frame);
            }

            CalibrationData data = scene.CreateCalibration();
            data.SetBaseline(recorder.BuildMedian(), simulator.Width, simulator.Height);
            HitDetector noisyDetector = new HitDetector(new DetectionSettings());
            Assert.That(noisyDetector.Configure(data), Is.True);

            int hits = 0;
            for (int i = 0; i < 20; i++)
            {
                simulator.TryGetLatestFrame(frame);
                hits += noisyDetector.Process(frame, i * 0.033f).Hits.Count;
            }

            Assert.That(hits, Is.EqualTo(0), "揺れだけではヒットしない");
            simulator.InjectBall(new UnityEngine.Vector2(0.5f, 0.3f));
            simulator.TryGetLatestFrame(frame);
            DetectionResult result = noisyDetector.Process(frame, 1f);
            Assert.That(result.Hits.Count, Is.EqualTo(1));
            Assert.That(result.Hits[0].ImageUv.x, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(result.Hits[0].ImageUv.y, Is.EqualTo(0.3f).Within(0.02f));
        }

        [Test]
        public void 揺れの大きい画素ではしきい値が上がり小さな変化を拾わない()
        {
            CalibrationData data = scene.CreateCalibration();
            ushort[] noise = new ushort[data.Baseline.Length];
            for (int i = 0; i < noise.Length; i++)
            {
                noise[i] = 40;
            }

            data.SetBaseline(data.Baseline, data.Width, data.Height, noise);
            HitDetector strict = new HitDetector(new DetectionSettings());
            Assert.That(strict.Configure(data), Is.True);
            int index = 100 * scene.Width + 160;
            Assert.That(strict.GetNoiseThreshold(index), Is.GreaterThan(45f));

            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 160f, 100f, 4.5f, 60f);
            Assert.That(detector.Process(frame, 0f).Hits.Count, Is.EqualTo(1), "揺れ幅が無ければ拾う");
            Assert.That(strict.Process(frame, 0f).Hits, Is.Empty, "揺れる画素では拾わない");
        }

        [Test]
        public void 基準を録る間に0になった画素は深度0でも候補にしない()
        {
            CalibrationData data = scene.CreateCalibration();
            ushort[] noise = new ushort[data.Baseline.Length];
            for (int i = 0; i < noise.Length; i++)
            {
                noise[i] = BaselineRecorder.UnstableNoise;
            }

            data.SetBaseline(data.Baseline, data.Width, data.Height, noise);
            HitDetector strict = new HitDetector(new DetectionSettings());
            Assert.That(strict.Configure(data), Is.True);
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 160f, 120f, 4.5f, -1f);

            Assert.That(strict.Process(frame, 0f).Hits, Is.Empty);
            Assert.That(strict.Process(frame, 0f).ChangedPixelCount, Is.EqualTo(0));
        }

        [Test]
        public void しきい値すれすれのまばらな点はボールにしない()
        {
            ushort[] frame = scene.EmptyFrame();
            System.Random random = new System.Random(9);
            for (int i = 0; i < 400; i++)
            {
                int x = 120 + random.Next(30);
                int y = 100 + random.Next(30);
                if (random.NextDouble() < 0.5)
                {
                    scene.DrawRect(frame, x, y, 1, 1, 50f);
                }
            }

            for (int f = 0; f < 5; f++)
            {
                Assert.That(detector.Process(frame, f * 0.033f).Hits, Is.Empty);
            }
        }

        [Test]
        public void 弱い縁も塊に含めてボールを拾いやすくする()
        {
            ushort[] coreOnly = scene.EmptyFrame();
            int coreArea = scene.DrawDisc(coreOnly, 160f, 100f, 2.3f, 70f);
            Assert.That(coreArea, Is.LessThan(24));
            Assert.That(detector.Process(coreOnly, 0f).Hits, Is.Empty, "芯だけなら1フレームでは出ない");

            detector.ResetTracking();
            ushort[] withRim = scene.EmptyFrame();
            scene.DrawDisc(withRim, 220f, 100f, 4.5f, 32f);
            scene.DrawDisc(withRim, 220f, 100f, 2.3f, 70f);
            DetectionResult result = detector.Process(withRim, 0f);

            Assert.That(result.Hits.Count, Is.EqualTo(1), "弱い縁も含めると1フレームで出る");
            Assert.That(result.Hits[0].Area, Is.GreaterThanOrEqualTo(24));
        }

        [Test]
        public void 弱い変化だけの塊はボールにしない()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 160f, 100f, 6f, 40f);

            for (int f = 0; f < 3; f++)
            {
                Assert.That(detector.Process(frame, f * 0.033f).Hits, Is.Empty);
            }
        }

        [Test]
        public void 揺れが大きくてもしきい値はノイズ床の2倍で止まる()
        {
            CalibrationData data = scene.CreateCalibration();
            ushort[] noise = new ushort[data.Baseline.Length];
            for (int i = 0; i < noise.Length; i++)
            {
                noise[i] = 500;
            }

            data.SetBaseline(data.Baseline, data.Width, data.Height, noise);
            HitDetector noisy = new HitDetector(new DetectionSettings());
            Assert.That(noisy.Configure(data), Is.True);

            Assert.That(noisy.GetNoiseThreshold(100 * scene.Width + 160), Is.EqualTo(90f).Within(0.01f));
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 160f, 100f, 4.5f, 120f);
            Assert.That(noisy.Process(frame, 0f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 採用しない理由を画面用に説明できる()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 160f, 100f, 4.5f, 70f);
            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.HasLargestBlob, Is.True);
            Assert.That(result.LargestBlobRejectReason, Is.EqualTo(string.Empty));
            Assert.That(detector.Process(scene.EmptyFrame(), 1f).DescribeLargestBlob(), Is.EqualTo("塊なし"));
        }

        [Test]
        public void はっきり壁から離れた小さい塊は1フレームで採用する()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 200f, 80f, 2.3f, 95f);

            Assert.That(detector.Process(frame, 0f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 同じ大きさの物は近くても遠くても同じ面積に直る()
        {
            ushort[] far = scene.EmptyFrame();
            ushort[] near = scene.EmptyFrame();
            scene.DrawDisc(far, 160f, 40f, 3f, 70f);
            scene.DrawDisc(near, 160f, 200f, 9f, 70f);
            float farDepth = scene.Simulator.GetDepthAtWallDistance(40, 70f);
            float nearDepth = scene.Simulator.GetDepthAtWallDistance(200, 70f);

            Assert.That(farDepth / nearDepth, Is.GreaterThan(2.5f), "遠い所は近い所の2.5倍以上の深度");
            Assert.That(detector.GetSizeWeight(farDepth), Is.GreaterThan(detector.GetSizeWeight(nearDepth) * 6f));

            DepthBlob farBlob = detector.Process(far, 0f).LargestBlob;
            detector.ResetTracking();
            DepthBlob nearBlob = detector.Process(near, 1f).LargestBlob;
            float farArea = farBlob.NormalizedArea;
            float nearArea = nearBlob.NormalizedArea;

            Assert.That(nearBlob.Area, Is.GreaterThan(farBlob.Area * 5));
            Assert.That(nearArea / farArea, Is.InRange(0.5f, 2f), "画素数は大きく違っても、実際の大きさはほぼ同じ");
        }

        [Test]
        public void 近い所の手のひら大の物は人と判定しない()
        {
            ushort[] frame = scene.EmptyFrame();
            int pixels = scene.DrawRect(frame, 130, 170, 60, 40, 70f);
            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(pixels, Is.GreaterThan(2000));
            Assert.That(result.PersonDetected, Is.False);
            Assert.That(result.Hits.Count, Is.EqualTo(1));
        }
    }
}
