using System;
using System.Globalization;
using System.Text;
using Dice.Engine;

namespace Dice.Presentation
{
    public static class DiagnosticText
    {
        public static string Humanize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var text = new StringBuilder(value.Length + 12);
            for (int i = 0; i < value.Length; ++i)
            {
                char current = value[i];
                if (current == '_' || current == '-' || current == '.')
                {
                    if (text.Length > 0 && text[text.Length - 1] != ' ') text.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(current) &&
                    (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]) ||
                     (i + 1 < value.Length && char.IsLower(value[i + 1]) && char.IsUpper(value[i - 1]))))
                    text.Append(' ');
                text.Append(current);
            }
            return text.ToString().Trim();
        }

        public static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        public static string DescribeInspectionStatus(ModifierInspectionStatus status)
        {
            switch (status)
            {
                case ModifierInspectionStatus.WouldActivate: return "would activate";
                case ModifierInspectionStatus.ConditionNotMet: return "condition not met";
                case ModifierInspectionStatus.ActivationLimitReached: return "activation limit reached";
                case ModifierInspectionStatus.DifferentPhase: return "evaluated on roll";
                case ModifierInspectionStatus.EncounterNotPlaying: return "encounter is not playing";
                default: return Humanize(status.ToString());
            }
        }

        public static string DescribeContribution(ModifierContribution contribution)
            => "+" + Number(contribution.BaseScore) + " base, +" + Number(contribution.Multiplier) +
               " multiplier, +" + Number(contribution.Rerolls) + " rerolls";

        public static string DescribeScore(ScoreBreakdown score)
        {
            if (score == null) return "Score unavailable.";
            return Humanize(score.Category.ToString()) + " (upgrade level " + score.UpgradeCount + ")\n" +
                "Base: dice " + Number(score.DiceSum) + " + category " + Number(score.CategoryBaseScore) +
                " + upgrades " + Number(score.UpgradeBaseScore) + " + modifiers " + Number(score.ModifierBaseScore) +
                " = " + Number(score.BaseScore) + "\nMultiplier: category " + Number(score.CategoryMultiplier) +
                " + upgrades " + Number(score.UpgradeMultiplier) + " + modifiers " + Number(score.ModifierMultiplier) +
                " = " + Number(score.Multiplier) + "\nFinal score: " + Number(score.BaseScore) + " x " +
                Number(score.Multiplier) + " = " + Number(score.HandScore);
        }

        public static string DescribeDice(DiceValues dice)
            => dice[0] + "  " + dice[1] + "  " + dice[2] + "  " + dice[3] + "  " + dice[4];

        /** Names charms by their presentation; hand upgrades and other values by their engine names. */
        public static string DescribeEvent(RuleEvent ruleEvent, MechanicalCatalog catalog, ModifierPresentationCatalog charms)
        {
            if (ruleEvent == null) return "";
            string Charm(string id) => charms.GetName(catalog.GetModifier(id));
            string prefix = "Stage " + (ruleEvent.StageIndex + 1) + " / hand " + (ruleEvent.HandIndex + 1) + " - ";
            switch (ruleEvent)
            {
                case StageStartedEvent stage:
                    return prefix + "Started. Target " + Number(stage.Threshold) + ", " + stage.Hands + " hands, " + stage.Rerolls + " rerolls.";
                case HandStartedEvent hand:
                    return prefix + "Started. " + hand.HandsRemaining + " hands and " + hand.RerollsRemaining + " rerolls remain.";
                case DiceRolledEvent rolled:
                    return prefix + Humanize(rolled.Kind.ToString()) + ": [" + DescribeDice(rolled.After) +
                        "]. Rerolled mask " + rolled.SelectedMask + ", changed mask " + rolled.ChangedMask + ".";
                case HandScoredEvent scored:
                    var text = new StringBuilder(prefix).Append("Played [").Append(DescribeDice(scored.Dice)).Append("]\n")
                        .Append(DescribeScore(scored.Score));
                    foreach (ModifierContribution contribution in scored.Score.ModifierContributions)
                        text.Append("\n  ").Append(Charm(contribution.DefinitionId)).Append(" #")
                            .Append(contribution.InstanceId).Append(": ").Append(DescribeContribution(contribution));
                    return text.ToString();
                case ThresholdChangedEvent threshold:
                    return prefix + "Target remaining " + Number(threshold.Before) + " -> " + Number(threshold.After) +
                        "; applied " + Number(threshold.AppliedScore) + ", excess " + Number(threshold.ExcessScore) + ".";
                case ResourceChangedEvent resource:
                    return prefix + Humanize(resource.Resource.ToString()) + " " + resource.Before + " -> " + resource.After +
                        " (" + Humanize(resource.Cause.ToString()) + ").";
                case ModifierActivatedEvent activated:
                    return prefix + Charm(activated.DefinitionId) + " #" + activated.InstanceId + " activated on " +
                        Humanize(activated.Phase.ToString()) + ": " + DescribeContribution(activated.Contribution) + ".";
                case StageClearedEvent cleared:
                    return prefix + "Cleared. " + cleared.HandsRemaining + " hands and " + cleared.RerollsRemaining + " rerolls remain.";
                case RewardOfferedEvent offered:
                    return prefix + (offered.IsRefresh ? "Refreshed " : "Offered ") + Humanize(offered.Offer.Kind.ToString()) +
                        " choices for stage " + (offered.Offer.TargetStageIndex + 1) + " (offer " + offered.Offer.OfferId + ").";
                case RewardSelectedEvent selected:
                    return prefix + "Selected " + (selected.Kind == RewardKind.HandUpgrade ? Humanize(selected.Option.DefinitionId)
                        : Charm(selected.Option.DefinitionId)) + " (offer " + selected.OfferId +
                        ", option " + selected.Option.OptionId + ").";
                case RewardSkippedEvent skipped:
                    return prefix + "Skipped " + Humanize(skipped.Kind.ToString()) + "; no eligible options.";
                case ModifierAcquiredEvent acquired:
                    return prefix + "Acquired " + Charm(acquired.DefinitionId) + " #" + acquired.InstanceId + ".";
                case HandUpgradedEvent upgraded:
                    return prefix + Humanize(upgraded.Category.ToString()) + " upgraded " + upgraded.Before + " -> " + upgraded.After + ".";
                case EncounterCompletedEvent completed:
                    return prefix + Humanize(completed.Outcome.ToString()) + ". Total score " + Number(completed.TotalScore) +
                        "; " + completed.HandsPlayed + " hands played, " + completed.RerollsSpent + " rerolls spent.";
                case DieMarkedEvent marked: return prefix + "Die " + (marked.Bay + 1) + " marked for binding.";
                case DieBoundEvent bound: return prefix + "Die " + (bound.Bay + 1) + " bound at " + bound.Face + ".";
                case RewardsRequiredEvent _: return prefix + "Rewards required before the next stage.";
                default: return prefix + Humanize(ruleEvent.GetType().Name);
            }
        }
    }
}
