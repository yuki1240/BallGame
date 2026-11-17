using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 飾りを、ボールが当たった所からツリーの決まった場所まで弧を描いて飛ばし、
    /// 着いたら「ぽよん」と弾ませます。着いたあとはツリーに付いたまま残ります。
    /// </summary>
    public sealed class OrnamentFlight : MonoBehaviour
    {
        /// <summary>飛ぶ秒数。</summary>
        public const float FlightSeconds = 0.8f;

        /// <summary>弾む秒数。</summary>
        public const float BounceSeconds = 0.35f;

        private Vector2 start;
        private Vector2 end;
        private Vector2 control;
        private float elapsed;
        private bool finished;

        /// <summary>着いたか。</summary>
        public bool IsFinished
        {
            get { return finished; }
        }

        /// <summary>
        /// 飛ばし始めます。
        /// </summary>
        /// <param name="from">出発点（Canvas 上の位置）</param>
        /// <param name="to">着く場所（Canvas 上の位置）</param>
        public void Fly(Vector2 from, Vector2 to)
        {
            start = from;
            end = to;
            control = (from + to) * 0.5f + new Vector2(0f, 260f);
            elapsed = 0f;
            finished = false;
            Apply(0f);
        }

        /// <summary>
        /// 飛んでいる途中の位置を返します（2次ベジェ曲線）。
        /// </summary>
        /// <param name="from">出発点</param>
        /// <param name="via">弧の頂点の目安</param>
        /// <param name="to">着く場所</param>
        /// <param name="t">0〜1</param>
        /// <returns>位置</returns>
        public static Vector2 EvaluateArc(Vector2 from, Vector2 via, Vector2 to, float t)
        {
            float u = 1f - t;
            return from * (u * u) + via * (2f * u * t) + to * (t * t);
        }

        private void Update()
        {
            if (finished)
            {
                return;
            }

            elapsed += Time.deltaTime;
            Apply(elapsed);
        }

        private void Apply(float time)
        {
            RectTransform rect = (RectTransform)transform;
            if (time < FlightSeconds)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / FlightSeconds);
                rect.anchoredPosition = EvaluateArc(start, control, end, t);
                rect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t);
                return;
            }

            rect.anchoredPosition = end;
            float bounce = Mathf.Clamp01((time - FlightSeconds) / BounceSeconds);
            rect.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(bounce * Mathf.PI));
            finished = bounce >= 1f;
        }
    }
}
