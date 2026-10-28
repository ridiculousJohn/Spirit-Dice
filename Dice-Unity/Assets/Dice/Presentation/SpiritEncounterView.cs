using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Presentation
{
    /** Portrait encounter surface; controls own rules and persistence, while this view presents ordered events. */
    [DisallowMultipleComponent]
    public sealed class SpiritEncounterView : MonoBehaviour, ISpiritScorePlaybackHost, IAmbientLightingTarget, IRuleSceneLights
    {
        [SerializeField] private Texture2D roomTexture;
        [SerializeField] private EncounterTablePresentationAsset tablePresentation;
        [SerializeField] private SpiritUiTheme uiTheme;
        [SerializeField] private SpiritTrayPresentationAsset trayPresentation;

        public SpiritUiTheme UiTheme => uiTheme;
        public SpiritTrayPresentationAsset TrayPresentation => trayPresentation;

        public bool IsCharmsOpen => OwnedCharmsPanel != null && OwnedCharmsPanel.Root.gameObject.activeSelf;
        public bool IsCharmPopupOpen => CharmPopup != null && CharmPopup.IsOpen;
        public long CharmPopupInstanceId => CharmPopup != null ? CharmPopup.InstanceId : -1;
        public bool IsHandRanksOpen => handRanksPanel != null && handRanksPanel.Root.gameObject.activeSelf;
        [field: SerializeField] public SpiritGameplayHud GameplayHud { get; private set; }
        [field: SerializeField] public SpiritRewardsPanel RewardsPanel { get; private set; }
        [field: SerializeField] public SpiritEncounterCharmsIntroduction EncounterCharmsIntroduction { get; private set; }
        [field: SerializeField] public SpiritOwnedCharmsPanel OwnedCharmsPanel { get; private set; }
        [field: SerializeField] public SpiritCharmPopup CharmPopup { get; private set; }
        [field: SerializeField] public SpiritDiceTray DiceTray { get; private set; }
        [field: SerializeField] public SpiritTrayScene TrayScene { get; private set; }
        [field: SerializeField] public SpiritModifierHistoryTray ModifierHistory { get; private set; }
        public Button PlayButton => GameplayHud != null ? GameplayHud.PlayButton : null;
        public Button RerollButton => GameplayHud != null ? GameplayHud.RerollButton : null;
        public Button CharmsButton => GameplayHud != null ? GameplayHud.CharmsButton : null;
        public Button HandRanksButton => GameplayHud != null ? GameplayHud.HandRanksButton : null;
        public Button RefreshButton => RewardsPanel != null ? RewardsPanel.RefreshButton : null;
        public Text BaseScoreLabel => GameplayHud != null ? GameplayHud.BaseScoreLabel : null;
        public Text MultiplierLabel => GameplayHud != null ? GameplayHud.MultiplierLabel : null;
        public Text HandScoreLabel => GameplayHud != null ? GameplayHud.HandScoreLabel : null;
        [field: SerializeField] public EncounterJourneyView JourneyView { get; private set; }
        public SpiritHostPortrait HostPortrait => hostPortrait;

        private ISpiritEncounterControls controls;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform portrait;
        [SerializeField] private RectTransform diceTrayFrame;
        [SerializeField] private RawImage trayImage;
        private Vector2 previousPortraitSize;
        [SerializeField] private RectTransform endingPanel;
        [SerializeField] private RectTransform menuPanel;
        [SerializeField] private SpiritHandRanksPanel handRanksPanel;
        [SerializeField] private SpiritHostPortrait hostPortrait;
        private IEncounterJourneyControls journeyControls;
        private ISpiritMainMenuControls mainMenuControls;
        [SerializeField] private Button mainMenuButton;
        private EncounterTablePresentationAsset configuredTable;
        [SerializeField] private RawImage roomImage;
        [SerializeField] private RawImage tableImage;
        private readonly SceneLightingState sceneLightingState = new SceneLightingState();
        private SceneLightingMaterial sceneLightingMaterial;
        [SerializeField] private Text menuTitle;
        [SerializeField] private SpiritEncounterEndingView endingView;
        [SerializeField] private SpiritCollectionView collectionView;
        [SerializeField] private SpiritEncounterAudio encounterAudio;
        [SerializeField] private SpiritActionModifierCue actionModifierCue;
        private SpiritScorePlayback scorePlayback;
        [SerializeField] private CanvasGroup inputGroup;
        [SerializeField] private List<CanvasGroup> openingGroups = new List<CanvasGroup>();
        [SerializeField] private Text profileSummary;
        [SerializeField] private Button soundButton;
        [SerializeField] private List<Button> diceButtons = new List<Button>();
        [SerializeField] private SpiritPauseMenu pauseMenu;
        [SerializeField] private Vector2 portraitReferenceSize = new Vector2(540, 960);
        [Tooltip("Authored props for the encounter's table pieces; empty when the encounter places none.")]
        [SerializeField] private List<SpiritTablePieceView> tablePieces = new List<SpiritTablePieceView>();
        private readonly Rect[] pieceSlotRects = new Rect[DiceValues.Count];
        private bool pieceSlotsMeasured;
        private float[] pieceLightIntensities = Array.Empty<float>();
        private Vector2[] pieceLightUvOffsets = Array.Empty<Vector2>();
        private Vector3[] pieceLightReceivers = Array.Empty<Vector3>();
        [Tooltip("Scene transforms for the table's ambient prop actors; empty when the encounter authors none.")]
        [SerializeField] private List<EncounterAmbientPropBinding> ambientProps = new List<EncounterAmbientPropBinding>();
        private EncounterAmbientActivity ambientActivity;
        private float[] ambientLightIntensities = Array.Empty<float>();
        // Play-mode testing multipliers set from the encounter screen inspector; NaN leaves a light to the ambient activity.
        private float[] testLightOverrides = Array.Empty<float>();
        private float[] sampledLightIntensities = Array.Empty<float>();
        // Resting multipliers authored on the table; the ambient activity's multipliers scale them.
        private float[] restingLightLevels = Array.Empty<float>();
        // Timed raises of lights that rule presenters ask for.
        private readonly SceneLightFlares lightFlares = new SceneLightFlares();
        // Presenters of the configured table's rules for the encounter they were created for; null when no rule card references one.
        private EncounterRulePresenters rulePresenters;
        // Presenters of the table pieces' interactions for the same encounter; null until an encounter is shown.
        private TablePiecePresenters piecePresenters;
        private string presentersEncounterId;
        // The configured table's rule card flagged as shown on the HUD; null when the table flags none.
        private EncounterRulePresentationAsset hudRuleCard;
        private bool applicationFocused = true;
        private bool applicationPaused;

        public RectTransform Root => (RectTransform)transform;
        public Canvas Canvas => canvas;
        public RectTransform Portrait => portrait;
        public RawImage RoomImage => roomImage;
        public RawImage TableImage => tableImage;
        public EncounterTablePresentationAsset TablePresentation => tablePresentation;
        public IReadOnlyList<SpiritTablePieceView> TablePieces => tablePieces;

        private ScoreBreakdown lastScore;
        private EncounterSnapshot presentedSnapshot;
        private EncounterSnapshot presentationSnapshot;
        private bool presenting;
        private Rect previousSafeArea;
        private Vector2 previousScreenSize;

        public void Bind(ISpiritEncounterControls encounterControls, IEncounterJourneyControls journey)
        {
            Unsubscribe();
            journeyControls = journey ?? throw new ArgumentNullException(nameof(journey));
            Bind(encounterControls);
            JourneyView.Bind(journeyControls, controls);
            Subscribe();
            if (isActiveAndEnabled) RefreshJourney();
        }

        public void Bind(ISpiritEncounterControls encounterControls)
        {
            var nextControls = encounterControls ?? throw new ArgumentNullException(nameof(encounterControls));
            if (scorePlayback == null) Build();
            Unsubscribe();
            bool canceledPresentation = presenting;
            ResetPresentation();
            if (canceledPresentation) controls?.CompletePresentation();
            // Rule and piece presenters are created for definitions of the bound controls' catalog.
            DisposePresenters();
            controls = nextControls;
            RewardsPanel.Bind(controls);
            OwnedCharmsPanel.Bind(controls);
            presentedSnapshot = null;
            hostPortrait?.ResetPose();
            Subscribe();
            if (isActiveAndEnabled) Refresh();
        }

        public void BindJourney(IEncounterJourneyControls journey)
        {
            Unsubscribe();
            journeyControls = journey ?? throw new ArgumentNullException(nameof(journey));
            if (scorePlayback == null) Build();
            JourneyView.Bind(journeyControls, controls);
            Subscribe();
            if (isActiveAndEnabled) RefreshJourney();
        }

        private void RefreshJourney()
        {
            if (!isActiveAndEnabled) return;
            if (!IsSelectedTable()) return;
            ConfigureTable(tablePresentation);
            if (journeyControls.JourneyPhase != EncounterJourneyPhase.Playing)
            {
                bool canceled = presenting;
                ResetPresentation(false);
                if (canceled) controls?.CompletePresentation();
                // Returning to the table resumes the run, which shows a pending charms introduction again.
                EncounterCharmsIntroduction.ResetDismissal();
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
            if (scorePlayback == null) Build();
            mainMenuButton.gameObject.SetActive(menuControls != null);
        }

        private void SetGameplayVisible(bool visible)
        {
            GameplayHud.SetGameplayVisible(visible, journeyControls != null);
            RewardsPanel.Root.gameObject.SetActive(visible);
            if (!visible) EncounterCharmsIntroduction.Hide();
        }

        private void ConfigureTable(EncounterTablePresentationAsset selectedTable)
        {
            if (selectedTable != tablePresentation)
                throw new InvalidOperationException("The selected encounter must use its authored table scene.");
            if (configuredTable == selectedTable) return;
            foreach (var pieceLight in selectedTable.PieceLights)
                if (pieceLight == null || FindTablePiece(pieceLight.PieceId, pieceLight.InteractionId) == null ||
                    selectedTable.SceneLighting != null && (selectedTable.SceneLighting.Lights == null ||
                    pieceLight.LightIndex < 0 || pieceLight.LightIndex >= selectedTable.SceneLighting.Lights.Length))
                    throw new InvalidOperationException("Each table piece light requires an authored piece and scene light.");
            var shownRule = selectedTable.HudRuleCard;
            ClearSceneLighting();
            ambientActivity?.Stop();
            configuredTable = selectedTable;
            hudRuleCard = shownRule;
            ambientLightIntensities = new float[selectedTable.SceneLighting != null && selectedTable.SceneLighting.Lights != null
                ? selectedTable.SceneLighting.Lights.Length : 0];
            for (int i = 0; i < ambientLightIntensities.Length; ++i) ambientLightIntensities[i] = 1f;
            var resting = selectedTable.RestingLightLevels;
            if (resting.Count > ambientLightIntensities.Length)
                throw new InvalidOperationException("Each resting light level requires a scene light.");
            restingLightLevels = new float[ambientLightIntensities.Length];
            for (int i = 0; i < restingLightLevels.Length; ++i)
            {
                restingLightLevels[i] = i < resting.Count ? resting[i] : 1f;
                if (!SceneLightingAsset.Finite(restingLightLevels[i]) || restingLightLevels[i] < 0f)
                    throw new InvalidOperationException("Resting light levels must be finite and nonnegative.");
            }
            lightFlares.Resize(ambientLightIntensities.Length);
            testLightOverrides = new float[ambientLightIntensities.Length];
            ReleaseTestLightOverrides();
            ambientActivity =selectedTable.AmbientActivity != null
                ? new EncounterAmbientActivity(selectedTable.AmbientActivity, this, ambientProps, encounterAudio) : null;
            // The table's rule and piece presenters are created once the encounter it presents is known.
            DisposePresenters();
            encounterAudio.SetTheme(selectedTable.AudioTheme);
            GameplayHud.ConfigureHostSigils(selectedTable.HostPresentation);
            actionModifierCue.ConfigureHost(selectedTable.HostPresentation);
            GameplayHud.EncounterTitle.text = menuTitle.text = selectedTable.DisplayTitle;
            var credits = encounterAudio.Theme?.credits;
            pauseMenu.Credits.text = credits != null ? credits.text : "";
            pauseMenu.CreditsBacking.gameObject.SetActive(credits != null);
            SpiritUi.SetButtonLabel(soundButton, encounterAudio.Muted ? "Sound: Off" : "Sound: On");
        }

        private void ConfigureTrays(SpiritTrayPresentationAsset presentation)
        {
            if (TrayScene.Presentation != presentation)
                throw new InvalidOperationException("The encounter screen must use its authored tray rig.");
            TrayScene.Initialize();
            DiceTray = TrayScene.DiceTray;
            if (DiceTray == null || TrayScene.HistoryRenderer == null)
                throw new InvalidOperationException("The encounter rig requires authored dice and modifier renderers.");
            DiceTray.Configure(presentation, TrayScene);
            DiceTray.Initialize();
            ModifierHistory.BindRenderer(TrayScene.HistoryRenderer);
            ModifierHistory.Configure(presentation, TrayScene);
            ModifierHistory.Build(trayImage.rectTransform);
            trayImage.texture = TrayScene.Texture;
            PositionTrays();
        }

        private void PositionTrays()
        {
            if (TrayScene == null || TrayScene.Presentation == null || DiceTray == null) return;
            for (int i = 0; i < diceButtons.Count; ++i)
            {
                Rect bounds = DiceTray.GetSlotViewportRect(i);
                var hit = (RectTransform)diceButtons[i].transform;
                hit.anchorMin = bounds.min;
                hit.anchorMax = bounds.max;
                hit.offsetMin = hit.offsetMax = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            DisposePresenters();
            ClearSceneLighting();
            if (hostPortrait != null) hostPortrait.SpiritGesture -= PlaySpiritCue;
            if (ModifierHistory != null) ModifierHistory.TokenSelected -= InspectHistoryToken;
            Unsubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearSceneLighting();
            bool canceled = presenting;
            ResetPresentation();
            if (JourneyView != null) JourneyView.gameObject.SetActive(false);
            if (canceled) controls?.CompletePresentation();
        }

        private void OnEnable()
        {
            applicationFocused = Application.isFocused;
            Subscribe();
            if (canvas == null) return;
            if (journeyControls != null) RefreshJourney();
            else Refresh();
        }

        private void Subscribe()
        {
            Unsubscribe();
            if (!isActiveAndEnabled) return;
            if (controls != null)
            {
                controls.StateChanged += Refresh;
                controls.Resolved += Present;
            }
            if (journeyControls != null) journeyControls.JourneyChanged += RefreshJourney;
        }

        private void Unsubscribe()
        {
            if (controls != null)
            {
                controls.StateChanged -= Refresh;
                controls.Resolved -= Present;
            }
            if (journeyControls != null) journeyControls.JourneyChanged -= RefreshJourney;
        }

        private void Update()
        {
            if (canvas != null && (previousSafeArea != Screen.safeArea ||
                previousScreenSize != new Vector2(Screen.width, Screen.height))) FitScreen();
        }

        private void LateUpdate()
        {
            if (scorePlayback == null) return;
            PositionTablePieces();
            // Ambient activity suspends with focus and pause like the encounter's ambient audio.
            if (ambientActivity != null && applicationFocused && !applicationPaused) ambientActivity.Tick(Time.unscaledDeltaTime);
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
            if (configuredTable == null) return;
            var asset = configuredTable.SceneLighting;
            if (asset != null && configuredTable.PieceLights.Count > 0) SamplePieceLights(asset, configuredTable.PieceLights);
            else sceneLightingState.Sample(asset, Time.unscaledTimeAsDouble, AmbientLightIntensities(asset));
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
            roomImage.material = tableImage.material = sceneLightingMaterial?.Material;
            var state = asset != null ? sceneLightingState : null;
            TrayScene?.SetSceneLighting(state);
            DiceTray?.SetSceneLighting(state);
            ModifierHistory?.Renderer?.SetSceneLighting(state);
            hostPortrait?.SetSceneLighting(state, configuredTable != null ? configuredTable.HostLightingPosition : Vector3.zero);
        }

        /** Lights bound to table pieces follow their props; every other light keeps its authored position. */
        private void SamplePieceLights(SceneLightingAsset asset, IReadOnlyList<EncounterTablePieceLight> pieceLights)
        {
            asset.Validate();
            int count = asset.Lights.Length;
            if (pieceLightIntensities.Length != count)
            {
                pieceLightIntensities = new float[count];
                pieceLightUvOffsets = new Vector2[count];
                pieceLightReceivers = new Vector3[count];
            }
            for (int i = 0; i < count; ++i)
            {
                pieceLightIntensities[i] = AmbientLightIntensity(i);
                pieceLightUvOffsets[i] = asset.Lights[i].UvOffset;
                pieceLightReceivers[i] = asset.Lights[i].ReceiverPosition;
            }
            foreach (var pieceLight in pieceLights)
            {
                var piece = FindTablePiece(pieceLight.PieceId, pieceLight.InteractionId);
                if (piece == null || pieceLight.LightIndex < 0 || pieceLight.LightIndex >= count)
                    throw new InvalidOperationException("Each table piece light requires an authored piece and scene light.");
                int index = pieceLight.LightIndex;
                if (!piece.gameObject.activeInHierarchy) { pieceLightIntensities[index] = 0f; continue; }
                Vector2 delta = RoomTextureCoordinate(piece.transform.position) - pieceLight.ReferencePosition;
                pieceLightUvOffsets[index] += delta;
                pieceLightReceivers[index] += pieceLight.ReceiverOffsetX * delta.x + pieceLight.ReceiverOffsetY * delta.y;
                pieceLightIntensities[index] = piece.LightIntensity * AmbientLightIntensity(index);
            }
            sceneLightingState.Sample(asset, Time.unscaledTimeAsDouble, pieceLightIntensities, pieceLightUvOffsets, pieceLightReceivers);
            foreach (var pieceLight in pieceLights)
            {
                SceneLightLayer light = asset.Lights[pieceLight.LightIndex];
                float relativeIntensity = light.Intensity > 0f ? sceneLightingState.GetIntensity(pieceLight.LightIndex) / light.Intensity : 0f;
                FindTablePiece(pieceLight.PieceId, pieceLight.InteractionId)
                    .SetEmission(Mathf.Clamp01(Mathf.Pow(relativeIntensity, light.SourceResponse)));
            }
        }

        /**
         * A held testing override replaces the light's multiplier. Otherwise the ambient activity's multiplier scales the
         * table's resting level, and a rule's flare raises the light to at least its own level while it runs.
         */
        private float AmbientLightIntensity(int index)
        {
            if (index < testLightOverrides.Length && !float.IsNaN(testLightOverrides[index])) return testLightOverrides[index];
            if (index >= ambientLightIntensities.Length) return 1f;
            float level = (index < restingLightLevels.Length ? restingLightLevels[index] : 1f) * ambientLightIntensities[index];
            return index < lightFlares.Count ? Mathf.Max(level, lightFlares.Level(index, Time.unscaledTimeAsDouble)) : level;
        }

        /** Per-light multipliers for a table without piece lights; null when the asset's lights differ from the configured table's. */
        private float[] AmbientLightIntensities(SceneLightingAsset asset)
        {
            if (asset == null || asset.Lights == null || asset.Lights.Length != ambientLightIntensities.Length) return null;
            if (sampledLightIntensities.Length != ambientLightIntensities.Length)
                sampledLightIntensities = new float[ambientLightIntensities.Length];
            for (int i = 0; i < sampledLightIntensities.Length; ++i) sampledLightIntensities[i] = AmbientLightIntensity(i);
            return sampledLightIntensities;
        }

        /** Lights of the configured table that the play-mode testing controls can hold; zero before a table is configured. */
        public int TestLightCount => testLightOverrides.Length;

        /** Whether a testing override holds the light; the multiplier is the held one, or the ambient activity's when none holds it. */
        public bool GetTestLightOverride(int index, out float multiplier)
        {
            if (index < 0 || index >= testLightOverrides.Length) throw new ArgumentOutOfRangeException(nameof(index));
            multiplier = testLightOverrides[index];
            if (!float.IsNaN(multiplier)) return true;
            multiplier = AmbientLightIntensity(index);
            return false;
        }

        /** Holds the light at this multiplier of its authored intensity, in place of the ambient activity's, until released. */
        public void SetTestLightOverride(int index, float multiplier)
        {
            if (index < 0 || index >= testLightOverrides.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (!SceneLightingAsset.Finite(multiplier) || multiplier < 0f) throw new ArgumentOutOfRangeException(nameof(multiplier));
            testLightOverrides[index] = multiplier;
        }

        /** Returns every light to the ambient activity's multiplier. */
        public void ReleaseTestLightOverrides()
        {
            for (int i = 0; i < testLightOverrides.Length; ++i) testLightOverrides[i] = float.NaN;
        }

        private void OnApplicationFocus(bool focus) => applicationFocused = focus;

        private void OnApplicationPause(bool pause) => applicationPaused = pause;

        /** The room and table images show different slices of one room texture; lights use that texture's coordinates. */
        private Vector2 RoomTextureCoordinate(Vector3 world)
        {
            RectTransform front = tableImage.rectTransform;
            RectTransform back = roomImage.rectTransform;
            return SpiritTablePieceLayout.TextureCoordinate(front.InverseTransformPoint(world), front.rect, tableImage.uvRect,
                back.InverseTransformPoint(world), back.rect, roomImage.uvRect);
        }

        private void ClearSceneLighting()
        {
            foreach (var piece in tablePieces)
                if (piece != null) piece.RestoreEmission();
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
            if (uiTheme == null || canvas == null || safeArea == null || portrait == null || inputGroup == null ||
                roomImage == null || tableImage == null || GameplayHud == null || RewardsPanel == null || EncounterCharmsIntroduction == null ||
                OwnedCharmsPanel == null || handRanksPanel == null || JourneyView == null || collectionView == null ||
                CharmPopup == null || endingView == null || ModifierHistory == null || actionModifierCue == null ||
                encounterAudio == null || pauseMenu == null || tablePresentation == null || TrayScene == null || hostPortrait == null)
                throw new InvalidOperationException("The encounter screen requires authored UI, table, host, audio, and rig references.");
            if (diceButtons.Count != DiceValues.Count)
                throw new InvalidOperationException("The encounter screen requires five authored dice targets.");
            foreach (var button in diceButtons)
                if (button == null) throw new InvalidOperationException("An authored dice target is missing.");
            GameplayHud.BuildHeader(portrait, ToggleMenu);
            GameplayHud.BuildPlayingPanel();
            GameplayHud.BuildPlayingHud(Reroll, Play);
            GameplayHud.BuildEncounterRule(GameplayHud.DiceTrayFrame);
            GameplayHud.BuildStageIntro();
            GameplayHud.BuildFooter(ShowCharms, ShowHandRanks, ReturnToInvestigation);
            diceTrayFrame = GameplayHud.DiceTrayFrame;
            trayImage = GameplayHud.DiceTrayImage;
            ConfigureTrays(tablePresentation.TrayPresentation ?? trayPresentation);
            hostPortrait.Build((RectTransform)hostPortrait.transform, tablePresentation.HostPresentation);
            hostPortrait.SpiritGesture -= PlaySpiritCue;
            hostPortrait.SpiritGesture += PlaySpiritCue;
            ModifierHistory.TokenSelected -= InspectHistoryToken;
            ModifierHistory.TokenSelected += InspectHistoryToken;
            UnityAction[] selectDie = { ToggleFirstDie, ToggleSecondDie, ToggleThirdDie, ToggleFourthDie, ToggleFifthDie };
            for (int i = 0; i < diceButtons.Count; ++i)
            {
                diceButtons[i].onClick.RemoveListener(selectDie[i]);
                diceButtons[i].onClick.AddListener(selectDie[i]);
            }
            actionModifierCue.Initialize();
            RewardsPanel.Build(portrait, () => Busy);
            EncounterCharmsIntroduction.Build();
            OwnedCharmsPanel.Build(portrait, ShowTable, () => Busy);
            handRanksPanel.Build(portrait, ShowTable);
            endingView.Build(portrait, ReturnFromEnding, RetryEncounter);
            endingPanel = endingView.Root;
            BuildMenu();
            JourneyView.Build(encounterAudio, (state, progress) => hostPortrait.SetState(state, progress));
            collectionView.Build(portrait, HideCollection);
            CharmPopup.Build(canvas.transform, portrait);
            scorePlayback = new SpiritScorePlayback(this, GameplayHud, DiceTray, ModifierHistory, encounterAudio,
                actionModifierCue, endingView);
            ConfigureTable(tablePresentation);
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
            float scale = Screen.height / portraitReferenceSize.y;
            float width = Mathf.Min(portraitReferenceSize.x, previousSafeArea.width / scale);
            portrait.sizeDelta = new Vector2(width, portrait.sizeDelta.y);
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
                CharmPopup.Show(instanceId, controls.ModifierPresentation, definition,
                    SpiritCharmVisuals.Status(controls, instance, definition),
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
            if (menuPanel == null || menuTitle == null || soundButton == null || profileSummary == null || mainMenuButton == null ||
                pauseMenu.CollectionButton == null || pauseMenu.SaveButton == null || pauseMenu.LoadButton == null ||
                pauseMenu.NewRunButton == null || pauseMenu.AbandonButton == null || pauseMenu.CloseButton == null ||
                pauseMenu.Credits == null || pauseMenu.CreditsBacking == null)
                throw new InvalidOperationException("The encounter pause menu requires authored UI references.");
            BindButton(soundButton, ToggleSound);
            BindButton(pauseMenu.CollectionButton, OpenCollectionFromMenu);
            BindButton(pauseMenu.SaveButton, SaveRun);
            BindButton(pauseMenu.LoadButton, LoadRun);
            BindButton(pauseMenu.NewRunButton, StartRun);
            BindButton(pauseMenu.AbandonButton, AbandonRun);
            BindButton(mainMenuButton, ReturnToMainMenu);
            BindButton(pauseMenu.CloseButton, ToggleMenu);
            mainMenuButton.gameObject.SetActive(mainMenuControls != null);
            menuPanel.gameObject.SetActive(false);
        }

        private static void BindButton(Button button, UnityAction action)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void Reroll() { if (!Busy) controls.Reroll(); }
        private void Play() { if (!Busy) controls.PlayHand(); }
        private void ReturnToInvestigation() { if (!Busy) journeyControls?.ReturnToInvestigation(); }
        private void HideCollection() => collectionView.Hide();
        private void PlaySpiritCue() => encounterAudio.Play(SpiritAudioCue.Spirit);
        private void InspectHistoryToken(ModifierContribution contribution, ModifierDefinition definition)
            => ShowCharmPopup(contribution.InstanceId);
        private void ToggleFirstDie() => ToggleDie(0);
        private void ToggleSecondDie() => ToggleDie(1);
        private void ToggleThirdDie() => ToggleDie(2);
        private void ToggleFourthDie() => ToggleDie(3);
        private void ToggleFifthDie() => ToggleDie(4);
        private void ToggleDie(int index) { if (!Busy) controls.ToggleDie(index); }
        private void ReturnFromEnding()
        {
            if (Busy) return;
            if (journeyControls != null) journeyControls.ReturnToInvestigation();
            else controls.StartNew();
        }
        private void RetryEncounter()
        {
            if (Busy) return;
            if (journeyControls != null) journeyControls.RetryEncounter();
            else controls.StartNew();
        }
        private void ToggleSound()
        {
            encounterAudio.Muted = !encounterAudio.Muted;
            SpiritUi.SetButtonLabel(soundButton, encounterAudio.Muted ? "Sound: Off" : "Sound: On");
        }
        private void OpenCollectionFromMenu() { if (!Busy) { menuPanel.gameObject.SetActive(false); ShowCollection(); } }
        private void SaveRun() { if (!Busy) { menuPanel.gameObject.SetActive(false); controls.Save(); } }
        private void LoadRun() { if (!Busy) { menuPanel.gameObject.SetActive(false); controls.Load(); } }
        private void StartRun() { if (!Busy) { menuPanel.gameObject.SetActive(false); controls.StartNew(); } }
        private void AbandonRun() { if (!Busy) { menuPanel.gameObject.SetActive(false); controls.Abandon(); } }
        private void ReturnToMainMenu() { if (!Busy) mainMenuControls?.ShowMainMenu(); }

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
            handRanksPanel.Render(controls.Catalog, controls.Snapshot);
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

        private bool IsSelectedTable()
        {
            var selected = journeyControls?.SelectedJourney?.TablePresentation;
            if (selected == null) return journeyControls?.JourneyPhase == EncounterJourneyPhase.Playing;
            if (tablePresentation == null) return false;
            if (selected == tablePresentation) return true;
            return selected.Scene != null && selected.Scene.RuntimeKeyIsValid() &&
                selected.Scene.AssetGUID == tablePresentation.Scene?.AssetGUID;
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled || canvas == null || controls == null) return;
            if (journeyControls != null && !IsSelectedTable()) return;
            encounterAudio.enabled = true;
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
                rulePresenters?.Sync(null, false);
                hud.RenderEncounterRule(null, hudRuleCard);
                inputGroup.interactable = false;
                ModifierHistory.SetInteractable(false);
                hud.PlayingPanel.gameObject.SetActive(false);
                RewardsPanel.Root.gameObject.SetActive(false);
                EncounterCharmsIntroduction.Hide();
                endingPanel.gameObject.SetActive(false);
                hud.StatusLabel.text = controls.StatusMessage;
                return;
            }
            bool createdPresenters = CreatePresenters(snapshot.EncounterId);
            if (!ReferenceEquals(snapshot, presentedSnapshot))
            {
                ResetPresentation();
                hostPortrait?.ResetPose();
                lastScore = null;
                RewardsPanel.Invalidate();
                OwnedCharmsPanel.Invalidate();
                presentedSnapshot = snapshot;
                // A snapshot that was not presented, such as a resumed run, shows its rule state at once.
                rulePresenters?.Sync(snapshot, false);
                // It also shows a pending charms introduction again; a presented refresh of the offer does not.
                EncounterCharmsIntroduction.ResetDismissal();
            }
            // New presenters of a presented snapshot show its state at once as well.
            else if (createdPresenters) rulePresenters?.Sync(snapshot, false);
            piecePresenters.Bind(snapshot);
            bool playing = snapshot.Phase == EncounterPhase.Playing;
            if (playing && controls.Preview != null)
                ModifierHistory.SynchronizeReady(controls.Preview.Score.ModifierContributions, controls.Catalog,
                    controls.ModifierPresentation);
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
            hud.RenderEncounterRule(snapshot, hudRuleCard);
            hud.ResourcesLabel.text = playing ? snapshot.HandsRemaining + " hands left  /  " + snapshot.RerollsRemaining + " rerolls left" :
                lastScore != null ? "Last hand  " + DiagnosticText.Number(lastScore.HandScore) + " points" :
                "Total score  " + DiagnosticText.Number(snapshot.TotalScore);
            hud.CharmCount.text = snapshot.Modifiers.Count + " kept";
            SpiritUi.SetButtonLabel(hud.CharmsButton, "CHARMS (" + snapshot.Modifiers.Count + ")");
            hud.StatusLabel.text = ProductStatus(controls.StatusMessage);
            RenderProfileSummary();
            if (playing)
            {
                ambientActivity?.Begin();
                hud.ModifierEmblem.gameObject.SetActive(false);
                DiceTray.SetValues(snapshot.CurrentDice);
                SetDiceSelection(controls.SelectedMask);
                // The hand rank shows only while a played hand is scored.
                hud.CategoryLabel.text = "";
                hud.SetScoreValues(snapshot.ModifierBaseScore, snapshot.ModifierMultiplier);
                hud.HandScoreLabel.text = "0";
                hud.ScoreDetail.text = "";
                hud.PlayButton.interactable = true;
                hud.RerollButton.interactable = controls.SelectedMask != 0 && snapshot.CanReroll;
                // Idle play shows no instruction or selection count; the tray highlight marks the reroll selection.
                hud.SelectionLabel.text = "";
            }
            else if (!completed) RewardsPanel.Render(snapshot);
            EncounterCharmsIntroduction.Refresh(controls.Catalog, controls.ModifierPresentation, snapshot, journeyControls?.SelectedJourney);
            if (completed)
            {
                SetGameplayVisible(false);
                endingPanel.gameObject.SetActive(true);
                endingView.Render(snapshot, journeyControls?.SelectedJourney,
                    SpiritCharmVisuals.UnlockSummary(controls, snapshot.NewlyUnlockedModifierIds));
            }
            if (IsCharmsOpen) OwnedCharmsPanel.Render();
            if (IsHandRanksOpen) RenderHandRanks();
        }

        /** Bay points offered to piece presenters follow the tray's resting dice slots, measured like the authored dice touch targets. */
        private void PositionTablePieces()
        {
            if (tablePieces.Count == 0 || DiceTray == null || trayImage == null) return;
            // Rolling dice would carry a bay's piece with them; resting slot bounds do not depend on the face shown.
            if (!pieceSlotsMeasured || !DiceTray.IsRolling)
            {
                for (int bay = 0; bay < DiceValues.Count; ++bay) pieceSlotRects[bay] = DiceTray.GetSlotViewportRect(bay);
                pieceSlotsMeasured = true;
            }
            piecePresenters?.Layout();
        }

        private SpiritTablePieceView FindTablePiece(string pieceId, string interactionId)
        {
            foreach (var piece in tablePieces)
                if (piece != null && string.Equals(piece.PieceId, pieceId, StringComparison.Ordinal) &&
                    string.Equals(piece.InteractionId, interactionId, StringComparison.Ordinal)) return piece;
            return null;
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
            // Leaving play for the journey lets the ambient activity settle; every other reset ends it at rest.
            if (resetHost) ambientActivity?.Stop();
            else ambientActivity?.Settle();
            lightFlares.Clear();
            ReleaseTestLightOverrides();
            ResetTransientVisuals();
        }

        private void ResetTransientVisuals()
        {
            actionModifierCue?.Clear();
            DiceTray?.SetScoringHighlight(0);
            ModifierHistory?.ClearResolving();
            if (GameplayHud != null && scorePlayback != null) GameplayHud.ResetTransientVisuals();
        }

        private void SetDiceSelection(int mask)
        {
            DiceTray.SetSelection(mask);
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
            if (!isActiveAndEnabled)
            {
                if (resolution.Accepted) controls?.CompletePresentation();
                return;
            }
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
            CreatePresenters(resolution.Snapshot.EncounterId);
            // An opening resolution may already have been refreshed as the installed state; its events build the rule state from nothing.
            if (resolution.PreviousRevision < 0) rulePresenters?.Sync(null, false);
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
            collectionView.Show(controls.Catalog, controls.ModifierPresentation,
                (controls as ISpiritProgressionControls)?.ProfileUnlockedModifierIds ?? Array.Empty<string>(),
                journeyControls?.JourneyCatalog,
                (controls as ISpiritProgressionControls)?.ProfileDiscoveredFamilyIds ?? Array.Empty<string>());
        }

        ISpiritEncounterControls ISpiritScorePlaybackHost.Controls => controls;
        IEncounterJourneyControls ISpiritScorePlaybackHost.Journey => journeyControls;
        SpiritHostPortrait ISpiritScorePlaybackHost.Host => hostPortrait;
        IReadOnlyList<EncounterRulePresentationAsset> ISpiritScorePlaybackHost.RuleCards
            => configuredTable != null ? configuredTable.RulePresentations : Array.Empty<EncounterRulePresentationAsset>();
        EncounterRulePresentationAsset ISpiritScorePlaybackHost.HudRuleCard => hudRuleCard;
        EncounterAmbientActivity ISpiritScorePlaybackHost.Ambient => ambientActivity;
        EncounterRulePresenters ISpiritScorePlaybackHost.RulePresenters => rulePresenters;
        void ISpiritScorePlaybackHost.SetDiceSelection(int mask) => SetDiceSelection(mask);
        void ISpiritScorePlaybackHost.SetOpeningOpacity(float opacity) => SetOpeningOpacity(opacity);
        void ISpiritScorePlaybackHost.RecordScore(ScoreBreakdown score) => lastScore = score;

        int IAmbientLightingTarget.LightCount => ambientLightIntensities.Length;

        float IAmbientLightingTarget.GetLightIntensity(int index)
        {
            if (index < 0 || index >= ambientLightIntensities.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return ambientLightIntensities[index];
        }

        /** Multiplies the authored light intensity for the ambient activity; piece lights multiply it with their piece's level. */
        void IAmbientLightingTarget.SetLightIntensity(int index, float multiplier)
        {
            if (index < 0 || index >= ambientLightIntensities.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (!SceneLightingAsset.Finite(multiplier) || multiplier < 0f) throw new ArgumentOutOfRangeException(nameof(multiplier));
            ambientLightIntensities[index] = multiplier;
        }

        int IRuleSceneLights.LightCount => ambientLightIntensities.Length;

        /** Raises the light above its resting and ambient level for a rule presenter; presentation resets end the flare. */
        void IRuleSceneLights.Flare(int index, float peak, float rise, float hold, float fall)
            => lightFlares.Start(index, peak, rise, hold, fall, Time.unscaledTimeAsDouble);

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
            // The events brought the rule presenters to this state; settling animates anything they did not.
            rulePresenters?.Sync(resolution.Snapshot, true);
            ResetTransientVisuals();
            controls.CompletePresentation();
            Refresh();
        }

        /**
         * Creates the configured table's rule presenters and its piece presenters for the encounter unless they already present
         * it; true when created.
         */
        private bool CreatePresenters(string encounterId)
        {
            if (presentersEncounterId != null && string.Equals(presentersEncounterId, encounterId, StringComparison.Ordinal))
                return false;
            DisposePresenters();
            var encounter = controls.Catalog.GetEncounter(encounterId);
            try
            {
                rulePresenters = EncounterRulePresenters.Create(configuredTable.RulePresentations, encounter,
                    () => new TableRuleContext(this));
                piecePresenters = TablePiecePresenters.Create(tablePieces, encounter, new TablePieceContext(this));
            }
            catch
            {
                DisposePresenters();
                throw;
            }
            presentersEncounterId = encounterId;
            return true;
        }

        private void DisposePresenters()
        {
            rulePresenters?.Dispose();
            rulePresenters = null;
            piecePresenters?.Dispose();
            piecePresenters = null;
            presentersEncounterId = null;
        }

        /** What the encounter screen offers rule presenters: its tray, host, lights, audio, ambient activity, pieces and HUD. */
        private sealed class TableRuleContext : RulePresenterContext
        {
            private readonly SpiritEncounterView view;

            internal TableRuleContext(SpiritEncounterView view) : base(view.DiceTray.Dice) => this.view = view;

            public override IRuleSceneLights Lights
                => view.configuredTable != null && view.configuredTable.SceneLighting != null ? view : null;

            public override void ReactHost()
            {
                if (view.hostPortrait != null) view.hostPortrait.React();
            }

            public override IEnumerator FlipDie(int bay, int face) => view.DiceTray.Flip(bay, face);

            public override IEnumerator RerollDie(int bay, int face)
                => view.DiceTray.RollDie(bay, face, view.encounterAudio.BeginRoll(1));

            public override void PlayCue(AudioClip clip) => view.encounterAudio.PlayCue(clip);

            public override void JoltAmbient() => view.ambientActivity?.Jolt();

            public override bool ShowsPiece(string pieceId, string interactionId)
                => view.FindTablePiece(pieceId, interactionId) != null;

            public override float FlarePiece(string pieceId, string interactionId)
            {
                var piece = view.FindTablePiece(pieceId, interactionId) ?? throw new InvalidOperationException(
                    "The table shows no prop for piece " + pieceId + " (" + interactionId + ").");
                return piece.isActiveAndEnabled ? piece.Flare() : 0f;
            }

            public override void ShowRuleBadge()
                => view.GameplayHud.RenderEncounterRule(view.presentationSnapshot, view.hudRuleCard);

            public override void CueAction(ModifierPhase phase, Sprite icon)
                => view.actionModifierCue.ShowHost(icon, phase, SpiritScorePlayback.GestureDurationOf(view.hostPortrait));

            public override void ShowSelection(string text)
            {
                var label = view.GameplayHud.SelectionLabel;
                label.text = text;
                label.color = SpiritUi.GetTheme(view.GameplayHud.transform).amber;
            }

            public override IEnumerator SweepSpirit(Sprite icon, float duration) => view.GameplayHud.AnimateRuleSpirit(icon, duration);
        }

        /** What the encounter screen offers piece interaction presenters: its controls' piece interactions and the tray's bay slots. */
        private sealed class TablePieceContext : PieceInteractionPresenterContext
        {
            private readonly SpiritEncounterView view;

            internal TablePieceContext(SpiritEncounterView view) => this.view = view;

            /** Pieces are used whenever the game accepts input during a hand and the piece's interaction is available. */
            public override bool CanInteract(string pieceId, string interactionId)
            {
                if (view.Busy || !view.isActiveAndEnabled || view.controls.Snapshot == null ||
                    view.controls.Snapshot.Phase != EncounterPhase.Playing ||
                    view.journeyControls != null && view.journeyControls.JourneyPhase != EncounterJourneyPhase.Playing) return false;
                foreach (var interaction in view.controls.Snapshot.PieceInteractions)
                    if (string.Equals(interaction.PieceId, pieceId, StringComparison.Ordinal) &&
                        string.Equals(interaction.InteractionId, interactionId, StringComparison.Ordinal)) return interaction.IsAvailable;
                return false;
            }

            public override bool Interact(string pieceId, string interactionId, PieceInteractionInput input)
            {
                if (!CanInteract(pieceId, interactionId)) return false;
                view.controls.InteractWithPiece(pieceId, interactionId, input);
                return true;
            }

            public override Vector3 BayPoint(int bay, Vector2 placement)
            {
                RectTransform frame = view.trayImage.rectTransform;
                return frame.TransformPoint(SpiritTablePieceLayout.ViewportPoint(frame.rect, view.pieceSlotRects[bay], placement));
            }
        }
    }
}
