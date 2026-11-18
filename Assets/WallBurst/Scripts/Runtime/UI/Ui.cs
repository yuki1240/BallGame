using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// 実行時に uGUI の部品（Canvas・文字・ボタン）を組み立てる道具です。
    /// Canvas は 1920×1080 を基準に、画面の大きさに合わせて拡大・縮小します。
    /// 位置は、親の左下を原点にした画素（上向きが正）で指定します。
    /// </summary>
    public static class Ui
    {
        /// <summary>基準の解像度。</summary>
        public static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        /// <summary>文字の色（白）。</summary>
        public static readonly Color TextColor = new Color(0.97f, 0.97f, 0.97f, 1f);

        /// <summary>パネルの色。</summary>
        public static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.16f, 0.92f);

        /// <summary>ボタンの色。</summary>
        public static readonly Color ButtonColor = new Color(0.22f, 0.42f, 0.78f, 1f);

        /// <summary>目立たせるボタンの色（緑）。</summary>
        public static readonly Color PrimaryColor = new Color(0.18f, 0.62f, 0.32f, 1f);

        /// <summary>注意のボタンの色（だいだい）。</summary>
        public static readonly Color WarningColor = new Color(0.85f, 0.45f, 0.12f, 1f);

        private static Font cachedFont;

        /// <summary>
        /// 日本語を表示できるフォントを返します（Windows のフォントを使い、無ければ Unity の標準フォント）。
        /// </summary>
        /// <returns>フォント</returns>
        public static Font GetFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "BIZ UDPGothic", "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Arial" }, 32);
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return cachedFont;
        }

        /// <summary>
        /// 新しい Input System 用の EventSystem を1つだけ用意します（ボタンを押せるようにするため）。
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>
        /// 画面いっぱいの Canvas を作ります。
        /// </summary>
        /// <param name="name">名前</param>
        /// <param name="sortingOrder">描画順（大きいほど手前）</param>
        /// <returns>Canvas</returns>
        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>
        /// 親の左下を原点にして、四角い部品を置きます。
        /// </summary>
        /// <param name="target">部品</param>
        /// <param name="position">左下の位置</param>
        /// <param name="size">大きさ</param>
        public static void Place(RectTransform target, Vector2 position, Vector2 size)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.zero;
            target.pivot = Vector2.zero;
            target.anchoredPosition = position;
            target.sizeDelta = size;
        }

        /// <summary>
        /// 親いっぱいに広げます。
        /// </summary>
        /// <param name="target">部品</param>
        public static void Stretch(RectTransform target)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.pivot = new Vector2(0.5f, 0.5f);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 色の付いた四角（パネル）を作ります。
        /// </summary>
        /// <param name="parent">親</param>
        /// <param name="name">名前</param>
        /// <param name="color">色</param>
        /// <returns>Image</returns>
        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>
        /// 文字を作ります。
        /// </summary>
        /// <param name="parent">親</param>
        /// <param name="content">文字</param>
        /// <param name="fontSize">大きさ</param>
        /// <param name="anchor">寄せ方</param>
        /// <returns>Text</returns>
        public static Text CreateText(Transform parent, string content, int fontSize, TextAnchor anchor)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = GetFont();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = TextColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// ボタンを作ります。
        /// </summary>
        /// <param name="parent">親</param>
        /// <param name="label">ボタンの文字</param>
        /// <param name="color">色</param>
        /// <param name="onClick">押したときの処理</param>
        /// <returns>Button</returns>
        public static Button CreateButton(Transform parent, string label, Color color, UnityAction onClick)
        {
            Image background = CreatePanel(parent, "Button " + label, color);
            Button button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            Text text = CreateText(background.transform, label, 34, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform);
            return button;
        }

        /// <summary>
        /// ボタンの文字を変えます。
        /// </summary>
        /// <param name="button">ボタン</param>
        /// <param name="label">文字</param>
        public static void SetLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
