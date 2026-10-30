using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
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

    /**
     * The encounter played in the current table scene, with its endings. It directly follows its invitation; a win
     * continues with the next beat, and the journey completes at the win when this is its last beat.
     */
    [Serializable]
    public sealed class EncounterBeat : EncounterJourneyBeat
    {
        [SerializeField] private EncounterAsset encounter;
        [SerializeField] private string victoryTitle;
        [SerializeField, TextArea] private string victoryText;
        [SerializeField] private string defeatTitle;
        [SerializeField, TextArea] private string defeatText;
        [SerializeField, TextArea] private string abandonText;
        [SerializeField] private string resultContinueLabel;
        [SerializeField] private EncounterOutcomePresentation victoryPresentation;
        [SerializeField] private EncounterOutcomePresentation defeatPresentation;
        [SerializeField] private EncounterOutcomePresentation abandonPresentation;
        [Tooltip("Narration above the family list when the encounter introduces its own charms before the first charm offer " +
            "that can contain them; shown in quotation marks without a speaker. Empty shows no narration.")]
        [SerializeField, TextArea] private string encounterCharmsIntroductionText;

        public EncounterAsset Encounter => encounter;
        public string VictoryTitle => victoryTitle;
        public string VictoryText => victoryText;
        public string DefeatTitle => defeatTitle;
        public string DefeatText => defeatText;
        public string AbandonText => abandonText;
        public string ResultContinueLabel => resultContinueLabel;
        public string EncounterCharmsIntroductionText => encounterCharmsIntroductionText;

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

        internal override void Validate(EncounterJourneyAsset journey, int index, MechanicalCatalog catalog)
        {
            base.Validate(journey, index, catalog);
            if (index == 0 || !(journey.Beats[index - 1] is InvitationBeat))
                throw Invalid(journey, index, "must directly follow an Invitation beat");
            var table = journey.SceneAt(index).Table;
            if (table == null) throw Invalid(journey, index, "must be in a table scene");
            if (encounter == null) throw Invalid(journey, index, "requires an encounter");
            var definition = catalog.GetEncounter(encounter.Identity);
            victoryPresentation?.Validate();
            defeatPresentation?.Validate();
            abandonPresentation?.Validate();
            var ruleIds = new HashSet<EncounterRuleDefinition.Id>();
            foreach (var rule in table.RulePresentations) ruleIds.Add(rule.RuleId);
            bool matches = ruleIds.Count == definition.Rules.Count;
            foreach (var rule in definition.Rules) matches &= ruleIds.Contains(rule.Identity);
            if (!matches)
                throw Invalid(journey, index, "plays in the table scene " + table.name + ", whose rule cards do not match the rules of " +
                    encounter.name);
            foreach (var rule in definition.Rules)
                EncounterRulePresentationAsset.For(table.RulePresentations, rule.Identity).ValidateText(rule);
        }
    }
}
