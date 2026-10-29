using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public static class HandEvaluator
    {
        /** The face masks, one bit per face, of the straights 1 to 5 and 2 to 6. */
        private const int LowStraightFaces = 0x3e, HighStraightFaces = 0x7c;

        public static HandCategory Evaluate(DiceValues dice) => Evaluate(dice, out _);

        /**
         * The hand's category and the dice that form it, as a mask of die slots: every die of a full house, straight or
         * five of a kind, the matched dice of a pair, two pair, three or four of a kind, and the first highest die of a
         * high die.
         */
        public static HandCategory Evaluate(DiceValues dice, out int formingMask)
        {
            if (!dice.IsValid) throw new ArgumentException("Five valid die faces are required.", nameof(dice));
            var counts = new int[DiceValues.MaximumFace + 1];
            int faceMask = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                ++counts[dice[i]];
                faceMask |= 1 << dice[i];
            }
            HandCategory category = Categorize(counts, faceMask);
            formingMask = FormingMask(dice, counts, category);
            return category;
        }

        private static HandCategory Categorize(int[] counts, int faceMask)
        {
            int pairs = 0;
            bool triple = false;
            for (int face = DiceValues.MinimumFace; face <= DiceValues.MaximumFace; ++face)
            {
                if (counts[face] == DiceValues.Count) return HandCategory.FiveOfAKind;
                if (counts[face] == 4) return HandCategory.FourOfAKind;
                if (counts[face] == 3) triple = true;
                if (counts[face] == 2) ++pairs;
            }
            if (triple && pairs == 1) return HandCategory.FullHouse;
            if (faceMask == LowStraightFaces || faceMask == HighStraightFaces) return HandCategory.Straight;
            if (triple) return HandCategory.ThreeOfAKind;
            if (pairs == 2) return HandCategory.TwoPair;
            if (pairs == 1) return HandCategory.Pair;
            return HandCategory.HighDie;
        }

        private static int FormingMask(DiceValues dice, int[] counts, HandCategory category)
        {
            switch (category)
            {
                case HandCategory.FullHouse:
                case HandCategory.Straight:
                case HandCategory.FiveOfAKind:
                    return DiceValues.AllMask;
                case HandCategory.HighDie:
                    int highestIndex = 0;
                    for (int index = 1; index < DiceValues.Count; ++index)
                        if (dice[index] > dice[highestIndex]) highestIndex = index;
                    return 1 << highestIndex;
                default:
                    int matchingCount = category == HandCategory.ThreeOfAKind ? 3 : category == HandCategory.FourOfAKind ? 4 : 2;
                    int mask = 0;
                    for (int index = 0; index < DiceValues.Count; ++index)
                        if (counts[dice[index]] == matchingCount) mask |= 1 << index;
                    return mask;
            }
        }
    }

    public sealed class ScoreBreakdown
    {
        public HandCategory Category { get; }
        /** The dice that form the scored category, as a mask of die slots. */
        public int FormingDiceMask { get; }
        public int DiceSum { get; }
        public int UpgradeCount { get; }
        public long CategoryBaseScore { get; }
        public long UpgradeBaseScore { get; }
        public long CategoryMultiplier { get; }
        public long UpgradeMultiplier { get; }
        public long BaseScore { get; }
        public long Multiplier { get; }
        public long HandScore { get; }
        public long ModifierBaseScore { get; }
        public long ModifierMultiplier { get; }
        public long EncounterRuleMultiplier { get; }
        public ReadOnlyCollection<ModifierContribution> ModifierContributions { get; }

        internal ScoreBreakdown(DiceValues dice, int formingDiceMask, HandDefinition hand, int upgradeCount,
            IEnumerable<ModifierContribution> contributions, long encounterRuleMultiplier = 0)
        {
            Category = hand.Category;
            FormingDiceMask = formingDiceMask;
            DiceSum = dice.Sum;
            UpgradeCount = upgradeCount;
            CategoryBaseScore = hand.BaseScore;
            CategoryMultiplier = hand.Multiplier;
            EncounterRuleMultiplier = encounterRuleMultiplier;
            ModifierContributions = new List<ModifierContribution>(contributions).AsReadOnly();
            checked
            {
                foreach (var contribution in ModifierContributions)
                {
                    ModifierBaseScore += contribution.BaseScore;
                    ModifierMultiplier += contribution.Multiplier;
                }
                UpgradeBaseScore = hand.BaseScorePerUpgrade * upgradeCount;
                UpgradeMultiplier = hand.MultiplierPerUpgrade * upgradeCount;
                BaseScore = DiceSum + CategoryBaseScore + UpgradeBaseScore + ModifierBaseScore;
                Multiplier = CategoryMultiplier + UpgradeMultiplier + ModifierMultiplier + EncounterRuleMultiplier;
                HandScore = BaseScore * Multiplier;
            }
        }
    }

    public static class HandScorer
    {
        public static ScoreBreakdown Calculate(DiceValues dice, MechanicalCatalog catalog, IReadOnlyList<int> upgrades)
            => Calculate(dice, catalog, upgrades, Array.Empty<ModifierContribution>());

        internal static ScoreBreakdown Calculate(DiceValues dice, MechanicalCatalog catalog, IReadOnlyList<int> upgrades,
            IEnumerable<ModifierContribution> contributions, long encounterRuleMultiplier = 0)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (upgrades == null) throw new ArgumentNullException(nameof(upgrades));
            if (upgrades.Count != HandDefinition.CategoryCount) throw new ArgumentException("Incorrect category count.", nameof(upgrades));
            for (int i = 0; i < upgrades.Count; ++i)
                if (upgrades[i] < 0 || upgrades[i] > catalog.Hands[i].MaximumUpgrades)
                    throw new ArgumentOutOfRangeException(nameof(upgrades));
            HandCategory category = HandEvaluator.Evaluate(dice, out int formingDiceMask);
            return new ScoreBreakdown(dice, formingDiceMask, catalog.GetHand(category), upgrades[(int)category], contributions,
                encounterRuleMultiplier);
        }
    }
}
