using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 山が紅葉していく様子を計算します（画面の描き方は知りません）。
    /// ボールが当たった場所から、円が少しずつ広がるように色が変わります。
    /// 山の画素ごとに「どれだけ紅葉したか（0 = 緑、1 = 紅葉）」を持ち、決して戻りません。
    /// 山の大部分が紅葉したら、残りは自動でいっせいに色づいて「全体が紅葉」になります。
    /// </summary>
    public sealed class AutumnColoring
    {
        /// <summary>1回の当たりで広がる最大の半径（横幅に対する割合）。</summary>
        public const float DefaultMaxRadius01 = 0.2f;

        /// <summary>最大の半径まで広がるのにかかる秒数。</summary>
        public const float DefaultGrowSeconds = 4f;

        /// <summary>色が変わりかけている縁のはば（横幅に対する割合）。</summary>
        public const float DefaultEdge01 = 0.04f;

        /// <summary>この割合まで紅葉したら、残りをいっせいに色づけて完成にする。</summary>
        public const float DefaultCompleteCoverage = 0.9f;

        /// <summary>残りがいっせいに色づくのにかける秒数。</summary>
        public const float FinishSeconds = 2.5f;

        /// <summary>「すっかり紅葉した」とみなす進み具合。</summary>
        public const float FullThreshold = 0.99f;

        private sealed class Spread
        {
            public float CenterX;
            public float CenterY;
            public float Age;
        }

        private readonly int width;
        private readonly int height;
        private readonly bool[] mask;
        private readonly float[] edgeNoise;
        private readonly float[] progress;
        private readonly float maxRadius01;
        private readonly float growSeconds;
        private readonly float edge01;
        private readonly float completeCoverage;
        private readonly List<Spread> spreads = new List<Spread>();
        private readonly int mountainCount;
        private int fullCount;
        private int sourceCount;
        private float finishElapsed;
        private bool finishing;
        private int dirtyMinY = int.MaxValue;
        private int dirtyMaxY = -1;

        /// <summary>
        /// 山の形を渡して作ります。
        /// </summary>
        /// <param name="width">横の画素数</param>
        /// <param name="height">縦の画素数</param>
        /// <param name="mask">山の部分なら true（画素の数だけ）</param>
        /// <param name="edgeNoise">縁をでこぼこにする 0〜1 のゆらぎ（無ければ null）</param>
        /// <param name="maxRadius01">最大の半径（横幅に対する割合）</param>
        /// <param name="growSeconds">最大まで広がる秒数</param>
        /// <param name="edge01">縁のはば（横幅に対する割合）</param>
        /// <param name="completeCoverage">完成とみなす紅葉の割合</param>
        public AutumnColoring(
            int width,
            int height,
            bool[] mask,
            float[] edgeNoise = null,
            float maxRadius01 = DefaultMaxRadius01,
            float growSeconds = DefaultGrowSeconds,
            float edge01 = DefaultEdge01,
            float completeCoverage = DefaultCompleteCoverage)
        {
            this.width = width;
            this.height = height;
            this.mask = mask;
            this.edgeNoise = edgeNoise;
            this.maxRadius01 = maxRadius01;
            this.growSeconds = Mathf.Max(0.01f, growSeconds);
            this.edge01 = edge01;
            this.completeCoverage = completeCoverage;
            progress = new float[width * height];
            for (int i = 0; i < mask.Length; i++)
            {
                mountainCount += mask[i] ? 1 : 0;
            }
        }

        /// <summary>これまでに色を広げ始めた当たりの数。</summary>
        public int SourceCount
        {
            get { return sourceCount; }
        }

        /// <summary>すっかり紅葉した山の割合（0〜1）。</summary>
        public float Coverage
        {
            get { return mountainCount == 0 ? 1f : fullCount / (float)mountainCount; }
        }

        /// <summary>完成の割合まで届いて、残りをいっせいに色づけている最中か、色づけ終わったか。</summary>
        public bool IsFinishing
        {
            get { return finishing; }
        }

        /// <summary>山全体がすっかり紅葉したか。</summary>
        public bool IsAllColored
        {
            get { return finishing && finishElapsed >= FinishSeconds; }
        }

        /// <summary>
        /// 画素ごとの紅葉の進み具合（0 = 緑、1 = 紅葉）を返します。
        /// </summary>
        /// <param name="x">横の位置</param>
        /// <param name="y">縦の位置（下が 0）</param>
        /// <returns>進み具合</returns>
        public float GetProgress(int x, int y)
        {
            return progress[y * width + x];
        }

        /// <summary>
        /// ボールが当たった場所から、紅葉を広げ始めます。完成に向かっている最中は無視します。
        /// </summary>
        /// <param name="wallUv">壁座標（左下 (0,0)・右上 (1,1)）</param>
        public void AddSource(Vector2 wallUv)
        {
            if (finishing)
            {
                return;
            }

            spreads.Add(new Spread
            {
                CenterX = Mathf.Clamp01(wallUv.x) * (width - 1),
                CenterY = Mathf.Clamp01(wallUv.y) * (height - 1),
                Age = 0f
            });
            sourceCount++;
        }

        /// <summary>
        /// 時間を進めます。
        /// </summary>
        /// <param name="deltaSeconds">進める秒</param>
        /// <returns>色が変わったとき true</returns>
        public bool Tick(float deltaSeconds)
        {
            bool changed = GrowSpreads(deltaSeconds);
            if (!finishing && Coverage >= completeCoverage)
            {
                finishing = true;
                finishElapsed = 0f;
            }

            if (finishing && !IsAllColored)
            {
                finishElapsed += deltaSeconds;
                changed |= FadeRemaining();
            }

            return changed;
        }

        /// <summary>
        /// 前回から色が変わった範囲の縦の位置を返します。
        /// </summary>
        /// <param name="minY">いちばん下の行</param>
        /// <param name="maxY">いちばん上の行</param>
        /// <returns>変わった所があれば true</returns>
        public bool TryGetDirtyRows(out int minY, out int maxY)
        {
            minY = dirtyMinY;
            maxY = dirtyMaxY;
            return dirtyMaxY >= 0;
        }

        /// <summary>
        /// 変わった範囲の記録を消します（絵を描き直したあとに呼ぶ）。
        /// </summary>
        public void ClearDirty()
        {
            dirtyMinY = int.MaxValue;
            dirtyMaxY = -1;
        }

        private bool GrowSpreads(float deltaSeconds)
        {
            bool changed = false;
            for (int i = spreads.Count - 1; i >= 0; i--)
            {
                Spread spread = spreads[i];
                spread.Age += deltaSeconds;
                changed |= PaintSpread(spread);
                if (spread.Age >= growSeconds)
                {
                    spreads.RemoveAt(i);
                }
            }

            return changed;
        }

        private bool PaintSpread(Spread spread)
        {
            float t = Mathf.Clamp01(spread.Age / growSeconds);
            float eased = 1f - (1f - t) * (1f - t);
            float radius = maxRadius01 * width * eased;
            float edge = Mathf.Max(1f, edge01 * width);
            float reach = radius + edge * 2f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(spread.CenterX - reach));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(spread.CenterX + reach));
            int minY = Mathf.Max(0, Mathf.FloorToInt(spread.CenterY - reach));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(spread.CenterY + reach));
            bool changed = false;
            for (int y = minY; y <= maxY; y++)
            {
                bool rowChanged = false;
                for (int x = minX; x <= maxX; x++)
                {
                    rowChanged |= PaintCell(spread, x, y, radius, edge);
                }

                if (rowChanged)
                {
                    MarkDirty(y);
                    changed = true;
                }
            }

            return changed;
        }

        private bool PaintCell(Spread spread, int x, int y, float radius, float edge)
        {
            int index = y * width + x;
            if (!mask[index])
            {
                return false;
            }

            float dx = x - spread.CenterX;
            float dy = y - spread.CenterY;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float wobble = edgeNoise == null ? 0f : (edgeNoise[index] - 0.5f) * edge * 2.5f;
            float value = Mathf.Clamp01((radius - distance + wobble) / edge);
            return Raise(index, value);
        }

        private bool FadeRemaining()
        {
            float level = IsAllColoredAfter(finishElapsed) ? 1f : Mathf.SmoothStep(0f, 1f, finishElapsed / FinishSeconds);
            bool changed = false;
            for (int y = 0; y < height; y++)
            {
                bool rowChanged = false;
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    rowChanged |= mask[index] && Raise(index, level);
                }

                if (rowChanged)
                {
                    MarkDirty(y);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsAllColoredAfter(float elapsed)
        {
            return elapsed >= FinishSeconds;
        }

        private bool Raise(int index, float value)
        {
            float old = progress[index];
            if (value <= old)
            {
                return false;
            }

            progress[index] = value;
            if (old < FullThreshold && value >= FullThreshold)
            {
                fullCount++;
            }

            return true;
        }

        private void MarkDirty(int y)
        {
            dirtyMinY = Mathf.Min(dirtyMinY, y);
            dirtyMaxY = Mathf.Max(dirtyMaxY, y);
        }
    }
}
