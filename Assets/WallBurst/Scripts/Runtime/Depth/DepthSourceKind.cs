namespace WallBurst
{
    /// <summary>
    /// 深度の入力源。
    /// </summary>
    public enum DepthSourceKind
    {
        /// <summary>Kinect v2（Kinect20.dll）。</summary>
        Kinect = 0,

        /// <summary>模擬深度（Kinect が無い PC 用）。ゲーム画面のクリックをボールとして入れる。</summary>
        Simulated = 1
    }
}
