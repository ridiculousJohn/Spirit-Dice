using System;
using System.IO;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Editor
{
    /** Creates the initial editable front-end prefab assets without replacing authored content. */
    public static class SpiritFrontendPrefabAuthoring
    {
        private const string DirectoryPath = "Assets/Dice/Content/Presentation/UI";
        private static SpiritUiTheme Theme => SpiritPrefabUi.Theme;

        public static void CreateAssets()
        {
            Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh();
            var recovery = Create<SpiritCampaignRecoveryPicker>("SpiritCampaignRecoveryPicker", CreateRecovery);
            var confirmation = Create<SpiritSaveReplacementConfirmation>("SpiritReplaceSaveConfirmation", CreateConfirmation);
            var slots = Create<SpiritSaveSlotPicker>("SpiritSaveSlotPicker", () => CreateSlots(recovery, confirmation));
            Create<SpiritMainMenu>("SpiritMainMenu", () => CreateMainMenu(slots));
            var chapterCard = Create<SpiritJourneyCard>("SpiritChapterCard", () => CreateJourneyCard(true));
            var clueCard = Create<SpiritJourneyCard>("SpiritClueCard", () => CreateJourneyCard(false));
            var completion = Create<SpiritCompletionClue>("SpiritCompletionClue", CreateCompletion);
            var chapters = Create<SpiritChapterSelector>("SpiritChapterSelector", () => CreateChapters(chapterCard));
            Create<SpiritInvestigationBoard>("SpiritInvestigationBoard", () => CreateBoard(chapters, clueCard, completion));
            AssetDatabase.SaveAssets();
        }

        /** Repairs the portrait layout constraints in existing authored frontend prefabs. */
        public static void RepairLayouts()
        {
            Repair<SpiritSaveSlotPicker>("SpiritSaveSlotPicker", picker =>
            {
                foreach (var button in picker.SlotButtons)
                {
                    var layout = button.GetComponent<LayoutElement>();
                    layout.minHeight = 72;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(layout);
                }
            });
            Repair<SpiritCampaignRecoveryPicker>("SpiritCampaignRecoveryPicker", picker =>
            {
                foreach (var button in picker.CandidateButtons)
                {
                    var layout = button.GetComponent<LayoutElement>();
                    layout.minHeight = 96;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(layout);
                }
            });
            Repair<SpiritInvestigationBoard>("SpiritInvestigationBoard", board =>
            {
                var rect = (RectTransform)board.ChapterButton.transform;
                rect.anchorMin = new Vector2(.20f, rect.anchorMin.y);
                rect.anchorMax = new Vector2(.80f, rect.anchorMax.y);
                var label = board.ChapterButton.GetComponentInChildren<Text>(true);
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = 17;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            });
            AssetDatabase.SaveAssets();
        }

        private static void Repair<T>(string name, Action<T> repair) where T : Component
        {
            string path = DirectoryPath + "/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                repair(root.GetComponent<T>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T Create<T>(string name, Func<GameObject> factory) where T : Component
        {
            string path = DirectoryPath + "/" + name + ".prefab";
            if (File.Exists(path))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing == null || existing.GetComponent<T>() == null)
                    throw new InvalidOperationException("The authored prefab requires its root component: " + path);
                return existing.GetComponent<T>();
            }
            var root = factory();
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<T>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static T Root<T>(string name, Color color) where T : Component
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = color;
            root.AddComponent<SpiritUiContext>().Configure(Theme);
            var component = root.AddComponent<T>();
            if (component is SpiritMainMenu || component is SpiritSaveSlotPicker ||
                component is SpiritCampaignRecoveryPicker || component is SpiritSaveReplacementConfirmation ||
                component is SpiritChapterSelector || component is SpiritInvestigationBoard)
                Set(component, "Root", rect);
            return component;
        }

        private static T Nested<T>(T prefab, Transform parent) where T : Component
        {
            return ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent)).GetComponent<T>();
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? serialized.FindProperty("<" + field + ">k__BackingField");
            if (property == null) throw new InvalidOperationException("Missing serialized reference " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; ++i) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
            => SpiritPrefabUi.Panel(name, parent, min, max, Theme.panelColor, 0, true).rectTransform;

        private static Button Button(string name, Transform parent, string label, Vector2 min, Vector2 max,
            bool primary = false)
            => SpiritPrefabUi.Button(name, parent, label, min, max, null, primary, true);

        private static void Vertical(RectTransform content, float spacing, bool fit)
        {
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            if (fit) content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void Height(GameObject target, float height, float minimum = -1)
        {
            var layout = target.AddComponent<LayoutElement>();
            layout.minHeight = minimum < 0 ? height : minimum;
            layout.preferredHeight = height;
        }

        private static void Frame(Image image)
        {
            var frame = Theme.darkFrame;
            image.sprite = SpiritPrefabUi.FrameSprite(frame);
            if (image.sprite == null) return;
            image.type = Image.Type.Sliced;
            image.material = frame.surfaceMaterial;
            image.pixelsPerUnitMultiplier = Mathf.Max(.1f, frame.pixelsPerUnitMultiplier);
            image.color = Color.white;
        }

        private static GameObject CreateMainMenu(SpiritSaveSlotPicker slots)
        {
            var menu = Root<SpiritMainMenu>("MainMenu", Theme.panelColor);
            var root = menu.Root;
            Set(menu, "Title", SpiritPrefabUi.Heading("MainMenuTitle", root, "Spirit Dice", 48,
                new Vector2(.06f, .76f), new Vector2(.94f, .89f), TextAnchor.MiddleCenter, Theme.amber));
            Set(menu, "ContinueButton", Button("ContinueGameButton", root, "Continue",
                new Vector2(.16f, .65f), new Vector2(.84f, .73f)));
            Set(menu, "SlotSelectorButton", Button("MainMenuSaveSlotButton", root, "Load Game",
                new Vector2(.16f, .55f), new Vector2(.84f, .63f)));
            Set(menu, "ChapterButton", Button("OpenSavedChapterButton", root, "Chapter",
                new Vector2(.16f, .45f), new Vector2(.84f, .53f)));
            Set(menu, "NewGameButton", Button("NewGameButton", root, "New Game",
                new Vector2(.16f, .35f), new Vector2(.84f, .43f)));
            Set(menu, "OptionsButton", Button("MainMenuOptionsButton", root, "Options",
                new Vector2(.16f, .25f), new Vector2(.84f, .33f)));
            Set(menu, "Status", SpiritPrefabUi.Label("MainMenuStatus", root, "", 17,
                new Vector2(.08f, .12f), new Vector2(.92f, .22f), TextAnchor.MiddleCenter, Theme.muted));
            Set(menu, "SlotPicker", Nested(slots, root));
            return menu.gameObject;
        }

        private static GameObject CreateSlots(SpiritCampaignRecoveryPicker recovery,
            SpiritSaveReplacementConfirmation confirmationPrefab)
        {
            var picker = Root<SpiritSaveSlotPicker>("SaveSlotPicker", new Color(0, 0, 0, .78f));
            var panel = Panel("SaveSlotPickerPanel", picker.Root, new Vector2(.065f, .18f), new Vector2(.935f, .82f));
            Set(picker, "Title", SpiritPrefabUi.Heading("SaveSlotPickerTitle", panel, "Save Slots", 28,
                new Vector2(.04f, .86f), new Vector2(.96f, .98f), TextAnchor.MiddleCenter, Theme.amber));
            Set(picker, "Subtitle", SpiritPrefabUi.Label("SaveSlotPickerSubtitle", panel, "Choose a save slot.", 18,
                new Vector2(.04f, .78f), new Vector2(.96f, .87f), TextAnchor.MiddleCenter, Theme.muted));
            var list = SpiritPrefabUi.Rect("SaveSlotList", panel, new Vector2(.055f, .28f), new Vector2(.945f, .78f));
            Vertical(list, 8, false);
            var buttons = new Button[3];
            var summaries = new Text[3];
            for (int i = 0; i < buttons.Length; ++i)
            {
                var button = Button("SaveSlotButton" + (i + 1), list, "Slot " + (i + 1), Vector2.zero, Vector2.one);
                buttons[i] = button;
                Height(button.gameObject, 92, 72);
                var title = button.GetComponentInChildren<Text>();
                title.rectTransform.anchorMin = new Vector2(.09f, .46f);
                title.rectTransform.anchorMax = new Vector2(.91f, .89f);
                summaries[i] = SpiritPrefabUi.Label("SaveSlotSummary" + (i + 1), button.transform, "", 16,
                    new Vector2(.09f, .08f), new Vector2(.91f, .48f), TextAnchor.MiddleCenter, Theme.muted);
                summaries[i].rectTransform.offsetMin = summaries[i].rectTransform.offsetMax = Vector2.zero;
                summaries[i].resizeTextForBestFit = true;
                summaries[i].resizeTextMinSize = 12;
                summaries[i].resizeTextMaxSize = 16;
                button.GetComponent<SpiritUiButtonVisual>().CaptureContent();
            }
            SetArray(picker, "slotButtons", buttons);
            SetArray(picker, "slotSummaries", summaries);
            Set(picker, "CloseButton", Button("CloseSaveSlotPickerButton", panel, "Cancel",
                new Vector2(.22f, .025f), new Vector2(.78f, .15f)));
            Set(picker, "RecoverSavesButton", Button("RecoverSavesButton", panel, "Recover saves",
                new Vector2(.12f, .155f), new Vector2(.88f, .28f)));
            var confirmation = Nested(confirmationPrefab, picker.Root);
            Set(picker, "Confirmation", confirmation.Root);
            Set(picker, "ConfirmationTitle", confirmation.Title);
            Set(picker, "ConfirmationText", confirmation.Description);
            Set(picker, "ConfirmReplaceSaveButton", confirmation.ConfirmButton);
            Set(picker, "CancelReplaceSaveButton", confirmation.CancelButton);
            Set(picker, "RecoveryPicker", Nested(recovery, picker.Root));
            picker.gameObject.SetActive(false);
            return picker.gameObject;
        }

        private static GameObject CreateConfirmation()
        {
            var confirmation = Root<SpiritSaveReplacementConfirmation>("ReplaceSaveConfirmation", new Color(0, 0, 0, .78f));
            var panel = Panel("ReplaceSaveConfirmationPanel", confirmation.Root,
                new Vector2(.065f, .3f), new Vector2(.935f, .68f));
            Set(confirmation, "Title", SpiritPrefabUi.Heading("ReplaceSaveConfirmationTitle", panel, "", 28,
                new Vector2(.04f, .75f), new Vector2(.96f, .96f), TextAnchor.MiddleCenter, Theme.amber));
            Set(confirmation, "Description", SpiritPrefabUi.Label("ReplaceSaveConfirmationText", panel,
                "This will reset its chapter progress, collected charms, and current encounter.", 20,
                new Vector2(.055f, .31f), new Vector2(.945f, .76f), TextAnchor.MiddleCenter));
            Set(confirmation, "ConfirmButton", Button("ConfirmReplaceSaveButton", panel, "New Game",
                new Vector2(.04f, .065f), new Vector2(.5f, .28f)));
            Set(confirmation, "CancelButton", Button("CancelReplaceSaveButton", panel, "Cancel",
                new Vector2(.5f, .065f), new Vector2(.96f, .28f)));
            confirmation.gameObject.SetActive(false);
            return confirmation.gameObject;
        }

        private static GameObject CreateRecovery()
        {
            var picker = Root<SpiritCampaignRecoveryPicker>("CampaignRecoveryPicker", new Color(0, 0, 0, .78f));
            var panel = Panel("CampaignRecoveryPickerPanel", picker.Root,
                new Vector2(.065f, .18f), new Vector2(.935f, .90f));
            SpiritPrefabUi.Heading("CampaignRecoveryPickerTitle", panel, "Recover saves", 28,
                new Vector2(.04f, .88f), new Vector2(.96f, .98f), TextAnchor.MiddleCenter, Theme.amber);
            SpiritPrefabUi.Label("CampaignRecoveryPickerSubtitle", panel, "Choose a campaign to recover.", 18,
                new Vector2(.04f, .78f), new Vector2(.96f, .88f), TextAnchor.MiddleCenter, Theme.muted);
            var list = SpiritPrefabUi.Rect("RecoveryCandidates", panel, new Vector2(.055f, .215f), new Vector2(.945f, .78f));
            Vertical(list, 8, false);
            var buttons = new Button[3];
            var summaries = new Text[3];
            var details = new Text[3];
            var identifiers = new Text[3];
            for (int i = 0; i < buttons.Length; ++i)
            {
                var button = Button("RecoveryCampaignButton" + (i + 1), list, "", Vector2.zero, Vector2.one);
                buttons[i] = button;
                Height(button.gameObject, 120, 96);
                var summary = button.GetComponentInChildren<Text>();
                summary.rectTransform.anchorMin = new Vector2(.07f, .54f);
                summary.rectTransform.anchorMax = new Vector2(.93f, .92f);
                summary.horizontalOverflow = HorizontalWrapMode.Wrap;
                summary.resizeTextMinSize = 14;
                summary.resizeTextMaxSize = 18;
                summaries[i] = summary;
                details[i] = SpiritPrefabUi.Label("RecoveryCampaignDetails" + (i + 1), button.transform, "", 15,
                    new Vector2(.07f, .29f), new Vector2(.93f, .54f), TextAnchor.MiddleCenter, Theme.muted);
                identifiers[i] = SpiritPrefabUi.Label("RecoveryCampaignIdentifier" + (i + 1), button.transform, "", 14,
                    new Vector2(.07f, .06f), new Vector2(.93f, .29f), TextAnchor.MiddleCenter, Theme.muted);
                details[i].rectTransform.offsetMin = details[i].rectTransform.offsetMax = Vector2.zero;
                identifiers[i].rectTransform.offsetMin = identifiers[i].rectTransform.offsetMax = Vector2.zero;
                button.GetComponent<SpiritUiButtonVisual>().CaptureContent();
            }
            SetArray(picker, "candidateButtons", buttons);
            SetArray(picker, "summaries", summaries);
            SetArray(picker, "details", details);
            SetArray(picker, "identifiers", identifiers);
            Set(picker, "PreviousPageButton", Button("PreviousRecoveryPageButton", panel, "Prev",
                new Vector2(.045f, .115f), new Vector2(.34f, .205f)));
            Set(picker, "PageLabel", SpiritPrefabUi.Label("RecoveryPageLabel", panel, "", 16,
                new Vector2(.34f, .115f), new Vector2(.66f, .205f), TextAnchor.MiddleCenter, Theme.muted));
            Set(picker, "NextPageButton", Button("NextRecoveryPageButton", panel, "Next",
                new Vector2(.66f, .115f), new Vector2(.955f, .205f)));
            Set(picker, "CloseButton", Button("CloseCampaignRecoveryPickerButton", panel, "Cancel",
                new Vector2(.22f, .015f), new Vector2(.78f, .115f)));
            picker.gameObject.SetActive(false);
            return picker.gameObject;
        }

        private static GameObject CreateJourneyCard(bool chapter)
        {
            var card = Root<SpiritJourneyCard>(chapter ? "ChapterCard" : "ClueCard", Theme.panelColor);
            var root = (RectTransform)card.transform;
            Height(card.gameObject, chapter ? 280 : 430);
            Frame(card.GetComponent<Image>());
            var button = card.gameObject.AddComponent<SpiritUiButtonVisual>();
            button.targetGraphic = card.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = Theme.highlightedTint;
            colors.pressedColor = Theme.pressedTint;
            colors.disabledColor = Theme.disabledTint;
            colors.fadeDuration = .12f;
            button.colors = colors;
            Set(card, "button", button);
            var artworkFrame = SpiritPrefabUi.Rect(chapter ? "ChapterArtworkFrame" : "ClueArtworkFrame", root,
                new Vector2(.055f, chapter ? .43f : .46f), new Vector2(.945f, .94f));
            artworkFrame.gameObject.AddComponent<RectMask2D>();
            var artwork = SpiritPrefabUi.Full(chapter ? "ChapterArtwork" : "ClueArtwork", artworkFrame).gameObject.AddComponent<RawImage>();
            artwork.raycastTarget = false;
            artwork.color = Color.clear;
            var aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = 1;
            Set(card, "artwork", artwork);
            Set(card, "artworkAspect", aspect);
            var number = SpiritPrefabUi.Label("ChapterNumber", root, "", 14,
                new Vector2(.055f, .33f), new Vector2(.945f, .43f), TextAnchor.MiddleCenter, Theme.muted);
            number.gameObject.SetActive(chapter);
            Set(card, "number", number);
            var title = SpiritPrefabUi.Heading(chapter ? "ChapterTitle" : "ClueTitle", root, "", chapter ? 25 : 26,
                new Vector2(.055f, chapter ? .13f : .33f), new Vector2(.945f, chapter ? .33f : .45f),
                TextAnchor.MiddleCenter, Theme.amber);
            title.resizeTextMinSize = chapter ? 20 : 21;
            Set(card, "title", title);
            var description = SpiritPrefabUi.Label("ClueLead", root, "", 19,
                new Vector2(.055f, .09f), new Vector2(.945f, .32f), TextAnchor.MiddleCenter);
            description.resizeTextForBestFit = true;
            description.resizeTextMinSize = 17;
            description.resizeTextMaxSize = 19;
            description.gameObject.SetActive(!chapter);
            Set(card, "description", description);
            Set(card, "state", SpiritPrefabUi.Label(chapter ? "ChapterState" : "ClueState", root, "", chapter ? 16 : 14,
                new Vector2(.055f, chapter ? .025f : .027f), new Vector2(.945f, chapter ? .13f : .09f),
                TextAnchor.MiddleCenter, Theme.muted));
            var marker = SpiritPrefabUi.Label("ClueNewLead", root, "NEW LEAD", 14,
                new Vector2(.055f, .94f), new Vector2(.945f, 1), TextAnchor.MiddleCenter, Theme.amber);
            marker.rectTransform.offsetMin = new Vector2(12, 0);
            marker.rectTransform.offsetMax = new Vector2(-12, 0);
            marker.gameObject.SetActive(false);
            Set(card, "newLead", marker.gameObject);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = Theme.amber;
            outline.effectDistance = chapter ? new Vector2(2, -2) : new Vector2(3, -3);
            outline.enabled = false;
            Set(card, "highlight", outline);
            var serialized = new SerializedObject(card);
            serialized.FindProperty("stateColor").colorValue = Theme.muted;
            serialized.FindProperty("highlightedStateColor").colorValue = chapter ? Theme.amber : Theme.muted;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            button.CaptureContent();
            return card.gameObject;
        }

        private static GameObject CreateCompletion()
        {
            var card = Root<SpiritCompletionClue>("CompletionClue", Theme.panelColor);
            Height(card.gameObject, 180);
            Frame(card.GetComponent<Image>());
            Set(card, "title", SpiritPrefabUi.Heading("CompletionClueTitle", card.transform, "", 24,
                new Vector2(.035f, .67f), new Vector2(.965f, .95f), TextAnchor.MiddleCenter, Theme.amber));
            var text = SpiritPrefabUi.Label("CompletionClueText", card.transform, "", 19,
                new Vector2(.045f, .06f), new Vector2(.955f, .66f), TextAnchor.MiddleCenter);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 17;
            text.resizeTextMaxSize = 19;
            Set(card, "description", text);
            return card.gameObject;
        }

        private static GameObject CreateChapters(SpiritJourneyCard chapterCard)
        {
            var selector = Root<SpiritChapterSelector>("ChapterSelectorOverlay", new Color(0, 0, 0, .78f));
            var panel = Panel("ChapterSelectorPanel", selector.Root,
                new Vector2(.025f, .065f), new Vector2(.975f, .955f));
            SpiritPrefabUi.Heading("ChapterSelectorTitle", panel, "CHAPTERS", 28,
                new Vector2(.035f, .875f), new Vector2(.7f, .975f), TextAnchor.MiddleLeft, Theme.amber);
            Set(selector, "CloseButton", Button("CloseChapterSelectorButton", panel, "Close",
                new Vector2(.72f, .89f), new Vector2(.975f, .975f)));
            SpiritPrefabUi.Label("ChapterSelectorSubtitle", panel, "Choose a chapter to investigate.", 18,
                new Vector2(.035f, .81f), new Vector2(.965f, .885f), TextAnchor.MiddleLeft, Theme.muted);
            var scroll = SpiritPrefabUi.Scroll("ChapterSelectorScroll", panel,
                new Vector2(.015f, .105f), new Vector2(.985f, .81f), out var content);
            Vertical(content, 14, true);
            Set(selector, "Scroll", scroll);
            Set(selector, "Content", content);
            Set(selector, "chapterCardPrefab", chapterCard);
            SpiritPrefabUi.Label("ChapterSelectorFooter", panel, "Completed chapters remain available to revisit.", 16,
                new Vector2(.04f, .015f), new Vector2(.96f, .105f), TextAnchor.MiddleCenter, Theme.muted);
            selector.gameObject.SetActive(false);
            return selector.gameObject;
        }

        private static GameObject CreateBoard(SpiritChapterSelector chapters, SpiritJourneyCard clueCard,
            SpiritCompletionClue completion)
        {
            var board = Root<SpiritInvestigationBoard>("InvestigationBoard", new Color32(17, 28, 23, 255));
            var root = board.Root;
            Set(board, "Title", SpiritPrefabUi.Heading("InvestigationTitle", root, "INVESTIGATION", 31,
                new Vector2(.04f, .883f), new Vector2(.96f, .945f), TextAnchor.MiddleCenter, Theme.amber));
            var description = SpiritPrefabUi.Label("InvestigationSubtitle", root, "", 18,
                new Vector2(.06f, .813f), new Vector2(.94f, .884f), TextAnchor.MiddleCenter, Theme.muted);
            description.resizeTextForBestFit = true;
            description.resizeTextMinSize = 14;
            description.resizeTextMaxSize = 18;
            Set(board, "Description", description);
            var scroll = SpiritPrefabUi.Scroll("InvestigationClues", root,
                new Vector2(.015f, .24f), new Vector2(.985f, .808f), out var content);
            Vertical(content, 16, true);
            Set(board, "Scroll", scroll);
            Set(board, "Content", content);
            Set(board, "Status", SpiritPrefabUi.Label("InvestigationStatus", root, "", 17,
                new Vector2(.04f, .172f), new Vector2(.96f, .24f), TextAnchor.MiddleCenter, Theme.muted));
            Set(board, "ResumeButton", Button("ResumeEncounterButton", root, "Continue at the table",
                new Vector2(.035f, .09f), new Vector2(.965f, .165f), true));
            Set(board, "LoadButton", Button("LoadJourneyButton", root, "Load saved encounter",
                new Vector2(.035f, .012f), new Vector2(.58f, .087f)));
            Set(board, "CollectionButton", Button("InvestigationCollectionButton", root, "Collection",
                new Vector2(.58f, .012f), new Vector2(.965f, .087f)));
            var chapterButton = Button("InvestigationChapterButton", root, "Chapters",
                new Vector2(.20f, .945f), new Vector2(.80f, .997f));
            Set(board, "ChapterButton", chapterButton);
            var chapterLabel = chapterButton.GetComponentInChildren<Text>();
            chapterLabel.resizeTextMaxSize = 17;
            chapterLabel.resizeTextMinSize = 12;
            chapterLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            Set(board, "chapterLabel", chapterLabel);
            Set(board, "ChapterSelector", Nested(chapters, root));
            Set(board, "clueCardPrefab", clueCard);
            Set(board, "completionCluePrefab", completion);
            return board.gameObject;
        }
    }
}
