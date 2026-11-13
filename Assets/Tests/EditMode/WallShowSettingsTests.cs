using System.Collections.Generic;
using NUnit.Framework;
using WallBurst.Effects;

namespace WallBurst.Tests
{
    /// <summary>
    /// 壁の上下で花と演出を分ける判定。
    /// </summary>
    public sealed class WallShowSettingsTests
    {
        [Test]
        public void IsLowerHit_SplitsAtTheGivenHeight()
        {
            Assert.IsTrue(WallShowSettings.IsLowerHit(0f, 0.5f));
            Assert.IsTrue(WallShowSettings.IsLowerHit(0.49f, 0.5f));
            Assert.IsFalse(WallShowSettings.IsLowerHit(0.5f, 0.5f));
            Assert.IsFalse(WallShowSettings.IsLowerHit(1f, 0.5f));
        }

        [Test]
        public void ChooseEffect_UsesOnlyTheAssignedKinds()
        {
            EffectKind[] assigned = { EffectKind.Ripples, EffectKind.Cross };
            var seen = new HashSet<EffectKind>();
            for (int i = 0; i < assigned.Length; i++)
            {
                int pick = i;
                seen.Add(WallShowSettings.ChooseEffect(assigned, 0, count => pick));
            }

            Assert.AreEqual(2, seen.Count);
            Assert.IsTrue(seen.Contains(EffectKind.Ripples));
            Assert.IsTrue(seen.Contains(EffectKind.Cross));
        }

        [Test]
        public void ChooseEffect_EmptyListFallsBackToTheImageSet()
        {
            EffectKind kind = WallShowSettings.ChooseEffect(null, 1, count => 0);

            Assert.AreEqual(EffectKind.Pulse, kind);
        }
    }
}
