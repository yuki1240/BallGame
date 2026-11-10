using System;

namespace WallBurst.Detection
{
    /// <summary>
    /// 深度差分から壁際の衝突を拾うときのしきい値。現場のスライダーからその場で変える。
    /// </summary>
    public sealed class DetectionTuning
    {
        /// <summary>これより小さい深度差（mm）はセンサーの揺れとして捨てる。</summary>
        public int NoiseFloorMillimeters = 45;

        /// <summary>壁よりこの距離（mm）以上手前にあるものは、部屋側の人として捨てる。</summary>
        public int MaxApproachMillimeters = 650;

        /// <summary>採用する塊の最小画素数。</summary>
        public int MinBlobArea = 16;

        /// <summary>最小面積の何倍以上なら、1フレームでもボールとして採用するか。</summary>
        public int StrongBlobMultiplier = 2;

        /// <summary>これより大きい塊は、壁の前にいる人として捨てる。</summary>
        public int MaxBlobArea = 4500;

        /// <summary>小さい塊を採用するまでに必要な連続フレーム数。</summary>
        public int ConfirmFrames = 2;

        /// <summary>3x3 のうち、差分とみなす画素がこの数未満なら点ノイズとして捨てる。</summary>
        public int SpatialMinNeighbors = 4;

        /// <summary>同じ場所から次のエフェクトを出すまでの秒数。</summary>
        public float CooldownSeconds = 0.45f;

        /// <summary>壁を横に割る数。追跡ではなく、連打防止の升目。</summary>
        public int GridColumns = 10;

        /// <summary>壁を縦に割る数。</summary>
        public int GridRows = 6;

        /// <summary>壁に留まった物体を、このフレーム数を超えたら打ち切る余裕。</summary>
        public int FreshFrameMargin = 3;

        /// <summary>長時間運転で温度ドリフトを、安定画素だけゆっくり基準へ吸収する。</summary>
        public bool EnableDriftCompensation = true;

        /// <summary>ドリフト吸収で現在値を混ぜる割合。</summary>
        public float DriftBlend = 0.04f;

        /// <summary>何フレームごとにドリフト吸収するか。</summary>
        public int DriftIntervalFrames = 45;

        /// <summary>
        /// スライダー操作で壊れない範囲に収める。
        /// </summary>
        public void Clamp()
        {
            NoiseFloorMillimeters = Math.Max(5, NoiseFloorMillimeters);
            MaxApproachMillimeters = Math.Max(NoiseFloorMillimeters + 1, MaxApproachMillimeters);
            MinBlobArea = Math.Max(1, MinBlobArea);
            StrongBlobMultiplier = Math.Max(2, StrongBlobMultiplier);
            MaxBlobArea = Math.Max(MinBlobArea, MaxBlobArea);
            ConfirmFrames = Math.Max(1, ConfirmFrames);
            SpatialMinNeighbors = Math.Max(1, Math.Min(9, SpatialMinNeighbors));
            CooldownSeconds = Math.Max(0.05f, CooldownSeconds);
            GridColumns = Math.Max(1, GridColumns);
            GridRows = Math.Max(1, GridRows);
            FreshFrameMargin = Math.Max(1, FreshFrameMargin);
            DriftBlend = Math.Max(0f, Math.Min(0.5f, DriftBlend));
            DriftIntervalFrames = Math.Max(1, DriftIntervalFrames);
        }
    }
}
