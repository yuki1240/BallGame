using System;

namespace WallBurst.Detection
{
    /// <summary>
    /// 深度差分から壁際の衝突を拾うときのしきい値。現場のスライダーからその場で変える。
    /// </summary>
    public sealed class DetectionTuning
    {
        /// <summary>壁から見てエフェクトを出す距離の初期値。15cm。</summary>
        public const int DefaultMaxApproachMillimeters = 150;

        /// <summary>設定できるいちばん壁に近い距離（cm）。</summary>
        public const int MinApproachCentimeters = 5;

        /// <summary>設定できるいちばん壁から遠い距離（cm）。</summary>
        public const int MaxApproachCentimeters = 80;

        /// <summary>これより小さい深度差（mm）はセンサーの揺れとして捨てる。</summary>
        public int NoiseFloorMillimeters = 45;

        /// <summary>壁からこの距離（mm）以内に入ったものをボールとして見る。</summary>
        public int MaxApproachMillimeters = DefaultMaxApproachMillimeters;

        /// <summary>採用する塊の最小画素数。</summary>
        public int MinBlobArea = 16;

        /// <summary>最小面積の何倍以上なら、1フレームでもボールとして採用するか。</summary>
        public int StrongBlobMultiplier = 2;

        /// <summary>変化した画素の合計がこれを超えたら、壁の前の人として、そのフレームはヒットにしない。</summary>
        public int MaxBlobArea = 4500;

        /// <summary>縦または横がこの画素数を超える塊は、壁の前の人として扱う。</summary>
        public int MaxBlobSpan = 80;

        /// <summary>人が映ったあと、歩きの途切れでエフェクトが出ないように待つ秒数。</summary>
        public float PersonHoldSeconds = 0.5f;

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
        /// いまの距離をセンチメートルで返す。
        /// </summary>
        /// <returns>壁からの距離（cm）。</returns>
        public int GetApproachCentimeters()
        {
            int centimeters = (MaxApproachMillimeters + 5) / 10;
            if (centimeters < MinApproachCentimeters)
            {
                return MinApproachCentimeters;
            }

            if (centimeters > MaxApproachCentimeters)
            {
                return MaxApproachCentimeters;
            }

            return centimeters;
        }

        /// <summary>
        /// 壁から何cm以内でエフェクトを出すかを決める。
        /// </summary>
        /// <param name="centimeters">壁からの距離（cm）。</param>
        public void SetApproachCentimeters(int centimeters)
        {
            if (centimeters < MinApproachCentimeters)
            {
                centimeters = MinApproachCentimeters;
            }

            if (centimeters > MaxApproachCentimeters)
            {
                centimeters = MaxApproachCentimeters;
            }

            MaxApproachMillimeters = centimeters * 10;
            Clamp();
        }

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
            MaxBlobSpan = Math.Max(4, MaxBlobSpan);
            PersonHoldSeconds = Math.Max(0f, Math.Min(2f, PersonHoldSeconds));
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
