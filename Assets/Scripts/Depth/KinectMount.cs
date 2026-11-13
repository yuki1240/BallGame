namespace WallBurst.Depth
{
    /// <summary>
    /// Kinect の置き方。調整画面でどちらかを押して選ぶ。
    /// </summary>
    public enum KinectMount
    {
        /// <summary>壁の真下に置き、上向きで壁を見る。</summary>
        LookingUp = 0,

        /// <summary>壁と水平。Xbox ロゴが手前を向き、映像の下が近く上が遠い。</summary>
        AlongWall = 1
    }
}
