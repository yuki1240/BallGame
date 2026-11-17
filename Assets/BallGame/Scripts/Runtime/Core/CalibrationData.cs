using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 校正データ。四隅・基準深度・しきい値・左右反転・入力源・設置向きを持ちます。
    /// 四隅は深度画像上の 0〜1（上端が 0）で、左上・右上・右下・左下の順に並びます。
    /// </summary>
    public sealed class CalibrationData
    {
        /// <summary>四隅の数。</summary>
        public const int CornerCount = 4;

        /// <summary>壁からの距離の初期値（mm）。</summary>
        public const int DefaultMaxDistanceMm = 150;

        /// <summary>壁からの距離の下限（cm）。</summary>
        public const int MinMaxDistanceCm = 5;

        /// <summary>壁からの距離の上限（cm）。</summary>
        public const int MaxMaxDistanceCm = 80;

        /// <summary>ノイズ床の初期値（mm）。</summary>
        public const int DefaultNoiseFloorMm = 45;

        /// <summary>四隅（深度画像上の 0〜1）。</summary>
        public readonly List<Vector2> Corners = new List<Vector2>();

        /// <summary>基準深度の幅（画素）。</summary>
        public int Width;

        /// <summary>基準深度の高さ（画素）。</summary>
        public int Height;

        /// <summary>基準深度（mm、0 は無効）。未取得なら null。</summary>
        public ushort[] Baseline;

        /// <summary>画素ごとの揺れ幅（mm）。古いデータでは null。不安定な画素は BaselineRecorder.UnstableNoise。</summary>
        public ushort[] BaselineNoise;

        /// <summary>ノイズ床の下限（mm）。</summary>
        public const int MinNoiseFloorMm = 20;

        /// <summary>ノイズ床の上限（mm）。</summary>
        public const int MaxNoiseFloorMm = 200;

        /// <summary>壁からの距離の上限（mm）。これより遠いものは捨てる。</summary>
        public int MaxDistanceMm = DefaultMaxDistanceMm;

        /// <summary>ノイズ床（mm）。これ未満はセンサーの揺れとして捨てる。</summary>
        public int NoiseFloorMm = DefaultNoiseFloorMm;

        /// <summary>左右反転。初期オン。</summary>
        public bool MirrorX = true;

        /// <summary>入力源。</summary>
        public DepthSourceKind InputSource = DepthSourceKind.Kinect;

        /// <summary>設置向き。</summary>
        public MountOrientation Mount = MountOrientation.Side;

        /// <summary>四隅がすべて置かれているか。</summary>
        public bool HasAllCorners
        {
            get { return Corners.Count == CornerCount; }
        }

        /// <summary>基準深度があるか。</summary>
        public bool HasBaseline
        {
            get { return Baseline != null && Width > 0 && Height > 0 && Baseline.Length == Width * Height; }
        }

        /// <summary>四隅と基準がそろっているか。</summary>
        public bool IsComplete
        {
            get { return HasAllCorners && HasBaseline; }
        }

        /// <summary>
        /// 指定した解像度の深度と基準深度の大きさが合っているかを返します。
        /// </summary>
        /// <param name="width">深度の幅</param>
        /// <param name="height">深度の高さ</param>
        /// <returns>合っているとき true</returns>
        public bool MatchesResolution(int width, int height)
        {
            return HasBaseline && Width == width && Height == height;
        }

        /// <summary>
        /// 四隅と基準深度だけを消します。
        /// </summary>
        public void ClearCornersAndBaseline()
        {
            Corners.Clear();
            ClearBaseline();
        }

        /// <summary>
        /// 基準深度だけを消します。
        /// </summary>
        public void ClearBaseline()
        {
            Baseline = null;
            BaselineNoise = null;
            Width = 0;
            Height = 0;
        }

        /// <summary>
        /// すべてを初期状態に戻します（設置向きは横設置）。
        /// </summary>
        public void ResetAll()
        {
            ClearCornersAndBaseline();
            MaxDistanceMm = DefaultMaxDistanceMm;
            NoiseFloorMm = DefaultNoiseFloorMm;
            MirrorX = true;
            InputSource = DepthSourceKind.Kinect;
            Mount = MountOrientation.Side;
        }

        /// <summary>
        /// 設置向きを選びます。同じ向きを選び直したときは四隅と基準を残し、別の向きへ切り替えたときだけ消します。
        /// </summary>
        /// <param name="mount">設置向き</param>
        /// <returns>四隅と基準を消したとき true</returns>
        public bool SelectMount(MountOrientation mount)
        {
            if (Mount == mount)
            {
                return false;
            }

            Mount = mount;
            ClearCornersAndBaseline();
            return true;
        }

        /// <summary>
        /// 基準深度を設定します。
        /// </summary>
        /// <param name="baseline">基準深度</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="noise">画素ごとの揺れ幅（無ければ null）</param>
        public void SetBaseline(ushort[] baseline, int width, int height, ushort[] noise = null)
        {
            Baseline = baseline;
            BaselineNoise = noise != null && baseline != null && noise.Length == baseline.Length ? noise : null;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// 四隅を1つ追加します。4つそろっていれば何もしません。
        /// </summary>
        /// <param name="imageUv">深度画像上の 0〜1</param>
        /// <returns>追加できたとき true</returns>
        public bool AddCorner(Vector2 imageUv)
        {
            if (HasAllCorners)
            {
                return false;
            }

            Corners.Add(new Vector2(Mathf.Clamp01(imageUv.x), Mathf.Clamp01(imageUv.y)));
            return true;
        }

        /// <summary>
        /// 置いてある隅を1つ動かします（0〜1 に収めます）。
        /// </summary>
        /// <param name="index">隅の番号（0:左上 1:右上 2:右下 3:左下）</param>
        /// <param name="imageUv">新しい位置（深度画像上の 0〜1）</param>
        /// <returns>動かせたとき true</returns>
        public bool SetCorner(int index, Vector2 imageUv)
        {
            if (index < 0 || index >= Corners.Count)
            {
                return false;
            }

            Corners[index] = new Vector2(Mathf.Clamp01(imageUv.x), Mathf.Clamp01(imageUv.y));
            return true;
        }

        /// <summary>
        /// 置いてある隅をすべて同じだけ動かします。どの隅も画像からはみ出さないように動く量を縮めます。
        /// </summary>
        /// <param name="delta">動かす量（深度画像上の 0〜1）</param>
        /// <returns>実際に動かした量</returns>
        public Vector2 TranslateCorners(Vector2 delta)
        {
            Vector2 applied = ClampTranslation(delta);
            for (int i = 0; i < Corners.Count; i++)
            {
                Corners[i] += applied;
            }

            return applied;
        }

        private Vector2 ClampTranslation(Vector2 delta)
        {
            float minX = -1f;
            float maxX = 1f;
            float minY = -1f;
            float maxY = 1f;
            foreach (Vector2 corner in Corners)
            {
                minX = Mathf.Max(minX, -corner.x);
                maxX = Mathf.Min(maxX, 1f - corner.x);
                minY = Mathf.Max(minY, -corner.y);
                maxY = Mathf.Min(maxY, 1f - corner.y);
            }

            return new Vector2(Mathf.Clamp(delta.x, minX, maxX), Mathf.Clamp(delta.y, minY, maxY));
        }

        /// <summary>
        /// 壁からの距離（cm）を 5〜80 に収めて設定します。
        /// </summary>
        /// <param name="centimeters">cm</param>
        /// <returns>実際に設定した値（cm）</returns>
        public int SetMaxDistanceCm(int centimeters)
        {
            int clamped = Mathf.Clamp(centimeters, MinMaxDistanceCm, MaxMaxDistanceCm);
            MaxDistanceMm = clamped * 10;
            return clamped;
        }

        /// <summary>
        /// ノイズ床（mm）を 20〜200 に収めて設定します。
        /// </summary>
        /// <param name="millimeters">mm</param>
        /// <returns>実際に設定した値（mm）</returns>
        public int SetNoiseFloorMm(int millimeters)
        {
            NoiseFloorMm = Mathf.Clamp(millimeters, MinNoiseFloorMm, MaxNoiseFloorMm);
            return NoiseFloorMm;
        }

        /// <summary>
        /// 壁からの距離（cm）を返します。
        /// </summary>
        /// <returns>cm</returns>
        public int GetMaxDistanceCm()
        {
            return Mathf.RoundToInt(MaxDistanceMm / 10f);
        }

        /// <summary>
        /// 左右反転を切り替え、四隅と基準深度も左右に写し替えます。
        /// </summary>
        /// <param name="mirror">新しい左右反転</param>
        public void ApplyMirror(bool mirror)
        {
            if (mirror == MirrorX)
            {
                return;
            }

            MirrorX = mirror;
            MirrorCorners();
            MirrorBaseline();
        }

        /// <summary>
        /// 複製を作ります。
        /// </summary>
        /// <returns>複製</returns>
        public CalibrationData Clone()
        {
            CalibrationData copy = new CalibrationData();
            copy.Corners.AddRange(Corners);
            copy.Width = Width;
            copy.Height = Height;
            copy.Baseline = Baseline != null ? (ushort[])Baseline.Clone() : null;
            copy.BaselineNoise = BaselineNoise != null ? (ushort[])BaselineNoise.Clone() : null;
            copy.MaxDistanceMm = MaxDistanceMm;
            copy.NoiseFloorMm = NoiseFloorMm;
            copy.MirrorX = MirrorX;
            copy.InputSource = InputSource;
            copy.Mount = Mount;
            return copy;
        }

        private void MirrorCorners()
        {
            for (int i = 0; i < Corners.Count; i++)
            {
                Vector2 corner = Corners[i];
                Corners[i] = new Vector2(1f - corner.x, corner.y);
            }

            if (HasAllCorners)
            {
                // 左右が入れ替わるので、左上⇔右上、右下⇔左下を入れ替えて順番を保つ
                SwapCorners(0, 1);
                SwapCorners(2, 3);
            }
        }

        private void SwapCorners(int a, int b)
        {
            Vector2 temp = Corners[a];
            Corners[a] = Corners[b];
            Corners[b] = temp;
        }

        private void MirrorBaseline()
        {
            if (HasBaseline)
            {
                DepthMirror.MirrorX(Baseline, Width, Height);
                if (BaselineNoise != null)
                {
                    DepthMirror.MirrorX(BaselineNoise, Width, Height);
                }
            }
        }
    }
}
