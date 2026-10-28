using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    /** The distinct faces rule: each distinct face of the scored hand adds one multiplier. */
    public sealed class DistinctFacesMultiplierRuleTests
    {
        private const string EncounterId = TestContent.EncounterId;
        private const string RuleId = DistinctFacesMultiplierRuleDefinition.StableId;

        /** Checks the bonus counts distinct faces / fails if it counts dice or repeated faces. */
        [Test]
        public void ScoredHandAddsOneMultiplierPerDistinctFace()
        {
            var catalog = Catalog();
            Assert.That(WithDice(catalog, 2, 2, 3, 5, 6).PreviewPlayHand().EncounterRuleMultiplier, Is.EqualTo(4));
            Assert.That(WithDice(catalog, 4, 4, 4, 4, 4).PreviewPlayHand().EncounterRuleMultiplier, Is.EqualTo(1));
        }

        /**
         * Checks a rule bonus joins the hand multiplier beside modifier bonuses and is announced before the hand is scored /
         * fails if the rule bonus is dropped, counted as a modifier, or reported after scoring.
         */
        [Test]
        public void RuleBonusAddsToTheHandMultiplierAndIsAnnouncedBeforeScoring()
        {
            var modifier = new ModifierDefinition("test.always", "test.always", ModifierPhase.PlayHand,
                new AlwaysCondition(),
                new ModifierEffect[] { new AddMultiplierEffect(7) });
            var catalog = Catalog(modifier);
            var session = WithDice(catalog, 2, 2, 3, 5, 6);
            var preview = session.PreviewPlayHand();
            Assert.That(preview.ModifierMultiplier, Is.EqualTo(7));
            Assert.That(preview.EncounterRuleMultiplier, Is.EqualTo(4));
            Assert.That(preview.Multiplier, Is.EqualTo(preview.CategoryMultiplier + preview.UpgradeMultiplier +
                preview.ModifierMultiplier + preview.EncounterRuleMultiplier));

            var result = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            var activation = result.Events.OfType<EncounterRuleActivatedEvent>().Single();
            var scored = result.Events.OfType<HandScoredEvent>().Single();
            Assert.That(activation.RuleId, Is.EqualTo(RuleId));
            Assert.That(activation.MultiplierBonus, Is.EqualTo(4));
            Assert.That(result.Events.IndexOf(activation), Is.LessThan(result.Events.IndexOf(scored)));
            Assert.That(scored.Score.HandScore, Is.EqualTo(preview.HandScore));
        }

        private static MechanicalCatalog Catalog(params ModifierDefinition[] modifiers)
            => new MechanicalCatalog("test.distinct_faces", TestContent.Hands(), new[]
            {
                new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 5, 3) },
                    startingModifiers: modifiers.Select(value => value.Id),
                    rules: new[] { new DistinctFacesMultiplierRuleDefinition() })
            }, modifiers);

        private static EncounterSession WithDice(MechanicalCatalog catalog, params int[] dice)
        {
            var save = EncounterSession.CreateEncounter(catalog, EncounterId, 27).CaptureSave();
            save.CurrentDice = dice;
            save.InitialDice = (int[])dice.Clone();
            var restored = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(restored.Success, Is.True, restored.Error + ": " + restored.Detail);
            return restored.Session;
        }
    }
}
