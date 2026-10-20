using System;
using Dice.Engine;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Ui = Dice.Editor.SpiritPrefabUi;
using Object = UnityEngine.Object;

namespace Dice.Editor
{
    /** Creates the initial editable charm UI assets without replacing authored prefabs. */
    public static class SpiritCharmPrefabAuthoring
    {
        private const string Folder = "Assets/Dice/Content/Presentation/UI";

        public static void CreateAssets()
        {
            EnsureFolder(Folder);
            var reward = Create<SpiritRewardCard>("SpiritRewardCard", RewardCard);
            var owned = Create<SpiritOwnedCharmCard>("SpiritOwnedCharmCard", OwnedCard);
            var family = Create<SpiritModifierFamilyRow>("SpiritModifierFamilyRow", FamilyRow);
            Create<SpiritCharmPopup>("SpiritCharmPopup", Popup);
            Create<SpiritOwnedCharmsPanel>("SpiritOwnedCharmsPanel", panel => Owned(panel, owned));
            Create<SpiritRewardsPanel>("SpiritRewardsPanel", panel => Rewards(panel, reward));
            Create<SpiritCollectionView>("SpiritCollectionView", panel => Collection(panel, family));
            Create<SpiritEncounterCharmsScreen>("SpiritEncounterCharmsScreen", panel => Encounter(panel, family));
            Create<SpiritHandRanksPanel>("SpiritHandRanksPanel", HandRanks);
        }

        /** Applies the original local button appearances to existing nested button instances. */
        public static void RepairAuthoredButtonStyles()
        {
            string ownedPath = Folder + "/SpiritOwnedCharmsPanel.prefab";
            var ownedRoot = PrefabUtility.LoadPrefabContents(ownedPath);
            try
            {
                var owned = ownedRoot.GetComponent<SpiritOwnedCharmsPanel>();
                var image = (Image)owned.CloseButton.targetGraphic;
                Frame(image, Ui.Theme.darkFrame);
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                PrefabUtility.SaveAsPrefabAsset(ownedRoot, ownedPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(ownedRoot); }
            string collectionPath = Folder + "/SpiritCollectionView.prefab";
            var collectionRoot = PrefabUtility.LoadPrefabContents(collectionPath);
            try
            {
                using var serialized = new SerializedObject(collectionRoot.GetComponent<SpiritCollectionView>());
                foreach (string field in new[] { "closeButton", "filterButton" })
                {
                    var button = (Button)serialized.FindProperty(field).objectReferenceValue;
                    PlainButton(button);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(button.targetGraphic);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(button.GetComponentInChildren<Text>(true));
                }
                PrefabUtility.SaveAsPrefabAsset(collectionRoot, collectionPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(collectionRoot); }
        }

        private static T Create<T>(string name, Action<T> author) where T : Component
        {
            string path = Folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
                throw new InvalidOperationException("An asset already exists at " + path + ". Assign its required component explicitly.");
            var root = new GameObject(name, typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                root.AddComponent<SpiritUiContext>().Configure(Ui.Theme);
                var component = root.AddComponent<T>();
                author(component);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                return saved.GetComponent<T>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? serialized.FindProperty("<" + field + ">k__BackingField");
            if (property == null) throw new InvalidOperationException(target.name + " is missing serialized field " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRowRef(SerializedProperty row, string field, Object value)
            => row.FindPropertyRelative("<" + field + ">k__BackingField").objectReferenceValue = value;

        private static void Set(Object target, string field, Color value)
        {
            using var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? serialized.FindProperty("<" + field + ">k__BackingField");
            if (property == null) throw new InvalidOperationException(target.name + " is missing serialized field " + field);
            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Frame(Image image, SpiritUiTheme.Frame frame)
        {
            image.sprite = Ui.FrameSprite(frame);
            image.material = frame.surfaceMaterial;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = frame.pixelsPerUnitMultiplier;
            image.color = Color.white;
        }

        private static Image Back(Component component, bool framed = false, bool opaque = false)
        {
            var image = component.gameObject.AddComponent<Image>();
            image.color = Ui.Theme.panelColor;
            if (framed)
            {
                var surface = Ui.Full("Frame", component.transform).gameObject.AddComponent<Image>();
                surface.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                surface.raycastTarget = false;
                Frame(surface, Ui.Theme.darkFrame);
                if (!opaque)
                {
                    image.color = Color.clear;
                    return surface;
                }
            }
            return image;
        }

        private static void ButtonColors(Button button, bool disabled = true)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = Ui.Theme.highlightedTint;
            colors.pressedColor = Ui.Theme.pressedTint;
            if (disabled) colors.disabledColor = Ui.Theme.disabledTint;
            colors.fadeDuration = .12f;
            button.colors = colors;
        }

        private static void PlainButton(Button button)
        {
            var image = (Image)button.targetGraphic;
            image.sprite = null; image.material = null; image.type = Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1; image.color = Ui.Theme.panelColor;
            var outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
            var amber = Ui.Theme.amber;
            outline.effectColor = new Color(amber.r, amber.g, amber.b, .6f);
            outline.effectDistance = new Vector2(1, -1);
            var label = button.GetComponentInChildren<Text>(true);
            label.fontSize = label.resizeTextMaxSize = 20;
            label.color = Ui.Theme.cream;
        }

        private static Image CardBackground(string name, Transform parent, bool framed, Color color)
        {
            var rect = Ui.Full(name, parent);
            rect.offsetMin = new Vector2(-16, -16);
            rect.offsetMax = new Vector2(16, 16);
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
            if (framed) Frame(image, Ui.Theme.darkFrame);
            return image;
        }

        private static VerticalLayoutGroup Stack(Transform parent, int padding = 16, float spacing = 10)
        {
            var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static RectTransform Row(string name, Transform parent, float height = -1)
        {
            var rect = Ui.Full(name, parent);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            if (height >= 0) Size(rect, height);
            return rect;
        }

        private static void Size(Component component, float height, float width = -1, float flexibleHeight = 0)
        {
            var element = component.GetComponent<LayoutElement>() ?? component.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.preferredWidth = width;
            element.flexibleHeight = flexibleHeight;
            element.flexibleWidth = width >= 0 ? 0 : 1;
        }

        private static Text Label(string name, Transform parent, string text = "", int size = 18, bool heading = false)
        {
            var label = heading ? Ui.Heading(name, parent, text, size, Vector2.zero, Vector2.one) :
                Ui.Label(name, parent, text, size, Vector2.zero, Vector2.one);
            label.resizeTextForBestFit = false;
            label.supportRichText = true;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }

        private static Button Button(string name, Transform parent, string text)
        {
            var button = Ui.Button(name, parent, text, Vector2.zero, Vector2.one);
            Size(button, 48);
            return button;
        }

        private static ScrollRect List(string name, Transform parent, out RectTransform content)
        {
            var scroll = Ui.Scroll(name, parent, Vector2.zero, Vector2.one, out content);
            Size(scroll, 0, -1, 1);
            Stack(content, 4, 10);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return scroll;
        }

        private static Text Header(Component panel, string title, string titleName, string closeName, out Button close)
        {
            Back(panel, true, true);
            Stack(panel.transform);
            var header = Row("Header", panel.transform, 56);
            var text = Label(titleName, header, title, 29, true);
            Size(text, 44);
            close = Button(closeName, header, "Close");
            Size(close, 48, 108);
            return text;
        }

        private static RectTransform Artwork(Transform parent, float size, out Image icon,
            out SpiritModifierSigilGraphic sigil, out SpiritModifierCategoryBadge category, string prefix = "Charm")
        {
            var rect = Ui.Full("Artwork", parent);
            Size(rect, size, size);
            var fixedSize = rect.GetComponent<LayoutElement>();
            fixedSize.minWidth = fixedSize.minHeight = size;
            rect.sizeDelta = new Vector2(size, size);
            icon = Ui.Full(prefix + "Icon", rect).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            sigil = Ui.Full(prefix + "Sigil", rect).gameObject.AddComponent<SpiritModifierSigilGraphic>();
            sigil.raycastTarget = false;
            category = AddCategoryBadge(prefix + "Category", icon);
            return rect;
        }

        public static SpiritModifierCategoryBadge AddCategoryBadge(string name, Image icon)
        {
            var rect = Ui.Rect(name, icon.transform.parent, icon.rectTransform.anchorMin, icon.rectTransform.anchorMax);
            rect.pivot = icon.rectTransform.pivot;
            rect.sizeDelta = icon.rectTransform.sizeDelta;
            rect.anchoredPosition = icon.rectTransform.anchoredPosition;
            var badge = rect.gameObject.AddComponent<SpiritModifierCategoryBadge>();
            var accent = Ui.Rect("Accent", rect, Vector2.zero, new Vector2(0, 1), -5, 2, 2, 2).gameObject.AddComponent<Image>();
            accent.raycastTarget = false;
            var backing = Ui.Rect("Backing", rect, new Vector2(1, 0), new Vector2(1, 0)).gameObject.AddComponent<Image>();
            backing.rectTransform.sizeDelta = new Vector2(18, 18);
            backing.rectTransform.anchoredPosition = new Vector2(-4, 4);
            backing.color = new Color32(18, 27, 24, 255);
            backing.raycastTarget = false;
            var border = backing.gameObject.AddComponent<Outline>();
            border.effectDistance = new Vector2(1, -1);
            var glyph = Ui.Full("Glyph", backing.transform, 2).gameObject.AddComponent<Image>();
            glyph.preserveAspect = true;
            glyph.raycastTarget = false;
            Set(badge, "Accent", accent); Set(badge, "Glyph", glyph); Set(badge, "border", border);
            return badge;
        }

        public static SpiritCharmStatusBadge AddStatusBadge(Transform parent)
        {
            var rect = Ui.Full("StatusBadge", parent);
            Size(rect, 24, 24);
            var fixedSize = rect.GetComponent<LayoutElement>();
            fixedSize.minWidth = fixedSize.minHeight = 24;
            var badge = rect.gameObject.AddComponent<SpiritCharmStatusBadge>();
            var icon = Ui.Full("StatusIcon", rect).gameObject.AddComponent<SpiritCharmStatusGraphic>();
            icon.raycastTarget = false;
            Set(badge, "Icon", icon);
            return badge;
        }

        private static void OwnedCard(SpiritOwnedCharmCard card)
        {
            var image = Back(card, true);
            Stack(card.transform, 16, 8);
            var cardLayout = card.gameObject.AddComponent<LayoutElement>();
            cardLayout.minHeight = 156;
            cardLayout.flexibleWidth = 1;
            var button = card.gameObject.AddComponent<SpiritUiButtonVisual>();
            button.targetGraphic = image;
            Set(card, "Button", button);
            var header = Row("Header", card.transform);
            Artwork(header, 36, out var icon, out var sigil, out var category);
            Set(card, "Icon", icon); Set(card, "Sigil", sigil); Set(card, "Category", category);
            Set(card, "Title", Label("Title", header, "Charm", 17, true));
            Set(card, "Body", Label("Condition", card.transform, "Condition", 15));
            Set(card, "Effect", Label("Effects", card.transform, "Effect", 16));
            var footer = Row("Footer", card.transform, 24);
            var spacer = Ui.Full("Spacer", footer); Size(spacer, 24);
            Set(card, "Badge", AddStatusBadge(footer));
            var selection = Ui.Full("SelectionOutline", card.transform).gameObject.AddComponent<SpiritCharmSelectionGraphic>();
            selection.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            selection.color = new Color32(255, 215, 119, 255);
            selection.raycastTarget = false;
            selection.gameObject.SetActive(false);
            Set(card, "SelectionOutline", selection);
            Set(card, "NormalColor", Color.white);
            Set(card, "SelectedColor", new Color32(255, 232, 179, 255));
            button.CaptureContent();
        }

        private static void RewardCard(SpiritRewardCard card)
        {
            var image = Back(card); Stack(card.transform, 16, 8);
            image.color = Color.clear;
            var cardLayout = card.gameObject.AddComponent<LayoutElement>();
            cardLayout.minHeight = 104;
            cardLayout.flexibleWidth = 1;
            var button = card.gameObject.AddComponent<SpiritUiButtonVisual>();
            button.targetGraphic = image; Set(card, "Button", button);
            ButtonColors(button);
            var modifier = Ui.Full("Modifier", card.transform); Stack(modifier, 0, 8);
            modifier.gameObject.AddComponent<LayoutElement>().minHeight = 148;
            Set(card, "modifierBackground", CardBackground("ModifierBackground", modifier, true, Color.white));
            Set(card, "modifierRoot", modifier.gameObject);
            var header = Row("Header", modifier);
            Artwork(header, 42, out var icon, out var sigil, out var category);
            Set(card, "charmIcon", icon); Set(card, "charmSigil", sigil); Set(card, "charmCategory", category);
            var identity = Ui.Full("Identity", header); Stack(identity, 0, 4);
            Set(card, "title", Label("Title", identity, "Charm", 20, true));
            Set(card, "effects", Label("Effects", identity, "Effect", 18));
            Set(card, "condition", Label("Condition", modifier, "Description", 18));
            var hand = Ui.Full("HandUpgrade", card.transform); Stack(hand, 0, 6);
            Set(card, "handBackground", CardBackground("HandBackground", hand, false, new Color32(26, 40, 32, 215)));
            Set(card, "handRoot", hand.gameObject);
            var handHeader = Row("Header", hand);
            var handTitle = Label("Title", handHeader, "Hand", 18, true); Size(handTitle, -1);
            Set(card, "handTitle", handTitle);
            var handLevel = Label("Level", handHeader, "Level 0", 15); handLevel.color = Ui.Theme.muted;
            Set(card, "handLevelText", handLevel);
            var handBody = Row("Body", hand);
            var pattern = Ui.Full("HandPattern", handBody).gameObject.AddComponent<SpiritHandPatternGraphic>();
            pattern.raycastTarget = false; Size(pattern, 34, 174);
            Set(card, "handPattern", pattern);
            var handValues = Label("Values", handBody, "BASE 0\nMULT 0", 16); handValues.color = Ui.Theme.amber;
            Set(card, "handValues", handValues);
            Set(card, "modifierColor", Color.white);
            Set(card, "modifierSelectedColor", new Color(1.12f, 1.08f, .82f, 1));
            Set(card, "handColor", new Color32(26, 40, 32, 215));
            Set(card, "handSelectedColor", new Color32(43, 65, 49, 255));
            Set(card, "titleColor", Ui.Theme.cream);
            Set(card, "selectedTitleColor", Ui.Theme.amber);
            Set(card, "handLevelColor", Ui.Theme.muted);
            var divider = Ui.Rect("UpgradeDivider", hand, Vector2.zero, new Vector2(1, 0), -2, -16, -2, 15).gameObject.AddComponent<Image>();
            divider.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            divider.color = new Color(Ui.Theme.amber.r, Ui.Theme.amber.g, Ui.Theme.amber.b, .3f);
            divider.raycastTarget = false;
            hand.gameObject.SetActive(false);
            var selection = Ui.Rect("UpgradeSelectionMarker", card.transform, Vector2.zero, new Vector2(0, 1), 0, 12, -3, 12).gameObject.AddComponent<Image>();
            selection.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            selection.raycastTarget = false; selection.color = Ui.Theme.amber;
            selection.gameObject.SetActive(false); Set(card, "selectionMarker", selection);
            button.CaptureContent();
        }

        private static void FamilyRow(SpiritModifierFamilyRow family)
        {
            Back(family, true); Stack(family.transform, 16, 10);
            var header = Row("Identity", family.transform);
            var artwork = Artwork(header, 40, out var icon, out var sigil, out var category, "Family");
            Set(family, "artwork", artwork.gameObject);
            category.gameObject.SetActive(false);
            Set(family, "icon", icon); Set(family, "sigil", sigil);
            var familyName = Label("FamilyName", header, "Family", 23, true); familyName.color = Ui.Theme.amber;
            Set(family, "familyName", familyName);
            Set(family, "condition", Label("FamilyCondition", family.transform, "Description", 18));
            var variants = Row("Variants", family.transform);
            Set(family, "variantRoot", variants.gameObject);
            var serialized = new SerializedObject(family);
            var items = serialized.FindProperty("variants"); items.arraySize = 4;
            for (int index = 0; index < 4; ++index)
            {
                var cell = Ui.Panel("FamilyVariant" + index, variants, Vector2.zero, Vector2.one, new Color32(15, 25, 20, 255));
                Stack(cell.transform, 5, 3); Size(cell, -1);
                var button = cell.gameObject.AddComponent<SpiritUiButtonVisual>(); button.targetGraphic = cell;
                ButtonColors(button, false);
                var border = cell.gameObject.AddComponent<Outline>();
                border.effectColor = new Color(Ui.Theme.muted.r, Ui.Theme.muted.g, Ui.Theme.muted.b, .3f);
                border.effectDistance = new Vector2(1, -1);
                var visibility = cell.gameObject.AddComponent<CanvasGroup>();
                var categoryImage = Ui.Full("VariantCategory", cell.transform).gameObject.AddComponent<Image>();
                categoryImage.preserveAspect = true; categoryImage.raycastTarget = false; Size(categoryImage, 22);
                var effect = Label("VariantEffect", cell.transform, "+0", 16); effect.alignment = TextAnchor.MiddleCenter;
                var state = Label("VariantState", cell.transform, "Locked", 12); state.alignment = TextAnchor.MiddleCenter;
                state.color = Ui.Theme.muted;
                var selection = Ui.Full("VariantSelection", cell.transform).gameObject.AddComponent<SpiritCharmSelectionGraphic>();
                selection.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                selection.color = Ui.Theme.amber; selection.raycastTarget = false; selection.enabled = false;
                var item = items.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("Button").objectReferenceValue = button;
                item.FindPropertyRelative("Category").objectReferenceValue = categoryImage;
                item.FindPropertyRelative("Effect").objectReferenceValue = effect;
                item.FindPropertyRelative("State").objectReferenceValue = state;
                item.FindPropertyRelative("Selection").objectReferenceValue = selection;
                item.FindPropertyRelative("Visibility").objectReferenceValue = visibility;
                button.CaptureContent();
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Popup(SpiritCharmPopup popup)
        {
            var backdrop = Back(popup); backdrop.color = new Color(0, 0, 0, .62f);
            var dismiss = popup.gameObject.AddComponent<Button>(); dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None; Set(popup, "dismissButton", dismiss);
            var panel = Ui.Panel("CharmPopupPanel", popup.transform, new Vector2(.08f, .5f), new Vector2(.92f, .5f), Ui.Theme.panelColor, 0, true);
            panel.rectTransform.pivot = new Vector2(.5f, .5f);
            Stack(panel.transform, 24, 12);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Set(panel.gameObject.AddComponent<SpiritCharmPopupLayout>(), "widthReference", popup.Root);
            Set(popup, "Panel", panel.rectTransform);
            var header = Row("Header", panel.transform);
            Artwork(header, 52, out var icon, out var sigil, out var category, "CharmPopup");
            Set(popup, "Icon", icon); Set(popup, "Sigil", sigil); Set(popup, "Category", category);
            Set(popup, "Title", Label("CharmPopupTitle", header, "Charm", 25, true));
            Set(popup, "Effect", Label("CharmPopupEffect", panel.transform, "Effect", 22));
            Set(popup, "Description", Label("CharmPopupDescription", panel.transform, "Description", 18));
            var status = Row("StatusRow", panel.transform);
            Set(popup, "statusRow", status);
            Set(popup, "Badge", AddStatusBadge(status));
            Set(popup, "Status", Label("CharmPopupStatus", status, "Ready", 16));
            Set(popup, "CloseButton", Button("CloseCharmPopupButton", panel.transform, "Close"));
            popup.gameObject.SetActive(false);
        }

        private static void Owned(SpiritOwnedCharmsPanel panel, SpiritOwnedCharmCard card)
        {
            Header(panel, "YOUR CHARMS", "CharmsTitle", "CloseCharmsButton", out var close); Set(panel, "CloseButton", close);
            Frame((Image)close.targetGraphic, Ui.Theme.darkFrame);
            Label("CharmsSubtitle", panel.transform, "Kept for this run. Tap a charm to inspect it.", 18).color = Ui.Theme.muted;
            var scroll = Ui.Scroll("OwnedCharmScroll", panel.transform, Vector2.zero, Vector2.one, out var content);
            Size(scroll, 0, -1, 1); Set(panel, "Scroll", scroll); Set(panel, "Content", content);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            grid.spacing = new Vector2(12, 12); grid.padding = new RectOffset(5, 5, 5, 5);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Set(panel, "grid", content.gameObject.AddComponent<SpiritCharmGridLayout>());
            Set(panel, "cardPrefab", card);
            var detail = Ui.Panel("CharmDetails", panel.transform, Vector2.zero, Vector2.one, Ui.Theme.panelColor, 0, true);
            Stack(detail.transform, 16, 8);
            Set(panel, "detailsLayout", detail.gameObject.AddComponent<SpiritCharmDetailsLayout>());
            var header = Row("Header", detail.transform);
            Artwork(header, 44, out var icon, out var sigil, out var category, "CharmDetail");
            Set(panel, "DetailIcon", icon); Set(panel, "DetailSigil", sigil); Set(panel, "DetailCategory", category);
            Set(panel, "DetailTitle", Label("CharmDetailTitle", header, "No charms yet", 23, true));
            Set(panel, "DetailBody", Label("CharmDetailBody", detail.transform, "Clear a stage to choose a charm.", 17));
            var status = Row("CharmDetailStatusRow", detail.transform);
            Set(panel, "DetailStatusRow", status);
            Set(panel, "DetailStatus", Label("CharmDetailStatus", status, "", 17));
            Set(panel, "DetailBadge", AddStatusBadge(status));
            status.gameObject.SetActive(false); panel.gameObject.SetActive(false);
        }

        private static void Rewards(SpiritRewardsPanel panel, SpiritRewardCard card)
        {
            Back(panel, true, true); Stack(panel.transform, 20, 10);
            var root = (RectTransform)panel.transform; root.anchorMin = new Vector2(.025f, .065f); root.anchorMax = new Vector2(.975f, .865f);
            var title = Label("RewardTitle", panel.transform, "Choose a charm", 25, true); title.color = Ui.Theme.amber;
            Set(panel, "Title", title);
            var step = Label("RewardStep", panel.transform, "", 14); step.gameObject.SetActive(false); Set(panel, "Step", step);
            Set(panel, "Instruction", Label("RewardInstruction", panel.transform, "", 17));
            Set(panel, "Scroll", List("RewardScroll", panel.transform, out var content)); Set(panel, "Content", content);
            Set(panel, "cardPrefab", card);
            var upcoming = Label("UpcomingStage", panel.transform, "", 14); upcoming.gameObject.SetActive(false); Set(panel, "UpcomingStage", upcoming);
            var buttons = Row("Actions", panel.transform, 52);
            var refresh = Button("RefreshRewardsButton", buttons, "Refresh choices"); Size(refresh, 52);
            var confirm = Button("UpgradeHandButton", buttons, "Select"); Size(confirm, 52);
            Set(panel, "RefreshButton", refresh);
            Set(panel, "ConfirmButton", confirm);
            panel.gameObject.SetActive(false);
        }

        private static void Collection(SpiritCollectionView panel, SpiritModifierFamilyRow family)
        {
            Header(panel, "COLLECTION", "CollectionTitle", "CloseCollectionButton", out var close).color = Ui.Theme.amber;
            PlainButton(close); Set(panel, "closeButton", close);
            var summary = Label("CollectionSummary", panel.transform, "", 17); summary.color = Ui.Theme.muted;
            Set(panel, "summary", summary);
            var filter = Button("CollectionFilterButton", panel.transform, "Owned"); Set(panel, "filterButton", filter);
            PlainButton(filter);
            Set(panel, "filterLabel", filter.GetComponentInChildren<Text>());
            Set(panel, "scroll", List("CollectionFamilies", panel.transform, out var content)); Set(panel, "content", content);
            Set(panel, "rowPrefab", family);
            var detail = Ui.Panel("CollectionDetail", panel.transform, Vector2.zero, Vector2.one, Ui.Theme.panelColor, 0, true);
            Stack(detail.transform, 12, 6); Size(detail, 240);
            var detailTitle = Label("CollectionDetailTitle", detail.transform, "", 22, true); detailTitle.color = Ui.Theme.amber;
            Set(panel, "detailTitle", detailTitle);
            Set(panel, "detailEffect", Label("CollectionDetailEffect", detail.transform, "", 18));
            List("CollectionDetailSources", detail.transform, out var sources);
            Set(panel, "detailSource", Label("CollectionDetailSource", sources, "", 17));
            panel.gameObject.SetActive(false);
        }

        private static void Encounter(SpiritEncounterCharmsScreen panel, SpiritModifierFamilyRow family)
        {
            Back(panel, true, true); Stack(panel.transform, 20, 12);
            Label("EncounterCharmsTitle", panel.transform, "ENCOUNTER CHARMS", 29, true).color = Ui.Theme.amber;
            Label("EncounterCharmsScope", panel.transform, "These charms can appear in charm choices during this encounter. They are not added to your permanent collection.", 19).color = Ui.Theme.muted;
            Set(panel, "Scroll", List("EncounterCharmFamilies", panel.transform, out var content)); Set(panel, "Content", content);
            Set(panel, "rowPrefab", family);
            Set(panel, "BeginButton", Button("BeginEncounterButton", panel.transform, "Begin encounter"));
            Set(panel, "BackButton", Button("BackToInvitationButton", panel.transform, "Back to invitation"));
            panel.gameObject.SetActive(false);
        }

        private static void HandRanks(SpiritHandRanksPanel panel)
        {
            Header(panel, "HAND RANKS", "HandRanksTitle", "CloseHandRanksButton", out var close).color = Ui.Theme.amber;
            Set(panel, "CloseButton", close);
            Label("HandRanksSubtitle", panel.transform, "Hand bonuses include upgrades. Dice and charm bonuses are extra.", 18).color = Ui.Theme.muted;
            var scroll = List("HandRanks", panel.transform, out var content);
            var serialized = new SerializedObject(panel);
            var rows = serialized.FindProperty("rows"); rows.arraySize = HandDefinition.CategoryCount;
            for (int index = 0; index < rows.arraySize; ++index)
            {
                var category = (HandCategory)index;
                var image = Ui.Panel("HandRankRow" + category, content, Vector2.zero, Vector2.one, Ui.Theme.panelColor);
                Stack(image.transform, 10, 5);
                var upper = Row("Identity", image.transform);
                var categoryLabel = Label("CategoryLabel", upper, DiagnosticText.Humanize(category.ToString()), 23, true);
                var current = Label("CurrentLabel", upper, "", 14);
                var lower = Row("Values", image.transform);
                var row = rows.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("<Category>k__BackingField").enumValueIndex = index;
                SetRowRef(row, "Background", image); SetRowRef(row, "CategoryLabel", categoryLabel); SetRowRef(row, "CurrentLabel", current);
                SetRowRef(row, "LevelLabel", Label("LevelLabel", lower, "", 18));
                SetRowRef(row, "BaseLabel", Label("BaseLabel", lower, "", 18));
                SetRowRef(row, "MultiplierLabel", Label("MultiplierLabel", lower, "", 18));
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Label("HandRanksFooter", panel.transform, "Highest matching rank is played.", 17).color = Ui.Theme.muted;
            panel.gameObject.SetActive(false);
        }
    }
}
