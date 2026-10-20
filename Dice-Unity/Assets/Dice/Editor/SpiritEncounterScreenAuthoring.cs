using System;
using System.Collections.Generic;
using System.IO;
using Dice.Engine;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Editor
{
    /** Authors the encounter canvas and nested pause surface without replacing existing prefabs. */
    public static class SpiritEncounterScreenAuthoring
    {
        private const string Folder = "Assets/Dice/Content/Presentation/UI/";

        public static void CreateAssets()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            SpiritHudPrefabAuthoring.CreateAssets();
            SpiritCharmPrefabAuthoring.CreateAssets();
            SpiritJourneyPrefabAuthoring.CreateAssets();
            var pause = CreatePause();
            string path = Folder + "SpiritEncounterScreen.prefab";
            if (File.Exists(path)) return;
            var root = SpiritPrefabUi.Full("SpiritEncounterScreen", null);
            root.gameObject.AddComponent<SpiritUiContext>().Configure(SpiritPrefabUi.Theme);
            var view = root.gameObject.AddComponent<SpiritEncounterView>();
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
            root.gameObject.AddComponent<GraphicRaycaster>();
            SpiritPrefabUi.Panel("Letterbox", root, Vector2.zero, Vector2.one, new Color32(12, 18, 16, 255));
            var safeArea = SpiritPrefabUi.Full("SafeArea", root);
            var portrait = SpiritPrefabUi.Rect("Portrait", safeArea, new Vector2(.5f, 0), new Vector2(.5f, 1));
            portrait.sizeDelta = new Vector2(540, 0);
            SpiritPrefabUi.Panel("ForestBackground", portrait, Vector2.zero, Vector2.one, new Color32(20, 29, 25, 255));
            var roomImage = SpiritPrefabUi.Rect("TeaRoom", portrait, new Vector2(0, .625f), Vector2.one)
                .gameObject.AddComponent<RawImage>();
            roomImage.color = Color.clear;
            roomImage.raycastTarget = false;
            var tableImage = SpiritPrefabUi.Rect("ForegroundTable", portrait, Vector2.zero, new Vector2(1, .625f))
                .gameObject.AddComponent<RawImage>();
            tableImage.color = Color.clear;
            tableImage.raycastTarget = false;
            var inputGroup = portrait.gameObject.AddComponent<CanvasGroup>();
            var hud = Nested<SpiritGameplayHud>("SpiritGameplayHud", portrait);
            var rewards = Nested<SpiritRewardsPanel>("SpiritRewardsPanel", portrait);
            var ending = Nested<SpiritEncounterEndingView>("SpiritEncounterEndingView", portrait);
            var owned = Nested<SpiritOwnedCharmsPanel>("SpiritOwnedCharmsPanel", portrait);
            var handRanks = Nested<SpiritHandRanksPanel>("SpiritHandRanksPanel", portrait);
            var pauseInstance = ((GameObject)PrefabUtility.InstantiatePrefab(pause.gameObject, portrait)).GetComponent<SpiritPauseMenu>();
            var journey = Nested<EncounterJourneyView>("EncounterJourneyView", portrait);
            var collection = Nested<SpiritCollectionView>("SpiritCollectionView", portrait);
            var popup = Nested<SpiritCharmPopup>("SpiritCharmPopup", root);
            Set(popup.Panel.GetComponent<SpiritCharmPopupLayout>(), "widthReference", portrait);
            var history = Nested<SpiritModifierHistoryTray>("ModifierHistoryTray", hud.DiceTrayImage.transform);
            var actionCue = root.gameObject.AddComponent<SpiritActionModifierCue>();
            SpiritEncounterObjectAuthoring.AddActionCues(actionCue, hud.RerollButton, hud.PlayButton);
            var audio = root.gameObject.AddComponent<SpiritEncounterAudio>();
            audio.enabled = false;
            SpiritEncounterObjectAuthoring.AddAudioSources(audio);
            AuthorDiceTargets(view, hud);
            var openingGroups = new List<CanvasGroup>();
            foreach (var element in new Transform[] { hud.PlayingPanel, hud.TargetPanel, hud.HandsPanel, hud.RerollsPanel,
                hud.StageLabel.transform, hud.StatusLabel.transform, hud.ResourcesLabel.transform, hud.CharmsButton.transform,
                hud.HandRanksButton.transform, hud.MenuButton.transform, hud.InvestigationButton.transform })
                openingGroups.Add(element.gameObject.AddComponent<CanvasGroup>());
            Set(view, "uiTheme", SpiritPrefabUi.Theme);
            Set(view, "canvas", canvas);
            Set(view, "safeArea", safeArea);
            Set(view, "portrait", portrait);
            Set(view, "inputGroup", inputGroup);
            Set(view, "roomImage", roomImage);
            Set(view, "tableImage", tableImage);
            Set(view, "GameplayHud", hud);
            Set(view, "RewardsPanel", rewards);
            Set(view, "OwnedCharmsPanel", owned);
            Set(view, "handRanksPanel", handRanks);
            Set(view, "endingView", ending);
            Set(view, "endingPanel", ending.Root);
            Set(view, "pauseMenu", pauseInstance);
            Set(view, "menuPanel", pauseInstance.Root);
            Set(view, "menuTitle", pauseInstance.Title);
            Set(view, "soundButton", pauseInstance.SoundButton);
            Set(view, "profileSummary", pauseInstance.ProfileSummary);
            Set(view, "mainMenuButton", pauseInstance.MainMenuButton);
            Set(view, "JourneyView", journey);
            Set(view, "collectionView", collection);
            Set(view, "CharmPopup", popup);
            Set(view, "ModifierHistory", history);
            Set(view, "actionModifierCue", actionCue);
            Set(view, "encounterAudio", audio);
            Set(view, "diceTrayFrame", hud.DiceTrayFrame);
            Set(view, "trayImage", hud.DiceTrayImage);
            SetArray(view, "openingGroups", openingGroups.ToArray());
            rewards.Root.gameObject.SetActive(false);
            owned.Root.gameObject.SetActive(false);
            handRanks.Root.gameObject.SetActive(false);
            pauseInstance.Root.gameObject.SetActive(false);
            journey.gameObject.SetActive(false);
            collection.Root.gameObject.SetActive(false);
            popup.Root.gameObject.SetActive(false);
            try { PrefabUtility.SaveAsPrefabAsset(root.gameObject, path); }
            finally { Object.DestroyImmediate(root.gameObject); }
            AssetDatabase.SaveAssets();
        }

        private static T Nested<T>(string name, Transform parent) where T : Component
        {
            string path = Folder + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<T>(path);
            if (prefab == null) throw new InvalidOperationException("Missing authored UI prefab: " + path);
            return ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent)).GetComponent<T>();
        }

        private static void AuthorDiceTargets(SpiritEncounterView view, SpiritGameplayHud hud)
        {
            var tray = hud.DiceTrayImage.rectTransform;
            var targets = SpiritPrefabUi.Full("DiceTouchTargets", tray);
            var buttons = new Button[DiceValues.Count];
            var labels = new GameObject[DiceValues.Count];
            for (int i = 0; i < buttons.Length; ++i)
            {
                float left = (float)i / buttons.Length;
                float right = (float)(i + 1) / buttons.Length;
                var target = SpiritPrefabUi.Rect("DieButton" + (i + 1), targets, new Vector2(left, 0), new Vector2(right, 1));
                var image = target.gameObject.AddComponent<Image>();
                image.color = Color.clear;
                var button = target.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                buttons[i] = button;
                var plate = SpiritPrefabUi.Panel("DieRerollPlate" + (i + 1), tray,
                    new Vector2(left, .02f), new Vector2(right, .09f), new Color32(38, 26, 12, 245), 0, true);
                plate.raycastTarget = false;
                var label = SpiritPrefabUi.Label("DieRerollLabel" + (i + 1), plate.transform, "REROLL", 12,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, SpiritPrefabUi.Theme.amber);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                labels[i] = plate.gameObject;
                plate.gameObject.SetActive(false);
            }
            SetArray(view, "diceButtons", buttons);
            SetArray(view, "diceSelectionLabels", labels);
        }

        public static SpiritPauseMenu CreatePause()
        {
            string path = Folder + "SpiritPauseMenu.prefab";
            if (File.Exists(path))
            {
                var existing = AssetDatabase.LoadAssetAtPath<SpiritPauseMenu>(path);
                if (existing == null) throw new InvalidOperationException("The pause prefab requires its root component.");
                return existing;
            }
            var root = SpiritPrefabUi.Full("MenuOverlay", null);
            root.gameObject.AddComponent<SpiritUiContext>().Configure(SpiritPrefabUi.Theme);
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .86f);
            var pause = root.gameObject.AddComponent<SpiritPauseMenu>();
            var panel = SpiritPrefabUi.Panel("MenuPanel", root, new Vector2(.12f, .17f), new Vector2(.88f, .83f),
                SpiritPrefabUi.Theme.panelColor).rectTransform;
            var frame = SpiritPrefabUi.Panel("Frame", panel, Vector2.zero, Vector2.one, SpiritPrefabUi.Theme.panelColor, 0, true);
            frame.raycastTarget = false;
            var content = SpiritPrefabUi.Rect("MenuContent", panel, new Vector2(.05f, .02f), new Vector2(.95f, .98f));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var title = SpiritPrefabUi.Heading("MenuTitle", content, "", 28, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter, SpiritPrefabUi.Theme.amber);
            Height(title.gameObject, 52);
            Set(pause, "Title", title);
            Set(pause, "SoundButton", PauseButton("SoundButton", "Sound: On", content));
            var summary = SpiritPrefabUi.Label("ProfileSummary", content, "", 14, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter, SpiritPrefabUi.Theme.amber);
            Height(summary.gameObject, 64);
            Set(pause, "ProfileSummary", summary);
            Set(pause, "CollectionButton", PauseButton("CollectionButton", "Collection", content));
            Set(pause, "SaveButton", PauseButton("SaveRunButton", "Save run", content));
            Set(pause, "LoadButton", PauseButton("LoadRunButton", "Load run", content));
            Set(pause, "NewRunButton", PauseButton("NewRunButton", "New run", content));
            Set(pause, "AbandonButton", PauseButton("AbandonRunButton", "Abandon run", content));
            Set(pause, "CloseButton", PauseButton("CloseMenuButton", "Back to table", content));
            Set(pause, "MainMenuButton", SpiritPrefabUi.Button("ReturnToMainMenuButton", root, "Main menu",
                new Vector2(.25f, .85f), new Vector2(.75f, .92f), compact: true));
            var creditsBacking = SpiritPrefabUi.Panel("AudioCreditsBacking", root, new Vector2(.04f, .025f), new Vector2(.96f, .16f),
                new Color(.025f, .035f, .025f, .97f)).rectTransform;
            var credits = SpiritPrefabUi.Label("AudioCredits", creditsBacking, "", 16, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter, SpiritPrefabUi.Theme.cream);
            Set(pause, "CreditsBacking", creditsBacking);
            Set(pause, "Credits", credits);
            root.gameObject.SetActive(false);
            try { return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<SpiritPauseMenu>(); }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        private static Button PauseButton(string name, string label, Transform parent)
        {
            var button = SpiritPrefabUi.Button(name, parent, label, Vector2.zero, Vector2.one, compact: true);
            Height(button.gameObject, 48);
            return button;
        }

        private static void Height(GameObject target, float height)
        {
            var layout = target.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? serialized.FindProperty("<" + field + ">k__BackingField");
            if (property == null) throw new InvalidOperationException("Missing authored reference " + field);
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
    }
}
