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

        private ISpiritEncounterControls controls;
        private Canvas canvas;
        private RectTransform safeArea;
        private RectTransform portrait;
        private RectTransform playingPanel;
        private RectTransform rewardsPanel;
        private RectTransform endingPanel;
        private RectTransform charmsPanel;
        private RectTransform menuPanel;
        private SpiritHandRanksPanel handRanksPanel;
        private RectTransform rewardContent;
        private RectTransform charmContent;
        private SpiritFoxPortrait fox;
        private SpiritEncounterAudio encounterAudio;
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
        private Text endingTitle;
        private Text endingDetail;
        private Text charmTitle;
        private Text charmDetail;
        private Text charmCount;
        private Text selectionLabel;
        private SpiritUiOrnament modifierEmblem;
        private Button soundButton;
        private Button menuButton;
        private readonly List<Button> diceButtons = new List<Button>();
        private readonly List<GameObject> diceSelectionLabels = new List<GameObject>();
        private readonly List<Card> rewardCards = new List<Card>();
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
            internal Text Status;
            internal long OfferId;
            internal long OptionId;
            internal long InstanceId;
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
            fox.ResetPose();
            controls.StateChanged += Refresh;
            controls.Resolved += Present;
            Refresh();
        }

        private void OnDestroy()
        {
            if (controls == null) return;
            controls.StateChanged -= Refresh;
            controls.Resolved -= Present;
        }

        private void Update()
        {
            if (canvas != null && (previousSafeArea != Screen.safeArea ||
                previousScreenSize != new Vector2(Screen.width, Screen.height))) FitScreen();
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
            var room = SpiritUi.Rect("TeaRoom", portrait, new Vector2(0, .65f), Vector2.one)
                .gameObject.AddComponent<RawImage>();
            room.texture = roomTexture;
            room.uvRect = new Rect(0, RoomTableEdge, 1, 1 - RoomTableEdge);
            room.color = roomTexture != null ? Color.white : Color.clear;
            room.raycastTarget = false;
            inputGroup = portrait.gameObject.AddComponent<CanvasGroup>();

            var host = SpiritUi.Full("HostPortrait", portrait);
            fox = host.gameObject.AddComponent<SpiritFoxPortrait>();
            fox.Build(host, foxBodyTexture, foxHeadTexture, foxTurnedHeadTexture);
            fox.SpiritGesture += () => encounterAudio.Play(SpiritAudioCue.Spirit);
            var table = SpiritUi.Rect("ForegroundTable", portrait, Vector2.zero, new Vector2(1, .65f))
                .gameObject.AddComponent<RawImage>();
            table.texture = roomTexture;
            table.uvRect = new Rect(0, 0, 1, RoomTableEdge);
            table.color = roomTexture != null ? Color.white : Color.clear;
            table.raycastTarget = false;
            SpiritUi.Heading("EncounterTitle", portrait, "THE LAST LANTERN", 21,
                new Vector2(.12f, .963f), new Vector2(.88f, .999f), TextAnchor.MiddleCenter);
            var headerRule = SpiritUi.Rect("HeaderRule", portrait, new Vector2(.16f, .925f), new Vector2(.84f, .995f));
            SpiritUi.Decorate(headerRule);
            stageLabel = SpiritUi.Heading("StageLabel", portrait, "", 16, new Vector2(.15f, .93f), new Vector2(.85f, .958f),
                TextAnchor.MiddleCenter);
            menuButton = SpiritUi.Button("MenuButton", portrait, "Menu", new Vector2(.855f, .932f), new Vector2(.99f, .975f), ToggleMenu);
            var menuText = menuButton.GetComponentInChildren<Text>();
            menuText.fontSize = menuText.resizeTextMaxSize = 15;
            menuText.resizeTextMinSize = 12;
            var target = SpiritUi.Panel("TargetPanel", portrait, new Vector2(.035f, .584f), new Vector2(.965f, .67f),
                SpiritUi.PanelColor, 0, true);
            targetLabel = SpiritUi.Heading("TargetLabel", target.transform, "", 56, new Vector2(.2f, .29f), new Vector2(.8f, .99f),
                TextAnchor.MiddleCenter);
            targetCaption = SpiritUi.Heading("ToClearLabel", target.transform, "TO CLEAR", 17,
                new Vector2(.23f, .1f), new Vector2(.77f, .35f), TextAnchor.MiddleCenter);
            targetCaption.rectTransform.offsetMin = new Vector2(12, 0);
            targetCaption.rectTransform.offsetMax = new Vector2(-12, 0);
            handsPanel = SpiritUi.Panel("HandsPanel", portrait, new Vector2(.12f, .546f), new Vector2(.495f, .582f),
                SpiritUi.PanelColor, 0, true).rectTransform;
            handsLabel = SpiritUi.Heading("HandsRemainingLabel", handsPanel, "", 20, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter);
            rerollsPanel = SpiritUi.Panel("RerollsPanel", portrait, new Vector2(.505f, .546f), new Vector2(.88f, .582f),
                SpiritUi.PanelColor, 0, true).rectTransform;
            rerollsLabel = SpiritUi.Heading("RerollsRemainingLabel", rerollsPanel, "", 20, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter);
            resourcesLabel = SpiritUi.Label("ResourcesLabel", portrait, "", 18, new Vector2(.04f, .543f), new Vector2(.96f, .58f),
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
            BuildCharms();
            handRanksPanel = gameObject.AddComponent<SpiritHandRanksPanel>();
            handRanksPanel.Build(portrait, ShowTable);
            BuildMenu();
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
            if (charmContent != null)
                charmContent.GetComponent<GridLayoutGroup>().cellSize = new Vector2((width - 46) / 2, 128);
        }

        private void BuildPlaying()
        {
            playingPanel = SpiritUi.Rect("PlayingPanel", portrait, new Vector2(0, .065f), new Vector2(1, .55f));
            var trayFrame = SpiritUi.Rect("DiceTrayFrame", playingPanel, new Vector2(0, .66f), new Vector2(1, 1.02f));
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
            var historySlot = SpiritUi.Rect("ModifierHistorySlot", playingPanel, new Vector2(.06f, .525f), new Vector2(.94f, .655f));
            ModifierHistory = historySlot.gameObject.AddComponent<SpiritModifierHistoryTray>();
            ModifierHistory.Build(historySlot);
            ModifierHistory.TokenSelected += (contribution, definition) =>
            {
                if (Busy) return;
                selectionLabel.color = SpiritUi.Amber;
                selectionLabel.text = CharmName(definition) + "\n" +
                    ContributionDelta(contribution.BaseScore, contribution.Multiplier, contribution.Rerolls);
            };
            selectionLabel = SpiritUi.Label("SelectionLabel", playingPanel, "Tap dice to select for reroll", 17,
                new Vector2(.115f, .425f), new Vector2(.97f, .52f), TextAnchor.MiddleCenter, SpiritUi.Muted);
            selectionLabel.resizeTextForBestFit = true;
            selectionLabel.resizeTextMinSize = 14;
            selectionLabel.resizeTextMaxSize = 17;
            selectionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectionLabel.rectTransform.offsetMin = new Vector2(0, 0);
            selectionLabel.rectTransform.offsetMax = new Vector2(0, 0);
            modifierEmblem = SpiritUi.Rect("ModifierEmblem", playingPanel, new Vector2(.045f, .442f), new Vector2(.105f, .497f))
                .gameObject.AddComponent<SpiritUiOrnament>();
            modifierEmblem.kind = "charm";
            modifierEmblem.color = SpiritUi.Amber;
            modifierEmblem.raycastTarget = false;
            modifierEmblem.gameObject.SetActive(false);
            var score = SpiritUi.Panel("ScorePreview", playingPanel, new Vector2(.045f, .19f), new Vector2(.955f, .42f),
                SpiritUi.PanelColor, 0, true);
            categoryLabel = SpiritUi.Heading("HandCategory", score.transform, "", 23,
                new Vector2(.05f, .73f), new Vector2(.95f, .98f), TextAnchor.MiddleCenter);
            SpiritUi.Decorate(score.transform);
            var divider = score.transform.Find("GoldDivider").GetComponent<RectTransform>();
            divider.anchorMin = new Vector2(.07f, .70f);
            divider.anchorMax = new Vector2(.93f, .72f);
            var formula = SpiritUi.Rect("ScoreFormula", score.transform, new Vector2(.04f, .22f), new Vector2(.96f, .70f));
            BaseScoreLabel = BuildScoreCounter("BaseScoreLabel", "BASE", formula, 0, .32f);
            MultiplierLabel = BuildScoreCounter("MultiplierLabel", "MULT", formula, .34f, .66f);
            HandScoreLabel = BuildScoreCounter("HandScore", "TOTAL", formula, .68f, 1);
            scoreDetail = SpiritUi.Label("ScoreBreakdown", score.transform, "ROLL BONUSES BANK HERE", 13,
                new Vector2(.04f, .04f), new Vector2(.96f, .20f), TextAnchor.MiddleCenter, SpiritUi.Muted);
            scoreDetail.rectTransform.offsetMin = scoreDetail.rectTransform.offsetMax = Vector2.zero;
            scoreDetail.resizeTextForBestFit = true;
            scoreDetail.resizeTextMinSize = 11;
            scoreDetail.resizeTextMaxSize = 13;
            SetScoreValues(0, 0);
            HandScoreLabel.text = "0";
            RerollButton = SpiritUi.Button("RerollButton", playingPanel, "REROLL 0 DICE", new Vector2(.035f, .095f), new Vector2(.965f, .188f),
                () => { if (!Busy) controls.Reroll(); });
            SpiritUi.ActionIcon(RerollButton.transform, "reroll");
            PlayButton = SpiritUi.Button("PlayButton", playingPanel, "PLAY HAND", new Vector2(.025f, 0), new Vector2(.975f, .092f),
                () => { if (!Busy) controls.PlayHand(); }, true);
            SpiritUi.ActionIcon(PlayButton.transform, "play");
            foreach (var button in new[] { RerollButton, PlayButton })
            {
                var text = button.GetComponentInChildren<Text>();
                text.fontSize = text.resizeTextMaxSize = 29;
                text.resizeTextMinSize = 18;
            }
        }

        private static Text BuildScoreCounter(string name, string caption, Transform parent, float left, float right)
        {
            var cell = SpiritUi.Rect(name + "Cell", parent, new Vector2(left, 0), new Vector2(right, 1));
            var heading = SpiritUi.Heading(name + "Caption", cell, caption, 14,
                new Vector2(0, .60f), Vector2.one, TextAnchor.MiddleCenter, SpiritUi.Muted);
            heading.rectTransform.offsetMin = heading.rectTransform.offsetMax = Vector2.zero;
            var value = SpiritUi.Heading(name, cell, "0", 30,
                Vector2.zero, new Vector2(1, .60f), TextAnchor.MiddleCenter);
            value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
            value.resizeTextMinSize = 16;
            return value;
        }

        private void BuildRewards()
        {
            rewardsPanel = SpiritUi.Panel("RewardsPanel", portrait, new Vector2(0, .065f), new Vector2(1, .945f),
                new Color32(20, 32, 27, 255), 0, true).rectTransform;
            rewardTitle = SpiritUi.Heading("RewardTitle", rewardsPanel, "STAGE CLEARED", 29, new Vector2(0, .87f), Vector2.one,
                TextAnchor.MiddleCenter, SpiritUi.Amber);
            rewardInstruction = SpiritUi.Label("RewardInstruction", rewardsPanel, "", 20, new Vector2(.02f, .77f), new Vector2(.98f, .87f),
                TextAnchor.MiddleCenter);
            SpiritUi.Scroll("RewardScroll", rewardsPanel, new Vector2(0, .12f), new Vector2(1, .77f), out rewardContent);
            var layout = rewardContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rewardContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RefreshButton = SpiritUi.Button("RefreshRewardsButton", rewardsPanel, "Refresh choices", Vector2.zero, new Vector2(1, .11f),
                () => { if (!Busy) controls.RefreshRewards(); });
            SpiritUi.ActionIcon(RefreshButton.transform, "reroll");
        }

        private void BuildEnding()
        {
            endingPanel = SpiritUi.Panel("EndingPanel", portrait, new Vector2(.045f, .075f), new Vector2(.955f, .535f),
                SpiritUi.PanelColor, 0, true).rectTransform;
            endingTitle = SpiritUi.Heading("EndingTitle", endingPanel, "", 42, new Vector2(0, .67f), Vector2.one,
                TextAnchor.MiddleCenter, SpiritUi.Amber);
            var endingRule = SpiritUi.Rect("EndingRule", endingPanel, new Vector2(.1f, .52f), new Vector2(.9f, .81f));
            SpiritUi.Decorate(endingRule);
            endingDetail = SpiritUi.Label("EndingDetail", endingPanel, "", 23, new Vector2(.05f, .35f), new Vector2(.95f, .67f),
                TextAnchor.MiddleCenter);
            SpiritUi.Button("EndingNewRunButton", endingPanel, "Return to the table", new Vector2(.1f, .08f), new Vector2(.9f, .27f),
                () => { if (!Busy) controls.StartNew(); }, true);
        }

        private void BuildCharms()
        {
            charmsPanel = SpiritUi.Panel("CharmsOverlay", portrait, Vector2.zero, Vector2.one,
                new Color32(20, 32, 27, 255), 0, true).rectTransform;
            SpiritUi.Heading("CharmsTitle", charmsPanel, "YOUR CHARMS", 29, new Vector2(.03f, .9f), new Vector2(.73f, .98f));
            SpiritUi.Button("CloseCharmsButton", charmsPanel, "Close", new Vector2(.75f, .9f), new Vector2(.98f, .98f), ShowTable);
            SpiritUi.Label("CharmsSubtitle", charmsPanel, "Kept for this run. Tap a charm to inspect it.", 18,
                new Vector2(.03f, .84f), new Vector2(.97f, .9f), TextAnchor.MiddleLeft, SpiritUi.Muted);
            SpiritUi.Scroll("OwnedCharmScroll", charmsPanel, new Vector2(0, .34f), new Vector2(1, .84f), out charmContent);
            var layout = charmContent.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(235, 128);
            layout.spacing = new Vector2(12, 12);
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            charmContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var details = SpiritUi.Panel("CharmDetails", charmsPanel, new Vector2(.02f, .04f), new Vector2(.98f, .33f),
                SpiritUi.PanelColor, 6, true);
            charmTitle = SpiritUi.Heading("CharmDetailTitle", details.transform, "No charms yet", 23, new Vector2(0, .72f), Vector2.one);
            charmDetail = SpiritUi.Label("CharmDetailBody", details.transform, "Clear a stage to choose a charm.", 17,
                Vector2.zero, new Vector2(1, .72f));
            foreach (var text in new[] { charmTitle, charmDetail })
            {
                text.rectTransform.offsetMin = new Vector2(24, 5);
                text.rectTransform.offsetMax = new Vector2(-24, -5);
            }
            charmsPanel.gameObject.SetActive(false);
        }

        private void BuildMenu()
        {
            menuPanel = SpiritUi.Panel("MenuOverlay", portrait, Vector2.zero, Vector2.one, new Color(0, 0, 0, .86f)).rectTransform;
            var panel = SpiritUi.Panel("MenuPanel", menuPanel, new Vector2(.12f, .17f), new Vector2(.88f, .83f), SpiritUi.PanelColor, 0, true);
            SpiritUi.Heading("MenuTitle", panel.transform, "THE LAST LANTERN", 28, new Vector2(0, .9f), Vector2.one,
                TextAnchor.MiddleCenter, SpiritUi.Amber);
            soundButton = SpiritUi.Button("SoundButton", panel.transform, encounterAudio.Muted ? "Sound: Off" : "Sound: On",
                new Vector2(.2f, .8f), new Vector2(.8f, .895f), () =>
                {
                    encounterAudio.Muted = !encounterAudio.Muted;
                    SpiritUi.SetButtonLabel(soundButton, encounterAudio.Muted ? "Sound: Off" : "Sound: On");
                });
            MenuAction(panel.transform, "SaveRunButton", "Save run", .65f, () => controls.Save());
            MenuAction(panel.transform, "LoadRunButton", "Load run", .49f, () => controls.Load());
            MenuAction(panel.transform, "NewRunButton", "New run", .33f, () => controls.StartNew());
            MenuAction(panel.transform, "AbandonRunButton", "Abandon run", .17f, () => controls.Abandon());
            SpiritUi.Button("CloseMenuButton", panel.transform, "Back to table", new Vector2(.05f, .02f), new Vector2(.95f, .16f), ToggleMenu);
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
            SpiritUi.Button(name, parent, title, new Vector2(.05f, y), new Vector2(.95f, y + .14f), () =>
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
            if (snapshot == null) return;
            if (!ReferenceEquals(snapshot, presentedSnapshot))
            {
                ResetPresentation();
                fox.ResetPose();
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
            resourcesLabel.text = playing ? snapshot.HandsRemaining + " hands left  /  " + snapshot.RerollsRemaining + " rerolls left" :
                lastScore != null ? "Last hand  " + DiagnosticText.Number(lastScore.HandScore) + " points" :
                "Total score  " + DiagnosticText.Number(snapshot.TotalScore);
            charmCount.text = snapshot.Modifiers.Count + " kept";
            SpiritUi.SetButtonLabel(CharmsButton, "CHARMS (" + snapshot.Modifiers.Count + ")");
            statusLabel.text = ProductStatus(controls.StatusMessage);
            if (playing)
            {
                DiceTray.SetValues(snapshot.CurrentDice);
                SetDiceSelection(controls.SelectedMask);
                var score = controls.Preview?.Score;
                SetScore(score, false);
                SetScoreValues(snapshot.ModifierBaseScore, snapshot.ModifierMultiplier);
                HandScoreLabel.text = "0";
                scoreDetail.text = "ROLL BONUSES BANK HERE";
                PlayButton.interactable = true;
                RerollButton.interactable = controls.SelectedMask != 0 && snapshot.RerollsRemaining > 0;
                int selected = 0;
                for (int i = 0; i < DiceValues.Count; ++i) if ((controls.SelectedMask & (1 << i)) != 0) ++selected;
                selectionLabel.text = selected == 0 ? "Tap dice to select for reroll" : selected + " selected for reroll";
            }
            else if (!completed) RenderRewards(snapshot);
            else
            {
                endingTitle.text = snapshot.Outcome.ToString();
                endingDetail.text = "Total score  " + DiagnosticText.Number(snapshot.TotalScore) + "\n" +
                    snapshot.TotalHandsPlayed + " hands played\n" + snapshot.TotalRerollsSpent + " rerolls spent";
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
            ResetTransientVisuals();
        }

        private void ResetTransientVisuals()
        {
            if (modifierEmblem == null) return;
            modifierEmblem.gameObject.SetActive(false);
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
            int selected = 0;
            for (int index = 0; index < DiceValues.Count; ++index) if ((mask & (1 << index)) != 0) ++selected;
            SpiritUi.SetButtonLabel(RerollButton, "REROLL " + selected + (selected == 1 ? " DIE" : " DICE"));
        }

        private static string ProductStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return "";
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
                if (ruleEvent is DiceRolledEvent rolled)
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
                    ShowContribution(activated.Contribution, true);
                    ShowModifierGrants(activated.InstanceId, activated.StageIndex, activated.HandIndex, resolution);
                    encounterAudio.Play(SpiritAudioCue.Score);
                    yield return AnimateScore(displayedBaseScore, displayedMultiplier,
                        checked(displayedBaseScore + activated.Contribution.BaseScore),
                        checked(displayedMultiplier + activated.Contribution.Multiplier), ScoreStepDuration);
                    yield return new WaitForSecondsRealtime(ModifierStepHold);
                }
                else if (ruleEvent is HandScoredEvent scored)
                {
                    lastScore = scored.Score;
                    SetDiceSelection(0);
                    DiceTray.SetValues(scored.Dice);
                    SetScore(scored.Score, true);
                    HandScoreLabel.text = "0";
                    fox.React();
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
                        ShowContribution(contribution, false);
                        ShowModifierGrants(contribution.InstanceId, scored.StageIndex, scored.HandIndex, resolution);
                        encounterAudio.Play(SpiritAudioCue.Score);
                        yield return AnimateScore(displayedBaseScore, displayedMultiplier,
                            checked(displayedBaseScore + contribution.BaseScore),
                            checked(displayedMultiplier + contribution.Multiplier), ScoreStepDuration);
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
                    yield return new WaitForSecondsRealtime(.4f);
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
                }
                else if (ruleEvent is HandStartedEvent hand)
                {
                    ModifierHistory.Clear();
                    SetResources(hand.HandsRemaining, hand.RerollsRemaining);
                    SetScoreValues(0, 0);
                    HandScoreLabel.text = "0";
                    categoryLabel.text = "";
                    modifierEmblem.gameObject.SetActive(false);
                    selectionLabel.color = SpiritUi.Muted;
                    selectionLabel.text = "NEW HAND";
                    scoreDetail.text = "ROLL BONUSES BANK HERE";
                    yield return new WaitForSecondsRealtime(.16f);
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

        private void ShowModifierGrants(long instanceId, int stageIndex, int handIndex, Resolution resolution)
        {
            foreach (var ruleEvent in resolution.Events)
                if (ruleEvent is ResourceChangedEvent resource && resource.Cause == ResourceChangeCause.ModifierEffect &&
                    resource.StageIndex == stageIndex && resource.HandIndex == handIndex && resource.SourceInstanceId == instanceId)
                    ShowResource(resource);
        }

        private void ShowResource(ResourceChangedEvent resource)
        {
            if (resource.Resource == ResourceKind.Rerolls) rerollsLabel.text = "REROLLS  " + resource.After;
            else if (resource.Resource == ResourceKind.Hands) handsLabel.text = "HANDS  " + resource.After;
        }

        private void ShowContribution(ModifierContribution contribution, bool banked)
        {
            ModifierHistory.Add(contribution, controls.Catalog.GetModifier(contribution.DefinitionId), true);
            modifierEmblem.gameObject.SetActive(true);
            selectionLabel.color = SpiritUi.Amber;
            selectionLabel.text = CharmName(controls.Catalog.GetModifier(contribution.DefinitionId)) + "\n" +
                ContributionDelta(contribution.BaseScore, contribution.Multiplier, contribution.Rerolls) +
                (banked ? " (banked)" : "");
            scoreDetail.text = banked ? "ROLL BONUS BANKED" : "ADDING CHARM";
        }

        private static string ContributionDelta(long baseScore, long multiplier, long rerolls)
        {
            var effects = new List<string>();
            if (baseScore != 0) effects.Add("+" + DiagnosticText.Number(baseScore) + " base");
            if (multiplier != 0) effects.Add("+" + DiagnosticText.Number(multiplier) + " mult");
            if (rerolls != 0) effects.Add("+" + rerolls + (rerolls == 1 ? " reroll" : " rerolls"));
            return string.Join("  /  ", effects);
        }

        private IEnumerator AnimateScore(long beforeBase, long beforeMultiplier, long afterBase, long afterMultiplier, float duration)
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
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            SetScoreValues(afterBase, afterMultiplier);
            foreach (var label in new[] { BaseScoreLabel, MultiplierLabel })
            {
                label.color = SpiritUi.Cream;
                label.rectTransform.localScale = Vector3.one;
            }
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
        {
            string typeName = definition.Condition.GetType().Name;
            if (typeName.EndsWith("Condition", StringComparison.Ordinal)) typeName = typeName.Substring(0, typeName.Length - 9);
            return DiagnosticText.Humanize(typeName);
        }

        private Card MakeCard(string name, Transform parent, bool owned)
        {
            var image = SpiritUi.Panel(name, parent, Vector2.zero, Vector2.one, SpiritUi.PanelColor, 0, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var card = new Card { Button = button };
            if (!owned) image.gameObject.AddComponent<LayoutElement>().preferredHeight = 155;
            card.Title = SpiritUi.Heading("Title", image.transform, "", owned ? 17 : 18,
                new Vector2(0, owned ? .57f : .69f), new Vector2(1, .94f), TextAnchor.MiddleLeft, SpiritUi.Cream);
            card.Body = SpiritUi.Label("Condition", image.transform, "", 17,
                new Vector2(0, owned ? .22f : .36f), new Vector2(1, owned ? .59f : .69f));
            card.Effect = SpiritUi.Label("Effects", image.transform, "", 17, new Vector2(0, .16f), new Vector2(1, .4f),
                TextAnchor.MiddleLeft, SpiritUi.Amber);
            card.Status = SpiritUi.Label("Status", image.transform, "", 14, Vector2.zero, new Vector2(1, owned ? .26f : .22f),
                TextAnchor.MiddleRight, SpiritUi.Muted);
            foreach (var text in new[] { card.Title, card.Body, card.Effect, card.Status })
            {
                text.rectTransform.offsetMin = new Vector2(24, 5);
                text.rectTransform.offsetMax = new Vector2(-24, -5);
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = text.fontSize;
            }
            card.Status.rectTransform.offsetMin = new Vector2(24, 10);
            card.Title.resizeTextForBestFit = true;
            card.Title.resizeTextMinSize = 14;
            card.Title.resizeTextMaxSize = owned ? 17 : 18;
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
            rewardTitle.text = "STAGE CLEARED" + (lastScore != null ?
                "\nLast hand  " + DiagnosticText.Number(lastScore.HandScore) + " points" : "");
            rewardInstruction.text = offer == null ? "Preparing the next stage" : offer.Kind == RewardKind.Modifier ?
                "Choose one free charm to keep for this run" : "Now choose a hand upgrade";
            bool canRefresh = offer != null && offer.Kind == RewardKind.Modifier;
            RefreshButton.gameObject.SetActive(canRefresh);
            int cost = controls.Catalog.GetEncounter(snapshot.EncounterId).RewardPolicy?.RefreshCost ?? 1;
            RefreshButton.interactable = canRefresh && snapshot.RewardRefreshes >= cost;
            SpiritUi.SetButtonLabel(RefreshButton, "Refresh choices  /  " + snapshot.RewardRefreshes + " remaining");
            long offerId = offer?.OfferId ?? -1;
            if (renderedOffer == offerId) return;
            renderedOffer = offerId;
            int count = offer?.Options.Count ?? 0;
            while (rewardCards.Count < count) rewardCards.Add(MakeCard("RewardCard" + (rewardCards.Count + 1), rewardContent, false));
            for (int i = 0; i < rewardCards.Count; ++i)
            {
                var card = rewardCards[i];
                card.Button.gameObject.SetActive(i < count);
                if (i >= count) continue;
                var option = offer.Options[i];
                card.OfferId = offer.OfferId;
                card.OptionId = option.OptionId;
                if (offer.Kind == RewardKind.Modifier)
                {
                    var definition = controls.Catalog.GetModifier(option.DefinitionId);
                    card.Title.text = CharmName(definition);
                    card.Body.text = DiagnosticText.DescribeCondition(definition.Condition);
                    card.Effect.text = DiagnosticText.DescribeEffects(definition);
                    card.Status.text = "FREE  /  KEEP FOR THIS RUN";
                }
                else if (option.Category.HasValue)
                {
                    var hand = controls.Catalog.GetHand(option.Category.Value);
                    int level = snapshot.HandUpgrades[(int)option.Category.Value];
                    card.Title.text = DiagnosticText.Humanize(option.Category.Value.ToString());
                    card.Body.text = "Upgrade level " + level + " to " + (level + 1) + "\nApplies whenever you play this hand.";
                    card.Effect.text = "+" + hand.BaseScorePerUpgrade + " base score, +" + hand.MultiplierPerUpgrade + " multiplier";
                    card.Status.text = "FREE HAND UPGRADE";
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
                    card.Title.text = CharmName(definition);
                    card.Body.text = DiagnosticText.DescribeEffects(definition);
                    card.Effect.text = "";
                    card.Status.text = CharmStatus(instance);
                }
            }
            if (snapshot.Modifiers.Count > 0 && !ContainsCharm(snapshot, selectedCharm)) selectedCharm = snapshot.Modifiers[0].InstanceId;
            RenderCharmDetail();
        }

        private static bool ContainsCharm(EncounterSnapshot snapshot, long instanceId)
        {
            foreach (var modifier in snapshot.Modifiers) if (modifier.InstanceId == instanceId) return true;
            return false;
        }

        private string CharmStatus(ModifierInstanceSnapshot instance)
        {
            if (controls.Snapshot.Phase != EncounterPhase.Playing && instance.ActivationsThisHand > 0)
                return "Activated on last hand";
            foreach (var inspection in controls.ModifierInspections)
                if (inspection.InstanceId == instance.InstanceId) return DiagnosticText.DescribeInspectionStatus(inspection.Status);
            return "Kept for this run";
        }

        private void RenderCharmDetail()
        {
            foreach (var instance in controls.Snapshot.Modifiers)
            {
                if (instance.InstanceId != selectedCharm) continue;
                var definition = controls.Catalog.GetModifier(instance.DefinitionId);
                charmTitle.text = CharmName(definition);
                charmDetail.text = DiagnosticText.DescribeCondition(definition.Condition) + "\n" +
                    DiagnosticText.DescribeEffects(definition) + "\n" + DiagnosticText.Humanize(definition.Phase.ToString()) +
                    " / " + DiagnosticText.Humanize(definition.ActivationLimit.ToString()) + "\n" + CharmStatus(instance);
                foreach (var card in charmCards)
                    card.Button.GetComponent<Image>().color = card.InstanceId == selectedCharm ? new Color32(255, 232, 179, 255) : Color.white;
                return;
            }
            charmTitle.text = "No charms yet";
            charmDetail.text = "Clear a stage to choose a charm.";
        }
    }
}
