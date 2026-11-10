using System;
using UnityEngine;

namespace WallBurst.Detection
{
    /// <summary>
    /// 設置時の基準深度と、投影範囲の四隅。ディスクには書かず、起動中のメモリだけに持つ。
    /// 四隅の順は左上、右上、右下、左下。画素の Y は深度画像の上を 0 とする。
    /// </summary>
    public sealed class CalibrationData
    {
        private readonly bool[] _cornerDefined;

        /// <summary>
        /// 空の基準を作る。基準深度の配列は呼び出し側の所有になり、後から差し替えられる。
        /// </summary>
        /// <param name="width">深度画像の幅。</param>
        /// <param name="height">深度画像の高さ。</param>
        /// <param name="baseline">無効画素は 0、それ以外はミリメートル。</param>
        /// <param name="tuning">検知しきい値。スライダーと同じインスタンスを渡す。</param>
        public CalibrationData(int width, int height, ushort[] baseline, DetectionTuning tuning)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "深度画像のサイズが不正です。");
            }

            if (baseline == null || baseline.Length != width * height)
            {
                throw new ArgumentException("基準深度の長さが画像サイズと一致しません。");
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            Width = width;
            Height = height;
            Baseline = baseline;
            Tuning = tuning;
            Corners = new Vector2[4];
            _cornerDefined = new bool[4];
        }

        /// <summary>深度画像の幅。</summary>
        public int Width { get; private set; }

        /// <summary>深度画像の高さ。</summary>
        public int Height { get; private set; }

        /// <summary>設置時の壁の深度（mm）。0 は無効。</summary>
        public ushort[] Baseline { get; private set; }

        /// <summary>現場で調整するしきい値。</summary>
        public DetectionTuning Tuning { get; private set; }

        /// <summary>左上、右上、右下、左下。</summary>
        public Vector2[] Corners { get; private set; }

        /// <summary>四隅がすべて指定済みか。</summary>
        public bool HasAllCorners
        {
            get
            {
                return _cornerDefined[0] && _cornerDefined[1] && _cornerDefined[2] && _cornerDefined[3];
            }
        }

        /// <summary>指定済みの隅の数。</summary>
        public int DefinedCornerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (_cornerDefined[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 深度源の解像度が、この基準と同じか。
        /// </summary>
        /// <param name="width">現在の深度幅。</param>
        /// <param name="height">現在の深度高さ。</param>
        /// <returns>一致すれば true。</returns>
        public bool Matches(int width, int height)
        {
            return Width == width && Height == height && Baseline != null && Baseline.Length == width * height;
        }

        /// <summary>
        /// 隅を1つ記録する。
        /// </summary>
        /// <param name="index">0 左上、1 右上、2 右下、3 左下。</param>
        /// <param name="pixel">深度画像上の画素座標。</param>
        public void SetCorner(int index, Vector2 pixel)
        {
            if (index < 0 || index > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "隅の番号は 0 から 3 です。");
            }

            Corners[index] = pixel;
            _cornerDefined[index] = true;
        }

        /// <summary>
        /// その隅が指定済みか。
        /// </summary>
        /// <param name="index">0 から 3。</param>
        /// <returns>指定済みなら true。</returns>
        public bool IsCornerDefined(int index)
        {
            if (index < 0 || index > 3)
            {
                return false;
            }

            return _cornerDefined[index];
        }

        /// <summary>
        /// 四隅の指定を消す。
        /// </summary>
        public void ClearCorners()
        {
            for (int i = 0; i < 4; i++)
            {
                Corners[i] = Vector2.zero;
                _cornerDefined[i] = false;
            }
        }

        /// <summary>
        /// 基準深度だけ取り直す。解像度が違う配列は拒否する。
        /// </summary>
        /// <param name="baseline">新しい中央値深度。</param>
        public void ReplaceBaseline(ushort[] baseline)
        {
            if (baseline == null || baseline.Length != Width * Height)
            {
                throw new ArgumentException("取り直した基準深度のサイズが一致しません。");
            }

            Baseline = baseline;
        }
    }
}
