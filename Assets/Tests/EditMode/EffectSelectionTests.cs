using NUnit.Framework;
using WallBurst.Effects;

namespace WallBurst.Tests
{
    /// <summary>
    /// 出す演出を複数選ぶ。
    /// </summary>
    public sealed class EffectSelectionTests
    {
        /// <summary>
        /// オンの種類だけが選ばれる。
        /// </summary>
        [Test]
        public void Pick_UsesOnlyEnabledKinds()
        {
            EffectSelection selection = new EffectSelection();
            Assert.IsTrue(selection.SetEnabled(0, false));
            Assert.IsTrue(selection.SetEnabled(1, false));
            Assert.IsTrue(selection.SetEnabled(3, false));

            int kind = selection.Pick(count => 0);

            Assert.AreEqual(1, selection.EnabledCount());
            Assert.AreEqual(2, kind);
        }

        /// <summary>
        /// 最後の1つはオフにできない。
        /// </summary>
        [Test]
        public void SetEnabled_KeepsTheLastKind()
        {
            EffectSelection selection = new EffectSelection();
            selection.SetEnabled(0, false);
            selection.SetEnabled(1, false);
            selection.SetEnabled(2, false);

            bool changed = selection.SetEnabled(3, false);

            Assert.IsFalse(changed);
            Assert.IsTrue(selection.IsEnabled(3));
        }
    }
}
