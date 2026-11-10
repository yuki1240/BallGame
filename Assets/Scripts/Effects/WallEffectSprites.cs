using UnityEngine;

namespace WallBurst.Effects
{
    /// <summary>
    /// 重なっても不自然になりにくい、大きい円と粒のスプライト。
    /// </summary>
    public static class WallEffectSprites
    {
        private static Sprite _soft;
        private static Sprite _ring;
        private static Sprite _spark;

        /// <summary>中心が明るく、端が消える円。</summary>
        public static Sprite Soft
        {
            get { return _soft ?? (_soft = Create(PaintSoft)); }
        }

        /// <summary>細い輪。</summary>
        public static Sprite Ring
        {
            get { return _ring ?? (_ring = Create(PaintRing)); }
        }

        /// <summary>小さい粒。</summary>
        public static Sprite Spark
        {
            get { return _spark ?? (_spark = Create(PaintSpark)); }
        }

        /// <summary>
        /// 128 画素のスプライトを作る。
        /// </summary>
        private static Sprite Create(System.Func<int, int, int, Color> painter)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[(y * size) + x] = painter(x, y, size);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 端へ向かって消える円。
        /// </summary>
        private static Color PaintSoft(int x, int y, int size)
        {
            float distance = Distance(x, y, size);
            float alpha = Mathf.Clamp01(1f - distance);
            alpha *= alpha;
            return new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>
        /// 半径 0.72 付近だけ残す輪。
        /// </summary>
        private static Color PaintRing(int x, int y, int size)
        {
            float distance = Distance(x, y, size);
            float alpha = Mathf.Clamp01(1f - (Mathf.Abs(distance - 0.72f) * 8f));
            return new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>
        /// ひし形の粒。
        /// </summary>
        private static Color PaintSpark(int x, int y, int size)
        {
            float dx = ((x + 0.5f) / size) - 0.5f;
            float dy = ((y + 0.5f) / size) - 0.5f;
            float manhattan = (Mathf.Abs(dx) + Mathf.Abs(dy)) * 2f;
            float alpha = Mathf.Clamp01(1f - manhattan);
            return new Color(1f, 1f, 1f, alpha * alpha);
        }

        /// <summary>
        /// 中心からの距離。端が 1。
        /// </summary>
        private static float Distance(int x, int y, int size)
        {
            float dx = ((x + 0.5f) / size) - 0.5f;
            float dy = ((y + 0.5f) / size) - 0.5f;
            return Mathf.Sqrt((dx * dx) + (dy * dy)) * 2f;
        }
    }
}
