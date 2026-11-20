using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// 画面全体に、いつも雪を降らせます（クリスマスツリーのシーンで使います）。
    /// <list type="bullet">
    /// <item>雪のつぶは、ふちのやわらかい白い丸（<see cref="ProceduralSprites.Circle"/>）です。</item>
    /// <item>小さい雪はゆっくり、大きい雪は少し速く落ちて、左右にゆらゆら揺れます（奥行きがあるように見せるため）。</item>
    /// <item>下まで落ちた雪は、また画面の上から降ってきます。つぶの数は増えません。</item>
    /// <item>クリックやボールの当たりのじゃまにならないよう、雪はクリックを受け取りません。</item>
    /// </list>
    /// </summary>
    public sealed class SnowFall : MonoBehaviour
    {
        /// <summary>雪のつぶの数の決まり。</summary>
        public const int DefaultCount = 90;

        private const float MinSize = 6f;
        private const float MaxSize = 22f;
        private const float MinSpeed = 35f;
        private const float MaxSpeed = 110f;

        private sealed class Flake
        {
            public RectTransform Rect;
            public float Speed;
            public float SwayWidth;
            public float SwaySpeed;
            public float SwayPhase;
            public float BaseX;
            public float Y;
        }

        private readonly List<Flake> flakes = new List<Flake>();
        private readonly System.Random random = new System.Random();
        private RectTransform area;

        /// <summary>
        /// 雪を降らせる Canvas を作ります。
        /// </summary>
        /// <param name="sortingOrder">Canvas の重なりの順番（エフェクトやデバッグ表示より下にします）</param>
        /// <param name="count">雪のつぶの数</param>
        /// <returns>作った雪</returns>
        public static SnowFall Create(int sortingOrder, int count = DefaultCount)
        {
            Canvas canvas = Ui.CreateCanvas("雪", sortingOrder);

            // 雪はクリックを受け取らないので、この Canvas のクリック判定は外す
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }

            SnowFall snow = canvas.gameObject.AddComponent<SnowFall>();
            snow.Build(count);
            return snow;
        }

        private void Build(int count)
        {
            area = (RectTransform)transform;
            for (int i = 0; i < count; i++)
            {
                GameObject flakeObject = new GameObject("雪 " + (i + 1), typeof(RectTransform));
                flakeObject.transform.SetParent(transform, false);
                Image image = flakeObject.AddComponent<Image>();
                image.sprite = ProceduralSprites.Circle;
                image.raycastTarget = false;

                // 0 なら遠く（小さく・ゆっくり・うすい）、1 なら近く（大きく・速く・こい）
                float near = (float)random.NextDouble();
                near *= near;
                float size = Mathf.Lerp(MinSize, MaxSize, near);
                image.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 0.95f, near));

                RectTransform rect = image.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);

                Flake flake = new Flake
                {
                    Rect = rect,
                    Speed = Mathf.Lerp(MinSpeed, MaxSpeed, near) * Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble()),
                    SwayWidth = Mathf.Lerp(10f, 40f, (float)random.NextDouble()),
                    SwaySpeed = Mathf.Lerp(0.5f, 1.4f, (float)random.NextDouble()),
                    SwayPhase = (float)random.NextDouble() * Mathf.PI * 2f,
                    BaseX = (float)random.NextDouble() * Ui.Reference.x,
                    Y = (float)random.NextDouble() * Ui.Reference.y
                };
                flakes.Add(flake);
            }

            Move(0f);
        }

        private void Update()
        {
            Move(Time.deltaTime);
        }

        private void Move(float deltaSeconds)
        {
            Vector2 size = area != null && area.rect.width > 1f ? area.rect.size : Ui.Reference;
            foreach (Flake flake in flakes)
            {
                flake.Y -= flake.Speed * deltaSeconds;
                float margin = flake.Rect.sizeDelta.y;
                if (flake.Y < -margin)
                {
                    // 下まで落ちたら、画面の上の外から、横の位置を変えてまた降らせる
                    flake.Y = size.y + margin;
                    flake.BaseX = (float)random.NextDouble() * size.x;
                }

                float x = flake.BaseX + Mathf.Sin(Time.time * flake.SwaySpeed + flake.SwayPhase) * flake.SwayWidth;
                flake.Rect.anchoredPosition = new Vector2(x, flake.Y);
            }
        }
    }
}
