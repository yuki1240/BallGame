using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 背景画像1枚ぶんの花と演出の割り当て。
    /// </summary>
    [Serializable]
    public sealed class BackgroundTheme
    {
        /// <summary>対象の背景画像の名前（空なら並び順で対応させる）。</summary>
        [Tooltip("対象の背景画像のファイル名（拡張子なし）。空にすると、リストの順番＝背景の名前順で対応させます。")]
        public string backgroundName = string.Empty;

        /// <summary>この背景で使う花（Assets/Textures/Flowers の Sprite）。</summary>
        [Tooltip("この背景で使う花。Assets/Textures/Flowers の Sprite を複数入れられます。")]
        public List<Sprite> flowers = new List<Sprite>();

        /// <summary>この背景で使う演出。空なら既定の3種。</summary>
        [Tooltip("この背景で使う演出。空にすると、背景1は輪・閃光・粒、背景2は二重・波紋・十字、背景3は渦・花びら・閃光粒になります。")]
        public List<EffectKind> effects = new List<EffectKind>();
    }

    /// <summary>
    /// Game の演出設定。Game シーンの起動オブジェクトから割り当てます。
    /// </summary>
    [CreateAssetMenu(fileName = "GameEffectSettings", menuName = "BallGame/演出設定")]
    public sealed class GameEffectSettings : ScriptableObject
    {
        /// <summary>花と演出の境目（壁座標の縦、0〜1）。これより下は花、上は演出。</summary>
        [Range(0f, 1f)]
        [Tooltip("花と演出の境目（壁座標の縦 0〜1）。これより下に当たると花、上だと演出。")]
        public float flowerBoundary = 0.5f;

        /// <summary>同時に出せる数。</summary>
        [Min(1)]
        public int maxConcurrent = 24;

        /// <summary>演出の色。</summary>
        public Color effectColor = new Color(0.62f, 0.32f, 0.95f, 1f);

        /// <summary>演出の表示倍率。</summary>
        [Min(0.1f)]
        public float effectScale = 1.25f;

        /// <summary>演出の再生時間（秒）。</summary>
        [Min(0.1f)]
        public float effectDuration = 1f;

        /// <summary>花の縦の大きさ（Canvas 上の画素）。横は画像の比率に合わせます。</summary>
        [Min(10f)]
        public float flowerHeight = 360f;

        /// <summary>花が伸びきるまでの秒数。</summary>
        [Min(0.1f)]
        public float flowerGrowSeconds = 1f;

        /// <summary>花が出てから消えきるまでの表示時間（秒）。伸びる時間と消える時間を含みます。</summary>
        [Min(0.1f)]
        [Tooltip("花が出てから消えきるまでの時間（秒）。伸びる時間と消える時間を含みます。")]
        public float flowerDisplaySeconds = 3f;

        /// <summary>消えるまでの秒数。</summary>
        [Min(0.05f)]
        public float flowerFadeSeconds = 0.5f;

        /// <summary>背景ごとの割り当て。</summary>
        public List<BackgroundTheme> themes = new List<BackgroundTheme>();

        /// <summary>
        /// 伸びきってから消え始めるまでの秒数を返します（表示時間 − 伸びる時間 − 消える時間、0 未満にはしない）。
        /// </summary>
        /// <returns>秒</returns>
        public float GetFlowerHoldSeconds()
        {
            return Mathf.Max(0f, flowerDisplaySeconds - flowerGrowSeconds - flowerFadeSeconds);
        }

        /// <summary>
        /// 背景に対応する割り当てを探します。名前が一致するものを優先し、無ければ並び順で探します。
        /// </summary>
        /// <param name="backgroundName">背景画像の名前</param>
        /// <param name="backgroundIndex">背景の番号（0 始まり）</param>
        /// <returns>割り当て（無ければ null）</returns>
        public BackgroundTheme FindTheme(string backgroundName, int backgroundIndex)
        {
            BackgroundTheme byName = FindThemeByName(backgroundName);
            if (byName != null)
            {
                return byName;
            }

            return FindThemeByIndex(backgroundIndex);
        }

        private BackgroundTheme FindThemeByName(string backgroundName)
        {
            if (string.IsNullOrEmpty(backgroundName) || themes == null)
            {
                return null;
            }

            foreach (BackgroundTheme theme in themes)
            {
                if (theme != null && string.Equals(theme.backgroundName, backgroundName, StringComparison.OrdinalIgnoreCase))
                {
                    return theme;
                }
            }

            return null;
        }

        private BackgroundTheme FindThemeByIndex(int backgroundIndex)
        {
            if (themes == null || backgroundIndex < 0 || backgroundIndex >= themes.Count)
            {
                return null;
            }

            BackgroundTheme theme = themes[backgroundIndex];
            bool isNamedForAnother = theme != null && !string.IsNullOrEmpty(theme.backgroundName);
            return isNamedForAnother ? null : theme;
        }
    }
}
