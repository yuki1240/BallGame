using UnityEngine;
using UnityEngine.UI;

namespace WallBurst.Ui
{
    /// <summary>
    /// ゲーム画面の上部に置く、リストとチェックボックス。
    /// </summary>
    public static class HudControls
    {
        /// <summary>
        /// 画像名を選ぶリスト。開くと下へ項目が並ぶ。
        /// </summary>
        /// <param name="parent">親。</param>
        /// <returns>リスト。</returns>
        public static Dropdown CreateImageList(Transform parent)
        {
            GameObject root = new GameObject("ImageList", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            root.transform.SetParent(parent, false);
            Image plate = root.GetComponent<Image>();
            plate.color = new Color(1f, 1f, 1f, 0.92f);

            Text caption = UiFactory.CreateText(root.transform, string.Empty, 24, TextAnchor.MiddleLeft);
            Stretch(caption.rectTransform, new Vector2(14f, 4f), new Vector2(-12f, -4f));
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;

            RectTransform template = CreateListTemplate(root.transform, out Text itemLabel);
            Dropdown dropdown = root.GetComponent<Dropdown>();
            dropdown.targetGraphic = plate;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        /// <summary>
        /// ラベル付きのチェックボックス。
        /// </summary>
        /// <param name="parent">親。</param>
        /// <param name="label">項目名。</param>
        /// <returns>チェックボックス。</returns>
        public static Toggle CreateCheckbox(Transform parent, string label)
        {
            GameObject root = new GameObject(label, typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);
            Toggle toggle = root.GetComponent<Toggle>();

            RectTransform box = CreateBox(root.transform);
            Image mark = CreateMark(box);
            Text caption = UiFactory.CreateText(root.transform, label, 24, TextAnchor.MiddleLeft);
            Stretch(caption.rectTransform, new Vector2(46f, 0f), new Vector2(-4f, 0f));
            caption.raycastTarget = true;

            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = mark;
            toggle.isOn = false;
            return toggle;
        }

        /// <summary>
        /// 開いたときの項目一覧。
        /// </summary>
        /// <param name="parent">リストの親。</param>
        /// <param name="itemLabel">項目の文字。</param>
        /// <returns>ひな型。</returns>
        private static RectTransform CreateListTemplate(Transform parent, out Text itemLabel)
        {
            GameObject templateObject = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateObject.transform.SetParent(parent, false);
            RectTransform template = templateObject.GetComponent<RectTransform>();
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = Vector2.zero;
            template.sizeDelta = new Vector2(0f, 260f);
            templateObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.96f);

            RectTransform viewport = CreateViewport(template);
            RectTransform content = CreateContent(viewport);
            RectTransform item = CreateItem(content, out itemLabel);

            ScrollRect scroll = templateObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            return template;
        }

        /// <summary>
        /// 一覧の表示枠。
        /// </summary>
        /// <param name="template">ひな型。</param>
        /// <returns>ビューポート。</returns>
        private static RectTransform CreateViewport(RectTransform template)
        {
            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(template, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(4f, 4f);
            viewport.offsetMax = new Vector2(-4f, -4f);
            Image image = viewportObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;
            return viewport;
        }

        /// <summary>
        /// 項目を縦に積む中身。
        /// </summary>
        /// <param name="viewport">ビューポート。</param>
        /// <returns>中身。</returns>
        private static RectTransform CreateContent(RectTransform viewport)
        {
            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        /// <summary>
        /// 一覧の1行。
        /// </summary>
        /// <param name="content">中身。</param>
        /// <param name="itemLabel">行の文字。</param>
        /// <returns>行。</returns>
        private static RectTransform CreateItem(RectTransform content, out Text itemLabel)
        {
            GameObject itemObject = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemObject.transform.SetParent(content, false);
            RectTransform item = itemObject.GetComponent<RectTransform>();
            Image background = itemObject.AddComponent<Image>();
            background.color = new Color(0.16f, 0.18f, 0.22f, 1f);

            RectTransform mark = CreateMarkRect(item);
            itemLabel = UiFactory.CreateText(item, "項目", 22, TextAnchor.MiddleLeft);
            Stretch(itemLabel.rectTransform, new Vector2(36f, 2f), new Vector2(-8f, -2f));

            Toggle toggle = itemObject.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = mark.GetComponent<Image>();
            toggle.isOn = false;
            return item;
        }

        /// <summary>
        /// 親いっぱいに広げ、内側の余白を残す。
        /// </summary>
        /// <param name="rect">矩形。</param>
        /// <param name="min">左下の余白。</param>
        /// <param name="max">右上の余白。右と上は負の値。</param>
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// チェックの箱。
        /// </summary>
        /// <param name="parent">チェックボックス。</param>
        /// <returns>箱。</returns>
        private static RectTransform CreateBox(Transform parent)
        {
            GameObject boxObject = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxObject.transform.SetParent(parent, false);
            RectTransform box = boxObject.GetComponent<RectTransform>();
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = new Vector2(8f, 0f);
            box.sizeDelta = new Vector2(28f, 28f);
            boxObject.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 1f);
            return box;
        }

        /// <summary>
        /// オンのときに出す印。
        /// </summary>
        /// <param name="box">箱。</param>
        /// <returns>印。</returns>
        private static Image CreateMark(RectTransform box)
        {
            GameObject markObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            markObject.transform.SetParent(box, false);
            RectTransform mark = markObject.GetComponent<RectTransform>();
            mark.anchorMin = Vector2.zero;
            mark.anchorMax = Vector2.one;
            mark.offsetMin = new Vector2(6f, 6f);
            mark.offsetMax = new Vector2(-6f, -6f);
            Image image = markObject.GetComponent<Image>();
            image.color = new Color(0.45f, 0.92f, 0.72f, 1f);
            return image;
        }

        /// <summary>
        /// 白い四角の印。
        /// </summary>
        /// <param name="parent">親。</param>
        /// <returns>印の矩形。</returns>
        private static RectTransform CreateMarkRect(Transform parent)
        {
            GameObject markObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            markObject.transform.SetParent(parent, false);
            RectTransform mark = markObject.GetComponent<RectTransform>();
            mark.anchorMin = new Vector2(0f, 0.5f);
            mark.anchorMax = new Vector2(0f, 0.5f);
            mark.pivot = new Vector2(0f, 0.5f);
            mark.anchoredPosition = new Vector2(8f, 0f);
            mark.sizeDelta = new Vector2(16f, 16f);
            markObject.GetComponent<Image>().color = new Color(0.45f, 0.92f, 0.72f, 1f);
            return mark;
        }
    }
}
