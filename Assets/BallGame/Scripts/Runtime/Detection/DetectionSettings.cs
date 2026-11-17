namespace BallGame
{
    /// <summary>
    /// ボール検知のしきい値。
    /// </summary>
    public sealed class DetectionSettings
    {
        /// <summary>ノイズ床（mm）。これ未満はセンサーの揺れとして捨てる。</summary>
        public int NoiseFloorMm = CalibrationData.DefaultNoiseFloorMm;

        /// <summary>画素ごとの揺れ幅の何倍までを揺れとみなすか。揺れる画素ほどしきい値が上がる。</summary>
        public float NoiseSigmaMultiplier = 3f;

        /// <summary>揺れ幅で上げるしきい値の上限（ノイズ床の何倍まで）。揺れの大きい画素でもボールを拾えるようにする。</summary>
        public float MaxNoiseFloorScale = 2f;

        /// <summary>壁からの距離の上限（mm）。これより遠いものは捨てる。</summary>
        public int MaxDistanceMm = CalibrationData.DefaultMaxDistanceMm;

        /// <summary>
        /// 大きさの基準にする深度（mm）。面積・縦横・人の判定・同じ物かどうかの距離は、
        /// 「Kinect（512×424）から この深度の所で何画素ぶんか」に直して比べる。
        /// 近い所は1画素が小さく、遠い所は大きいので、実際の大きさで比べるため。
        /// </summary>
        public float ReferenceDepthMm = 2000f;

        /// <summary>これ未満の面積（2m の所での画素数に直した値）の塊は捨てる。</summary>
        public int MinArea = 12;

        /// <summary>これ以上の面積（2m の所での画素数に直した値）の塊は1フレームで採用する。</summary>
        public int InstantArea = 24;

        /// <summary>芯につながっていれば、しきい値のこの割合の弱い変化でも塊に含める（ボールの縁を拾うため）。</summary>
        public float WeakRatio = 0.5f;

        /// <summary>塊の中で一番強い点が、しきい値のこの倍率以上ないと採用しない（ノイズを捨てるため）。</summary>
        public float MinPeakStrength = 1.15f;

        /// <summary>塊が縦横の箱のこの割合以上を埋めていないと採用しない（まばらなノイズを捨てるため）。</summary>
        public float MinFillRatio = 0.2f;

        /// <summary>深度 0 になった画素の強さ（しきい値の何倍とみなすか）。</summary>
        public float ZeroDepthStrength = 2f;

        /// <summary>一番強い点がしきい値のこの倍率以上なら、小さい塊でも1フレームで採用する（速いボール用）。</summary>
        public float InstantPeakStrength = 2f;

        /// <summary>小さい塊を採用するまでに続けて見えるフレーム数。</summary>
        public int ConfirmFrames = 2;

        /// <summary>変化した面積（2m の所での画素数に直した値）の合計がこれを超えたら人とみなす。</summary>
        public int PersonPixelTotal = 4500;

        /// <summary>塊の縦横どちらか（2m の所での画素数に直した値）がこれを超えたら人とみなす。</summary>
        public int PersonMaxExtent = 80;

        /// <summary>人が去ったあと待つ秒数。</summary>
        public float PersonCooldownSeconds = 0.5f;

        /// <summary>同じ物体とみなす重心の距離（2m の所での画素数に直した値）。</summary>
        public float TrackMatchRadius = 8f;

        /// <summary>当たりを出したあと、この距離（2m の所での画素数に直した値）以内に続けて見えるボールは同じ1球とみなす。</summary>
        public float BurstRadius = 60f;

        /// <summary>同じ1球とみなす間、ボールが見えなくなってよいフレーム数（これを過ぎると次の当たりを出せる）。</summary>
        public int BurstGapFrames = 6;

        /// <summary>遠くから飛んできたボールとみなす強さの割合（上限の強さに対する割合）。これ未満まで壁に近づいたら、壁に着いたとみなす。</summary>
        public float FlightRatio = 0.8f;

        /// <summary>壁にいちばん近づいたあと、強さがこれだけ戻ったら「壁から離れた」とみなして当たりを出す。</summary>
        public float ContactRise = 1f;

        /// <summary>壁にいちばん近づいたまま、このフレーム数が過ぎたら当たりを出す（壁に留まるボール用）。</summary>
        public int ContactStuckFrames = 4;

        /// <summary>深度 0 の画素だけで見え続けた物体を、壁の近くにあるものとして扱うまでのフレーム数。</summary>
        public int ZeroOnlyFrames = 10;

        /// <summary>見えなくなった物体を覚えておくフレーム数（同じ場所に留まった物体を出し続けないため）。</summary>
        public int TrackForgetFrames = 10;

        /// <summary>
        /// 校正データのしきい値を取り込みます。
        /// </summary>
        /// <param name="data">校正データ</param>
        public void ApplyCalibration(CalibrationData data)
        {
            NoiseFloorMm = data.NoiseFloorMm;
            MaxDistanceMm = data.MaxDistanceMm;
        }
    }
}
