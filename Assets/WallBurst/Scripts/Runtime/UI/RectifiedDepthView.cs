using UnityEngine;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// 四隅で囲んだ台形の中の深度を、射影変換で長方形に直して、画面いっぱい（1920×1080）に映す表示です。
    /// <para>プロジェクターは画面いっぱいを壁の投影範囲に映すので、この表示は実物の壁とぴったり重なります。
    /// ボールや手を壁に近づけると、その位置に色が付いて写るので、次のことを壁の前で確かめられます。</para>
    /// <list type="bullet">
    /// <item>射影変換が合っているか（手を当てた所に、手が写るか）</item>
    /// <item>ボールが深度に写っているか、どう見分けられているか（赤＝ボール、青＝体・手、黄＝ちらつき）</item>
    /// </list>
    /// <para>表示は 480×270 の画像を画面いっぱいに引き伸ばしています（1920×1080 の 1/4。計算を軽くするため）。
    /// 画像の画素ごとに「元の深度画像のどの画素か」を、射影変換で先に求めておきます。</para>
    /// </summary>
    public sealed class RectifiedDepthView
    {
        /// <summary>表示の画像の幅。</summary>
        public const int ViewWidth = 480;

        /// <summary>表示の画像の高さ。</summary>
        public const int ViewHeight = 270;

        private static readonly Color32 Outside = new Color32(0, 0, 0, 255);
        private static readonly Color32 Border = new Color32(60, 255, 90, 255);
        private static readonly Color32 Grid = new Color32(60, 255, 90, 110);

        private readonly RawImage image;
        private readonly Texture2D texture;
        private readonly Color32[] pixels = new Color32[ViewWidth * ViewHeight];
        private readonly int[] lookup = new int[ViewWidth * ViewHeight];
        private WallMapping lookupFor;

        /// <summary>
        /// 表示を作ります（はじめは隠しておきます）。
        /// </summary>
        /// <param name="parent">親（Canvas）</param>
        public RectifiedDepthView(Transform parent)
        {
            GameObject imageObject = new GameObject("深度を壁に重ねる表示", typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            imageObject.transform.SetAsFirstSibling();
            image = imageObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            Ui.Stretch(image.rectTransform);
            texture = new Texture2D(ViewWidth, ViewHeight, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            image.texture = texture;
            imageObject.SetActive(false);
        }

        /// <summary>表示しているか。</summary>
        public bool Visible
        {
            get { return image.gameObject.activeSelf; }
            set { image.gameObject.SetActive(value); }
        }

        /// <summary>
        /// 新しい深度で描き直します。
        /// </summary>
        /// <param name="depth">深度（幅×高さ）</param>
        /// <param name="detector">当たり判定（校正が済んでいること）</param>
        public void Render(ushort[] depth, HitDetector detector)
        {
            if (!Visible || depth == null || detector == null || !detector.IsConfigured)
            {
                return;
            }

            WallMapping mapping = detector.Mapping;
            if (lookupFor != mapping)
            {
                BuildLookup(mapping, detector);
            }

            byte[] classes = detector.Classes;
            bool[] blind = detector.BlindMask;
            for (int i = 0; i < pixels.Length; i++)
            {
                int source = lookup[i];
                if (source < 0 || source >= depth.Length)
                {
                    pixels[i] = Outside;
                    continue;
                }

                pixels[i] = CalibrationPreview.PixelColor(depth[source], classes[source], blind[source]);
            }

            DrawFrame();
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        // 表示の画素ごとに、元の深度画像の画素の番号を求めておく（射影変換の逆：壁座標 → 深度画像）
        private void BuildLookup(WallMapping mapping, HitDetector detector)
        {
            int width = detector.Width;
            int height = detector.Height;
            for (int y = 0; y < ViewHeight; y++)
            {
                for (int x = 0; x < ViewWidth; x++)
                {
                    // テクスチャは下の行から並ぶ。壁座標も左下が (0, 0) なので、そのまま対応する
                    Vector2 wall = new Vector2((x + 0.5f) / ViewWidth, (y + 0.5f) / ViewHeight);
                    Vector2 source = mapping.WallToImage(wall);
                    int sx = Mathf.FloorToInt(source.x * width);
                    int sy = Mathf.FloorToInt(source.y * height);
                    bool inside = sx >= 0 && sx < width && sy >= 0 && sy < height;
                    lookup[y * ViewWidth + x] = inside ? sy * width + sx : -1;
                }
            }

            lookupFor = mapping;
        }

        // 外わくと、4×2 のマス目（上下左右がわかるように）を描く
        private void DrawFrame()
        {
            for (int x = 0; x < ViewWidth; x++)
            {
                for (int t = 0; t < 2; t++)
                {
                    pixels[t * ViewWidth + x] = Border;
                    pixels[(ViewHeight - 1 - t) * ViewWidth + x] = Border;
                }

                if (x % (ViewWidth / 4) == 0)
                {
                    for (int y = 0; y < ViewHeight; y++)
                    {
                        pixels[y * ViewWidth + x] = Color32.Lerp(pixels[y * ViewWidth + x], Grid, 0.5f);
                    }
                }
            }

            for (int y = 0; y < ViewHeight; y++)
            {
                for (int t = 0; t < 2; t++)
                {
                    pixels[y * ViewWidth + t] = Border;
                    pixels[y * ViewWidth + ViewWidth - 1 - t] = Border;
                }
            }

            int middle = ViewHeight / 2;
            for (int x = 0; x < ViewWidth; x++)
            {
                pixels[middle * ViewWidth + x] = Color32.Lerp(pixels[middle * ViewWidth + x], Grid, 0.5f);
            }
        }
    }
}
