namespace BallGame
{
    /// <summary>
    /// 深度の入力源。
    /// </summary>
    public enum DepthSourceKind
    {
        /// <summary>Kinect v2（Kinect20.dll）。</summary>
        Kinect = 0,

        /// <summary>320×240 の模擬深度。Game 画面のクリックをボールとして入れる。</summary>
        Simulated = 1
    }
}
