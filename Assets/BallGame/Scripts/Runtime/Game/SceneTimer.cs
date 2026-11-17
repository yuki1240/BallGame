using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 1つの場面に居られる時間（既定は10分）を数えます。
    /// 時間になったら次の場面へ移ります。ただし、最後の1分のあいだにボールが当たったときは、
    /// 終わりを1分だけ先へ延ばします（遊んでいる途中で急に切り替わらないようにするため）。
    /// 終わりの10秒前からは、カウントダウンの数を返します。
    /// </summary>
    public sealed class SceneTimer
    {
        /// <summary>場面に居られる時間の既定値（秒）。10分です。</summary>
        public const float DefaultDurationSeconds = 600f;

        /// <summary>「最後の1分」の長さの既定値（秒）。この間に当たると終わりが延びます。</summary>
        public const float DefaultExtendWindowSeconds = 60f;

        /// <summary>1回で延びる時間の既定値（秒）。</summary>
        public const float DefaultExtendSeconds = 60f;

        /// <summary>カウントダウンを始める残り秒数の既定値。</summary>
        public const int DefaultCountdownFrom = 10;

        private readonly float extendWindowSeconds;
        private readonly float extendSeconds;
        private readonly int countdownFrom;
        private float remainingSeconds;
        private int extendCount;

        /// <summary>
        /// 場面に居られる時間を決めて作ります。
        /// </summary>
        /// <param name="durationSeconds">場面に居られる時間（秒）</param>
        /// <param name="extendWindowSeconds">終わりの何秒前から、当たると延びるか</param>
        /// <param name="extendSeconds">1回で延びる秒数</param>
        /// <param name="countdownFrom">終わりの何秒前からカウントダウンを出すか</param>
        public SceneTimer(
            float durationSeconds = DefaultDurationSeconds,
            float extendWindowSeconds = DefaultExtendWindowSeconds,
            float extendSeconds = DefaultExtendSeconds,
            int countdownFrom = DefaultCountdownFrom)
        {
            remainingSeconds = Mathf.Max(0f, durationSeconds);
            this.extendWindowSeconds = extendWindowSeconds;
            this.extendSeconds = extendSeconds;
            this.countdownFrom = countdownFrom;
        }

        /// <summary>残りの秒数。</summary>
        public float RemainingSeconds
        {
            get { return remainingSeconds; }
        }

        /// <summary>時間になったか。</summary>
        public bool IsExpired
        {
            get { return remainingSeconds <= 0f; }
        }

        /// <summary>これまでに終わりを延ばした回数。</summary>
        public int ExtendCount
        {
            get { return extendCount; }
        }

        /// <summary>
        /// 時間を進めます。
        /// </summary>
        /// <param name="deltaSeconds">進める秒</param>
        public void Tick(float deltaSeconds)
        {
            remainingSeconds = Mathf.Max(0f, remainingSeconds - deltaSeconds);
        }

        /// <summary>
        /// ボールが当たったことを知らせます。残りが最後の1分以内なら、終わりを延ばします。
        /// </summary>
        /// <returns>終わりを延ばしたとき true</returns>
        public bool NotifyHit()
        {
            if (IsExpired || remainingSeconds > extendWindowSeconds)
            {
                return false;
            }

            remainingSeconds += extendSeconds;
            extendCount++;
            return true;
        }

        /// <summary>
        /// カウントダウンで今出す数（10, 9, ... 1）を返します。まだ始まっていないとき、または時間になったあとは 0 です。
        /// </summary>
        /// <returns>出す数</returns>
        public int GetCountdownNumber()
        {
            if (IsExpired || remainingSeconds > countdownFrom)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.CeilToInt(remainingSeconds), 1, countdownFrom);
        }

        /// <summary>
        /// 残りの時間を「9:05」のような分と秒の文字にします（操作パネルの表示用）。
        /// </summary>
        /// <returns>分:秒の文字</returns>
        public string FormatRemaining()
        {
            int total = Mathf.CeilToInt(remainingSeconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }
    }
}
