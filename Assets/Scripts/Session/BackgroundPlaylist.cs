using System;

namespace WallBurst.Session
{
    /// <summary>
    /// 背景をいつ、どの順で次へ進めるか。画像そのものは持たない。
    /// 起動中のメモリだけに残り、ファイルへは書かない。
    /// </summary>
    public sealed class BackgroundPlaylist
    {
        /// <summary>初期の切替間隔。5分。</summary>
        public const float DefaultIntervalSeconds = 300f;

        /// <summary>画面から設定できる最短。</summary>
        public const float MinIntervalSeconds = 60f;

        /// <summary>画面から設定できる最長。</summary>
        public const float MaxIntervalSeconds = 3600f;

        private float _intervalSeconds = DefaultIntervalSeconds;
        private bool _randomOrder = true;
        private bool _holdImage;
        private int _currentIndex;
        private float _nextSwitchTime = -1f;

        /// <summary>次の画像までの秒。</summary>
        public float IntervalSeconds
        {
            get { return _intervalSeconds; }
        }

        /// <summary>順不同で次を選ぶなら true。false なら名前順に一周する。</summary>
        public bool RandomOrder
        {
            get { return _randomOrder; }
        }

        /// <summary>選んだ画像で止めるなら true。</summary>
        public bool HoldImage
        {
            get { return _holdImage; }
        }

        /// <summary>いま出している画像の番号。</summary>
        public int CurrentIndex
        {
            get { return _currentIndex; }
        }

        /// <summary>次に切り替える時刻。未開始は 0 未満。</summary>
        public float NextSwitchTime
        {
            get { return _nextSwitchTime; }
        }

        /// <summary>
        /// 枚数に合わせて番号を整え、最初の切替時刻を決める。すでに進み始めていれば時刻は維持する。
        /// </summary>
        /// <param name="now">現在時刻（秒）。</param>
        /// <param name="count">画像の枚数。</param>
        public void Arm(float now, int count)
        {
            if (count <= 0)
            {
                _currentIndex = 0;
                return;
            }

            if (_currentIndex < 0 || _currentIndex >= count)
            {
                _currentIndex = 0;
            }

            if (_nextSwitchTime < 0f)
            {
                _nextSwitchTime = now + _intervalSeconds;
            }
        }

        /// <summary>
        /// 間隔を変える。すでに計時中なら、いまからその間隔で数え直す。
        /// </summary>
        /// <param name="seconds">秒。</param>
        /// <param name="now">現在時刻（秒）。</param>
        public void SetInterval(float seconds, float now)
        {
            _intervalSeconds = ClampInterval(seconds);
            if (_nextSwitchTime >= 0f)
            {
                _nextSwitchTime = now + _intervalSeconds;
            }
        }

        /// <summary>
        /// ランダム切替のオンオフを変える。いまの画像と残り時間はそのまま。
        /// </summary>
        /// <param name="randomOrder">順不同にするなら true。</param>
        public void SetRandomOrder(bool randomOrder)
        {
            _randomOrder = randomOrder;
        }

        /// <summary>
        /// リストで選んだ画像をいまの画像にする。固定中でなければ間隔を数え直す。
        /// </summary>
        /// <param name="index">画像の番号。</param>
        /// <param name="count">画像の枚数。</param>
        /// <param name="now">現在時刻（秒）。</param>
        public void Select(int index, int count, float now)
        {
            if (count <= 0)
            {
                _currentIndex = 0;
                return;
            }

            if (index < 0)
            {
                index = 0;
            }
            else if (index >= count)
            {
                index = count - 1;
            }

            _currentIndex = index;
            if (!_holdImage && _nextSwitchTime >= 0f)
            {
                _nextSwitchTime = now + _intervalSeconds;
            }
        }

        /// <summary>
        /// 固定のオンオフ。外したときは、いまから間隔を数え直す。
        /// </summary>
        /// <param name="holdImage">この画像で止めるなら true。</param>
        /// <param name="now">現在時刻（秒）。</param>
        public void SetHoldImage(bool holdImage, float now)
        {
            _holdImage = holdImage;
            if (!_holdImage && _nextSwitchTime >= 0f)
            {
                _nextSwitchTime = now + _intervalSeconds;
            }
        }

        /// <summary>
        /// 時刻が来ていれば次の画像へ進める。
        /// </summary>
        /// <param name="now">現在時刻（秒）。</param>
        /// <param name="count">画像の枚数。</param>
        /// <param name="roll">0 以上 count 未満の乱数を返す。</param>
        /// <returns>画像が変わったら true。</returns>
        public bool TryAdvance(float now, int count, Func<int, int> roll)
        {
            if (_holdImage || count <= 1 || _nextSwitchTime < 0f || now < _nextSwitchTime)
            {
                return false;
            }

            if (roll == null)
            {
                throw new ArgumentNullException(nameof(roll));
            }

            _currentIndex = _randomOrder
                ? NextRandomIndex(count, roll)
                : (_currentIndex + 1) % count;
            _nextSwitchTime = now + _intervalSeconds;
            return true;
        }

        /// <summary>
        /// いまの画像以外を選ぶ。乱数が同じ番号でも隣へずらす。
        /// </summary>
        /// <param name="count">画像の枚数。</param>
        /// <param name="roll">乱数。</param>
        /// <returns>次の番号。</returns>
        private int NextRandomIndex(int count, Func<int, int> roll)
        {
            int picked = roll(count);
            if (picked < 0)
            {
                picked = 0;
            }

            if (picked >= count)
            {
                picked = count - 1;
            }

            if (picked == _currentIndex)
            {
                picked = (picked + 1) % count;
            }

            return picked;
        }

        /// <summary>
        /// 画面の範囲へ秒数を収める。
        /// </summary>
        /// <param name="seconds">希望の秒。</param>
        /// <returns>60秒から60分。</returns>
        private static float ClampInterval(float seconds)
        {
            if (seconds < MinIntervalSeconds)
            {
                return MinIntervalSeconds;
            }

            if (seconds > MaxIntervalSeconds)
            {
                return MaxIntervalSeconds;
            }

            return seconds;
        }
    }
}
