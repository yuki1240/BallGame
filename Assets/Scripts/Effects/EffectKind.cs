namespace WallBurst.Effects
{
    /// <summary>
    /// 壁の上部に出す演出の種類。インスペクタの並びは演出の中身と対応する。
    /// </summary>
    public enum EffectKind
    {
        /// <summary>輪。</summary>
        [UnityEngine.InspectorName("輪")]
        Rings = 0,

        /// <summary>閃光。</summary>
        [UnityEngine.InspectorName("閃光")]
        Flash = 1,

        /// <summary>粒。</summary>
        [UnityEngine.InspectorName("粒")]
        Sparks = 2,

        /// <summary>二重。</summary>
        [UnityEngine.InspectorName("二重")]
        Pulse = 3,

        /// <summary>波紋。</summary>
        [UnityEngine.InspectorName("波紋")]
        Ripples = 4,

        /// <summary>十字。</summary>
        [UnityEngine.InspectorName("十字")]
        Cross = 5,

        /// <summary>渦。</summary>
        [UnityEngine.InspectorName("渦")]
        Swirl = 6,

        /// <summary>花びら。</summary>
        [UnityEngine.InspectorName("花びら")]
        Petals = 7,

        /// <summary>閃光と粒。</summary>
        [UnityEngine.InspectorName("閃光粒")]
        FlashSparks = 8
    }
}
