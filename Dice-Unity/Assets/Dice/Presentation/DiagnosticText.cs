using System;
using System.Globalization;
using System.Reflection;
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

        public static string DescribeCondition(IModifierCondition condition)
        {
            if (condition == null) return "No condition supplied.";
            string type = condition.GetType().Name;
            switch (condition)
            {
                case NoDiceRerolledCondition _: return "No rerolls spent on this hand.";
                case ContainsFaceCondition value: return "At least one die shows " + value.Parameter + ".";
                case FavoredFaceCountAtLeastCondition value: return "At least " + value.Parameter + " dice show the favored face.";
                case ExactlyOneSelectedFaceCondition value: return "Exactly one rerolled die shows " + value.Parameter + ".";
                case OutOfRerollsCondition _: return "No rerolls remain.";
                case RangeLessThanCondition value: return "Largest minus smallest face is less than " + value.Parameter + ".";
                case SumLessThanInitialCondition _: return "Dice sum is below the initial roll's sum.";
                case LastHandCondition _: return "One hand remains in the evaluation context.";
                case SelectedFaceUnchangedCondition _: return "At least one rerolled die kept its previous face.";
                case SelectedMatchingGroupAtLeastCondition value: return "At least " + value.Parameter + " rerolled dice share one face.";
                case ContainsUpperFaceCondition value: return "At least one die shows " + value.Parameter + ".";
                case RankBelowPreviousCondition _: return "Category ranks below the previous played hand (High Die if none).";
                case RankAbovePreviousCondition _: return "Category ranks above the previous played hand (High Die if none).";
                case RankEqualsPreviousCondition _: return "Category matches the previous played hand (High Die if none).";
                case RankEqualsBeforeRerollCondition _: return "Category matches the roll before the latest reroll.";
                case SumDivisibleByCondition value: return "Dice sum is divisible by " + value.Parameter + ".";
                case EdgePairSumsEqualCondition _: return "Dice 1 + 2 equal dice 4 + 5.";
                case OrderedCondition _: return "Faces are in ascending or descending order; ties allowed.";
                case HighDieCondition _: return "Category is High Die.";
                case AdjacentDifferentCondition _: return "Every adjacent pair has different faces.";
                case AdjacentMatchingTripleCondition _: return "Three consecutive dice show the same face.";
                case ExactlyOneFaceCondition value: return "Exactly one die shows " + value.Parameter + ".";
                case ContainsRerollsRemainingCondition _: return "A die face equals rerolls remaining.";
                case SelectedMatchesHandsRemainingCondition _: return "A rerolled die face equals hands remaining in the evaluation context.";
                case AdjacentDifferenceAtLeastCondition value: return "Every adjacent face difference is at least " + value.Parameter + ".";
                case PositiveRerollsDivisibleByCondition value: return "Rerolls remaining are positive and divisible by " + value.Parameter + ".";
                case MiddleUniqueCondition _: return "Die 3 has a face no other die shows.";
                case HighDieOrStraightCondition _: return "Category is High Die or Straight.";
                case AlternatingParityCondition _: return "Odd and even faces alternate across all five dice.";
                case InteriorEqualsNeighborSumCondition _: return "An interior die equals the sum of its two neighbors.";
                case SelectedInteriorEqualsNeighborSumCondition _: return "A rerolled interior die equals the sum of its two neighbors.";
                case EveryDieRerolledCondition _: return "Every die has been rerolled at least once this hand.";
                case ExactlyOneSelectedMatchesSelectedCountCondition _: return "Exactly one rerolled die face equals the number of dice rerolled.";
                case MinimumEqualsRerollsSpentCondition _: return "Smallest face equals rerolls spent on this hand.";
                case RerollsExceedHandsRemainingCondition _: return "Rerolls remaining exceed hands remaining + 1 in the evaluation context.";
                case MaximumAtMostRerollsRemainingCondition _: return "Largest face is at most rerolls remaining.";
                case LargestGroupEqualsHandsOrRerollsCondition _: return "Largest matching group size equals hands remaining + 1 or rerolls remaining.";
                case EdgeSumEqualsEitherCondition value: return "Dice 1 + 5 equal " + value.Parameter + " or " + value.SecondaryParameter + ".";
                case FaceEqualsPositionCondition _: return "At least one face equals its die position (1 through 5).";
                case FirstHandCondition _: return "No hands have been played in this stage.";
                case ExtremeEqualsHandsRemainingCondition _: return "Smallest or largest face equals hands remaining + 1 in the evaluation context.";
                case SumLessThanCondition value: return "Dice sum is less than " + value.Parameter + ".";
                case TwoPairCondition _: return "Category is Two Pair.";
                case SelectedSumEqualsEitherCondition value: return "Rerolled dice sum to " + value.Parameter + " or " + value.SecondaryParameter + "; at least one die was rerolled.";
                case RerollsSpentAtLeastCondition value: return "At least " + value.Parameter + " rerolls were spent on this hand.";
                case AdjacentDifferenceAtMostCondition value: return "Every adjacent face difference is at most " + value.Parameter + ".";
                case EdgeSumGreaterThanInteriorCondition _: return "Dice 1 + 5 exceed the sum of dice 2 + 3 + 4.";
                case InitialPairCondition _: return "The initial roll's category is Pair.";
                case InitialRankAbovePreviousCondition _: return "Initial roll category ranks above the previous played hand; requires a previous hand.";
                case RangeGreaterThanCondition value: return "Largest minus smallest face exceeds " + value.Parameter + ".";
                case PreviousStageExhaustedHandsCondition _: return "First hand of this stage, and the previous stage used every hand.";
                case SameParityCondition _: return "All faces are odd, or all faces are even.";
                case InteriorSumDivisibleByCondition value: return "Dice 2 + 3 + 4 sum to a multiple of " + value.Parameter + ".";
                case EdgeSumEqualsHandsOrRerollsCondition _: return "Dice 1 + 5 equal hands remaining + 1 or rerolls remaining.";
                case PreviousStageAtMostHalfHandsPlayedCondition _: return "First hand of this stage; previous stage used at most half its hands.";
                case LastRerollOnLastHandCondition _: return "One hand and zero rerolls remain in the evaluation context.";
                case RankBelowBeforeRerollCondition _: return "Category ranks below the roll before the latest reroll.";
                case NeverCondition _: return "Never activates.";
                case ThreeSmallestSumAtMostMaximumCondition _: return "Sum of the three smallest faces is at most the largest face.";
                case SecondFourthProductAtLeastSumCondition _: return "Die 2 times die 4 is at least the full dice sum.";
                case InteriorEqualsNeighborDifferenceCondition _: return "An interior die equals the absolute difference of its two neighbors.";
                case MiddleEqualsOddCountCondition _: return "Die 3 equals the number of odd faces.";
                case SumGreaterThanCondition value: return "Dice sum exceeds " + value.Parameter + ".";
                case HandsRemainingEvenCondition _: return "Hands remaining + 1 is even in the evaluation context.";
                case EdgePairDifferencesEqualCondition _: return "Absolute difference of dice 1 and 2 equals that of dice 4 and 5.";
                case InteriorContainsPairCondition _: return "At least two of dice 2, 3, 4 share a face.";
                case OddSumGreaterThanEvenSumCondition _: return "Sum of odd faces exceeds sum of even faces.";
                case SumPrimeCondition _: return "Dice sum is prime.";
                case EdgesMatchInteriorCondition _: return "Dice 1 and 5 match, and an interior die has that face.";
                case UniqueCountLessThanRerollsSpentCondition _: return "Number of distinct faces is below rerolls spent on this hand.";
                case NoExtremeFacesCondition value: return "No die shows " + value.Parameter + " or " + value.SecondaryParameter + ".";
                case MiddleFacesCountAtLeastCondition value: return "At least " + value.Parameter + " dice show 3 or 4.";
                case MiddleHandCondition _: return "Hands remaining equal half the starting hands, rounded up, in the evaluation context.";
                case InteriorPairMatchesEdgeCondition _: return "At least two interior dice match die 1 or die 5.";
                case AlwaysCondition _: return "Always meets its condition.";
                default:
                    var text = new StringBuilder(Humanize(type.EndsWith("Condition", StringComparison.Ordinal)
                        ? type.Substring(0, type.Length - 9) : type));
                    foreach (PropertyInfo property in condition.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                        TypeCode code = Type.GetTypeCode(property.PropertyType);
                        if (code < TypeCode.SByte || code > TypeCode.Decimal) continue;
                        text.Append("; ").Append(Humanize(property.Name)).Append(" = ")
                            .Append(Convert.ToString(property.GetValue(condition), CultureInfo.InvariantCulture));
                    }
                    return text.ToString();
            }
        }

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

        public static string DescribeModifier(ModifierDefinition modifier)
        {
            if (modifier == null) return "Modifier unavailable.";
            return Humanize(modifier.Id) + "\n" + DescribeCondition(modifier.Condition) + "\n" +
                DescribeEffects(modifier) + " | " + Humanize(modifier.Phase.ToString()) + " | " +
                Humanize(modifier.ActivationLimit.ToString());
        }

        public static string DescribeEffects(ModifierDefinition modifier)
        {
            var text = new StringBuilder();
            foreach (ModifierEffect effect in modifier.Effects)
            {
                if (text.Length != 0) text.Append(", ");
                text.Append('+').Append(Number(effect.Amount)).Append(' ');
                switch (effect.Kind)
                {
                    case ModifierEffectKind.AddBaseScore: text.Append("base score"); break;
                    case ModifierEffectKind.AddMultiplier: text.Append("multiplier"); break;
                    case ModifierEffectKind.AddRerolls: text.Append("rerolls"); break;
                    default: text.Append(Humanize(effect.Kind.ToString())); break;
                }
            }
            return text.Length == 0 ? "No effects" : text.ToString();
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

        public static string DescribeEvent(RuleEvent ruleEvent, MechanicalCatalog catalog)
        {
            if (ruleEvent == null) return "";
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
                        text.Append("\n  ").Append(Humanize(contribution.DefinitionId)).Append(" #")
                            .Append(contribution.InstanceId).Append(": ").Append(DescribeContribution(contribution));
                    return text.ToString();
                case ThresholdChangedEvent threshold:
                    return prefix + "Target remaining " + Number(threshold.Before) + " -> " + Number(threshold.After) +
                        "; applied " + Number(threshold.AppliedScore) + ", excess " + Number(threshold.ExcessScore) + ".";
                case ResourceChangedEvent resource:
                    return prefix + Humanize(resource.Resource.ToString()) + " " + resource.Before + " -> " + resource.After +
                        " (" + Humanize(resource.Cause.ToString()) + ").";
                case ModifierActivatedEvent activated:
                    return prefix + Humanize(activated.DefinitionId) + " #" + activated.InstanceId + " activated on " +
                        Humanize(activated.Phase.ToString()) + ": " + DescribeContribution(activated.Contribution) + ".";
                case StageClearedEvent cleared:
                    return prefix + "Cleared. " + cleared.HandsRemaining + " hands and " + cleared.RerollsRemaining + " rerolls remain.";
                case RewardOfferedEvent offered:
                    return prefix + (offered.IsRefresh ? "Refreshed " : "Offered ") + Humanize(offered.Offer.Kind.ToString()) +
                        " choices for stage " + (offered.Offer.TargetStageIndex + 1) + " (offer " + offered.Offer.OfferId + ").";
                case RewardSelectedEvent selected:
                    return prefix + "Selected " + Humanize(selected.Option.DefinitionId) + " (offer " + selected.OfferId +
                        ", option " + selected.Option.OptionId + ").";
                case RewardSkippedEvent skipped:
                    return prefix + "Skipped " + Humanize(skipped.Kind.ToString()) + "; no eligible options.";
                case ModifierAcquiredEvent acquired:
                    return prefix + "Acquired " + Humanize(acquired.DefinitionId) + " #" + acquired.InstanceId + ".";
                case HandUpgradedEvent upgraded:
                    return prefix + Humanize(upgraded.Category.ToString()) + " upgraded " + upgraded.Before + " -> " + upgraded.After + ".";
                case EncounterCompletedEvent completed:
                    return prefix + Humanize(completed.Outcome.ToString()) + ". Total score " + Number(completed.TotalScore) +
                        "; " + completed.HandsPlayed + " hands played, " + completed.RerollsSpent + " rerolls spent.";
                case RewardsRequiredEvent _: return prefix + "Rewards required before the next stage.";
                default: return prefix + Humanize(ruleEvent.GetType().Name);
            }
        }
    }
}
