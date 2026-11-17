using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// ふわふわ浮かぶシャボン玉 1 個ぶんの状態です。位置と大きさは 1920×1080 の Canvas の画素（左下が原点）です。
    /// </summary>
    public sealed class Bubble
    {
        /// <summary>見分けるための番号。</summary>
        public int Id;

        /// <summary>今の中心の位置。</summary>
        public Vector2 Position;

        /// <summary>半径。</summary>
        public float Radius;

        /// <summary>上へ昇る速さ（画素／秒）。</summary>
        public float RiseSpeed;

        /// <summary>ゆらゆらの中心になる横の位置。</summary>
        public float BaseX;

        /// <summary>ゆらゆらの横幅。</summary>
        public float SwayAmplitude;

        /// <summary>ゆらゆらの速さ。</summary>
        public float SwaySpeed;

        /// <summary>ゆらゆらの位相。</summary>
        public float SwayPhase;

        /// <summary>色の種類の番号（見た目の色分け用）。</summary>
        public int ColorIndex;
    }

    /// <summary>
    /// 画面に浮かぶシャボン玉の動きと「割れる」判定を扱います。画面に依存しないので、テストで確かめられます。
    /// シャボン玉は下から上へゆらゆら昇り、上へ出たものは下から新しく出てきます。割ったぶんも下から補われます。
    /// </summary>
    public sealed class BubbleField
    {
        /// <summary>同時に浮かべるシャボン玉の数の既定値。</summary>
        public const int DefaultCount = 14;

        /// <summary>半径の最小（画素）。</summary>
        public const float MinRadius = 75f;

        /// <summary>半径の最大（画素）。</summary>
        public const float MaxRadius = 150f;

        /// <summary>ボールの当たりと玉の縁が少しずれていても割れるようにする余裕（画素）。</summary>
        public const float HitMargin = 40f;

        /// <summary>1回の当たりで割れる玉の数の上限（近い順）。</summary>
        public const int MaxPopsPerHit = 3;

        /// <summary>色の種類の数。</summary>
        public const int ColorCount = 6;

        private readonly float width;
        private readonly float height;
        private readonly System.Random random;
        private readonly int targetCount;
        private readonly bool refill;
        private readonly List<Bubble> bubbles = new List<Bubble>();
        private int nextId = 1;
        private int poppedCount;

        /// <summary>
        /// シャボン玉の場を作り、画面いっぱいに最初の玉を浮かべます。
        /// </summary>
        /// <param name="width">画面の幅（画素）</param>
        /// <param name="height">画面の高さ（画素）</param>
        /// <param name="random">ばらつきを決める乱数</param>
        /// <param name="targetCount">同時に浮かべる数</param>
        /// <param name="refill">割った玉や昇って消えた玉を下から補うか（false のときは、割らなかった玉が下から戻ってくるだけで、数は増えない）</param>
        public BubbleField(float width, float height, System.Random random, int targetCount = DefaultCount, bool refill = true)
        {
            this.refill = refill;
            this.width = width;
            this.height = height;
            this.random = random;
            this.targetCount = Mathf.Max(1, targetCount);
            for (int i = 0; i < this.targetCount; i++)
            {
                bubbles.Add(CreateBubble(true));
            }
        }

        /// <summary>今浮かんでいるシャボン玉。</summary>
        public IReadOnlyList<Bubble> Bubbles
        {
            get { return bubbles; }
        }

        /// <summary>これまでに割れた数。</summary>
        public int PoppedCount
        {
            get { return poppedCount; }
        }

        /// <summary>
        /// 時間を進めます。玉を昇らせ、上へ出たものを消して、足りないぶんを下から出します。
        /// </summary>
        /// <param name="deltaSeconds">進める秒数</param>
        public void Tick(float deltaSeconds)
        {
            foreach (Bubble bubble in bubbles)
            {
                bubble.SwayPhase += bubble.SwaySpeed * deltaSeconds;
                float y = bubble.Position.y + bubble.RiseSpeed * deltaSeconds;
                float x = bubble.BaseX + Mathf.Sin(bubble.SwayPhase) * bubble.SwayAmplitude;
                bubble.Position = new Vector2(x, y);
            }

            if (!refill)
            {
                WrapBubblesAboveScreen();
                return;
            }

            bubbles.RemoveAll(IsAboveScreen);
            while (bubbles.Count < targetCount)
            {
                bubbles.Add(CreateBubble(false));
            }
        }

        // 数を増やさない場では、上へ出た玉を同じ玉のまま下へ戻す（割りそびれても、また届く所に来る）
        private void WrapBubblesAboveScreen()
        {
            foreach (Bubble bubble in bubbles)
            {
                if (IsAboveScreen(bubble))
                {
                    float y = -bubble.Radius - Mathf.Lerp(0f, 200f, (float)random.NextDouble());
                    bubble.Position = new Vector2(bubble.Position.x, y);
                }
            }
        }

        /// <summary>
        /// 指定の場所にボールが当たったとして、近くの玉を割ります。
        /// </summary>
        /// <param name="point">当たった位置（画素）</param>
        /// <param name="popped">割れた玉を入れる一覧（先に空にします）</param>
        /// <returns>割れた数</returns>
        public int PopAt(Vector2 point, List<Bubble> popped)
        {
            popped.Clear();
            foreach (Bubble bubble in bubbles)
            {
                if (Vector2.Distance(bubble.Position, point) <= bubble.Radius + HitMargin)
                {
                    popped.Add(bubble);
                }
            }

            popped.Sort((a, b) => Vector2.Distance(a.Position, point).CompareTo(Vector2.Distance(b.Position, point)));
            if (popped.Count > MaxPopsPerHit)
            {
                popped.RemoveRange(MaxPopsPerHit, popped.Count - MaxPopsPerHit);
            }

            foreach (Bubble bubble in popped)
            {
                bubbles.Remove(bubble);
            }

            poppedCount += popped.Count;
            return popped.Count;
        }

        /// <summary>
        /// 画面の中にいる玉を1つ、数に入れずに割ります（お祝いの演出用）。
        /// </summary>
        /// <param name="popped">割った玉（無ければ null）</param>
        /// <returns>割ったとき true</returns>
        public bool PopRandomVisible(out Bubble popped)
        {
            List<Bubble> visible = bubbles.FindAll(IsVisible);
            popped = null;
            if (visible.Count == 0)
            {
                return false;
            }

            popped = visible[random.Next(visible.Count)];
            bubbles.Remove(popped);
            return true;
        }

        private bool IsAboveScreen(Bubble bubble)
        {
            return bubble.Position.y - bubble.Radius > height;
        }

        private bool IsVisible(Bubble bubble)
        {
            return bubble.Position.y > 0f && bubble.Position.y < height;
        }

        private Bubble CreateBubble(bool anywhere)
        {
            float radius = Mathf.Lerp(MinRadius, MaxRadius, (float)random.NextDouble());
            float amplitude = Mathf.Lerp(15f, 55f, (float)random.NextDouble());
            float baseX = Mathf.Lerp(radius + amplitude, width - radius - amplitude, (float)random.NextDouble());
            float y = anywhere
                ? Mathf.Lerp(0f, height, (float)random.NextDouble())
                : -radius - Mathf.Lerp(0f, 300f, (float)random.NextDouble());
            Bubble bubble = new Bubble
            {
                Id = nextId++,
                Radius = radius,
                BaseX = baseX,
                SwayAmplitude = amplitude,
                SwaySpeed = Mathf.Lerp(0.6f, 1.4f, (float)random.NextDouble()),
                SwayPhase = Mathf.Lerp(0f, 6.28f, (float)random.NextDouble()),
                RiseSpeed = Mathf.Lerp(55f, 120f, (float)random.NextDouble()),
                ColorIndex = random.Next(ColorCount)
            };
            bubble.Position = new Vector2(baseX + Mathf.Sin(bubble.SwayPhase) * amplitude, y);
            return bubble;
        }
    }
}
