using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Dice.Presentation
{
    internal static class SpiritUi
    {
        internal static SpiritUiTheme GetTheme(Transform parent)
        {
            var context = parent.GetComponentInParent<SpiritUiContext>(true);
            if (context == null || context.Theme == null)
                throw new System.InvalidOperationException("The UI hierarchy requires an assigned theme.");
            return context.Theme;
        }

        internal static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max,
            float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        internal static RectTransform Full(string name, Transform parent, float inset = 0)
            => Rect(name, parent, Vector2.zero, Vector2.one, inset, inset, inset, inset);

        internal static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color,
            float inset = 0, bool border = false)
        {
            var rect = Rect(name, parent, min, max, inset, inset, inset, inset);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            bool decorated = border || name == "RewardsPanel" || name == "CharmsOverlay";
            if (decorated)
            {
                var theme = GetTheme(parent);
                var frame = name == "TargetPanel" ? theme.greenFrame : theme.darkFrame;
                bool largePanel = name == "RewardsPanel" || name == "CharmsOverlay" || name == "HandRanksOverlay" ||
                    name == "MenuPanel" || name == "EndingPanel";
                var frameImage = image;
                if (largePanel)
                {
                    image.color = theme.panelColor;
                    frameImage = Full("Frame", rect).gameObject.AddComponent<Image>();
                    frameImage.raycastTarget = false;
                }
                ApplyFrame(frameImage, frame, color);
            }
            return image;
        }

        private static void ApplyFrame(Image image, SpiritUiTheme.Frame frame, Color fallback)
        {
            var sprite = frame?.GetSprite();
            image.sprite = sprite;
            if (sprite != null)
            {
                image.type = Image.Type.Sliced;
                image.material = frame.surfaceMaterial;
                image.pixelsPerUnitMultiplier = Mathf.Max(.1f, frame.pixelsPerUnitMultiplier);
                image.color = Color.white;
            }
            else
            {
                image.color = fallback;
                var outline = image.gameObject.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
                var amber = GetTheme(image.transform).amber;
                outline.effectColor = new Color(amber.r, amber.g, amber.b, .6f);
                outline.effectDistance = new Vector2(1, -1);
            }
        }

        internal static Text Label(string name, Transform parent, string value, int size,
            Vector2 min, Vector2 max, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rect = Rect(name, parent, min, max, 12, 4, 12, 4);
            var text = rect.gameObject.AddComponent<Text>();
            var theme = GetTheme(parent);
            text.font = theme.bodyFont;
            text.text = value;
            text.fontSize = size;
            text.color = color ?? theme.cream;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        internal static Text Heading(string name, Transform parent, string value, int size,
            Vector2 min, Vector2 max, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var text = Label(name, parent, value, size, min, max, align, color);
            var theme = GetTheme(parent);
            text.font = theme.headingFont != null ? theme.headingFont : text.font;
            text.rectTransform.offsetMin = new Vector2(12, 0);
            text.rectTransform.offsetMax = new Vector2(-12, 0);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(size, Mathf.Max(14, Mathf.RoundToInt(size * .65f)));
            text.resizeTextMaxSize = size;
            return text;
        }

        internal static void Decorate(Transform parent, bool leaves = false)
        {
            if (leaves)
            {
                Ornament("LeftLeaves", parent, "leaf", new Vector2(.06f, .18f), new Vector2(.19f, .82f));
                var right = Ornament("RightLeaves", parent, "leaf", new Vector2(.81f, .18f), new Vector2(.94f, .82f));
                right.mirrored = true;
            }
            else Ornament("GoldDivider", parent, "divider", new Vector2(.07f, .53f), new Vector2(.93f, .55f));
            parent.GetComponent<SpiritUiButtonVisual>()?.CaptureContent();
        }

        internal static void ActionIcon(Transform parent, string kind)
        {
            Ornament("ActionIcon", parent, kind, new Vector2(.10f, .24f), new Vector2(.21f, .76f));
            var text = parent.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.rectTransform.anchorMin = new Vector2(.23f, 0);
                text.rectTransform.anchorMax = new Vector2(.92f, 1);
                text.rectTransform.offsetMin = new Vector2(0, 4);
                text.rectTransform.offsetMax = new Vector2(0, -4);
            }
            parent.GetComponent<SpiritUiButtonVisual>()?.CaptureContent();
        }

        private static SpiritUiOrnament Ornament(string name, Transform parent, string kind, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent, min, max);
            var ornament = rect.gameObject.AddComponent<SpiritUiOrnament>();
            ornament.kind = kind;
            ornament.color = GetTheme(parent).amber;
            ornament.raycastTarget = false;
            return ornament;
        }

        internal static void SetButtonLabel(Button button, string value)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = value;
        }

        internal static Button Button(string name, Transform parent, string value,
            Vector2 min, Vector2 max, UnityAction action, bool primary = false, bool compact = false)
        {
            var theme = GetTheme(parent);
            var image = Panel(name, parent, min, max,
                primary ? new Color32(89, 73, 44, 255) : theme.panelColor, 5);
            compact |= name == "MenuButton" || name == "CloseCharmsButton" || name == "ReturnToInvestigationButton";
            bool actionButton = name == "RerollButton" || name == "PlayButton";
            bool footer = name == "CharmsButton" || name == "HandRanksButton";
            var frame = compact || actionButton ? theme.darkFrame : footer ? theme.woodFrame : primary ? theme.amberFrame : theme.greenFrame;
            ApplyFrame(image, frame, primary ? new Color32(89, 73, 44, 255) : theme.panelColor);
            if (actionButton)
                image.color = primary ? new Color32(255, 196, 103, 255) : new Color32(170, 216, 181, 255);
            var button = image.gameObject.AddComponent<SpiritUiButtonVisual>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = theme.highlightedTint;
            colors.selectedColor = theme.highlightedTint;
            colors.pressedColor = theme.pressedTint;
            colors.disabledColor = theme.disabledTint;
            colors.fadeDuration = .12f;
            button.colors = colors;
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = Heading(name + "Label", image.transform, value, 21, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            float padding = compact ? .09f : footer ? .10f : .08f;
            text.rectTransform.anchorMin = new Vector2(padding, .08f);
            text.rectTransform.anchorMax = new Vector2(1 - padding, .92f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 21;
            button.CaptureContent();
            return button;
        }

        internal static ScrollRect Scroll(string name, Transform parent, Vector2 min, Vector2 max, out RectTransform content)
        {
            var frame = Rect(name, parent, min, max, 12, 8, 12, 8);
            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            var viewport = Full("Viewport", frame);
            var image = viewport.gameObject.AddComponent<Image>();
            image.color = new Color(0, 0, 0, .05f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            return scroll;
        }
    }
}
