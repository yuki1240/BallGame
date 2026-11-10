using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WallBurst.Effects;

namespace WallBurst.Tests
{
    /// <summary>
    /// 演出オブジェクトが寿命で破棄されること。
    /// </summary>
    public sealed class EffectLifetimeTests
    {
        /// <summary>
        /// 武装した期限を過ぎるとオブジェクトが消える。
        /// </summary>
        [UnityTest]
        public IEnumerator EffectAutoDestroy_DestroysObjectAfterLifetime()
        {
            GameObject effect = new GameObject("effect");
            EffectAutoDestroy watchdog = effect.AddComponent<EffectAutoDestroy>();
            watchdog.Arm(0.15f);

            yield return new WaitForSeconds(0.45f);

            Assert.IsTrue(effect == null);
        }
    }
}
