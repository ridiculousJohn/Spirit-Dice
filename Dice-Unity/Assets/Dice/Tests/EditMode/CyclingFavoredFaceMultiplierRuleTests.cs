using System.Linq;
using Dice.Engine;
using NUnit.Framework;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    /** The cycling favored face rule: the favored face follows the encounter-wide hand count and scored matches add multiplier. */
    public sealed class CyclingFavoredFaceMultiplierRuleTests
    {
        private const string EncounterId = TestContent.EncounterId;
        private const int MultiplierPerFavoredDie = 3;

        /** Checks each scored die showing the favored face adds the rule's multiplier / fails if other faces count or matches are ignored. */
        [Test]
        public void EachScoredDieShowingTheFavoredFaceAddsTheRulesMultiplier()
        {
            var catalog = RuleCatalog(new StageDefinition(1000000, 12, 3));
            int favored = FavoredFace(EncounterSession.CreateEncounter(catalog, EncounterId, 27));
            int other = favored % 6 + 1;
            Assert.That(WithDice(catalog, favored, favored, other, other, other).PreviewPlayHand().EncounterRuleMultiplier,
                Is.EqualTo(2 * MultiplierPerFavoredDie));
            Assert.That(WithDice(catalog, other, other, other, other, other).PreviewPlayHand().EncounterRuleMultiplier,
                Is.Zero);
        }

        /** Checks the favored face steps 1 through 6 and wraps with each played hand / fails if a reroll advances it or it never wraps. */
        [Test]
        public void FavoredFaceAdvancesWithPlayedHandsAndNotWithRerolls()
        {
            var session = EncounterSession.CreateEncounter(RuleCatalog(new StageDefinition(1000000, 12, 7)), EncounterId, 27);
            for (int hand = 0; hand < 7; ++hand)
            {
                Assert.That(FavoredFace(session), Is.EqualTo(hand % 6 + 1));
                Execute(session, new RerollDiceAction(1));
                Assert.That(FavoredFace(session), Is.EqualTo(hand % 6 + 1));
                Execute(session, PlayHandAction.Instance);
            }
        }

        /** Checks the favored face keeps counting hands across a stage change / fails if a new stage restarts the cycle. */
        [Test]
        public void FavoredFaceCountsHandsAcrossStages()
        {
            var session = EncounterSession.CreateEncounter(
                RuleCatalog(new StageDefinition(1, 3, 3), new StageDefinition(1000000, 3, 3)), EncounterId, 27);
            int first = FavoredFace(session);
            Execute(session, PlayHandAction.Instance);
            Assert.That(session.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(session.Snapshot.HandIndex, Is.Zero);
            Assert.That(FavoredFace(session), Is.EqualTo(first % 6 + 1));
        }

        /** Checks a favored face count charm reads the rule's current face / fails if it ignores the face or counts other faces. */
        [Test]
        public void FavoredFaceCountCharmReadsTheRulesFavoredFace()
        {
            var charm = new ModifierDefinition("test.favored_face_count", TestFamilies.Of("test.favored_face_count"), ModifierPhase.PlayHand,
                new FavoredFaceCountAtLeastCondition(2),
                new ModifierEffect[] { new AddMultiplierEffect(7) });
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 8, 3) },
                startingModifiers: new[] { charm.Id }, rules: new[] { new CyclingFavoredFaceMultiplierRuleDefinition(MultiplierPerFavoredDie) });
            var catalog = new MechanicalCatalog("test.favored_face.charms", TestContent.Hands(), new[] { encounter }, new[] { charm });
            int favored = FavoredFace(EncounterSession.CreateEncounter(catalog, EncounterId, 27));
            int other = favored % 6 + 1;
            Assert.That(WithDice(catalog, favored, favored, other, other, other).InspectModifiersForPlayHand().Single().ConditionMet,
                Is.EqualTo(true));
            Assert.That(WithDice(catalog, favored, other, other, other, other).InspectModifiersForPlayHand().Single().ConditionMet,
                Is.EqualTo(false));
        }

        private static MechanicalCatalog RuleCatalog(params StageDefinition[] stages)
            => new MechanicalCatalog("test.cycling_favored_face", TestContent.Hands(), new[]
            {
                new EncounterDefinition(EncounterId, stages, rules: new[] { new CyclingFavoredFaceMultiplierRuleDefinition(MultiplierPerFavoredDie) })
            });

        private static int FavoredFace(EncounterSession session)
            => ((FavoredFaceRuleSnapshot)session.Snapshot.EncounterRules.Single()).FavoredFace;

        private static EncounterSession WithDice(MechanicalCatalog catalog, params int[] dice)
        {
            var save = EncounterSession.CreateEncounter(catalog, EncounterId, 27).CaptureSave();
            save.CurrentDice = dice;
            save.InitialDice = (int[])dice.Clone();
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.True, result.Error + ": " + result.Detail);
            return result.Session;
        }

        private static Resolution Execute(EncounterSession session, EncounterAction action)
        {
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            return result;
        }
    }
}
