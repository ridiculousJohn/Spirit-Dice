using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public static class HandEvaluator
    {
        public static HandCategory Evaluate(DiceValues dice)
        {
            if (!dice.IsValid) throw new ArgumentException("Five valid die faces are required.", nameof(dice));
            var counts = new int[7];
            int faceMask = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                ++counts[dice[i]];
                faceMask |= 1 << dice[i];
            }
            int pairs = 0;
            bool triple = false;
            for (int face = 1; face <= 6; ++face)
            {
                if (counts[face] == 5) return HandCategory.FiveOfAKind;
                if (counts[face] == 4) return HandCategory.FourOfAKind;
                if (counts[face] == 3) triple = true;
                if (counts[face] == 2) ++pairs;
            }
            if (triple && pairs == 1) return HandCategory.FullHouse;
            if (faceMask == 0x3e || faceMask == 0x7c) return HandCategory.Straight;
            if (triple) return HandCategory.ThreeOfAKind;
            if (pairs == 2) return HandCategory.TwoPair;
            if (pairs == 1) return HandCategory.Pair;
            return HandCategory.HighDie;
        }
    }

    public sealed class ScoreBreakdown
    {
        public HandCategory Category { get; }
        public string HandDefinitionId { get; }
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

        internal ScoreBreakdown(DiceValues dice, HandDefinition hand, int upgradeCount,
            IEnumerable<ModifierContribution> contributions, long encounterRuleMultiplier = 0)
        {
            Category = hand.Category;
            HandDefinitionId = hand.Id;
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
            HandCategory category = HandEvaluator.Evaluate(dice);
            return new ScoreBreakdown(dice, catalog.GetHand(category), upgrades[(int)category], contributions,
                encounterRuleMultiplier);
        }
    }
}
