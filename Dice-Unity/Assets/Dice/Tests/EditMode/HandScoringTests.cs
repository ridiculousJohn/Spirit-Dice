using System;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class HandScoringTests
    {
        [Test]
        public void AllOrderedHandsMatchMultiplicityOracleAndKnownCounts()
        {
            var distribution = new int[8];
            for (int encoded = 0; encoded < 7776; ++encoded)
            {
                int remaining = encoded;
                var faces = new int[5];
                for (int i = 0; i < faces.Length; ++i)
                {
                    faces[i] = remaining % 6 + 1;
                    remaining /= 6;
                }
                var dice = new DiceValues(faces[0], faces[1], faces[2], faces[3], faces[4]);
                HandCategory actual = HandEvaluator.Evaluate(dice);
                Assert.That(actual, Is.EqualTo(Oracle(faces)), "Ordered hand " + encoded);
                Assert.That(dice.ToArray(), Is.EqualTo(faces));
                ++distribution[(int)actual];
            }
            Assert.That(distribution, Is.EqualTo(new[] { 480, 3600, 1800, 1200, 300, 150, 240, 6 }));
        }

        private static HandCategory Oracle(int[] faces)
        {
            var sizes = faces.GroupBy(value => value).Select(group => group.Count()).OrderByDescending(value => value).ToArray();
            if (sizes.SequenceEqual(new[] { 5 })) return HandCategory.FiveOfAKind;
            if (sizes.SequenceEqual(new[] { 4, 1 })) return HandCategory.FourOfAKind;
            if (sizes.SequenceEqual(new[] { 3, 2 })) return HandCategory.FullHouse;
            if (sizes.SequenceEqual(new[] { 3, 1, 1 })) return HandCategory.ThreeOfAKind;
            if (sizes.SequenceEqual(new[] { 2, 2, 1 })) return HandCategory.TwoPair;
            if (sizes.SequenceEqual(new[] { 2, 1, 1, 1 })) return HandCategory.Pair;
            return faces.Max() - faces.Min() == 4 ? HandCategory.Straight : HandCategory.HighDie;
        }

        [TestCase(0, 216)]
        [TestCase(1, 287)]
        [TestCase(6, 792)]
        public void FullHouseMatchesReferenceExample(int upgrades, long expectedScore)
        {
            var counts = new int[8];
            counts[(int)HandCategory.FullHouse] = upgrades;
            var result = HandScorer.Calculate(new DiceValues(2, 2, 2, 5, 5), TestContent.Catalog(), counts);
            Assert.That(result.HandScore, Is.EqualTo(expectedScore));
            Assert.That(result.DiceSum, Is.EqualTo(16));
            Assert.That(result.CategoryBaseScore, Is.EqualTo(20));
            Assert.That(result.CategoryMultiplier, Is.EqualTo(6));
        }

        [Test]
        public void FiveOfAKindReceivesTwoMultiplierPerUpgrade()
        {
            var counts = new int[8];
            counts[(int)HandCategory.FiveOfAKind] = 6;
            var result = HandScorer.Calculate(new DiceValues(6, 6, 6, 6, 6), TestContent.Catalog(), counts);
            Assert.That(result.BaseScore, Is.EqualTo(90));
            Assert.That(result.Multiplier, Is.EqualTo(26));
            Assert.That(result.HandScore, Is.EqualTo(2340));
        }

        [Test]
        public void HighDieHasNoExtraUniversalMultiplier()
        {
            var result = HandScorer.Calculate(new DiceValues(1, 2, 3, 4, 6), TestContent.Catalog(), new int[8]);
            Assert.That(result.Category, Is.EqualTo(HandCategory.HighDie));
            Assert.That(result.Multiplier, Is.EqualTo(1));
            Assert.That(result.HandScore, Is.EqualTo(21));
        }

        [Test]
        public void AllSameParityDoesNotIntroduceInactiveFlushCategory()
        {
            Assert.That(HandEvaluator.Evaluate(new DiceValues(2, 2, 4, 4, 6)), Is.EqualTo(HandCategory.TwoPair));
            Assert.That(HandEvaluator.Evaluate(new DiceValues(1, 1, 1, 3, 3)), Is.EqualTo(HandCategory.FullHouse));
        }

        [Test]
        public void DefinitionsAndSnapshotsRejectExternalMutation()
        {
            var stages = new[] { new StageDefinition(100, 3, 3) };
            var upgrades = new int[8];
            var definition = new EncounterDefinition("encounter.immutable", stages, upgrades);
            stages[0] = new StageDefinition(1, 1, 0);
            upgrades[0] = 6;
            Assert.That(definition.Stages[0].Threshold, Is.EqualTo(100));
            Assert.That(definition.StartingUpgrades[0], Is.Zero);
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<int>)definition.StartingUpgrades)[0] = 2);
            var snapshot = TestContent.Session().Snapshot;
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<int>)snapshot.HandUpgrades)[0] = 2);
        }

        [Test]
        public void InvalidMechanicalDefinitionsFailBeforePlay()
        {
            Assert.Throws<ArgumentException>(() => TestContent.Catalog(hands: TestContent.Hands().Take(7)));
            Assert.Throws<ArgumentException>(() => TestContent.Catalog(hands: TestContent.Hands().Concat(new[] { TestContent.Hands()[0] })));
            Assert.Throws<ArgumentException>(() => TestContent.Catalog(upgrades: new[] { 7, 0, 0, 0, 0, 0, 0, 0 }));
            Assert.Throws<ArgumentException>(() => new EncounterDefinition("encounter.empty", Array.Empty<StageDefinition>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StageDefinition(0, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StageDefinition(1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StageDefinition(1, 1, -1));
            Assert.Throws<OverflowException>(() => new HandDefinition("hand.overflow", HandCategory.Pair, long.MaxValue, 1, 0, 0, 0));
            Assert.Throws<ArgumentException>(() => HandEvaluator.Evaluate(default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiceValues(0, 1, 2, 3, 4));
        }

        /** Checks the evaluator states the dice that form each category: every die of a full house, straight or five of a kind, the matched dice of pairs and sets, and the first highest die of a high die / fails if a category marks dice outside its pattern or misses one. */
        [TestCase(1, 2, 3, 4, 6, HandCategory.HighDie, 16)]
        [TestCase(4, 1, 3, 4, 6, HandCategory.Pair, 9)]
        [TestCase(4, 2, 4, 6, 2, HandCategory.TwoPair, 23)]
        [TestCase(3, 1, 3, 6, 3, HandCategory.ThreeOfAKind, 21)]
        [TestCase(3, 6, 3, 6, 3, HandCategory.FullHouse, 31)]
        [TestCase(6, 6, 2, 6, 6, HandCategory.FourOfAKind, 27)]
        [TestCase(5, 1, 3, 2, 4, HandCategory.Straight, 31)]
        [TestCase(3, 6, 2, 5, 4, HandCategory.Straight, 31)]
        [TestCase(2, 2, 2, 2, 2, HandCategory.FiveOfAKind, 31)]
        [TestCase(6, 4, 3, 1, 2, HandCategory.HighDie, 1)]
        [TestCase(5, 1, 6, 3, 5, HandCategory.Pair, 17)]
        [TestCase(2, 2, 1, 3, 3, HandCategory.TwoPair, 27)]
        [TestCase(2, 6, 6, 6, 4, HandCategory.ThreeOfAKind, 14)]
        [TestCase(1, 5, 5, 5, 5, HandCategory.FourOfAKind, 30)]
        public void EvaluatorStatesTheDiceThatFormTheCategory(int first, int second, int third, int fourth, int fifth,
            HandCategory category, int expectedMask)
        {
            Assert.That(HandEvaluator.Evaluate(new DiceValues(first, second, third, fourth, fifth), out int mask), Is.EqualTo(category));
            Assert.That(mask, Is.EqualTo(expectedMask));
        }

        /** Checks the score breakdown states the dice that form its category / fails if the breakdown's dice differ from the evaluator's or are left empty. */
        [TestCase(4, 1, 3, 4, 6, 9)]
        [TestCase(1, 2, 3, 4, 6, 16)]
        public void ScoreStatesTheDiceThatFormItsCategory(int first, int second, int third, int fourth, int fifth, int expectedMask)
        {
            var score = HandScorer.Calculate(new DiceValues(first, second, third, fourth, fifth), TestContent.Catalog(), new int[8]);
            Assert.That(score.FormingDiceMask, Is.EqualTo(expectedMask));
        }

        /** Checks a hand states its base score and multiplier at each upgrade level from zero to its maximum and rejects levels outside that range / fails if a level's values drift from the per-upgrade rise or an out-of-range level is answered. */
        [TestCase(0, 17L, 3L)]
        [TestCase(2, 31L, 7L)]
        [TestCase(6, 59L, 15L)]
        public void HandStatesItsValuesAtAnUpgradeLevel(int level, long baseScore, long multiplier)
        {
            var hand = new HandDefinition("hand.level-test", HandCategory.Pair, 17, 3, 7, 2, 6);
            Assert.That(hand.BaseScoreAt(level), Is.EqualTo(baseScore));
            Assert.That(hand.MultiplierAt(level), Is.EqualTo(multiplier));
        }

        [TestCase(-1)]
        [TestCase(7)]
        public void HandRejectsAnUpgradeLevelOutsideItsRange(int level)
        {
            var hand = new HandDefinition("hand.level-test", HandCategory.Pair, 17, 3, 7, 2, 6);
            Assert.Throws<ArgumentOutOfRangeException>(() => hand.BaseScoreAt(level));
            Assert.Throws<ArgumentOutOfRangeException>(() => hand.MultiplierAt(level));
        }

        [TestCase(-1)]
        [TestCase(7)]
        public void InvalidUpgradeCountIsNotSilentlyClamped(int count)
        {
            var upgrades = new int[8];
            upgrades[0] = count;
            Assert.Throws<ArgumentOutOfRangeException>(() => HandScorer.Calculate(new DiceValues(1, 2, 3, 4, 6), TestContent.Catalog(), upgrades));
        }
    }
}
