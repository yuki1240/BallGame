using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// シャボン玉割りの1回の遊び（15個・15秒・結果5秒・15分のくり返し）のテスト。
    /// </summary>
    public sealed class BubbleGameTests
    {
        private const float Width = 1920f;
        private const float Height = 1080f;

        private static BubbleGame CreateGame(float sceneSeconds = BubbleGame.DefaultSceneSeconds)
        {
            return new BubbleGame(Width, Height, new System.Random(5), sceneSeconds);
        }

        private static void Run(BubbleGame game, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                game.Tick(0.05f);
                elapsed += 0.05f;
            }
        }

        private static int PopAll(BubbleGame game, int limit)
        {
            List<Bubble> popped = new List<Bubble>();
            int total = 0;
            for (int i = 0; i < limit && game.Field.Bubbles.Count > 0 && game.Phase == BubblePhase.Playing; i++)
            {
                Vector2 target = game.Field.Bubbles[0].Position;
                total += game.PopAt(target, popped);
            }

            return total;
        }

        [Test]
        public void 最初は15個浮かんでいて遊んでいる()
        {
            BubbleGame game = CreateGame();

            Assert.That(game.Field.Bubbles.Count, Is.EqualTo(15));
            Assert.That(game.Phase, Is.EqualTo(BubblePhase.Playing));
            Assert.That(game.RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void 割らなくても玉の数は増えず減らない()
        {
            BubbleGame game = CreateGame();

            Run(game, 14f);

            Assert.That(game.Field.Bubbles.Count, Is.EqualTo(15));
            Assert.That(game.PoppedCount, Is.EqualTo(0));
        }

        [Test]
        public void 割った玉は補われない()
        {
            BubbleGame game = CreateGame();
            List<Bubble> popped = new List<Bubble>();
            int count = game.PopAt(game.Field.Bubbles[0].Position, popped);

            Run(game, 1f);

            Assert.That(count >= 1, Is.True);
            Assert.That(game.Field.Bubbles.Count, Is.EqualTo(15 - game.PoppedCount));
        }

        [Test]
        public void 制限の15秒がたつと結果になる()
        {
            BubbleGame game = CreateGame();

            Run(game, 14.5f);
            Assert.That(game.Phase, Is.EqualTo(BubblePhase.Playing));
            Run(game, 1f);

            Assert.That(game.Phase, Is.EqualTo(BubblePhase.Result));
        }

        [Test]
        public void 全部割れたら15秒を待たずに結果になる()
        {
            BubbleGame game = CreateGame();

            PopAll(game, 100);

            Assert.That(game.PoppedCount, Is.EqualTo(15));
            Assert.That(game.Phase, Is.EqualTo(BubblePhase.Result));
        }

        [Test]
        public void 結果のあいだは玉を割れない()
        {
            BubbleGame game = CreateGame();
            Run(game, 15.5f);
            List<Bubble> popped = new List<Bubble>();

            int count = game.PopAt(game.Field.Bubbles[0].Position, popped);

            Assert.That(count, Is.EqualTo(0));
            Assert.That(game.PoppedCount, Is.EqualTo(0));
        }

        [Test]
        public void 結果を5秒見せたら次の回が始まる()
        {
            BubbleGame game = CreateGame();
            PopAll(game, 100);

            Run(game, 4.5f);
            Assert.That(game.Phase, Is.EqualTo(BubblePhase.Result));
            Run(game, 1f);

            Assert.That(game.Phase, Is.EqualTo(BubblePhase.Playing));
            Assert.That(game.RoundNumber, Is.EqualTo(2));
            Assert.That(game.PoppedCount, Is.EqualTo(0));
            Assert.That(game.Field.Bubbles.Count, Is.EqualTo(15));
        }

        [Test]
        public void 結果の文は割れた数ごとに決まる()
        {
            Assert.That(BubbleGame.GetResultMessage(15), Is.EqualTo("15こ われたよ！やったね！"));
            Assert.That(BubbleGame.GetResultMessage(14), Is.EqualTo("14こ われたよ！　おしかったね！"));
            Assert.That(BubbleGame.GetResultMessage(10), Is.EqualTo("10こ われたよ！　おしかったね！"));
            Assert.That(BubbleGame.GetResultMessage(9), Is.EqualTo("9こ われたよ！まだまだこれから！"));
            Assert.That(BubbleGame.GetResultMessage(1), Is.EqualTo("1こ われたよ！まだまだこれから！"));
            Assert.That(BubbleGame.GetResultMessage(0), Is.EqualTo("0こ だったよ！つぎはがんばろう！"));
        }

        [Test]
        public void 制限の15分がたつまでは回を繰り返し終わらない()
        {
            BubbleGame game = CreateGame();

            Run(game, 600f);

            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.RoundNumber > 20, Is.True);
        }

        [Test]
        public void 制限の15分を過ぎても遊んでいる回は結果まで終える()
        {
            // 1回は15秒＋結果5秒＝20秒。15分(900秒)の直前の回は、15分を過ぎても最後まで続ける
            BubbleGame game = CreateGame();
            Run(game, 895f);
            Assert.That(game.IsFinished, Is.False);
            int roundAtStart = game.RoundNumber;

            Run(game, 6f);
            Assert.That(game.SceneElapsedSeconds > 900f, Is.True);
            Assert.That(game.IsFinished, Is.False, "回の途中では終えない");

            Run(game, 30f);

            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.RoundNumber <= roundAtStart + 1, Is.True, "15分を過ぎてから新しい回は始めない");
        }

        [Test]
        public void 最後の回の結果の間は場面を終えると分かる()
        {
            BubbleGame game = CreateGame(60f);
            Run(game, 14f);
            PopAll(game, 100);
            Assert.That(game.WillFinishAfterResult, Is.False, "まだ時間があるので次の回がある");

            BubbleGame last = CreateGame(10f);
            Run(last, 16f);

            Assert.That(last.Phase, Is.EqualTo(BubblePhase.Result));
            Assert.That(last.WillFinishAfterResult, Is.True);
        }

        [Test]
        public void 割らなかった玉は上から出ても下へ戻る()
        {
            BubbleGame game = CreateGame();
            Run(game, 14f);

            foreach (Bubble bubble in game.Field.Bubbles)
            {
                Assert.That(bubble.Position.y > -400f && bubble.Position.y < Height + 400f, Is.True);
            }
        }
    }
}
