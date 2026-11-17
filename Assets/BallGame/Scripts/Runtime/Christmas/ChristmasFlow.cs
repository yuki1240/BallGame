using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// クリスマスの場面の進み具合。
    /// </summary>
    public enum ChristmasPhase
    {
        /// <summary>ボールを当てて飾り付けをしている。</summary>
        Playing = 0,

        /// <summary>「クリスマスツリーの完成～！」を出している。</summary>
        Celebrating = 1,

        /// <summary>次の場面へのカウントダウン中。</summary>
        Countdown = 2,

        /// <summary>次の場面へ移ってよい。</summary>
        Finished = 3
    }

    /// <summary>
    /// クリスマスの場面の流れ（10回当てる → 完成のきらきらを3秒 → カウントダウン10秒 → 次の場面）を管理します。
    /// 画面に依存しないので、テストで確かめられます。
    /// </summary>
    public sealed class ChristmasFlow
    {
        /// <summary>ツリーが完成するまでに当てる回数（最後の1回で星が付く）。</summary>
        public const int DefaultGoal = 10;

        /// <summary>「完成～！」ときらきらを出しておく秒数（このあとカウントダウン）。</summary>
        public const float DefaultCelebrateSeconds = 3f;

        /// <summary>カウントダウンの始まりの数（秒）。</summary>
        public const int DefaultCountdownFrom = 10;

        /// <summary>当たりを数える間隔の下限（秒）。1秒に1個まで。</summary>
        public const float DefaultMinHitIntervalSeconds = 1f;

        private int goal;
        private readonly float celebrateSeconds;
        private readonly int countdownFrom;
        private readonly float minHitIntervalSeconds;
        private float lastCountedTime = float.NegativeInfinity;
        private ChristmasPhase phase = ChristmasPhase.Playing;
        private int hitCount;
        private float phaseElapsed;

        /// <summary>
        /// 流れを作ります。
        /// </summary>
        /// <param name="goal">完成までに当てる回数</param>
        /// <param name="celebrateSeconds">完成を出す秒数</param>
        /// <param name="countdownFrom">カウントダウンの始まりの数</param>
        /// <param name="minHitIntervalSeconds">当たりを数える間隔の下限（秒）</param>
        public ChristmasFlow(int goal = DefaultGoal, float celebrateSeconds = DefaultCelebrateSeconds, int countdownFrom = DefaultCountdownFrom, float minHitIntervalSeconds = DefaultMinHitIntervalSeconds)
        {
            this.minHitIntervalSeconds = Mathf.Max(0f, minHitIntervalSeconds);
            this.goal = Mathf.Max(1, goal);
            this.celebrateSeconds = Mathf.Max(0f, celebrateSeconds);
            this.countdownFrom = Mathf.Max(1, countdownFrom);
        }

        /// <summary>今の進み具合。</summary>
        public ChristmasPhase Phase
        {
            get { return phase; }
        }

        /// <summary>当てた回数（飾りの数）。</summary>
        public int HitCount
        {
            get { return hitCount; }
        }

        /// <summary>完成までに当てる回数。</summary>
        public int Goal
        {
            get { return goal; }
        }

        /// <summary>完成まであと何回か。</summary>
        public int Remaining
        {
            get { return goal - hitCount; }
        }

        /// <summary>最後の1個（星）を付ける番か。</summary>
        public bool IsNextFinal
        {
            get { return phase == ChristmasPhase.Playing && hitCount == goal - 1; }
        }

        /// <summary>
        /// 当たったことを知らせます。飾り付け中で、前に数えてから1秒たっているときだけ数えます
        /// （弾んだボールで何度も数えないため）。
        /// </summary>
        /// <param name="timeSeconds">今の時刻（秒）</param>
        /// <returns>数えたとき true（新しい飾りを付ける）</returns>
        public bool RegisterHit(float timeSeconds)
        {
            if (phase != ChristmasPhase.Playing || timeSeconds - lastCountedTime < minHitIntervalSeconds)
            {
                return false;
            }

            lastCountedTime = timeSeconds;
            hitCount++;
            if (hitCount >= goal)
            {
                ChangePhase(ChristmasPhase.Celebrating);
            }

            return true;
        }

        /// <summary>
        /// 完成までに当てる回数を変えます（固定モードの入り切りで使う）。
        /// すでに当てた回数より少なくはならず、飾り付け中でなければ何もしません。
        /// </summary>
        /// <param name="newGoal">新しい回数</param>
        public void SetGoal(int newGoal)
        {
            if (phase == ChristmasPhase.Playing)
            {
                goal = Mathf.Max(newGoal, hitCount + 1);
            }
        }

        /// <summary>
        /// 回数ではなく別の条件（山が全部紅葉した、など）で完成したときに、お祝いへ進めます。
        /// 飾り付け中でなければ何もしません。
        /// </summary>
        /// <returns>お祝いへ進んだとき true</returns>
        public bool Complete()
        {
            if (phase != ChristmasPhase.Playing)
            {
                return false;
            }

            ChangePhase(ChristmasPhase.Celebrating);
            return true;
        }

        /// <summary>
        /// 時間を進めます。完成を出し終えたらカウントダウン、数え終えたら終わりにします。
        /// </summary>
        /// <param name="deltaSeconds">進める秒</param>
        public void Tick(float deltaSeconds)
        {
            if (phase == ChristmasPhase.Playing || phase == ChristmasPhase.Finished)
            {
                return;
            }

            phaseElapsed += deltaSeconds;
            if (phase == ChristmasPhase.Celebrating && phaseElapsed >= celebrateSeconds)
            {
                ChangePhase(ChristmasPhase.Countdown);
            }
            else if (phase == ChristmasPhase.Countdown && phaseElapsed >= countdownFrom)
            {
                ChangePhase(ChristmasPhase.Finished);
            }
        }

        /// <summary>
        /// カウントダウンで今出す数（3, 2, 1）を返します。カウントダウン中でなければ 0 です。
        /// </summary>
        /// <returns>出す数</returns>
        public int GetCountdownNumber()
        {
            if (phase != ChristmasPhase.Countdown)
            {
                return 0;
            }

            return Mathf.Clamp(countdownFrom - Mathf.FloorToInt(phaseElapsed), 1, countdownFrom);
        }

        /// <summary>
        /// 今の場面に入ってからの秒数を返します（演出の動きに使う）。
        /// </summary>
        /// <returns>秒</returns>
        public float GetPhaseElapsed()
        {
            return phaseElapsed;
        }

        private void ChangePhase(ChristmasPhase next)
        {
            phase = next;
            phaseElapsed = 0f;
        }
    }
}
