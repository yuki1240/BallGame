using System.Collections.Generic;
using UnityEngine;

namespace WallBurst.Effects
{
    /// <summary>
    /// 壁座標へ演出を出し、終わったものや上限を超えたものを破棄する。
    /// </summary>
    public sealed class WallEffectDirector : MonoBehaviour
    {
        private const int MaxAlive = 24;
        private const float HardLifetime = 2.4f;

        private readonly List<GameObject> _alive = new List<GameObject>(MaxAlive);
        private RectTransform _root;

        private static readonly Color[] Palette =
        {
            new Color(0.35f, 0.95f, 1f, 1f),
            new Color(1f, 0.72f, 0.25f, 1f),
            new Color(1f, 0.4f, 0.72f, 1f),
            new Color(0.55f, 1f, 0.45f, 1f),
            new Color(0.75f, 0.55f, 1f, 1f)
        };

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
        /// 壁の正規化座標に、大きい演出を1つ出す。
        /// </summary>
        /// <param name="wallU">横 0〜1。</param>
        /// <param name="wallV">縦 0〜1。下が 0。</param>
        public void Play(float wallU, float wallV)
        {
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
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(760f, 760f);
            rect.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));

            WallBurstEffect effect = effectObject.AddComponent<WallBurstEffect>();
            effect.Play(Random.Range(0, 4), Palette[Random.Range(0, Palette.Length)]);

            EffectAutoDestroy watchdog = effectObject.AddComponent<EffectAutoDestroy>();
            watchdog.Arm(HardLifetime);
            _alive.Add(effectObject);
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
