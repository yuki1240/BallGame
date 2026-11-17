using UnityEngine;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// 当たった位置を根元にして花をにょきっと伸ばし、しばらく見せてから消して破棄します。
    /// </summary>
    public sealed class FlowerPlayer : MonoBehaviour
    {
        private Image image;
        private float growSeconds;
        private float holdSeconds;
        private float fadeSeconds;
        private float elapsed;

        /// <summary>
        /// 花を出して伸ばし始めます。縦は height、横は画像の比率に合わせます。
        /// </summary>
        /// <param name="sprite">花の Sprite</param>
        /// <param name="height">縦の大きさ</param>
        /// <param name="grow">伸びきるまでの秒数</param>
        /// <param name="hold">伸びたあと見せる秒数</param>
        /// <param name="fade">消えるまでの秒数</param>
        public void Play(Sprite sprite, float height, float grow, float hold, float fade)
        {
            growSeconds = Mathf.Max(0.05f, grow);
            holdSeconds = Mathf.Max(0f, hold);
            fadeSeconds = Mathf.Max(0.05f, fade);
            image = gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            RectTransform rect = (RectTransform)transform;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(height * GetAspect(sprite), height);
            ApplyGrowth(0f);
        }

        /// <summary>
        /// Sprite の横÷縦を返します。
        /// </summary>
        /// <param name="sprite">Sprite</param>
        /// <returns>横÷縦（不明なら 1）</returns>
        public static float GetAspect(Sprite sprite)
        {
            if (sprite == null || sprite.rect.height <= 0f)
            {
                return 1f;
            }

            return sprite.rect.width / sprite.rect.height;
        }

        /// <summary>
        /// 伸び具合（0〜1）から縦横の拡大率を返します。少し行き過ぎてから戻る「にょきっ」とした動きです。
        /// </summary>
        /// <param name="progress">伸び具合（0〜1）</param>
        /// <returns>拡大率</returns>
        public static Vector3 EvaluateGrowthScale(float progress)
        {
            float t = Mathf.Clamp01(progress);
            const float overshoot = 1.9f;
            float inverse = t - 1f;
            float vertical = 1f + (overshoot + 1f) * inverse * inverse * inverse + overshoot * inverse * inverse;
            float horizontal = Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, t));
            return new Vector3(horizontal, Mathf.Max(0f, vertical), 1f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed < growSeconds)
            {
                ApplyGrowth(elapsed / growSeconds);
                return;
            }

            ApplyGrowth(1f);
            float fadeStart = growSeconds + holdSeconds;
            if (elapsed < fadeStart)
            {
                return;
            }

            float fade = (elapsed - fadeStart) / fadeSeconds;
            if (fade >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            ApplyAlpha(1f - fade);
        }

        private void ApplyGrowth(float progress)
        {
            transform.localScale = EvaluateGrowthScale(progress);
        }

        private void ApplyAlpha(float alpha)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
