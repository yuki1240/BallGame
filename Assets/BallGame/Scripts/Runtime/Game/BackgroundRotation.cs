using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 背景の切替を管理します。固定・ランダム・切替間隔（60秒〜1時間、初期5分）を扱います。
    /// </summary>
    public sealed class BackgroundRotation
    {
        /// <summary>切替間隔の下限（秒）。</summary>
        public const float MinIntervalSeconds = 60f;

        /// <summary>切替間隔の上限（秒）。</summary>
        public const float MaxIntervalSeconds = 3600f;

        /// <summary>切替間隔の初期値（秒）。</summary>
        public const float DefaultIntervalSeconds = 300f;

        private readonly System.Random random;
        private int count;
        private int currentIndex;
        private float intervalSeconds = DefaultIntervalSeconds;
        private float elapsedSeconds;

        /// <summary>
        /// 切替を作ります。
        /// </summary>
        /// <param name="count">背景の枚数</param>
        /// <param name="seed">ランダム用の種</param>
        public BackgroundRotation(int count, int seed = 0)
        {
            random = seed == 0 ? new System.Random() : new System.Random(seed);
            SetCount(count);
        }

        /// <summary>背景の枚数。</summary>
        public int Count
        {
            get { return count; }
        }

        /// <summary>今の背景の番号。</summary>
        public int CurrentIndex
        {
            get { return currentIndex; }
        }

        /// <summary>切替時にランダムに選ぶか（オフなら名前順に次へ）。</summary>
        public bool IsRandom { get; set; }

        /// <summary>固定（自動で切り替えない）か。</summary>
        public bool IsFixed { get; set; }

        /// <summary>切替間隔（秒）。</summary>
        public float IntervalSeconds
        {
            get { return intervalSeconds; }
        }

        /// <summary>次の切替までの残り秒数。</summary>
        public float RemainingSeconds
        {
            get { return Mathf.Max(0f, intervalSeconds - elapsedSeconds); }
        }

        /// <summary>
        /// 背景の枚数を変えます。今の番号は範囲内に収めます。
        /// </summary>
        /// <param name="newCount">枚数</param>
        public void SetCount(int newCount)
        {
            count = Mathf.Max(0, newCount);
            currentIndex = count == 0 ? 0 : Mathf.Clamp(currentIndex, 0, count - 1);
        }

        /// <summary>
        /// 背景を選びます。タイマーは最初からになります。
        /// </summary>
        /// <param name="index">番号</param>
        public void Select(int index)
        {
            if (count == 0)
            {
                return;
            }

            currentIndex = Wrap(index);
            elapsedSeconds = 0f;
        }

        /// <summary>
        /// 名前順で次の背景を選びます。
        /// </summary>
        public void SelectNext()
        {
            Select(currentIndex + 1);
        }

        /// <summary>
        /// 名前順で前の背景を選びます。
        /// </summary>
        public void SelectPrevious()
        {
            Select(currentIndex - 1);
        }

        /// <summary>
        /// 切替間隔を 60秒〜1時間に収めて設定します。
        /// </summary>
        /// <param name="seconds">秒</param>
        /// <returns>実際に設定した秒</returns>
        public float SetInterval(float seconds)
        {
            intervalSeconds = Mathf.Clamp(seconds, MinIntervalSeconds, MaxIntervalSeconds);
            elapsedSeconds = Mathf.Min(elapsedSeconds, intervalSeconds);
            return intervalSeconds;
        }

        /// <summary>
        /// 時間を進めます。間隔に達したら背景を切り替えます（固定中は切り替えません）。
        /// </summary>
        /// <param name="deltaSeconds">進める秒</param>
        /// <returns>切り替えたとき true</returns>
        public bool Tick(float deltaSeconds)
        {
            if (IsFixed || count <= 1)
            {
                return false;
            }

            elapsedSeconds += deltaSeconds;
            if (elapsedSeconds < intervalSeconds)
            {
                return false;
            }

            Select(PickNextIndex());
            return true;
        }

        /// <summary>
        /// 次に切り替える番号を決めます。ランダムなら今と違う番号、そうでなければ名前順で次です。
        /// </summary>
        /// <returns>次の番号</returns>
        public int PickNextIndex()
        {
            if (count <= 1)
            {
                return currentIndex;
            }

            if (!IsRandom)
            {
                return Wrap(currentIndex + 1);
            }

            int next = random.Next(count - 1);
            return next >= currentIndex ? next + 1 : next;
        }

        private int Wrap(int index)
        {
            return ((index % count) + count) % count;
        }
    }
}
