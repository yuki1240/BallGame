using UnityEngine;

namespace WallBurst.Effects
{
    /// <summary>
    /// 演出が終わらなかった場合でも、期限でオブジェクトを破棄する。
    /// </summary>
    public sealed class EffectAutoDestroy : MonoBehaviour
    {
        private float _deadline;
        private bool _armed;

        /// <summary>
        /// この秒数が過ぎたら破棄する。時間はスケールの影響を受けない。
        /// </summary>
        /// <param name="lifeSeconds">寿命。</param>
        public void Arm(float lifeSeconds)
        {
            if (lifeSeconds < 0.05f)
            {
                lifeSeconds = 0.05f;
            }

            _deadline = Time.unscaledTime + lifeSeconds;
            _armed = true;
        }

        /// <summary>
        /// 期限を過ぎていたら破棄する。
        /// </summary>
        private void Update()
        {
            if (!_armed)
            {
                return;
            }

            if (Time.unscaledTime < _deadline)
            {
                return;
            }

            Destroy(gameObject);
        }
    }
}
