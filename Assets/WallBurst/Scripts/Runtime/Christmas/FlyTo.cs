using System;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 物を、弧をえがいて目的の位置まで飛ばします。着いたらこの部品だけ消え、物は残ります。
    /// </summary>
    public sealed class FlyTo : MonoBehaviour
    {
        private Vector3 from;
        private Vector3 to;
        private Vector3 startScale;
        private Vector3 endScale;
        private float arcHeight;
        private float seconds;
        private float elapsed;
        private Action arrived;

        /// <summary>
        /// 飛ばし始めます。
        /// </summary>
        /// <param name="target">飛ばす物</param>
        /// <param name="destination">着く位置</param>
        /// <param name="finalScale">着いたときの大きさ</param>
        /// <param name="arc">弧の高さ</param>
        /// <param name="duration">かかる秒数</param>
        /// <param name="onArrived">着いたときの処理（無くてもよい）</param>
        public static void Launch(GameObject target, Vector3 destination, Vector3 finalScale, float arc, float duration, Action onArrived)
        {
            FlyTo flight = target.AddComponent<FlyTo>();
            flight.from = target.transform.position;
            flight.to = destination;
            flight.endScale = finalScale;
            flight.startScale = finalScale * 1.8f;
            flight.arcHeight = arc;
            flight.seconds = Mathf.Max(0.05f, duration);
            flight.arrived = onArrived;
            target.transform.localScale = flight.startScale;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float ease = t * t * (3f - 2f * t);
            Vector3 position = Vector3.Lerp(from, to, ease);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.position = position;
            transform.localScale = Vector3.Lerp(startScale, endScale, ease);
            if (t < 1f)
            {
                return;
            }

            Action callback = arrived;
            arrived = null;
            Destroy(this);
            if (callback != null)
            {
                callback();
            }
        }
    }
}
