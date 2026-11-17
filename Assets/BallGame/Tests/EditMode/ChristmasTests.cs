using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// クリスマスの場面（飾り付け → 完成 → カウントダウン）のテスト。
    /// </summary>
    public sealed class ChristmasTests
    {
        [Test]
        public void 十回当てるとツリーが完成する()
        {
            ChristmasFlow flow = new ChristmasFlow();
            for (int i = 0; i < 9; i++)
            {
                Assert.That(flow.RegisterHit(i * 1.5f), Is.True);
            }

            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Playing));
            Assert.That(flow.Remaining, Is.EqualTo(1));
            Assert.That(flow.IsNextFinal, Is.True, "10個目は星");
            Assert.That(flow.RegisterHit(20f), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Celebrating));
            Assert.That(flow.HitCount, Is.EqualTo(10));
            Assert.That(flow.IsNextFinal, Is.False);
        }

        [Test]
        public void 当たりは一秒に一個までしか数えない()
        {
            ChristmasFlow flow = new ChristmasFlow();

            Assert.That(flow.RegisterHit(5f), Is.True);
            Assert.That(flow.RegisterHit(5.2f), Is.False, "0.2秒後は数えない");
            Assert.That(flow.RegisterHit(5.9f), Is.False, "0.9秒後も数えない");
            Assert.That(flow.RegisterHit(6f), Is.True, "1秒たったら数える");
            Assert.That(flow.HitCount, Is.EqualTo(2));
        }

        [Test]
        public void 完成のあとに当てても数えない()
        {
            ChristmasFlow flow = CreateCompletedFlow();

            Assert.That(flow.RegisterHit(100f), Is.False);
            Assert.That(flow.HitCount, Is.EqualTo(10));
        }

        [Test]
        public void 完成のきらきらを三秒出してからカウントダウンする()
        {
            ChristmasFlow flow = CreateCompletedFlow();

            flow.Tick(2.9f);
            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Celebrating));
            Assert.That(flow.GetCountdownNumber(), Is.EqualTo(0));
            flow.Tick(0.2f);
            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Countdown));
            Assert.That(flow.GetCountdownNumber(), Is.EqualTo(10));
        }

        [Test]
        public void カウントダウンは十から一まで進んで終わる()
        {
            ChristmasFlow flow = CreateCompletedFlow();
            flow.Tick(ChristmasFlow.DefaultCelebrateSeconds);
            List<int> numbers = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                numbers.Add(flow.GetCountdownNumber());
                flow.Tick(1f);
            }

            Assert.That(numbers, Is.EqualTo(new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Finished));
        }

        [Test]
        public void 飾り付け中は時間がたっても進まない()
        {
            ChristmasFlow flow = new ChristmasFlow();
            flow.RegisterHit(0f);
            flow.Tick(100f);

            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Playing));
        }

        [Test]
        public void 飾りの場所は九か所でツリーの中にあり十個目は星()
        {
            List<Vector2> slots = ChristmasTreeLayout.GetOrnamentSlots();

            Assert.That(slots.Count, Is.EqualTo(ChristmasFlow.DefaultGoal - 1));
            foreach (Vector2 slot in slots)
            {
                float halfWidth = ChristmasTreeLayout.GetHalfWidthAt(slot.y);
                Assert.That(Mathf.Abs(slot.x - ChristmasTreeLayout.CenterX), Is.LessThan(halfWidth), slot.ToString());
                Assert.That(slot.y, Is.InRange(ChristmasTreeLayout.BaseY, ChristmasTreeLayout.ApexY));
            }

            for (int i = 0; i < slots.Count; i++)
            {
                for (int j = i + 1; j < slots.Count; j++)
                {
                    Assert.That(Vector2.Distance(slots[i], slots[j]), Is.GreaterThan(80f), "飾りが重ならない");
                }
            }

            Assert.That(ChristmasTreeLayout.StarPosition.y, Is.GreaterThan(ChristmasTreeLayout.ApexY));
        }

        [Test]
        public void 飾りはボールと赤い電球がまざる()
        {
            int bulbs = 0;
            for (int i = 0; i < ChristmasFlow.DefaultGoal - 1; i++)
            {
                bulbs += ChristmasTreeLayout.IsRedBulb(i) ? 1 : 0;
            }

            Assert.That(bulbs, Is.EqualTo(3));
            Assert.That(ChristmasTreeLayout.IsRedBulb(0), Is.False, "最初はボール");
        }

        [Test]
        public void 立体のツリーの飾りは9か所でカメラから見える範囲に並ぶ()
        {
            List<ChristmasTree3DLayout.Slot> slots = ChristmasTree3DLayout.GetSlots();

            Assert.That(slots.Count, Is.EqualTo(ChristmasFlow.DefaultGoal - 1), "最後の1回は星");
            foreach (ChristmasTree3DLayout.Slot slot in slots)
            {
                Assert.That(slot.Height01, Is.InRange(0.05f, 0.9f));
                Assert.That(slot.Lateral01, Is.InRange(-0.8f, 0.8f), "見かけの幅のふちには付けない");
            }

            for (int i = 0; i < slots.Count; i++)
            {
                for (int j = i + 1; j < slots.Count; j++)
                {
                    bool sameRow = Mathf.Abs(slots[i].Height01 - slots[j].Height01) < 0.01f;
                    bool tooClose = sameRow && Mathf.Abs(slots[i].Lateral01 - slots[j].Lateral01) < 0.2f;
                    Assert.That(tooClose, Is.False, "同じ段で飾りが重ならない");
                }
            }
        }

        [Test]
        public void 立体のツリーは上ほど細い()
        {
            Assert.That(ChristmasTree3DLayout.GetConeRadius(0f, 2f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(ChristmasTree3DLayout.GetConeRadius(0.5f, 2f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ChristmasTree3DLayout.GetConeRadius(1f, 2f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void 元の飾りと電球のマテリアルだけを隠す()
        {
            Assert.That(ChristmasTree3DLayout.IsHiddenMaterialName("MI_Christmas_Decorations"), Is.True);
            Assert.That(ChristmasTree3DLayout.IsHiddenMaterialName("MI_Christmas_Lights (Instance)"), Is.True);
            Assert.That(ChristmasTree3DLayout.IsHiddenMaterialName("MI_Christmas_TreeLeavesSnow"), Is.False);
            Assert.That(ChristmasTree3DLayout.IsHiddenMaterialName("MI_Christmas_TreeBark"), Is.False);
            Assert.That(ChristmasTree3DLayout.IsHiddenMaterialName(null), Is.False);
        }

        [Test]
        public void 星は先が上を向いた5つの角()
        {
            Vector3[] outline = ChristmasTree3DLayout.GetStarOutline(5, 1f, 0.45f);

            Assert.That(outline.Length, Is.EqualTo(10));
            Assert.That(outline[0].x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(outline[0].y, Is.EqualTo(1f).Within(1e-5f), "先が真上");
            Assert.That(outline[1].magnitude, Is.EqualTo(0.45f).Within(1e-5f), "内側の点");
        }

        [Test]
        public void 飾りを付ける順番は全部の場所を一度ずつ使う()
        {
            int[] order = ChristmasTreeLayout.GetShuffledOrder(9, new System.Random(4));
            List<int> sorted = new List<int>(order);
            sorted.Sort();

            Assert.That(sorted, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }));
        }

        [Test]
        public void 飾りは当たった所から出てツリーの場所に着く()
        {
            Vector2 from = new Vector2(100f, 900f);
            Vector2 to = new Vector2(960f, 500f);
            Vector2 via = new Vector2(530f, 960f);

            Assert.That(OrnamentFlight.EvaluateArc(from, via, to, 0f), Is.EqualTo(from));
            Assert.That(OrnamentFlight.EvaluateArc(from, via, to, 1f), Is.EqualTo(to));
        }

        [Test]
        public void 電球はぴかぴか明るさが変わる()
        {
            float bright = Twinkle.EvaluateBrightness(0f, Mathf.PI * 0.5f, 1f);
            float dark = Twinkle.EvaluateBrightness(0f, -Mathf.PI * 0.5f, 1f);

            Assert.That(bright, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(dark, Is.EqualTo(0.35f).Within(1e-4f));
        }

        private static ChristmasFlow CreateCompletedFlow()
        {
            ChristmasFlow flow = new ChristmasFlow();
            for (int i = 0; i < ChristmasFlow.DefaultGoal; i++)
            {
                flow.RegisterHit(i * 2f);
            }

            return flow;
        }
    }
}
