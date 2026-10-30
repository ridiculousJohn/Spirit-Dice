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
    public sealed class SpiritEncounterView : MonoBehaviour, ISpiritScorePlaybackHost
    {
        [SerializeField] private EncounterTablePresentationAsset tablePresentation;

        public bool IsCharmsOpen => OwnedCharmsPanel != null && OwnedCharmsPanel.Root.gameObject.activeSelf;
        public bool IsCharmPopupOpen => CharmPopup.IsOpen;
        public bool IsHandRanksOpen => handRanksPanel != null && handRanksPanel.Root.gameObject.activeSelf;
        [field: SerializeField] public SpiritGameplayHud GameplayHud { get; private set; }
        [field: SerializeField] public SpiritRewardsPanel RewardsPanel { get; private set; }
        [field: SerializeField] public SpiritEncounterCharmsIntroduction EncounterCharmsIntroduction { get; private set; }
        [field: SerializeField] public SpiritOwnedCharmsPanel OwnedCharmsPanel { get; private set; }
        [field: SerializeField] public SpiritCharmPopup CharmPopup { get; private set; }
        [field: SerializeField] public SpiritDiceTray DiceTray { get; private set; }
        [field: SerializeField] public SpiritTrayScene TrayScene { get; private set; }
        [field: SerializeField] public SpiritModifierHistoryTray ModifierHistory { get; private set; }
        [field: SerializeField] public EncounterJourneyView JourneyView { get; private set; }
        public SpiritHostPortrait HostPortrait => hostPortrait;

        private ISpiritEncounterControls controls;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform portrait;
        [SerializeField] private RectTransform endingPanel;
        [SerializeField] private RectTransform menuPanel;
        [SerializeField] private SpiritHandRanksPanel handRanksPanel;
        [SerializeField] private SpiritHostPortrait hostPortrait;
        private IEncounterJourneyControls journeyControls;
        private ISpiritMainMenuControls mainMenuControls;
        private EncounterTablePresentationAsset configuredTable;
        [SerializeField] private RawImage roomImage;
        [SerializeField] private RawImage tableImage;
        // Scene lighting of the configured table; null until a table is configured.
        private EncounterTableLighting tableLighting;
        [SerializeField] private SpiritEncounterEndingView endingView;
        [SerializeField] private SpiritCollectionView collectionView;
        [SerializeField] private SpiritEncounterAudio encounterAudio;
        [SerializeField] private SpiritActionModifierCue actionModifierCue;
        private SpiritScorePlayback scorePlayback;
        [SerializeField] private CanvasGroup inputGroup;
        [SerializeField] private List<CanvasGroup> openingGroups = new List<CanvasGroup>();
        [SerializeField] private List<Button> diceButtons = new List<Button>();
        [SerializeField] private SpiritPauseMenu pauseMenu;
        [SerializeField] private Vector2 portraitReferenceSize = SpiritPortraitSafeArea.DefaultReferenceSize;
        [Tooltip("Authored props for the encounter's table pieces; empty when the encounter places none.")]
        [SerializeField] private List<SpiritTablePieceView> tablePieces = new List<SpiritTablePieceView>();
        private readonly Rect[] pieceSlotRects = new Rect[DiceValues.Count];
        private bool pieceSlotsMeasured;
        [Tooltip("Scene transforms for the table's ambient prop actors; empty when the encounter authors none.")]
        [SerializeField] private List<EncounterAmbientPropBinding> ambientProps = new List<EncounterAmbientPropBinding>();
        private EncounterAmbientActivity ambientActivity;
        [Tooltip("Scene figures that play an authored clip for the encounter's outcome; empty when the table scene authors none.")]
        [SerializeField] private List<EncounterOutcomeAnimation> outcomeAnimations = new List<EncounterOutcomeAnimation>();
        // Presenters of the configured table's rules for the encounter they were created for; null when no rule card references one.
        private EncounterRulePresenters rulePresenters;
        // Presenters of the table pieces' interactions for the same encounter; null until an encounter is shown.
        private TablePiecePresenters piecePresenters;
        private EncounterDefinition.Id? presentersEncounterId;
        // The configured table's rule card flagged as shown on the HUD; null when the table flags none.
        private EncounterRulePresentationAsset hudRuleCard;
        private bool applicationFocused = true;
        private bool applicationPaused;
        private UnityAction[] dieToggles;

        public RectTransform Root => (RectTransform)transform;
        public Canvas Canvas => canvas;
        public RectTransform Portrait => portrait;
        public RawImage RoomImage => roomImage;
        public RawImage TableImage => tableImage;
        public EncounterTablePresentationAsset TablePresentation => tablePresentation;
        public IReadOnlyList<SpiritTablePieceView> TablePieces => tablePieces;
        /** Scene figures told the encounter's outcome as it is presented. */
        public IReadOnlyList<EncounterOutcomeAnimation> OutcomeAnimations => outcomeAnimations;
        /** Scene lighting of the configured table, which the play-mode testing controls address; null before a table is configured. */
        public EncounterTableLighting TableLighting => tableLighting;

        private ScoreBreakdown lastScore;
        private EncounterSnapshot presentedSnapshot;
        private EncounterSnapshot presentationSnapshot;
        private bool presenting;

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
            hostPortrait.ResetPose();
            Subscribe();
            if (isActiveAndEnabled) Refresh();
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
                CloseOverlays();
                SetGameplayVisible(false);
                // The journey screen shows or hides the host as the scene of each beat presents itself.
            }
            else
            {
                hostPortrait.SetVisible(true);
                hostPortrait.SetState(SpiritPresentationState.Idle, 1);
                SetGameplayVisible(true);
            }
            JourneyView.Refresh();
            Refresh();
        }

        public void BindMainMenu(ISpiritMainMenuControls menuControls)
        {
            mainMenuControls = menuControls;
            if (scorePlayback == null) Build();
            pauseMenu.MainMenuButton.gameObject.SetActive(menuControls != null);
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
            // The previous table's lighting comes off the shared images and receivers before the new one binds to them.
            tableLighting?.Release();
            var lighting = new EncounterTableLighting(selectedTable, tablePieces, roomImage, tableImage, TrayScene, DiceTray,
                ModifierHistory.Renderer, hostPortrait);
            var shownRule = selectedTable.HudRuleCard;
            ambientActivity?.Stop();
            configuredTable = selectedTable;
            hudRuleCard = shownRule;
            tableLighting = lighting;
            ambientActivity =selectedTable.AmbientActivity != null
                ? new EncounterAmbientActivity(selectedTable.AmbientActivity, tableLighting, ambientProps, encounterAudio) : null;
            // The table's rule and piece presenters are created once the encounter it presents is known.
            DisposePresenters();
            encounterAudio.SetTheme(selectedTable.AudioTheme);
            GameplayHud.ConfigureHostSigils(selectedTable.HostPresentation);
            actionModifierCue.ConfigureHost(selectedTable.HostPresentation);
            GameplayHud.ShowEncounterTitle(selectedTable.DisplayTitle);
            pauseMenu.Title.text = selectedTable.DisplayTitle;
            var credits = encounterAudio.Theme?.credits;
            pauseMenu.Credits.text = credits != null ? credits.text : "";
            pauseMenu.CreditsBacking.gameObject.SetActive(credits != null);
            RenderSoundLabel();
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
            DiceTray.RollEnded -= LayoutTable;
            DiceTray.RollEnded += LayoutTable;
            ModifierHistory.BindRenderer(TrayScene.HistoryRenderer);
            ModifierHistory.Configure(presentation, TrayScene);
            ModifierHistory.Build();
            GameplayHud.DiceTrayImage.texture = TrayScene.Texture;
            PositionTrays();
        }

        /** Lays the dice touch targets and the table pieces out over the tray's dice. */
        private void LayoutTable()
        {
            PositionTrays();
            PositionTablePieces();
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

        // Teardown destroys the images, pieces, tray, host and the presenters' die visuals in no fixed order, so the screen
        // frees only the lighting's material and drops its subscriptions.
        private void OnDestroy()
        {
            tableLighting?.ReleaseOwnedResources();
            tableLighting = null;
            hostPortrait.SpiritGesture -= PlaySpiritCue;
            ModifierHistory.TokenSelected -= InspectHistoryToken;
            DiceTray.RollEnded -= LayoutTable;
            Unsubscribe();
        }

        /**
         * Leaves the screen before its owner deactivates it: the screen stops following its controls, resets what it shows,
         * hides the journey and reports a presentation it interrupts as cancelled. Enabling the screen again follows the
         * controls again.
         */
        public void Close()
        {
            Unsubscribe();
            // An unbuilt screen has presented nothing.
            if (scorePlayback == null) return;
            bool canceled = presenting;
            ResetPresentation();
            JourneyView.gameObject.SetActive(false);
            if (canceled) controls.CompletePresentation();
        }

        // An inactive screen shows nothing and its table's lighting stays bound, so disabling only stops following the controls.
        private void OnDisable() => Unsubscribe();

        private void OnEnable()
        {
            applicationFocused = Application.isFocused;
            Subscribe();
            if (canvas == null) return;
            // The screen may have changed size while it was disabled.
            if (scorePlayback != null) FitScreen();
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

        // The canvas resizes this root object whenever the screen changes size or orientation; a built screen fits to it.
        private void OnRectTransformDimensionsChange()
        {
            if (scorePlayback != null) FitScreen();
        }

        private void LateUpdate()
        {
            if (scorePlayback == null) return;
            // Ambient activity suspends with focus and pause like the encounter's ambient audio.
            if (ambientActivity != null && applicationFocused && !applicationPaused) ambientActivity.Tick(Time.unscaledDeltaTime);
            tableLighting.Update();
            // Dice touch targets follow the dice a roll or flip carries; resting dice are laid out when it ends.
            if (DiceTray.IsRolling) PositionTrays();
        }

        private void OnApplicationFocus(bool focus) => applicationFocused = focus;

        private void OnApplicationPause(bool pause) => applicationPaused = pause;

        private void Build()
        {
            if (canvas == null || safeArea == null || portrait == null || inputGroup == null ||
                roomImage == null || tableImage == null || GameplayHud == null || RewardsPanel == null || EncounterCharmsIntroduction == null ||
                OwnedCharmsPanel == null || handRanksPanel == null || JourneyView == null || collectionView == null ||
                CharmPopup == null || endingView == null || ModifierHistory == null || actionModifierCue == null ||
                encounterAudio == null || pauseMenu == null || tablePresentation == null || TrayScene == null || hostPortrait == null)
                throw new InvalidOperationException("The encounter screen requires authored UI, table, host, audio, and rig references.");
            if (diceButtons.Count != DiceValues.Count)
                throw new InvalidOperationException("The encounter screen requires five authored dice targets.");
            foreach (var button in diceButtons)
                if (button == null) throw new InvalidOperationException("An authored dice target is missing.");
            foreach (var figure in outcomeAnimations)
            {
                if (figure == null) throw new InvalidOperationException("The encounter screen " + name + " lists a missing outcome animation.");
                figure.Validate();
            }
            SpiritUi.RequireWording(transform);
            GameplayHud.BuildHeader(portrait, ToggleMenu);
            GameplayHud.BuildPlayingPanel();
            GameplayHud.BuildPlayingHud(Reroll, Play);
            GameplayHud.BuildEncounterRule();
            GameplayHud.BuildStageIntro();
            GameplayHud.BuildFooter(ShowCharms, ShowHandRanks, ReturnToInvestigation);
            ConfigureTrays(tablePresentation.TrayPresentation);
            hostPortrait.Build(tablePresentation.HostPresentation);
            hostPortrait.SpiritGesture -= PlaySpiritCue;
            hostPortrait.SpiritGesture += PlaySpiritCue;
            ModifierHistory.TokenSelected -= InspectHistoryToken;
            ModifierHistory.TokenSelected += InspectHistoryToken;
            if (dieToggles == null)
            {
                dieToggles = new UnityAction[diceButtons.Count];
                for (int i = 0; i < dieToggles.Length; ++i)
                {
                    int index = i;
                    dieToggles[i] = () => ToggleDie(index);
                }
            }
            for (int i = 0; i < diceButtons.Count; ++i)
            {
                diceButtons[i].onClick.RemoveListener(dieToggles[i]);
                diceButtons[i].onClick.AddListener(dieToggles[i]);
            }
            actionModifierCue.Initialize();
            RewardsPanel.Build(() => Busy);
            EncounterCharmsIntroduction.Build();
            OwnedCharmsPanel.Build(ShowTable, () => Busy);
            handRanksPanel.Build(ShowTable);
            endingView.Build(ReturnFromEnding, RetryEncounter);
            endingPanel = endingView.Root;
            BuildMenu();
            JourneyView.Build(encounterAudio, (state, progress) => hostPortrait.SetState(state, progress), hostPortrait.SetVisible);
            collectionView.Build(HideCollection);
            CharmPopup.Build();
            scorePlayback = new SpiritScorePlayback(this, GameplayHud, DiceTray, ModifierHistory, encounterAudio,
                actionModifierCue, endingView);
            ConfigureTable(tablePresentation);
            FitScreen();
        }

        /** Fits the portrait to the screen's safe area and lays out what follows the portrait's size. */
        private void FitScreen()
        {
            SpiritPortraitSafeArea.FitToScreen(safeArea, portrait, portraitReferenceSize);
            if (RewardsPanel.Root.gameObject.activeSelf) RewardsPanel.UpdateLayout();
            if (IsCharmPopupOpen) CharmPopup.Layout();
            LayoutTable();
        }

        /**
         * Readies the screen, which stays disabled, as a still editor preview that the camera renders at the portrait size:
         * the gameplay HUD and its tray show at rest over the table with the host seated, and every other panel is hidden.
         */
        public void PreparePreview(Camera camera, Vector2 portraitSize, SpiritHostPresentationAsset host)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            SizePreview(portraitSize);
            GameplayHud.SetGameplayVisible(true, true);
            RewardsPanel.Root.gameObject.SetActive(false);
            OwnedCharmsPanel.Root.gameObject.SetActive(false);
            JourneyView.gameObject.SetActive(false);
            GameplayHud.ResetTransientVisuals();
            TrayScene.Initialize();
            var presentation = TrayScene.Presentation;
            TrayScene.DiceTray.Configure(presentation, TrayScene);
            TrayScene.DiceTray.Initialize();
            TrayScene.HistoryRenderer.Configure(presentation, TrayScene);
            TrayScene.HistoryRenderer.Initialize();
            GameplayHud.PreparePreview(TrayScene.Texture);
            hostPortrait.Build(host);
            hostPortrait.SetVisible(true);
        }

        /** Centres the preview portrait at the given size and lays the screen out for it. */
        public void SizePreview(Vector2 portraitSize)
        {
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = portraitSize;
            portrait.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
        }

        public void ShowCharmPopup(long instanceId)
        {
            if (Busy || controls.Snapshot == null || controls.Snapshot.Phase != EncounterPhase.Playing ||
                journeyControls != null && journeyControls.JourneyPhase != EncounterJourneyPhase.Playing) return;
            foreach (var instance in controls.Snapshot.Modifiers)
            {
                if (instance.InstanceId != instanceId) continue;
                var definition = controls.Catalog.GetModifier(instance.DefinitionId);
                CloseOverlays();
                CharmPopup.Show(instanceId, controls.ModifierPresentation, definition,
                    SpiritCharmVisuals.Status(controls, instance, definition),
                    controls.Snapshot.Phase);
                return;
            }
        }

        public void CloseCharmPopup()
        {
            CharmPopup.Close();
        }

        private void BuildMenu()
        {
            if (menuPanel == null || pauseMenu.Title == null || pauseMenu.SoundButton == null || pauseMenu.ProfileSummary == null ||
                pauseMenu.MainMenuButton == null || pauseMenu.CollectionButton == null || pauseMenu.SaveButton == null || pauseMenu.LoadButton == null ||
                pauseMenu.NewRunButton == null || pauseMenu.AbandonButton == null || pauseMenu.CloseButton == null ||
                pauseMenu.Credits == null || pauseMenu.CreditsBacking == null)
                throw new InvalidOperationException("The encounter pause menu requires authored UI references.");
            BindButton(pauseMenu.SoundButton, ToggleSound);
            BindButton(pauseMenu.CollectionButton, OpenCollectionFromMenu);
            BindButton(pauseMenu.SaveButton, SaveRun);
            BindButton(pauseMenu.LoadButton, LoadRun);
            BindButton(pauseMenu.NewRunButton, StartRun);
            BindButton(pauseMenu.AbandonButton, AbandonRun);
            BindButton(pauseMenu.MainMenuButton, ReturnToMainMenu);
            BindButton(pauseMenu.CloseButton, ToggleMenu);
            pauseMenu.MainMenuButton.gameObject.SetActive(mainMenuControls != null);
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
        private void ToggleDie(int index) { if (!Busy) controls.ToggleDie(index); }
        private void ReturnFromEnding()
        {
            if (Busy) return;
            // A win continues the journey with its next beat, or returns to the board after its last.
            if (journeyControls != null && controls.Snapshot?.Outcome == EncounterOutcome.Won) journeyControls.AdvanceJourney();
            else if (journeyControls != null) journeyControls.ReturnToInvestigation();
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
            RenderSoundLabel();
        }
        private void RenderSoundLabel()
            => SpiritUi.SetButtonLabel(pauseMenu.SoundButton, SpiritUi.GetWording(transform).pauseMenu.Sound(encounterAudio.Muted));
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
            CloseOverlays();
            OwnedCharmsPanel.Show();
        }

        public void ShowHandRanks()
        {
            if (Busy) return;
            CloseOverlays();
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
            CloseOverlays();
        }

        private void ToggleMenu()
        {
            if (Busy) return;
            bool open = !menuPanel.gameObject.activeSelf;
            CloseOverlays();
            menuPanel.gameObject.SetActive(open);
        }

        /** Closes the charm popup and the charms, hand ranks and pause menu panels; at most one of them is open. */
        private void CloseOverlays()
        {
            CloseCharmPopup();
            OwnedCharmsPanel.Root.gameObject.SetActive(false);
            handRanksPanel.Root.gameObject.SetActive(false);
            menuPanel.gameObject.SetActive(false);
        }

        private bool IsSelectedTable()
        {
            var selected = journeyControls?.SelectedJourney?.TableSceneAt(journeyControls.BeatIndex);
            if (selected == null) return journeyControls?.JourneyPhase == EncounterJourneyPhase.Playing;
            if (tablePresentation == null) return false;
            if (selected == tablePresentation) return true;
            // A table scene loaded through Addressables can hold its own instance of the table asset the journey references, so
            // identity falls back to the scene address the application loads the table scene by.
            return selected.Scene != null && selected.Scene.RuntimeKeyIsValid() &&
                selected.Scene.AssetGUID == tablePresentation.Scene?.AssetGUID;
        }

        // Internal so PlayMode tests refresh the shown state without changing it.
        internal void Refresh()
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
            var snapshot = controls.Snapshot;
            if (snapshot == null)
            {
                CloseCharmPopup();
                rulePresenters?.Sync(null, false);
                inputGroup.interactable = false;
                ModifierHistory.SetInteractable(false);
                RewardsPanel.Root.gameObject.SetActive(false);
                EncounterCharmsIntroduction.Hide();
                endingPanel.gameObject.SetActive(false);
                GameplayHud.ShowNoEncounter(hudRuleCard, controls.Status);
                return;
            }
            bool createdPresenters = CreatePresenters(snapshot.EncounterId);
            if (!ReferenceEquals(snapshot, presentedSnapshot))
            {
                ResetPresentation();
                hostPortrait.ResetPose();
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
            GameplayHud.ShowSnapshot(snapshot, hudRuleCard, controls.Status, lastScore, controls.SelectedMask);
            RewardsPanel.Root.gameObject.SetActive(!playing && !completed);
            endingPanel.gameObject.SetActive(completed);
            RenderProfileSummary();
            if (playing)
            {
                ambientActivity?.Begin();
                DiceTray.SetValues(snapshot.CurrentDice);
                SetDiceSelection(controls.SelectedMask);
            }
            else if (!completed) RewardsPanel.Render(snapshot);
            EncounterCharmsIntroduction.Refresh(controls.Catalog, controls.ModifierPresentation, snapshot, journeyControls?.SelectedJourney);
            if (completed)
            {
                SetGameplayVisible(false);
                endingPanel.gameObject.SetActive(true);
                endingView.Render(snapshot, journeyControls?.SelectedJourney,
                    SpiritCharmVisuals.UnlockSummary(controls, snapshot.NewlyUnlockedModifierIds, SpiritUi.GetWording(transform)));
            }
            if (IsCharmsOpen) OwnedCharmsPanel.Render();
            if (IsHandRanksOpen) RenderHandRanks();
        }

        /** Bay points offered to piece presenters follow the tray's resting dice slots, measured like the authored dice touch targets. */
        private void PositionTablePieces()
        {
            if (tablePieces.Count == 0 || DiceTray == null) return;
            // Rolling dice would carry a bay's piece with them; resting slot bounds do not depend on the face shown.
            if (!pieceSlotsMeasured || !DiceTray.IsRolling)
            {
                for (int bay = 0; bay < DiceValues.Count; ++bay) pieceSlotRects[bay] = DiceTray.GetSlotViewportRect(bay);
                pieceSlotsMeasured = true;
            }
            piecePresenters?.Layout();
        }

        private void ResetPresentation(bool resetHost = true)
        {
            CloseCharmPopup();
            StopAllCoroutines();
            endingView.Reset();
            SetOpeningOpacity(1);
            scorePlayback.CancelRoll();
            presenting = false;
            presentationSnapshot = null;
            if (resetHost)
            {
                hostPortrait.ResetPose();
                foreach (var figure in outcomeAnimations) figure.ResetToIdle();
            }
            // Leaving play for the journey lets the ambient activity settle; every other reset ends it at rest.
            if (resetHost) ambientActivity?.Stop();
            else ambientActivity?.Settle();
            tableLighting.ResetPresentation();
            ResetTransientVisuals();
        }

        private void ResetTransientVisuals()
        {
            actionModifierCue.Clear();
            DiceTray.SetScoringHighlight(0);
            ModifierHistory.ClearResolving();
            GameplayHud.ResetTransientVisuals();
        }

        private void SetDiceSelection(int mask)
        {
            DiceTray.SetSelection(mask);
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
            CloseOverlays();
            if (resolution.Snapshot.Phase == EncounterPhase.Completed && RewardsPanel.Root.gameObject.activeSelf)
                SetGameplayVisible(false);
            CreatePresenters(resolution.Snapshot.EncounterId);
            // An opening resolution may already have been refreshed as the installed state; its events build the rule state from nothing.
            if (resolution.PreviousRevision < 0) rulePresenters?.Sync(null, false);
            float openingFade = resolution.PreviousRevision < 0 && configuredTable != null
                ? configuredTable.OpeningFadeDuration : 0;
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
            if (!(controls is ISpiritProgressionControls progression)) { pauseMenu.ProfileSummary.text = ""; return; }
            int unlocked = 0;
            var ids = new HashSet<ModifierDefinition.Id>(progression.ProfileUnlockedModifierIds);
            foreach (var modifier in controls.Catalog.Modifiers)
                if (ids.Contains(modifier.Identity)) ++unlocked;
            var wording = SpiritUi.GetWording(transform).pauseMenu;
            pauseMenu.ProfileSummary.text = progression.ProfileStatus.Kind == PlayerProfileStatusKind.None
                ? wording.ProfileSummary(unlocked, progression.CompletedEncounterIds.Count)
                : wording.ProfileAttention(SpiritCharmVisuals.HasPendingProgress(controls));
        }

        public void ShowCollection()
        {
            if (Busy || controls?.Catalog == null || collectionView == null) return;
            CloseCharmPopup();
            menuPanel.gameObject.SetActive(false);
            collectionView.Show(controls.Catalog, controls.ModifierPresentation,
                (controls as ISpiritProgressionControls)?.ProfileUnlockedModifierIds ?? Array.Empty<ModifierDefinition.Id>(),
                journeyControls?.JourneyCatalog,
                (controls as ISpiritProgressionControls)?.ProfileDiscoveredFamilyIds ?? Array.Empty<ModifierFamilyDefinition.Id>());
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

        void ISpiritScorePlaybackHost.PlayOutcome(EncounterOutcome outcome)
        {
            foreach (var figure in outcomeAnimations) figure.Play(outcome);
        }

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
        private bool CreatePresenters(EncounterDefinition.Id encounterId)
        {
            if (presentersEncounterId == encounterId) return false;
            DisposePresenters();
            CreatePresenters(controls.Catalog.GetEncounter(encounterId));
            presentersEncounterId = encounterId;
            // New piece presenters place their props over the tray's current bay slots.
            PositionTablePieces();
            return true;
        }

        private void CreatePresenters(EncounterDefinition encounter)
        {
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
        }

        /**
         * Builds the screen for its table, readies its encounter audio and, given an encounter, creates the table's rule and
         * piece presenters for it, as binding the screen, loading it and showing the encounter do, then disposes the
         * presenters; it fails as they fail. The editor's content validation runs it on an edit-mode copy of the encounter
         * scene, where Awake does not run and no controls are bound.
         */
        internal void ValidateContent(EncounterDefinition encounter)
        {
            if (scorePlayback == null) Build();
            // An edit-mode copy receives no OnDestroy, so the table lighting that building bound is released here.
            tableLighting.Release();
            encounterAudio.Initialize();
            if (encounter == null) return;
            try { CreatePresenters(encounter); }
            finally { DisposePresenters(); }
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

            public override IRuleSceneLights Lights => view.tableLighting.RuleLights;

            public override void ReactHost()
            {
                view.hostPortrait.React();
            }

            public override IEnumerator FlipDie(int bay, int face) => view.DiceTray.Flip(bay, face);

            public override IEnumerator RerollDie(int bay, int face)
                => view.DiceTray.RollDie(bay, face, view.encounterAudio.BeginRoll(1));

            public override void PlayCue(AudioClip clip) => view.encounterAudio.PlayCue(clip);

            public override void JoltAmbient() => view.ambientActivity?.Jolt();

            public override bool ShowsPiece(TablePieceDefinition.Id pieceId)
                => SpiritTablePieceView.Find(view.tablePieces, pieceId) != null;

            public override float FlarePiece(TablePieceDefinition.Id pieceId)
            {
                var piece = SpiritTablePieceView.Find(view.tablePieces, pieceId) ?? throw new InvalidOperationException(
                    "The table shows no prop for piece " + pieceId + ".");
                return piece.isActiveAndEnabled ? piece.Flare() : 0f;
            }

            public override void ShowRuleBadge()
                => view.GameplayHud.RenderEncounterRule(view.presentationSnapshot, view.hudRuleCard);

            public override void CueAction(ModifierPhase phase, Sprite icon)
                => view.actionModifierCue.ShowHost(icon, phase, SpiritScorePlayback.GestureDurationOf(view.hostPortrait));

            public override void ShowSelection(string text) => view.GameplayHud.ShowRuleSelection(text);

            public override IEnumerator SweepSpirit(Sprite icon, float duration) => view.GameplayHud.AnimateRuleSpirit(icon, duration);
        }

        /** What the encounter screen offers piece interaction presenters: its controls' piece interactions and the tray's bay slots. */
        private sealed class TablePieceContext : PieceInteractionPresenterContext
        {
            private readonly SpiritEncounterView view;

            internal TablePieceContext(SpiritEncounterView view) => this.view = view;

            /** Pieces are used whenever the game accepts input during a hand and the piece's interaction is available. */
            public override bool CanInteract(TablePieceDefinition.Id pieceId, int interactionIndex)
            {
                if (view.Busy || !view.isActiveAndEnabled || view.controls.Snapshot == null ||
                    view.controls.Snapshot.Phase != EncounterPhase.Playing ||
                    view.journeyControls != null && view.journeyControls.JourneyPhase != EncounterJourneyPhase.Playing) return false;
                foreach (var interaction in view.controls.Snapshot.PieceInteractions)
                    if (interaction.PieceId == pieceId && interaction.InteractionIndex == interactionIndex) return interaction.IsAvailable;
                return false;
            }

            public override bool Interact(TablePieceDefinition.Id pieceId, int interactionIndex, PieceInteractionInput input)
            {
                if (!CanInteract(pieceId, interactionIndex)) return false;
                view.controls.InteractWithPiece(pieceId, interactionIndex, input);
                return true;
            }

            public override Vector3 BayPoint(int bay, Vector2 placement)
            {
                RectTransform frame = view.GameplayHud.DiceTrayImage.rectTransform;
                return frame.TransformPoint(SpiritTablePieceLayout.ViewportPoint(frame.rect, view.pieceSlotRects[bay], placement));
            }
        }
    }
}
