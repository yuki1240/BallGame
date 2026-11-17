using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 3D の飾りを、ボールが当たった場所からツリーの上まで山なりに飛ばし、着いたら「ぽよん」と弾ませます。
    /// 終わったら自分（この部品）を消します。
    /// </summary>
    public sealed class OrnamentFlight3D : MonoBehaviour
    {
        /// <summary>飛んでいる時間（秒）。</summary>
        public const float FlightSeconds = 0.75f;

        /// <summary>着いてから弾む時間（秒）。</summary>
        public const float BounceSeconds = 0.3f;

        private Vector3 from;
        private Vector3 to;
        private Vector3 finalScale;
        private float arcHeight;
        private float elapsed;

        /// <summary>
        /// 飛ばし始めます。
        /// </summary>
        /// <param name="start">飛び始める場所（ワールド座標）</param>
        /// <param name="target">着く場所（ワールド座標）</param>
        /// <param name="scale">着いたときの大きさ</param>
        /// <param name="arc">山なりの高さ</param>
        public void Fly(Vector3 start, Vector3 target, Vector3 scale, float arc)
        {
            from = start;
            to = target;
            finalScale = scale;
            arcHeight = arc;
            elapsed = 0f;
            transform.position = start;
            transform.localScale = scale * 0.4f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed < FlightSeconds)
            {
                MoveAlongArc(elapsed / FlightSeconds);
                return;
            }

            Bounce((elapsed - FlightSeconds) / BounceSeconds);
        }

        private void MoveAlongArc(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            transform.position = Vector3.Lerp(from, to, eased) + Vector3.up * (arcHeight * 4f * t * (1f - t));
            transform.localScale = finalScale * Mathf.Lerp(0.4f, 1f, eased);
        }

        private void Bounce(float t)
        {
            transform.position = to;
            if (t >= 1f)
            {
                transform.localScale = finalScale;
                Destroy(this);
                return;
            }

            transform.localScale = finalScale * (1f + 0.25f * Mathf.Sin(Mathf.PI * t));
        }
    }
}
