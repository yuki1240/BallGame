using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WallBurst.Effects
{
    /// <summary>
    /// 壁座標へ演出を出し、終わったものや上限を超えたものを破棄する。
    /// </summary>
    public sealed class WallEffectDirector : MonoBehaviour
    {
        private const int MaxAlive = 24;
        private const float HardLifetime = 1.05f;

        private readonly List<GameObject> _alive = new List<GameObject>(MaxAlive);
        private RectTransform _root;

        private static readonly Color EffectColor = new Color(0.56f, 0.18f, 0.95f, 1f);

        /// <summary>
        /// 自分の矩形を演出の親にする。
        /// </summary>
        private void Awake()
        {
            _root = GetComponent<RectTransform>();
        }

        /// <summary>
        /// 破棄済みの参照を外す。
        /// </summary>
        private void LateUpdate()
        {
            Prune();
        }

        /// <summary>
        /// 壁の正規化座標に、指定した演出を1つ出す。
        /// </summary>
        /// <param name="wallU">横 0〜1。</param>
        /// <param name="wallV">縦 0〜1。下が 0。</param>
        /// <param name="kind">出す演出。</param>
        /// <returns>置いた矩形。出せないときは null。</returns>
        public RectTransform Play(float wallU, float wallV, int kind)
        {
            RectTransform rect = CreateHost(wallU, wallV, new Vector2(0.5f, 0.5f), new Vector2(950f, 950f));
            if (rect == null)
            {
                return null;
            }

            rect.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            WallBurstEffect effect = rect.gameObject.AddComponent<WallBurstEffect>();
            effect.Play(kind, EffectColor);
            Arm(rect.gameObject);
            return rect;
        }

        /// <summary>
        /// 当たった位置を根元に、花の画像をにょきっと出す。
        /// </summary>
        /// <param name="wallU">横 0〜1。</param>
        /// <param name="wallV">縦 0〜1。下が 0。</param>
        /// <param name="sprite">出す花。</param>
        /// <returns>置いた矩形。出せないときは null。</returns>
        public RectTransform PlayFlower(float wallU, float wallV, Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }

            RectTransform rect = CreateHost(wallU, wallV, new Vector2(0.5f, 0f), FlowerSize(sprite));
            if (rect == null)
            {
                return null;
            }

            Image image = rect.gameObject.AddComponent<Image>();
            FlowerSprout sprout = rect.gameObject.AddComponent<FlowerSprout>();
            sprout.Play(image, sprite);
            Arm(rect.gameObject);
            return rect;
        }

        /// <summary>
        /// 演出を置く矩形を作り、同時表示の上限を超えた古いものを捨てる。
        /// </summary>
        private RectTransform CreateHost(float wallU, float wallV, Vector2 pivot, Vector2 size)
        {
            if (_root == null)
            {
                return null;
            }

            Prune();
            while (_alive.Count >= MaxAlive)
            {
                Destroy(_alive[0]);
                _alive.RemoveAt(0);
            }

            GameObject effectObject = new GameObject("HitEffect");
            effectObject.transform.SetParent(_root, false);
            RectTransform rect = effectObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(wallU, wallV);
            rect.anchorMax = new Vector2(wallU, wallV);
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            _alive.Add(effectObject);
            return rect;
        }

        /// <summary>
        /// 再生が終わらなくても、一定時間で破棄する。
        /// </summary>
        private static void Arm(GameObject effectObject)
        {
            EffectAutoDestroy watchdog = effectObject.AddComponent<EffectAutoDestroy>();
            watchdog.Arm(HardLifetime);
        }

        /// <summary>
        /// 花の縦を 360 にし、横は画像の比率に合わせる。
        /// </summary>
        private static Vector2 FlowerSize(Sprite sprite)
        {
            const float height = 360f;
            float width = height;
            if (sprite.rect.height > 1f)
            {
                width = height * (sprite.rect.width / sprite.rect.height);
            }

            return new Vector2(width, height);
        }

        /// <summary>
        /// すでに破棄された参照を一覧から外す。
        /// </summary>
        private void Prune()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null)
                {
                    _alive.RemoveAt(i);
                }
            }
        }
    }
}
