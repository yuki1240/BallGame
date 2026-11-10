namespace WallBurst.Depth
{
    /// <summary>
    /// 深度カメラ。値はミリメートル。0 は穴（無効）。
    /// </summary>
    public interface IDepthSource
    {
        /// <summary>横の画素数。</summary>
        int Width { get; }

        /// <summary>縦の画素数。</summary>
        int Height { get; }

        /// <summary>1枚でも受信したか。</summary>
        bool HasFrame { get; }

        /// <summary>画面に出す状態文。</summary>
        string StatusMessage { get; }

        /// <summary>
        /// 最新フレームをコピーする。同じシリアルを何度読んでも中身は最新のまま。
        /// </summary>
        /// <param name="destination">コピー先。幅×高さ以上。</param>
        /// <param name="frameSerial">新しいフレームが来るたびに増える番号。</param>
        /// <returns>コピーできたら true。</returns>
        bool TryCopyLatest(ushort[] destination, out int frameSerial);
    }
}
