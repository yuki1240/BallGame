using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WallBurst.Tests
{
    /// <summary>
    /// 当たり判定のテストです。模擬深度（壁を斜めから見た深度）にボールや手を描いて確かめます。
    /// </summary>
    public sealed class HitDetectorTests
    {
        // 使うボールの直径（mm）。校正の初期値と同じ 7cm
        private const float Ball = CalibrationData.DefaultBallDiameterMm;

        private SimulatedDepthSource source;
        private HitDetector detector;
        private ushort[] frame;
        private float time;

        [SetUp]
        public void SetUp()
        {
            source = new SimulatedDepthSource();
            frame = new ushort[source.Width * source.Height];
            BaselineRecorder recorder = new BaselineRecorder(source.Width, source.Height, 10);
            while (!recorder.IsDone)
            {
                source.RenderFrame(frame);
                recorder.Add(frame);
            }

            ushort[] baseline;
            ushort[] noise;
            recorder.Build(out baseline, out noise);
            CalibrationData data = new CalibrationData();
            data.Width = source.Width;
            data.Height = source.Height;
            data.Baseline = baseline;
            data.Noise = noise;
            data.Corners = new[] { new Vector2(0.2f, 0.35f), new Vector2(0.8f, 0.35f), new Vector2(0.85f, 0.95f), new Vector2(0.15f, 0.95f) };
            detector = new HitDetector();
            string error;
            Assert.That(detector.Configure(data, out error), error);
            time = 0f;
        }

        [Test]
        public void OneBall_GivesOneHitAtTheRightPlace()
        {
            source.ThrowBall(detector.Mapping.WallToPoint(new Vector2(0.5f, 0.5f)), Ball);
            List<WallHit> hits = Run(20);
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(Vector2.Distance(hits[0].Wall, new Vector2(0.5f, 0.5f)), Is.LessThan(0.06f));
        }

        [Test]
        public void TwoBallsAtOnce_GiveTwoHits()
        {
            source.ThrowBall(detector.Mapping.WallToPoint(new Vector2(0.2f, 0.4f)), Ball);
            source.ThrowBall(detector.Mapping.WallToPoint(new Vector2(0.8f, 0.6f)), Ball);
            List<WallHit> hits = Run(20);
            Assert.That(hits.Count, Is.EqualTo(2));
        }

        [Test]
        public void HandTouchingWall_GivesNoHit()
        {
            source.SetHand(true, detector.Mapping.WallToPoint(new Vector2(0.5f, 0.5f)));
            List<WallHit> hits = Run(30);
            Assert.That(hits.Count, Is.EqualTo(0));
        }

        [Test]
        public void BallNearAHand_IsStillCounted()
        {
            source.SetHand(true, detector.Mapping.WallToPoint(new Vector2(0.25f, 0.5f)));
            Run(5);
            source.ThrowBall(detector.Mapping.WallToPoint(new Vector2(0.75f, 0.5f)), Ball);
            List<WallHit> hits = Run(20);
            Assert.That(hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void SixBallsAtOnceWithAHand_GiveSixHits()
        {
            // 6人が同時に投げ、1人は壁を手で触っている
            source.SetHand(true, detector.Mapping.WallToPoint(new Vector2(0.3f, 0.5f)));
            Vector2[] places = { new Vector2(0.1f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(0.9f, 0.3f), new Vector2(0.1f, 0.75f), new Vector2(0.5f, 0.75f), new Vector2(0.9f, 0.75f) };
            foreach (Vector2 place in places)
            {
                source.ThrowBall(detector.Mapping.WallToPoint(place), Ball);
            }

            Assert.That(Run(20).Count, Is.EqualTo(6));
        }

        [Test]
        public void EmptyWall_GivesNoHit()
        {
            Assert.That(Run(60).Count, Is.EqualTo(0));
        }

        private List<WallHit> Run(int frames)
        {
            List<WallHit> all = new List<WallHit>();
            for (int i = 0; i < frames; i++)
            {
                source.RenderFrame(frame);
                time += 1f / 30f;
                all.AddRange(detector.Process(frame, time));
            }

            return all;
        }
    }
}
