using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 紅葉の山の景色を、プログラムで描きます（画像ファイルは使いません）。
    /// 同じ形の山を「緑の山」と「紅葉した山」の2枚で作り、
    /// 当たった所から緑の絵を紅葉の絵へ少しずつ置き換えていくために使います。
    /// 空は2枚とも同じで、色が変わるのは山（マスク）の部分だけです。
    /// </summary>
    public sealed class AutumnScenery
    {
        /// <summary>横の画素数の既定値。</summary>
        public const int DefaultWidth = 800;

        /// <summary>縦の画素数の既定値（横の 16:9）。</summary>
        public const int DefaultHeight = 450;

        private const float TreeScaleX = 0.5f;
        private const float TreeScaleY = 0.8f;

        private static readonly Color SkyTop = new Color(0.42f, 0.67f, 0.93f, 1f);
        private static readonly Color SkyHorizon = new Color(0.88f, 0.94f, 0.98f, 1f);
        private static readonly Color Haze = new Color(0.78f, 0.86f, 0.92f, 1f);

        private static readonly Color DeepGreen = new Color(0.09f, 0.31f, 0.12f, 1f);
        private static readonly Color LightGreen = new Color(0.33f, 0.58f, 0.17f, 1f);

        private static readonly Color DeepRed = new Color(0.72f, 0.1f, 0.07f, 1f);
        private static readonly Color Orange = new Color(0.94f, 0.45f, 0.07f, 1f);
        private static readonly Color Yellow = new Color(0.99f, 0.8f, 0.14f, 1f);
        private static readonly Color Evergreen = new Color(0.08f, 0.27f, 0.11f, 1f);

        private readonly int seed;

        private AutumnScenery(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            this.seed = seed;
            Green = new Color[width * height];
            Autumn = new Color[width * height];
            Mask = new bool[width * height];
            EdgeNoise = new float[width * height];
        }

        /// <summary>横の画素数。</summary>
        public int Width { get; private set; }

        /// <summary>縦の画素数。</summary>
        public int Height { get; private set; }

        /// <summary>緑の山の絵（左下から右へ、下から上へ並ぶ）。</summary>
        public Color[] Green { get; private set; }

        /// <summary>紅葉した山の絵（形は緑の山と同じ）。</summary>
        public Color[] Autumn { get; private set; }

        /// <summary>山の部分なら true（色が変わるのはここだけ）。</summary>
        public bool[] Mask { get; private set; }

        /// <summary>色が広がる縁をでこぼこにするための 0〜1 のゆらぎ。</summary>
        public float[] EdgeNoise { get; private set; }

        /// <summary>
        /// 景色を作ります。同じ引数なら、いつも同じ絵になります。
        /// </summary>
        /// <param name="width">横の画素数</param>
        /// <param name="height">縦の画素数</param>
        /// <param name="seed">絵のもとになる数</param>
        /// <returns>景色</returns>
        public static AutumnScenery Generate(int width = DefaultWidth, int height = DefaultHeight, int seed = 2026)
        {
            AutumnScenery scenery = new AutumnScenery(width, height, seed);
            scenery.Paint();
            return scenery;
        }

        /// <summary>
        /// 山の部分の画素の数を返します。
        /// </summary>
        /// <returns>画素の数</returns>
        public int CountMountainPixels()
        {
            int count = 0;
            for (int i = 0; i < Mask.Length; i++)
            {
                count += Mask[i] ? 1 : 0;
            }

            return count;
        }

        private void Paint()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    PaintPixel(x, y);
                }
            }
        }

        private void PaintPixel(int x, int y)
        {
            int index = y * Width + x;
            float u = (x + 0.5f) / Width;
            float v = (y + 0.5f) / Height;
            EdgeNoise[index] = Fbm(u * 9f, v * 9f, seed + 7, 3);
            int layer = GetLayer(u, v);
            if (layer < 0)
            {
                Color sky = PaintSky(u, v);
                Green[index] = sky;
                Autumn[index] = sky;
                return;
            }

            Mask[index] = true;
            PaintMountain(index, x, y, u, v, layer);
        }

        // 手前の層から順に調べ、そのピクセルがどの山の層か（無ければ空 = -1）を返す。0 が奥、3 がいちばん手前。
        private int GetLayer(float u, float v)
        {
            if (v < ForegroundRidge(u))
            {
                return 3;
            }

            if (v < MainRidge(u))
            {
                return 2;
            }

            if (v < MidRidge(u))
            {
                return 1;
            }

            return v < FarRidge(u) ? 0 : -1;
        }

        private float FarRidge(float u)
        {
            return 0.6f + 0.09f * (Fbm(u * 3.2f, 1.3f, seed + 1, 3) - 0.5f) * 2f + 0.05f * Peak(u, 0.78f, 0.22f);
        }

        private float MidRidge(float u)
        {
            return 0.47f + 0.1f * (Fbm(u * 2.6f, 4.1f, seed + 2, 3) - 0.5f) * 2f + 0.07f * Peak(u, 0.12f, 0.25f);
        }

        private float MainRidge(float u)
        {
            float shape = 0.15f + 0.5f * Peak(u, 0.4f, 0.3f) + 0.2f * Peak(u, 0.85f, 0.2f);
            return shape + 0.035f * (Fbm(u * 8f, 7.7f, seed + 3, 3) - 0.5f) * 2f;
        }

        private float ForegroundRidge(float u)
        {
            return 0.1f + 0.05f * (Fbm(u * 4.5f, 9.9f, seed + 4, 3) - 0.5f) * 2f;
        }

        // とがった山の形（中心 center で 1、中心から width 離れると 0）
        private static float Peak(float u, float center, float width)
        {
            float t = Mathf.Clamp01(1f - Mathf.Abs(u - center) / width);
            return Mathf.Pow(t, 1.3f);
        }

        private Color PaintSky(float u, float v)
        {
            Color baseColor = Mix(SkyHorizon, SkyTop, Mathf.Clamp01((v - 0.45f) / 0.55f));
            float cloud = Mathf.SmoothStep(0.55f, 0.8f, Fbm(u * 4f + 3f, v * 9f, seed + 5, 4));
            float amount = cloud * 0.75f * Mathf.Clamp01((v - 0.5f) / 0.3f);
            return Mix(baseColor, new Color(1f, 1f, 1f, 1f), amount);
        }

        private void PaintMountain(int index, int x, int y, float u, float v, int layer)
        {
            float light = GetLight(u, layer);
            float trees = GetTreeNoise(x, y);
            float region = Fbm(u * 5f + 11f, v * 6f + 5f, seed + 9, 3);
            float conifer = Fbm(x * 0.25f, y * 0.35f, seed + 13, 2);
            float haze = layer == 0 ? 0.62f : layer == 1 ? 0.36f : layer == 2 ? 0.06f : 0f;
            Color green = Mix(DeepGreen, LightGreen, trees * 0.7f + region * 0.3f);
            Color autumn = GetAutumnColor(region, trees, conifer);
            Green[index] = ApplyShade(green, light, trees, haze, layer);
            Autumn[index] = ApplyShade(autumn, light, trees, haze, layer);
        }

        private static Color GetAutumnColor(float region, float trees, float conifer)
        {
            // 赤・だいだい・黄が、まだらに分かれる。ところどころに常緑樹の緑を残す
            float t = Mathf.Clamp01(region * 1.15f - 0.05f + (trees - 0.5f) * 0.25f);
            Color color = t < 0.5f ? Mix(DeepRed, Orange, t / 0.5f) : Mix(Orange, Yellow, (t - 0.5f) / 0.5f);
            float keepGreen = Mathf.SmoothStep(0.74f, 0.82f, conifer) * 0.85f;
            return Mix(color, Evergreen, keepGreen);
        }

        // 左から光が当たるものとして、山の左がわ（右上がりの斜面）を明るく、右がわを暗くする
        private float GetLight(float u, int layer)
        {
            float du = 0.004f;
            float slope = (RidgeOf(layer, u + du) - RidgeOf(layer, u - du)) / (2f * du);
            return 1f + 0.22f * Mathf.Clamp(slope * 0.9f, -1f, 1f);
        }

        private float RidgeOf(int layer, float u)
        {
            return layer == 0 ? FarRidge(u) : layer == 1 ? MidRidge(u) : layer == 2 ? MainRidge(u) : ForegroundRidge(u);
        }

        // 木が1本ずつ並んでいるような、こまかいまだら
        private float GetTreeNoise(int x, int y)
        {
            float coarse = Fbm(x * TreeScaleX * 0.5f, y * TreeScaleY * 0.5f, seed + 21, 2);
            float fine = ValueNoise(x * TreeScaleX * 1.6f, y * TreeScaleY * 1.6f, seed + 22);
            return Mathf.Clamp01(coarse * 0.6f + fine * 0.4f);
        }

        private static Color ApplyShade(Color color, float light, float trees, float haze, int layer)
        {
            float texture = 0.78f + 0.4f * trees;
            float scale = light * texture * (layer == 3 ? 0.82f : 1f);
            Color shaded = new Color(Mathf.Clamp01(color.r * scale), Mathf.Clamp01(color.g * scale), Mathf.Clamp01(color.b * scale), 1f);
            return Mix(shaded, Haze, haze);
        }

        private static Color Mix(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, seed);
            float b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed);
            float d = Hash(x0 + 1, y0 + 1, seed);
            float top = a + (b - a) * sx;
            float bottom = c + (d - c) * sx;
            return top + (bottom - top) * sy;
        }

        private static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x, y, seed + i * 31) * amplitude;
                total += amplitude;
                x *= 2.03f;
                y *= 2.03f;
                amplitude *= 0.5f;
            }

            return sum / total;
        }
    }
}
