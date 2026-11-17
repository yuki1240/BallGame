using UnityEngine;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// シャボン玉が割れたときの演出です。広がって消える輪、飛び散る水しぶき、「パチン！」の文字が出ます。
    /// 1回ぶんをこの部品が持ち、終わると自分で破棄します（使い回しはしません）。
    /// </summary>
    public sealed class BubblePop : MonoBehaviour
    {
        /// <summary>演出の長さ（秒）。</summary>
        public const float Lifetime = 0.6f;

        /// <summary>同時に出してよい演出の数の上限（たくさん割れても重くならないようにする）。</summary>
        public const int MaxActive = 24;

        /// <summary>1回の演出で飛び散る水しぶきの数。</summary>
        public const int DropletCount = 9;

        private static readonly Color LabelColor = new Color(1f, 0.45f, 0.1f, 1f);

        private static int activeCount;

        private RectTransform ring;
        private Image ringImage;
        private float startDiameter;
        private Color tint;
        private RectTransform[] droplets;
        private Image[] dropletImages;
        private Vector2[] directions;
        private float radius;
        private RectTransform label;
        private Text labelText;
        private float age;

        /// <summary>今出ている演出の数。</summary>
        public static int ActiveCount
        {
            get { return activeCount; }
        }

        /// <summary>
        /// 割れた演出を出します。
        /// </summary>
        /// <param name="parent">演出を置く親（左下が原点の 1920×1080 の Canvas の中）</param>
        /// <param name="bubble">割れたシャボン玉</param>
        /// <param name="random">ばらつきを決める乱数</param>
        /// <param name="showLabel">「パチン！」の文字を出すか</param>
        public static void Spawn(Transform parent, Bubble bubble, System.Random random, bool showLabel)
        {
            if (activeCount >= MaxActive)
            {
                return;
            }

            RectTransform root = UiFactory.CreateRect(parent, "BubblePop");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = bubble.Position;
            root.sizeDelta = Vector2.zero;
            BubblePop pop = root.gameObject.AddComponent<BubblePop>();
            pop.Build(root, bubble, random, showLabel);
        }

        private void Build(RectTransform root, Bubble bubble, System.Random random, bool showLabel)
        {
            radius = bubble.Radius;
            tint = BubbleSprites.GetTint(bubble.ColorIndex);
            startDiameter = bubble.Radius * 2f;
            ring = CreateImage(root, "Ring", BubbleSprites.GetBubble(), out ringImage);
            ring.sizeDelta = new Vector2(startDiameter, startDiameter);
            droplets = new RectTransform[DropletCount];
            dropletImages = new Image[DropletCount];
            directions = new Vector2[DropletCount];
            for (int i = 0; i < DropletCount; i++)
            {
                droplets[i] = CreateImage(root, "Droplet", BubbleSprites.GetDroplet(), out dropletImages[i]);
                float size = 18f + (float)random.NextDouble() * 22f;
                droplets[i].sizeDelta = new Vector2(size, size);
                float angle = (i + (float)random.NextDouble() * 0.8f) / DropletCount * Mathf.PI * 2f;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            if (showLabel)
            {
                labelText = UiFactory.CreateKidsText(root, "パチン！", 64, LabelColor);
                label = labelText.rectTransform;
                label.anchorMin = new Vector2(0.5f, 0.5f);
                label.anchorMax = new Vector2(0.5f, 0.5f);
                label.sizeDelta = new Vector2(420f, 100f);
                labelText.raycastTarget = false;
            }

            activeCount++;
        }

        private static RectTransform CreateImage(Transform parent, string name, Sprite sprite, out Image image)
        {
            RectTransform rect = UiFactory.CreateRect(parent, name);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return rect;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Lifetime);
            float eased = 1f - (1f - t) * (1f - t);
            float diameter = startDiameter * (1f + 0.45f * eased);
            ring.sizeDelta = new Vector2(diameter, diameter);
            ringImage.color = new Color(tint.r, tint.g, tint.b, 1f - t);
            for (int i = 0; i < droplets.Length; i++)
            {
                droplets[i].anchoredPosition = directions[i] * (radius * (0.8f + 0.9f * eased));
                dropletImages[i].color = new Color(tint.r, tint.g, tint.b, 1f - t);
            }

            if (label != null)
            {
                label.anchoredPosition = new Vector2(0f, 30f + 90f * eased);
                labelText.color = new Color(LabelColor.r, LabelColor.g, LabelColor.b, 1f - Mathf.Clamp01((t - 0.4f) / 0.6f));
            }

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
