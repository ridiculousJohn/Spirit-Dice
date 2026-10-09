using System;
using System.Collections;
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
    public sealed class SpiritEncounterView : MonoBehaviour
    {
        private const float RoomTableEdge = .5323f;
        private const float ScoreStepDuration = .52f;
        private const float ScoreStepHold = .35f;
        private const float ModifierStepHold = .55f;
        [SerializeField] private Texture2D roomTexture;
        [SerializeField] private Texture2D foxBodyTexture;
        [SerializeField] private Texture2D foxHeadTexture;
        [SerializeField] private Texture2D foxTurnedHeadTexture;
        [SerializeField] private EncounterRulePresentationAsset encounterRulePresentation;
        [SerializeField] private EncounterTablePresentationAsset tablePresentation;

        public bool IsCharmsOpen => charmsPanel != null && charmsPanel.gameObject.activeSelf;
        public bool IsHandRanksOpen => handRanksPanel != null && handRanksPanel.Root.gameObject.activeSelf;
        public SpiritDiceTray DiceTray { get; private set; }
        public SpiritModifierHistoryTray ModifierHistory { get; private set; }
        public Button PlayButton { get; private set; }
        public Button RerollButton { get; private set; }
        public Button CharmsButton { get; private set; }
        public Button HandRanksButton { get; private set; }
        public Button RefreshButton { get; private set; }
        public Text BaseScoreLabel { get; private set; }
        public Text MultiplierLabel { get; private set; }
        public Text HandScoreLabel { get; private set; }
        public EncounterRulePresentationAsset EncounterRulePresentation => encounterRulePresentation;
        public EncounterJourneyView JourneyView { get; private set; }
        public SpiritHostPortrait HostPortrait => fox;

        private ISpiritEncounterControls controls;
        private Canvas canvas;
        private RectTransform safeArea;
        private RectTransform portrait;
        private RectTransform playingPanel;
        private RectTransform targetPanel;
        private RectTransform diceTrayFrame;
        private Vector2 previousPortraitSize;
        private RectTransform rewardsPanel;
        private RectTransform stageIntroPanel;
        private RectTransform endingPanel;
        private RectTransform charmsPanel;
        private RectTransform menuPanel;
        private SpiritHandRanksPanel handRanksPanel;
        private RectTransform rewardContent;
        private RectTransform charmContent;
        private SpiritCharmGridLayout charmGrid;
        private SpiritCharmDetailsLayout charmDetailsLayout;
        private SpiritHostPortrait fox;
        private IEncounterJourneyControls journeyControls;
        private EncounterTablePresentationAsset configuredTable;
        private RawImage roomImage;
        private RawImage tableImage;
        private Text encounterTitle;
        private Text menuTitle;
        private Button endingContinueButton;
        private Button investigationButton;
        private SpiritEncounterAudio encounterAudio;
        private SpiritActionModifierCue actionModifierCue;
        private CanvasGroup inputGroup;
        private Text stageLabel;
        private Text targetLabel;
        private Text targetCaption;
        private Text resourcesLabel;
        private Text handsLabel;
        private Text rerollsLabel;
        private RectTransform handsPanel;
        private RectTransform rerollsPanel;
        private Text categoryLabel;
        private Text scoreDetail;
        private Text statusLabel;
        private Text rewardTitle;
        private Text rewardInstruction;
        private Text rewardStep;
        private Text upcomingStage;
        private Text stageIntroTitle;
        private Text stageIntroDetail;
        private CanvasGroup encounterRuleGroup;
        private Text encounterRuleLabel;
        private Image encounterRuleIcon;
        private Graphic encounterRuleSigil;
        private RectTransform encounterRuleSigilRoot;
        private RectTransform rerollActionLabel;
        private Vector2 rerollLabelMin;
        private Vector2 rerollLabelMax;
        private CanvasGroup ruleSpiritSweep;
        private Image ruleSpiritIcon;
        private Graphic ruleSpiritSigil;
        private RectTransform ruleSpiritSigilRoot;
        private Text endingTitle;
        private Text endingDetail;
        private Text endingUnlocks;
        private RectTransform endingUnlockContent;
        private Text profileSummary;
        private Text charmTitle;
        private Text charmDetail;
        private RectTransform charmDetailStatusRow;
        private Text charmDetailStatus;
        private SpiritCharmStatusBadge charmDetailBadge;
        private Text charmCount;
        private Text selectionLabel;
        private SpiritUiOrnament modifierEmblem;
        private Image modifierActivationIcon;
        private Image charmDetailIcon;
        private SpiritModifierSigilGraphic charmDetailSigil;
        private CounterHighlight baseScoreHighlight;
        private CounterHighlight multiplierHighlight;
        private CounterHighlight rerollsHighlight;
        private CounterHighlight handsHighlight;
        private Button soundButton;
        private Button menuButton;
        private readonly List<Button> diceButtons = new List<Button>();
        private readonly List<GameObject> diceSelectionLabels = new List<GameObject>();
        private readonly List<SpiritRewardCard> rewardCards = new List<SpiritRewardCard>();
        private readonly List<Card> charmCards = new List<Card>();
        private long renderedOffer = -1;
        private long renderedCharmsRevision = -1;
        private long selectedCharm = -1;
        private ScoreBreakdown lastScore;
        private EncounterSnapshot presentedSnapshot;
        private EncounterSnapshot presentationSnapshot;
        private IEnumerator activeRoll;
        private bool presenting;
        private long displayedBaseScore;
        private long displayedMultiplier;
        private Rect previousSafeArea;
        private Vector2 previousScreenSize;

        private sealed class Card
        {
            internal Button Button;
            internal Text Title;
            internal Text Body;
            internal Text Effect;
            internal Image Icon;
            internal SpiritModifierSigilGraphic Sigil;
            internal SpiritCharmCardLayout Layout;
            internal SpiritCharmStatusBadge Badge;
            internal long OfferId;
            internal long OptionId;
            internal long InstanceId;
        }

        private sealed class CounterHighlight
        {
            internal Text Label;
            internal Text Caption;
            internal Image Icon;
            internal Vector2 OriginalInset;
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
            presentedSnapshot = null;
            fox?.ResetPose();
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
                ResetPresentation();
                if (canceled) controls?.CompletePresentation();
                charmsPanel.gameObject.SetActive(false);
                handRanksPanel.Root.gameObject.SetActive(false);
                menuPanel.gameObject.SetActive(false);
                SetGameplayVisible(false);
                bool seated = journeyControls.JourneyPhase == EncounterJourneyPhase.Sequence &&
                    journeyControls.SelectedJourney != null && journeyControls.BeatIndex >= 0 &&
                    journeyControls.BeatIndex < journeyControls.SelectedJourney.Beats.Count &&
                    journeyControls.SelectedJourney.Beats[journeyControls.BeatIndex].Kind != EncounterJourneyBeatKind.Illustration;
                fox?.SetVisible(seated);
            }
            else
            {
                fox?.SetVisible(true);
                SetGameplayVisible(true);
            }
            JourneyView.Refresh();
            Refresh();
        }

        private void SetGameplayVisible(bool visible)
        {
            foreach (var rect in new[] { playingPanel, rewardsPanel, endingPanel, targetPanel, handsPanel, rerollsPanel })
                rect.gameObject.SetActive(visible);
            stageLabel.gameObject.SetActive(visible);
            statusLabel.gameObject.SetActive(visible);
            resourcesLabel.gameObject.SetActive(visible);
            CharmsButton.gameObject.SetActive(visible);
            HandRanksButton.gameObject.SetActive(visible);
            menuButton.gameObject.SetActive(visible);
            investigationButton.gameObject.SetActive(visible && journeyControls != null);
            encounterRuleGroup.gameObject.SetActive(visible);
        }

        private void ConfigureTable(EncounterTablePresentationAsset selectedTable)
        {
            if (configuredTable == selectedTable && (selectedTable == null || fox != null || selectedTable.HostPresentation == null)) return;
            configuredTable = selectedTable;
            if (fox != null) { fox.gameObject.SetActive(false); Destroy(fox.gameObject); fox = null; }
            if (selectedTable != null)
            {
                if (selectedTable.HostPresentation != null)
                {
                    fox = selectedTable.HostPresentation.Create(portrait);
                    fox.transform.SetSiblingIndex(tableImage.transform.GetSiblingIndex() + 1);
                    fox.SpiritGesture += () => encounterAudio.Play(SpiritAudioCue.Spirit);
                }
                encounterRulePresentation = selectedTable.RulePresentations.Count > 0 ? selectedTable.RulePresentations[0] : null;
                encounterAudio.SetTheme(selectedTable.AudioTheme);
            }
            ConfigureHostSigils(selectedTable?.HostPresentation);
            Texture2D texture = selectedTable != null ? selectedTable.RoomTexture : roomTexture;
            float edge = selectedTable != null ? selectedTable.RoomTableEdge : RoomTableEdge;
            float split = selectedTable != null ? selectedTable.RoomSplit : .625f;
            roomImage.texture = tableImage.texture = texture;
            roomImage.color = tableImage.color = texture != null ? Color.white : Color.clear;
            roomImage.uvRect = new Rect(0, edge, 1, 1 - edge);
            tableImage.uvRect = new Rect(0, 0, 1, edge);
            roomImage.rectTransform.anchorMin = new Vector2(0, split);
            tableImage.rectTransform.anchorMax = new Vector2(1, split);
            encounterTitle.text = menuTitle.text = selectedTable != null ? selectedTable.DisplayTitle : "";
        }

        private void ReactHost() => fox?.React();

        private void ConfigureHostSigils(SpiritHostPresentationAsset presentation)
        {
            if (encounterRuleSigil != null) { encounterRuleSigil.gameObject.SetActive(false); Destroy(encounterRuleSigil.gameObject); }
            if (ruleSpiritSigil != null) { ruleSpiritSigil.gameObject.SetActive(false); Destroy(ruleSpiritSigil.gameObject); }
            encounterRuleSigil = presentation?.CreateSigil("EncounterRuleSigil", encounterRuleSigilRoot);
            ruleSpiritSigil = presentation?.CreateSigil("RuleSpiritSigil", ruleSpiritSigilRoot);
            if (ruleSpiritSigil != null) ruleSpiritSigil.color = ruleSpiritIcon.color;
            actionModifierCue.ConfigureHost(presentation);
        }

        private void OnDestroy()
        {
            if (journeyControls != null) journeyControls.JourneyChanged -= RefreshJourney;
            if (controls == null) return;
            controls.StateChanged -= Refresh;
            controls.Resolved -= Present;
        }

        private void OnDisable()
        {
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
            if (portrait == null || diceTrayFrame == null || ModifierHistory == null) return;
            Vector2 size = portrait.rect.size;
            if (size == previousPortraitSize) return;
            previousPortraitSize = size;
            float frameHeight = playingPanel.rect.height * .36f;
            float imageHeight = Mathf.Min(frameHeight, size.x * DiceTray.Texture.height / DiceTray.Texture.width);
            float historyHeight = ((RectTransform)ModifierHistory.transform).rect.height;
            // Include the rendered wood margins and the aspect fitter's letterboxing in the visible gap.
            float gap = (frameHeight - imageHeight) * .5f + imageHeight * (13f / 320f) + historyHeight * .20f;
            Vector2 offset = new Vector2(0, -Mathf.Max(0, gap - 5f));
            diceTrayFrame.anchoredPosition = offset;
            targetPanel.anchoredPosition = offset;
            handsPanel.anchoredPosition = offset;
            rerollsPanel.anchoredPosition = offset;
            resourcesLabel.rectTransform.anchoredPosition = offset;
        }

        private void Build()
        {
            encounterAudio = GetComponent<SpiritEncounterAudio>() ?? gameObject.AddComponent<SpiritEncounterAudio>();
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
            encounterTitle = SpiritUi.Heading("EncounterTitle", portrait, "", 21,
                new Vector2(.12f, .963f), new Vector2(.88f, .999f), TextAnchor.MiddleCenter);
            var headerRule = SpiritUi.Rect("HeaderRule", portrait, new Vector2(.16f, .925f), new Vector2(.84f, .995f));
            SpiritUi.Decorate(headerRule);
            stageLabel = SpiritUi.Heading("StageLabel", portrait, "", 16, new Vector2(.15f, .939f), new Vector2(.85f, .963f),
                TextAnchor.MiddleCenter);
            menuButton = SpiritUi.Button("MenuButton", portrait, "Menu", new Vector2(.80f, .932f), new Vector2(.99f, .978f), ToggleMenu);
            var menuText = menuButton.GetComponentInChildren<Text>();
            menuText.fontSize = menuText.resizeTextMaxSize = 15;
            menuText.resizeTextMinSize = 12;
            var target = SpiritUi.Panel("TargetPanel", portrait, new Vector2(.035f, .544f), new Vector2(.965f, .63f),
                SpiritUi.PanelColor, 0, true);
            targetPanel = target.rectTransform;
            targetLabel = SpiritUi.Heading("TargetLabel", target.transform, "", 56, new Vector2(.2f, .29f), new Vector2(.8f, .99f),
                TextAnchor.MiddleCenter);
            targetCaption = SpiritUi.Heading("ToClearLabel", target.transform, "TO CLEAR", 17,
                new Vector2(.23f, .1f), new Vector2(.77f, .35f), TextAnchor.MiddleCenter);
            targetCaption.rectTransform.offsetMin = new Vector2(12, 0);
            targetCaption.rectTransform.offsetMax = new Vector2(-12, 0);
            handsPanel = SpiritUi.Panel("HandsPanel", portrait, new Vector2(.12f, .506f), new Vector2(.495f, .542f),
                SpiritUi.PanelColor, 0, true).rectTransform;
            handsLabel = SpiritUi.Heading("HandsRemainingLabel", handsPanel, "", 20, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter);
            rerollsPanel = SpiritUi.Panel("RerollsPanel", portrait, new Vector2(.505f, .506f), new Vector2(.88f, .542f),
                SpiritUi.PanelColor, 0, true).rectTransform;
            rerollsLabel = SpiritUi.Heading("RerollsRemainingLabel", rerollsPanel, "", 20, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter);
            resourcesLabel = SpiritUi.Label("ResourcesLabel", portrait, "", 18, new Vector2(.04f, .503f), new Vector2(.96f, .54f),
                TextAnchor.MiddleCenter, SpiritUi.Muted);

            BuildPlaying();
            BuildRewards();
            BuildEnding();
            CharmsButton = SpiritUi.Button("CharmsButton", portrait, "CHARMS (0)", new Vector2(.035f, .007f), new Vector2(.49f, .065f), ShowCharms);
            HandRanksButton = SpiritUi.Button("HandRanksButton", portrait, "HAND RANKS", new Vector2(.51f, .007f), new Vector2(.965f, .065f), ShowHandRanks);
            foreach (var button in new[] { CharmsButton, HandRanksButton })
            {
                var text = button.GetComponentInChildren<Text>();
                text.fontSize = text.resizeTextMaxSize = 19;
                text.resizeTextMinSize = 14;
            }
            charmCount = SpiritUi.Label("CharmCount", portrait, "", 15, new Vector2(.35f, .005f), new Vector2(.52f, .065f),
                TextAnchor.MiddleCenter, SpiritUi.Muted);
            charmCount.gameObject.SetActive(false);
            statusLabel = SpiritUi.Label("StatusMessage", portrait, "", 14, new Vector2(.12f, .892f), new Vector2(.88f, .927f),
                TextAnchor.MiddleCenter, SpiritUi.Muted);
            investigationButton = SpiritUi.Button("ReturnToInvestigationButton", portrait, "Clues",
                new Vector2(.01f, .932f), new Vector2(.20f, .978f), () =>
                { if (!Busy) journeyControls?.ReturnToInvestigation(); });
            var investigationText = investigationButton.GetComponentInChildren<Text>();
            investigationText.fontSize = investigationText.resizeTextMaxSize = 15;
            investigationText.resizeTextMinSize = 12;
            investigationButton.gameObject.SetActive(false);
            BuildCharms();
            handRanksPanel = gameObject.AddComponent<SpiritHandRanksPanel>();
            handRanksPanel.Build(portrait, ShowTable);
            BuildMenu();
            JourneyView = SpiritUi.Full("EncounterJourneyOverlay", portrait).gameObject.AddComponent<EncounterJourneyView>();
            JourneyView.Build(ReactHost, encounterAudio);
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
            playingPanel = SpiritUi.Rect("PlayingPanel", portrait, new Vector2(0, .065f), new Vector2(1, .55f));
            var trayFrame = SpiritUi.Rect("DiceTrayFrame", playingPanel, new Vector2(0, .578f), new Vector2(1, .938f));
            diceTrayFrame = trayFrame;
            var trayRect = SpiritUi.Full("DiceTrayImage", trayFrame);
            var trayImage = trayRect.gameObject.AddComponent<RawImage>();
            var aspect = trayRect.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            DiceTray = GetComponent<SpiritDiceTray>() ?? gameObject.AddComponent<SpiritDiceTray>();
            DiceTray.Initialize();
            aspect.aspectRatio = (float)DiceTray.Texture.width / DiceTray.Texture.height;
            trayImage.texture = DiceTray.Texture;
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
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, SpiritUi.Amber);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                diceSelectionLabels.Add(plate.gameObject);
                plate.gameObject.SetActive(false);
            }
            var historySlot = SpiritUi.Rect("ModifierHistorySlot", playingPanel, new Vector2(.06f, .48f), new Vector2(.94f, .578f));
            ModifierHistory = historySlot.gameObject.AddComponent<SpiritModifierHistoryTray>();
            ModifierHistory.Build(historySlot);
            ModifierHistory.TokenSelected += (contribution, definition) =>
            {
                if (Busy) return;
                selectionLabel.color = SpiritUi.Amber;
                selectionLabel.text = CharmName(definition) + "\n" +
                    ContributionDelta(contribution.BaseScore, contribution.Multiplier, contribution.Rerolls) +
                    (contribution.Phase == ModifierPhase.PlayHand ? " (on play)" : " (banked)");
                ShowActivationIcon(definition);
            };
            selectionLabel = SpiritUi.Label("SelectionLabel", playingPanel, "Tap dice to select for reroll", 17,
                new Vector2(.115f, .394f), new Vector2(.97f, .478f), TextAnchor.MiddleCenter, SpiritUi.Muted);
            selectionLabel.resizeTextForBestFit = true;
            selectionLabel.resizeTextMinSize = 14;
            selectionLabel.resizeTextMaxSize = 17;
            selectionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectionLabel.rectTransform.offsetMin = new Vector2(0, 0);
            selectionLabel.rectTransform.offsetMax = new Vector2(0, 0);
            modifierEmblem = SpiritUi.Rect("ModifierEmblem", playingPanel, new Vector2(.045f, .409f), new Vector2(.105f, .464f))
                .gameObject.AddComponent<SpiritUiOrnament>();
            modifierEmblem.kind = "charm";
            modifierEmblem.color = SpiritUi.Amber;
            modifierEmblem.raycastTarget = false;
            modifierActivationIcon = MakeIcon("ModifierActivationIcon", modifierEmblem.transform,
                new Vector2(.5f, .5f), 32);
            modifierEmblem.gameObject.SetActive(false);
            var score = SpiritUi.Panel("ScorePreview", playingPanel, new Vector2(.045f, .19f), new Vector2(.955f, .39f),
                SpiritUi.PanelColor, 0, true);
            categoryLabel = SpiritUi.Heading("HandCategory", score.transform, "", 23,
                new Vector2(.05f, .70f), new Vector2(.95f, .91f), TextAnchor.MiddleCenter);
            SpiritUi.Decorate(score.transform);
            var divider = score.transform.Find("GoldDivider").GetComponent<RectTransform>();
            divider.anchorMin = new Vector2(.07f, .65f);
            divider.anchorMax = new Vector2(.93f, .67f);
            var formula = SpiritUi.Rect("ScoreFormula", score.transform, new Vector2(.04f, .24f), new Vector2(.96f, .70f));
            BaseScoreLabel = BuildScoreCounter("BaseScoreLabel", "BASE", formula, 0, .32f);
            MultiplierLabel = BuildScoreCounter("MultiplierLabel", "MULT", formula, .34f, .66f);
            HandScoreLabel = BuildScoreCounter("HandScore", "TOTAL", formula, .68f, 1);
            baseScoreHighlight = MakeCounterHighlight("BaseScoreCharmIcon", BaseScoreLabel, true);
            multiplierHighlight = MakeCounterHighlight("MultiplierCharmIcon", MultiplierLabel, true);
            rerollsHighlight = MakeCounterHighlight("RerollsCharmIcon", rerollsLabel, false);
            handsHighlight = MakeCounterHighlight("HandsCharmIcon", handsLabel, false);
            scoreDetail = SpiritUi.Label("ScoreBreakdown", score.transform, "ROLL BONUSES BANK HERE", 13,
                new Vector2(.04f, .07f), new Vector2(.96f, .25f), TextAnchor.MiddleCenter, SpiritUi.Muted);
            scoreDetail.rectTransform.offsetMin = scoreDetail.rectTransform.offsetMax = Vector2.zero;
            scoreDetail.resizeTextForBestFit = true;
            scoreDetail.resizeTextMinSize = 11;
            scoreDetail.resizeTextMaxSize = 13;
            SetScoreValues(0, 0);
            HandScoreLabel.text = "0";
            RerollButton = SpiritUi.Button("RerollButton", playingPanel, "Reroll", new Vector2(.035f, 0), new Vector2(.49f, .188f),
                () => { if (!Busy) controls.Reroll(); });
            PlayButton = SpiritUi.Button("PlayButton", playingPanel, "Play Hand", new Vector2(.51f, 0), new Vector2(.965f, .188f),
                () => { if (!Busy) controls.PlayHand(); }, true);
            foreach (var button in new[] { RerollButton, PlayButton })
            {
                var text = button.GetComponentInChildren<Text>();
                text.fontSize = text.resizeTextMaxSize = 25;
                text.resizeTextMinSize = 18;
            }
            actionModifierCue = gameObject.AddComponent<SpiritActionModifierCue>();
            actionModifierCue.Initialize(RerollButton, PlayButton);
            BuildEncounterRule();
        }

        private void BuildEncounterRule()
        {
            rerollActionLabel = RerollButton.GetComponentInChildren<Text>().rectTransform;
            rerollLabelMin = rerollActionLabel.anchorMin;
            rerollLabelMax = rerollActionLabel.anchorMax;
            var favor = SpiritUi.Rect("EncounterRuleFavor", RerollButton.transform,
                new Vector2(.08f, .06f), new Vector2(.94f, .35f));
            encounterRuleGroup = favor.gameObject.AddComponent<CanvasGroup>();
            encounterRuleGroup.interactable = false;
            encounterRuleGroup.blocksRaycasts = false;
            var icon = SpiritUi.Rect("EncounterRuleIcon", favor, new Vector2(0, .5f), new Vector2(0, .5f));
            icon.sizeDelta = new Vector2(18, 18);
            icon.anchoredPosition = new Vector2(9, 0);
            encounterRuleIcon = icon.gameObject.AddComponent<Image>();
            encounterRuleIcon.preserveAspect = true;
            encounterRuleIcon.color = SpiritUi.Amber;
            encounterRuleIcon.raycastTarget = false;
            encounterRuleSigilRoot = icon;
            encounterRuleLabel = SpiritUi.Label("EncounterRuleLabel", favor, "", 14,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, SpiritUi.Amber);
            encounterRuleLabel.rectTransform.offsetMin = new Vector2(21, 0);
            encounterRuleLabel.rectTransform.offsetMax = Vector2.zero;
            encounterRuleLabel.resizeTextForBestFit = true;
            encounterRuleLabel.resizeTextMinSize = 12;
            encounterRuleLabel.resizeTextMaxSize = 14;
            favor.gameObject.SetActive(false);

            var sweep = SpiritUi.Rect("RuleSpiritSweep", diceTrayFrame, new Vector2(0, .5f), new Vector2(0, .5f));
            sweep.sizeDelta = new Vector2(72, 72);
            ruleSpiritSweep = sweep.gameObject.AddComponent<CanvasGroup>();
            ruleSpiritSweep.interactable = false;
            ruleSpiritSweep.blocksRaycasts = false;
            ruleSpiritIcon = SpiritUi.Full("RuleSpiritArtwork", sweep).gameObject.AddComponent<Image>();
            ruleSpiritIcon.preserveAspect = true;
            ruleSpiritIcon.color = new Color(.35f, .9f, .9f, .75f);
            ruleSpiritIcon.raycastTarget = false;
            ruleSpiritSigilRoot = sweep;
            sweep.gameObject.SetActive(false);
            RerollButton.GetComponent<SpiritUiButtonVisual>().CaptureContent();
        }

        private void RenderEncounterRule(EncounterSnapshot snapshot)
        {
            EncounterRuleSnapshot rule = null;
            if (encounterRulePresentation != null && snapshot?.Phase == EncounterPhase.Playing)
                foreach (var candidate in snapshot.EncounterRules)
                    if (candidate.Id == encounterRulePresentation.RuleId) { rule = candidate; break; }
            bool visible = rule != null;
            encounterRuleGroup.gameObject.SetActive(visible);
            rerollActionLabel.anchorMin = visible ? new Vector2(rerollLabelMin.x, .36f) : rerollLabelMin;
            rerollActionLabel.anchorMax = rerollLabelMax;
            if (visible)
            {
                encounterRuleLabel.text = rule.IsAvailable ? encounterRulePresentation.AvailableText : encounterRulePresentation.UsedText;
                encounterRuleGroup.alpha = rule.IsAvailable ? 1 : .4f;
                encounterRuleIcon.sprite = encounterRulePresentation.Icon;
                encounterRuleIcon.enabled = encounterRulePresentation.Icon != null;
                if (encounterRuleSigil != null) encounterRuleSigil.gameObject.SetActive(encounterRulePresentation.Icon == null);
            }
            RerollButton.GetComponent<SpiritUiButtonVisual>().CaptureContent();
        }

        private static Text BuildScoreCounter(string name, string caption, Transform parent, float left, float right)
        {
            var cell = SpiritUi.Rect(name + "Cell", parent, new Vector2(left, 0), new Vector2(right, 1));
            var heading = SpiritUi.Heading(name + "Caption", cell, caption, 14,
                new Vector2(0, .46f), Vector2.one, TextAnchor.MiddleCenter, SpiritUi.Muted);
            heading.rectTransform.offsetMin = heading.rectTransform.offsetMax = new Vector2(0, -4);
            var value = SpiritUi.Heading(name, cell, "0", 30,
                Vector2.zero, new Vector2(1, .50f), TextAnchor.MiddleCenter);
            value.rectTransform.offsetMin = value.rectTransform.offsetMax = new Vector2(0, -1);
            value.resizeTextMinSize = 16;
            return value;
        }

        private static Image MakeIcon(string name, Transform parent, Vector2 anchor, float size)
        {
            var rect = SpiritUi.Rect(name, parent, anchor, anchor);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            return image;
        }

        private static CounterHighlight MakeCounterHighlight(string name, Text label, bool scoreCounter)
        {
            var caption = scoreCounter ? label.transform.parent.Find(label.name + "Caption").GetComponent<Text>() : label;
            var icon = MakeIcon(name, label.transform.parent,
                scoreCounter ? new Vector2(.17f, .7f) : new Vector2(0, .5f), 22);
            if (!scoreCounter) icon.rectTransform.anchoredPosition = new Vector2(19, 0);
            return new CounterHighlight
            {
                Label = label,
                Caption = caption,
                Icon = icon,
                OriginalInset = caption.rectTransform.offsetMin
            };
        }

        private void ShowActivationIcon(ModifierDefinition definition)
        {
            modifierActivationIcon.sprite = SpiritModifierIcons.GetSprite(definition);
            modifierActivationIcon.gameObject.SetActive(modifierActivationIcon.sprite != null);
            modifierEmblem.enabled = modifierActivationIcon.sprite == null;
            modifierEmblem.gameObject.SetActive(true);
        }

        private static void SetCardIcon(Card card, ModifierDefinition definition)
        {
            card.Icon.sprite = definition == null ? null : SpiritModifierIcons.GetSprite(definition);
            bool visible = card.Icon.sprite != null;
            card.Icon.gameObject.SetActive(visible);
            if (definition != null) card.Sigil.Seed = SpiritModifierIcons.GetSigilSeed(definition);
            card.Sigil.gameObject.SetActive(definition != null && !visible);
        }

        private static SpiritModifierSigilGraphic MakeCharmSigil(string name, Image icon)
        {
            var rect = SpiritUi.Rect(name, icon.transform.parent, icon.rectTransform.anchorMin, icon.rectTransform.anchorMax);
            rect.pivot = icon.rectTransform.pivot;
            rect.anchoredPosition = icon.rectTransform.anchoredPosition;
            rect.sizeDelta = icon.rectTransform.sizeDelta;
            var sigil = rect.gameObject.AddComponent<SpiritModifierSigilGraphic>();
            sigil.color = SpiritUi.Amber;
            sigil.raycastTarget = false;
            sigil.gameObject.SetActive(false);
            return sigil;
        }

        private void BuildRewards()
        {
            rewardsPanel = SpiritUi.Panel("RewardsPanel", portrait, new Vector2(.025f, .065f), new Vector2(.975f, .80f),
                new Color32(20, 32, 27, 255), 0, true).rectTransform;
            rewardTitle = SpiritUi.Heading("RewardTitle", rewardsPanel, "STAGE CLEARED", 25, new Vector2(0, .94f), Vector2.one,
                TextAnchor.MiddleCenter, SpiritUi.Amber);
            rewardStep = SpiritUi.Label("RewardStep", rewardsPanel, "", 14, new Vector2(.04f, .885f), new Vector2(.96f, .94f),
                TextAnchor.MiddleCenter, SpiritUi.Amber);
            rewardInstruction = SpiritUi.Label("RewardInstruction", rewardsPanel, "", 17, new Vector2(.04f, .80f), new Vector2(.96f, .885f),
                TextAnchor.MiddleCenter);
            SpiritUi.Scroll("RewardScroll", rewardsPanel, new Vector2(.015f, .15f), new Vector2(.985f, .80f), out rewardContent);
            var layout = rewardContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rewardContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            upcomingStage = SpiritUi.Label("UpcomingStage", rewardsPanel, "", 14, new Vector2(.04f, .08f), new Vector2(.96f, .15f),
                TextAnchor.MiddleCenter, SpiritUi.Muted);
            RefreshButton = SpiritUi.Button("RefreshRewardsButton", rewardsPanel, "Refresh choices", new Vector2(.035f, 0), new Vector2(.965f, .075f),
                () => { if (!Busy) controls.RefreshRewards(); });
            SpiritUi.ActionIcon(RefreshButton.transform, "reroll");
            stageIntroPanel = SpiritUi.Panel("StageIntroPanel", portrait, new Vector2(.06f, .37f), new Vector2(.94f, .57f),
                SpiritUi.PanelColor, 0, true).rectTransform;
            stageIntroTitle = SpiritUi.Heading("StageIntroTitle", stageIntroPanel, "", 28,
                new Vector2(.04f, .55f), new Vector2(.96f, .94f), TextAnchor.MiddleCenter, SpiritUi.Amber);
            stageIntroDetail = SpiritUi.Label("StageIntroDetail", stageIntroPanel, "", 19,
                new Vector2(.04f, .08f), new Vector2(.96f, .55f), TextAnchor.MiddleCenter);
            stageIntroPanel.gameObject.SetActive(false);
        }

        private void BuildEnding()
        {
            endingPanel = SpiritUi.Panel("EndingPanel", portrait, new Vector2(.045f, .075f), new Vector2(.955f, .625f),
                SpiritUi.PanelColor, 0, true).rectTransform;
            endingTitle = SpiritUi.Heading("EndingTitle", endingPanel, "", 42, new Vector2(0, .84f), Vector2.one,
                TextAnchor.MiddleCenter, SpiritUi.Amber);
            var endingRule = SpiritUi.Rect("EndingRule", endingPanel, new Vector2(.1f, .45f), new Vector2(.9f, .48f));
            SpiritUi.Decorate(endingRule);
            endingDetail = SpiritUi.Label("EndingDetail", endingPanel, "", 19, new Vector2(.05f, .49f), new Vector2(.95f, .84f),
                TextAnchor.MiddleCenter);
            SpiritUi.Scroll("EndingUnlockScroll", endingPanel, new Vector2(.05f, .29f), new Vector2(.95f, .44f),
                out endingUnlockContent);
            endingUnlocks = SpiritUi.Label("EndingUnlocks", endingUnlockContent, "", 16, Vector2.zero, Vector2.one,
                TextAnchor.UpperLeft, SpiritUi.Amber);
            endingContinueButton = SpiritUi.Button("EndingNewRunButton", endingPanel, "Return to the table", new Vector2(.1f, .08f), new Vector2(.9f, .27f),
                () => { if (!Busy) { if (journeyControls != null) journeyControls.ReturnToInvestigation(); else controls.StartNew(); } }, true);
        }

        private void BuildCharms()
        {
            charmsPanel = SpiritUi.Panel("CharmsOverlay", portrait, Vector2.zero, Vector2.one,
                new Color32(20, 32, 27, 255), 0, true).rectTransform;
            SpiritUi.Heading("CharmsTitle", charmsPanel, "YOUR CHARMS", 29, new Vector2(.03f, .9f), new Vector2(.73f, .98f));
            SpiritUi.Button("CloseCharmsButton", charmsPanel, "Close", new Vector2(.75f, .9f), new Vector2(.98f, .98f), ShowTable);
            SpiritUi.Label("CharmsSubtitle", charmsPanel, "Kept for this run. Tap a charm to inspect it.", 18,
                new Vector2(.03f, .84f), new Vector2(.97f, .9f), TextAnchor.MiddleLeft, SpiritUi.Muted);
            var scroll = SpiritUi.Scroll("OwnedCharmScroll", charmsPanel, new Vector2(0, .34f), new Vector2(1, .84f), out charmContent);
            var layout = charmContent.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(235, 156);
            layout.spacing = new Vector2(12, 12);
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            charmContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            charmGrid = charmContent.gameObject.AddComponent<SpiritCharmGridLayout>();
            var details = SpiritUi.Panel("CharmDetails", charmsPanel, new Vector2(.02f, .04f), new Vector2(.98f, .33f),
                SpiritUi.PanelColor, 6, true);
            charmTitle = SpiritUi.Heading("CharmDetailTitle", details.transform, "No charms yet", 23, new Vector2(0, .72f), Vector2.one);
            charmDetailIcon = MakeIcon("CharmDetailIcon", details.transform, new Vector2(0, .86f), 44);
            charmDetailIcon.rectTransform.anchoredPosition = new Vector2(42, 0);
            charmDetailSigil = MakeCharmSigil("CharmDetailSigil", charmDetailIcon);
            charmDetail = SpiritUi.Label("CharmDetailBody", details.transform, "Clear a stage to choose a charm.", 17,
                Vector2.zero, new Vector2(1, .72f));
            foreach (var text in new[] { charmTitle, charmDetail })
            {
                text.rectTransform.offsetMin = new Vector2(24, 5);
                text.rectTransform.offsetMax = new Vector2(-24, -5);
            }
            charmDetailStatusRow = SpiritUi.Rect("CharmDetailStatusRow", details.transform, Vector2.zero, Vector2.one);
            charmDetailStatus = SpiritUi.Label("CharmDetailStatus", charmDetailStatusRow, "", 17,
                Vector2.zero, Vector2.one, TextAnchor.MiddleLeft);
            charmDetailStatus.resizeTextForBestFit = false;
            charmDetailBadge = SpiritCharmStatusBadge.Create(charmDetailStatusRow);
            ((RectTransform)charmDetailBadge.transform).anchoredPosition = Vector2.zero;
            charmDetailStatusRow.gameObject.SetActive(false);
            charmDetailsLayout = details.gameObject.AddComponent<SpiritCharmDetailsLayout>();
            charmDetailsLayout.Initialize(charmTitle, charmDetail, charmDetailIcon, (RectTransform)scroll.transform,
                charmDetailStatusRow, charmDetailStatus, charmDetailBadge, charmDetailSigil);
            charmsPanel.gameObject.SetActive(false);
        }

        private void BuildMenu()
        {
            menuPanel = SpiritUi.Panel("MenuOverlay", portrait, Vector2.zero, Vector2.one, new Color(0, 0, 0, .86f)).rectTransform;
            var panel = SpiritUi.Panel("MenuPanel", menuPanel, new Vector2(.12f, .17f), new Vector2(.88f, .83f), SpiritUi.PanelColor, 0, true);
            menuTitle = SpiritUi.Heading("MenuTitle", panel.transform, "", 28, new Vector2(0, .9f), Vector2.one,
                TextAnchor.MiddleCenter, SpiritUi.Amber);
            soundButton = SpiritUi.Button("SoundButton", panel.transform, encounterAudio.Muted ? "Sound: Off" : "Sound: On",
                new Vector2(.2f, .8f), new Vector2(.8f, .895f), () =>
                {
                    encounterAudio.Muted = !encounterAudio.Muted;
                    SpiritUi.SetButtonLabel(soundButton, encounterAudio.Muted ? "Sound: Off" : "Sound: On");
                });
            profileSummary = SpiritUi.Label("ProfileSummary", panel.transform, "", 14, new Vector2(.05f, .67f),
                new Vector2(.95f, .80f), TextAnchor.MiddleCenter, SpiritUi.Amber);
            MenuAction(panel.transform, "SaveRunButton", "Save run", .54f, () => controls.Save());
            MenuAction(panel.transform, "LoadRunButton", "Load run", .41f, () => controls.Load());
            MenuAction(panel.transform, "NewRunButton", "New run", .28f, () => controls.StartNew());
            MenuAction(panel.transform, "AbandonRunButton", "Abandon run", .15f, () => controls.Abandon());
            SpiritUi.Button("CloseMenuButton", panel.transform, "Back to table", new Vector2(.05f, .02f), new Vector2(.95f, .13f), ToggleMenu);
            if (encounterAudio.Theme != null && encounterAudio.Theme.credits != null)
            {
                SpiritUi.Panel("AudioCreditsBacking", menuPanel, new Vector2(.04f, .025f), new Vector2(.96f, .16f),
                    new Color(.025f, .035f, .025f, .97f));
                SpiritUi.Label("AudioCredits", menuPanel, encounterAudio.Theme.credits.text, 16,
                    new Vector2(.05f, .035f), new Vector2(.95f, .155f), TextAnchor.MiddleCenter, SpiritUi.Cream);
            }
            menuPanel.gameObject.SetActive(false);
        }

        private void MenuAction(Transform parent, string name, string title, float y, Action action)
        {
            SpiritUi.Button(name, parent, title, new Vector2(.05f, y), new Vector2(.95f, y + .12f), () =>
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
            menuPanel.gameObject.SetActive(false);
            handRanksPanel.Root.gameObject.SetActive(false);
            RenderCharms();
            charmsPanel.gameObject.SetActive(true);
        }

        public void ShowHandRanks()
        {
            if (Busy) return;
            menuPanel.gameObject.SetActive(false);
            charmsPanel.gameObject.SetActive(false);
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
            charmsPanel.gameObject.SetActive(false);
            handRanksPanel.Root.gameObject.SetActive(false);
        }

        private void ToggleMenu()
        {
            if (Busy) return;
            charmsPanel.gameObject.SetActive(false);
            handRanksPanel.Root.gameObject.SetActive(false);
            menuPanel.gameObject.SetActive(!menuPanel.gameObject.activeSelf);
        }

        private void Refresh()
        {
            if (canvas == null || controls == null) return;
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
            var snapshot = controls.Snapshot;
            if (snapshot == null)
            {
                RenderEncounterRule(null);
                inputGroup.interactable = false;
                ModifierHistory.SetInteractable(false);
                playingPanel.gameObject.SetActive(false);
                rewardsPanel.gameObject.SetActive(false);
                endingPanel.gameObject.SetActive(false);
                statusLabel.text = controls.StatusMessage;
                return;
            }
            if (!ReferenceEquals(snapshot, presentedSnapshot))
            {
                ResetPresentation();
                fox?.ResetPose();
                lastScore = null;
                renderedOffer = -2;
                renderedCharmsRevision = -1;
                presentedSnapshot = snapshot;
            }
            ModifierHistory.Synchronize(snapshot, controls.Catalog);
            bool playing = snapshot.Phase == EncounterPhase.Playing;
            bool completed = snapshot.Phase == EncounterPhase.Completed;
            playingPanel.gameObject.SetActive(playing);
            rewardsPanel.gameObject.SetActive(!playing && !completed);
            endingPanel.gameObject.SetActive(completed);
            stageLabel.text = "STAGE " + (snapshot.StageIndex + 1) + " / " + snapshot.StageCount;
            targetLabel.text = DiagnosticText.Number(completed ? snapshot.TotalScore : snapshot.RemainingThreshold);
            targetCaption.text = playing ? "TO CLEAR" : completed ? "TOTAL SCORE" : "STAGE CLEARED";
            handsPanel.gameObject.SetActive(playing);
            rerollsPanel.gameObject.SetActive(playing);
            resourcesLabel.gameObject.SetActive(!playing);
            SetResources(snapshot.HandsRemaining, snapshot.RerollsRemaining);
            RenderEncounterRule(snapshot);
            resourcesLabel.text = playing ? snapshot.HandsRemaining + " hands left  /  " + snapshot.RerollsRemaining + " rerolls left" :
                lastScore != null ? "Last hand  " + DiagnosticText.Number(lastScore.HandScore) + " points" :
                "Total score  " + DiagnosticText.Number(snapshot.TotalScore);
            charmCount.text = snapshot.Modifiers.Count + " kept";
            SpiritUi.SetButtonLabel(CharmsButton, "CHARMS (" + snapshot.Modifiers.Count + ")");
            statusLabel.text = ProductStatus(controls.StatusMessage);
            RenderProfileSummary();
            if (playing)
            {
                modifierEmblem.gameObject.SetActive(false);
                DiceTray.SetValues(snapshot.CurrentDice);
                SetDiceSelection(controls.SelectedMask);
                var score = controls.Preview?.Score;
                SetScore(score, false);
                SetScoreValues(snapshot.ModifierBaseScore, snapshot.ModifierMultiplier);
                HandScoreLabel.text = "0";
                scoreDetail.text = "ROLL BONUSES BANK HERE";
                PlayButton.interactable = true;
                RerollButton.interactable = controls.SelectedMask != 0 && snapshot.CanReroll;
                int selected = 0;
                for (int i = 0; i < DiceValues.Count; ++i) if ((controls.SelectedMask & (1 << i)) != 0) ++selected;
                selectionLabel.text = selected == 0 ? "Tap dice to select for reroll" : selected + " selected for reroll";
            }
            else if (!completed) RenderRewards(snapshot);
            else
            {
                var journey = journeyControls?.SelectedJourney;
                endingTitle.text = journey == null ? snapshot.Outcome.ToString() :
                    snapshot.Outcome == EncounterOutcome.Won ? journey.VictoryTitle : journey.DefeatTitle;
                string endingText = journey == null ? "" : snapshot.Outcome == EncounterOutcome.Won ? journey.VictoryText :
                    snapshot.Outcome == EncounterOutcome.Abandoned ? journey.AbandonText : journey.DefeatText;
                endingDetail.text = (string.IsNullOrWhiteSpace(endingText) ? "" : endingText + "\n\n") +
                    "Total score  " + DiagnosticText.Number(snapshot.TotalScore) + "\n" +
                    snapshot.TotalHandsPlayed + (snapshot.TotalHandsPlayed == 1 ? " hand played\n" : " hands played\n") +
                    snapshot.TotalRerollsSpent + (snapshot.TotalRerollsSpent == 1 ? " reroll spent" : " rerolls spent");
                endingDetail.resizeTextForBestFit = journey != null;
                endingDetail.resizeTextMinSize = 16;
                endingDetail.resizeTextMaxSize = 19;
                SpiritUi.SetButtonLabel(endingContinueButton, journey == null ? "Return to the table" : journey.ResultContinueLabel);
                endingUnlocks.text = UnlockSummary(snapshot.NewlyUnlockedModifierIds);
                endingUnlockContent.sizeDelta = new Vector2(endingUnlockContent.sizeDelta.x,
                    Mathf.Max(80, endingUnlocks.preferredHeight + 12));
            }
            if (IsCharmsOpen) RenderCharms();
            if (IsHandRanksOpen) RenderHandRanks();
        }

        private void SetScore(ScoreBreakdown score, bool played)
        {
            categoryLabel.text = score == null ? "" : DiagnosticText.Humanize(score.Category.ToString()).ToUpperInvariant();
            if (played) selectionLabel.text = "HAND SCORED";
        }

        private void SetScoreValues(long baseScore, long multiplier)
        {
            displayedBaseScore = baseScore;
            displayedMultiplier = multiplier;
            BaseScoreLabel.text = DiagnosticText.Number(baseScore);
            MultiplierLabel.text = DiagnosticText.Number(multiplier);
        }

        private void ResetPresentation()
        {
            StopAllCoroutines();
            (activeRoll as IDisposable)?.Dispose();
            activeRoll = null;
            presenting = false;
            presentationSnapshot = null;
            fox?.ResetPose();
            ResetTransientVisuals();
        }

        private void ResetTransientVisuals()
        {
            actionModifierCue?.Clear();
            if (ruleSpiritSweep != null) ruleSpiritSweep.gameObject.SetActive(false);
            if (stageIntroPanel != null) stageIntroPanel.gameObject.SetActive(false);
            if (modifierEmblem == null) return;
            modifierEmblem.gameObject.SetActive(false);
            modifierEmblem.enabled = true;
            modifierActivationIcon.sprite = null;
            modifierActivationIcon.gameObject.SetActive(false);
            modifierActivationIcon.rectTransform.localScale = Vector3.one;
            modifierActivationIcon.color = Color.white;
            foreach (var highlight in new[] { baseScoreHighlight, multiplierHighlight, rerollsHighlight, handsHighlight })
                ResetCounterHighlight(highlight);
            selectionLabel.text = "";
            selectionLabel.color = SpiritUi.Muted;
            selectionLabel.rectTransform.localScale = Vector3.one;
            scoreDetail.color = SpiritUi.Muted;
            scoreDetail.rectTransform.localScale = Vector3.one;
            foreach (var label in new[] { BaseScoreLabel, MultiplierLabel })
            {
                label.color = SpiritUi.Cream;
                label.rectTransform.localScale = Vector3.one;
            }
            HandScoreLabel.color = SpiritUi.Cream;
            HandScoreLabel.rectTransform.localScale = Vector3.one;
            targetLabel.color = SpiritUi.Cream;
            targetLabel.rectTransform.localScale = Vector3.one;
        }

        private void SetResources(int hands, int rerolls)
        {
            handsLabel.text = "HANDS  " + hands;
            rerollsLabel.text = "REROLLS  " + rerolls;
            resourcesLabel.text = hands + " hands left  /  " + rerolls + " rerolls left";
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
            presenting = true;
            presentationSnapshot = resolution.Snapshot;
            inputGroup.interactable = false;
            ModifierHistory.SetInteractable(false);
            charmsPanel.gameObject.SetActive(false);
            handRanksPanel.Root.gameObject.SetActive(false);
            menuPanel.gameObject.SetActive(false);
            StartCoroutine(PresentEvents(resolution));
        }

        private IEnumerator PresentEvents(Resolution resolution)
        {
            // The committed snapshot may include a new hand; only event dice are shown until presentation completes.
            foreach (var ruleEvent in resolution.Events)
            {
                if (ruleEvent is EncounterRuleActivatedEvent activatedRule)
                {
                    if (encounterRulePresentation == null || activatedRule.RuleId != encounterRulePresentation.RuleId) continue;
                    RenderEncounterRule(resolution.Snapshot);
                    actionModifierCue.ShowHost(encounterRulePresentation.Icon, ModifierPhase.PlayerReroll,
                        fox != null ? fox.GestureDuration : 1.8f);
                    selectionLabel.text = encounterRulePresentation.DisplayName;
                    selectionLabel.color = SpiritUi.Amber;
                    fox?.React();
                    yield return AnimateRuleSpirit(.65f);
                }
                else if (ruleEvent is DiceRolledEvent rolled)
                {
                    playingPanel.gameObject.SetActive(true);
                    rewardsPanel.gameObject.SetActive(false);
                    endingPanel.gameObject.SetActive(false);
                    if (rolled.Before.HasValue) DiceTray.SetValues(rolled.Before.Value);
                    SetDiceSelection(rolled.SelectedMask);
                    int rollMask = rolled.Kind == RollKind.Initial ? DiceValues.AllMask : rolled.SelectedMask;
                    int diceCount = 0;
                    for (int die = 0; die < DiceValues.Count; die++)
                        if ((rollMask & (1 << die)) != 0) diceCount++;
                    SpiritRollTiming rollTiming = encounterAudio.BeginRoll(diceCount);
                    activeRoll = DiceTray.Roll(rolled.After, rollMask, rollTiming);
                    yield return activeRoll;
                    activeRoll = null;
                    SetScore(controls.Preview?.Score, false);
                }
                else if (ruleEvent is ModifierActivatedEvent activated && activated.Phase != ModifierPhase.PlayHand)
                {
                    encounterAudio.Play(SpiritAudioCue.Score);
                    yield return AnimateContribution(activated.Contribution, activated.StageIndex, activated.HandIndex, resolution, true);
                    yield return new WaitForSecondsRealtime(ModifierStepHold);
                }
                else if (ruleEvent is HandScoredEvent scored)
                {
                    lastScore = scored.Score;
                    SetDiceSelection(0);
                    DiceTray.SetValues(scored.Dice);
                    SetScore(scored.Score, true);
                    HandScoreLabel.text = "0";
                    fox?.React();
                    modifierEmblem.gameObject.SetActive(false);
                    selectionLabel.color = SpiritUi.Muted;
                    selectionLabel.text = "DICE\n+" + DiagnosticText.Number(scored.Score.DiceSum) + " base";
                    scoreDetail.text = "COUNTING DICE";
                    encounterAudio.Play(SpiritAudioCue.Score);
                    yield return AnimateScore(displayedBaseScore, displayedMultiplier,
                        checked(displayedBaseScore + scored.Score.DiceSum), displayedMultiplier, ScoreStepDuration);
                    yield return new WaitForSecondsRealtime(ScoreStepHold);

                    long handBase = checked(scored.Score.CategoryBaseScore + scored.Score.UpgradeBaseScore);
                    long handMultiplier = checked(scored.Score.CategoryMultiplier + scored.Score.UpgradeMultiplier);
                    selectionLabel.text = categoryLabel.text + " / LEVEL " + scored.Score.UpgradeCount + "\n" +
                        ContributionDelta(handBase, handMultiplier, 0);
                    scoreDetail.text = "ADDING HAND CATEGORY";
                    encounterAudio.Play(SpiritAudioCue.Score);
                    yield return AnimateScore(displayedBaseScore, displayedMultiplier,
                        checked(displayedBaseScore + handBase),
                        checked(displayedMultiplier + handMultiplier), ScoreStepDuration);
                    yield return new WaitForSecondsRealtime(ScoreStepHold);

                    // Recorded contributions retain the eligibility and activation order of the engine phase.
                    foreach (var contribution in scored.Score.ModifierContributions)
                    {
                        if (contribution.Phase != ModifierPhase.PlayHand) continue;
                        encounterAudio.Play(SpiritAudioCue.Score);
                        yield return AnimateContribution(contribution, scored.StageIndex, scored.HandIndex, resolution, false);
                        yield return new WaitForSecondsRealtime(ModifierStepHold);
                    }
                    modifierEmblem.gameObject.SetActive(false);
                    selectionLabel.text = "TOTAL";
                    selectionLabel.color = SpiritUi.Muted;
                    scoreDetail.text = DiagnosticText.Number(scored.Score.BaseScore) + " x " +
                        DiagnosticText.Number(scored.Score.Multiplier);
                    encounterAudio.Play(SpiritAudioCue.Score);
                    yield return AnimateTotal(scored.Score.HandScore, ScoreStepDuration);
                    yield return new WaitForSecondsRealtime(.55f);
                }
                else if (ruleEvent is ThresholdChangedEvent threshold)
                {
                    targetCaption.text = "TO CLEAR";
                    yield return AnimateThreshold(threshold.Before, threshold.After, .35f);
                    yield return new WaitForSecondsRealtime(.16f);
                }
                else if (ruleEvent is StageClearedEvent)
                {
                    targetLabel.text = "0";
                    targetCaption.text = "STAGE CLEARED";
                    encounterAudio.Play(SpiritAudioCue.StageClear);
                    fox?.React();
                    yield return new WaitForSecondsRealtime(fox != null ? fox.GestureDuration : 1.8f);
                }
                else if (ruleEvent is StageStartedEvent stage)
                {
                    stageLabel.text = "STAGE " + (stage.StageIndex + 1) + " / " + resolution.Snapshot.StageCount;
                    targetLabel.text = DiagnosticText.Number(stage.Threshold);
                    targetCaption.text = "TO CLEAR";
                    handsPanel.gameObject.SetActive(true);
                    rerollsPanel.gameObject.SetActive(true);
                    resourcesLabel.gameObject.SetActive(false);
                    SetResources(stage.Hands, stage.Rerolls);
                    playingPanel.gameObject.SetActive(false);
                    rewardsPanel.gameObject.SetActive(false);
                    endingPanel.gameObject.SetActive(false);
                    stageIntroTitle.text = stageLabel.text;
                    stageIntroDetail.fontSize = 19;
                    stageIntroDetail.text = "CLEAR " + DiagnosticText.Number(stage.Threshold) + " POINTS\n" +
                        stage.Hands + " hands / " + stage.Rerolls + " rerolls";
                    if (resolution.PreviousRevision >= 0)
                    {
                        stageIntroPanel.gameObject.SetActive(true);
                        yield return new WaitForSecondsRealtime(.9f);
                    }
                    stageIntroPanel.gameObject.SetActive(false);
                }
                else if (ruleEvent is ModifierUnlockedEvent unlocked)
                {
                    var definition = controls.Catalog.GetModifier(unlocked.ModifierId);
                    stageIntroTitle.text = HasPendingProgress ? "CHARM UNLOCKED - SAVE PENDING" : "CHARM UNLOCKED";
                    stageIntroDetail.fontSize = 16;
                    stageIntroDetail.text = CharmName(definition) + "\n" +
                        (HasPendingProgress ? "Unlock save pending / " + AvailabilityScopeText(definition) : AvailabilityText(definition));
                    stageIntroPanel.gameObject.SetActive(true);
                    yield return new WaitForSecondsRealtime(1.1f);
                    stageIntroPanel.gameObject.SetActive(false);
                }
                else if (ruleEvent is HandStartedEvent hand)
                {
                    ModifierHistory.Clear();
                    RenderEncounterRule(resolution.Snapshot);
                    SetResources(hand.HandsRemaining, hand.RerollsRemaining);
                    SetScoreValues(0, 0);
                    HandScoreLabel.text = "0";
                    categoryLabel.text = "";
                    modifierEmblem.gameObject.SetActive(false);
                    selectionLabel.color = SpiritUi.Muted;
                    selectionLabel.text = "NEW HAND";
                    scoreDetail.text = "ROLL BONUSES BANK HERE";
                    if (resolution.PreviousRevision >= 0) yield return new WaitForSecondsRealtime(.16f);
                }
                else if (ruleEvent is ResourceChangedEvent resource)
                {
                    if (IsModifierGrant(resource, resolution)) continue;
                    ShowResource(resource);
                    if (resource.Resource == ResourceKind.Rerolls && resource.Cause != ResourceChangeCause.ModifierEffect)
                        selectionLabel.text = resource.After + " rerolls remaining";
                }
            }
            presentedSnapshot = resolution.Snapshot;
            presenting = false;
            presentationSnapshot = null;
            ResetTransientVisuals();
            controls.CompletePresentation();
            Refresh();
        }

        private static bool IsModifierGrant(ResourceChangedEvent resource, Resolution resolution)
        {
            if (resource.Cause != ResourceChangeCause.ModifierEffect) return false;
            if (IsPlayHandGrant(resource, resolution)) return true;
            foreach (var ruleEvent in resolution.Events)
                if (ruleEvent is ModifierActivatedEvent activated && activated.Phase != ModifierPhase.PlayHand &&
                    activated.StageIndex == resource.StageIndex && activated.HandIndex == resource.HandIndex &&
                    activated.InstanceId == resource.SourceInstanceId) return true;
            return false;
        }

        private static bool IsPlayHandGrant(ResourceChangedEvent resource, Resolution resolution)
        {
            if (resource.Cause != ResourceChangeCause.ModifierEffect) return false;
            foreach (var ruleEvent in resolution.Events)
                if (ruleEvent is ModifierActivatedEvent activated && activated.Phase == ModifierPhase.PlayHand &&
                    activated.StageIndex == resource.StageIndex && activated.HandIndex == resource.HandIndex &&
                    activated.InstanceId == resource.SourceInstanceId) return true;
            return false;
        }

        private void ShowModifierGrants(long instanceId, int stageIndex, int handIndex, Resolution resolution,
            Sprite sprite, List<CounterHighlight> highlights)
        {
            foreach (var ruleEvent in resolution.Events)
                if (ruleEvent is ResourceChangedEvent resource && resource.Cause == ResourceChangeCause.ModifierEffect &&
                    resource.StageIndex == stageIndex && resource.HandIndex == handIndex && resource.SourceInstanceId == instanceId)
                {
                    ShowResource(resource);
                    if (resource.After == resource.Before) continue;
                    if (resource.Resource == ResourceKind.Rerolls) AddCounterHighlight(highlights, rerollsHighlight, sprite);
                    else if (resource.Resource == ResourceKind.Hands) AddCounterHighlight(highlights, handsHighlight, sprite);
                }
        }

        private void ShowResource(ResourceChangedEvent resource)
        {
            if (resource.Resource == ResourceKind.Rerolls) rerollsLabel.text = "REROLLS  " + resource.After;
            else if (resource.Resource == ResourceKind.Hands) handsLabel.text = "HANDS  " + resource.After;
        }

        private void ShowContribution(ModifierContribution contribution, bool banked)
        {
            var definition = controls.Catalog.GetModifier(contribution.DefinitionId);
            ModifierHistory.Add(contribution, definition, true);
            ShowActivationIcon(definition);
            selectionLabel.color = SpiritUi.Amber;
            selectionLabel.text = CharmName(definition) + "\n" +
                ContributionDelta(contribution.BaseScore, contribution.Multiplier, contribution.Rerolls) +
                (banked ? " (banked)" : " (on play)");
            scoreDetail.text = banked ? "ROLL BONUS BANKED" : "ADDING PLAY BONUS";
        }

        private IEnumerator AnimateContribution(ModifierContribution contribution, int stageIndex, int handIndex,
            Resolution resolution, bool banked)
        {
            ShowContribution(contribution, banked);
            var definition = controls.Catalog.GetModifier(contribution.DefinitionId);
            actionModifierCue.Show(definition, ScoreStepDuration + ModifierStepHold);
            var sprite = SpiritModifierIcons.GetSprite(definition);
            var highlights = new List<CounterHighlight>();
            if (contribution.BaseScore != 0) AddCounterHighlight(highlights, baseScoreHighlight, sprite);
            if (contribution.Multiplier != 0) AddCounterHighlight(highlights, multiplierHighlight, sprite);
            ShowModifierGrants(contribution.InstanceId, stageIndex, handIndex, resolution, sprite, highlights);
            yield return AnimateScore(displayedBaseScore, displayedMultiplier,
                checked(displayedBaseScore + contribution.BaseScore), checked(displayedMultiplier + contribution.Multiplier),
                ScoreStepDuration, highlights);
        }

        private static void AddCounterHighlight(List<CounterHighlight> highlights, CounterHighlight highlight, Sprite sprite)
        {
            if (highlights.Contains(highlight)) return;
            highlight.Icon.sprite = sprite;
            highlight.Icon.gameObject.SetActive(sprite != null);
            if (sprite != null) highlight.Caption.rectTransform.offsetMin = highlight.OriginalInset + new Vector2(22, 0);
            highlights.Add(highlight);
        }

        private static void ResetCounterHighlight(CounterHighlight highlight)
        {
            highlight.Label.color = SpiritUi.Cream;
            highlight.Label.rectTransform.localScale = Vector3.one;
            highlight.Caption.rectTransform.offsetMin = highlight.OriginalInset;
            highlight.Icon.gameObject.SetActive(false);
            highlight.Icon.sprite = null;
            highlight.Icon.rectTransform.localScale = Vector3.one;
            highlight.Icon.color = Color.white;
        }

        private static string ContributionDelta(long baseScore, long multiplier, long rerolls)
        {
            var effects = new List<string>();
            if (baseScore != 0) effects.Add("+" + DiagnosticText.Number(baseScore) + " base");
            if (multiplier != 0) effects.Add("+" + DiagnosticText.Number(multiplier) + " mult");
            if (rerolls != 0) effects.Add("+" + rerolls + (rerolls == 1 ? " reroll" : " rerolls"));
            return string.Join("  /  ", effects);
        }

        private IEnumerator AnimateScore(long beforeBase, long beforeMultiplier, long afterBase, long afterMultiplier, float duration,
            List<CounterHighlight> highlights = null)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                SetScoreValues(Interpolate(beforeBase, afterBase, progress), Interpolate(beforeMultiplier, afterMultiplier, progress));
                if (beforeBase != afterBase)
                {
                    BaseScoreLabel.color = SpiritUi.Amber;
                    BaseScoreLabel.rectTransform.localScale = Vector3.one * (1 + .07f * Mathf.Sin(progress * Mathf.PI));
                }
                if (beforeMultiplier != afterMultiplier)
                {
                    MultiplierLabel.color = SpiritUi.Amber;
                    MultiplierLabel.rectTransform.localScale = Vector3.one * (1 + .07f * Mathf.Sin(progress * Mathf.PI));
                }
                if (highlights != null)
                    foreach (var highlight in highlights)
                    {
                        float pulse = Mathf.Sin(progress * Mathf.PI);
                        highlight.Label.color = Color.Lerp(SpiritUi.Cream, SpiritUi.Amber, pulse);
                        highlight.Label.rectTransform.localScale = Vector3.one * (1 + .07f * pulse);
                        highlight.Icon.rectTransform.localScale = Vector3.one * (1 + .1f * pulse);
                    }
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            SetScoreValues(afterBase, afterMultiplier);
            foreach (var label in new[] { BaseScoreLabel, MultiplierLabel })
            {
                label.color = SpiritUi.Cream;
                label.rectTransform.localScale = Vector3.one;
            }
            if (highlights != null)
                foreach (var highlight in highlights) ResetCounterHighlight(highlight);
        }

        private IEnumerator AnimateTotal(long score, float duration)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                HandScoreLabel.text = DiagnosticText.Number(Interpolate(0, score, progress));
                HandScoreLabel.color = SpiritUi.Amber;
                HandScoreLabel.rectTransform.localScale = Vector3.one * (1 + .14f * Mathf.Sin(progress * Mathf.PI));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            HandScoreLabel.text = DiagnosticText.Number(score);
            HandScoreLabel.color = SpiritUi.Cream;
            HandScoreLabel.rectTransform.localScale = Vector3.one;
        }

        private IEnumerator AnimateThreshold(long before, long after, float duration)
        {
            float elapsed = 0;
            targetLabel.text = DiagnosticText.Number(before);
            while (elapsed < duration)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                targetLabel.text = DiagnosticText.Number(Interpolate(before, after, progress));
                targetLabel.color = SpiritUi.Amber;
                targetLabel.rectTransform.localScale = Vector3.one * (1 + .06f * Mathf.Sin(progress * Mathf.PI));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            targetLabel.text = DiagnosticText.Number(after);
            targetLabel.color = SpiritUi.Cream;
            targetLabel.rectTransform.localScale = Vector3.one;
        }

        private static long Interpolate(long before, long after, float progress)
            => (long)((decimal)before + ((decimal)after - before) * (decimal)progress);

        private static IEnumerator Pulse(Text label, float duration, float amount)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                float pulse = Mathf.Sin(elapsed / duration * Mathf.PI);
                label.color = Color.Lerp(SpiritUi.Cream, SpiritUi.Amber, pulse);
                label.rectTransform.localScale = Vector3.one * (1 + amount * pulse);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            label.color = SpiritUi.Cream;
            label.rectTransform.localScale = Vector3.one;
        }

        private static string CharmName(ModifierDefinition definition)
            => SpiritModifierPresentation.GetName(definition);

        private string AvailabilityText(ModifierDefinition definition)
        {
            var availability = definition.Availability;
            var unlocked = new HashSet<string>(controls.Snapshot.UnlockedModifierIds, StringComparer.Ordinal);
            string access = availability.Access == ModifierAccess.PermanentUnlock ?
                (unlocked.Contains(definition.Id) ? "Permanently unlocked" : "Requires permanent unlock") :
                availability.Access == ModifierAccess.EncounterExclusive ? "Encounter exclusive" : "Available from the start";
            return access + " / " + AvailabilityScopeText(definition);
        }

        private static string AvailabilityScopeText(ModifierDefinition definition)
        {
            var availability = definition.Availability;
            string scope = availability.Scope == ModifierAvailabilityScope.Global ? "All encounters" :
                availability.Scope == ModifierAvailabilityScope.Spirit ? "This spirit's encounters" : "This encounter";
            return scope + " / Tier " + availability.PowerTier;
        }

        private string UnlockSummary(IReadOnlyList<string> ids)
        {
            if (ids.Count == 0) return "No new charms unlocked this run.";
            var names = new List<string>();
            foreach (string id in ids) names.Add(CharmName(controls.Catalog.GetModifier(id)));
            return (HasPendingProgress ? "UNLOCKED THIS RUN - SAVE PENDING\n" : "PERMANENTLY UNLOCKED THIS RUN\n") +
                string.Join("\n", names);
        }

        private bool HasPendingProgress => controls is ISpiritProgressionControls progression &&
            progression.ProfileStatusMessage.StartsWith("Progress is pending:", StringComparison.Ordinal);

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
                : "Player profile needs attention.\n" + (HasPendingProgress ? "Progress save is pending." : "Profile could not be loaded.");
        }

        private IEnumerator AnimateRuleSpirit(float duration)
        {
            var rect = (RectTransform)ruleSpiritSweep.transform;
            ruleSpiritIcon.sprite = encounterRulePresentation.Icon;
            ruleSpiritIcon.gameObject.SetActive(encounterRulePresentation.Icon != null);
            if (ruleSpiritSigil != null) ruleSpiritSigil.gameObject.SetActive(encounterRulePresentation.Icon == null);
            ruleSpiritSweep.gameObject.SetActive(true);
            float elapsed = 0;
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                rect.anchoredPosition = new Vector2(Mathf.Lerp(-36, diceTrayFrame.rect.width + 36, progress),
                    Mathf.Sin(progress * Mathf.PI) * 16);
                ruleSpiritSweep.alpha = Mathf.Sin(progress * Mathf.PI);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            ruleSpiritSweep.gameObject.SetActive(false);
        }

        private Card MakeCard(string name, Transform parent, bool owned)
        {
            var image = SpiritUi.Panel(name, parent, Vector2.zero, Vector2.one, SpiritUi.PanelColor, 0, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var card = new Card { Button = button };
            if (!owned) image.gameObject.AddComponent<LayoutElement>().preferredHeight = 155;
            card.Title = SpiritUi.Heading("Title", image.transform, "", owned ? 17 : 18,
                new Vector2(0, .70f), new Vector2(1, .96f), TextAnchor.MiddleLeft, SpiritUi.Cream);
            card.Icon = MakeIcon("CharmIcon", image.transform, new Vector2(0, .83f), 36);
            card.Icon.rectTransform.anchoredPosition = new Vector2(42, 0);
            card.Sigil = MakeCharmSigil("CharmSigil", card.Icon);
            card.Body = SpiritUi.Label("Condition", image.transform, "", 17,
                new Vector2(0, .40f), new Vector2(1, .70f));
            card.Effect = SpiritUi.Label("Effects", image.transform, "", 17, new Vector2(0, .20f), new Vector2(1, .4f),
                TextAnchor.MiddleLeft, SpiritUi.Amber);
            foreach (var text in new[] { card.Title, card.Body, card.Effect })
            {
                text.rectTransform.offsetMin = new Vector2(24, 5);
                text.rectTransform.offsetMax = new Vector2(-24, -5);
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = text.fontSize;
            }
            card.Title.resizeTextForBestFit = true;
            card.Title.resizeTextMinSize = 14;
            card.Title.resizeTextMaxSize = owned ? 17 : 18;
            if (owned)
            {
                card.Body.fontSize = 15;
                card.Effect.fontSize = 16;
                card.Layout = image.gameObject.AddComponent<SpiritCharmCardLayout>();
                card.Layout.Initialize(card.Title, card.Body, card.Effect, card.Icon, card.Sigil);
                card.Badge = SpiritCharmStatusBadge.Create(image.transform);
            }
            button.onClick.AddListener(() =>
            {
                if (Busy) return;
                if (owned) { selectedCharm = card.InstanceId; RenderCharmDetail(); }
                else controls.ChooseReward(card.OfferId, card.OptionId);
            });
            return card;
        }

        private void RenderRewards(EncounterSnapshot snapshot)
        {
            var offer = snapshot.PendingReward;
            rewardTitle.text = "STAGE " + (snapshot.StageIndex + 1) + " CLEARED";
            rewardStep.text = offer == null ? "REWARDS COMPLETE" : offer.Kind == RewardKind.Modifier ?
                "CHARM CHOICE" : "HAND UPGRADE CHOICE";
            rewardInstruction.text = offer == null ? "Preparing the next stage" : offer.Kind == RewardKind.Modifier ?
                "Choose one free charm to keep for this run" : "Choose one hand upgrade\nScroll to see all choices";
            var encounter = controls.Catalog.GetEncounter(snapshot.EncounterId);
            int nextStageIndex = offer?.TargetStageIndex ?? snapshot.StageIndex + 1;
            if (nextStageIndex < encounter.Stages.Count)
            {
                var nextStage = encounter.Stages[nextStageIndex];
                upcomingStage.text = "NEXT STAGE " + (nextStageIndex + 1) + " / " + snapshot.StageCount +
                    " - CLEAR " + DiagnosticText.Number(nextStage.Threshold) + "\n" +
                    nextStage.StartingHands + " hands / " + nextStage.StartingRerolls + " rerolls";
            }
            else upcomingStage.text = "";
            bool canRefresh = offer != null && offer.Kind == RewardKind.Modifier;
            RefreshButton.gameObject.SetActive(canRefresh);
            int cost = encounter.RewardPolicy?.RefreshCost ?? 1;
            RefreshButton.interactable = canRefresh && snapshot.RewardRefreshes >= cost;
            SpiritUi.SetButtonLabel(RefreshButton, "Refresh / cost " + cost + " / balance " + snapshot.RewardRefreshes);
            long offerId = offer?.OfferId ?? -1;
            if (renderedOffer == offerId) return;
            renderedOffer = offerId;
            int count = offer?.Options.Count ?? 0;
            while (rewardCards.Count < count)
                rewardCards.Add(SpiritRewardCard.Create("RewardCard" + (rewardCards.Count + 1), rewardContent,
                    (chosenOfferId, chosenOptionId) => { if (!Busy) controls.ChooseReward(chosenOfferId, chosenOptionId); }));
            for (int i = 0; i < rewardCards.Count; ++i)
            {
                var card = rewardCards[i];
                card.Button.gameObject.SetActive(i < count);
                if (i >= count) continue;
                var option = offer.Options[i];
                if (offer.Kind == RewardKind.Modifier)
                {
                    var definition = controls.Catalog.GetModifier(option.DefinitionId);
                    card.RenderModifier(definition, offer.OfferId, option.OptionId);
                }
                else if (option.Category.HasValue)
                {
                    var hand = controls.Catalog.GetHand(option.Category.Value);
                    int level = snapshot.HandUpgrades[(int)option.Category.Value];
                    card.RenderHandUpgrade(hand, level, offer.OfferId, option.OptionId);
                }
            }
            rewardContent.anchoredPosition = Vector2.zero;
        }

        private void RenderCharms()
        {
            var snapshot = controls.Snapshot;
            if (snapshot == null) return;
            if (renderedCharmsRevision != snapshot.Revision)
            {
                renderedCharmsRevision = snapshot.Revision;
                while (charmCards.Count < snapshot.Modifiers.Count)
                    charmCards.Add(MakeCard("OwnedCharm" + (charmCards.Count + 1), charmContent, true));
                for (int i = 0; i < charmCards.Count; ++i)
                {
                    var card = charmCards[i];
                    card.Button.gameObject.SetActive(i < snapshot.Modifiers.Count);
                    if (i >= snapshot.Modifiers.Count) continue;
                    var instance = snapshot.Modifiers[i];
                    var definition = controls.Catalog.GetModifier(instance.DefinitionId);
                    card.InstanceId = instance.InstanceId;
                    SetCardIcon(card, definition);
                    card.Title.text = CharmName(definition);
                    card.Body.text = SpiritModifierPresentation.GetConditionText(definition);
                    card.Effect.text = SpiritModifierPresentation.GetEffectText(definition);
                    card.Badge.Render(CharmStatus(instance, definition));
                }
                charmGrid.RefreshLayout();
            }
            if (snapshot.Modifiers.Count > 0 && !ContainsCharm(snapshot, selectedCharm)) selectedCharm = snapshot.Modifiers[0].InstanceId;
            RenderCharmDetail();
        }

        private static bool ContainsCharm(EncounterSnapshot snapshot, long instanceId)
        {
            foreach (var modifier in snapshot.Modifiers) if (modifier.InstanceId == instanceId) return true;
            return false;
        }

        private SpiritCharmStatusPresentation CharmStatus(ModifierInstanceSnapshot instance, ModifierDefinition definition)
        {
            ModifierInspection inspection = null;
            foreach (var candidate in controls.ModifierInspections)
                if (candidate.InstanceId == instance.InstanceId) { inspection = candidate; break; }
            return SpiritCharmStatusPresentation.Create(instance, definition, controls.Snapshot.Phase, inspection);
        }

        private void RenderCharmDetail()
        {
            foreach (var instance in controls.Snapshot.Modifiers)
            {
                if (instance.InstanceId != selectedCharm) continue;
                var definition = controls.Catalog.GetModifier(instance.DefinitionId);
                charmDetailIcon.sprite = SpiritModifierIcons.GetSprite(definition);
                charmDetailIcon.gameObject.SetActive(charmDetailIcon.sprite != null);
                charmDetailSigil.Seed = SpiritModifierIcons.GetSigilSeed(definition);
                charmDetailSigil.gameObject.SetActive(charmDetailIcon.sprite == null);
                charmTitle.text = CharmName(definition);
                charmDetail.text = SpiritModifierPresentation.GetConditionText(definition) + "\n" +
                    SpiritModifierPresentation.GetEffectText(definition) + "\n" +
                    SpiritModifierPresentation.GetTimingText(definition) + "\n" +
                    AvailabilityText(definition);
                var status = CharmStatus(instance, definition);
                charmDetailBadge.Render(status);
                charmDetailStatus.text = status.GetDetailText(definition.Phase, controls.Snapshot.Phase);
                charmDetailStatus.color = charmDetailBadge.Icon.color;
                charmDetailStatusRow.gameObject.SetActive(true);
                charmDetailsLayout.RefreshLayout();
                foreach (var card in charmCards)
                    card.Button.GetComponent<Image>().color = card.InstanceId == selectedCharm ? new Color32(255, 232, 179, 255) : Color.white;
                return;
            }
            charmTitle.text = "No charms yet";
            charmDetailIcon.sprite = null;
            charmDetailIcon.gameObject.SetActive(false);
            charmDetailSigil.gameObject.SetActive(false);
            charmDetail.text = "Clear a stage to choose a charm.";
            charmDetailStatusRow.gameObject.SetActive(false);
            charmDetailsLayout.RefreshLayout();
        }
    }
}
