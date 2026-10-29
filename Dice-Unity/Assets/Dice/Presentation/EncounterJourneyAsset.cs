using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    public enum EncounterJourneyBeatKind { Illustration, Arrival, Dialogue, Invitation }
    public enum EncounterJourneyTransition { Fade, PartingPanels }

    [Serializable]
    public sealed class EncounterOutcomePresentation
    {
        [SerializeField] private bool enabled;
        [SerializeField] private SpiritPresentationState hostState;
        [SerializeField] private float holdDuration;
        [SerializeField] private float fadeDuration = .3f;
        [SerializeField] private bool showDialogue;
        [SerializeField] private Texture2D artwork;
        [SerializeField] private AudioClip audioCue;

        public SpiritPresentationState HostState => hostState;
        public float HoldDuration => holdDuration;
        public float FadeDuration => fadeDuration;
        public bool ShowDialogue => showDialogue;
        public Texture2D Artwork => artwork;
        public AudioClip AudioCue => audioCue;
        internal bool Enabled => enabled;

        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(SpiritPresentationState), hostState) ||
                hostState == SpiritPresentationState.Reaction ||
                !FiniteValue.Is(holdDuration) || holdDuration < 0 ||
                !FiniteValue.Is(fadeDuration) || fadeDuration < 0)
                throw new FormatException("Encounter outcome presentation data is invalid.");
        }
    }

    [Serializable]
    public sealed class EncounterJourneyBeat
    {
        [SerializeField] private string id;
        [SerializeField] private EncounterJourneyBeatKind kind;
        [SerializeField] private string title;
        [SerializeField, TextArea] private string text;
        [SerializeField] private string continueLabel;
        [SerializeField] private Texture2D background;
        [SerializeField] private SceneLightingAsset sceneLighting;
        [SerializeField] private Texture2D foreground;
        [SerializeField] private AudioClip audioCue;
        [SerializeField] private float duration = 1;
        [SerializeField] private ProgressionRewardAsset progressionReward;
        [SerializeField] private ModifierAsset charm;
        [SerializeField] private Vector2 motionFrom;
        [SerializeField] private Vector2 motionTo;
        [SerializeField] private Rect foregroundRect = new Rect(0, 0, 1, 1);
        [SerializeField] private Color foregroundTint = Color.white;
        [SerializeField] private EncounterJourneyTransition transition;
        [SerializeField] private float scaleFrom = 1;
        [SerializeField] private float scaleTo = 1;
        [SerializeField] private SpiritPresentationState hostState;
        [SerializeField] private bool hideNarrativeDuringMotion;
        [SerializeField] private bool autoAdvance;
        [SerializeField] private Vector2 backgroundMotionFrom;
        [SerializeField] private Vector2 backgroundMotionTo;
        [SerializeField] private float backgroundScaleFrom = 1;
        [SerializeField] private float backgroundScaleTo = 1;

        public string Id => id;
        public EncounterJourneyBeatKind Kind => kind;
        public string Title => title;
        public string Text => text;
        public string ContinueLabel => continueLabel;
        public Texture2D Background => background;
        public SceneLightingAsset SceneLighting => sceneLighting;
        public Texture2D Foreground => foreground;
        public AudioClip AudioCue => audioCue;
        public float Duration => duration;
        public ProgressionRewardAsset ProgressionReward => progressionReward;
        public ModifierAsset Charm => charm;
        public Vector2 MotionFrom => motionFrom;
        public Vector2 MotionTo => motionTo;
        public Rect ForegroundRect => foregroundRect;
        public Color ForegroundTint => foregroundTint;
        public EncounterJourneyTransition Transition => transition;
        public float ScaleFrom => scaleFrom;
        public float ScaleTo => scaleTo;
        public SpiritPresentationState HostState => hostState;
        public bool HideNarrativeDuringMotion => hideNarrativeDuringMotion;
        public bool AutoAdvance => autoAdvance;
        public Vector2 BackgroundMotionFrom => backgroundMotionFrom;
        public Vector2 BackgroundMotionTo => backgroundMotionTo;
        public float BackgroundScaleFrom => backgroundScaleFrom;
        public float BackgroundScaleTo => backgroundScaleTo;
    }

    /** Authoring data joins the investigation lead, arrival sequence, and encounter presentation. */
    [CreateAssetMenu(menuName = "Dice/Encounter Journey")]
    public sealed class EncounterJourneyAsset : ScriptableObject
    {
        [SerializeField] private EncounterAsset encounter;
        [SerializeField] private string storyId = string.Empty;
        [SerializeField] private string locationTitle;
        [SerializeField] private string spiritName;
        [SerializeField, TextArea] private string leadText;
        [SerializeField] private Texture2D cardArtwork;
        [SerializeField] private EncounterJourneyBeat[] beats = Array.Empty<EncounterJourneyBeat>();
        [SerializeField] private EncounterTablePresentationAsset tablePresentation;
        [SerializeField] private string victoryTitle;
        [SerializeField, TextArea] private string victoryText;
        [SerializeField] private string defeatTitle;
        [SerializeField, TextArea] private string defeatText;
        [SerializeField, TextArea] private string abandonText;
        [Tooltip("Narration above the family list when the encounter introduces its own charms before the first charm offer " +
            "that can contain them; shown in quotation marks without a speaker. Empty shows no narration.")]
        [SerializeField, TextArea] private string encounterCharmsIntroductionText;
        [SerializeField] private string resultContinueLabel;
        [SerializeField] private string completionClueTitle;
        [SerializeField, TextArea] private string completionClueText;
        [SerializeField] private ProgressionRewardAsset completionReward;
        [SerializeField] private EncounterOutcomePresentation victoryPresentation;
        [SerializeField] private EncounterOutcomePresentation defeatPresentation;
        [SerializeField] private EncounterOutcomePresentation abandonPresentation;
        [SerializeField] private EncounterAsset[] followUpEncounters = Array.Empty<EncounterAsset>();
        [SerializeField] private string[] followUpStoryIds = Array.Empty<string>();
        [SerializeField] private EncounterAsset[] requiredCompletedEncounters = Array.Empty<EncounterAsset>();
        [SerializeField] private string[] requiredCompletedStoryIds = Array.Empty<string>();

        public bool IsStoryOnly => !string.IsNullOrEmpty(storyId);
        public string StoryId => storyId;
        /** The encounter the journey leads to; a story journey has none. */
        public EncounterAsset Encounter => encounter;
        public string LocationTitle => locationTitle;
        public string SpiritName => spiritName;
        public string LeadText => leadText;
        public Texture2D CardArtwork => cardArtwork;
        public IReadOnlyList<EncounterJourneyBeat> Beats => beats;
        public EncounterTablePresentationAsset TablePresentation => tablePresentation;
        public string VictoryTitle => victoryTitle;
        public string VictoryText => victoryText;
        public string DefeatTitle => defeatTitle;
        public string DefeatText => defeatText;
        public string AbandonText => abandonText;
        public string EncounterCharmsIntroductionText => encounterCharmsIntroductionText;
        public string ResultContinueLabel => resultContinueLabel;
        public string CompletionClueTitle => completionClueTitle;
        public string CompletionClueText => completionClueText;
        public ProgressionRewardAsset CompletionReward => completionReward;
        /** The encounter leads the board highlights after the journey's encounter is first won. */
        public IReadOnlyList<EncounterAsset> FollowUpEncounters => followUpEncounters;
        /** The story leads the board highlights after the journey's encounter is first won. */
        public IReadOnlyList<string> FollowUpStoryIds => followUpStoryIds;
        public IReadOnlyList<EncounterAsset> RequiredCompletedEncounters => requiredCompletedEncounters;
        public IReadOnlyList<string> RequiredCompletedStoryIds => requiredCompletedStoryIds;

        public EncounterOutcomePresentation GetOutcomePresentation(EncounterOutcome outcome)
        {
            switch (outcome)
            {
                case EncounterOutcome.Won: return victoryPresentation?.Enabled == true ? victoryPresentation : null;
                case EncounterOutcome.Lost: return defeatPresentation?.Enabled == true ? defeatPresentation : null;
                case EncounterOutcome.Abandoned: return abandonPresentation?.Enabled == true ? abandonPresentation : null;
                default: return null;
            }
        }

        public void Validate(MechanicalCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (IsStoryOnly ? !IsStableId(storyId) : encounter == null)
                throw new FormatException("The journey " + name + " requires a valid story ID or an encounter.");
            if (IsStoryOnly && encounter != null)
                throw new FormatException("The story journey " + name + " cannot specify an encounter.");
            var definition = IsStoryOnly ? null : catalog.GetEncounter(encounter.Identity);
            completionReward?.Build(catalog);
            victoryPresentation?.Validate();
            defeatPresentation?.Validate();
            abandonPresentation?.Validate();
            ValidateEncounters(followUpEncounters, "Journey follow-up encounters");
            ValidateStoryIds(followUpStoryIds, "Journey follow-up story IDs");
            ValidateEncounters(requiredCompletedEncounters, "Journey completion prerequisites");
            ValidateStoryIds(requiredCompletedStoryIds, "Journey completion prerequisite IDs");
            foreach (var required in requiredCompletedEncounters) catalog.GetEncounter(required.Identity);
            if (string.IsNullOrWhiteSpace(locationTitle) || string.IsNullOrWhiteSpace(spiritName) ||
                string.IsNullOrWhiteSpace(leadText))
                throw new FormatException("An encounter journey requires its location, host, and investigation lead.");
            if (beats == null || beats.Length == 0) throw new FormatException("An encounter journey requires beats.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int invitations = 0;
            foreach (var beat in beats)
            {
                if (beat == null || !IsStableId(beat.Id) || !ids.Add(beat.Id))
                    throw new FormatException("Journey beat IDs must be nonempty and unique.");
                if (!Enum.IsDefined(typeof(EncounterJourneyBeatKind), beat.Kind) ||
                    !Enum.IsDefined(typeof(SpiritPresentationState), beat.HostState) ||
                    beat.HostState == SpiritPresentationState.Reaction ||
                    !Enum.IsDefined(typeof(EncounterJourneyTransition), beat.Transition) ||
                    !FiniteValue.Is(beat.Duration) || beat.Duration < 0 ||
                    !FiniteValue.Is(beat.ScaleFrom) || beat.ScaleFrom <= 0 ||
                    !FiniteValue.Is(beat.ScaleTo) || beat.ScaleTo <= 0 ||
                    !FiniteValue.Is(beat.BackgroundScaleFrom) || beat.BackgroundScaleFrom <= 0 ||
                    !FiniteValue.Is(beat.BackgroundScaleTo) || beat.BackgroundScaleTo <= 0 ||
                    !FiniteValue.Is(beat.BackgroundMotionFrom) || !FiniteValue.Is(beat.BackgroundMotionTo))
                    throw new FormatException("Journey beat animation data is invalid.");
                if (beat.Kind == EncounterJourneyBeatKind.Invitation) ++invitations;
                beat.SceneLighting?.Validate();
                beat.ProgressionReward?.Build(catalog);
                if (beat.Charm != null) catalog.GetModifier(beat.Charm.Id);
            }
            if (IsStoryOnly && (invitations != 0 || beats[beats.Length - 1].AutoAdvance))
                throw new FormatException("A story journey requires no invitations and an explicit final continue action.");
            if (!IsStoryOnly && (invitations != 1 || beats[beats.Length - 1].Kind != EncounterJourneyBeatKind.Invitation))
                throw new FormatException("The final journey beat must be its only invitation.");
            if (tablePresentation == null) throw new FormatException("An encounter journey requires table presentation.");
            tablePresentation.SceneLighting?.Validate();
            if (tablePresentation.SceneLighting != null &&
                !FiniteValue.Is(tablePresentation.HostLightingPosition))
                throw new FormatException("Journey host lighting position must be finite.");
            if (tablePresentation.RulePresentations == null) throw new FormatException("Journey rule presentations require an array.");
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in tablePresentation.RulePresentations)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.RuleId) || !ruleIds.Add(rule.RuleId))
                    throw new FormatException("Journey rule presentations must be nonnull and unique.");
            }
            if (IsStoryOnly) return;
            if (ruleIds.Count != definition.Rules.Count)
                throw new FormatException("Journey rule presentation does not match the encounter rules.");
            foreach (var rule in definition.Rules)
                if (!ruleIds.Contains(rule.Id)) throw new FormatException("An encounter rule is missing its presentation.");
        }

        private void ValidateEncounters(EncounterAsset[] encounters, string description)
        {
            if (encounters == null) throw new FormatException(description + " require an array.");
            var unique = new HashSet<EncounterAsset>();
            foreach (var value in encounters)
                if (value == null || value == encounter || !unique.Add(value))
                    throw new FormatException(description + " must be present, unique, and different from the journey's encounter.");
        }

        private void ValidateStoryIds(string[] storyIds, string description)
        {
            if (storyIds == null) throw new FormatException(description + " require an array.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in storyIds)
                if (!IsStableId(id) || id == storyId || !ids.Add(id))
                    throw new FormatException(description + " must be valid, unique, and different from the journey.");
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Trim() != value) return false;
            foreach (char character in value) if (char.IsControl(character)) return false;
            return true;
        }
    }
}
