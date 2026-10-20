using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Presentation
{
    /** Portrait encounter surface; controls own rules and persistence, while this view presents ordered events. */
    [DisallowMultipleComponent]
    public sealed class SpiritEncounterView : MonoBehaviour, ISpiritScorePlaybackHost
    {
        private const float RoomTableEdge = .5323f;
        [SerializeField] private Texture2D roomTexture;
        [SerializeField] private EncounterRulePresentationAsset encounterRulePresentation;
        [SerializeField] private EncounterTablePresentationAsset tablePresentation;
        [SerializeField] private SpiritUiTheme uiTheme;
        [SerializeField] private SpiritTrayPresentationAsset trayPresentation;

        public SpiritUiTheme UiTheme => uiTheme;
        public SpiritTrayPresentationAsset TrayPresentation => trayPresentation;

        public bool IsCharmsOpen => OwnedCharmsPanel != null && OwnedCharmsPanel.Root.gameObject.activeSelf;
        public bool IsCharmPopupOpen => CharmPopup != null && CharmPopup.IsOpen;
        public long CharmPopupInstanceId => CharmPopup != null ? CharmPopup.InstanceId : -1;
        public bool IsHandRanksOpen => handRanksPanel != null && handRanksPanel.Root.gameObject.activeSelf;
        public SpiritGameplayHud GameplayHud { get; private set; }
        public SpiritRewardsPanel RewardsPanel { get; private set; }
        public SpiritOwnedCharmsPanel OwnedCharmsPanel { get; private set; }
        public SpiritCharmPopup CharmPopup { get; private set; }
        public SpiritDiceTray DiceTray { get; private set; }
        public SpiritTrayScene TrayScene { get; private set; }
        public SpiritModifierHistoryTray ModifierHistory { get; private set; }
        public Button PlayButton => GameplayHud != null ? GameplayHud.PlayButton : null;
        public Button RerollButton => GameplayHud != null ? GameplayHud.RerollButton : null;
        public Button CharmsButton => GameplayHud != null ? GameplayHud.CharmsButton : null;
        public Button HandRanksButton => GameplayHud != null ? GameplayHud.HandRanksButton : null;
        public Button RefreshButton => RewardsPanel != null ? RewardsPanel.RefreshButton : null;
        public Text BaseScoreLabel => GameplayHud != null ? GameplayHud.BaseScoreLabel : null;
        public Text MultiplierLabel => GameplayHud != null ? GameplayHud.MultiplierLabel : null;
        public Text HandScoreLabel => GameplayHud != null ? GameplayHud.HandScoreLabel : null;
        public EncounterRulePresentationAsset EncounterRulePresentation => encounterRulePresentation;
        public EncounterJourneyView JourneyView { get; private set; }
        public SpiritHostPortrait HostPortrait => hostPortrait;

        private ISpiritEncounterControls controls;
        private Canvas canvas;
        private RectTransform safeArea;
        private RectTransform portrait;
        private RectTransform diceTrayFrame;
        private RawImage trayImage;
        private Vector2 previousPortraitSize;
        private RectTransform endingPanel;
        private RectTransform menuPanel;
        private SpiritHandRanksPanel handRanksPanel;
        private SpiritHostPortrait hostPortrait;
        private IEncounterJourneyControls journeyControls;
        private ISpiritMainMenuControls mainMenuControls;
        private Button mainMenuButton;
        private EncounterTablePresentationAsset configuredTable;
        private RawImage roomImage;
        private RawImage tableImage;
        private readonly SceneLightingState sceneLightingState = new SceneLightingState();
        private SceneLightingMaterial sceneLightingMaterial;
        private Text menuTitle;
        private SpiritEncounterEndingView endingView;
        private SpiritCollectionView collectionView;
        private SpiritEncounterAudio encounterAudio;
        private SpiritActionModifierCue actionModifierCue;
        private SpiritScorePlayback scorePlayback;
        private CanvasGroup inputGroup;
        private readonly List<CanvasGroup> openingGroups = new List<CanvasGroup>();
        private Text profileSummary;
        private Button soundButton;
        private readonly List<Button> diceButtons = new List<Button>();
        private readonly List<GameObject> diceSelectionLabels = new List<GameObject>();
        private ScoreBreakdown lastScore;
        private EncounterSnapshot presentedSnapshot;
        private EncounterSnapshot presentationSnapshot;
        private bool presenting;
        private Rect previousSafeArea;
        private Vector2 previousScreenSize;
        private SpiritHostPresentationAsset compositionHost;

        /** Builds the gameplay HUD on an existing portrait for composition authoring. */
        public void BuildCompositionOverlay(RectTransform parent, SpiritUiTheme theme)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            if (portrait != null || canvas != null || controls != null)
                throw new InvalidOperationException("Build the composition overlay on an unused encounter view.");
            enabled = false;
            uiTheme = theme;
            var context = GetComponent<SpiritUiContext>();
            if (context == null) context = gameObject.AddComponent<SpiritUiContext>();
            context.Configure(theme);
            var parentContext = parent.GetComponent<SpiritUiContext>();
            if (parentContext == null) parentContext = parent.gameObject.AddComponent<SpiritUiContext>();
            parentContext.Configure(theme);
            portrait = parent;
            inputGroup = parent.GetComponent<CanvasGroup>();
            if (inputGroup == null) inputGroup = parent.gameObject.AddComponent<CanvasGroup>();
            inputGroup.blocksRaycasts = false;
            GameplayHud = gameObject.AddComponent<SpiritGameplayHud>();
            GameplayHud.BuildHeader(portrait, ToggleMenu);
            GameplayHud.BuildPlayingPanel();
            BuildPlayingHud();
            GameplayHud.BuildEncounterRule(diceTrayFrame);
            BuildFooter();
        }

        /** Representative playing values expose the HUD's occupied space without a game session. */
        public void RefreshCompositionOverlay(EncounterTablePresentationAsset table, DiceValues faces)
        {
            if (portrait == null || canvas != null || controls != null)
                throw new InvalidOperationException("Build the composition overlay before refreshing it.");
            if (!faces.IsValid) throw new ArgumentException("Every die must have a face from 1 to 6.", nameof(faces));
            SpiritHostPresentationAsset host = table != null ? table.HostPresentation : null;
            EncounterRulePresentationAsset rule = table != null && table.RulePresentations.Count > 0
                ? table.RulePresentations[0] : null;
            if (configuredTable != table || compositionHost != host || encounterRulePresentation != rule)
            {
                configuredTable = table;
                compositionHost = host;
                encounterRulePresentation = rule;
                GameplayHud.ConfigureEncounterRuleSigil(host);
            }
            var hud = GameplayHud;
            hud.EncounterTitle.text = table != null ? table.DisplayTitle : "ENCOUNTER";
            hud.StageLabel.text = "STAGE 1 / 3";
            hud.TargetLabel.text = "120";
            hud.TargetCaption.text = "TO CLEAR";
            hud.SetResources(3, 3);
            hud.ResourcesLabel.gameObject.SetActive(false);
            hud.CategoryLabel.text = DiagnosticText.Humanize(HandEvaluator.Evaluate(faces).ToString()).ToUpperInvariant();
            hud.SetScoreValues(0, 0);
            hud.HandScoreLabel.text = "0";
            hud.SelectionLabel.text = "Tap dice to select for reroll";
            hud.ScoreDetail.text = "ROLL BONUSES BANK HERE";
            hud.StatusLabel.text = "COMPOSITION PREVIEW";
            hud.InvestigationButton.gameObject.SetActive(table != null);
            string ruleText = rule != null ? rule.AvailableText?.Replace("{face}",
                faces[0].ToString(System.Globalization.CultureInfo.InvariantCulture)) : null;
            hud.RenderEncounterRuleBadge(encounterRulePresentation, rule != null, true, ruleText);
            foreach (Button button in new[] { hud.PlayButton, hud.RerollButton, hud.CharmsButton, hud.HandRanksButton,
                hud.MenuButton, hud.InvestigationButton })
                button.interactable = true;
        }

        public void Bind(ISpiritEncounterControls encounterControls, IEncounterJourneyControls journey)
        {
            if (journeyControls != null) journeyControls.JourneyChanged -= RefreshJourney;
            journeyControls = journey ?? throw new ArgumentNullException(nameof(journey));
            Bind(encounterControls);
            JourneyView.Bind(journeyControls, controls);
            journeyControls.JourneyChanged += RefreshJourney;
            RefreshJourney();
        }

        public void Bind(ISpiritEncounterControls encounterControls)
        {
            var nextControls = encounterControls ?? throw new ArgumentNullException(nameof(encounterControls));
            if (controls != null)
            {
                controls.StateChanged -= Refresh;
                controls.Resolved -= Present;
            }
            bool canceledPresentation = presenting;
            ResetPresentation();
            if (canceledPresentation) controls?.CompletePresentation();
            controls = nextControls;
            if (canvas == null) Build();
            RewardsPanel.Bind(controls);
            OwnedCharmsPanel.Bind(controls);
            presentedSnapshot = null;
            hostPortrait?.ResetPose();
            controls.StateChanged += Refresh;
            controls.Resolved += Present;
            Refresh();
        }

        public void BindJourney(IEncounterJourneyControls journey)
        {
            if (journeyControls != null) journeyControls.JourneyChanged -= RefreshJourney;
            journeyControls = journey ?? throw new ArgumentNullException(nameof(journey));
            if (canvas == null) Build();
            JourneyView.Bind(journeyControls, controls);
            journeyControls.JourneyChanged += RefreshJourney;
            RefreshJourney();
        }

        private void RefreshJourney()
        {
            ConfigureTable(journeyControls.SelectedJourney?.TablePresentation ?? tablePresentation);
            if (journeyControls.JourneyPhase != EncounterJourneyPhase.Playing)
            {
                bool canceled = presenting;
                ResetPresentation(false);
                if (canceled) controls?.CompletePresentation();
                OwnedCharmsPanel.Root.gameObject.SetActive(false);
                handRanksPanel.Root.gameObject.SetActive(false);
                menuPanel.gameObject.SetActive(false);
                SetGameplayVisible(false);
                bool seated = journeyControls.JourneyPhase == EncounterJourneyPhase.Sequence &&
                    journeyControls.SelectedJourney != null && journeyControls.BeatIndex >= 0 &&
                    journeyControls.BeatIndex < journeyControls.SelectedJourney.Beats.Count &&
                    journeyControls.SelectedJourney.Beats[journeyControls.BeatIndex].Kind != EncounterJourneyBeatKind.Illustration;
                hostPortrait?.SetVisible(seated);
            }
            else
            {
                hostPortrait?.SetVisible(true);
                hostPortrait?.SetState(SpiritPresentationState.Idle, 1);
                SetGameplayVisible(true);
            }
            JourneyView.Refresh();
            Refresh();
        }

        public void BindMainMenu(ISpiritMainMenuControls menuControls)
        {
            mainMenuControls = menuControls;
            if (canvas == null) Build();
            JourneyView.BindMainMenu(menuControls);
            mainMenuButton.gameObject.SetActive(menuControls != null);
        }

        private void SetGameplayVisible(bool visible)
        {
            GameplayHud.SetGameplayVisible(visible, journeyControls != null);
            RewardsPanel.Root.gameObject.SetActive(visible);
            endingPanel.gameObject.SetActive(visible);
        }

        private void ConfigureTable(EncounterTablePresentationAsset selectedTable)
        {
            if (configuredTable == selectedTable && (selectedTable == null || hostPortrait != null || selectedTable.HostPresentation == null)) return;
            ClearSceneLighting();
            configuredTable = selectedTable;
            if (hostPortrait != null) { hostPortrait.gameObject.SetActive(false); Destroy(hostPortrait.gameObject); hostPortrait = null; }
            if (selectedTable != null)
            {
                if (selectedTable.HostPresentation != null)
                {
                    hostPortrait = selectedTable.HostPresentation.Create(portrait);
                    hostPortrait.transform.SetSiblingIndex(tableImage.transform.GetSiblingIndex() + 1);
                    hostPortrait.SpiritGesture += () => encounterAudio.Play(SpiritAudioCue.Spirit);
                }
                encounterRulePresentation = selectedTable.RulePresentations.Count > 0 ? selectedTable.RulePresentations[0] : null;
                encounterAudio.SetTheme(selectedTable.AudioTheme);
            }
            GameplayHud.ConfigureHostSigils(selectedTable?.HostPresentation);
            actionModifierCue.ConfigureHost(selectedTable?.HostPresentation);
            Texture2D texture = selectedTable?.SceneLighting != null ? selectedTable.SceneLighting.BaseTexture :
                selectedTable != null ? selectedTable.RoomTexture : roomTexture;
            float edge = selectedTable != null ? selectedTable.RoomTableEdge : RoomTableEdge;
            float split = selectedTable != null ? selectedTable.RoomSplit : .625f;
            roomImage.texture = tableImage.texture = texture;
            roomImage.color = tableImage.color = texture != null ? Color.white : Color.clear;
            roomImage.uvRect = new Rect(0, edge, 1, 1 - edge);
            tableImage.uvRect = new Rect(0, 0, 1, edge);
            roomImage.rectTransform.anchorMin = new Vector2(0, split);
            tableImage.rectTransform.anchorMax = new Vector2(1, split);
            GameplayHud.EncounterTitle.text = menuTitle.text = selectedTable != null ? selectedTable.DisplayTitle : "";
            ConfigureTrays(selectedTable?.TrayPresentation ?? tablePresentation?.TrayPresentation ?? trayPresentation);
        }

        private void ConfigureTrays(SpiritTrayPresentationAsset presentation)
        {
            if (TrayScene == null || TrayScene.Presentation == presentation) return;
            DiceTray.ReleasePresentation();
            ModifierHistory.ReleasePresentation();
            TrayScene.Configure(presentation);
            TrayScene.Initialize();
            DiceTray.Configure(presentation, TrayScene);
            DiceTray.Initialize();
            ModifierHistory.Configure(presentation, TrayScene);
            ModifierHistory.Build(trayImage.rectTransform);
            trayImage.texture = TrayScene.Texture;
            PositionTrays();
        }

        private void PositionTrays()
        {
            if (TrayScene == null || TrayScene.Presentation == null) return;
            if (portrait.rect.width <= 0 || portrait.rect.height <= 0) return;
            Rect placement = TrayScene.Presentation.GetCompositionRect(portrait.rect.width / portrait.rect.height);
            var playingPanel = GameplayHud.PlayingPanel;
            Vector2 panelMin = playingPanel.anchorMin;
            Vector2 panelSize = playingPanel.anchorMax - panelMin;
            diceTrayFrame.anchorMin = new Vector2((placement.xMin - panelMin.x) / panelSize.x,
                (placement.yMin - panelMin.y) / panelSize.y);
            diceTrayFrame.anchorMax = new Vector2((placement.xMax - panelMin.x) / panelSize.x,
                (placement.yMax - panelMin.y) / panelSize.y);
            diceTrayFrame.offsetMin = diceTrayFrame.offsetMax = Vector2.zero;
            for (int i = 0; i < diceButtons.Count; ++i)
            {
                Rect bounds = DiceTray.GetSlotViewportRect(i);
                var hit = (RectTransform)diceButtons[i].transform;
                hit.anchorMin = bounds.min;
                hit.anchorMax = bounds.max;
                hit.offsetMin = hit.offsetMax = Vector2.zero;
                Vector2 labelCenter = DiceTray.GetSlotLabelViewportPosition(i);
                var plate = (RectTransform)diceSelectionLabels[i].transform;
                plate.anchorMin = labelCenter - new Vector2(bounds.width * .43f, .035f);
                plate.anchorMax = labelCenter + new Vector2(bounds.width * .43f, .035f);
                plate.offsetMin = plate.offsetMax = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            ClearSceneLighting();
            if (journeyControls != null) journeyControls.JourneyChanged -= RefreshJourney;
            if (controls == null) return;
            controls.StateChanged -= Refresh;
            controls.Resolved -= Present;
        }

        private void OnDisable()
        {
            ClearSceneLighting();
            bool canceled = presenting;
            ResetPresentation();
            if (JourneyView != null) JourneyView.gameObject.SetActive(false);
            if (canceled) controls?.CompletePresentation();
        }

        private void OnEnable()
        {
            if (canvas == null) return;
            if (journeyControls != null) RefreshJourney();
            else Refresh();
        }

        private void Update()
        {
            if (canvas != null && (previousSafeArea != Screen.safeArea ||
                previousScreenSize != new Vector2(Screen.width, Screen.height))) FitScreen();
        }

        private void LateUpdate()
        {
            UpdateSceneLighting();
            if (portrait == null || diceTrayFrame == null || ModifierHistory == null) return;
            PositionTrays();
            Vector2 size = portrait.rect.size;
            if (size == previousPortraitSize) return;
            previousPortraitSize = size;
            if (RewardsPanel.Root.gameObject.activeSelf) RewardsPanel.UpdateLayout();
            if (IsCharmPopupOpen) CharmPopup.Layout();
        }

        private void UpdateSceneLighting()
        {
            var asset = configuredTable?.SceneLighting;
            sceneLightingState.Sample(asset, Time.unscaledTimeAsDouble);
            if (asset != null)
            {
                if (sceneLightingMaterial == null || sceneLightingMaterial.Material.shader != asset.Shader)
                {
                    sceneLightingMaterial?.Dispose();
                    sceneLightingMaterial = new SceneLightingMaterial(asset.Shader);
                }
                sceneLightingMaterial.Apply(sceneLightingState);
            }
            else
            {
                sceneLightingMaterial?.Dispose();
                sceneLightingMaterial = null;
            }
            Texture2D texture = asset != null ? asset.BaseTexture :
                configuredTable != null ? configuredTable.RoomTexture : roomTexture;
            SetBackgroundLighting(roomImage, texture, sceneLightingMaterial?.Material);
            SetBackgroundLighting(tableImage, texture, sceneLightingMaterial?.Material);
            var state = asset != null ? sceneLightingState : null;
            TrayScene?.SetSceneLighting(state);
            DiceTray?.SetSceneLighting(state);
            ModifierHistory?.Renderer?.SetSceneLighting(state);
            hostPortrait?.SetSceneLighting(state, configuredTable != null ? configuredTable.HostLightingPosition : Vector3.zero);
        }

        private static void SetBackgroundLighting(RawImage image, Texture2D texture, Material material)
        {
            if (image == null) return;
            image.texture = texture;
            image.color = texture != null ? Color.white : Color.clear;
            image.material = material;
        }

        private void ClearSceneLighting()
        {
            if (roomImage != null) roomImage.material = null;
            if (tableImage != null) tableImage.material = null;
            sceneLightingMaterial?.Dispose();
            sceneLightingMaterial = null;
            sceneLightingState.Sample(null, Time.unscaledTimeAsDouble);
            TrayScene?.SetSceneLighting(null);
            DiceTray?.SetSceneLighting(null);
            ModifierHistory?.Renderer?.SetSceneLighting(null);
            hostPortrait?.SetSceneLighting(null, Vector3.zero);
        }

        private void Build()
        {
            var context = GetComponent<SpiritUiContext>() ?? gameObject.AddComponent<SpiritUiContext>();
            context.Configure(uiTheme);
            encounterAudio = GetComponent<SpiritEncounterAudio>() ?? gameObject.AddComponent<SpiritEncounterAudio>();
            encounterAudio.enabled = false;
            encounterAudio.SetTheme(tablePresentation?.AudioTheme);
            var canvasRect = SpiritUi.Full("SpiritCanvas", transform);
            canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            SpiritUi.Panel("Letterbox", canvasRect, Vector2.zero, Vector2.one, new Color32(12, 18, 16, 255));
            safeArea = SpiritUi.Full("SafeArea", canvasRect);
            portrait = SpiritUi.Rect("Portrait", safeArea, new Vector2(.5f, 0), new Vector2(.5f, 1));
            SpiritUi.Panel("ForestBackground", portrait, Vector2.zero, Vector2.one, new Color32(20, 29, 25, 255));
            var room = SpiritUi.Rect("TeaRoom", portrait, new Vector2(0, .625f), Vector2.one)
                .gameObject.AddComponent<RawImage>();
            roomImage = room;
            room.texture = roomTexture;
            room.uvRect = new Rect(0, RoomTableEdge, 1, 1 - RoomTableEdge);
            room.color = roomTexture != null ? Color.white : Color.clear;
            room.raycastTarget = false;
            inputGroup = portrait.gameObject.AddComponent<CanvasGroup>();

            var table = SpiritUi.Rect("ForegroundTable", portrait, Vector2.zero, new Vector2(1, .625f))
                .gameObject.AddComponent<RawImage>();
            tableImage = table;
            table.texture = roomTexture;
            table.uvRect = new Rect(0, 0, 1, RoomTableEdge);
            table.color = roomTexture != null ? Color.white : Color.clear;
            table.raycastTarget = false;
            GameplayHud = gameObject.AddComponent<SpiritGameplayHud>();
            GameplayHud.BuildHeader(portrait, ToggleMenu);

            BuildPlaying();
            RewardsPanel = gameObject.AddComponent<SpiritRewardsPanel>();
            RewardsPanel.Build(portrait, () => Busy);
            GameplayHud.BuildStageIntro();
            BuildEnding();
            BuildFooter();
            OwnedCharmsPanel = gameObject.AddComponent<SpiritOwnedCharmsPanel>();
            OwnedCharmsPanel.Build(portrait, ShowTable, () => Busy);
            handRanksPanel = gameObject.AddComponent<SpiritHandRanksPanel>();
            handRanksPanel.Build(portrait, ShowTable);
            BuildMenu();
            var hud = GameplayHud;
            foreach (var element in new Transform[] { hud.PlayingPanel, hud.TargetPanel, hud.HandsPanel, hud.RerollsPanel,
                hud.StageLabel.transform, hud.StatusLabel.transform, hud.ResourcesLabel.transform, hud.CharmsButton.transform,
                hud.HandRanksButton.transform, hud.MenuButton.transform, hud.InvestigationButton.transform })
                openingGroups.Add(element.gameObject.AddComponent<CanvasGroup>());
            JourneyView = SpiritUi.Full("EncounterJourneyOverlay", portrait).gameObject.AddComponent<EncounterJourneyView>();
            JourneyView.Build(encounterAudio, (state, progress) => hostPortrait?.SetState(state, progress));
            collectionView = gameObject.AddComponent<SpiritCollectionView>();
            collectionView.Build(portrait, () => collectionView.Hide());
            JourneyView.SetCollectionAction(ShowCollection);
            CharmPopup = gameObject.AddComponent<SpiritCharmPopup>();
            CharmPopup.Build(canvas.transform, portrait);
            scorePlayback = new SpiritScorePlayback(this, GameplayHud, DiceTray, ModifierHistory, encounterAudio,
                actionModifierCue, endingView);
            ConfigureTable(tablePresentation);
            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("SpiritEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(transform, false);
            }
            FitScreen();
        }

        private void FitScreen()
        {
            previousSafeArea = Screen.safeArea;
            previousScreenSize = new Vector2(Screen.width, Screen.height);
            if (Screen.width == 0 || Screen.height == 0) return;
            safeArea.anchorMin = new Vector2(previousSafeArea.xMin / Screen.width, previousSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(previousSafeArea.xMax / Screen.width, previousSafeArea.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            float scale = Screen.height / 960f;
            float width = Mathf.Min(540, previousSafeArea.width / scale);
            portrait.sizeDelta = new Vector2(width, 0);
            portrait.anchoredPosition = Vector2.zero;
        }

        private void BuildPlaying()
        {
            var playingPanel = GameplayHud.BuildPlayingPanel();
            var trayFrame = SpiritUi.Rect("DiceTrayFrame", playingPanel, new Vector2(0, .578f), new Vector2(1, .938f));
            diceTrayFrame = trayFrame;
            var trayRect = SpiritUi.Full("DiceTrayImage", trayFrame);
            trayImage = trayRect.gameObject.AddComponent<RawImage>();
            var presentation = tablePresentation?.TrayPresentation ?? trayPresentation;
            TrayScene = gameObject.AddComponent<SpiritTrayScene>();
            TrayScene.Configure(presentation);
            TrayScene.Initialize();
            DiceTray = GetComponent<SpiritDiceTray>() ?? gameObject.AddComponent<SpiritDiceTray>();
            DiceTray.Configure(presentation, TrayScene);
            DiceTray.Initialize();
            trayImage.texture = TrayScene.Texture;
            trayImage.raycastTarget = false;
            var hitArea = SpiritUi.Full("DiceTouchTargets", trayRect);
            float slotWidth = DiceTray.GetSlotViewportPosition(1).x - DiceTray.GetSlotViewportPosition(0).x;
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                int index = i;
                Vector2 center = DiceTray.GetSlotViewportPosition(i);
                var hit = SpiritUi.Rect("DieButton" + (i + 1), hitArea,
                    new Vector2(center.x - slotWidth * .48f, .04f), new Vector2(center.x + slotWidth * .48f, .95f));
                var image = hit.gameObject.AddComponent<Image>();
                image.color = Color.clear;
                var button = hit.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => { if (!Busy) controls.ToggleDie(index); });
                diceButtons.Add(button);
                Vector2 labelCenter = DiceTray.GetSlotLabelViewportPosition(i);
                var plate = SpiritUi.Panel("DieRerollPlate" + (i + 1), trayRect,
                    labelCenter - new Vector2(slotWidth * .43f, .064f),
                    labelCenter + new Vector2(slotWidth * .43f, .064f), new Color32(38, 26, 12, 245), 0, true);
                plate.raycastTarget = false;
                var label = SpiritUi.Label("DieRerollLabel" + (i + 1), plate.transform, "REROLL", 12,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                diceSelectionLabels.Add(plate.gameObject);
                plate.gameObject.SetActive(false);
            }
            ModifierHistory = trayRect.gameObject.AddComponent<SpiritModifierHistoryTray>();
            ModifierHistory.Configure(presentation, TrayScene);
            ModifierHistory.Build(trayRect);
            ModifierHistory.TokenSelected += (contribution, definition) =>
            {
                ShowCharmPopup(contribution.InstanceId);
            };
            PositionTrays();
            BuildPlayingHud();
            actionModifierCue = gameObject.AddComponent<SpiritActionModifierCue>();
            actionModifierCue.Initialize(RerollButton, PlayButton);
            GameplayHud.BuildEncounterRule(diceTrayFrame);
        }

        private void BuildPlayingHud()
        {
            GameplayHud.BuildPlayingHud(() => { if (!Busy) controls.Reroll(); }, () => { if (!Busy) controls.PlayHand(); });
        }

        private void BuildFooter()
        {
            GameplayHud.BuildFooter(ShowCharms, ShowHandRanks, () => { if (!Busy) journeyControls?.ReturnToInvestigation(); });
        }

        private void BuildEnding()
        {
            endingView = gameObject.AddComponent<SpiritEncounterEndingView>();
            endingView.Build(portrait,
                () => { if (!Busy) { if (journeyControls != null) journeyControls.ReturnToInvestigation(); else controls.StartNew(); } },
                () => { if (!Busy) { if (journeyControls != null) journeyControls.RetryEncounter(); else controls.StartNew(); } });
            endingPanel = endingView.Root;
        }

        public void ShowCharmPopup(long instanceId)
        {
            if (Busy || controls.Snapshot == null || controls.Snapshot.Phase != EncounterPhase.Playing ||
                journeyControls != null && journeyControls.JourneyPhase != EncounterJourneyPhase.Playing) return;
            foreach (var instance in controls.Snapshot.Modifiers)
            {
                if (instance.InstanceId != instanceId) continue;
                var definition = controls.Catalog.GetModifier(instance.DefinitionId);
                OwnedCharmsPanel.Root.gameObject.SetActive(false);
                handRanksPanel.Root.gameObject.SetActive(false);
                menuPanel.gameObject.SetActive(false);
                CharmPopup.Show(instanceId, definition, SpiritCharmVisuals.Status(controls, instance, definition),
                    controls.Snapshot.Phase);
                return;
            }
        }

        public void CloseCharmPopup()
        {
            if (CharmPopup != null) CharmPopup.Close();
        }

        private void BuildMenu()
        {
            menuPanel = SpiritUi.Panel("MenuOverlay", portrait, Vector2.zero, Vector2.one, new Color(0, 0, 0, .86f)).rectTransform;
            var panel = SpiritUi.Panel("MenuPanel", menuPanel, new Vector2(.12f, .17f), new Vector2(.88f, .83f), SpiritUi.GetTheme(transform).panelColor, 0, true);
            menuTitle = SpiritUi.Heading("MenuTitle", panel.transform, "", 28, new Vector2(0, .9f), Vector2.one,
                TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            soundButton = SpiritUi.Button("SoundButton", panel.transform, encounterAudio.Muted ? "Sound: Off" : "Sound: On",
                new Vector2(.2f, .8f), new Vector2(.8f, .895f), () =>
                {
                    encounterAudio.Muted = !encounterAudio.Muted;
                    SpiritUi.SetButtonLabel(soundButton, encounterAudio.Muted ? "Sound: Off" : "Sound: On");
                });
            profileSummary = SpiritUi.Label("ProfileSummary", panel.transform, "", 14, new Vector2(.05f, .67f),
                new Vector2(.95f, .80f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            MenuAction(panel.transform, "CollectionButton", "Collection", .55f, ShowCollection);
            MenuAction(panel.transform, "SaveRunButton", "Save run", .45f, () => controls.Save());
            MenuAction(panel.transform, "LoadRunButton", "Load run", .35f, () => controls.Load());
            MenuAction(panel.transform, "NewRunButton", "New run", .25f, () => controls.StartNew());
            MenuAction(panel.transform, "AbandonRunButton", "Abandon run", .15f, () => controls.Abandon());
            mainMenuButton = SpiritUi.Button("ReturnToMainMenuButton", menuPanel, "Main menu",
                new Vector2(.25f, .85f), new Vector2(.75f, .92f), () =>
                {
                    if (!Busy) mainMenuControls?.ShowMainMenu();
                }, compact: true);
            mainMenuButton.gameObject.SetActive(false);
            SpiritUi.Button("CloseMenuButton", panel.transform, "Back to table", new Vector2(.05f, .02f), new Vector2(.95f, .13f), ToggleMenu);
            if (encounterAudio.Theme != null && encounterAudio.Theme.credits != null)
            {
                SpiritUi.Panel("AudioCreditsBacking", menuPanel, new Vector2(.04f, .025f), new Vector2(.96f, .16f),
                    new Color(.025f, .035f, .025f, .97f));
                SpiritUi.Label("AudioCredits", menuPanel, encounterAudio.Theme.credits.text, 16,
                    new Vector2(.05f, .035f), new Vector2(.95f, .155f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).cream);
            }
            menuPanel.gameObject.SetActive(false);
        }

        private void MenuAction(Transform parent, string name, string title, float y, Action action)
        {
            SpiritUi.Button(name, parent, title, new Vector2(.05f, y), new Vector2(.95f, y + .09f), () =>
            {
                if (Busy) return;
                menuPanel.gameObject.SetActive(false);
                action();
            });
        }

        private bool Busy => controls == null || controls.IsBusy || presenting;

        public void ShowCharms()
        {
            if (Busy) return;
            CloseCharmPopup();
            menuPanel.gameObject.SetActive(false);
            handRanksPanel.Root.gameObject.SetActive(false);
            OwnedCharmsPanel.Show();
        }

        public void ShowHandRanks()
        {
            if (Busy) return;
            CloseCharmPopup();
            menuPanel.gameObject.SetActive(false);
            OwnedCharmsPanel.Root.gameObject.SetActive(false);
            RenderHandRanks();
            handRanksPanel.Root.gameObject.SetActive(true);
        }

        private void RenderHandRanks()
        {
            handRanksPanel.Render(controls.Catalog, controls.Snapshot,
                controls.Snapshot.Phase == EncounterPhase.Playing ? controls.Preview?.Score.Category : null);
        }

        public void ShowTable()
        {
            if (Busy) return;
            CloseCharmPopup();
            OwnedCharmsPanel.Root.gameObject.SetActive(false);
            handRanksPanel.Root.gameObject.SetActive(false);
        }

        private void ToggleMenu()
        {
            if (Busy) return;
            CloseCharmPopup();
            OwnedCharmsPanel.Root.gameObject.SetActive(false);
            handRanksPanel.Root.gameObject.SetActive(false);
            menuPanel.gameObject.SetActive(!menuPanel.gameObject.activeSelf);
        }

        private void Refresh()
        {
            if (canvas == null || controls == null) return;
            bool atMenu = journeyControls != null && journeyControls.JourneyPhase == EncounterJourneyPhase.MainMenu;
            roomImage.enabled = tableImage.enabled = !atMenu;
            encounterAudio.enabled = !atMenu;
            if (journeyControls == null) JourneyView.gameObject.SetActive(false);
            if (journeyControls != null && journeyControls.JourneyPhase != EncounterJourneyPhase.Playing)
            {
                inputGroup.interactable = true;
                ModifierHistory.SetInteractable(false);
                SetGameplayVisible(false);
                return;
            }
            if (presenting && !ReferenceEquals(controls.Snapshot, presentationSnapshot))
            {
                ResetPresentation();
                controls.CompletePresentation();
                Refresh();
                return;
            }
            inputGroup.interactable = !Busy;
            ModifierHistory.SetInteractable(!Busy);
            if (Busy) return;
            var hud = GameplayHud;
            var snapshot = controls.Snapshot;
            if (snapshot == null)
            {
                CloseCharmPopup();
                hud.RenderEncounterRule(null, encounterRulePresentation);
                inputGroup.interactable = false;
                ModifierHistory.SetInteractable(false);
                hud.PlayingPanel.gameObject.SetActive(false);
                RewardsPanel.Root.gameObject.SetActive(false);
                endingPanel.gameObject.SetActive(false);
                hud.StatusLabel.text = controls.StatusMessage;
                return;
            }
            if (!ReferenceEquals(snapshot, presentedSnapshot))
            {
                ResetPresentation();
                hostPortrait?.ResetPose();
                lastScore = null;
                RewardsPanel.Invalidate();
                OwnedCharmsPanel.Invalidate();
                presentedSnapshot = snapshot;
            }
            bool playing = snapshot.Phase == EncounterPhase.Playing;
            if (playing && controls.Preview != null)
                ModifierHistory.SynchronizeReady(controls.Preview.Score.ModifierContributions, controls.Catalog);
            else ModifierHistory.Clear();
            bool completed = snapshot.Phase == EncounterPhase.Completed;
            bool choosingUpgrade = snapshot.Phase == EncounterPhase.ChoosingHandUpgrade;
            bool choosingUnlock = snapshot.Phase == EncounterPhase.ChoosingUnlock;
            hud.TargetPanel.gameObject.SetActive(!choosingUpgrade && !choosingUnlock);
            hud.StatusLabel.gameObject.SetActive(!choosingUpgrade && !choosingUnlock);
            hud.PlayingPanel.gameObject.SetActive(playing);
            RewardsPanel.Root.gameObject.SetActive(!playing && !completed);
            endingPanel.gameObject.SetActive(completed);
            hud.StageLabel.text = "STAGE " + (snapshot.StageIndex + 1) + " / " + snapshot.StageCount;
            hud.TargetLabel.text = DiagnosticText.Number(completed ? snapshot.TotalScore : snapshot.RemainingThreshold);
            hud.TargetCaption.text = playing ? "TO CLEAR" : completed ? "TOTAL SCORE" : "STAGE CLEARED";
            hud.HandsPanel.gameObject.SetActive(playing);
            hud.RerollsPanel.gameObject.SetActive(playing);
            hud.ResourcesLabel.gameObject.SetActive(!playing && !choosingUpgrade && !choosingUnlock);
            hud.SetResources(snapshot.HandsRemaining, snapshot.RerollsRemaining);
            hud.RenderEncounterRule(snapshot, encounterRulePresentation);
            hud.ResourcesLabel.text = playing ? snapshot.HandsRemaining + " hands left  /  " + snapshot.RerollsRemaining + " rerolls left" :
                lastScore != null ? "Last hand  " + DiagnosticText.Number(lastScore.HandScore) + " points" :
                "Total score  " + DiagnosticText.Number(snapshot.TotalScore);
            hud.CharmCount.text = snapshot.Modifiers.Count + " kept";
            SpiritUi.SetButtonLabel(hud.CharmsButton, "CHARMS (" + snapshot.Modifiers.Count + ")");
            hud.StatusLabel.text = ProductStatus(controls.StatusMessage);
            RenderProfileSummary();
            if (playing)
            {
                hud.ModifierEmblem.gameObject.SetActive(false);
                DiceTray.SetValues(snapshot.CurrentDice);
                SetDiceSelection(controls.SelectedMask);
                var score = controls.Preview?.Score;
                hud.SetScore(score, false);
                hud.SetScoreValues(snapshot.ModifierBaseScore, snapshot.ModifierMultiplier);
                hud.HandScoreLabel.text = "0";
                hud.ScoreDetail.text = "ROLL BONUSES BANK HERE";
                hud.PlayButton.interactable = true;
                hud.RerollButton.interactable = controls.SelectedMask != 0 && snapshot.CanReroll;
                int selected = 0;
                for (int i = 0; i < DiceValues.Count; ++i) if ((controls.SelectedMask & (1 << i)) != 0) ++selected;
                hud.SelectionLabel.text = selected == 0 ? "Tap dice to select for reroll" : selected + " selected for reroll";
                hud.SelectionLabel.text += "\n" + (ModifierHistory.Count == 0 ? "No charms ready on play" :
                    ModifierHistory.Count + (ModifierHistory.Count == 1 ? " charm ready" : " charms ready") + " - tap to inspect");
            }
            else if (!completed) RewardsPanel.Render(snapshot);
            else
            {
                SetGameplayVisible(false);
                endingPanel.gameObject.SetActive(true);
                endingView.Render(snapshot, journeyControls?.SelectedJourney,
                    SpiritCharmVisuals.UnlockSummary(controls, snapshot.NewlyUnlockedModifierIds));
            }
            if (IsCharmsOpen) OwnedCharmsPanel.Render();
            if (IsHandRanksOpen) RenderHandRanks();
        }

        private void ResetPresentation(bool resetHost = true)
        {
            CloseCharmPopup();
            StopAllCoroutines();
            endingView?.Reset();
            SetOpeningOpacity(1);
            scorePlayback?.CancelRoll();
            presenting = false;
            presentationSnapshot = null;
            if (resetHost) hostPortrait?.ResetPose();
            ResetTransientVisuals();
        }

        private void ResetTransientVisuals()
        {
            actionModifierCue?.Clear();
            DiceTray?.SetScoringHighlight(0);
            ModifierHistory?.ClearResolving();
            if (GameplayHud != null) GameplayHud.ResetTransientVisuals();
        }

        private void SetDiceSelection(int mask)
        {
            DiceTray.SetSelection(mask);
            for (int index = 0; index < diceSelectionLabels.Count; ++index)
                diceSelectionLabels[index].SetActive((mask & (1 << index)) != 0);
            SpiritUi.SetButtonLabel(RerollButton, "Reroll");
        }

        private static string ProductStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return "";
            int profileWarning = status.IndexOf("Progress is pending:", StringComparison.Ordinal);
            if (profileWarning >= 0) return status.Substring(profileWarning);
            if (status.StartsWith("Started encounter", StringComparison.Ordinal) ||
                status.StartsWith("Accepted action", StringComparison.Ordinal) ||
                status.StartsWith("Encounter ready.", StringComparison.Ordinal))
            {
                int previewWarning = status.IndexOf(" Hand preview unavailable:", StringComparison.Ordinal);
                int inspectionWarning = status.IndexOf(" Modifier inspection unavailable:", StringComparison.Ordinal);
                int warning = previewWarning < 0 ? inspectionWarning : inspectionWarning < 0 ? previewWarning :
                    Math.Min(previewWarning, inspectionWarning);
                return warning < 0 ? "" : status.Substring(warning).Trim();
            }
            return status;
        }

        private void Present(Resolution resolution)
        {
            if (resolution == null) return;
            if (!resolution.Accepted) { Refresh(); return; }
            CloseCharmPopup();
            presenting = true;
            presentationSnapshot = resolution.Snapshot;
            inputGroup.interactable = false;
            ModifierHistory.SetInteractable(false);
            OwnedCharmsPanel.Root.gameObject.SetActive(false);
            handRanksPanel.Root.gameObject.SetActive(false);
            menuPanel.gameObject.SetActive(false);
            if (resolution.Snapshot.Phase == EncounterPhase.Completed && RewardsPanel.Root.gameObject.activeSelf)
                SetGameplayVisible(false);
            float openingFade = resolution.PreviousRevision < 0 && configuredTable != null
                ? Mathf.Clamp(configuredTable.OpeningFadeDuration, 0, 2) : 0;
            if (openingFade > 0) SetOpeningOpacity(0);
            StartCoroutine(scorePlayback.Play(resolution, openingFade));
        }

        private void SetOpeningOpacity(float opacity)
        {
            foreach (var group in openingGroups)
                if (group != null) group.alpha = opacity;
        }

        private void RenderProfileSummary()
        {
            if (!(controls is ISpiritProgressionControls progression)) { profileSummary.text = ""; return; }
            int unlocked = 0;
            var ids = new HashSet<string>(progression.ProfileUnlockedModifierIds, StringComparer.Ordinal);
            foreach (var modifier in controls.Catalog.Modifiers)
                if (ids.Contains(modifier.Id)) ++unlocked;
            profileSummary.text = string.IsNullOrEmpty(progression.ProfileStatusMessage)
                ? unlocked + (unlocked == 1 ? " charm permanently unlocked\n" : " charms permanently unlocked\n") +
                    progression.CompletedEncounterIds.Count +
                    (progression.CompletedEncounterIds.Count == 1 ? " encounter completed" : " encounters completed")
                : "Player profile needs attention.\n" +
                    (SpiritCharmVisuals.HasPendingProgress(controls) ? "Progress save is pending." : "Profile could not be loaded.");
        }

        public void ShowCollection()
        {
            if (Busy || controls?.Catalog == null || collectionView == null) return;
            CloseCharmPopup();
            menuPanel.gameObject.SetActive(false);
            collectionView.Show(controls.Catalog,
                (controls as ISpiritProgressionControls)?.ProfileUnlockedModifierIds ?? Array.Empty<string>(),
                journeyControls?.JourneyCatalog,
                (controls as ISpiritProgressionControls)?.ProfileDiscoveredFamilyIds ?? Array.Empty<string>());
        }

        ISpiritEncounterControls ISpiritScorePlaybackHost.Controls => controls;
        IEncounterJourneyControls ISpiritScorePlaybackHost.Journey => journeyControls;
        SpiritHostPortrait ISpiritScorePlaybackHost.Host => hostPortrait;
        EncounterRulePresentationAsset ISpiritScorePlaybackHost.RulePresentation => encounterRulePresentation;
        void ISpiritScorePlaybackHost.SetDiceSelection(int mask) => SetDiceSelection(mask);
        void ISpiritScorePlaybackHost.SetOpeningOpacity(float opacity) => SetOpeningOpacity(opacity);
        void ISpiritScorePlaybackHost.RecordScore(ScoreBreakdown score) => lastScore = score;

        void ISpiritScorePlaybackHost.HideRewardsAndEnding()
        {
            RewardsPanel.Root.gameObject.SetActive(false);
            endingPanel.gameObject.SetActive(false);
        }

        void ISpiritScorePlaybackHost.ShowEnding()
        {
            SetGameplayVisible(false);
            endingPanel.gameObject.SetActive(true);
        }

        void ISpiritScorePlaybackHost.FinishPresentation(Resolution resolution)
        {
            presentedSnapshot = resolution.Snapshot;
            presenting = false;
            presentationSnapshot = null;
            ResetTransientVisuals();
            controls.CompletePresentation();
            Refresh();
        }
    }
}
