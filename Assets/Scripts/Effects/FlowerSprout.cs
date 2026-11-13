using UnityEngine;
using UnityEngine.UI;

namespace WallBurst.Effects
{
    /// <summary>
    /// 当たった位置を根元にして、花の画像を下からにょきっと伸ばす。
    /// </summary>
    public sealed class FlowerSprout : MonoBehaviour
    {
        private const float Lifetime = 1f;

        private Image _image;
        private float _age;

        /// <summary>
        /// 花を表示し、縮んだ状態から伸ばし始める。
        /// </summary>
        /// <param name="image">このオブジェクトの Image。</param>
        /// <param name="sprite">出す花。</param>
        public void Play(Image image, Sprite sprite)
        {
            _image = image;
            _image.sprite = sprite;
            _image.preserveAspect = true;
            _image.raycastTarget = false;
            _image.color = Color.white;
            transform.localScale = new Vector3(0.2f, 0.02f, 1f);
        }

        /// <summary>
        /// 伸びて、最後に少し戻して消える。
        /// </summary>
        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Lifetime;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float scale = SproutScale(t);
            transform.localScale = new Vector3(scale, scale, 1f);
            Color color = _image.color;
            color.a = t < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f);
            _image.color = color;
        }

        /// <summary>
        /// 0.7秒で少し大きくなり、その後 1 に戻る。
        /// </summary>
        /// <param name="t">0〜1 の経過。</param>
        /// <returns>表示倍率。</returns>
        private static float SproutScale(float t)
        {
            if (t < 0.7f)
            {
                float rise = Mathf.Sin((t / 0.7f) * Mathf.PI * 0.5f);
                return Mathf.Lerp(0.02f, 1.08f, rise);
            }

            return Mathf.Lerp(1.08f, 1f, (t - 0.7f) / 0.3f);
        }
    }
}
