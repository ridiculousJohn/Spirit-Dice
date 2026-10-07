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
