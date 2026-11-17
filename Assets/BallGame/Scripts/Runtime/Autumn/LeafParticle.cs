using UnityEngine;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// ボールが当たった所から、赤・だいだい・黄色の葉がぱっと舞い上がって落ちていく演出です。
    /// 1枚ずつがこの部品を持ち、消えるときに自分で破棄します（使い回しはしません）。
    /// </summary>
    public sealed class LeafParticle : MonoBehaviour
    {
        /// <summary>葉が見えている秒数。</summary>
        public const float Lifetime = 1.5f;

        /// <summary>同時に出してよい葉の数の上限（たくさん当たっても重くならないようにする）。</summary>
        public const int MaxActive = 140;

        private static readonly Color[] LeafColors =
        {
            new Color(0.86f, 0.14f, 0.08f, 1f),
            new Color(0.95f, 0.42f, 0.07f, 1f),
            new Color(0.98f, 0.72f, 0.12f, 1f),
            new Color(1f, 0.88f, 0.28f, 1f),
            new Color(0.7f, 0.2f, 0.08f, 1f)
        };

        private static int activeCount;

        private RectTransform rect;
        private Image image;
        private Color baseColor;
        private Vector2 velocity;
        private float spin;
        private float age;

        /// <summary>今出ている葉の数。</summary>
        public static int ActiveCount
        {
            get { return activeCount; }
        }

        /// <summary>
        /// 葉をまとめて舞い上がらせます。
        /// </summary>
        /// <param name="parent">葉を置く親（左下が原点の 1920×1080 の Canvas の中）</param>
        /// <param name="canvasPosition">葉が出てくる位置（Canvas 上、左下が原点）</param>
        /// <param name="random">ばらつきを決める乱数</param>
        /// <param name="count">葉の数</param>
        public static void Burst(Transform parent, Vector2 canvasPosition, System.Random random, int count)
        {
            for (int i = 0; i < count && activeCount < MaxActive; i++)
            {
                Spawn(parent, canvasPosition, random);
            }
        }

        private static void Spawn(Transform parent, Vector2 position, System.Random random)
        {
            RectTransform rect = UiFactory.CreateRect(parent, "Leaf");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            float size = 34f + (float)random.NextDouble() * 34f;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 360f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = EffectSprites.Get(EffectShape.Petal);
            image.raycastTarget = false;
            LeafParticle particle = rect.gameObject.AddComponent<LeafParticle>();
            particle.Setup(rect, image, LeafColors[random.Next(LeafColors.Length)], random);
        }

        private void Setup(RectTransform leafRect, Image leafImage, Color color, System.Random random)
        {
            rect = leafRect;
            image = leafImage;
            baseColor = color;
            image.color = color;
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float speed = 220f + (float)random.NextDouble() * 420f;
            velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Abs(Mathf.Sin(angle)) * speed + 160f);
            spin = ((float)random.NextDouble() * 2f - 1f) * 420f;
            activeCount++;
        }

        private void Update()
        {
            age += Time.deltaTime;
            velocity.y -= 780f * Time.deltaTime;
            velocity.x *= 1f - 0.9f * Time.deltaTime;
            rect.anchoredPosition += velocity * Time.deltaTime;
            rect.Rotate(0f, 0f, spin * Time.deltaTime);
            float fade = 1f - Mathf.Clamp01((age - (Lifetime - 0.5f)) / 0.5f);
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, fade);
            if (age >= Lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            activeCount = Mathf.Max(0, activeCount - 1);
        }
    }
}
