using System;
using UnityEngine;

namespace WallBurst.Effects
{
    /// <summary>
    /// 背景画像ごとに、下部の花と上部の演出をインスペクタで指定する。
    /// </summary>
    [CreateAssetMenu(fileName = "WallShowSettings", menuName = "Wall Burst/背景ごとの演出")]
    public sealed class WallShowSettings : ScriptableObject
    {
        [SerializeField]
        [Range(0.05f, 0.95f)]
        [Tooltip("この高さより下にあたると花、以上は演出。0 が壁の下、1 が壁の上。")]
        private float _lowerHeight = 0.5f;

        [SerializeField]
        [Tooltip("上から、1枚目の背景、2枚目の背景、の順。花と演出はそれぞれ複数指定できる。")]
        private BackgroundAct[] _backgrounds = Array.Empty<BackgroundAct>();

        /// <summary>
        /// 花と演出を分ける高さ。0 が壁の下。
        /// </summary>
        public float LowerHeight
        {
            get { return Mathf.Clamp01(_lowerHeight); }
        }

        /// <summary>
        /// 壁の下側にあたったか。
        /// </summary>
        /// <param name="wallV">縦 0〜1。下が 0。</param>
        /// <returns>境目より下なら true。</returns>
        public bool IsLower(float wallV)
        {
            return IsLowerHit(wallV, LowerHeight);
        }

        /// <summary>
        /// その背景に割り当てた花を1枚選ぶ。無いときは null。
        /// </summary>
        /// <param name="imageIndex">名前順の背景番号。</param>
        /// <param name="background">いま映している背景。</param>
        /// <param name="roll">候補数を受け取り、選ぶ番号を返す。</param>
        /// <returns>花のスプライト。未設定なら null。</returns>
        public Sprite PickFlower(int imageIndex, Texture2D background, Func<int, int> roll)
        {
            BackgroundAct act = Find(imageIndex, background);
            if (act == null || act.Flowers == null || act.Flowers.Length == 0)
            {
                return null;
            }

            int index = ChooseIndex(act.Flowers.Length, roll);
            return act.Flowers[index];
        }

        /// <summary>
        /// その背景に割り当てた演出を1つ選ぶ。未設定なら画像番号の既定3種。
        /// </summary>
        /// <param name="imageIndex">名前順の背景番号。</param>
        /// <param name="background">いま映している背景。</param>
        /// <param name="roll">候補数を受け取り、選ぶ番号を返す。</param>
        /// <returns>出す演出。</returns>
        public EffectKind PickEffect(int imageIndex, Texture2D background, Func<int, int> roll)
        {
            BackgroundAct act = Find(imageIndex, background);
            EffectKind[] effects = act == null ? null : act.Effects;
            return ChooseEffect(effects, imageIndex, roll);
        }

        /// <summary>
        /// 境目より下か。
        /// </summary>
        /// <param name="wallV">縦 0〜1。下が 0。</param>
        /// <param name="lowerHeight">境目。</param>
        /// <returns>下側なら true。</returns>
        public static bool IsLowerHit(float wallV, float lowerHeight)
        {
            return wallV < Mathf.Clamp01(lowerHeight);
        }

        /// <summary>
        /// 指定が空なら既定の3種から、あればその中から1つ選ぶ。
        /// </summary>
        /// <param name="effects">インスペクタで指定した演出。</param>
        /// <param name="imageIndex">名前順の背景番号。</param>
        /// <param name="roll">候補数を受け取り、選ぶ番号を返す。</param>
        /// <returns>出す演出。</returns>
        public static EffectKind ChooseEffect(EffectKind[] effects, int imageIndex, Func<int, int> roll)
        {
            if (effects == null || effects.Length == 0)
            {
                return (EffectKind)EffectSets.Pick(imageIndex, roll);
            }

            return effects[ChooseIndex(effects.Length, roll)];
        }

        /// <summary>
        /// 背景画像が一致する枠を探し、無ければ番号の枠を返す。
        /// </summary>
        private BackgroundAct Find(int imageIndex, Texture2D background)
        {
            if (_backgrounds == null || _backgrounds.Length == 0)
            {
                return null;
            }

            if (background != null)
            {
                for (int i = 0; i < _backgrounds.Length; i++)
                {
                    BackgroundAct act = _backgrounds[i];
                    if (act != null && act.Matches(background))
                    {
                        return act;
                    }
                }
            }

            if (imageIndex < 0 || imageIndex >= _backgrounds.Length)
            {
                return null;
            }

            return _backgrounds[imageIndex];
        }

        /// <summary>
        /// 0 以上 count 未満の番号。範囲外は 0。
        /// </summary>
        private static int ChooseIndex(int count, Func<int, int> roll)
        {
            if (count <= 1 || roll == null)
            {
                return 0;
            }

            int index = roll(count);
            if (index < 0 || index >= count)
            {
                return 0;
            }

            return index;
        }

        /// <summary>
        /// 背景1枚ぶんの花と演出。
        /// </summary>
        [Serializable]
        public sealed class BackgroundAct
        {
            [SerializeField]
            [Tooltip("どの背景のときに使うか。未指定なら並びの番号で対応する。")]
            private Texture2D _background;

            [SerializeField]
            [Tooltip("壁の下側にあたったとき、この中から1枚をにょきっと出す。Textures/Flowers の画像を割り当てる。")]
            private Sprite[] _flowers = Array.Empty<Sprite>();

            [SerializeField]
            [Tooltip("壁の上側にあたったとき、この中から1つを出す。")]
            private EffectKind[] _effects = Array.Empty<EffectKind>();

            /// <summary>
            /// 割り当てた花。
            /// </summary>
            public Sprite[] Flowers
            {
                get { return _flowers; }
            }

            /// <summary>
            /// 割り当てた演出。
            /// </summary>
            public EffectKind[] Effects
            {
                get { return _effects; }
            }

            /// <summary>
            /// 同じテクスチャか、同じ名前の背景か。
            /// </summary>
            /// <param name="current">いま映している背景。</param>
            /// <returns>この枠の対象なら true。</returns>
            public bool Matches(Texture2D current)
            {
                if (current == null || _background == null)
                {
                    return false;
                }

                if (current == _background)
                {
                    return true;
                }

                return current.name == _background.name;
            }
        }
    }
}
