using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace BallGame.Tests
{
    /// <summary>
    /// 場面をランダムに替える決まりのテスト。
    /// </summary>
    public sealed class SceneRotationTests
    {
        [Test]
        public void 次の場面は今の場面と同じにならない()
        {
            Random random = new Random(1);
            string[] scenes = { SceneRotation.ChristmasScene, SceneRotation.MeadowScene, SceneRotation.AutumnScene, SceneRotation.BubbleScene };
            foreach (string current in scenes)
            {
                for (int i = 0; i < 200; i++)
                {
                    Assert.That(SceneRotation.PickNext(current, random), Is.Not.EqualTo(current));
                }
            }
        }

        [Test]
        public void 残りの3つの場面がどちらも選ばれる()
        {
            Random random = new Random(2);
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                seen.Add(SceneRotation.PickNext(SceneRotation.MeadowScene, random));
            }

            Assert.That(seen.Count, Is.EqualTo(3));
            Assert.That(seen.Contains(SceneRotation.BubbleScene), Is.True);
            Assert.That(seen.Contains(SceneRotation.ChristmasScene), Is.True);
            Assert.That(seen.Contains(SceneRotation.AutumnScene), Is.True);
        }

        [Test]
        public void 子ども向けの文は場面の名前が入る()
        {
            Assert.That(SceneRotation.GetKidsTitle(SceneRotation.MeadowScene), Is.EqualTo("つぎは おはなばたけ！"));
            Assert.That(SceneRotation.GetKidsTitle(SceneRotation.AutumnScene), Is.EqualTo("つぎは もみじの やま！"));
            Assert.That(SceneRotation.GetKidsTitle(SceneRotation.ChristmasScene), Is.EqualTo("つぎは クリスマス！"));
            Assert.That(SceneRotation.GetKidsTitle(SceneRotation.BubbleScene), Is.EqualTo("つぎは シャボンだま！"));
        }
    }
}
