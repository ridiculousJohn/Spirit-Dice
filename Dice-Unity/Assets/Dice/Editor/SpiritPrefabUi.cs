using Dice.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Dice.Editor
{
    public enum SpiritButtonAppearance { Green, Amber, DarkCompact, GreenAction, AmberAction, WoodFooter }

    public static class SpiritPrefabUi
    {
        private const string ButtonFolder = "Assets/Dice/Content/Presentation/UI/Buttons";
        public static SpiritUiTheme Theme => AssetDatabase.LoadAssetAtPath<SpiritUiTheme>(
            "Assets/Dice/Presentation/Shared/SpiritHouseTheme.asset");

        public static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException(target.GetType().Name + "." + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Sprite FrameSprite(SpiritUiTheme.Frame frame)
        {
            if (frame == null) return null;
            if (frame.sprite != null) return frame.sprite;
            if (frame.texture == null) return null;
            string key = ReferenceEquals(frame, Theme.darkFrame) ? "Dark" :
                ReferenceEquals(frame, Theme.greenFrame) ? "Green" :
                ReferenceEquals(frame, Theme.amberFrame) ? "Amber" : "Wood";
            const string folder = "Assets/Dice/Content/Presentation/UI/Frames";
            EnsureFolder(folder);
            string path = folder + "/" + key + "Frame.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (saved != null)
            {
                frame.sprite = saved;
                EditorUtility.SetDirty(Theme);
                return saved;
            }
            RequireVacant(path);
            float sx = frame.referenceTextureSize.x > 0 ? frame.texture.width / frame.referenceTextureSize.x : 1;
            float sy = frame.referenceTextureSize.y > 0 ? frame.texture.height / frame.referenceTextureSize.y : 1;
            var area = frame.spriteRect.width > 0 && frame.spriteRect.height > 0 ? frame.spriteRect :
                new Rect(0, 0, frame.texture.width, frame.texture.height);
            if (frame.spriteRect.width > 0) area = new Rect(area.x * sx, area.y * sy, area.width * sx, area.height * sy);
            var border = new Vector4(Mathf.Min(frame.border.x * sx, area.width * .5f), Mathf.Min(frame.border.y * sy, area.height * .5f),
                Mathf.Min(frame.border.z * sx, area.width * .5f), Mathf.Min(frame.border.w * sy, area.height * .5f));
            var sprite = Sprite.Create(frame.texture, area, new Vector2(.5f, .5f), 100 * sy, 0, SpriteMeshType.FullRect, border);
            sprite.name = key + "Frame";
            AssetDatabase.CreateAsset(sprite, path);
            frame.sprite = sprite;
            EditorUtility.SetDirty(Theme);
            return sprite;
        }

        public static SpiritUiTheme GetTheme(Transform parent)
        {
            var context = parent.GetComponentInParent<SpiritUiContext>(true);
            if (context == null || context.Theme == null)
                return Theme;
            return context.Theme;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max,
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

        public static RectTransform Full(string name, Transform parent, float inset = 0)
            => Rect(name, parent, Vector2.zero, Vector2.one, inset, inset, inset, inset);

        public static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color,
            float inset = 0, bool border = false, SpiritUiTheme.Frame frame = null)
        {
            var rect = Rect(name, parent, min, max, inset, inset, inset, inset);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            bool decorated = border;
            if (decorated)
            {
                var theme = GetTheme(parent);
                ApplyFrame(image, frame ?? theme.darkFrame, color);
            }
            return image;
        }

        private static void ApplyFrame(Image image, SpiritUiTheme.Frame frame, Color fallback)
        {
            var sprite = FrameSprite(frame);
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

        public static Text Label(string name, Transform parent, string value, int size,
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

        public static Text Heading(string name, Transform parent, string value, int size,
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

        public static void Decorate(Transform parent, bool leaves = false)
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

        public static void ActionIcon(Transform parent, string kind)
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

        public static void SetButtonLabel(Button button, string value)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = value;
        }

        public static Button Button(string name, Transform parent, string value,
            Vector2 min, Vector2 max, UnityAction action = null, bool primary = false, bool compact = false,
            SpiritButtonAppearance? appearance = null)
        {
            CreateButtonAssets();
            SpiritButtonAppearance selected = appearance ?? (name == "RerollButton" ? SpiritButtonAppearance.GreenAction :
                name == "PlayButton" ? SpiritButtonAppearance.AmberAction :
                name == "CharmsButton" || name == "HandRanksButton" ? SpiritButtonAppearance.WoodFooter :
                compact ? SpiritButtonAppearance.DarkCompact : primary ? SpiritButtonAppearance.Amber : SpiritButtonAppearance.Green);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonFolder + "/" + selected + "Button.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = new Vector2(5, 5); rect.offsetMax = new Vector2(-5, -5);
            var button = instance.GetComponent<SpiritUiButtonVisual>();
            var text = button.GetComponentInChildren<Text>(true);
            text.name = name + "Label";
            text.text = value;
            if (action != null) button.onClick.AddListener(action);
            button.CaptureContent();
            return button;
        }

        /** Saved button variants keep shared structure and styling editable across authored screens. */
        public static void CreateButtonAssets()
        {
            EnsureFolder(ButtonFolder);
            string basePath = ButtonFolder + "/SpiritButton.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            if (source == null)
            {
                RequireVacant(basePath);
                var button = AuthorButton();
                source = PrefabUtility.SaveAsPrefabAsset(button.gameObject, basePath);
                UnityEngine.Object.DestroyImmediate(button.gameObject);
            }
            foreach (SpiritButtonAppearance appearance in System.Enum.GetValues(typeof(SpiritButtonAppearance)))
            {
                string path = ButtonFolder + "/" + appearance + "Button.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                RequireVacant(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                var image = instance.GetComponent<Image>();
                var text = instance.GetComponentInChildren<Text>(true);
                var theme = Theme;
                bool action = appearance == SpiritButtonAppearance.GreenAction || appearance == SpiritButtonAppearance.AmberAction;
                bool compact = appearance == SpiritButtonAppearance.DarkCompact;
                var frame = compact || action ? theme.darkFrame : appearance == SpiritButtonAppearance.Amber ? theme.amberFrame :
                    appearance == SpiritButtonAppearance.WoodFooter ? theme.woodFrame : theme.greenFrame;
                ApplyFrame(image, frame, theme.panelColor);
                if (action) image.color = appearance == SpiritButtonAppearance.AmberAction ? new Color32(255, 196, 103, 255) : new Color32(170, 216, 181, 255);
                float padding = compact ? .09f : appearance == SpiritButtonAppearance.WoodFooter ? .10f : .08f;
                text.rectTransform.anchorMin = new Vector2(padding, .08f);
                text.rectTransform.anchorMax = new Vector2(1 - padding, .92f);
                if (action) { text.fontSize = text.resizeTextMaxSize = 25; text.resizeTextMinSize = 18; }
                instance.name = appearance + "Button";
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static SpiritUiButtonVisual AuthorButton()
        {
            var rect = Full("SpiritButton", null);
            rect.gameObject.AddComponent<SpiritUiContext>().Configure(Theme);
            var image = rect.gameObject.AddComponent<Image>();
            var theme = Theme;
            ApplyFrame(image, theme.greenFrame, theme.panelColor);
            var button = rect.gameObject.AddComponent<SpiritUiButtonVisual>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = theme.highlightedTint;
            colors.selectedColor = theme.highlightedTint;
            colors.pressedColor = theme.pressedTint;
            colors.disabledColor = theme.disabledTint;
            colors.fadeDuration = .12f;
            button.colors = colors;
            button.targetGraphic = image;
            var text = Heading("Label", image.transform, "Button", 21, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            float padding = .08f;
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static void RequireVacant(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
                throw new System.InvalidOperationException("An authored asset already exists at " + path + ". Assign its required component explicitly.");
        }

        public static ScrollRect Scroll(string name, Transform parent, Vector2 min, Vector2 max, out RectTransform content)
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
