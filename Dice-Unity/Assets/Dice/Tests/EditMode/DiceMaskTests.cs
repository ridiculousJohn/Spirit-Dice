using System;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class DiceMaskTests
    {
        /** Checks a dice mask counts only the bays it selects / fails if bits outside the five bays are counted. */
        [Test]
        public void CountBaysCountsTheSelectedBays()
        {
            Assert.That(DiceValues.CountBays(0), Is.Zero);
            Assert.That(DiceValues.CountBays(0b10101), Is.EqualTo(3));
            Assert.That(DiceValues.CountBays(DiceValues.AllMask), Is.EqualTo(DiceValues.Count));
            Assert.That(DiceValues.CountBays(1 << DiceValues.Count), Is.Zero);
        }

        /** Checks the nth selected bay is found from the first bay / fails if unselected bays count or a missing bay is invented. */
        [Test]
        public void NthBayFindsTheSelectedBayAtAPosition()
        {
            Assert.That(DiceValues.NthBay(0b10110, 0), Is.EqualTo(1));
            Assert.That(DiceValues.NthBay(0b10110, 1), Is.EqualTo(2));
            Assert.That(DiceValues.NthBay(0b10110, 2), Is.EqualTo(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => DiceValues.NthBay(0b10110, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => DiceValues.NthBay(0, 0));
        }
    }
}
