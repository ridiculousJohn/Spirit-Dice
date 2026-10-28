using System.Collections.Generic;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierEffectTests
    {
        /**
         * Checks each effect adds its amount to its own total of an activation and to no other / fails if an effect applies
         * to the wrong total or the totals are not summed from the effects themselves.
         */
        [TestCaseSource(nameof(Effects))]
        public void EachEffectAppliesItselfToItsOwnTotal(ModifierEffect effect, long baseScore, long multiplier, int rerolls)
        {
            ModifierEffectTotals totals = ModifierEffectTotals.Of(Charm(effect, effect));
            Assert.That(totals.BaseScore, Is.EqualTo(baseScore * 2));
            Assert.That(totals.Multiplier, Is.EqualTo(multiplier * 2));
            Assert.That(totals.Rerolls, Is.EqualTo(rerolls * 2));
        }

        private static IEnumerable<TestCaseData> Effects()
        {
            yield return new TestCaseData(new AddBaseScoreEffect(3), 3L, 0L, 0).SetName(nameof(AddBaseScoreEffect));
            yield return new TestCaseData(new AddMultiplierEffect(4), 0L, 4L, 0).SetName(nameof(AddMultiplierEffect));
            yield return new TestCaseData(new AddRerollsEffect(2), 0L, 0L, 2).SetName(nameof(AddRerollsEffect));
        }

        private static ModifierDefinition Charm(params ModifierEffect[] effects)
            => new ModifierDefinition("charm.effects", "family.effects", ModifierPhase.PlayHand, new AlwaysCondition(), effects);
    }
}
