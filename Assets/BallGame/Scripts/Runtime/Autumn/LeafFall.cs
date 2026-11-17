using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// 背景にゆっくり落ちてくる秋の葉。画面の下まで落ちたら上へ戻して繰り返します（飾りなので破棄しません）。
    /// </summary>
    public sealed class LeafFall : MonoBehaviour
    {
        private static readonly Color[] Colors =
        {
            new Color(0.86f, 0.18f, 0.08f, 0.9f),
            new Color(0.95f, 0.5f, 0.08f, 0.9f),
            new Color(0.98f, 0.76f, 0.15f, 0.9f)
        };

        private sealed class Leaf
        {
            public RectTransform Rect;
            public float Speed;
            public float SwayPhase;
            public float SwayWidth;
            public float BaseX;
            public float Spin;
        }

        private readonly List<Leaf> leaves = new List<Leaf>();
        private readonly System.Random random = new System.Random(1117);

        /// <summary>
        /// 葉を作ります。
        /// </summary>
        /// <param name="count">葉の数</param>
        public void Build(int count)
        {
            for (int i = 0; i < count; i++)
            {
                leaves.Add(CreateLeaf((float)random.NextDouble() * UiFactory.ReferenceResolution.y));
            }
        }

        private Leaf CreateLeaf(float y)
        {
            RectTransform rect = UiFactory.CreateRect(transform, "FallingLeaf");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            float size = 22f + (float)random.NextDouble() * 26f;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 360f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = EffectSprites.Get(EffectShape.Petal);
            image.color = Colors[random.Next(Colors.Length)];
            image.raycastTarget = false;
            Leaf leaf = new Leaf
            {
                Rect = rect,
                Speed = 38f + (float)random.NextDouble() * 55f,
                SwayPhase = (float)random.NextDouble() * Mathf.PI * 2f,
                SwayWidth = 30f + (float)random.NextDouble() * 50f,
                BaseX = (float)random.NextDouble() * UiFactory.ReferenceResolution.x,
                Spin = ((float)random.NextDouble() * 2f - 1f) * 60f
            };
            rect.anchoredPosition = new Vector2(leaf.BaseX, y);
            return leaf;
        }

        private void Update()
        {
            foreach (Leaf leaf in leaves)
            {
                MoveLeaf(leaf, Time.deltaTime);
            }
        }

        private static void MoveLeaf(Leaf leaf, float deltaSeconds)
        {
            Vector2 position = leaf.Rect.anchoredPosition;
            position.y -= leaf.Speed * deltaSeconds;
            if (position.y < -30f)
            {
                position.y = UiFactory.ReferenceResolution.y + 30f;
            }

            leaf.SwayPhase += deltaSeconds;
            position.x = leaf.BaseX + Mathf.Sin(leaf.SwayPhase * 0.9f) * leaf.SwayWidth;
            leaf.Rect.anchoredPosition = position;
            leaf.Rect.Rotate(0f, 0f, leaf.Spin * deltaSeconds);
        }
    }
}
