using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// シャボン玉割りの1回の遊びの段階。
    /// </summary>
    public enum BubblePhase
    {
        /// <summary>遊んでいる（制限時間のあいだ）。</summary>
        Playing,

        /// <summary>結果を見せている。</summary>
        Result
    }

    /// <summary>
    /// シャボン玉割りの進み方を決めます。画面に依存しないので、テストで確かめられます。
    /// 15個の玉を15秒で割る1回の遊びを、場面に居られる時間（15分）が過ぎるまで繰り返します。
    /// 15分を過ぎたときに遊んでいる最中の回は、結果を見せ終えるまで続け、そのあとで場面を終えます。
    /// </summary>
    public sealed class BubbleGame
    {
        /// <summary>1回に浮かぶ玉の数（全部割れたら満点）。</summary>
        public const int BubbleCount = 15;

        /// <summary>1回の制限時間（秒）。</summary>
        public const float RoundSeconds = 15f;

        /// <summary>結果を見せる時間（秒）。</summary>
        public const float ResultSeconds = 5f;

        /// <summary>場面に居られる時間の既定値（秒）。15分です。</summary>
        public const float DefaultSceneSeconds = 900f;

        private readonly float width;
        private readonly float height;
        private readonly System.Random random;
        private readonly float sceneSeconds;

        /// <summary>
        /// 遊びを作り、1回目を始めます。
        /// </summary>
        /// <param name="width">画面の幅（画素）</param>
        /// <param name="height">画面の高さ（画素）</param>
        /// <param name="random">ばらつきを決める乱数</param>
        /// <param name="sceneSeconds">場面に居られる時間（秒）</param>
        public BubbleGame(float width, float height, System.Random random, float sceneSeconds = DefaultSceneSeconds)
        {
            this.width = width;
            this.height = height;
            this.random = random;
            this.sceneSeconds = sceneSeconds;
            StartRound();
        }

        /// <summary>今の回の玉の場。</summary>
        public BubbleField Field { get; private set; }

        /// <summary>今の段階。</summary>
        public BubblePhase Phase { get; private set; }

        /// <summary>今の回の残り秒数（遊んでいる間）。</summary>
        public float RoundRemainingSeconds { get; private set; }

        /// <summary>結果を見せる残り秒数（結果の間）。</summary>
        public float ResultRemainingSeconds { get; private set; }

        /// <summary>場面に入ってからの秒数。</summary>
        public float SceneElapsedSeconds { get; private set; }

        /// <summary>何回目か（1から）。</summary>
        public int RoundNumber { get; private set; }

        /// <summary>この回に割れた数。</summary>
        public int PoppedCount
        {
            get { return Field.PoppedCount; }
        }

        /// <summary>場面を終えてよいか（15分を過ぎ、最後の回の結果も見せ終えた）。</summary>
        public bool IsFinished { get; private set; }

        /// <summary>今の結果のあとで場面を終えるか（結果の間だけ意味がある）。</summary>
        public bool WillFinishAfterResult
        {
            get { return Phase == BubblePhase.Result && SceneElapsedSeconds + ResultRemainingSeconds >= sceneSeconds; }
        }

        /// <summary>場面に居られる残りの秒数（0 以上）。</summary>
        public float SceneRemainingSeconds
        {
            get { return Mathf.Max(0f, sceneSeconds - SceneElapsedSeconds); }
        }

        /// <summary>
        /// 時間を進めます。玉を動かし、制限時間・結果の表示・次の回への切り替えを進めます。
        /// </summary>
        /// <param name="deltaSeconds">進める秒数</param>
        public void Tick(float deltaSeconds)
        {
            SceneElapsedSeconds += deltaSeconds;
            Field.Tick(deltaSeconds);
            if (IsFinished)
            {
                return;
            }

            if (Phase == BubblePhase.Playing)
            {
                RoundRemainingSeconds = Mathf.Max(0f, RoundRemainingSeconds - deltaSeconds);
                FinishRoundIfDone();
                return;
            }

            ResultRemainingSeconds = Mathf.Max(0f, ResultRemainingSeconds - deltaSeconds);
            if (ResultRemainingSeconds <= 0f)
            {
                AdvanceAfterResult();
            }
        }

        /// <summary>
        /// 玉を動かすだけで、時間は進めません（固定モード用）。
        /// </summary>
        /// <param name="deltaSeconds">進める秒数</param>
        public void FloatOnly(float deltaSeconds)
        {
            Field.Tick(deltaSeconds);
        }

        /// <summary>
        /// 指定の場所にボールが当たったとして、玉を割ります。遊んでいる間だけ割れます。
        /// </summary>
        /// <param name="point">当たった位置（画素）</param>
        /// <param name="popped">割れた玉を入れる一覧</param>
        /// <returns>割れた数</returns>
        public int PopAt(Vector2 point, List<Bubble> popped)
        {
            popped.Clear();
            if (Phase != BubblePhase.Playing || IsFinished)
            {
                return 0;
            }

            int count = Field.PopAt(point, popped);
            FinishRoundIfDone();
            return count;
        }

        /// <summary>
        /// 割れた数に合わせた、結果の文を返します。
        /// </summary>
        /// <param name="count">割れた数</param>
        /// <returns>結果の文</returns>
        public static string GetResultMessage(int count)
        {
            if (count >= BubbleCount)
            {
                return count + "こ われたよ！やったね！";
            }

            if (count >= 10)
            {
                return count + "こ われたよ！　おしかったね！";
            }

            if (count >= 1)
            {
                return count + "こ われたよ！まだまだこれから！";
            }

            return "0こ だったよ！つぎはがんばろう！";
        }

        private void FinishRoundIfDone()
        {
            if (Phase != BubblePhase.Playing)
            {
                return;
            }

            if (Field.Bubbles.Count == 0 || RoundRemainingSeconds <= 0f)
            {
                Phase = BubblePhase.Result;
                ResultRemainingSeconds = ResultSeconds;
            }
        }

        private void AdvanceAfterResult()
        {
            if (SceneElapsedSeconds >= sceneSeconds)
            {
                IsFinished = true;
                return;
            }

            StartRound();
        }

        private void StartRound()
        {
            RoundNumber++;
            Field = new BubbleField(width, height, random, BubbleCount, false);
            Phase = BubblePhase.Playing;
            RoundRemainingSeconds = RoundSeconds;
            ResultRemainingSeconds = 0f;
        }
    }
}
