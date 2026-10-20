using System;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Editor
{
    /** Creates the diagnostic canvas and its data row prefab without replacing authored content. */
    public static class SpiritDiagnosticPrefabAuthoring
    {
        public const string PrefabPath = "Assets/Dice/Content/Presentation/UI/DiagnosticEncounter.prefab";
        private const string CardPath = "Assets/Dice/Content/Presentation/UI/DiagnosticEncounterCard.prefab";
        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static readonly Color TextColor = new Color(.91f, .94f, .97f);
        private static readonly Color PanelColor = new Color(.092f, .112f, .139f);
        private static readonly Color Cyan = new Color(.37f, .87f, .93f);

        public static void CreateAssets()
        {
            EnsureFolder("Assets/Dice/Content/Presentation/UI");
            var card = AssetDatabase.LoadAssetAtPath<DiagnosticEncounterCard>(CardPath);
            if (card == null)
            {
                RequireVacant(CardPath);
                var rect = Panel("Diagnostic Card", null);
                card = rect.gameObject.AddComponent<DiagnosticEncounterCard>();
                card.Title = Label("Title", rect, "Modifier or upgrade", 18, Cyan);
                card.Body = Label("Details", rect, "Condition, effects, and activation details", 15, TextColor);
                card.Choose = Button("Choose", rect, "Choose");
                card = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, CardPath).GetComponent<DiagnosticEncounterCard>();
                Object.DestroyImmediate(rect.gameObject);
            }
            if (AssetDatabase.LoadAssetAtPath<DiagnosticEncounterView>(PrefabPath) != null) return;
            RequireVacant(PrefabPath);
            var root = Rect("Diagnostic Encounter", null);
            var view = root.gameObject.AddComponent<DiagnosticEncounterView>();
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            root.gameObject.AddComponent<Image>().color = new Color(.055f, .068f, .086f);
            var header = Rect("Header", root); header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one;
            header.pivot = new Vector2(.5f, 1); header.sizeDelta = new Vector2(-32, 188); header.anchoredPosition = new Vector2(0, -12);
            Stack(header, false, 5);
            Label("Title", header, "DICE / MECHANICAL DIAGNOSTIC", 25, TextColor);
            Label("Subtitle", header, "Deterministic encounter, score resolution, and modifier inspection", 13, TextColor);
            var toolbar = Rect("Toolbar", header); Stack(toolbar, true, 8); Height(toolbar, 40);
            Label("Seed Label", toolbar, "New run seed", 16, TextColor);
            var inputRect = Rect("Seed", toolbar); inputRect.gameObject.AddComponent<Image>().color = new Color(.145f, .18f, .22f);
            var input = inputRect.gameObject.AddComponent<InputField>();
            var inputText = Label("Value", inputRect, "60", 17, TextColor, false);
            inputText.rectTransform.offsetMin = new Vector2(10, 3); inputText.rectTransform.offsetMax = new Vector2(-10, -3);
            input.textComponent = inputText; input.characterLimit = 32; input.text = "60";
            Height(inputRect, 40); inputRect.GetComponent<LayoutElement>().preferredWidth = 200;
            Set(view, "seedInput", input);
            Set(view, "startButton", Button("Start New", toolbar, "Start new"));
            Set(view, "saveButton", Button("Save", toolbar, "Save"));
            Set(view, "loadButton", Button("Load", toolbar, "Load"));
            Set(view, "abandonButton", Button("Abandon", toolbar, "Abandon"));
            Set(view, "savePathText", Label("Save Path", header, "Save file: unavailable", 13, TextColor));
            Set(view, "statusText", Label("Status", header, "Waiting for encounter controls.", 14, new Color(1, .76f, .37f)));

            var scrollRect = Rect("Inspection Scroll", root); scrollRect.offsetMin = new Vector2(16, 16); scrollRect.offsetMax = new Vector2(-16, -210);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", scrollRect); viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var body = Rect("Inspection Columns", viewport); body.anchorMin = new Vector2(0, 1); body.anchorMax = Vector2.one; body.pivot = new Vector2(.5f, 1);
            Stack(body, true, 18); var fitter = body.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = body; scroll.scrollSensitivity = 40;
            var left = Rect("Encounter", body); Stack(left, false, 14); Column(left);
            var right = Rect("Inspection", body); Stack(right, false, 14); Column(right);
            Set(view, "runText", Label("Run", Panel("Run", left), "Start a run or load a saved encounter.", 17, TextColor));
            var dicePanel = Panel("Current Hand", left);
            Label("Heading", dicePanel, "CURRENT HAND", 18, TextColor);
            Set(view, "selectionText", Label("Selection", dicePanel, "Select dice for reroll.", 13, TextColor));
            var diceRow = Rect("Dice", dicePanel); Stack(diceRow, true, 6); Height(diceRow, 116);
            var buttons = new Object[5]; var texts = new Object[5]; var selections = new Object[5];
            for (int i = 0; i < buttons.Length; ++i)
            {
                var button = Button("Die " + (i + 1), diceRow, "DIE " + (i + 1) + "\n1\nKEEP");
                Height((RectTransform)button.transform, 116);
                var selection = Rect("Selected", button.transform).gameObject.AddComponent<Image>();
                selection.color = new Color(.09f, .365f, .41f); selection.raycastTarget = false; selection.enabled = false;
                selection.transform.SetAsFirstSibling();
                buttons[i] = button; texts[i] = button.GetComponentInChildren<Text>(); selections[i] = selection;
            }
            SetArray(view, "dieButtons", buttons); SetArray(view, "dieTexts", texts); SetArray(view, "dieSelection", selections);
            var selectRow = Rect("Selection Controls", dicePanel); Stack(selectRow, true, 6); Height(selectRow, 40);
            Set(view, "selectAllButton", Button("Select All", selectRow, "Select all"));
            Set(view, "clearSelectionButton", Button("Clear Selection", selectRow, "Clear selection"));
            var actionRow = Rect("Actions", dicePanel); Stack(actionRow, true, 6); Height(actionRow, 40);
            Set(view, "rerollButton", Button("Reroll", actionRow, "Reroll selected"));
            Set(view, "playButton", Button("Play", actionRow, "Play hand"));
            var preview = Panel("Score Preview", left); Label("Heading", preview, "IF PLAYED NOW / SCORE PREVIEW", 18, TextColor);
            Set(view, "previewText", Label("Preview", preview, "Available while the encounter is playing.", 16, Cyan));
            var rewards = Panel("Rewards", left);
            Set(view, "rewardText", Label("Heading", rewards, "REWARDS", 18, TextColor));
            var rewardsContent = Rect("Reward Cards", rewards); Stack(rewardsContent, false, 8);
            Set(view, "rewardsContent", rewardsContent);
            Set(view, "refreshButton", Button("Refresh Rewards", rewards, "Refresh modifiers"));
            Set(view, "upgradesText", Label("Upgrades", Panel("Hand Upgrades", left), "HAND UPGRADES / CATEGORY VALUES", 16, TextColor));
            var inventory = Panel("Owned Modifiers", right);
            Set(view, "inventoryText", Label("Heading", inventory, "OWNED MODIFIERS", 18, TextColor));
            Label("Explanation", inventory, "If played now: Play Hand modifiers. Roll modifiers are evaluated on their roll; their current-hand contributions are shown separately.", 13, TextColor);
            var inventoryContent = Rect("Inventory Cards", inventory); Stack(inventoryContent, false, 8);
            Set(view, "inventoryContent", inventoryContent);
            Set(view, "eventText", Label("Recent Events", Panel("Recent Events", right), "RECENT EVENTS\nNo events recorded.", 15, TextColor));
            Set(view, "cardPrefab", card);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Object.DestroyImmediate(root.gameObject);
            AssetDatabase.SaveAssets();
        }

        private static RectTransform Panel(string name, Transform parent)
        {
            var rect = Rect(name, parent); rect.gameObject.AddComponent<Image>().color = PanelColor;
            var layout = Stack(rect, false, 6); layout.padding = new RectOffset(16, 16, 14, 14); return rect;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; return rect;
        }
        private static HorizontalOrVerticalLayoutGroup Stack(RectTransform rect, bool horizontal, float spacing)
        {
            HorizontalOrVerticalLayoutGroup layout = horizontal ? rect.gameObject.AddComponent<HorizontalLayoutGroup>() : rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing; layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; return layout;
        }
        private static Text Label(string name, Transform parent, string value, int size, Color color, bool layout = true)
        {
            var rect = Rect(name, parent); var text = rect.gameObject.AddComponent<Text>(); text.font = Font;
            text.fontSize = size; text.text = value; text.color = color; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            if (layout) rect.gameObject.AddComponent<LayoutElement>().minHeight = size + 6;
            return text;
        }
        private static Button Button(string name, Transform parent, string value)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.145f, .18f, .22f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Label("Label", rect, value, 16, TextColor, false); label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.offsetMin = new Vector2(8, 4); label.rectTransform.offsetMax = new Vector2(-8, -4);
            Height(rect, 40); rect.GetComponent<LayoutElement>().flexibleWidth = 1; return button;
        }
        private static void Height(RectTransform rect, float height)
        {
            var element = rect.GetComponent<LayoutElement>(); if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
        }
        private static void Column(RectTransform rect)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = 0; element.flexibleWidth = 1;
        }
        private static void Set(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(name).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray(Object target, string name, Object[] values)
        {
            var serialized = new SerializedObject(target); var array = serialized.FindProperty(name); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; ++i) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void RequireVacant(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
                throw new InvalidOperationException("An authored asset already exists at " + path + ". Assign its required component explicitly.");
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
