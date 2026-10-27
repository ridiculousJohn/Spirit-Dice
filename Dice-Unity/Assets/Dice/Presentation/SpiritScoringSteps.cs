using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * One held state of score playback: what the score counters, labels, dice tray, modifier history and action cues show
     * once the step's counters have finished animating.
     */
    public sealed class SpiritScoringStep
    {
        /** The base score counter at the end of the step. */
        public long BaseScore { get; internal set; }
        /** The multiplier counter at the end of the step. */
        public long Multiplier { get; internal set; }
        /** The total counter at the end of the step; zero until the step that reveals the hand score. */
        public long Total { get; internal set; }
        /** True when the step animates the total counter instead of the base score and multiplier counters. */
        public bool RevealsTotal { get; internal set; }
        /** The score detail line, such as "STEP 1 OF 4 - DICE". */
        public string StepLabel { get; internal set; }
        public string CategoryText { get; internal set; }
        public string SelectionText { get; internal set; }
        public Color SelectionColor { get; internal set; }
        /** The dice tray's scoring highlight mask. */
        public int HighlightMask { get; internal set; }
        /** The index of the resolving token among the hand's PlayHand contributions, or -1 when no token resolves. */
        public int ResolvingIndex { get; internal set; }
        /** The modifier contribution the step presents, or null when it presents none. */
        public ModifierContribution? Contribution { get; internal set; }
        /** The charm whose activation the step presents, or null; its accent colours the step's counter highlights. */
        public ModifierDefinition Charm { get; internal set; }
        /** The charm whose emblem is shown during the step, or null when the emblem is hidden. */
        public ModifierDefinition Emblem { get; internal set; }
        /** Whether the charm's action cue marks the reroll button. */
        public bool RerollCue { get; internal set; }
        /** Whether the charm's action cue marks the play button. */
        public bool PlayCue { get; internal set; }
        /** Whether the encounter rule's host cue marks the play button. */
        public bool RuleCue { get; internal set; }
        /** Whether the host reacts as the step begins. */
        public bool HostReacts { get; internal set; }
        public bool HighlightsBaseScore { get; internal set; }
        public bool HighlightsMultiplier { get; internal set; }
        public bool HighlightsRerolls { get; internal set; }
        public bool HighlightsHands { get; internal set; }
        /** The charm's resource changes, shown on the resource counters as the step begins. */
        public IReadOnlyList<ResourceChangedEvent> Grants { get; internal set; } = Array.Empty<ResourceChangedEvent>();
        /** Real seconds the step is held after its counters finish animating. */
        public float Hold { get; internal set; }
    }

    /**
     * Builds the ordered steps that present a scored hand - dice, hand rank, each PlayHand charm in activation order, the
     * encounter rule bonus and the total - and the step that presents a banked roll bonus. Charm names, colours and
     * contribution wording come from the charm presentation.
     */
    public sealed class SpiritScoringStepBuilder
    {
        public const float ScoreStepHold = .35f;
        public const float ModifierStepHold = .55f;
        public const float TotalHold = .55f;
        private readonly MechanicalCatalog catalog;
        private readonly ModifierPresentationCatalog charms;
        private readonly SpiritUiTheme theme;

        public SpiritScoringStepBuilder(MechanicalCatalog catalog, ModifierPresentationCatalog charms, SpiritUiTheme theme)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.charms = charms ?? throw new ArgumentNullException(nameof(charms));
            this.theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
        }

        /**
         * The steps of a scored hand, starting from the counters' banked base score and multiplier. Events are the
         * resolution's events, searched for the resource changes of each charm in the hand's stage and hand index; rule is
         * the encounter rule presentation, or null when the encounter shows none.
         */
        public IReadOnlyList<SpiritScoringStep> BuildHand(ScoreBreakdown score, DiceValues dice, long startingBaseScore,
            long startingMultiplier, EncounterRulePresentationAsset rule, IReadOnlyList<RuleEvent> events, int stageIndex,
            int handIndex)
        {
            if (score == null) throw new ArgumentNullException(nameof(score));
            if (events == null) throw new ArgumentNullException(nameof(events));
            var steps = new List<SpiritScoringStep>();
            long baseScore = checked(startingBaseScore + score.DiceSum);
            long multiplier = startingMultiplier;
            steps.Add(new SpiritScoringStep
            {
                BaseScore = baseScore,
                Multiplier = multiplier,
                StepLabel = "STEP 1 OF 4 - DICE",
                CategoryText = "COUNTING DICE",
                SelectionText = "DICE\n" + charms.GetContributionText(score.DiceSum, 0, 0),
                SelectionColor = theme.muted,
                HighlightMask = DiceValues.AllMask,
                ResolvingIndex = -1,
                HostReacts = true,
                Hold = ScoreStepHold
            });

            long handBase = checked(score.CategoryBaseScore + score.UpgradeBaseScore);
            long handMultiplier = checked(score.CategoryMultiplier + score.UpgradeMultiplier);
            baseScore = checked(baseScore + handBase);
            multiplier = checked(multiplier + handMultiplier);
            string category = DiagnosticText.Humanize(score.Category.ToString()).ToUpperInvariant();
            steps.Add(new SpiritScoringStep
            {
                BaseScore = baseScore,
                Multiplier = multiplier,
                StepLabel = "STEP 2 OF 4 - HAND RANK",
                CategoryText = category,
                SelectionText = category + " / LEVEL " + score.UpgradeCount + "\n" +
                    charms.GetContributionText(handBase, handMultiplier, 0),
                SelectionColor = theme.muted,
                HighlightMask = SpiritHandScoringHighlights.GetMask(dice, score.Category),
                ResolvingIndex = -1,
                Hold = ScoreStepHold
            });

            // Recorded contributions retain the eligibility and activation order of the engine phase.
            ModifierDefinition emblem = null;
            int readyIndex = 0;
            foreach (var contribution in score.ModifierContributions)
            {
                if (contribution.Phase != ModifierPhase.PlayHand) continue;
                var step = BuildContribution(contribution, false, readyIndex++, baseScore, multiplier, events, stageIndex,
                    handIndex);
                baseScore = step.BaseScore;
                multiplier = step.Multiplier;
                emblem = step.Emblem;
                steps.Add(step);
            }

            if (score.EncounterRuleMultiplier != 0)
            {
                multiplier = checked(multiplier + score.EncounterRuleMultiplier);
                steps.Add(new SpiritScoringStep
                {
                    BaseScore = baseScore,
                    Multiplier = multiplier,
                    StepLabel = "STEP 3 OF 4 - ENCOUNTER RULE",
                    CategoryText = "ENCOUNTER RULE",
                    SelectionText = (rule != null ? rule.DisplayName : "ENCOUNTER RULE") +
                        "\n+" + DiagnosticText.Number(score.EncounterRuleMultiplier) + " multiplier",
                    SelectionColor = theme.amber,
                    ResolvingIndex = -1,
                    // The rule bonus is shown beside the last charm's emblem.
                    Emblem = emblem,
                    RuleCue = true,
                    HostReacts = true,
                    Hold = ScoreStepHold
                });
            }

            steps.Add(new SpiritScoringStep
            {
                BaseScore = baseScore,
                Multiplier = multiplier,
                Total = score.HandScore,
                RevealsTotal = true,
                StepLabel = "STEP 4 OF 4 - " + DiagnosticText.Number(score.BaseScore) + " x " +
                    DiagnosticText.Number(score.Multiplier),
                CategoryText = "FINAL SCORE",
                SelectionText = "TOTAL",
                SelectionColor = theme.muted,
                ResolvingIndex = -1,
                Hold = TotalHold
            });
            return steps;
        }

        /**
         * The step of a roll bonus banked by a charm outside the PlayHand phase, starting from the counters' current base
         * score and multiplier. Events are searched for the charm's resource changes in the given stage and hand index.
         */
        public SpiritScoringStep BuildBankedContribution(ModifierContribution contribution, long startingBaseScore,
            long startingMultiplier, IReadOnlyList<RuleEvent> events, int stageIndex, int handIndex)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            return BuildContribution(contribution, true, -1, startingBaseScore, startingMultiplier, events, stageIndex, handIndex);
        }

        private SpiritScoringStep BuildContribution(ModifierContribution contribution, bool banked, int resolvingIndex,
            long startingBaseScore, long startingMultiplier, IReadOnlyList<RuleEvent> events, int stageIndex, int handIndex)
        {
            var definition = catalog.GetModifier(contribution.DefinitionId);
            string name = charms.GetName(definition);
            var grants = new List<ResourceChangedEvent>();
            bool grantsRerolls = false;
            bool grantsHands = false;
            foreach (var ruleEvent in events)
                if (ruleEvent is ResourceChangedEvent resource && resource.Cause == ResourceChangeCause.ModifierEffect &&
                    resource.StageIndex == stageIndex && resource.HandIndex == handIndex &&
                    resource.SourceInstanceId == contribution.InstanceId)
                {
                    grants.Add(resource);
                    if (resource.After == resource.Before) continue;
                    grantsRerolls |= resource.Resource == ResourceKind.Rerolls;
                    grantsHands |= resource.Resource == ResourceKind.Hands;
                }
            bool showReroll = contribution.Rerolls != 0 || grantsRerolls;
            bool showPlay = grantsHands;
            if (contribution.BaseScore != 0 || contribution.Multiplier != 0 || (!showReroll && !showPlay))
            {
                showPlay |= definition.Phase == ModifierPhase.PlayHand;
                showReroll |= definition.Phase != ModifierPhase.PlayHand;
            }
            return new SpiritScoringStep
            {
                BaseScore = checked(startingBaseScore + contribution.BaseScore),
                Multiplier = checked(startingMultiplier + contribution.Multiplier),
                StepLabel = banked ? "ROLL BONUS" : "STEP 3 OF 4 - CHARMS",
                CategoryText = name,
                SelectionText = name + "\n" +
                    charms.GetContributionText(contribution.BaseScore, contribution.Multiplier, contribution.Rerolls) +
                    (banked ? " (banked)" : " (on play)"),
                SelectionColor = charms.GetEffectColor(definition),
                ResolvingIndex = resolvingIndex,
                Contribution = contribution,
                Charm = definition,
                Emblem = definition,
                RerollCue = showReroll,
                PlayCue = showPlay,
                HighlightsBaseScore = contribution.BaseScore != 0,
                HighlightsMultiplier = contribution.Multiplier != 0,
                HighlightsRerolls = grantsRerolls,
                HighlightsHands = grantsHands,
                Grants = grants,
                Hold = ModifierStepHold
            };
        }
    }
}
