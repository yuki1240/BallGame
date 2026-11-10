using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WallBurst.Ui
{
    /// <summary>
    /// 実行時にキャンバス部品を組む。シーンは空の受け皿だけで、現場操作はすべて UI 上で完結する。
    /// </summary>
    public static class UiFactory
    {
        /// <summary>
        /// 新しい入力システムで UI をクリックできるようにする。
        /// シーンに置いたままだと、次のシーンへ移った瞬間に消えてクリックが届かなくなる。
        /// </summary>
        public static void EnsureEventSystem()
        {
            EventSystem current = EventSystem.current;
            if (current != null && current.gameObject.scene.name == "DontDestroyOnLoad")
            {
                return;
            }

            GameObject eventObject = new GameObject("EventSystem");
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<InputSystemUIInputModule>();
            Object.DontDestroyOnLoad(eventObject);
        }

        /// <summary>
        /// 画面いっぱいのキャンバスを作る。
        /// </summary>
        /// <param name="name">オブジェクト名。</param>
        /// <returns>キャンバスの矩形。</returns>
        public static RectTransform CreateCanvas(string name)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasObject.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 親いっぱいに伸ばしたパネル。
        /// </summary>
        /// <param name="parent">親。</param>
        /// <param name="name">名前。</param>
        /// <param name="color">色。</param>
        /// <returns>矩形。</returns>
        public static RectTransform CreateStretch(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>
        /// アンカーで範囲を切った矩形。
        /// </summary>
        /// <param name="parent">親。</param>
        /// <param name="name">名前。</param>
        /// <param name="anchorMin">アンカー最小。</param>
        /// <param name="anchorMax">アンカー最大。</param>
        /// <returns>矩形。</returns>
        public static RectTransform CreateRegion(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        /// <summary>
        /// 縦スクロールの中身。現場で項目が増えても収まる。
        /// </summary>
        /// <param name="parent">右ペイン。</param>
        /// <returns>項目を並べる先。</returns>
        public static RectTransform CreateScrollContent(Transform parent)
        {
            RectTransform scrollRect = CreateRect(parent, "Scroll");
            Stretch(scrollRect);
            Image background = scrollRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = CreateRect(scrollRect, "Viewport");
            Stretch(viewport);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 28);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        /// <summary>
        /// 折り返す本文。
        /// </summary>
        /// <param name="parent">親。</param>
        /// <param name="body">本文。</param>
        /// <param name="size">字の大きさ。</param>
        /// <param name="height">確保する高さ。</param>
        /// <param name="anchor">揃え。</param>
        /// <returns>テキスト。</returns>
        public static Text CreateBody(Transform parent, string body, int size, float height, TextAnchor anchor)
        {
            Text text = CreateText(parent, body, size, anchor);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// ボタン。
        /// </summary>
        /// <param name="parent">親。</param>
        /// <param name="label">ラベル。</param>
        /// <param name="color">地の色。</param>
        /// <returns>ボタン。</returns>
        public static Button CreateButton(Transform parent, string label, Color color)
        {
            RectTransform rect = CreateRect(parent, label);
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 64f;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text caption = CreateText(rect, label, 26, TextAnchor.MiddleCenter);
            Stretch(caption.rectTransform);
            caption.color = Color.white;
            return button;
        }

        /// <summary>
        /// キャプション付きスライダー。値は整数。
        /// </summary>
        /// <param name="parent">親。</param>
        /// <param name="caption">項目名。</param>
        /// <param name="min">最小。</param>
        /// <param name="max">最大。</param>
        /// <param name="value">初期値。</param>
        /// <returns>スライダーとキャプション。</returns>
        public static LabeledSlider CreateSlider(Transform parent, string caption, float min, float max, float value)
        {
            RectTransform row = CreateRect(parent, caption);
            LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 76f;
            VerticalLayoutGroup column = row.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 4f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            Text label = CreateText(row, caption, 22, TextAnchor.MiddleLeft);
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredHeight = 28f;

            Slider slider = BuildSlider(row, min, max, value);
            LabeledSlider labeled = new LabeledSlider();
            labeled.Slider = slider;
            labeled.Caption = label;
            return labeled;
        }

        /// <summary>
        /// OS の日本語フォント。無ければ Unity の既定フォント。
        /// </summary>
        /// <returns>フォント。</returns>
        public static Font GetFont()
        {
            if (_font != null)
            {
                return _font;
            }

            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "MS Gothic", "Segoe UI" },
                32);
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _font;
        }

        /// <summary>
        /// テキスト部品。
        /// </summary>
        public static Text CreateText(Transform parent, string body, int size, TextAnchor anchor)
        {
            RectTransform rect = CreateRect(parent, "Text");
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = GetFont();
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(0.94f, 0.95f, 0.96f, 1f);
            text.text = body;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Font _font;

        /// <summary>
        /// 空の矩形を作る。
        /// </summary>
        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 親いっぱいに伸ばす。
        /// </summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// 横スライダーの見た目を組む。
        /// </summary>
        private static Slider BuildSlider(Transform parent, float min, float max, float value)
        {
            RectTransform root = CreateRect(parent, "Slider");
            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 36f;
            Slider slider = root.gameObject.AddComponent<Slider>();

            RectTransform background = CreateImage(root, "Background", new Color(0.05f, 0.06f, 0.08f, 1f));
            background.anchorMin = new Vector2(0f, 0.25f);
            background.anchorMax = new Vector2(1f, 0.75f);
            background.offsetMin = new Vector2(10f, 0f);
            background.offsetMax = new Vector2(-10f, 0f);

            RectTransform fillArea = CreateRect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(10f, 0f);
            fillArea.offsetMax = new Vector2(-10f, 0f);

            RectTransform fill = CreateImage(fillArea, "Fill", new Color(0.25f, 0.78f, 0.86f, 1f));
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            RectTransform handleArea = CreateRect(root, "Handle Slide Area");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);

            RectTransform handle = CreateImage(handleArea, "Handle", Color.white);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(22f, 0f);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.value = value;
            return slider;
        }

        /// <summary>
        /// 色付きの画像矩形。
        /// </summary>
        private static RectTransform CreateImage(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }
    }

    /// <summary>
    /// スライダーと、その数値を書くテキスト。
    /// </summary>
    public struct LabeledSlider
    {
        /// <summary>スライダー。</summary>
        public Slider Slider;

        /// <summary>項目名と数値。</summary>
        public Text Caption;
    }
}
