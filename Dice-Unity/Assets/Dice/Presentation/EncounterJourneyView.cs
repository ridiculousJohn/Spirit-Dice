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
        [SerializeField] private RawImage foreground;
        [SerializeField] private RawImage leftPanel;
        [SerializeField] private RawImage rightPanel;
        [SerializeField] private CanvasGroup illustrationGroup;
        [SerializeField] private Text title;
        [SerializeField] private Text body;
        [SerializeField] private RectTransform narrative;
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
        // The scene shown and its picture; a table scene shows no picture.
        private JourneySceneAsset shownScene;
        private Texture2D shownBackground;
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
            CancelAnimation();
            var beat = journey.Beats[index];
            var scene = journey.SceneAt(index);
            bool entering = !continuingJourney || scene != shownScene;
            // Only the beat that sets the scene changes it with its transition, and panels part only a picture shown before.
            bool parting = entering && continuingJourney && beat.Scene != null &&
                beat.Transition == EncounterJourneyTransition.PartingPanels && background.texture != null;
            Texture2D previous = parting ? background.texture as Texture2D : null;
            background.material = null;
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
            }
            fadingForeground = entering && !parting;
            activeBeat = beat;
            title.text = beat.Title;
            body.text = beat.Text;
            charmDetail.gameObject.SetActive(false);
            invitationShown = false;
            invitationRuleCount = 0;
            beat.Show(this, journey, index);
            ShowInvitationRules();
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
            leftPanel.material = rightPanel.material = previousSceneLightingMaterial?.Material;
            if (beat.AudioCue != null) audio?.PlayCue(beat.AudioCue);
            StartCoroutine(AnimateBeat(journey, index));
        }

        void IJourneyStage.ShowPicture(Texture2D picture, SceneLightingAsset lighting, Texture2D foregroundTexture,
            Rect foregroundRect, Color foregroundTint)
        {
            shownBackground = picture;
            shownLighting = lighting;
            shownForegroundTint = foregroundTint;
            var backgroundTexture = lighting != null ? lighting.BaseTexture : picture;
            background.texture = backgroundTexture;
            background.color = backgroundTexture != null ? Color.white : Color.clear;
            foreground.texture = foregroundTexture;
            foreground.GetComponent<AspectRatioFitter>().aspectRatio = foregroundTexture != null ?
                (float)foregroundTexture.width / foregroundTexture.height : 1;
            foreground.color = foregroundTexture != null ? foregroundTint : Color.clear;
            var clip = (RectTransform)foreground.transform.parent;
            clip.anchorMin = foregroundRect.min;
            clip.anchorMax = foregroundRect.max;
            clip.offsetMin = clip.offsetMax = Vector2.zero;
        }

        void IJourneyStage.ClearPicture()
            => ((IJourneyStage)this).ShowPicture(null, null, null, new Rect(0, 0, 1, 1), Color.white);

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

        /** Shows the rule cards an invitation rendered, in place of the title, or hides the rule section. */
        private void ShowInvitationRules()
        {
            bool showRules = invitationRuleCount > 0;
            ruleHeading.text = SpiritUi.GetWording(transform).journey.ruleHeading.For(invitationRuleCount);
            SetInvitationRulesVisible(showRules);
            title.gameObject.SetActive(!showRules);
            LayoutRebuilder.MarkLayoutForRebuild(narrative);
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
            IsAnimating = false;
            ClearPartingLighting();
            if (illustrationGroup == null) return;
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

        internal void LateUpdate()
        {
            double time = Time.unscaledTimeAsDouble;
            var asset = shownLighting;
            sceneLightingState.Sample(asset, time);
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
            if (background != null)
            {
                var texture = asset != null ? asset.BaseTexture : shownBackground;
                background.texture = texture;
                background.color = texture != null ? Color.white : Color.clear;
                background.material = sceneLightingMaterial?.Material;
            }
            if (previousSceneLighting == null) return;
            previousSceneLightingState.Sample(previousSceneLighting, time);
            if (previousSceneLightingMaterial == null) previousSceneLightingMaterial = new SceneLightingMaterial(previousSceneLighting.Shader);
            previousSceneLightingMaterial.Apply(previousSceneLightingState);
            leftPanel.texture = rightPanel.texture = previousSceneLighting.BaseTexture;
            leftPanel.material = rightPanel.material = previousSceneLightingMaterial.Material;
        }

        private void ClearPartingLighting()
        {
            if (leftPanel != null) { leftPanel.material = null; leftPanel.texture = null; }
            if (rightPanel != null) { rightPanel.material = null; rightPanel.texture = null; }
            previousSceneLightingMaterial?.Dispose();
            previousSceneLightingMaterial = null;
            previousSceneLighting = null;
            previousSceneLightingState.Sample(null, Time.unscaledTimeAsDouble);
        }

        private void ClearSceneLighting()
        {
            if (background != null) background.material = null;
            sceneLightingMaterial?.Dispose();
            sceneLightingMaterial = null;
            sceneLightingState.Sample(null, Time.unscaledTimeAsDouble);
            activeBeat = null;
            shownScene = null;
            shownBackground = null;
            shownLighting = null;
        }

        private void OnDestroy()
        {
            ClearPartingLighting();
            ClearSceneLighting();
        }
    }
}
