using System;
using System.Linq;
using Dice.App;
using Dice.Engine;
using NUnit.Framework;
using static Dice.Tests.TestContent;

namespace Dice.Tests
{
    /** The rot rule: each player reroll drops every kept die by one face, and the endpoint decides what a 1 becomes. */
    public sealed class RotRuleTests
    {
        private static readonly EncounterRuleDefinition.Id RotId = TestRules.IdOf("rot");
        private const ulong Seed = 11;

        /** Checks a reroll drops each kept die by one and leaves the rerolled dice as they landed / fails if a kept die keeps its face or a rerolled die rots. */
        [Test]
        public void ARerollRotsTheKeptDiceByOneAndLeavesTheRerolledDiceAlone()
        {
            var catalog = Catalog();
            var session = WithDice(catalog, Seed, 2, 3, 4, 5, 6);
            var result = Execute(session, new RerollDiceAction(1));
            var rolled = result.Events.OfType<DiceRolledEvent>().Single();
            var rotted = result.Events.OfType<DiceRottedEvent>().Single();
            var dice = result.Snapshot.CurrentDice;
            Assert.That(new[] { dice[1], dice[2], dice[3], dice[4] }, Is.EqualTo(new[] { 2, 3, 4, 5 }));
            Assert.That(dice[0], Is.EqualTo(rolled.After[0]), "The rerolled die keeps the face it landed on.");
            Assert.That(rotted.RuleId, Is.EqualTo(RotId));
            Assert.That(rotted.RottedMask, Is.EqualTo(30));
            Assert.That(rotted.Before, Is.EqualTo(rolled.After));
            Assert.That(rotted.After, Is.EqualTo(dice));
            Assert.That(rotted.WitheredMask, Is.Zero);
            Assert.That(rotted.RerolledMask, Is.Zero);
            Assert.That(result.Events.IndexOf(rotted), Is.GreaterThan(result.Events.IndexOf(rolled)));
            Assert.That(Rot(result.Snapshot).WitheredMask, Is.Zero);
            Restore(catalog, session.CaptureSave());
        }

        /** Checks a kept 1 withers and counts toward nothing / fails if it drops below 1, stays in the hand or adds to the sum. */
        [Test]
        public void AKeptOneWithers()
        {
            var catalog = Catalog();
            var session = WithDice(catalog, Seed, 1, 3, 4, 5, 6);
            var result = Execute(session, new RerollDiceAction(16));
            var rotted = result.Events.OfType<DiceRottedEvent>().Single();
            Assert.That(rotted.RottedMask, Is.EqualTo(15));
            Assert.That(rotted.WitheredMask, Is.EqualTo(1));
            Assert.That(result.Snapshot.CurrentDice[0], Is.EqualTo(1));
            Assert.That(Rot(result.Snapshot).WitheredMask, Is.EqualTo(1));
            Assert.That(Rot(result.Snapshot).DiagnosticDieLabel(0), Is.EqualTo("WITHERED"));
            Assert.That(result.Snapshot.CountedMask, Is.EqualTo(30));
            var score = session.PreviewPlayHand();
            Assert.That(score.CountedDiceMask, Is.EqualTo(30));
            Assert.That(score.DiceSum, Is.EqualTo(new HandDice(result.Snapshot.CurrentDice, 30).Sum));
            Restore(catalog, session.CaptureSave());
        }

        /** Checks a withered die left out of a later reroll stays withered without rotting / fails if it is restored or rots again. */
        [Test]
        public void AWitheredDieKeptAgainStaysWithered()
        {
            var catalog = Catalog();
            var session = WithDice(catalog, Seed, 1, 3, 4, 5, 6);
            Execute(session, new RerollDiceAction(16));
            var result = Execute(session, new RerollDiceAction(16));
            var rotted = result.Events.OfType<DiceRottedEvent>().Single();
            Assert.That(rotted.RottedMask, Is.EqualTo(14), "A withered die does not rot further.");
            Assert.That(rotted.WitheredMask, Is.Zero);
            Assert.That(result.Events.OfType<DiceRestoredEvent>(), Is.Empty);
            Assert.That(Rot(result.Snapshot).WitheredMask, Is.EqualTo(1));
            Assert.That(result.Snapshot.CurrentDice[0], Is.EqualTo(1));
            Restore(catalog, session.CaptureSave());
        }

        /** Checks rerolling a withered die restores it with a fresh face / fails if it stays withered or rots. */
        [Test]
        public void RerollingAWitheredDieRestoresIt()
        {
            var catalog = Catalog();
            var session = WithDice(catalog, Seed, 1, 3, 4, 5, 6);
            Execute(session, new RerollDiceAction(16));
            var result = Execute(session, new RerollDiceAction(1));
            var restored = result.Events.OfType<DiceRestoredEvent>().Single();
            Assert.That(restored.RuleId, Is.EqualTo(RotId));
            Assert.That(restored.RestoredMask, Is.EqualTo(1));
            Assert.That(restored.Roll, Is.EqualTo(RollKind.PlayerReroll));
            Assert.That(result.Events.OfType<DiceRottedEvent>().Single().RottedMask, Is.EqualTo(30));
            Assert.That(Rot(result.Snapshot).WitheredMask, Is.Zero);
            Assert.That(result.Snapshot.CurrentDice[0], Is.EqualTo(result.Events.OfType<DiceRolledEvent>().Single().After[0]));
            Restore(catalog, session.CaptureSave());
        }

        /** Checks the next hand's opening roll restores a withered die and playing rots nothing / fails if withering outlasts the hand or a played hand rots. */
        [Test]
        public void TheOpeningRollRestoresWitheredDice()
        {
            var catalog = Catalog();
            var session = WithDice(catalog, Seed, 1, 3, 4, 5, 6);
            Execute(session, new RerollDiceAction(16));
            var played = Execute(session, PlayHandAction.Instance);
            Assert.That(played.Events.OfType<DiceRottedEvent>(), Is.Empty);
            var restored = played.Events.OfType<DiceRestoredEvent>().Single();
            Assert.That(restored.RestoredMask, Is.EqualTo(1));
            Assert.That(restored.Roll, Is.EqualTo(RollKind.Initial));
            int opening = played.Events.IndexOf(played.Events.OfType<DiceRolledEvent>().Single());
            Assert.That(played.Events.IndexOf(restored), Is.GreaterThan(opening));
            Assert.That(Rot(played.Snapshot).WitheredMask, Is.Zero);
            Restore(catalog, session.CaptureSave());
        }

        /** Checks a die's rot count rises on each rot step it rots in and returns to 0 when a reroll or the next opening roll throws it, reported by the rule's events and snapshot / fails if a count misses a step, survives a roll or changes unreported. */
        [Test]
        public void ADiesRotCountRisesEachStepAndReturnsToZeroWhenItIsRolled()
        {
            var catalog = Catalog();
            var session = WithDice(catalog, Seed, 2, 3, 4, 5, 6);
            var first = Execute(session, new RerollDiceAction(0b00001));
            Assert.That(first.Events.OfType<DiceRestoredEvent>(), Is.Empty, "Nothing had rotted.");
            Assert.That(first.Events.OfType<DiceRottedEvent>().Single().Counts.ToArray(), Is.EqualTo(new[] { 0, 1, 1, 1, 1 }));

            var second = Execute(session, new RerollDiceAction(0b00010));
            var cleared = second.Events.OfType<DiceRestoredEvent>().Single();
            Assert.That(cleared.ClearedMask, Is.EqualTo(0b00010));
            Assert.That(cleared.Roll, Is.EqualTo(RollKind.PlayerReroll));
            Assert.That(cleared.Counts.ToArray(), Is.EqualTo(new[] { 0, 0, 1, 1, 1 }));
            var rotted = second.Events.OfType<DiceRottedEvent>().Single();
            Assert.That(rotted.Counts.ToArray(), Is.EqualTo(new[] { 1, 0, 2, 2, 2 }));
            Assert.That(Rot(second.Snapshot).Counts, Is.EqualTo(rotted.Counts));
            Assert.That(Rot(second.Snapshot).DiagnosticDieLabel(2), Is.EqualTo("ROT 2"));
            Assert.That(new RotRuleSnapshot(RotId, true, 0, rotted.Counts), Is.Not.EqualTo(new RotRuleSnapshot(RotId, true, 0, default)),
                "The snapshot compares the counts.");

            var all = Execute(session, new RerollDiceAction(DiceValues.AllMask));
            Assert.That(all.Events.OfType<DiceRottedEvent>(), Is.Empty, "Rerolling every die rots nothing.");
            cleared = all.Events.OfType<DiceRestoredEvent>().Single();
            Assert.That(cleared.ClearedMask, Is.EqualTo(0b11101));
            Assert.That(cleared.Counts, Is.EqualTo(default(RotCounts)));
            Assert.That(Rot(all.Snapshot).Counts, Is.EqualTo(default(RotCounts)));

            Execute(session, new RerollDiceAction(0b00001));
            var played = Execute(session, PlayHandAction.Instance);
            cleared = played.Events.OfType<DiceRestoredEvent>().Single();
            Assert.That(cleared.Roll, Is.EqualTo(RollKind.Initial));
            Assert.That(cleared.ClearedMask, Is.EqualTo(0b11110));
            Assert.That(Rot(played.Snapshot).Counts, Is.EqualTo(default(RotCounts)));
            Restore(catalog, session.CaptureSave());
        }

        /** Checks each other endpoint's treatment of a kept 1 and that only rerolling draws from the rule stream / fails if a 1 withers or changes otherwise. */
        [TestCase(RotEndpoint.Clamp, 1)]
        [TestCase(RotEndpoint.Wrap, 6)]
        [TestCase(RotEndpoint.Reroll, 0)]
        public void TheEndpointDecidesWhatAKeptOneBecomes(RotEndpoint endpoint, int face)
        {
            var catalog = Catalog(endpoint);
            var session = WithDice(catalog, Seed, 1, 3, 4, 5, 6);
            var ruleStream = session.CaptureSave().RuleRandomState;
            Assert.That(ruleStream != 0, Is.EqualTo(endpoint == RotEndpoint.Reroll), "Only rerolling draws from the rule stream.");
            var result = Execute(session, new RerollDiceAction(16));
            var rotted = result.Events.OfType<DiceRottedEvent>().Single();
            Assert.That(rotted.RottedMask, Is.EqualTo(15));
            Assert.That(rotted.WitheredMask, Is.Zero);
            Assert.That(Rot(result.Snapshot).WitheredMask, Is.Zero);
            Assert.That(rotted.Counts.ToArray(), Is.EqualTo(new[] { endpoint == RotEndpoint.Reroll ? 0 : 1, 1, 1, 1, 0 }),
                "A die the rule rolls again starts its count over.");
            if (endpoint == RotEndpoint.Reroll)
            {
                Assert.That(rotted.RerolledMask, Is.EqualTo(1));
                Assert.That(session.CaptureSave().RuleRandomState, Is.Not.EqualTo(ruleStream));
            }
            else
            {
                Assert.That(rotted.RerolledMask, Is.Zero);
                Assert.That(result.Snapshot.CurrentDice[0], Is.EqualTo(face));
            }
            Restore(catalog, session.CaptureSave());
        }

        /** Checks the rot state survives the save codec and continues identically, and an unusable state is rejected / fails if a mask or count is lost, a die outside the bays loads, a hand with every die withered loads or counts of the wrong length or range load. */
        [Test]
        public void RotStateRoundTripsAndUnusableStatesAreRejected()
        {
            var catalog = Catalog();
            var session = WithDice(catalog, Seed, 1, 3, 4, 5, 6);
            Execute(session, new RerollDiceAction(16));
            Execute(session, new RerollDiceAction(16));
            var save = session.CaptureSave();
            var restored = Restore(catalog, EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(save), catalog));
            Assert.That(Rot(restored.Snapshot).WitheredMask, Is.EqualTo(1));
            Assert.That(Rot(restored.Snapshot).Counts.ToArray(), Is.EqualTo(new[] { 1, 2, 2, 2, 0 }));
            EncounterPersistenceTests.AssertGraphEqual(save, restored.CaptureSave());
            var original = Execute(session, new RerollDiceAction(2));
            var continued = Execute(restored, new RerollDiceAction(2));
            Assert.That(continued.Events.Select(value => value.GetType()), Is.EqualTo(original.Events.Select(value => value.GetType())));
            EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), restored.CaptureSave());

            foreach (int withered in new[] { 32, DiceValues.AllMask })
            {
                var unusable = session.CaptureSave();
                ((RotSaveData)unusable.RuleStates.Single().State).WitheredMask = withered;
                var result = EncounterSession.RestoreEncounter(catalog, unusable);
                Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState), result.Detail);
                Assert.That(result.Detail, Does.Contain(withered == 32 ? "outside the five bays" : "no die counted"));
            }
            foreach (var counts in new[] { null, new[] { 0, 0, 0, 0 }, new[] { 0, -1, 0, 0, 0 }, new[] { 0, 0, RotCounts.MaximumCount + 1, 0, 0 } })
            {
                var unusable = session.CaptureSave();
                ((RotSaveData)unusable.RuleStates.Single().State).Counts = counts;
                var result = EncounterSession.RestoreEncounter(catalog, unusable);
                Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState), result.Detail);
                Assert.That(result.Detail, Does.Contain("Rot counts"));
            }
        }

        /** Checks a rank comparison across a reroll reads the earlier hand with the dice withered then / fails if the withered die counts toward the earlier rank. */
        [Test]
        public void RankBeforeARerollCountsTheDiceWitheredThen()
        {
            var charm = new ModifierDefinition(TestModifiers.IdOf("charm.rot.rank"), TestFamilies.Of("family.rot.rank"),
                ModifierPhase.PlayHand, new RankEqualsBeforeRerollCondition(), new ModifierEffect[] { new AddBaseScoreEffect(7) });
            var catalog = Catalog(RotEndpoint.Wither, charm);
            var session = WithDice(catalog, Seed, 1, 3, 3, 6, 6);
            Execute(session, new RerollDiceAction(16));
            session = Landed(catalog, session, 4, 1);
            Execute(session, new RerollDiceAction(16));
            session = Landed(catalog, session, 4, 6);
            var snapshot = session.Snapshot;
            Assert.That(snapshot.HandBeforeLastReroll, Is.EqualTo(new HandDice(new DiceValues(1, 2, 2, 5, 1), 30)));
            Assert.That(snapshot.CurrentDice, Is.EqualTo(new DiceValues(1, 1, 1, 4, 6)));
            Assert.That(Rot(snapshot).WitheredMask, Is.EqualTo(1));
            Assert.That(HandEvaluator.Evaluate(HandDice.Full(snapshot.HandBeforeLastReroll.Value.Faces)),
                Is.EqualTo(HandCategory.TwoPair), "Counting the die withered then would make the earlier hand two pair.");
            Assert.That(session.InspectModifiersForPlayHand().Single().ConditionMet, Is.True, "A pair then and a pair now.");
        }

        /** The encounter's single rot rule. */
        private static RotRuleSnapshot Rot(EncounterSnapshot snapshot) => snapshot.EncounterRules.OfType<RotRuleSnapshot>().Single();

        private static MechanicalCatalog Catalog(RotEndpoint endpoint = RotEndpoint.Wither, params ModifierDefinition[] modifiers)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                startingModifiers: modifiers.Select(value => value.Identity),
                rules: new EncounterRuleDefinition[] { new RotRuleDefinition(RotId, endpoint) });
            return new MechanicalCatalog("test.rot", TestContent.Hands(), new[] { encounter }, modifiers);
        }

        /** The session restored with the die the latest reroll threw showing the given face. */
        private static EncounterSession Landed(MechanicalCatalog catalog, EncounterSession session, int bay, int face)
        {
            var save = session.CaptureSave();
            Assert.That(save.LatestRerollMask, Is.EqualTo(1 << bay));
            save.CurrentDice[bay] = face;
            save.LatestChangedMask = save.HandBeforeLastReroll.Faces[bay] != face ? 1 << bay : 0;
            return Restore(catalog, save);
        }
    }
}
