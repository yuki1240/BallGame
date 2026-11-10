using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WallBurst.Ui
{
    /// <summary>
    /// 深度のプレビュー。クリック位置を画素座標で返し、四隅と検知マスクを重ねる。
    /// </summary>
    public sealed class DepthPreviewWidget : MonoBehaviour, IPointerClickHandler
    {
        private RawImage _image;
        private RectTransform _rect;
        private Texture2D _texture;
        private Color32[] _colors;
        private int _width;
        private int _height;
        private readonly Vector2[] _corners = new Vector2[4];
        private int _definedCount;
        private RectTransform[] _markers;
        private RectTransform[] _lines;
        private static readonly Color[] MarkerColors =
        {
            new Color(1f, 0.86f, 0.2f, 1f),
            new Color(0.35f, 0.95f, 0.55f, 1f),
            new Color(0.35f, 0.85f, 1f, 1f),
            new Color(1f, 0.45f, 0.75f, 1f)
        };

        /// <summary>深度画像上の画素がクリックされた。</summary>
        public event Action<Vector2> PixelClicked;

        /// <summary>
        /// 画像と四隅の印を作る。
        /// </summary>
        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _image = GetComponent<RawImage>();
            _image.raycastTarget = true;
            _markers = new RectTransform[4];
            _lines = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                _markers[i] = CreateMarker(i);
                _lines[i] = CreateLine();
            }
        }

        /// <summary>
        /// 深度と、オレンジで塗る検知マスクを描く。マスクは null でよい。
        /// </summary>
        /// <param name="depth">ミリメートル。</param>
        /// <param name="width">幅。</param>
        /// <param name="height">高さ。</param>
        /// <param name="highlight">1 の画素をオレンジにする。無ければ null。</param>
        public void SetFrame(ushort[] depth, int width, int height, byte[] highlight)
        {
            if (depth == null || width <= 0 || height <= 0 || depth.Length < width * height)
            {
                return;
            }

            EnsureTexture(width, height);
            Paint(depth, highlight);
            _texture.SetPixels32(_colors);
            _texture.Apply(false);
        }

        /// <summary>
        /// 指定済みの四隅を描く。個数は 0 から 4。
        /// </summary>
        /// <param name="corners">4 要素。未使用分は無視する。</param>
        /// <param name="definedCount">有効な個数。順番に入っている前提。</param>
        public void SetCorners(Vector2[] corners, int definedCount)
        {
            _definedCount = Mathf.Clamp(definedCount, 0, 4);
            if (corners == null)
            {
                return;
            }

            for (int i = 0; i < _definedCount; i++)
            {
                _corners[i] = corners[i];
            }
        }

        /// <summary>
        /// レイアウト後に印の位置を合わせる。
        /// </summary>
        private void LateUpdate()
        {
            PlaceOverlays();
        }

        /// <summary>
        /// クリックを深度画素に直す。Y は画像上端が 0。
        /// </summary>
        /// <param name="eventData">ポインタ。</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_width <= 0)
            {
                return;
            }

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.pressEventCamera, out local))
            {
                return;
            }

            Rect area = _rect.rect;
            float u = Mathf.InverseLerp(area.xMin, area.xMax, local.x);
            float v = Mathf.InverseLerp(area.yMin, area.yMax, local.y);
            Vector2 pixel = new Vector2(u * _width, (1f - v) * _height);
            if (PixelClicked != null)
            {
                PixelClicked(pixel);
            }
        }

        /// <summary>
        /// テクスチャをサイズが変わったときだけ作り直す。
        /// </summary>
        private void EnsureTexture(int width, int height)
        {
            if (_texture != null && _width == width && _height == height)
            {
                return;
            }

            if (_texture != null)
            {
                Destroy(_texture);
            }

            _width = width;
            _height = height;
            _colors = new Color32[width * height];
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _texture.filterMode = FilterMode.Point;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _image.texture = _texture;
            AspectRatioFitter fitter = GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.aspectRatio = width / (float)height;
            }
        }

        /// <summary>
        /// 近いほど明るく、検知画素はオレンジで塗る。テクスチャの下が画像の下になるよう上下を反転する。
        /// </summary>
        private void Paint(ushort[] depth, byte[] highlight)
        {
            for (int y = 0; y < _height; y++)
            {
                int sourceRow = y * _width;
                int targetRow = (_height - 1 - y) * _width;
                for (int x = 0; x < _width; x++)
                {
                    int source = sourceRow + x;
                    byte shade = Shade(depth[source]);
                    Color32 color = new Color32(shade, shade, shade, 255);
                    if (highlight != null && source < highlight.Length && highlight[source] != 0)
                    {
                        color = new Color32(255, 150, 40, 255);
                    }

                    _colors[targetRow + x] = color;
                }
            }
        }

        /// <summary>
        /// 0.5m〜4.5m を明るい灰色から暗い灰色にする。0 は黒。
        /// </summary>
        private static byte Shade(ushort millimeters)
        {
            if (millimeters == 0)
            {
                return 0;
            }

            float t = Mathf.InverseLerp(500f, 4500f, millimeters);
            return (byte)Mathf.Lerp(235f, 28f, t);
        }

        /// <summary>
        /// 四隅の印と辺を、今のレイアウトに合わせて置く。
        /// </summary>
        private void PlaceOverlays()
        {
            bool ready = _width > 0 && _rect.rect.width > 2f;
            for (int i = 0; i < 4; i++)
            {
                bool show = ready && i < _definedCount;
                _markers[i].gameObject.SetActive(show);
                _lines[i].gameObject.SetActive(show && _definedCount == 4);
                if (!show)
                {
                    continue;
                }

                _markers[i].anchoredPosition = PixelToLocal(_corners[i]);
            }

            if (!ready || _definedCount < 4)
            {
                return;
            }

            PlaceLine(0, _corners[0], _corners[1]);
            PlaceLine(1, _corners[1], _corners[2]);
            PlaceLine(2, _corners[2], _corners[3]);
            PlaceLine(3, _corners[3], _corners[0]);
        }

        /// <summary>
        /// 2点のあいだに細い帯を置く。
        /// </summary>
        private void PlaceLine(int index, Vector2 fromPixel, Vector2 toPixel)
        {
            Vector2 from = PixelToLocal(fromPixel);
            Vector2 to = PixelToLocal(toPixel);
            Vector2 delta = to - from;
            _lines[index].anchoredPosition = (from + to) * 0.5f;
            _lines[index].sizeDelta = new Vector2(delta.magnitude, 4f);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            _lines[index].localEulerAngles = new Vector3(0f, 0f, angle);
        }

        /// <summary>
        /// 深度画素を、この画像のローカル座標にする。
        /// </summary>
        private Vector2 PixelToLocal(Vector2 pixel)
        {
            Rect area = _rect.rect;
            float u = pixel.x / _width;
            float v = 1f - (pixel.y / _height);
            return new Vector2(Mathf.Lerp(area.xMin, area.xMax, u), Mathf.Lerp(area.yMin, area.yMax, v));
        }

        /// <summary>
        /// 番号付きの隅マーカー。
        /// </summary>
        private RectTransform CreateMarker(int index)
        {
            GameObject markerObject = new GameObject("Corner" + (index + 1), typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(transform, false);
            RectTransform rect = markerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(28f, 28f);
            Image image = markerObject.GetComponent<Image>();
            image.color = MarkerColors[index];
            image.raycastTarget = false;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(rect, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text label = labelObject.GetComponent<Text>();
            label.font = UiFactory.GetFont();
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.08f, 0.08f, 0.1f, 1f);
            label.text = (index + 1).ToString();
            label.raycastTarget = false;
            markerObject.SetActive(false);
            return rect;
        }

        /// <summary>
        /// 四辺用の細い画像。
        /// </summary>
        private RectTransform CreateLine()
        {
            GameObject lineObject = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(transform, false);
            RectTransform rect = lineObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(8f, 4f);
            Image image = lineObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.85f);
            image.raycastTarget = false;
            lineObject.SetActive(false);
            return rect;
        }

        /// <summary>
        /// テクスチャを漏らさない。
        /// </summary>
        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }
    }
}
