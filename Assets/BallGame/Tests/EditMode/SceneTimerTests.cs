using NUnit.Framework;

namespace BallGame.Tests
{
    /// <summary>
    /// 場面の切り替え時間（10分・最後の1分に当たったら1分延ばす）のテスト。
    /// </summary>
    public sealed class SceneTimerTests
    {
        [Test]
        public void 始めは10分で時間切れにならない()
        {
            SceneTimer timer = new SceneTimer();

            Assert.That(timer.RemainingSeconds, Is.EqualTo(600f).Within(1e-3f));
            Assert.That(timer.IsExpired, Is.False);
            Assert.That(timer.GetCountdownNumber(), Is.EqualTo(0), "まだカウントダウンしない");
        }

        [Test]
        public void 十分たつと時間切れになる()
        {
            SceneTimer timer = new SceneTimer();
            timer.Tick(599f);
            Assert.That(timer.IsExpired, Is.False);

            timer.Tick(1f);
            Assert.That(timer.IsExpired, Is.True);
        }

        [Test]
        public void 最後の1分より前に当たっても延びない()
        {
            SceneTimer timer = new SceneTimer();
            timer.Tick(500f);

            Assert.That(timer.NotifyHit(), Is.False);
            Assert.That(timer.RemainingSeconds, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(timer.ExtendCount, Is.EqualTo(0));
        }

        [Test]
        public void 最後の1分に当たると終わりが1分延びる()
        {
            SceneTimer timer = new SceneTimer();
            timer.Tick(540f);

            Assert.That(timer.NotifyHit(), Is.True);
            Assert.That(timer.RemainingSeconds, Is.EqualTo(120f).Within(1e-3f));
            Assert.That(timer.ExtendCount, Is.EqualTo(1));
        }

        [Test]
        public void 延びたあとはまた最後の1分に入るまで延びない()
        {
            SceneTimer timer = new SceneTimer();
            timer.Tick(570f);
            Assert.That(timer.NotifyHit(), Is.True, "残り30秒で当たる");
            Assert.That(timer.RemainingSeconds, Is.EqualTo(90f).Within(1e-3f));

            Assert.That(timer.NotifyHit(), Is.False, "残り90秒なので延びない");

            timer.Tick(30f);
            Assert.That(timer.NotifyHit(), Is.True, "残り60秒でまた最後の1分");
            Assert.That(timer.RemainingSeconds, Is.EqualTo(120f).Within(1e-3f));
        }

        [Test]
        public void 時間切れのあとに当たっても延びない()
        {
            SceneTimer timer = new SceneTimer();
            timer.Tick(700f);

            Assert.That(timer.NotifyHit(), Is.False);
            Assert.That(timer.IsExpired, Is.True);
        }

        [Test]
        public void 終わりの10秒前からカウントダウンの数が出る()
        {
            SceneTimer timer = new SceneTimer();
            timer.Tick(589f);
            Assert.That(timer.GetCountdownNumber(), Is.EqualTo(0), "残り11秒");

            timer.Tick(1f);
            Assert.That(timer.GetCountdownNumber(), Is.EqualTo(10), "残り10秒");

            timer.Tick(4.5f);
            Assert.That(timer.GetCountdownNumber(), Is.EqualTo(6), "残り5.5秒");

            timer.Tick(5.2f);
            Assert.That(timer.GetCountdownNumber(), Is.EqualTo(1), "残り0.3秒");

            timer.Tick(1f);
            Assert.That(timer.GetCountdownNumber(), Is.EqualTo(0), "時間切れのあとは出さない");
        }

        [Test]
        public void カウントダウン中に当たると延びて数が消える()
        {
            SceneTimer timer = new SceneTimer();
            timer.Tick(594f);
            Assert.That(timer.GetCountdownNumber(), Is.EqualTo(6));

            timer.NotifyHit();

            Assert.That(timer.GetCountdownNumber(), Is.EqualTo(0));
            Assert.That(timer.IsExpired, Is.False);
        }

        [Test]
        public void 残り時間を分と秒の文字にできる()
        {
            SceneTimer timer = new SceneTimer();
            Assert.That(timer.FormatRemaining(), Is.EqualTo("10:00"));

            timer.Tick(535f);
            Assert.That(timer.FormatRemaining(), Is.EqualTo("1:05"));
        }
    }
}
