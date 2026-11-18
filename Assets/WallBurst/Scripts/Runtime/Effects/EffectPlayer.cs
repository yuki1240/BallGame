using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// 仮のエフェクト1回ぶん：当たった所で光の輪が広がり、粒が飛び散ります。
    /// 再生が終わったら自分で消えます（念のため、どんなときも <see cref="EffectSpawner.MaxLifetimeSeconds"/> 秒で消えます）。
    /// </summary>
    public sealed class EffectPlayer : MonoBehaviour
    {
        private sealed class Piece
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float StartSize;
            public float EndSize;
            public bool Fall;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        private float duration;
        private float elapsed;
        private Color color;

        /// <summary>
        /// 部品を作って再生を始めます。
        /// </summary>
        /// <param name="effectColor">色</param>
        /// <param name="scale">大きさの倍率（1 で直径およそ 300 画素）</param>
        /// <param name="seconds">再生時間</param>
        /// <param name="random">乱数</param>
        public void Play(Color effectColor, float scale, float seconds, System.Random random)
        {
            color = effectColor;
            duration = Mathf.Max(0.1f, seconds);
            AddPiece(ProceduralSprites.Glow, Vector2.zero, 120f * scale, 360f * scale, false);
            AddPiece(ProceduralSprites.Ring, Vector2.zero, 40f * scale, 340f * scale, false);
            int count = 12;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.6f) / count * Mathf.PI * 2f;
                float speed = (260f + (float)random.NextDouble() * 220f) * scale;
                Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                AddPiece(i % 3 == 0 ? ProceduralSprites.Sparkle : ProceduralSprites.Circle, velocity, 36f * scale, 10f * scale, true);
            }

            UpdatePieces(0f);
        }

        private void AddPiece(Sprite sprite, Vector2 velocity, float startSize, float endSize, bool fall)
        {
            GameObject pieceObject = new GameObject("Piece", typeof(RectTransform));
            pieceObject.transform.SetParent(transform, false);
            Image image = pieceObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            Piece piece = new Piece();
            piece.Rect = (RectTransform)pieceObject.transform;
            piece.Rect.anchorMin = new Vector2(0.5f, 0.5f);
            piece.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            piece.Image = image;
            piece.Velocity = velocity;
            piece.StartSize = startSize;
            piece.EndSize = endSize;
            piece.Fall = fall;
            pieces.Add(piece);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                Destroy(gameObject);
                return;
            }

            UpdatePieces(elapsed / duration);
        }

        private void UpdatePieces(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            float alpha = 1f - t * t;
            float seconds = t * duration;
            foreach (Piece piece in pieces)
            {
                Vector2 position = piece.Velocity * seconds;
                if (piece.Fall)
                {
                    position.y -= 300f * seconds * seconds;
                }

                piece.Rect.anchoredPosition = position;
                float size = Mathf.Lerp(piece.StartSize, piece.EndSize, ease);
                piece.Rect.sizeDelta = new Vector2(size, size);
                Color c = color;
                c.a = alpha;
                piece.Image.color = c;
            }
        }
    }
}
