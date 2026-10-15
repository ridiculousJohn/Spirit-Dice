using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.Serialization;

namespace Dice.Presentation
{
    public enum EncounterJourneyBeatKind { Illustration, Arrival, Dialogue, Invitation }
    public enum EncounterJourneyTransition { Fade, PartingPanels }

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
        [SerializeField] private Vector2 motionFrom;
        [SerializeField] private Vector2 motionTo;
        [SerializeField] private Rect foregroundRect = new Rect(0, 0, 1, 1);
        [SerializeField] private Color foregroundTint = Color.white;
        [SerializeField] private EncounterJourneyTransition transition;
        [SerializeField] private float scaleFrom = 1;
        [SerializeField] private float scaleTo = 1;
        [SerializeField, FormerlySerializedAs("hostPose")] private SpiritPresentationState hostState;
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
        [SerializeField] private string encounterId;
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
        [SerializeField] private string resultContinueLabel;
        [SerializeField] private string completionClueTitle;
        [SerializeField, TextArea] private string completionClueText;

        public string EncounterId => encounterId;
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
        public string ResultContinueLabel => resultContinueLabel;
        public string CompletionClueTitle => completionClueTitle;
        public string CompletionClueText => completionClueText;

        public void Validate(MechanicalCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!IsStableId(encounterId)) throw new FormatException("A journey requires a valid encounter ID.");
            var definition = catalog.GetEncounter(encounterId);
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
                    float.IsNaN(beat.Duration) || float.IsInfinity(beat.Duration) || beat.Duration < 0 ||
                    float.IsNaN(beat.ScaleFrom) || float.IsInfinity(beat.ScaleFrom) || beat.ScaleFrom <= 0 ||
                    float.IsNaN(beat.ScaleTo) || float.IsInfinity(beat.ScaleTo) || beat.ScaleTo <= 0 ||
                    float.IsNaN(beat.BackgroundScaleFrom) || float.IsInfinity(beat.BackgroundScaleFrom) || beat.BackgroundScaleFrom <= 0 ||
                    float.IsNaN(beat.BackgroundScaleTo) || float.IsInfinity(beat.BackgroundScaleTo) || beat.BackgroundScaleTo <= 0 ||
                    !Finite(beat.BackgroundMotionFrom) || !Finite(beat.BackgroundMotionTo))
                    throw new FormatException("Journey beat animation data is invalid.");
                if (beat.Kind == EncounterJourneyBeatKind.Invitation) ++invitations;
                beat.SceneLighting?.Validate();
            }
            if (invitations != 1 || beats[beats.Length - 1].Kind != EncounterJourneyBeatKind.Invitation)
                throw new FormatException("The final journey beat must be its only invitation.");
            if (tablePresentation == null) throw new FormatException("An encounter journey requires table presentation.");
            tablePresentation.SceneLighting?.Validate();
            if (tablePresentation.SceneLighting != null &&
                (!SceneLightingAsset.Finite(tablePresentation.HostLightingPosition.x) ||
                 !SceneLightingAsset.Finite(tablePresentation.HostLightingPosition.y) ||
                 !SceneLightingAsset.Finite(tablePresentation.HostLightingPosition.z)))
                throw new FormatException("Journey host lighting position must be finite.");
            if (tablePresentation.RulePresentations == null) throw new FormatException("Journey rule presentations require an array.");
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in tablePresentation.RulePresentations)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.RuleId) || !ruleIds.Add(rule.RuleId))
                    throw new FormatException("Journey rule presentations must be nonnull and unique.");
            }
            if (ruleIds.Count != definition.Rules.Count)
                throw new FormatException("Journey rule presentation does not match the encounter rules.");
            foreach (var rule in definition.Rules)
                if (!ruleIds.Contains(rule.Id)) throw new FormatException("An encounter rule is missing its presentation.");
        }

        private static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Trim() != value) return false;
            foreach (char character in value) if (char.IsControl(character)) return false;
            return true;
        }
    }
}
