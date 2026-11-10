using System;
using System.Collections.Generic;

namespace WallBurst.Detection
{
    /// <summary>
    /// 差分マスクから 4 近傍の塊を取り出す。バッファは再利用し、毎フレーム確保しない。
    /// </summary>
    public sealed class BlobExtractor
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _pixelCount;
        private readonly int[] _stamp;
        private readonly int[] _queue;
        private int _generation;

        /// <summary>
        /// 画像サイズに合わせた作業バッファを確保する。
        /// </summary>
        /// <param name="width">幅。</param>
        /// <param name="height">高さ。</param>
        public BlobExtractor(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "画像サイズが不正です。");
            }

            _width = width;
            _height = height;
            _pixelCount = width * height;
            _stamp = new int[_pixelCount];
            _queue = new int[_pixelCount];
            _generation = 0;
        }

        /// <summary>
        /// マスク上の塊を面積の大きい順に、最大 maxResults 個まで返す。
        /// </summary>
        /// <param name="mask">0 以外が差分画素。</param>
        /// <param name="persistence">画素ごとの残像スコア。null なら 1 として数える。</param>
        /// <param name="minArea">これ未満の塊は捨てる。</param>
        /// <param name="results">結果の格納先。中身は置き換わる。</param>
        /// <param name="maxResults">残す最大個数。</param>
        /// <returns>採用した塊の数。</returns>
        public int Extract(byte[] mask, byte[] persistence, int minArea, List<Blob> results, int maxResults)
        {
            if (mask == null || mask.Length != _pixelCount)
            {
                throw new ArgumentException("マスクのサイズが画像と一致しません。");
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (maxResults <= 0)
            {
                return 0;
            }

            BeginGeneration();
            for (int index = 0; index < _pixelCount; index++)
            {
                if (mask[index] == 0 || _stamp[index] == _generation)
                {
                    continue;
                }

                Blob blob = Flood(index, mask, persistence);
                if (blob.Area >= minArea)
                {
                    InsertTop(results, blob, maxResults);
                }
            }

            return results.Count;
        }

        /// <summary>
        /// 訪問スタンプを進める。一周したら配列を消して 1 に戻す。
        /// </summary>
        private void BeginGeneration()
        {
            _generation++;
            if (_generation == int.MaxValue)
            {
                Array.Clear(_stamp, 0, _stamp.Length);
                _generation = 1;
            }
        }

        /// <summary>
        /// 開始画素からつながる画素を集める。
        /// </summary>
        private Blob Flood(int start, byte[] mask, byte[] persistence)
        {
            int head = 0;
            int tail = 0;
            _queue[tail++] = start;
            _stamp[start] = _generation;

            Blob blob = new Blob();
            while (head < tail)
            {
                int index = _queue[head++];
                int x = index % _width;
                int y = index / _width;
                blob.Area++;
                blob.SumX += x + 0.5f;
                blob.SumY += y + 0.5f;
                blob.PersistenceSum += persistence == null ? 1 : persistence[index];
                PushNeighbor(x - 1, y, mask, ref tail);
                PushNeighbor(x + 1, y, mask, ref tail);
                PushNeighbor(x, y - 1, mask, ref tail);
                PushNeighbor(x, y + 1, mask, ref tail);
            }

            return blob;
        }

        /// <summary>
        /// 未訪問の差分画素をキューに積む。
        /// </summary>
        private void PushNeighbor(int x, int y, byte[] mask, ref int tail)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height)
            {
                return;
            }

            int index = (y * _width) + x;
            if (mask[index] == 0 || _stamp[index] == _generation)
            {
                return;
            }

            _stamp[index] = _generation;
            _queue[tail++] = index;
        }

        /// <summary>
        /// 面積の大きい方から最大件数だけ残す。
        /// </summary>
        private static void InsertTop(List<Blob> results, Blob blob, int maxResults)
        {
            int insertAt = results.Count;
            for (int i = 0; i < results.Count; i++)
            {
                if (blob.Area > results[i].Area)
                {
                    insertAt = i;
                    break;
                }
            }

            if (insertAt >= maxResults)
            {
                return;
            }

            results.Insert(insertAt, blob);
            if (results.Count > maxResults)
            {
                results.RemoveAt(results.Count - 1);
            }
        }
    }
}
