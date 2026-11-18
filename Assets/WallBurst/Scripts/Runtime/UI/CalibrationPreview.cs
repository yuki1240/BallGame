using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// キャリブレーション画面の、Kinect の深度の見本（プレビュー）です。
    /// <list type="bullet">
    /// <item>灰色：壁や物（明るいほど近い）。黒：深度が取れない所</item>
    /// <item>紫：四隅の内側で、ボールを取りにくい所（基準の深度が取れない・不安定）</item>
    /// <item>赤：ボールと見た物　青：体・手と見た物　黄：小さなちらつき</item>
    /// <item>緑の線：タップした四隅の台形</item>
    /// </list>
    /// 表示だけを回したり左右反転したりできます（判定には関係ありません）。
    /// タップした位置は、回転・反転を戻して、元の深度画像の位置（0〜1、上端が 0）にして返します。
    /// </summary>
    public sealed class CalibrationPreview
    {
        private static readonly Color32 Missing = new Color32(12, 12, 20, 255);
        private static readonly Color32 Purple = new Color32(150, 60, 200, 255);
        private static readonly Color32 Red = new Color32(255, 50, 40, 255);
        private static readonly Color32 Blue = new Color32(60, 130, 255, 255);
        private static readonly Color32 Yellow = new Color32(240, 220, 60, 255);
        private static readonly Color32 Green = new Color32(60, 255, 90, 255);

        private static readonly string[] CornerNames = { "①左上", "②右上", "③右下", "④左下" };

        private readonly RawImage image;
        private readonly RectTransform area;
        private readonly Texture2D texture;
        private readonly Color32[] pixels;
        private readonly int width;
        private readonly int height;
        private readonly List<Text> markers = new List<Text>();
        private int rotation;
        private bool mirror;

        /// <summary>
        /// 見本を作ります。
        /// </summary>
        /// <param name="parent">親</param>
        /// <param name="position">置く範囲の左下</param>
        /// <param name="size">置く範囲の大きさ</param>
        /// <param name="depthWidth">深度の幅</param>
        /// <param name="depthHeight">深度の高さ</param>
        public CalibrationPreview(Transform parent, Vector2 position, Vector2 size, int depthWidth, int depthHeight)
        {
            width = depthWidth;
            height = depthHeight;
            Image frame = Ui.CreatePanel(parent, "プレビューの枠", new Color(0f, 0f, 0f, 0.6f));
            area = frame.rectTransform;
            Ui.Place(area, position, size);
            GameObject imageObject = new GameObject("プレビュー", typeof(RectTransform));
            imageObject.transform.SetParent(area, false);
            image = imageObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            image.texture = texture;
            pixels = new Color32[width * height];
            for (int i = 0; i < 4; i++)
            {
                Text marker = Ui.CreateText(parent, CornerNames[i], 28, TextAnchor.MiddleCenter);
                marker.fontStyle = FontStyle.Bold;
                marker.color = Green;
                marker.rectTransform.sizeDelta = new Vector2(160f, 40f);
                Outline outline = marker.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2f, -2f);
                marker.enabled = false;
                markers.Add(marker);
            }

            SetView(1, false);
        }

        /// <summary>
        /// 見本を出したり隠したりします（深度を壁に重ねて表示するときは隠す）。
        /// </summary>
        /// <param name="visible">出すとき true</param>
        public void SetVisible(bool visible)
        {
            area.gameObject.SetActive(visible);
            if (!visible)
            {
                foreach (Text marker in markers)
                {
                    marker.enabled = false;
                }
            }
        }

        /// <summary>
        /// 表示の向きを変えます。
        /// </summary>
        /// <param name="quarterTurns">90°ずつの回転（0〜3）</param>
        /// <param name="mirrored">左右反転するか</param>
        public void SetView(int quarterTurns, bool mirrored)
        {
            rotation = ((quarterTurns % 4) + 4) % 4;
            mirror = mirrored;
            Vector2 areaSize = area.sizeDelta;
            bool sideways = rotation % 2 == 1;
            float shownWidth = sideways ? height : width;
            float shownHeight = sideways ? width : height;
            float scale = Mathf.Min(areaSize.x / shownWidth, areaSize.y / shownHeight);
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(width * scale, height * scale);
            rect.anchoredPosition = Vector2.zero;
            rect.localEulerAngles = new Vector3(0f, 0f, -90f * rotation);
            image.uvRect = mirror ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
        }

        /// <summary>
        /// 画面の位置が見本の上なら、元の深度画像の位置（0〜1、上端が 0）を返します。
        /// </summary>
        /// <param name="screenPosition">画面の位置</param>
        /// <param name="image01">深度画像の位置</param>
        /// <returns>見本の上なら true</returns>
        public bool TryScreenToImage(Vector2 screenPosition, out Vector2 image01)
        {
            image01 = Vector2.zero;
            RectTransform rect = image.rectTransform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, null, out local))
            {
                return false;
            }

            float lx = local.x / rect.rect.width + 0.5f;
            float ly = local.y / rect.rect.height + 0.5f;
            if (lx < 0f || lx > 1f || ly < 0f || ly > 1f)
            {
                return false;
            }

            float u = mirror ? 1f - lx : lx;
            image01 = new Vector2(u, 1f - ly);
            return true;
        }

        /// <summary>
        /// 深度と見分けの結果から、見本を描き直します。
        /// </summary>
        /// <param name="depth">深度（無ければ null）</param>
        /// <param name="calibration">校正</param>
        /// <param name="detector">当たり判定（使えないときは null）</param>
        /// <param name="corners">描く四隅（タップの途中なら 4 個未満）</param>
        public void Render(ushort[] depth, CalibrationData calibration, HitDetector detector, IList<Vector2> corners)
        {
            if (depth == null || depth.Length != width * height)
            {
                return;
            }

            byte[] classes = detector != null && detector.IsConfigured ? detector.Classes : null;
            bool[] blind = detector != null && detector.IsConfigured ? detector.BlindMask : null;
            ushort[] baseline = calibration != null && calibration.HasBaseline ? calibration.Baseline : null;
            for (int y = 0; y < height; y++)
            {
                // テクスチャは下の行から並ぶので、上下を入れ替えて写す
                int row = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    bool isBlind = blind != null ? blind[i] : (baseline != null && baseline[i] == 0 && depth[i] == 0);
                    pixels[row + x] = PixelColor(depth[i], classes != null ? classes[i] : HitDetector.ClassNone, isBlind);
                }
            }

            if (corners != null && corners.Count > 0)
            {
                DrawQuad(corners);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        /// <summary>
        /// 四隅の名前（①左上 など）を、タップした位置に出します。
        /// </summary>
        /// <param name="corners">タップした点（0〜4個）</param>
        public void ShowMarkers(IList<Vector2> corners)
        {
            RectTransform rect = image.rectTransform;
            for (int i = 0; i < markers.Count; i++)
            {
                bool visible = corners != null && i < corners.Count;
                markers[i].enabled = visible;
                if (!visible)
                {
                    continue;
                }

                Vector2 c = corners[i];
                float lx = mirror ? 1f - c.x : c.x;
                float ly = 1f - c.y;
                Vector3 local = new Vector3((lx - 0.5f) * rect.rect.width, (ly - 0.5f) * rect.rect.height, 0f);
                markers[i].rectTransform.position = rect.TransformPoint(local) + new Vector3(0f, 24f, 0f);
            }
        }

        /// <summary>
        /// 1つの画素の色を返します（深度の灰色に、見分けの結果の色を重ねる）。
        /// </summary>
        /// <param name="depth">深度（mm）</param>
        /// <param name="kind">見分けの結果</param>
        /// <param name="blind">ボールを取りにくい所か</param>
        /// <returns>色</returns>
        public static Color32 PixelColor(ushort depth, byte kind, bool blind)
        {
            if (kind != HitDetector.ClassNone)
            {
                return kind == HitDetector.ClassBall ? Red : kind == HitDetector.ClassBody ? Blue : Yellow;
            }

            Color32 color = Gray(depth);
            return blind ? Color32.Lerp(color, Purple, 0.6f) : color;
        }

        private static Color32 Gray(ushort depth)
        {
            if (depth == 0)
            {
                return Missing;
            }

            float t = Mathf.Clamp01((depth - 500f) / 4000f);
            byte v = (byte)(235 - t * 190);
            return new Color32(v, v, v, 255);
        }

        private void DrawQuad(IList<Vector2> corners)
        {
            for (int i = 0; i < corners.Count; i++)
            {
                DrawDot(corners[i], 4);
                if (i + 1 < corners.Count)
                {
                    DrawLine(corners[i], corners[i + 1]);
                }
            }

            if (corners.Count == 4)
            {
                DrawLine(corners[3], corners[0]);
            }
        }

        private void DrawLine(Vector2 a, Vector2 b)
        {
            Vector2 pa = new Vector2(a.x * width, a.y * height);
            Vector2 pb = new Vector2(b.x * width, b.y * height);
            int steps = Mathf.CeilToInt(Vector2.Distance(pa, pb)) + 1;
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(pa, pb, s / (float)steps);
                SetPixel(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Green);
                SetPixel(Mathf.RoundToInt(p.x) + 1, Mathf.RoundToInt(p.y), Green);
            }
        }

        private void DrawDot(Vector2 c, int radius)
        {
            int cx = Mathf.RoundToInt(c.x * width);
            int cy = Mathf.RoundToInt(c.y * height);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        SetPixel(cx + dx, cy + dy, Green);
                    }
                }
            }
        }

        private void SetPixel(int x, int y, Color32 color)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            pixels[(height - 1 - y) * width + x] = color;
        }
    }
}
