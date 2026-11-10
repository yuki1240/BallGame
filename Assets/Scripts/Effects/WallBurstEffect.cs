using UnityEngine;
using UnityEngine.UI;

namespace WallBurst.Effects
{
    /// <summary>
    /// 壁の1ヒット分の演出。輪、閃光、粒は重なっても光が重なるだけなので、追跡しなくてよい。
    /// 再生が終わったら自分を破棄する。
    /// </summary>
    public sealed class WallBurstEffect : MonoBehaviour
    {
        private Piece[] _pieces = System.Array.Empty<Piece>();
        private Color _color = Color.white;
        private float _age;
        private const float Span = 1.15f;

        /// <summary>
        /// 種類を選んで再生する。0 輪、1 閃光、2 粒、それ以外は二重の閃光。
        /// </summary>
        /// <param name="kind">演出の種類。</param>
        /// <param name="color">色。</param>
        public void Play(int kind, Color color)
        {
            _color = color;
            if (kind == 0)
            {
                CreateRings();
            }
            else if (kind == 1)
            {
                CreateFlash();
            }
            else if (kind == 2)
            {
                CreateSparks();
            }
            else
            {
                CreatePulse();
            }
        }

        /// <summary>
        /// 拡大しながら消す。
        /// </summary>
        private void Update()
        {
            _age += Time.deltaTime;
            bool alive = false;
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (Animate(ref _pieces[i]))
                {
                    alive = true;
                }
            }

            if (!alive && _age > 0.05f)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 1片を進める。まだ見えるなら true。
        /// </summary>
        private bool Animate(ref Piece piece)
        {
            float local = _age - piece.Delay;
            if (local < 0f)
            {
                return true;
            }

            float t = local / Span;
            if (t >= 1f)
            {
                SetAlpha(piece.Image, 0f);
                return false;
            }

            float scale = Mathf.Lerp(piece.StartScale, piece.EndScale, t);
            piece.Rect.localScale = new Vector3(scale, scale, 1f);
            piece.Rect.anchoredPosition = piece.Origin + (piece.Velocity * local);
            SetAlpha(piece.Image, piece.BaseAlpha * (1f - t));
            return true;
        }

        /// <summary>
        /// 時間差で広がる輪を3つ置く。
        /// </summary>
        private void CreateRings()
        {
            _pieces = new Piece[3];
            for (int i = 0; i < 3; i++)
            {
                Image image = CreateGraphic(WallEffectSprites.Ring, 760f);
                _pieces[i] = PieceAt(image, Vector2.zero, Vector2.zero, 0.12f * i, 0.08f, 1f, 0.9f);
            }
        }

        /// <summary>
        /// 中心の柔らかい閃光。
        /// </summary>
        private void CreateFlash()
        {
            Image image = CreateGraphic(WallEffectSprites.Soft, 760f);
            _pieces = new[] { PieceAt(image, Vector2.zero, Vector2.zero, 0f, 0.18f, 1.05f, 0.55f) };
        }

        /// <summary>
        /// 外へ飛びながら消える粒。
        /// </summary>
        private void CreateSparks()
        {
            const int count = 16;
            _pieces = new Piece[count];
            for (int i = 0; i < count; i++)
            {
                Image image = CreateGraphic(WallEffectSprites.Spark, 48f);
                float angle = (Mathf.PI * 2f * i) / count;
                float speed = Random.Range(220f, 560f);
                Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                _pieces[i] = PieceAt(image, Vector2.zero, velocity, Random.Range(0f, 0.08f), 0.7f, 0.15f, 1f);
            }
        }

        /// <summary>
        /// 大きい閃光と、少し遅れる輪。
        /// </summary>
        private void CreatePulse()
        {
            Image flash = CreateGraphic(WallEffectSprites.Soft, 680f);
            Image ring = CreateGraphic(WallEffectSprites.Ring, 760f);
            _pieces = new[]
            {
                PieceAt(flash, Vector2.zero, Vector2.zero, 0f, 0.12f, 0.85f, 0.5f),
                PieceAt(ring, Vector2.zero, Vector2.zero, 0.08f, 0.2f, 1.15f, 0.95f)
            };
        }

        /// <summary>
        /// 子イメージを1つ作る。レイキャストは受けない。
        /// </summary>
        private Image CreateGraphic(Sprite sprite, float size)
        {
            GameObject child = new GameObject("Piece");
            child.transform.SetParent(transform, false);
            RectTransform rect = child.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            Image image = child.AddComponent<Image>();
            image.sprite = sprite;
            image.color = _color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// アニメーション用の1片を作る。
        /// </summary>
        private static Piece PieceAt(
            Image image,
            Vector2 origin,
            Vector2 velocity,
            float delay,
            float startScale,
            float endScale,
            float alpha)
        {
            Piece piece = new Piece();
            piece.Rect = image.rectTransform;
            piece.Image = image;
            piece.Origin = origin;
            piece.Velocity = velocity;
            piece.Delay = delay;
            piece.StartScale = startScale;
            piece.EndScale = endScale;
            piece.BaseAlpha = alpha;
            piece.Rect.localScale = new Vector3(startScale, startScale, 1f);
            SetAlpha(image, 0f);
            return piece;
        }

        /// <summary>
        /// 色のアルファだけ変える。
        /// </summary>
        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private struct Piece
        {
            public RectTransform Rect;
            public Graphic Image;
            public Vector2 Origin;
            public Vector2 Velocity;
            public float Delay;
            public float StartScale;
            public float EndScale;
            public float BaseAlpha;
        }
    }
}
