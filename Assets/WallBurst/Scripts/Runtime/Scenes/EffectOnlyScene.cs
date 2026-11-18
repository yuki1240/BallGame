using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// エフェクトだけのシーン：ボールが当たった所にエフェクト（仮）が出るだけです。
    /// <para>終わり方：シーンが始まってから <see cref="DurationSeconds"/> 秒たつか、<see cref="HitLimit"/> 回当たるか、早い方で次のシーンへ進みます。
    /// 時間はシーンが始まってから数えるので、誰も遊んでいないときも一定の時間で替わります。</para>
    /// </summary>
    public sealed class EffectOnlyScene : GameSceneBase
    {
        /// <summary>このシーンを続ける秒数（3分）。</summary>
        public const float DurationSeconds = 180f;

        /// <summary>この回数当たったら次へ進む。</summary>
        public const int HitLimit = 30;

        /// <summary>最後の当たりのあと、エフェクトを見せてから替えるまでの秒数。</summary>
        private const float FinishDelaySeconds = 1.5f;

        private static readonly Color Background = new Color(0.03f, 0.03f, 0.07f, 1f);

        private EffectSpawner effects;
        private float finishAt = -1f;

        /// <inheritdoc/>
        protected override void BuildScene()
        {
            CreateCamera(Background);
            effects = EffectSpawner.Create(10);
        }

        /// <inheritdoc/>
        protected override void OnWallHit(Vector2 screenPosition)
        {
            effects.Spawn(screenPosition);
            if (HitTotal >= HitLimit && finishAt < 0f)
            {
                finishAt = SceneSeconds + FinishDelaySeconds;
            }
        }

        /// <inheritdoc/>
        protected override void Update()
        {
            base.Update();
            if (SceneSeconds >= DurationSeconds || (finishAt >= 0f && SceneSeconds >= finishAt))
            {
                RequestNext();
            }
        }

        /// <inheritdoc/>
        protected override string DescribeState()
        {
            int remaining = Mathf.Max(0, Mathf.CeilToInt(DurationSeconds - SceneSeconds));
            return "エフェクトだけ：あと " + remaining + " 秒 か あと " + Mathf.Max(0, HitLimit - HitTotal) + " 回で次へ　エフェクト：" + (effects != null ? effects.ActiveCount : 0) + " 個";
        }
    }
}
