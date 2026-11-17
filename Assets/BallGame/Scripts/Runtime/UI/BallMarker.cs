using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// 固定モード（デバッグ）のとき、いまボールが検知されている場所を画面に赤い輪で出します。
    /// 演出（Effect）や飾りは出さず、ボールが「どこにあると見られているか」だけを確かめるための表示です。
    /// ボールが動くと、輪もいっしょに動きます。見えなくなると、短い時間のあとに消えます。
    /// </summary>
    public sealed class BallMarker : MonoBehaviour
    {
        /// <summary>輪が残る秒数。</summary>
        public const float HoldSeconds = 0.35f;

        /// <summary>同じボールとみなす距離（Canvas 上の画素）。</summary>
        public const float SameBallDistance = 140f;

        /// <summary>同時に出せる輪の数。</summary>
        public const int MaxMarkers = 8;

        private static readonly Color RingColor = new Color(1f, 0.1f, 0.1f, 1f);

        private sealed class Marker
        {
            public RectTransform Rect;
            public Image Ring;
            public Image Dot;
            public float LastSeen = float.NegativeInfinity;
        }

        private readonly List<Marker> markers = new List<Marker>();

        /// <summary>
        /// 輪を出す層を作ります。
        /// </summary>
        /// <param name="parent">親（Canvas）</param>
        /// <returns>BallMarker</returns>
        public static BallMarker Create(Transform parent)
        {
            RectTransform rect = UiFactory.CreateRect(parent, "BallMarkers（固定モードのときだけ使う）");
            UiFactory.Stretch(rect);
            BallMarker marker = rect.gameObject.AddComponent<BallMarker>();
            for (int i = 0; i < MaxMarkers; i++)
            {
                marker.markers.Add(marker.CreateMarker());
            }

            return marker;
        }

        /// <summary>
        /// ボールのいる場所に輪を出します（同じ場所のものは動かして使い回します）。
        /// </summary>
        /// <param name="wallUv">壁座標（左下 (0,0)・右上 (1,1)）</param>
        public void Mark(Vector2 wallUv)
        {
            Vector2 position = WallHitInput.ToCanvas(wallUv);
            Marker marker = FindNear(position) ?? FindOldest();
            marker.Rect.anchoredPosition = position;
            marker.LastSeen = Time.unscaledTime;
            marker.Rect.gameObject.SetActive(true);
        }

        /// <summary>
        /// 輪をすべて消します（固定モードを切ったとき）。
        /// </summary>
        public void Clear()
        {
            foreach (Marker marker in markers)
            {
                marker.LastSeen = float.NegativeInfinity;
                marker.Rect.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            foreach (Marker marker in markers)
            {
                if (!marker.Rect.gameObject.activeSelf)
                {
                    continue;
                }

                float age = Time.unscaledTime - marker.LastSeen;
                if (age >= HoldSeconds)
                {
                    marker.Rect.gameObject.SetActive(false);
                    continue;
                }

                float fade = 1f - age / HoldSeconds;
                marker.Ring.color = new Color(RingColor.r, RingColor.g, RingColor.b, fade);
                marker.Dot.color = new Color(RingColor.r, RingColor.g, RingColor.b, fade);
            }
        }

        private Marker FindNear(Vector2 position)
        {
            Marker best = null;
            float bestDistance = SameBallDistance;
            foreach (Marker marker in markers)
            {
                if (!marker.Rect.gameObject.activeSelf)
                {
                    continue;
                }

                float distance = Vector2.Distance(marker.Rect.anchoredPosition, position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = marker;
                }
            }

            return best;
        }

        private Marker FindOldest()
        {
            Marker oldest = markers[0];
            foreach (Marker marker in markers)
            {
                if (marker.LastSeen < oldest.LastSeen)
                {
                    oldest = marker;
                }
            }

            return oldest;
        }

        private Marker CreateMarker()
        {
            Marker marker = new Marker();
            marker.Rect = UiFactory.CreateRect(transform, "BallMarker");
            marker.Rect.anchorMin = Vector2.zero;
            marker.Rect.anchorMax = Vector2.zero;
            marker.Rect.pivot = new Vector2(0.5f, 0.5f);
            marker.Rect.sizeDelta = new Vector2(130f, 130f);
            marker.Ring = AddImage(marker.Rect, "Ring", EffectShape.Ring, new Vector2(130f, 130f));
            marker.Dot = AddImage(marker.Rect, "Dot", EffectShape.Dot, new Vector2(26f, 26f));
            marker.Rect.gameObject.SetActive(false);
            return marker;
        }

        private static Image AddImage(RectTransform parent, string name, EffectShape shape, Vector2 size)
        {
            RectTransform rect = UiFactory.CreateRect(parent, name);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = EffectSprites.Get(shape);
            image.color = RingColor;
            image.raycastTarget = false;
            return image;
        }
    }
}
