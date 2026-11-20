using UnityEngine;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// 四隅で囲んだ台形の中の深度を、深度を持ったまま壁の面の上の長方形（壁グリッド、<see cref="RectifiedDepthGrid"/>）に直して、
    /// 画面いっぱい（1920×1080）に引き伸ばして映す表示です。
    /// <para>プロジェクターは画面いっぱいを壁の投影範囲に映すので、この表示は実物の壁とぴったり重なります。
    /// ボールや手を壁に近づけると、その位置に色が付いて写るので、次のことを壁の前で確かめられます。</para>
    /// <list type="bullet">
    /// <item>壁の上の位置が合っているか（手を当てた所に、手が写るか。Kinect から遠い側でもずれないか）</item>
    /// <item>ボールが深度に写っているか、どう見分けられているか（赤＝ボール、青＝体・手、黄＝ちらつき）</item>
    /// </list>
    /// <para>表示の画像は、壁グリッドと同じ大きさ（1マス＝壁の上の 1cm）です。値の入らないマスは黒で出します。</para>
    /// </summary>
    public sealed class RectifiedDepthView
    {
        private static readonly Color32 Outside = new Color32(0, 0, 0, 255);
        private static readonly Color32 Border = new Color32(60, 255, 90, 255);
        private static readonly Color32 Grid = new Color32(60, 255, 90, 110);

        private readonly RawImage image;
        private Texture2D texture;
        private Color32[] pixels;
        private RectifiedDepthGrid grid;

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
            if (grid == null || !grid.IsFor(mapping))
            {
                grid = new RectifiedDepthGrid(mapping, detector.InsideMask, detector.Width, detector.Height);
                CreateTexture(grid.Width, grid.Height);
            }

            grid.Build(depth);
            byte[] classes = detector.Classes;
            bool[] blind = detector.BlindMask;
            int width = grid.Width;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // テクスチャは下の行から並ぶ。壁グリッドも下の行が y = 0 なので、そのまま対応する
                    int source = grid.GetSourcePixel(x, y);
                    pixels[y * width + x] = source < 0
                        ? Outside
                        : CalibrationPreview.PixelColor(grid.GetDepth(x, y), classes[source], blind[source]);
                }
            }

            DrawFrame();
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        private void CreateTexture(int width, int height)
        {
            if (texture != null)
            {
                Object.Destroy(texture);
            }

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            pixels = new Color32[width * height];
            image.texture = texture;
        }

        // 外わくと、4×2 のマス目（上下左右がわかるように）を描く
        private void DrawFrame()
        {
            int width = grid.Width;
            int height = grid.Height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    {
                        pixels[i] = Border;
                    }
                    else if (x == width / 4 || x == width / 2 || x == width * 3 / 4 || y == height / 2)
                    {
                        pixels[i] = Color32.Lerp(pixels[i], Grid, 0.5f);
                    }
                }
            }
        }
    }
}
