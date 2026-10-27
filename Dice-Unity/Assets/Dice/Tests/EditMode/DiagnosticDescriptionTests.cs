using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class DiagnosticDescriptionTests
    {
        private sealed class WordedEvent : RuleEvent
        {
            private readonly string modifierId;

            public WordedEvent(string modifierId) : base(1, 2) => this.modifierId = modifierId;

            protected override string DescribeDetail(IDiagnosticFormat format)
                => format.ModifierName(modifierId) + " gave " + format.Number(12345) + " to " + format.Humanize("HandUpgrade") + ".";
        }

        [Test]
        public void TheEventLogShowsEachEventsOwnDescription()
        {
            using var synthetic = new SyntheticCharms();
            MechanicalCatalog catalog = RewardTestContent.Catalog();
            ModifierPresentationCatalog charms = synthetic.For(catalog);
            string name = charms.GetName(catalog.GetModifier("first"));
            Assert.That(DiagnosticText.DescribeEvent(new WordedEvent("first"), catalog, charms),
                Is.EqualTo("Stage 2 / hand 3 - " + name + " gave 12,345 to Hand Upgrade."));
        }

        [Test]
        public void RuleSnapshotsLabelOnlyTheDiceTheirStateConcerns()
        {
            var binding = new DieBindingRuleSnapshot("rule.test.binding", true, 0b00101, 0b01000);
            Assert.That(Enumerable.Range(0, DiceValues.Count).Select(binding.DiagnosticDieLabel),
                Is.EqualTo(new[] { "BOUND", null, "BOUND", "MARKED", null }));
            var interference = new DieInterferenceRuleSnapshot("rule.test.interference", true, 0b10010, 0b10000);
            Assert.That(Enumerable.Range(0, DiceValues.Count).Select(interference.DiagnosticDieLabel),
                Is.EqualTo(new[] { null, "DISTURBED", null, null, "DISTURBED" }));
            var plain = new EncounterRuleSnapshot("rule.test.plain", true);
            Assert.That(Enumerable.Range(0, DiceValues.Count).Select(plain.DiagnosticDieLabel), Is.All.Null);
        }
    }
}
