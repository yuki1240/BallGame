namespace BallGame
{
    /// <summary>
    /// 「いまの場面をずっと続ける」固定モード（F5 キーで入り切り）。
    /// 場面の切り替え・カウントダウンをせず、エフェクトや飾りも出さず、ボールが検知されている場所だけを赤い輪で見せます（検知の確かめ用）。
    /// 場面をまたいで状態を覚えるため、静的に持ちます。
    /// </summary>
    public static class SceneHold
    {
        /// <summary>固定モードでツリーを完成させるまでに当てる回数。</summary>
        public const int TreeGoal = 1000;

        /// <summary>固定モードで終わりのない場面（草原など）に使う回数。</summary>
        public const int EndlessGoal = 1000000;

        /// <summary>固定モードか。</summary>
        public static bool IsHolding { get; private set; }

        /// <summary>
        /// 固定モードを入れたり切ったりします。
        /// </summary>
        /// <returns>切り替えたあとの状態</returns>
        public static bool Toggle()
        {
            IsHolding = !IsHolding;
            return IsHolding;
        }

        /// <summary>
        /// 今の状態での、完成までに当てる回数を返します。
        /// </summary>
        /// <param name="holdGoal">固定モードのときの回数</param>
        /// <returns>回数</returns>
        public static int GetGoal(int holdGoal)
        {
            return IsHolding ? holdGoal : ChristmasFlow.DefaultGoal;
        }

        /// <summary>
        /// 操作する人向けの、固定モードの案内を返します（固定モードでなければ空）。
        /// </summary>
        /// <returns>案内</returns>
        public static string GetOperatorHint()
        {
            return IsHolding ? "　【固定モード：場面を替えません（F5で解除）】" : string.Empty;
        }
    }
}
