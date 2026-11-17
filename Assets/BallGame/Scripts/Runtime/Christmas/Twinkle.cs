using UnityEngine;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// 画像の明るさをゆっくり上げ下げして、電球がぴかぴか光っているように見せます。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class Twinkle : MonoBehaviour
    {
        private Image image;
        private Color baseColor;
        private float phase;
        private float speed = 4f;

        /// <summary>
        /// 光り方を決めます。
        /// </summary>
        /// <param name="startPhase">光り始めのずれ（電球ごとにばらばらに光らせるため）</param>
        /// <param name="blinkSpeed">光る速さ</param>
        public void Setup(float startPhase, float blinkSpeed)
        {
            image = GetComponent<Image>();
            baseColor = image.color;
            phase = startPhase;
            speed = blinkSpeed;
        }

        /// <summary>
        /// 時刻から明るさの割合（0.35〜1）を返します。
        /// </summary>
        /// <param name="time">時刻（秒）</param>
        /// <param name="startPhase">ずれ</param>
        /// <param name="blinkSpeed">速さ</param>
        /// <returns>明るさの割合</returns>
        public static float EvaluateBrightness(float time, float startPhase, float blinkSpeed)
        {
            return 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(time * blinkSpeed + startPhase));
        }

        private void Update()
        {
            if (image == null)
            {
                return;
            }

            Color color = baseColor;
            color.a = baseColor.a * EvaluateBrightness(Time.time, phase, speed);
            image.color = color;
        }
    }
}
