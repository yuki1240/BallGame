using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// 背景にゆっくり降る雪。画面の下まで落ちたら上へ戻して繰り返します（飾りなので破棄しません）。
    /// </summary>
    public sealed class SnowFall : MonoBehaviour
    {
        private sealed class Flake
        {
            public RectTransform Rect;
            public float Speed;
            public float SwayPhase;
            public float BaseX;
        }

        private readonly List<Flake> flakes = new List<Flake>();
        private readonly System.Random random = new System.Random(2026);

        /// <summary>
        /// 雪を作ります。
        /// </summary>
        /// <param name="count">粒の数</param>
        public void Build(int count)
        {
            for (int i = 0; i < count; i++)
            {
                flakes.Add(CreateFlake((float)random.NextDouble() * UiFactory.ReferenceResolution.y));
            }
        }

        private Flake CreateFlake(float y)
        {
            RectTransform rect = UiFactory.CreateRect(transform, "Snow");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            float size = 8f + (float)random.NextDouble() * 14f;
            rect.sizeDelta = new Vector2(size, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = EffectSprites.Get(EffectShape.SoftCircle);
            image.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * (float)random.NextDouble());
            image.raycastTarget = false;
            Flake flake = new Flake
            {
                Rect = rect,
                Speed = 30f + (float)random.NextDouble() * 50f,
                SwayPhase = (float)random.NextDouble() * Mathf.PI * 2f,
                BaseX = (float)random.NextDouble() * UiFactory.ReferenceResolution.x
            };
            rect.anchoredPosition = new Vector2(flake.BaseX, y);
            return flake;
        }

        private void Update()
        {
            foreach (Flake flake in flakes)
            {
                MoveFlake(flake, Time.deltaTime);
            }
        }

        private void MoveFlake(Flake flake, float deltaSeconds)
        {
            Vector2 position = flake.Rect.anchoredPosition;
            position.y -= flake.Speed * deltaSeconds;
            if (position.y < -20f)
            {
                position.y = UiFactory.ReferenceResolution.y + 20f;
            }

            flake.SwayPhase += deltaSeconds;
            position.x = flake.BaseX + Mathf.Sin(flake.SwayPhase) * 25f;
            flake.Rect.anchoredPosition = position;
        }
    }
}
