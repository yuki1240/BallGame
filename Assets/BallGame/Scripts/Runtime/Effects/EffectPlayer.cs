using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// 演出を1回再生し、終わったら自分を破棄します（プールしません）。
    /// </summary>
    public sealed class EffectPlayer : MonoBehaviour
    {
        private readonly List<EffectPieceSpec> specs = new List<EffectPieceSpec>();
        private readonly List<Image> images = new List<Image>();
        private Color color;
        private float scale;
        private float duration;
        private float elapsed;

        /// <summary>
        /// 部品を作って再生を始めます。
        /// </summary>
        /// <param name="pieces">部品の動き</param>
        /// <param name="effectColor">色</param>
        /// <param name="effectScale">表示倍率</param>
        /// <param name="effectDuration">再生時間（秒）</param>
        public void Play(List<EffectPieceSpec> pieces, Color effectColor, float effectScale, float effectDuration)
        {
            color = effectColor;
            scale = effectScale;
            duration = Mathf.Max(0.05f, effectDuration);
            elapsed = 0f;
            foreach (EffectPieceSpec piece in pieces)
            {
                specs.Add(piece);
                images.Add(CreateImage(piece));
            }

            ApplyFrame(0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            if (progress >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            ApplyFrame(progress);
        }

        private Image CreateImage(EffectPieceSpec piece)
        {
            RectTransform rect = UiFactory.CreateRect(transform, "Piece " + piece.Shape);
            rect.sizeDelta = piece.Size * scale;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = EffectSprites.Get(piece.Shape);
            image.raycastTarget = false;
            image.color = Color.Lerp(color, Color.white, piece.Whiten);
            return image;
        }

        private void ApplyFrame(float progress)
        {
            for (int i = 0; i < specs.Count; i++)
            {
                ApplyPiece(specs[i], images[i], progress);
            }
        }

        private void ApplyPiece(EffectPieceSpec spec, Image image, float progress)
        {
            float local = spec.Length <= 0f ? 1f : Mathf.Clamp01((progress - spec.Delay) / spec.Length);
            bool started = progress >= spec.Delay;
            float eased = EaseOutCubic(local);
            RectTransform rect = image.rectTransform;
            rect.anchoredPosition = Vector2.LerpUnclamped(spec.StartPosition, spec.EndPosition, eased) * scale;
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(spec.StartScale, spec.EndScale, eased);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(spec.StartRotation, spec.EndRotation, eased));
            Color pieceColor = image.color;
            pieceColor.a = started ? spec.StartAlpha * (1f - local * local) : 0f;
            image.color = pieceColor;
        }

        private static float EaseOutCubic(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }
    }
}
