using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * Presents a journey's beats and the scenes they take place in, without creating encounter mechanics. Each kind of beat
     * and of scene shows itself through this screen.
     */
    public sealed class EncounterJourneyView : MonoBehaviour, IJourneyStage
    {
        /** Seconds the foreground takes to fade in at the start of the beat that enters its picture. */
        private const float ForegroundFadeInSeconds = .25f;
        /** How far each parting panel travels, as a fraction of the artwork width. */
        private const float PanelPartingDistance = .54f;

        private IEncounterJourneyControls controls;
        private ISpiritEncounterControls encounterControls;
        [SerializeField] private RectTransform artwork;
        [SerializeField] private RawImage background;
        [Tooltip("Holds the shown picture's flow layers over the background and below its drift layers, so they follow its " +
            "motion.")]
        [SerializeField] private RectTransform flowLayersRoot;
        [SerializeField] private PictureFlowLayerImage flowLayerPrefab;
        private readonly List<PictureFlowLayerImage> flowLayers = new List<PictureFlowLayerImage>();
        private int shownFlowLayerCount;
        [Tooltip("Holds the shown picture's drift layers over the background, so they follow its motion.")]
        [SerializeField] private RectTransform driftLayersRoot;
        [SerializeField] private PictureDriftLayerImage driftLayerPrefab;
        private readonly List<PictureDriftLayerImage> driftLayers = new List<PictureDriftLayerImage>();
        private int shownDriftLayerCount;
        // Flow and drift layers advance on unscaled time from when their picture is shown.
        private double driftStartTime;
        [SerializeField] private RawImage foreground;
        [SerializeField] private RawImage leftPanel;
        [SerializeField] private RawImage rightPanel;
        [SerializeField] private CanvasGroup illustrationGroup;
        [SerializeField] private Text title;
        [SerializeField] private Text body;
        [SerializeField] private RectTransform narrative;
        [Tooltip("The narration panel's least height in view units, so a one-line beat still reads as a panel. The panel's " +
            "top sits on the shown scene's Narrative Top and its height fits the beat's content, growing down to at most its " +
            "authored bottom anchor, below which the content scrolls.")]
        [Min(0)]
        [SerializeField] private float minimumNarrativeHeight = 120;
        [Tooltip("Seconds the narration panel takes to move its top and bottom to a new beat's, smoothly in and out, on " +
            "unscaled time. It takes its new place at once on the first beat the journey screen shows and when the view " +
            "changes size.")]
        [Min(0)]
        [SerializeField] private float narrativeEaseSeconds = .25f;
        // The narration panel's ease toward its fitted rectangle: its top anchor and bottom offset from and to, and when it
        // began. LateUpdate steps it only while it runs; anything that interrupts the beat completes it.
        private bool easingNarrative;
        private float narrativeEaseFromTop, narrativeEaseFromBottom, narrativeEaseToTop, narrativeEaseToBottom;
        private double narrativeEaseStartTime;
        [SerializeField] private ScrollRect narrativeScroll;
        [SerializeField] private Text ruleHeading;
        [SerializeField] private SpiritInvitationRuleView invitationRulePrefab;
        [SerializeField] private GameObject invitationSection;
        [SerializeField] private RectTransform invitationRulesRoot;
        private readonly List<SpiritInvitationRuleView> invitationRules = new List<SpiritInvitationRuleView>();
        private int invitationRuleCount;
        private bool invitationShown;
        [SerializeField] private RectTransform charmDetail;
        [SerializeField] private Image charmIcon;
        [SerializeField] private Text charmName;
        [SerializeField] private Text charmEffect;
        [SerializeField] private Text charmCondition;
        [SerializeField] private Text journeyStatus;
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button returnButton;
        private Action<SpiritPresentationState, float> hostState;
        private Action<bool> hostVisibility;
        [SerializeField] private CanvasGroup narrativeGroup;
        private SpiritEncounterAudio audio;
        private EncounterJourneyAsset renderedJourney;
        private EncounterJourneyCatalogAsset renderedCatalog;
        private EncounterJourneyPhase renderedPhase;
        private bool hasRendered;
        private EncounterJourneyBeat activeBeat;
        // The scene shown and its picture's lighting; a table scene shows no picture.
        private JourneySceneAsset shownScene;
        private SceneLightingAsset shownLighting;
        private Color shownForegroundTint;
        // The foreground fades in over the beat that enters its picture, unless panels part to reveal it.
        private bool fadingForeground;
        private SceneLightingAsset previousSceneLighting;
        private SceneLightingState sceneLightingState = new SceneLightingState();
        private SceneLightingState previousSceneLightingState = new SceneLightingState();
        private SceneLightingMaterial sceneLightingMaterial;
        private SceneLightingMaterial previousSceneLightingMaterial;

        // Internal so EditMode tests read the lighting the background and its departing panels show.
        internal RawImage Background => background;
        internal RawImage LeftPanel => leftPanel;
        internal RawImage RightPanel => rightPanel;
        // Internal so PlayMode tests accept the invitation through the screen's own button.
        internal Button AcceptButton => acceptButton;
        internal SceneLightingState SceneLighting => sceneLightingState;
        internal SceneLightingState PreviousSceneLighting => previousSceneLightingState;
        internal IReadOnlyList<PictureDriftLayerImage> DriftLayers => driftLayers;
        internal int ShownDriftLayerCount => shownDriftLayerCount;
        // Internal so review captures wait for the narration panel to reach its fitted rectangle.
        internal bool IsEasingNarrative => easingNarrative;

        public bool IsAnimating { get; private set; }
        public int VisibleBeatIndex { get; private set; } = -1;
        [field: SerializeField] public SpiritEncounterCharmsScreen EncounterCharms { get; private set; }

        public void Build(SpiritEncounterAudio encounterAudio,
            Action<SpiritPresentationState, float> presentHostState = null, Action<bool> presentHostVisibility = null)
        {
            SpiritUi.RequireWording(transform);
            hostState = presentHostState;
            hostVisibility = presentHostVisibility;
            audio = encounterAudio;
            advanceButton.onClick.RemoveAllListeners();
            advanceButton.onClick.AddListener(() => controls.AdvanceJourney());
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(ShowEncounterCharms);
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(() => controls.ReturnToInvestigation());
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => controls.SkipIntro());
            EncounterCharms.Build(() => controls.AcceptEncounter());
        }

        private void ShowEncounterCharms()
        {
            var journey = controls?.SelectedJourney;
            var beat = journey != null ? journey.EncounterBeatAt(controls.BeatIndex + 1) : null;
            if (beat == null || encounterControls?.Catalog == null) return;
            var encounter = encounterControls.Catalog.GetEncounter(beat.Encounter.Identity);
            // Additions first offered after a later stage are introduced inside the encounter instead.
            if (!encounter.EncounterModifiersAvailableFromStart)
            {
                controls.AcceptEncounter();
                return;
            }
            // Back to invitation returns to this beat, so it must rest at its authored final pose.
            FinishAnimation();
            EncounterCharms.Show(encounterControls.Catalog, encounterControls.ModifierPresentation, encounter.EncounterModifierIds);
        }

        public void Bind(IEncounterJourneyControls journey, ISpiritEncounterControls encounter)
        {
            CancelAnimation();
            ClearSceneLighting();
            controls = journey;
            encounterControls = encounter;
            EncounterCharms.Hide();
            hasRendered = false;
        }

        public void Refresh()
        {
            if (controls == null) return;
            var phase = controls.JourneyPhase;
            var journey = controls.SelectedJourney;
            int index = controls.BeatIndex;
            bool changed = !hasRendered || renderedPhase != phase || renderedJourney != journey ||
                renderedCatalog != controls.JourneyCatalog || VisibleBeatIndex != index;
            bool visible = phase == EncounterJourneyPhase.Sequence;
            gameObject.SetActive(visible);
            if (!visible)
            {
                CancelAnimation();
                ClearSceneLighting();
                hasRendered = false;
                return;
            }
            if (journey != null && index >= 0 && index < journey.Beats.Count)
            {
                journeyStatus.text = SpiritStatusText.Describe(controls.JourneyStatus, SpiritUi.GetWording(transform).status);
                if (changed) ShowBeat(journey, index);
            }
            renderedJourney = journey;
            renderedCatalog = controls.JourneyCatalog;
            renderedPhase = phase;
            VisibleBeatIndex = index;
            hasRendered = true;
        }

        private void ShowBeat(EncounterJourneyAsset journey, int index)
        {
            EncounterCharms.Hide();
            bool continuingJourney = hasRendered && renderedJourney == journey && renderedPhase == EncounterJourneyPhase.Sequence;
            // The panel eases from where it shows, part-way through the previous beat's ease if that still runs.
            float shownNarrativeTop = NarrativePanel.anchorMax.y, shownNarrativeBottom = NarrativePanel.offsetMin.y;
            CancelAnimation();
            var beat = journey.Beats[index];
            var scene = journey.SceneAt(index);
            bool entering = !continuingJourney || scene != shownScene;
            // Only the beat that sets the scene changes it with its transition, and panels part only a picture shown before.
            bool parting = entering && continuingJourney && beat.Scene != null &&
                beat.Transition == EncounterJourneyTransition.PartingPanels && background.texture != null;
            Texture2D previous = parting ? background.texture as Texture2D : null;
            Rect previousCrop = background.uvRect;
            if (entering)
            {
                if (parting)
                {
                    previousSceneLighting = shownLighting;
                    previousSceneLightingMaterial = sceneLightingMaterial;
                    var state = previousSceneLightingState;
                    previousSceneLightingState = sceneLightingState;
                    sceneLightingState = state;
                    sceneLightingMaterial = null;
                }
                else
                {
                    sceneLightingMaterial?.Dispose();
                    sceneLightingMaterial = null;
                }
                sceneLightingState.Sample(null, Time.unscaledTimeAsDouble);
                shownScene = scene;
                scene.Present(this);
                // A lit picture gets a compositor of its own; LateUpdate samples its lighting onto it.
                if (shownLighting != null) sceneLightingMaterial = new SceneLightingMaterial(shownLighting.Shader);
            }
            background.material = sceneLightingMaterial?.Material;
            fadingForeground = entering && !parting;
            activeBeat = beat;
            title.text = beat.Title;
            body.text = beat.Text;
            charmDetail.gameObject.SetActive(false);
            invitationShown = false;
            invitationRuleCount = 0;
            beat.Show(this, journey, index);
            ShowInvitationRules();
            FitNarrative();
            // The first beat the screen shows has no panel on screen to ease from.
            if (continuingJourney) EaseNarrative(shownNarrativeTop, shownNarrativeBottom);
            narrativeScroll.StopMovement();
            narrativeScroll.verticalNormalizedPosition = 1;
            advanceButton.gameObject.SetActive(!invitationShown);
            acceptButton.gameObject.SetActive(invitationShown);
            var wording = SpiritUi.GetWording(transform).journey;
            SpiritUi.SetButtonLabel(invitationShown ? acceptButton : advanceButton, string.IsNullOrWhiteSpace(beat.ContinueLabel)
                ? invitationShown ? wording.acceptButton : wording.continueButton : beat.ContinueLabel);
            skipButton.gameObject.SetActive(controls.CanSkipIntro && !invitationShown);
            leftPanel.gameObject.SetActive(parting);
            rightPanel.gameObject.SetActive(parting);
            leftPanel.texture = rightPanel.texture = previous;
            // The departing panels show the departing plate as it was cropped, each one half of it.
            leftPanel.uvRect = new Rect(previousCrop.x, previousCrop.y, previousCrop.width / 2, previousCrop.height);
            rightPanel.uvRect = new Rect(previousCrop.center.x, previousCrop.y, previousCrop.width / 2, previousCrop.height);
            leftPanel.material = rightPanel.material = previousSceneLightingMaterial?.Material;
            if (beat.AudioCue != null) audio?.PlayCue(beat.AudioCue);
            StartCoroutine(AnimateBeat(journey, index));
        }

        void IJourneyStage.ShowPicture(Texture2D picture, SceneLightingAsset lighting,
            IReadOnlyList<PictureFlowLayer> pictureFlowLayers, IReadOnlyList<PictureDriftLayer> pictureDriftLayers,
            Texture2D foregroundTexture, Rect foregroundRect, Color foregroundTint, float horizontalCropAnchor)
        {
            // The artwork's AspectRatioFitter keeps it at the portrait's shape, covering the view; where it overflows the
            // view, its pivot is the part that stays in view: the bottom edge, and the side the picture names.
            artwork.pivot = new Vector2(horizontalCropAnchor, 0);
            shownLighting = lighting;
            shownForegroundTint = foregroundTint;
            var backgroundTexture = lighting != null ? lighting.BaseTexture : picture;
            background.texture = backgroundTexture;
            background.color = backgroundTexture != null ? Color.white : Color.clear;
            // The plate shows at one scale, covering the artwork's portrait shape (its fitter's ratio) and cropping the
            // excess about its centre. The lighting samples through the same texture coordinates, so it stays registered.
            Rect crop = backgroundTexture != null
                ? CoverCrop(backgroundTexture, artwork.GetComponent<AspectRatioFitter>().aspectRatio) : new Rect(0, 0, 1, 1);
            background.uvRect = crop;
            ShowFlowLayers(pictureFlowLayers, picture, crop);
            ShowDriftLayers(pictureDriftLayers, crop);
            driftStartTime = Time.unscaledTimeAsDouble;
            foreground.texture = foregroundTexture;
            foreground.GetComponent<AspectRatioFitter>().aspectRatio = foregroundTexture != null ?
                (float)foregroundTexture.width / foregroundTexture.height : 1;
            foreground.color = foregroundTexture != null ? foregroundTint : Color.clear;
            var clip = (RectTransform)foreground.transform.parent;
            Rect clipRect = PictureToFrame(foregroundRect, crop);
            clip.anchorMin = clipRect.min;
            clip.anchorMax = clipRect.max;
            clip.offsetMin = clip.offsetMax = Vector2.zero;
        }

        /** The part of the texture that shows at one scale over a frame of this aspect (width / height), cropped about its centre. */
        private static Rect CoverCrop(Texture texture, float frameAspect)
        {
            float textureAspect = (float)texture.width / texture.height;
            if (textureAspect > frameAspect)
            {
                float width = frameAspect / textureAspect;
                return new Rect((1 - width) / 2, 0, width, 1);
            }
            float height = textureAspect / frameAspect;
            return new Rect(0, (1 - height) / 2, 1, height);
        }

        /**
         * A rectangle in picture coordinates (normalized over the plate, origin bottom left) in the coordinates of the frame
         * that shows the crop of the plate.
         */
        private static Rect PictureToFrame(Rect rect, Rect crop)
            => new Rect((rect.x - crop.x) / crop.width, (rect.y - crop.y) / crop.height, rect.width / crop.width,
                rect.height / crop.height);

        void IJourneyStage.ClearPicture()
            => ((IJourneyStage)this).ShowPicture(null, null, Array.Empty<PictureFlowLayer>(), Array.Empty<PictureDriftLayer>(),
                null, new Rect(0, 0, 1, 1), Color.white, .5f);

        void IJourneyStage.ShowHost(bool visible) => hostVisibility?.Invoke(visible);

        void IJourneyStage.ShowCharm(ModifierAsset charm)
        {
            if (encounterControls?.Catalog == null) return;
            charmDetail.gameObject.SetActive(true);
            var definition = encounterControls.Catalog.GetModifier(charm.Identity);
            var charms = encounterControls.ModifierPresentation;
            var theme = SpiritUi.GetTheme(transform);
            string name = charms.GetName(definition);
            body.text = body.text.Replace("{charm}", "<b><color=#" + ColorUtility.ToHtmlStringRGB(theme.amber) +
                ">" + charms.GetFamilyName(definition) + "</color></b>");
            charmName.text = name;
            charmEffect.text = "<b>" + charms.GetEffectText(definition) + "</b>";
            charmEffect.color = charms.GetEffectColor(definition);
            charmCondition.text = SpiritCharmVisuals.GetModifierDescription(charms, definition, SpiritUi.GetWording(transform).charmText,
                theme.amber, theme.GetModifierTriggerColor(definition.Phase));
            charmIcon.sprite = charms.GetIcon(definition);
            charmIcon.color = charms.GetEffectColor(definition);
            charmIcon.gameObject.SetActive(true);
        }

        void IJourneyStage.ShowInvitation(EncounterTablePresentationAsset table)
        {
            invitationShown = true;
            foreach (var rule in table.RulePresentations)
            {
                if (rule == null) continue;
                int index = invitationRuleCount++;
                if (index == invitationRules.Count)
                    invitationRules.Add(Instantiate(invitationRulePrefab, invitationRulesRoot, false));
                invitationRules[index].Render(rule.DisplayName, rule.Description, rule.Icon,
                    table.HostPresentation);
            }
        }

        void IJourneyStage.MovePicture(Vector2 backgroundOffset, float backgroundScale, Vector2 foregroundOffset,
            float foregroundScale)
        {
            background.rectTransform.anchoredPosition = Vector2.Scale(backgroundOffset, artwork.rect.size);
            background.rectTransform.localScale = Vector3.one * backgroundScale;
            foreground.rectTransform.anchoredPosition = Vector2.Scale(foregroundOffset, artwork.rect.size);
            foreground.rectTransform.localScale = Vector3.one * foregroundScale;
        }

        /**
         * Shows the picture's flow layers in order over the background, each redrawing the picture inside its rectangle and
         * starting its flow now. Their rectangles are in picture coordinates, so their root spans the whole plate, of which
         * the background shows the crop.
         */
        private void ShowFlowLayers(IReadOnlyList<PictureFlowLayer> layers, Texture2D picture, Rect crop)
        {
            HideFlowLayers();
            if (layers.Count > 0) SpanPlate(flowLayersRoot, crop);
            for (int i = 0; i < layers.Count; ++i)
            {
                if (i == flowLayers.Count) flowLayers.Add(Instantiate(flowLayerPrefab, flowLayersRoot, false));
                flowLayers[i].Show(layers[i], picture);
            }
            shownFlowLayerCount = layers.Count;
        }

        private void HideFlowLayers()
        {
            for (int i = 0; i < shownFlowLayerCount; ++i) flowLayers[i].Hide();
            shownFlowLayerCount = 0;
        }

        /**
         * Shows the picture's drift layers in order over the background, each starting its drift now. Their rectangles are in
         * picture coordinates, so their root spans the whole plate, of which the background shows the crop.
         */
        private void ShowDriftLayers(IReadOnlyList<PictureDriftLayer> layers, Rect crop)
        {
            HideDriftLayers();
            if (layers.Count > 0) SpanPlate(driftLayersRoot, crop);
            for (int i = 0; i < layers.Count; ++i)
            {
                if (i == driftLayers.Count) driftLayers.Add(Instantiate(driftLayerPrefab, driftLayersRoot, false));
                driftLayers[i].Show(layers[i]);
            }
            shownDriftLayerCount = layers.Count;
        }

        /** Spans a layer root over the whole plate around the background, which shows the crop of it. */
        private static void SpanPlate(RectTransform root, Rect crop)
        {
            Rect plate = PictureToFrame(new Rect(0, 0, 1, 1), crop);
            root.anchorMin = plate.min;
            root.anchorMax = plate.max;
            root.offsetMin = root.offsetMax = Vector2.zero;
        }

        private void HideDriftLayers()
        {
            for (int i = 0; i < shownDriftLayerCount; ++i) driftLayers[i].Hide();
            shownDriftLayerCount = 0;
        }

        /** Shows the rule cards an invitation rendered, in place of the title, or hides the rule section. */
        private void ShowInvitationRules()
        {
            bool showRules = invitationRuleCount > 0;
            ruleHeading.text = SpiritUi.GetWording(transform).journey.ruleHeading.For(invitationRuleCount);
            SetInvitationRulesVisible(showRules);
            title.gameObject.SetActive(!showRules);
        }

        /**
         * Sits the narration panel's top on the shown scene's narrative top and fits its height to the beat's content (title
         * or invitation rules, body, charm detail) with the panel's frame around the scroll view, growing down from the top.
         * It keeps at least its minimum height and never reaches below its authored bottom anchor; content taller than that
         * scrolls inside it. Called once the beat's content is set and when the view changes size.
         */
        private void FitNarrative()
        {
            if (shownScene == null) return;
            var panel = NarrativePanel;
            var top = panel.anchorMax;
            top.y = shownScene.NarrativeTop;
            panel.anchorMax = top;
            LayoutRebuilder.ForceRebuildLayoutImmediate(narrative);
            float frame = panel.rect.height - ((RectTransform)narrativeScroll.transform).rect.height;
            // The panel's height with its bottom on its authored bottom anchor.
            float room = ((RectTransform)panel.parent).rect.height * (top.y - panel.anchorMin.y) + panel.offsetMax.y;
            float height = Mathf.Min(room, Mathf.Max(minimumNarrativeHeight, LayoutUtility.GetPreferredHeight(narrative) + frame));
            panel.offsetMin = new Vector2(panel.offsetMin.x, room - height);
        }

        // The narration panel is the object whose group fades the narrative, around the scroll view.
        private RectTransform NarrativePanel => (RectTransform)narrativeGroup.transform;

        /**
         * Moves the narration panel from the given top anchor and bottom offset to the rectangle FitNarrative just set, over
         * the ease. Its content is already laid out for that rectangle; only the panel's top and bottom move.
         */
        private void EaseNarrative(float fromTop, float fromBottom)
        {
            var panel = NarrativePanel;
            narrativeEaseToTop = panel.anchorMax.y;
            narrativeEaseToBottom = panel.offsetMin.y;
            if (narrativeEaseSeconds <= 0 || fromTop == narrativeEaseToTop && fromBottom == narrativeEaseToBottom) return;
            narrativeEaseFromTop = fromTop;
            narrativeEaseFromBottom = fromBottom;
            narrativeEaseStartTime = Time.unscaledTimeAsDouble;
            easingNarrative = true;
            SetNarrativeRect(fromTop, fromBottom);
        }

        private void StepNarrativeEase(double time)
        {
            float progress = (float)((time - narrativeEaseStartTime) / narrativeEaseSeconds);
            if (progress >= 1)
            {
                CompleteNarrativeEase();
                return;
            }
            float eased = Mathf.SmoothStep(0, 1, progress);
            SetNarrativeRect(Mathf.Lerp(narrativeEaseFromTop, narrativeEaseToTop, eased),
                Mathf.Lerp(narrativeEaseFromBottom, narrativeEaseToBottom, eased));
        }

        /** Ends a running ease at its fitted rectangle, so the panel is never left part-way. */
        private void CompleteNarrativeEase()
        {
            if (!easingNarrative) return;
            easingNarrative = false;
            SetNarrativeRect(narrativeEaseToTop, narrativeEaseToBottom);
        }

        private void SetNarrativeRect(float top, float bottom)
        {
            var panel = NarrativePanel;
            panel.anchorMax = new Vector2(panel.anchorMax.x, top);
            panel.offsetMin = new Vector2(panel.offsetMin.x, bottom);
        }

        // The canvas resizes this root object whenever the screen changes size; the narration's wrapping and room follow it
        // at once, ending any ease, whose target was fitted to the old size.
        private void OnRectTransformDimensionsChange()
        {
            CompleteNarrativeEase();
            FitNarrative();
        }

        private void SetInvitationRulesVisible(bool visible)
        {
            invitationSection.SetActive(visible);
            for (int i = 0; i < invitationRules.Count; ++i)
                invitationRules[i].gameObject.SetActive(visible && i < invitationRuleCount);
        }

        private IEnumerator AnimateBeat(EncounterJourneyAsset journey, int index)
        {
            var beat = journey.Beats[index];
            IsAnimating = true;
            SetButtons();
            float duration = Mathf.Max(0, beat.Duration);
            float age = 0;
            // Let Refresh finish recording this beat before an immediate continuation can refresh it.
            if (duration == 0 && beat.AdvancesItself)
            {
                ApplyMotion(beat, 0);
                yield return null;
            }
            while (age < duration)
            {
                ApplyMotion(beat, duration > 0 ? age / duration : 1);
                age += Time.unscaledDeltaTime;
                yield return null;
            }
            if (controls == null || controls.JourneyPhase != EncounterJourneyPhase.Sequence ||
                controls.SelectedJourney != journey || controls.BeatIndex != index || activeBeat != beat) yield break;
            ApplyMotion(beat, 1);
            leftPanel.gameObject.SetActive(false);
            rightPanel.gameObject.SetActive(false);
            ClearPartingLighting();
            IsAnimating = false;
            if (beat.AdvancesItself && index + 1 < journey.Beats.Count)
            {
                controls.AdvanceJourney();
                yield break;
            }
            narrativeGroup.alpha = 1;
            SetButtons();
        }

        private void ApplyMotion(EncounterJourneyBeat beat, float value)
        {
            float eased = Mathf.SmoothStep(0, 1, value);
            hostState?.Invoke(beat.HostState, value);
            narrativeGroup.alpha = beat.NarrativeOpacity(value);
            beat.Move(this, eased);
            illustrationGroup.alpha = 1;
            if (foreground.texture != null)
            {
                Color tint = shownForegroundTint;
                tint.a *= !fadingForeground || value >= 1 ? 1 : Mathf.Clamp01(value * beat.Duration / ForegroundFadeInSeconds);
                foreground.color = tint;
            }
            float distance = artwork.rect.width * PanelPartingDistance * eased;
            leftPanel.rectTransform.anchoredPosition = new Vector2(-distance, 0);
            rightPanel.rectTransform.anchoredPosition = new Vector2(distance, 0);
        }

        /** Continue and Accept stay available throughout; only skipping depends on journey state. */
        private void SetButtons()
        {
            skipButton.interactable = controls != null && controls.CanSkipIntro;
            returnButton.interactable = true;
        }

        /** Ends the current beat at its authored final pose, as its uninterrupted motion would, without auto-advancing. */
        private void FinishAnimation()
        {
            // The panel's ease can outlast a short beat's motion.
            CompleteNarrativeEase();
            if (!IsAnimating || activeBeat == null) return;
            StopAllCoroutines();
            ApplyMotion(activeBeat, 1);
            leftPanel.gameObject.SetActive(false);
            rightPanel.gameObject.SetActive(false);
            ClearPartingLighting();
            IsAnimating = false;
            narrativeGroup.alpha = 1;
            SetButtons();
        }

        private void CancelAnimation()
        {
            StopAllCoroutines();
            CompleteNarrativeEase();
            IsAnimating = false;
            ClearPartingLighting();
            illustrationGroup.alpha = 1;
            leftPanel.rectTransform.anchoredPosition = rightPanel.rectTransform.anchoredPosition = Vector2.zero;
            leftPanel.gameObject.SetActive(false);
            rightPanel.gameObject.SetActive(false);
            foreground.rectTransform.anchoredPosition = Vector2.zero;
            foreground.rectTransform.localScale = Vector3.one;
            background.rectTransform.anchoredPosition = Vector2.zero;
            background.rectTransform.localScale = Vector3.one;
            narrativeGroup.alpha = 1;
            SetButtons();
        }

        // Internal so EditMode tests, where Unity sends no lifecycle messages, drive them directly.
        internal void OnDisable()
        {
            CancelAnimation();
            ClearSceneLighting();
            hasRendered = false;
        }

        /**
         * Steps the narration panel's ease while it runs, advances the shown picture's flow and drift layers, and samples the
         * shown and departing pictures' lighting from one clock onto their compositors while there is any.
         */
        internal void LateUpdate()
        {
            double time = Time.unscaledTimeAsDouble;
            if (easingNarrative) StepNarrativeEase(time);
            for (int i = 0; i < shownFlowLayerCount; ++i) flowLayers[i].Flow(time - driftStartTime);
            for (int i = 0; i < shownDriftLayerCount; ++i) driftLayers[i].Drift(time - driftStartTime);
            if (shownLighting == null && previousSceneLighting == null) return;
            if (shownLighting != null)
            {
                sceneLightingState.Sample(shownLighting, time);
                sceneLightingMaterial.Apply(sceneLightingState);
            }
            if (previousSceneLighting == null) return;
            previousSceneLightingState.Sample(previousSceneLighting, time);
            previousSceneLightingMaterial.Apply(previousSceneLightingState);
        }

        private void ClearPartingLighting()
        {
            leftPanel.material = null;
            leftPanel.texture = null;
            rightPanel.material = null;
            rightPanel.texture = null;
            previousSceneLightingMaterial?.Dispose();
            previousSceneLightingMaterial = null;
            previousSceneLighting = null;
            previousSceneLightingState.Sample(null, Time.unscaledTimeAsDouble);
        }

        private void ClearSceneLighting()
        {
            background.texture = null;
            background.color = Color.clear;
            background.material = null;
            HideFlowLayers();
            HideDriftLayers();
            sceneLightingMaterial?.Dispose();
            sceneLightingMaterial = null;
            sceneLightingState.Sample(null, Time.unscaledTimeAsDouble);
            activeBeat = null;
            shownScene = null;
            shownLighting = null;
        }

        // Teardown destroys the background and panels in no fixed order, so the screen frees only the compositors it created,
        // which are assets the scene does not unload.
        private void OnDestroy()
        {
            sceneLightingMaterial?.Dispose();
            sceneLightingMaterial = null;
            previousSceneLightingMaterial?.Dispose();
            previousSceneLightingMaterial = null;
        }
    }
}
