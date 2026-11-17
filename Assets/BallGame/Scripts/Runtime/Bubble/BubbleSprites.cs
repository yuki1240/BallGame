using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// シャボン玉の場面で使う絵と音を、コードで作ります（画像や音のファイルは使いません）。
    /// 一度作ったものは使い回します。
    /// </summary>
    public static class BubbleSprites
    {
        private const int BubbleSize = 160;
        private const int DropletSize = 32;
        private const int SkyWidth = 480;
        private const int SkyHeight = 270;
        private const int SampleRate = 44100;

        private static readonly Color[] Tints =
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(0.85f, 0.95f, 1f, 1f),
            new Color(1f, 0.88f, 0.95f, 1f),
            new Color(0.88f, 1f, 0.92f, 1f),
            new Color(1f, 0.96f, 0.8f, 1f),
            new Color(0.93f, 0.88f, 1f, 1f)
        };

        private static Sprite bubble;
        private static Sprite droplet;
        private static Texture2D sky;
        private static AudioClip popClip;

        /// <summary>
        /// 色の種類の番号から、シャボン玉の色合いを返します。
        /// </summary>
        /// <param name="colorIndex">色の種類の番号</param>
        /// <returns>色</returns>
        public static Color GetTint(int colorIndex)
        {
            return Tints[Mathf.Abs(colorIndex) % Tints.Length];
        }

        /// <summary>
        /// 透きとおったシャボン玉の絵（縁が虹色に光り、左上にハイライトがある）を返します。
        /// </summary>
        /// <returns>絵</returns>
        public static Sprite GetBubble()
        {
            if (bubble == null)
            {
                bubble = ToSprite(CreateBubbleTexture());
            }

            return bubble;
        }

        /// <summary>
        /// 割れたときに飛び散る、小さな水しぶきの絵を返します。
        /// </summary>
        /// <returns>絵</returns>
        public static Sprite GetDroplet()
        {
            if (droplet == null)
            {
                droplet = ToSprite(CreateDropletTexture());
            }

            return droplet;
        }

        /// <summary>
        /// 空と雲の背景を返します。
        /// </summary>
        /// <returns>絵</returns>
        public static Texture2D GetSky()
        {
            if (sky == null)
            {
                sky = CreateSkyTexture();
            }

            return sky;
        }

        /// <summary>
        /// 割れるときの「パチン」という短い音を返します。
        /// </summary>
        /// <returns>音</returns>
        public static AudioClip GetPopClip()
        {
            if (popClip == null)
            {
                popClip = CreatePopClip();
            }

            return popClip;
        }

        private static Sprite ToSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D CreateBubbleTexture()
        {
            Color32[] pixels = new Color32[BubbleSize * BubbleSize];
            float half = BubbleSize * 0.5f;
            for (int y = 0; y < BubbleSize; y++)
            {
                for (int x = 0; x < BubbleSize; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    pixels[y * BubbleSize + x] = ComputeBubblePixel(dx, dy);
                }
            }

            return BuildTexture(BubbleSize, BubbleSize, pixels, TextureWrapMode.Clamp);
        }

        private static Color32 ComputeBubblePixel(float dx, float dy)
        {
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            if (radius >= 1f)
            {
                return new Color32(255, 255, 255, 0);
            }

            // 縁ほど濃く、中はほとんど透明。縁の色は角度で虹色にずらす
            float rim = Smooth(0.72f, 0.96f, radius) * (1f - Smooth(0.97f, 1f, radius));
            float hue = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 1f) % 1f;
            Color rainbow = Color.HSVToRGB(hue, 0.35f, 1f);
            float alpha = 0.08f + rim * 0.62f;
            Color color = Color.Lerp(Color.white, rainbow, rim);

            // 左上のハイライト
            float hx = dx + 0.42f;
            float hy = dy - 0.42f;
            float highlight = Mathf.Clamp01(1f - (hx * hx * 6f + hy * hy * 14f));
            alpha = Mathf.Clamp01(alpha + highlight * 0.7f);
            color = Color.Lerp(color, Color.white, highlight);
            return new Color32(ToByte(color.r), ToByte(color.g), ToByte(color.b), ToByte(alpha));
        }

        private static Texture2D CreateDropletTexture()
        {
            Color32[] pixels = new Color32[DropletSize * DropletSize];
            float half = DropletSize * 0.5f;
            for (int y = 0; y < DropletSize; y++)
            {
                for (int x = 0; x < DropletSize; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Smooth(0.6f, 1f, radius);
                    pixels[y * DropletSize + x] = new Color32(255, 255, 255, ToByte(alpha));
                }
            }

            return BuildTexture(DropletSize, DropletSize, pixels, TextureWrapMode.Clamp);
        }

        private static Texture2D CreateSkyTexture()
        {
            Color top = new Color(0.36f, 0.72f, 1f, 1f);
            Color bottom = new Color(0.86f, 0.96f, 1f, 1f);
            Color[] colors = new Color[SkyWidth * SkyHeight];
            for (int y = 0; y < SkyHeight; y++)
            {
                Color row = Color.Lerp(bottom, top, Mathf.Pow(y / (float)(SkyHeight - 1), 0.8f));
                for (int x = 0; x < SkyWidth; x++)
                {
                    colors[y * SkyWidth + x] = row;
                }
            }

            AddCloud(colors, 90f, 200f, 70f, 16f);
            AddCloud(colors, 330f, 230f, 90f, 18f);
            AddCloud(colors, 230f, 120f, 60f, 12f);
            AddCloud(colors, 410f, 90f, 55f, 12f);
            Color32[] pixels = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                pixels[i] = colors[i];
            }

            return BuildTexture(SkyWidth, SkyHeight, pixels, TextureWrapMode.Clamp);
        }

        private static void AddCloud(Color[] colors, float centerX, float centerY, float halfWidth, float halfHeight)
        {
            for (int y = 0; y < SkyHeight; y++)
            {
                for (int x = 0; x < SkyWidth; x++)
                {
                    float dx = (x - centerX) / halfWidth;
                    float dy = (y - centerY) / halfHeight;
                    float strength = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)) * 0.85f;
                    if (strength > 0f)
                    {
                        colors[y * SkyWidth + x] = Color.Lerp(colors[y * SkyWidth + x], Color.white, strength);
                    }
                }
            }
        }

        private static AudioClip CreatePopClip()
        {
            int count = Mathf.RoundToInt(SampleRate * 0.16f);
            float[] samples = new float[count];
            System.Random noise = new System.Random(7);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float click = Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 70f);
                float hiss = ((float)noise.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 45f);
                samples[i] = Mathf.Clamp(0.55f * click + 0.6f * hiss, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("BubblePop", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Texture2D BuildTexture(int width, int height, Color32[] pixels, TextureWrapMode wrap)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = wrap;
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        // edge0 から edge1 にかけて 0 から 1 へなめらかに変わる値（Mathf.SmoothStep とは別のもの）
        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }
    }
}
