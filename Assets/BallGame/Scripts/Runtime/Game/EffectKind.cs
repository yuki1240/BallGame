namespace BallGame
{
    /// <summary>
    /// 演出の種類。
    /// </summary>
    public enum EffectKind
    {
        /// <summary>輪。</summary>
        Ring = 0,

        /// <summary>閃光。</summary>
        Flash = 1,

        /// <summary>粒。</summary>
        Particles = 2,

        /// <summary>二重。</summary>
        DoubleRing = 3,

        /// <summary>波紋。</summary>
        Ripple = 4,

        /// <summary>十字。</summary>
        Cross = 5,

        /// <summary>渦。</summary>
        Swirl = 6,

        /// <summary>花びら。</summary>
        Petals = 7,

        /// <summary>閃光粒。</summary>
        FlashParticles = 8
    }

    /// <summary>
    /// 当たったときの反応の種類。
    /// </summary>
    public enum HitReactionKind
    {
        /// <summary>花を伸ばす（壁座標の下半分）。</summary>
        Flower = 0,

        /// <summary>演出を出す（壁座標の上半分）。</summary>
        Effect = 1
    }
}
