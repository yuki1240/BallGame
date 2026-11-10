namespace WallBurst.Detection
{
    /// <summary>
    /// 壁際で新しく現れた差分の塊。エフェクトを1つ出す単位。
    /// </summary>
    public readonly struct WallHit
    {
        /// <summary>
        /// ヒットを作る。
        /// </summary>
        /// <param name="pixelX">深度画像上の重心 X。</param>
        /// <param name="pixelY">深度画像上の重心 Y。</param>
        /// <param name="wallU">壁の横 0〜1。</param>
        /// <param name="wallV">壁の縦 0〜1。下が 0。</param>
        /// <param name="area">画素数。</param>
        public WallHit(float pixelX, float pixelY, float wallU, float wallV, int area)
        {
            PixelX = pixelX;
            PixelY = pixelY;
            WallU = wallU;
            WallV = wallV;
            Area = area;
        }

        /// <summary>深度画像上の重心 X。</summary>
        public float PixelX { get; }

        /// <summary>深度画像上の重心 Y。</summary>
        public float PixelY { get; }

        /// <summary>壁の横位置。</summary>
        public float WallU { get; }

        /// <summary>壁の縦位置。下が 0。</summary>
        public float WallV { get; }

        /// <summary>塊の画素数。</summary>
        public int Area { get; }
    }
}
