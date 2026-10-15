using System;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class SpiritHandScoringHighlightsTests
    {
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
        public void HighlightsTheDiceThatFormTheEvaluatedHand(int first, int second, int third, int fourth,
            int fifth, HandCategory category, int expectedMask)
        {
            var dice = new DiceValues(first, second, third, fourth, fifth);
            Assert.That(HandEvaluator.Evaluate(dice), Is.EqualTo(category));
            Assert.That(SpiritHandScoringHighlights.GetMask(dice, category), Is.EqualTo(expectedMask));
        }

        [Test]
        public void HighDieTiesChooseTheFirstHighestSlot()
        {
            Assert.That(SpiritHandScoringHighlights.GetMask(new DiceValues(2, 6, 3, 6, 1), HandCategory.HighDie),
                Is.EqualTo(1 << 1));
        }

        [Test]
        public void RejectsInvalidDiceAndCategory()
        {
            Assert.Throws<ArgumentException>(() => SpiritHandScoringHighlights.GetMask(default, HandCategory.Pair));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpiritHandScoringHighlights.GetMask(
                new DiceValues(1, 2, 3, 4, 6), (HandCategory)8));
        }
    }
}
