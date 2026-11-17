using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// シャボン玉の動きと、割れる判定のテスト。
    /// </summary>
    public sealed class BubbleFieldTests
    {
        private const float Width = 1920f;
        private const float Height = 1080f;

        private BubbleField CreateField(int count = BubbleField.DefaultCount)
        {
            return new BubbleField(Width, Height, new System.Random(3), count);
        }

        [Test]
        public void 最初に決めた数の玉が浮かぶ()
        {
            BubbleField field = CreateField(10);

            Assert.That(field.Bubbles.Count, Is.EqualTo(10));
            foreach (Bubble bubble in field.Bubbles)
            {
                Assert.That(bubble.Radius >= BubbleField.MinRadius && bubble.Radius <= BubbleField.MaxRadius, Is.True);
                Assert.That(bubble.Position.x >= 0f && bubble.Position.x <= Width, Is.True);
            }
        }

        [Test]
        public void 玉は時間とともに上へ昇る()
        {
            BubbleField field = CreateField(1);
            float before = field.Bubbles[0].Position.y;

            field.Tick(0.5f);

            Assert.That(field.Bubbles[0].Position.y > before, Is.True);
        }

        [Test]
        public void 上へ出た玉は消えて数は保たれる()
        {
            BubbleField field = CreateField(8);
            for (int i = 0; i < 600; i++)
            {
                field.Tick(0.1f);
                Assert.That(field.Bubbles.Count, Is.EqualTo(8));
            }

            Assert.That(field.PoppedCount, Is.EqualTo(0), "昇って消えたものは割った数に入らない");
        }

        [Test]
        public void 玉の上に当てると割れて数に入る()
        {
            BubbleField field = CreateField();
            Bubble target = field.Bubbles[0];
            List<Bubble> popped = new List<Bubble>();

            int count = field.PopAt(target.Position, popped);

            Assert.That(count >= 1, Is.True);
            Assert.That(popped.Contains(target), Is.True);
            Assert.That(field.Bubbles.Contains(target), Is.False);
            Assert.That(field.PoppedCount, Is.EqualTo(count));
        }

        [Test]
        public void 玉の縁から少しずれても割れる()
        {
            BubbleField field = CreateField(1);
            Bubble target = field.Bubbles[0];
            List<Bubble> popped = new List<Bubble>();
            Vector2 point = target.Position + new Vector2(target.Radius + BubbleField.HitMargin - 1f, 0f);

            Assert.That(field.PopAt(point, popped), Is.EqualTo(1));
        }

        [Test]
        public void 遠くに当てても割れない()
        {
            BubbleField field = CreateField(1);
            Bubble target = field.Bubbles[0];
            List<Bubble> popped = new List<Bubble>();
            Vector2 point = target.Position + new Vector2(target.Radius + BubbleField.HitMargin + 5f, 0f);

            Assert.That(field.PopAt(point, popped), Is.EqualTo(0));
            Assert.That(field.Bubbles.Count, Is.EqualTo(1));
        }

        [Test]
        public void 割ったぶんは下から補われる()
        {
            BubbleField field = CreateField(5);
            List<Bubble> popped = new List<Bubble>();
            field.PopAt(field.Bubbles[0].Position, popped);

            field.Tick(0.016f);

            Assert.That(field.Bubbles.Count, Is.EqualTo(5));
        }

        [Test]
        public void 重なった玉でも1回で割れるのは上限まで()
        {
            BubbleField field = CreateField(BubbleField.DefaultCount);
            List<Bubble> popped = new List<Bubble>();

            foreach (Bubble bubble in field.Bubbles)
            {
                bubble.Position = new Vector2(960f, 540f);
            }

            Assert.That(field.PopAt(new Vector2(960f, 540f), popped), Is.EqualTo(BubbleField.MaxPopsPerHit));
        }

        [Test]
        public void お祝いで割った玉は数に入らない()
        {
            BubbleField field = CreateField();
            foreach (Bubble bubble in field.Bubbles)
            {
                bubble.Position = new Vector2(960f, 540f);
            }

            Bubble popped;
            Assert.That(field.PopRandomVisible(out popped), Is.True);
            Assert.That(popped != null, Is.True);
            Assert.That(field.PoppedCount, Is.EqualTo(0));
        }
    }
}
