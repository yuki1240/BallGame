using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// 操作パネルに置く「固定モード」の入り切りボタン（F5 キーと同じ働き）。
    /// 操作パネルといっしょに、スペースキーで隠れます。
    /// </summary>
    public static class HoldButton
    {
        private static readonly Color OnColor = new Color(0.85f, 0.45f, 0.1f, 1f);
        private static readonly Color OffColor = new Color(0.25f, 0.28f, 0.35f, 1f);

        /// <summary>
        /// ボタンを作ります。
        /// </summary>
        /// <param name="parent">置き場所（操作パネル）</param>
        /// <param name="x">左からの位置</param>
        /// <param name="y">上からの位置</param>
        /// <param name="onClick">押したときの処理</param>
        /// <returns>Button</returns>
        public static Button Create(Transform parent, float x, float y, UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(parent, "固定モード", 26, onClick);
            UiFactory.PlaceTopLeft((RectTransform)button.transform, x, y, 280f, 70f);
            Refresh(button);
            return button;
        }

        /// <summary>
        /// 今の状態（ON / OFF）に合わせて、文字と色を変えます。
        /// </summary>
        /// <param name="button">ボタン</param>
        public static void Refresh(Button button)
        {
            if (button == null)
            {
                return;
            }

            bool holding = SceneHold.IsHolding;
            UiFactory.SetButtonLabel(button, holding ? "固定モード：ON" : "固定モード：OFF");
            UiFactory.SetButtonColor(button, holding ? OnColor : OffColor);
        }
    }
}
