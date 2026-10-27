using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents authored arrival and story beats without creating encounter mechanics. */
    public sealed class EncounterJourneyView : MonoBehaviour
    {
        private IEncounterJourneyControls controls;
        private ISpiritEncounterControls encounterControls;
        [SerializeField] private RectTransform sequence;
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
        [SerializeField] private Text ruleScope;
        [SerializeField] private SpiritInvitationRuleView invitationRulePrefab;
        [SerializeField] private GameObject invitationSection;
        [SerializeField] private RectTransform invitationRulesRoot;
        private readonly List<SpiritInvitationRuleView> invitationRules = new List<SpiritInvitationRuleView>();
        private int invitationRuleCount;
        [SerializeField] private RectTransform charmDetail;
        [SerializeField] private RectTransform charmIconRoot;
        [SerializeField] private Image charmIcon;
        [SerializeField] private SpiritModifierSigilGraphic charmSigil;
        [SerializeField] private Text charmName;
        [SerializeField] private Text charmEffect;
        [SerializeField] private Text charmCondition;
        [SerializeField] private Text journeyStatus;
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button returnButton;
        private Action<SpiritPresentationState, float> hostState;
        [SerializeField] private CanvasGroup narrativeGroup;
        private SpiritEncounterAudio audio;
        private EncounterJourneyAsset renderedJourney;
        private EncounterJourneyCatalogAsset renderedCatalog;
        private EncounterJourneyPhase renderedPhase;
        private bool hasRendered;
        private EncounterJourneyBeat activeBeat;
        private SceneLightingAsset previousSceneLighting;
        private SceneLightingState sceneLightingState = new SceneLightingState();
        private SceneLightingState previousSceneLightingState = new SceneLightingState();
        private SceneLightingMaterial sceneLightingMaterial;
        private SceneLightingMaterial previousSceneLightingMaterial;

        public bool IsAnimating { get; private set; }
        public Text Body => body;
        public RectTransform CharmDetail => charmDetail;
        public Image CharmIcon => charmIcon;
        public SpiritModifierSigilGraphic CharmSigil => charmSigil;
        public Text CharmName => charmName;
        public Text CharmEffect => charmEffect;
        public Text CharmCondition => charmCondition;
        public int VisibleBeatIndex { get; private set; } = -1;
        [field: SerializeField] public SpiritEncounterCharmsScreen EncounterCharms { get; private set; }

        public void Build(SpiritEncounterAudio encounterAudio,
            Action<SpiritPresentationState, float> presentHostState = null)
        {
            hostState = presentHostState;
            audio = encounterAudio;
            advanceButton.onClick.RemoveAllListeners();
            advanceButton.onClick.AddListener(() => controls.AdvanceJourney());
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(ShowEncounterCharms);
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(() => controls.ReturnToInvestigation());
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => controls.SkipIntro());
            EncounterCharms.Build((RectTransform)transform, () => controls.AcceptEncounter());
        }

        private void ShowEncounterCharms()
        {
            var journey = controls?.SelectedJourney;
            if (journey == null || journey.IsStoryOnly || encounterControls?.Catalog == null) return;
            var encounter = encounterControls.Catalog.GetEncounter(journey.EncounterId);
            // Additions first offered after a later stage are introduced inside the encounter instead.
            if (encounter.EncounterModifierIds.Count == 0 || encounter.EncounterModifiersFirstClearedStageNumber != 1)
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
                journeyStatus.text = controls.JourneyStatus;
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
            Texture2D previous = continuingJourney ? background.texture as Texture2D : null;
            var previousLighting = continuingJourney ? activeBeat?.SceneLighting : null;
            CancelAnimation();
            var beat = journey.Beats[index];
            bool parting = beat.Transition == EncounterJourneyTransition.PartingPanels && previous != null;
            background.material = null;
            if (parting)
            {
                previousSceneLighting = previousLighting;
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
            activeBeat = beat;
            title.text = beat.Title;
            body.text = beat.Text;
            bool invitation = !journey.IsStoryOnly && beat.Kind == EncounterJourneyBeatKind.Invitation;
            ConfigureCharm(beat);
            ConfigureInvitation(journey.TablePresentation, invitation);
            narrativeScroll.StopMovement();
            narrativeScroll.verticalNormalizedPosition = 1;
            advanceButton.gameObject.SetActive(!invitation);
            acceptButton.gameObject.SetActive(invitation);
            SpiritUi.SetButtonLabel(invitation ? acceptButton : advanceButton,
                string.IsNullOrWhiteSpace(beat.ContinueLabel) ? invitation ? "Accept" : "Continue" : beat.ContinueLabel);
            skipButton.gameObject.SetActive(controls.CanSkipIntro && !invitation);
            var backgroundTexture = beat.SceneLighting != null ? beat.SceneLighting.BaseTexture : beat.Background;
            background.texture = backgroundTexture;
            background.color = backgroundTexture != null ? Color.white : Color.clear;
            foreground.texture = beat.Foreground;
            foreground.GetComponent<AspectRatioFitter>().aspectRatio = beat.Foreground != null ?
                (float)beat.Foreground.width / beat.Foreground.height : 1;
            foreground.color = beat.Foreground != null ? beat.ForegroundTint : Color.clear;
            var clip = (RectTransform)foreground.transform.parent;
            clip.anchorMin = beat.ForegroundRect.min;
            clip.anchorMax = beat.ForegroundRect.max;
            clip.offsetMin = clip.offsetMax = Vector2.zero;
            leftPanel.gameObject.SetActive(parting);
            rightPanel.gameObject.SetActive(parting);
            leftPanel.texture = rightPanel.texture = previous;
            leftPanel.material = rightPanel.material = previousSceneLightingMaterial?.Material;
            if (beat.AudioCue != null) audio?.PlayJourneyCue(beat.AudioCue);
            StartCoroutine(AnimateBeat(journey, index, parting));
        }

        private void ConfigureCharm(EncounterJourneyBeat beat)
        {
            bool visible = beat.Charm != null && encounterControls?.Catalog != null;
            charmDetail.gameObject.SetActive(visible);
            if (!visible) return;
            var definition = encounterControls.Catalog.GetModifier(beat.Charm.Id);
            var charms = encounterControls.ModifierPresentation;
            var theme = SpiritUi.GetTheme(transform);
            string name = charms.GetName(definition);
            body.text = body.text.Replace("{charm}", "<b><color=#" + ColorUtility.ToHtmlStringRGB(theme.amber) +
                ">" + SpiritModifierFamilyRow.GetFamilyName(charms, definition) + "</color></b>");
            charmName.text = name;
            charmEffect.text = "<b>" + charms.GetEffectText(definition) + "</b>";
            charmEffect.color = charms.GetEffectColor(definition);
            charmCondition.text = SpiritRewardCard.GetModifierDescription(charms, definition, theme.amber,
                theme.GetModifierTriggerColor(definition.Phase));
            charmIcon.sprite = charms.GetIcon(definition);
            charmIcon.color = charmSigil.color = charms.GetEffectColor(definition);
            charmIcon.gameObject.SetActive(charmIcon.sprite != null);
            charmSigil.Seed = charms.GetSigilSeed(definition);
            charmSigil.gameObject.SetActive(charmIcon.sprite == null);
        }

        private void ConfigureInvitation(EncounterTablePresentationAsset table, bool invitation)
        {
            invitationRuleCount = 0;
            if (invitation && table != null)
            {
                foreach (var rule in table.RulePresentations)
                {
                    if (rule == null) continue;
                    int index = invitationRuleCount++;
                    if (index == invitationRules.Count)
                        invitationRules.Add(Instantiate(invitationRulePrefab, invitationRulesRoot, false));
                    invitationRules[index].Render(rule.DisplayName, rule.Description, rule.Icon,
                        table.HostPresentation != null ? table.HostPresentation.SigilPrefab : null);
                }
            }
            bool showRules = invitationRuleCount > 0;
            ruleHeading.text = invitationRuleCount > 1 ? "ENCOUNTER RULES" : "ENCOUNTER RULE";
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

        private IEnumerator AnimateBeat(EncounterJourneyAsset journey, int index, bool parting)
        {
            var beat = journey.Beats[index];
            IsAnimating = true;
            SetButtons();
            float duration = Mathf.Max(0, beat.Duration);
            float age = 0;
            // Let Refresh finish recording this beat before an immediate continuation can refresh it.
            if (duration == 0 && beat.AutoAdvance)
            {
                ApplyMotion(beat, 0, parting);
                yield return null;
            }
            while (age < duration)
            {
                ApplyMotion(beat, duration > 0 ? age / duration : 1, parting);
                age += Time.unscaledDeltaTime;
                yield return null;
            }
            if (controls == null || controls.JourneyPhase != EncounterJourneyPhase.Sequence ||
                controls.SelectedJourney != journey || controls.BeatIndex != index || activeBeat != beat) yield break;
            ApplyMotion(beat, 1, parting);
            leftPanel.gameObject.SetActive(false);
            rightPanel.gameObject.SetActive(false);
            ClearPartingLighting();
            IsAnimating = false;
            if (beat.AutoAdvance && index + 1 < journey.Beats.Count)
            {
                controls.AdvanceJourney();
                yield break;
            }
            narrativeGroup.alpha = 1;
            SetButtons();
        }

        private void ApplyMotion(EncounterJourneyBeat beat, float value, bool parting)
        {
            float eased = Mathf.SmoothStep(0, 1, value);
            hostState?.Invoke(beat.HostState, value);
            narrativeGroup.alpha = beat.HideNarrativeDuringMotion ?
                beat.AutoAdvance ? 0 : Mathf.Clamp01((value - .9f) * 10f) : 1;
            background.rectTransform.anchoredPosition = Vector2.Scale(
                Vector2.Lerp(beat.BackgroundMotionFrom, beat.BackgroundMotionTo, eased), artwork.rect.size);
            background.rectTransform.localScale = Vector3.one * Mathf.Lerp(beat.BackgroundScaleFrom, beat.BackgroundScaleTo, eased);
            var rect = foreground.rectTransform;
            rect.anchoredPosition = Vector2.Scale(Vector2.Lerp(beat.MotionFrom, beat.MotionTo, eased), artwork.rect.size);
            rect.localScale = Vector3.one * Mathf.Lerp(beat.ScaleFrom, beat.ScaleTo, eased);
            illustrationGroup.alpha = 1;
            if (beat.Foreground != null)
            {
                Color tint = beat.ForegroundTint;
                tint.a *= parting || value >= 1 ? 1 : Mathf.Clamp01(value * beat.Duration / .25f);
                foreground.color = tint;
            }
            float distance = artwork.rect.width * .54f * eased;
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
            ApplyMotion(activeBeat, 1, false);
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

        private void OnDisable()
        {
            CancelAnimation();
            ClearSceneLighting();
            hasRendered = false;
        }

        private void LateUpdate()
        {
            double time = Time.unscaledTimeAsDouble;
            var asset = activeBeat?.SceneLighting;
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
                var texture = asset != null ? asset.BaseTexture : activeBeat?.Background;
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
        }

        private void OnDestroy()
        {
            ClearPartingLighting();
            ClearSceneLighting();
        }
    }
}
