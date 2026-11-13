using NUnit.Framework;
using WallBurst.Session;

namespace WallBurst.Tests
{
    /// <summary>
    /// 背景を切り替える時刻と順番。
    /// </summary>
    public sealed class BackgroundPlaylistTests
    {
        /// <summary>
        /// 5分経つまでは同じ画像のまま。
        /// </summary>
        [Test]
        public void TryAdvance_WaitsUntilInterval()
        {
            BackgroundPlaylist playlist = new BackgroundPlaylist();
            playlist.Arm(0f, 3);

            Assert.IsFalse(playlist.TryAdvance(299f, 3, AlwaysZero));
            Assert.AreEqual(0, playlist.CurrentIndex);
            Assert.IsTrue(playlist.TryAdvance(300f, 3, AlwaysZero));
            Assert.AreEqual(1, playlist.CurrentIndex);
        }

        /// <summary>
        /// ランダムを切ると、名前順の次へ進む。
        /// </summary>
        [Test]
        public void TryAdvance_UsesOrderWhenRandomIsOff()
        {
            BackgroundPlaylist playlist = new BackgroundPlaylist();
            playlist.SetRandomOrder(false);
            playlist.Arm(0f, 3);

            Assert.IsTrue(playlist.TryAdvance(300f, 3, AlwaysZero));
            Assert.AreEqual(1, playlist.CurrentIndex);
            Assert.IsTrue(playlist.TryAdvance(600f, 3, AlwaysZero));
            Assert.AreEqual(2, playlist.CurrentIndex);
            Assert.IsTrue(playlist.TryAdvance(900f, 3, AlwaysZero));
            Assert.AreEqual(0, playlist.CurrentIndex);
        }

        /// <summary>
        /// 乱数がいまの画像を指しても、同じ絵は続けない。
        /// </summary>
        [Test]
        public void TryAdvance_SkipsRepeatingTheCurrentImage()
        {
            BackgroundPlaylist playlist = new BackgroundPlaylist();
            playlist.Arm(0f, 4);

            Assert.IsTrue(playlist.TryAdvance(300f, 4, AlwaysZero));
            Assert.AreEqual(1, playlist.CurrentIndex);
        }

        /// <summary>
        /// 間隔を変えたら、その場から数え直す。
        /// </summary>
        [Test]
        public void SetInterval_RestartsTheCountdown()
        {
            BackgroundPlaylist playlist = new BackgroundPlaylist();
            playlist.Arm(0f, 2);
            playlist.SetInterval(60f, 10f);

            Assert.IsFalse(playlist.TryAdvance(69f, 2, AlwaysZero));
            Assert.IsTrue(playlist.TryAdvance(70f, 2, AlwaysZero));
        }

        /// <summary>
        /// 固定中は時間が来ても画像を変えない。
        /// </summary>
        [Test]
        public void TryAdvance_StaysWhileTheImageIsHeld()
        {
            BackgroundPlaylist playlist = new BackgroundPlaylist();
            playlist.Arm(0f, 3);
            playlist.SetHoldImage(true, 0f);

            Assert.IsFalse(playlist.TryAdvance(1000f, 3, AlwaysZero));
            Assert.AreEqual(0, playlist.CurrentIndex);
        }

        /// <summary>
        /// リストで選んだ番号が、いまの画像になる。
        /// </summary>
        [Test]
        public void Select_UsesTheChosenIndex()
        {
            BackgroundPlaylist playlist = new BackgroundPlaylist();
            playlist.Arm(0f, 4);
            playlist.Select(2, 4, 10f);

            Assert.AreEqual(2, playlist.CurrentIndex);
            Assert.IsFalse(playlist.TryAdvance(20f, 4, AlwaysZero));
        }

        /// <summary>
        /// 固定したあと別の画像を選んでも、その画像で止まる。
        /// </summary>
        [Test]
        public void Select_KeepsTheNewImageWhileHeld()
        {
            BackgroundPlaylist playlist = new BackgroundPlaylist();
            playlist.Arm(0f, 3);
            playlist.SetHoldImage(true, 0f);
            playlist.Select(1, 3, 5f);

            Assert.AreEqual(1, playlist.CurrentIndex);
            Assert.IsFalse(playlist.TryAdvance(9999f, 3, AlwaysZero));
        }

        /// <summary>
        /// 一枚しか無いときは切り替えない。
        /// </summary>
        [Test]
        public void TryAdvance_StaysWhenOnlyOneImage()
        {
            BackgroundPlaylist playlist = new BackgroundPlaylist();
            playlist.Arm(0f, 1);

            Assert.IsFalse(playlist.TryAdvance(10000f, 1, AlwaysZero));
            Assert.AreEqual(0, playlist.CurrentIndex);
        }

        /// <summary>
        /// 1分未満や60分超は、画面の範囲へ収める。
        /// </summary>
        [Test]
        public void SetInterval_ClampsToTheSliderRange()
        {
            BackgroundPlaylist playlist = new BackgroundPlaylist();
            playlist.SetInterval(1f, 0f);
            Assert.AreEqual(BackgroundPlaylist.MinIntervalSeconds, playlist.IntervalSeconds);

            playlist.SetInterval(99999f, 0f);
            Assert.AreEqual(BackgroundPlaylist.MaxIntervalSeconds, playlist.IntervalSeconds);
        }

        /// <summary>
        /// テスト用に、常に 0 を返す。
        /// </summary>
        /// <param name="count">枚数。使わない。</param>
        /// <returns>0。</returns>
        private static int AlwaysZero(int count)
        {
            return 0;
        }
    }
}
