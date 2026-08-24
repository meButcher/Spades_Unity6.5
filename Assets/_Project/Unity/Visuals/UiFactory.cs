using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.Visuals
{
    /// <summary>
    /// Builds the view hierarchy from code.
    ///
    /// The scene therefore holds three objects and no wired references, which removes the entire
    /// class of failure where a prefab loses a link and something is silently null at runtime.
    /// It also means the layout is readable: what the table looks like is a function you can
    /// step through, not a tree you have to click into.
    /// </summary>
    public static class UiFactory
    {
        public static RectTransform Root(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Center(rect);
            return rect;
        }

        /// <summary>A rect that fills its parent completely.</summary>
        public static RectTransform Stretch(string name, Transform parent)
        {
            RectTransform rect = Root(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image Panel(string name, Transform parent, Sprite sprite, Color color, bool stretch = false)
        {
            RectTransform rect = stretch ? Stretch(name, parent) : Root(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;

            // Sliced only makes sense for a sprite that actually carries a nine-slice border;
            // asking for it on a borderless sprite silently degrades to Simple.
            image.type = HasBorder(sprite) ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static bool HasBorder(Sprite sprite) => sprite != null && sprite.border != Vector4.zero;

        public static Image Pip(string name, Transform parent, Sprite sprite, Color color, float size)
        {
            Image image = Panel(name, parent, sprite, color);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            Size(image.rectTransform, size, size);
            return image;
        }

        public static TextMeshProUGUI Text(
            string name, Transform parent, string content, float fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = Root(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            return text;
        }

        public static Button Button(
            string name, Transform parent, string label, Sprite sprite,
            Color background, Color textColor, float fontSize, Action onClick)
        {
            RectTransform rect = Root(name, parent);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = background;
            image.type = HasBorder(sprite) ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TextMeshProUGUI text = Text(name + " Label", rect, label, fontSize, textColor);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            if (onClick != null) button.onClick.AddListener(() => onClick());

            return button;
        }

        public static CanvasGroup Group(RectTransform rect)
        {
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            return group != null ? group : rect.gameObject.AddComponent<CanvasGroup>();
        }

        // -- rect helpers ---------------------------------------------------------------------

        public static void Center(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        public static RectTransform Size(RectTransform rect, float width, float height)
        {
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform At(RectTransform rect, float x, float y)
        {
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }

        public static RectTransform Place(RectTransform rect, float x, float y, float width, float height)
        {
            Center(rect);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>Anchors a rect to a screen corner so it survives aspect-ratio changes.</summary>
        public static RectTransform Corner(RectTransform rect, Vector2 anchor, float x, float y, float width, float height)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
    }
}
