using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// エフェクトを出す係です（シーンに1つ）。長い時間動かしても重くならないように、次を守ります。
    /// <list type="bullet">
    /// <item>エフェクトは再生が終わったら自分で消える</item>
    /// <item>念のため、どんなエフェクトも <see cref="MaxLifetimeSeconds"/> 秒で必ず消す</item>
    /// <item>同時に出せるのは <see cref="MaxActive"/> 個まで。超えたら古いものから消す</item>
    /// <item>シーンが替わると、この係ごと全部消える</item>
    /// </list>
    /// </summary>
    public sealed class EffectSpawner : MonoBehaviour
    {
        /// <summary>同時に出せるエフェクトの数。</summary>
        public const int MaxActive = 30;

        /// <summary>どんなエフェクトも、この秒数で必ず消す。</summary>
        public const float MaxLifetimeSeconds = 15f;

        private static readonly Color[] Palette =
        {
            new Color(1f, 0.45f, 0.45f),
            new Color(1f, 0.8f, 0.3f),
            new Color(0.5f, 1f, 0.5f),
            new Color(0.45f, 0.8f, 1f),
            new Color(0.85f, 0.55f, 1f),
            new Color(1f, 0.6f, 0.85f)
        };

        private readonly List<GameObject> active = new List<GameObject>();
        private readonly System.Random random = new System.Random();
        private Canvas canvas;

        /// <summary>今出ているエフェクトの数。</summary>
        public int ActiveCount
        {
            get
            {
                Prune();
                return active.Count;
            }
        }

        /// <summary>
        /// 係を作ります。
        /// </summary>
        /// <param name="sortingOrder">描画順</param>
        /// <returns>係</returns>
        public static EffectSpawner Create(int sortingOrder)
        {
            Canvas canvas = Ui.CreateCanvas("エフェクト", sortingOrder);
            EffectSpawner spawner = canvas.gameObject.AddComponent<EffectSpawner>();
            spawner.canvas = canvas;
            return spawner;
        }

        /// <summary>
        /// 画面の位置（左下が原点の画素）に、ランダムな色のエフェクトを出します。
        /// </summary>
        /// <param name="screenPosition">画面の位置</param>
        /// <param name="scale">大きさの倍率</param>
        public void Spawn(Vector2 screenPosition, float scale = 1f)
        {
            Spawn(screenPosition, Palette[random.Next(Palette.Length)], scale, 0.9f);
        }

        /// <summary>
        /// 画面の位置に、色を決めてエフェクトを出します。
        /// </summary>
        /// <param name="screenPosition">画面の位置</param>
        /// <param name="color">色</param>
        /// <param name="scale">大きさの倍率</param>
        /// <param name="seconds">再生時間</param>
        public void Spawn(Vector2 screenPosition, Color color, float scale, float seconds)
        {
            Prune();
            while (active.Count >= MaxActive)
            {
                Destroy(active[0]);
                active.RemoveAt(0);
            }

            GameObject effect = new GameObject("Effect", typeof(RectTransform));
            effect.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)effect.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.position = screenPosition;
            EffectPlayer player = effect.AddComponent<EffectPlayer>();
            player.Play(color, scale, seconds, random);
            Destroy(effect, MaxLifetimeSeconds);
            active.Add(effect);
        }

        private void Prune()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] == null)
                {
                    active.RemoveAt(i);
                }
            }
        }
    }
}
