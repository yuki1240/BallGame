using NUnit.Framework;
using WallBurst.Effects;

namespace WallBurst.Tests
{
    /// <summary>
    /// 画像ごとの演出の組。
    /// </summary>
    public sealed class EffectSetsTests
    {
        /// <summary>
        /// 先頭の画像は 0、1、2 のどれか。
        /// </summary>
        [Test]
        public void Pick_FirstImageUsesFirstThree()
        {
            Assert.AreEqual(0, EffectSets.Pick(0, count => 0));
            Assert.AreEqual(2, EffectSets.Pick(0, count => 2));
        }

        /// <summary>
        /// 2枚目は 3、4、5。3枚目は 6、7、8。
        /// </summary>
        [Test]
        public void Pick_LaterImagesUseDifferentSets()
        {
            Assert.AreEqual(3, EffectSets.Pick(1, count => 0));
            Assert.AreEqual(5, EffectSets.Pick(1, count => 2));
            Assert.AreEqual(6, EffectSets.Pick(2, count => 0));
            Assert.AreEqual(8, EffectSets.Pick(2, count => 2));
        }

        /// <summary>
        /// 4枚目は先頭の組に戻る。
        /// </summary>
        [Test]
        public void Pick_FourthImageRepeatsFirstSet()
        {
            Assert.AreEqual(1, EffectSets.Pick(3, count => 1));
        }
    }
}
