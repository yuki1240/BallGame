using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 演出と花を出します。同時に出せる数を超えたら古いものから破棄します（プールしません）。
    /// </summary>
    public sealed class EffectSpawner : MonoBehaviour
    {
        private readonly List<GameObject> active = new List<GameObject>();
        private readonly System.Random random = new System.Random();
        private GameEffectSettings settings;

        /// <summary>今出ている数。</summary>
        public int ActiveCount
        {
            get
            {
                PruneDestroyed();
                return active.Count;
            }
        }

        /// <summary>
        /// 設定を受け取ります。
        /// </summary>
        /// <param name="effectSettings">演出設定</param>
        public void Initialize(GameEffectSettings effectSettings)
        {
            settings = effectSettings;
        }

        /// <summary>
        /// 演出を出します。
        /// </summary>
        /// <param name="kind">演出の種類</param>
        /// <param name="wallUv">壁座標（左下 (0,0)、右上 (1,1)）</param>
        /// <returns>置いた演出の RectTransform</returns>
        public RectTransform SpawnEffect(EffectKind kind, Vector2 wallUv)
        {
            RectTransform rect = CreateSlot("Effect " + kind, wallUv);
            EffectPlayer player = rect.gameObject.AddComponent<EffectPlayer>();
            player.Play(EffectRecipes.Build(kind, random), settings.effectColor, settings.effectScale, settings.effectDuration);
            return rect;
        }

        /// <summary>
        /// 花を出します。当たった位置が根元になります。
        /// </summary>
        /// <param name="sprite">花の Sprite</param>
        /// <param name="wallUv">壁座標</param>
        /// <returns>置いた花の RectTransform</returns>
        public RectTransform SpawnFlower(Sprite sprite, Vector2 wallUv)
        {
            RectTransform rect = CreateSlot("Flower " + sprite.name, wallUv);
            FlowerPlayer player = rect.gameObject.AddComponent<FlowerPlayer>();
            player.Play(sprite, settings.flowerHeight, settings.flowerGrowSeconds, settings.GetFlowerHoldSeconds(), settings.flowerFadeSeconds);
            return rect;
        }

        /// <summary>
        /// 壁座標をこの部品の中の位置（左下原点の画素）に変えます。
        /// </summary>
        /// <param name="wallUv">壁座標</param>
        /// <returns>位置</returns>
        public Vector2 WallToLocal(Vector2 wallUv)
        {
            Rect area = ((RectTransform)transform).rect;
            return new Vector2(wallUv.x * area.width, wallUv.y * area.height);
        }

        private RectTransform CreateSlot(string name, Vector2 wallUv)
        {
            MakeRoom();
            RectTransform rect = UiFactory.CreateRect(transform, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = WallToLocal(wallUv);
            active.Add(rect.gameObject);
            return rect;
        }

        private void MakeRoom()
        {
            PruneDestroyed();
            int limit = Mathf.Max(1, settings.maxConcurrent);
            while (active.Count >= limit)
            {
                Destroy(active[0]);
                active.RemoveAt(0);
            }
        }

        private void PruneDestroyed()
        {
            active.RemoveAll(item => item == null);
        }
    }
}
