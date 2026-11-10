using System;
using System.Collections.Generic;
using UnityEngine;

namespace WallBurst.Detection
{
    /// <summary>
    /// 設置時の壁深度と毎フレームの深度を比べ、壁際の新しい塊だけをヒットにする。
    /// ノイズ抑制は次の順で行う。
    /// 1. 基準が無い画素は見ない。壁があった場所が穴になった画素は、赤外線を返さないボールとして候補にする。
    /// 2. ノイズ床未満の差はセンサーの揺れとして捨てる。
    /// 3. 壁から遠すぎる差は部屋にいる人として捨てる。
    /// 4. 3x3 で孤立した画素（フライングピクセル）を捨てる。
    /// 5. ボール級の塊は 1 フレームで採用し、それより小さい塊は数フレーム続いてから採用する。
    /// 6. 人ひとり分より大きい塊、および同じ場所に留まった物体は出さない。物体追跡はしない。
    /// </summary>
    public sealed class WallHitDetector
    {
        private readonly CalibrationData _data;
        private readonly int _pixelCount;
        private readonly byte[] _candidate;
        private readonly byte[] _spatial;
        private readonly byte[] _persistence;
        private readonly byte[] _roi;
        private readonly float[] _cooldownUntil;
        private readonly BlobExtractor _blobs;
        private readonly List<Blob> _blobBuffer;
        private readonly int _columns;
        private readonly int _rows;
        private int _driftTick;

        /// <summary>
        /// 基準と作業バッファを用意する。
        /// </summary>
        /// <param name="data">キャリブレーション。しきい値は共有参照のまま毎フレーム読む。</param>
        public WallHitDetector(CalibrationData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Baseline == null || data.Baseline.Length != data.Width * data.Height)
            {
                throw new ArgumentException("基準深度が未設定か、サイズが一致しません。");
            }

            _data = data;
            _pixelCount = data.Width * data.Height;
            _candidate = new byte[_pixelCount];
            _spatial = new byte[_pixelCount];
            _persistence = new byte[_pixelCount];
            _roi = new byte[_pixelCount];
            _columns = Math.Max(1, data.Tuning.GridColumns);
            _rows = Math.Max(1, data.Tuning.GridRows);
            _cooldownUntil = new float[_columns * _rows];
            _blobs = new BlobExtractor(data.Width, data.Height);
            _blobBuffer = new List<Blob>(8);
            RebuildRoi();
        }

        /// <summary>
        /// ノイズ抑制後に「壁際の変化」とみなした画素。1 が変化。プレビュー用。
        /// </summary>
        public byte[] PreviewMask
        {
            get { return _spatial; }
        }

        /// <summary>
        /// 四隅から検知範囲を作り直す。
        /// </summary>
        public void RebuildRoi()
        {
            if (!_data.HasAllCorners)
            {
                Array.Clear(_roi, 0, _roi.Length);
                return;
            }

            QuadMapper.FillMask(
                _roi,
                _data.Width,
                _data.Height,
                _data.Corners[0],
                _data.Corners[1],
                _data.Corners[2],
                _data.Corners[3]);
        }

        /// <summary>
        /// 残像と連打防止を消す。ゲーム開始時に呼ぶ。
        /// </summary>
        public void ResetTemporal()
        {
            Array.Clear(_persistence, 0, _persistence.Length);
            Array.Clear(_cooldownUntil, 0, _cooldownUntil.Length);
            Array.Clear(_spatial, 0, _spatial.Length);
            _driftTick = 0;
        }

        /// <summary>
        /// 基準深度を差し替えて、残像を捨てる。
        /// </summary>
        /// <param name="baseline">新しい基準。</param>
        public void SetBaseline(ushort[] baseline)
        {
            _data.ReplaceBaseline(baseline);
            ResetTemporal();
        }

        /// <summary>
        /// 1フレームを処理し、新しく出すヒットだけを results に入れる。
        /// </summary>
        /// <param name="depth">ミリメートル深度。</param>
        /// <param name="nowSeconds">単調増加の時刻。連打防止に使う。</param>
        /// <param name="results">ヒットの格納先。</param>
        /// <returns>ヒット数。</returns>
        public int Detect(ushort[] depth, float nowSeconds, List<WallHit> results)
        {
            if (depth == null)
            {
                throw new ArgumentNullException(nameof(depth));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (depth.Length != _pixelCount)
            {
                throw new ArgumentException("深度フレームのサイズがキャリブレーションと一致しません。");
            }

            results.Clear();
            _data.Tuning.Clamp();
            BuildCandidates(depth);
            ApplySpatialFilter();
            UpdatePersistence();
            CollectHits(nowSeconds, results);
            CompensateDrift(depth);
            return results.Count;
        }

        /// <summary>
        /// 壁より近く、かつ壁から離れすぎていない画素を候補にする。
        /// </summary>
        private void BuildCandidates(ushort[] depth)
        {
            int floor = _data.Tuning.NoiseFloorMillimeters;
            int ceiling = _data.Tuning.MaxApproachMillimeters;
            ushort[] baseline = _data.Baseline;

            for (int i = 0; i < _pixelCount; i++)
            {
                _spatial[i] = 0;
                _candidate[i] = 0;
                if (_roi[i] == 0)
                {
                    continue;
                }

                ushort baseDepth = baseline[i];
                ushort current = depth[i];
                if (baseDepth == 0)
                {
                    continue;
                }

                if (current == 0)
                {
                    _candidate[i] = 1;
                    continue;
                }

                int closer = baseDepth - current;
                if (closer >= floor && closer <= ceiling)
                {
                    _candidate[i] = 1;
                }
            }
        }

        /// <summary>
        /// 孤立点を落とす。ボールの塊は近傍が一緒に反応する。
        /// </summary>
        private void ApplySpatialFilter()
        {
            int minNeighbors = _data.Tuning.SpatialMinNeighbors;
            int height = _data.Height;
            int width = _data.Width;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    if (_candidate[index] == 0)
                    {
                        continue;
                    }

                    if (CountNeighbors(x, y) >= minNeighbors)
                    {
                        _spatial[index] = 1;
                    }
                }
            }
        }

        /// <summary>
        /// 自分を含む 3x3 の候補数。
        /// </summary>
        private int CountNeighbors(int x, int y)
        {
            int width = _data.Width;
            int height = _data.Height;
            int count = 0;
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                int sampleY = y + offsetY;
                if (sampleY < 0 || sampleY >= height)
                {
                    continue;
                }

                int row = sampleY * width;
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int sampleX = x + offsetX;
                    if (sampleX < 0 || sampleX >= width)
                    {
                        continue;
                    }

                    if (_candidate[row + sampleX] != 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// 点いた画素は残像を足し、消えた画素は1ずつ減らす。ちらつきは伸びない。
        /// </summary>
        private void UpdatePersistence()
        {
            for (int i = 0; i < _pixelCount; i++)
            {
                if (_spatial[i] != 0)
                {
                    if (_persistence[i] < 250)
                    {
                        _persistence[i]++;
                    }
                }
                else if (_persistence[i] > 0)
                {
                    _persistence[i]--;
                }
            }
        }

        /// <summary>
        /// 塊をヒットへ変換する。大きい塊は即時、小さい塊は残像が育ってから。
        /// </summary>
        private void CollectHits(float nowSeconds, List<WallHit> results)
        {
            _blobs.Extract(_spatial, _persistence, _data.Tuning.MinBlobArea, _blobBuffer, 8);
            for (int i = 0; i < _blobBuffer.Count; i++)
            {
                if (results.Count >= 4)
                {
                    break;
                }

                Blob blob = _blobBuffer[i];
                if (blob.Area > _data.Tuning.MaxBlobArea || !IsAcceptable(blob))
                {
                    continue;
                }

                Vector2 pixel = new Vector2(blob.CentroidX, blob.CentroidY);
                float wallU;
                float wallV;
                if (!TryMap(pixel, out wallU, out wallV))
                {
                    continue;
                }

                if (!TryClaimCell(wallU, wallV, nowSeconds))
                {
                    continue;
                }

                results.Add(new WallHit(pixel.x, pixel.y, wallU, wallV, blob.Area));
            }
        }

        /// <summary>
        /// ボール級の大きさか、小さい塊が指定フレーム続いていて、しかも壁に居座っていない。
        /// </summary>
        private bool IsAcceptable(Blob blob)
        {
            int confirm = _data.Tuning.ConfirmFrames;
            int strongArea = _data.Tuning.MinBlobArea * _data.Tuning.StrongBlobMultiplier;
            bool strong = blob.Area >= strongArea;
            bool stable = blob.PersistenceSum >= confirm * blob.Area;
            if (!strong && !stable)
            {
                return false;
            }

            int freshLimit = (confirm + _data.Tuning.FreshFrameMargin) * blob.Area;
            return blob.PersistenceSum <= freshLimit;
        }

        /// <summary>
        /// 重心を壁座標にする。
        /// </summary>
        private bool TryMap(Vector2 pixel, out float wallU, out float wallV)
        {
            return QuadMapper.TryPixelToWall(
                pixel,
                _data.Corners[0],
                _data.Corners[1],
                _data.Corners[2],
                _data.Corners[3],
                out wallU,
                out wallV);
        }

        /// <summary>
        /// 升目が冷えていれば、周囲も含めて再発生を止める。
        /// </summary>
        private bool TryClaimCell(float wallU, float wallV, float nowSeconds)
        {
            int cellX = ToCell(wallU, _columns);
            int cellY = ToCell(wallV, _rows);
            int index = (cellY * _columns) + cellX;
            if (nowSeconds < _cooldownUntil[index])
            {
                return false;
            }

            float until = nowSeconds + _data.Tuning.CooldownSeconds;
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                StampRow(cellX, cellY + offsetY, until);
            }

            return true;
        }

        /// <summary>
        /// 1行分の近傍升目に冷却時刻を書く。
        /// </summary>
        private void StampRow(int cellX, int cellY, float until)
        {
            if (cellY < 0 || cellY >= _rows)
            {
                return;
            }

            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int x = cellX + offsetX;
                if (x < 0 || x >= _columns)
                {
                    continue;
                }

                int index = (cellY * _columns) + x;
                if (_cooldownUntil[index] < until)
                {
                    _cooldownUntil[index] = until;
                }
            }
        }

        /// <summary>
        /// 0〜1 を升目番号にする。
        /// </summary>
        private static int ToCell(float unit, int count)
        {
            int cell = (int)(unit * count);
            if (cell < 0)
            {
                return 0;
            }

            if (cell >= count)
            {
                return count - 1;
            }

            return cell;
        }

        /// <summary>
        /// 揺れの範囲に収まっている画素だけ、基準を少し現在値へ寄せる。
        /// ボールや人がいる画素は残像が残るので吸収しない。
        /// </summary>
        private void CompensateDrift(ushort[] depth)
        {
            if (!_data.Tuning.EnableDriftCompensation)
            {
                return;
            }

            _driftTick++;
            if (_driftTick < _data.Tuning.DriftIntervalFrames)
            {
                return;
            }

            _driftTick = 0;
            float blend = _data.Tuning.DriftBlend;
            float keep = 1f - blend;
            int limit = _data.Tuning.NoiseFloorMillimeters / 2;
            ushort[] baseline = _data.Baseline;

            for (int i = 0; i < _pixelCount; i++)
            {
                if (_persistence[i] != 0 || baseline[i] == 0 || depth[i] == 0)
                {
                    continue;
                }

                int delta = Math.Abs(baseline[i] - depth[i]);
                if (delta > limit)
                {
                    continue;
                }

                int blended = Mathf.RoundToInt((baseline[i] * keep) + (depth[i] * blend));
                if (blended < 1)
                {
                    blended = 1;
                }

                baseline[i] = (ushort)blended;
            }
        }
    }
}
