using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 当たった位置から花か演出かを決め、出すものを選びます。
    /// </summary>
    public static class HitReactionSelector
    {
        private static readonly EffectKind[][] DefaultEffects =
        {
            new[] { EffectKind.Ring, EffectKind.Flash, EffectKind.Particles },
            new[] { EffectKind.DoubleRing, EffectKind.Ripple, EffectKind.Cross },
            new[] { EffectKind.Swirl, EffectKind.Petals, EffectKind.FlashParticles }
        };

        /// <summary>
        /// 壁座標の縦の位置から反応を決めます。境目より下は花、境目以上は演出です。
        /// </summary>
        /// <param name="wallV">壁座標の縦（0 が下、1 が上）</param>
        /// <param name="boundary">境目</param>
        /// <returns>反応の種類</returns>
        public static HitReactionKind Classify(float wallV, float boundary)
        {
            return wallV < boundary ? HitReactionKind.Flower : HitReactionKind.Effect;
        }

        /// <summary>
        /// 演出を指定していない背景の既定の演出を返します（背景1・2・3 の順に繰り返し）。
        /// </summary>
        /// <param name="backgroundIndex">背景の番号（0 始まり）</param>
        /// <returns>既定の演出</returns>
        public static IReadOnlyList<EffectKind> GetDefaultEffects(int backgroundIndex)
        {
            int count = DefaultEffects.Length;
            int index = ((backgroundIndex % count) + count) % count;
            return DefaultEffects[index];
        }

        /// <summary>
        /// 背景で使う演出の一覧を返します。指定が無ければ既定の演出です。
        /// </summary>
        /// <param name="theme">背景の割り当て（null 可）</param>
        /// <param name="backgroundIndex">背景の番号</param>
        /// <returns>演出の一覧</returns>
        public static IReadOnlyList<EffectKind> ResolveEffects(BackgroundTheme theme, int backgroundIndex)
        {
            if (theme != null && theme.effects != null && theme.effects.Count > 0)
            {
                return theme.effects;
            }

            return GetDefaultEffects(backgroundIndex);
        }

        /// <summary>
        /// 一覧からランダムに演出を1つ選びます。
        /// </summary>
        /// <param name="effects">演出の一覧</param>
        /// <param name="random">乱数</param>
        /// <returns>選んだ演出</returns>
        public static EffectKind PickEffect(IReadOnlyList<EffectKind> effects, System.Random random)
        {
            if (effects == null || effects.Count == 0)
            {
                return EffectKind.Ring;
            }

            return effects[random.Next(effects.Count)];
        }

        /// <summary>
        /// 背景に割り当てた花からランダムに1枚選びます。
        /// </summary>
        /// <param name="theme">背景の割り当て（null 可）</param>
        /// <param name="random">乱数</param>
        /// <returns>選んだ花（割り当てが無ければ null）</returns>
        public static Sprite PickFlower(BackgroundTheme theme, System.Random random)
        {
            if (theme == null || theme.flowers == null)
            {
                return null;
            }

            List<Sprite> candidates = theme.flowers.FindAll(sprite => sprite != null);
            return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
        }
    }
}
