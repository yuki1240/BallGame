using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 画像ファイルを使わずに、コードで描いた丸・輪・光の粒の画像を作ります。
    /// 1回だけ作って使い回すので、何度呼んでも増えません。
    /// </summary>
    public static class ProceduralSprites
    {
        private const int Size = 128;

        private static Sprite circle;
        private static Sprite ring;
        private static Sprite glow;
        private static Sprite sparkle;

        /// <summary>ふちのやわらかい丸。</summary>
        public static Sprite Circle
        {
            get { return circle != null ? circle : (circle = Create("WB_Circle", CircleAlpha)); }
        }

        /// <summary>輪。</summary>
        public static Sprite Ring
        {
            get { return ring != null ? ring : (ring = Create("WB_Ring", RingAlpha)); }
        }

        /// <summary>中心が明るく、外へぼやける光。</summary>
        public static Sprite Glow
        {
            get { return glow != null ? glow : (glow = Create("WB_Glow", GlowAlpha)); }
        }

        /// <summary>十字に光る、きらきらの粒。</summary>
        public static Sprite Sparkle
        {
            get { return sparkle != null ? sparkle : (sparkle = Create("WB_Sparkle", SparkleAlpha)); }
        }

        private delegate float AlphaFunction(float x, float y);

        private static Sprite Create(string name, AlphaFunction alpha)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            return sprite;
        }

        private static float CircleAlpha(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((1f - r) * 12f);
        }

        private static float RingAlpha(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((1f - r) * 12f) * Mathf.Clamp01((r - 0.78f) * 12f);
        }

        private static float GlowAlpha(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float t = Mathf.Clamp01(1f - r);
            return t * t;
        }

        private static float SparkleAlpha(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float core = Mathf.Clamp01(1f - r * 3f);
            float cross = Mathf.Clamp01(1f - Mathf.Abs(x) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(y)) + Mathf.Clamp01(1f - Mathf.Abs(y) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(x));
            return Mathf.Clamp01(core + cross * 0.8f) * Mathf.Clamp01((1f - r) * 4f);
        }
    }
}
