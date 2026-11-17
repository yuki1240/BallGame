using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// 背景の切替のテスト。
    /// </summary>
    public sealed class BackgroundRotationTests
    {
        [Test]
        public void 初期の切替間隔は5分()
        {
            BackgroundRotation rotation = new BackgroundRotation(3, 1);

            Assert.That(rotation.IntervalSeconds, Is.EqualTo(300f));
            Assert.That(rotation.CurrentIndex, Is.EqualTo(0));
        }

        [Test]
        public void 切替間隔は60秒から1時間に収まる()
        {
            BackgroundRotation rotation = new BackgroundRotation(3, 1);

            Assert.That(rotation.SetInterval(10f), Is.EqualTo(60f));
            Assert.That(rotation.SetInterval(99999f), Is.EqualTo(3600f));
        }

        [Test]
        public void 間隔が来たら名前順で次へ切り替わり最後の次は最初()
        {
            BackgroundRotation rotation = new BackgroundRotation(3, 1);
            rotation.SetInterval(60f);

            Assert.That(rotation.Tick(59f), Is.False);
            Assert.That(rotation.Tick(1f), Is.True);
            Assert.That(rotation.CurrentIndex, Is.EqualTo(1));
            rotation.Tick(60f);
            rotation.Tick(60f);
            Assert.That(rotation.CurrentIndex, Is.EqualTo(0));
        }

        [Test]
        public void 固定のときは切り替わらない()
        {
            BackgroundRotation rotation = new BackgroundRotation(3, 1) { IsFixed = true };

            Assert.That(rotation.Tick(10000f), Is.False);
            Assert.That(rotation.CurrentIndex, Is.EqualTo(0));
        }

        [Test]
        public void ランダムのときは今と違う背景になる()
        {
            BackgroundRotation rotation = new BackgroundRotation(4, 7) { IsRandom = true };
            for (int i = 0; i < 50; i++)
            {
                int before = rotation.CurrentIndex;
                rotation.Tick(rotation.IntervalSeconds);
                Assert.That(rotation.CurrentIndex, Is.Not.EqualTo(before));
                Assert.That(rotation.CurrentIndex, Is.InRange(0, 3));
            }
        }

        [Test]
        public void 手動で選ぶと前後に回りタイマーが戻る()
        {
            BackgroundRotation rotation = new BackgroundRotation(3, 1);
            rotation.Tick(200f);
            rotation.SelectPrevious();

            Assert.That(rotation.CurrentIndex, Is.EqualTo(2));
            Assert.That(rotation.RemainingSeconds, Is.EqualTo(300f));
            rotation.SelectNext();
            Assert.That(rotation.CurrentIndex, Is.EqualTo(0));
        }

        [Test]
        public void 背景が1枚なら切り替えない()
        {
            BackgroundRotation rotation = new BackgroundRotation(1, 1) { IsRandom = true };

            Assert.That(rotation.Tick(10000f), Is.False);
            Assert.That(rotation.PickNextIndex(), Is.EqualTo(0));
        }

        [Test]
        public void 背景画像は名前順に並ぶ()
        {
            List<Texture2D> textures = new List<Texture2D>
            {
                CreateTexture("bg_c"),
                CreateTexture("BG_a"),
                null,
                CreateTexture("bg_b")
            };

            List<Texture2D> sorted = BackgroundLibrary.SortByName(textures);

            Assert.That(sorted.ConvertAll(t => t.name), Is.EqualTo(new[] { "BG_a", "bg_b", "bg_c" }));
            sorted.ForEach(texture => Object.DestroyImmediate(texture));
        }

        private static Texture2D CreateTexture(string name)
        {
            return new Texture2D(2, 2) { name = name };
        }
    }
}
