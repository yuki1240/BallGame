using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 深度をプレビュー用の画像にします。近いほど明るく、無効（0）は紺色、変化した所は琥珀色、ボールとして検知できた所は赤で重ねます。
    /// 真下設置では遠い側が画面の上に来るように上下を入れ替えて表示します（データは反転しません）。
    /// </summary>
    public sealed class DepthPreviewRenderer
    {
        /// <summary>明るさの基準にする最も近い深度（mm）。</summary>
        public const float NearMm = 500f;

        /// <summary>明るさの基準にする最も遠い深度（mm）。</summary>
        public const float FarMm = 4500f;

        private static readonly Color32 InvalidColor = new Color32(20, 24, 60, 255);
        private static readonly Color32 CandidateColor = new Color32(190, 140, 40, 255);
        private static readonly Color32 BlindColor = new Color32(170, 60, 200, 255);
        private static readonly Color32 BallColor = new Color32(255, 20, 20, 255);
        private static readonly Color32 OutsideTint = new Color32(0, 0, 0, 255);

        /// <summary>赤い表示を、ボールが見えなくなってからも残す秒数（一瞬で消えて見逃さないため）。</summary>
        public const float BallHoldSeconds = 0.6f;

        private Texture2D texture;
        private Color32[] pixels;
        private float[] ballUntil;

        /// <summary>プレビューの画像。</summary>
        public Texture2D Texture
        {
            get { return texture; }
        }

        /// <summary>
        /// 深度を画像に描きます。
        /// </summary>
        /// <param name="depth">深度</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="mount">設置向き（表示の上下に使う）</param>
        /// <param name="candidates">候補画素（null 可）</param>
        /// <param name="detector">四隅の内側を暗くしないための検知器（null 可）</param>
        /// <param name="ball">ボールとして検知できた画素（null 可）。赤く表示します</param>
        /// <param name="timeSeconds">今の時刻（秒。赤い表示を少し残すために使う）</param>
        /// <param name="blind">ボールを検知できない画素（null 可）。紫を重ねます</param>
        /// <returns>描いた画像</returns>
        public Texture2D Render(ushort[] depth, int width, int height, MountOrientation mount, bool[] candidates, HitDetector detector, bool[] ball = null, float timeSeconds = 0f, bool[] blind = null)
        {
            EnsureTexture(width, height);
            bool flip = MountGeometry.IsPreviewFlippedVertically(mount);
            bool showMask = detector != null && detector.IsConfigured && detector.Width == width && detector.Height == height;
            for (int y = 0; y < height; y++)
            {
                // テクスチャは下の行が 0 なので、画像の上端（Y=0）を上に出すには行を入れ替える
                int textureRow = flip ? y : height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (ball != null && index < ball.Length && ball[index])
                    {
                        ballUntil[index] = timeSeconds + BallHoldSeconds;
                    }

                    bool isBall = ballUntil[index] > timeSeconds;
                    Color32 color = isBall ? BallColor : GetColor(depth[index], candidates, index, showMask && !detector.IsInside(index));
                    pixels[textureRow * width + x] = !isBall && blind != null && index < blind.Length && blind[index] ? Color32.Lerp(color, BlindColor, 0.6f) : color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// 画像を破棄します。
        /// </summary>
        public void Dispose()
        {
            if (texture != null)
            {
                Object.Destroy(texture);
                texture = null;
            }
        }

        private void EnsureTexture(int width, int height)
        {
            if (texture != null && texture.width == width && texture.height == height)
            {
                return;
            }

            Dispose();
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            pixels = new Color32[width * height];
            ballUntil = new float[width * height];
        }

        private static Color32 GetColor(ushort depth, bool[] candidates, int index, bool outside)
        {
            if (candidates != null && index < candidates.Length && candidates[index])
            {
                return CandidateColor;
            }

            Color32 color = depth == 0 ? InvalidColor : ToGray(depth);
            return outside ? Color32.Lerp(color, OutsideTint, 0.55f) : color;
        }

        private static Color32 ToGray(ushort depth)
        {
            float t = Mathf.Clamp01((depth - NearMm) / (FarMm - NearMm));
            byte value = (byte)Mathf.RoundToInt(Mathf.Lerp(250f, 40f, t));
            return new Color32(value, value, value, 255);
        }
    }
}
