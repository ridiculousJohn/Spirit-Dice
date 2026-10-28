using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierSessionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PhaseEffectsCannotEnableAnotherInventoryPredicate(bool reverse)
        {
            int face = TestContent.Session().Snapshot.CurrentDice[0];
            var grant = Modifier("grant", ModifierPhase.PlayHand, ModifierActivationLimit.Unlimited,
                new AddRerollsEffect(face));
            var conditional = new ModifierDefinition("conditional", TestFamilies.Of("test.conditional"), ModifierPhase.PlayHand,
                new ContainsRerollsRemainingCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(17) });
            var session = Session(new[] { grant, conditional },
                reverse ? new[] { "conditional", "grant" } : new[] { "grant", "conditional" },
                new[] { new StageDefinition(1000000, 3, 0) });
            var result = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(result.Events.OfType<ModifierActivatedEvent>().Select(value => value.DefinitionId),
                Is.EqualTo(new[] { "grant" }));
            Assert.That(result.Events.OfType<HandScoredEvent>().Single().Score.ModifierBaseScore, Is.Zero);
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(4, true)]
        public void HandParityConditionUsesTheHandCountBeforePlaying(int startingHands, bool expected)
            => AssertHandCountPredicate(new HandsRemainingEvenCondition(), startingHands, 0, expected);

        [Test]
        public void ResourceComparisonConditionsPreserveTheirNativeHandCountTiming()
        {
            var dice = TestContent.Session().Snapshot.CurrentDice;
            AssertHandCountPredicate(new ExtremeEqualsHandsRemainingCondition(), dice.ToArray().Min(), 0, true);
            int largestGroup = dice.ToArray().GroupBy(value => value).Max(value => value.Count());
            AssertHandCountPredicate(new LargestGroupEqualsHandsOrRerollsCondition(), largestGroup, 0, true);
            AssertHandCountPredicate(new EdgeSumEqualsHandsOrRerollsCondition(), dice[0] + dice[4], 0, true);
            AssertHandCountPredicate(new RerollsExceedHandsRemainingCondition(), 3, 3, false);
            AssertHandCountPredicate(new RerollsExceedHandsRemainingCondition(), 3, 4, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PhasePredicatesObserveResourcesBeforeAnyModifierEffects(bool reverse)
        {
            var grant = Modifier("grant", ModifierPhase.PlayHand, ModifierActivationLimit.Unlimited,
                new AddRerollsEffect(2));
            var exhausted = new ModifierDefinition("empty", TestFamilies.Of("test.empty"), ModifierPhase.PlayHand,
                new OutOfRerollsCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(17) });
            var definitions = reverse ? new[] { exhausted, grant } : new[] { grant, exhausted };
            var session = Session(definitions, reverse ? new[] { "empty", "grant" } : new[] { "grant", "empty" },
                new[] { new StageDefinition(1000000, 3, 0) });

            var result = session.Execute(PlayHandAction.Instance, 0);
            var activations = result.Events.OfType<ModifierActivatedEvent>().ToArray();
            Assert.That(activations.Select(value => value.DefinitionId),
                Is.EqualTo(reverse ? new[] { "empty", "grant" } : new[] { "grant", "empty" }));
            Assert.That(activations.Select(value => value.InstanceId), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(result.Snapshot.RerollsRemaining, Is.EqualTo(2));
            Assert.That(result.Events.OfType<HandScoredEvent>().Single().Score.ModifierBaseScore, Is.EqualTo(17));
        }

        [Test]
        public void DuplicateDefinitionsHaveIndependentOncePerHandCounters()
        {
            var definition = Modifier("reroll", ModifierPhase.PlayerReroll, ModifierActivationLimit.OncePerHand,
                new AddBaseScoreEffect(7));
            var session = Session(new[] { definition }, new[] { "reroll", "reroll" });
            var first = session.Execute(new RerollDiceAction(1), 0);
            Assert.That(first.Events.OfType<ModifierActivatedEvent>().Select(value => value.InstanceId),
                Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(first.Snapshot.ModifierBaseScore, Is.EqualTo(14));
            Assert.That(first.Snapshot.Modifiers.Select(value => value.ActivationsThisHand), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(first.Snapshot.Modifiers.Select(value => value.TotalActivations), Is.EqualTo(new long[] { 1, 1 }));

            var second = session.Execute(new RerollDiceAction(1), 1);
            Assert.That(second.Events.OfType<ModifierActivatedEvent>(), Is.Empty);
            Assert.That(second.Snapshot.ModifierBaseScore, Is.EqualTo(14));
            var played = session.Execute(PlayHandAction.Instance, 2);
            Assert.That(played.Events.OfType<HandScoredEvent>().Single().Score.ModifierBaseScore, Is.EqualTo(14));
            Assert.That(played.Snapshot.ModifierBaseScore, Is.Zero);
            Assert.That(played.Snapshot.ModifierContributions, Is.Empty);
            Assert.That(played.Snapshot.Modifiers.Select(value => value.ActivationsThisHand), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(played.Snapshot.Modifiers.Select(value => value.ActivationsThisStage), Is.EqualTo(new[] { 1, 1 }));

            var nextHand = session.Execute(new RerollDiceAction(1), 3);
            Assert.That(nextHand.Events.OfType<ModifierActivatedEvent>().Count(), Is.EqualTo(2));
            Assert.That(nextHand.Snapshot.Modifiers.Select(value => value.TotalActivations), Is.EqualTo(new long[] { 2, 2 }));
        }

        [Test]
        public void OncePerStageCountersResetOnStageEntryAndDuplicateInstancesRemainIndependent()
        {
            var definition = Modifier("stage", ModifierPhase.PlayerReroll, ModifierActivationLimit.OncePerStage,
                new AddBaseScoreEffect(1));
            var probe = TestContent.Session();
            probe.Execute(new RerollDiceAction(1), 0);
            var firstScore = probe.PreviewPlayHand();
            long threshold = (firstScore.BaseScore + 2) * firstScore.Multiplier + 1;
            var session = Session(new[] { definition }, new[] { "stage", "stage" }, new[]
            {
                new StageDefinition(threshold, 20, 20), new StageDefinition(1000000, 20, 20)
            });
            Assert.That(session.Execute(new RerollDiceAction(1), 0).Events.OfType<ModifierActivatedEvent>().Count(), Is.EqualTo(2));
            session.Execute(PlayHandAction.Instance, 1);
            Assert.That(session.Execute(new RerollDiceAction(1), 2).Events.OfType<ModifierActivatedEvent>(), Is.Empty);
            session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(session.Snapshot.StageIndex, Is.EqualTo(1), "Fixture must clear the first stage.");
            Assert.That(session.Snapshot.Modifiers.Select(value => value.ActivationsThisStage), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(session.Snapshot.Modifiers.Select(value => value.TotalActivations), Is.EqualTo(new long[] { 1, 1 }));
            var firstNextStage = session.Execute(new RerollDiceAction(1), session.Snapshot.Revision);
            Assert.That(firstNextStage.Events.OfType<ModifierActivatedEvent>().Count(), Is.EqualTo(2));
            Assert.That(firstNextStage.Snapshot.Modifiers.Select(value => value.TotalActivations), Is.EqualTo(new long[] { 2, 2 }));
        }

        [Test]
        public void ContributionsAccumulateAcrossPhasesAndResetBeforeTheNextInitialRoll()
        {
            var definitions = new[]
            {
                Modifier("initial", ModifierPhase.InitialRoll, ModifierActivationLimit.OncePerHand,
                    new AddBaseScoreEffect(5)),
                Modifier("reroll", ModifierPhase.PlayerReroll, ModifierActivationLimit.OncePerHand,
                    new AddMultiplierEffect(2)),
                Modifier("play", ModifierPhase.PlayHand, ModifierActivationLimit.OncePerHand,
                    new AddBaseScoreEffect(11))
            };
            var session = Session(definitions, definitions.Select(value => value.Id));
            Assert.That(session.Snapshot.ModifierBaseScore, Is.EqualTo(5));
            session.Execute(new RerollDiceAction(3), 0);
            var result = session.Execute(PlayHandAction.Instance, 1);
            var score = result.Events.OfType<HandScoredEvent>().Single().Score;
            Assert.That(score.ModifierBaseScore, Is.EqualTo(16));
            Assert.That(score.ModifierMultiplier, Is.EqualTo(2));
            Assert.That(score.ModifierContributions.Select(value => value.Phase), Is.EqualTo(new[]
            {
                ModifierPhase.InitialRoll, ModifierPhase.PlayerReroll, ModifierPhase.PlayHand
            }));
            Assert.That(score.HandScore, Is.EqualTo(score.BaseScore * score.Multiplier));
            Assert.That(result.Snapshot.ModifierBaseScore, Is.EqualTo(5));
            Assert.That(result.Snapshot.ModifierMultiplier, Is.Zero);
            Assert.That(result.Snapshot.ModifierContributions.Count, Is.EqualTo(1));
            Assert.That(result.Snapshot.ModifierContributions[0].DefinitionId, Is.EqualTo("initial"));
        }

        /** Checks opening-roll and last-reroll bonuses are banked before scoring, survive save and restore, and add into the scored hand / fails if the last-reroll bonus fires early, either bank is lost on restore, or scoring drops them. */
        [Test]
        public void OpeningAndLastRerollBonusesAreBankedAcrossSaveAndScored()
        {
            var opening = Modifier("opening", ModifierPhase.InitialRoll, ModifierActivationLimit.OncePerHand,
                new AddBaseScoreEffect(7));
            var lastReroll = new ModifierDefinition("last-reroll", TestFamilies.Of("test.last-reroll"), ModifierPhase.PlayerReroll,
                new LastRerollOnLastHandCondition(),
                new ModifierEffect[] { new AddMultiplierEffect(3) });
            var definitions = new[] { opening, lastReroll };
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 1, 2) },
                startingModifiers: definitions.Select(value => value.Id));
            var catalog = new MechanicalCatalog("test.modifiers", TestContent.Hands(), new[] { encounter }, definitions);
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 0);
            var banked = session.Execute(new RerollDiceAction(1), 0).Snapshot;
            Assert.That(banked.ModifierContributions.Select(value => value.DefinitionId), Is.EqualTo(new[] { "opening" }),
                "A reroll that leaves rerolls is not the last reroll.");
            banked = session.Execute(new RerollDiceAction(1), 1).Snapshot;
            Assert.That(banked.ModifierContributions.Select(value => value.DefinitionId),
                Is.EqualTo(new[] { "opening", "last-reroll" }));
            Assert.That(banked.ModifierBaseScore, Is.EqualTo(7));
            Assert.That(banked.ModifierMultiplier, Is.EqualTo(3));

            var restored = EncounterSession.RestoreEncounter(catalog, session.CaptureSave());
            Assert.That(restored.Success, Is.True, restored.Error + ": " + restored.Detail);
            var resumed = restored.Session.Snapshot;
            Assert.That(resumed.ModifierContributions, Is.EqualTo(banked.ModifierContributions));
            Assert.That(resumed.ModifierBaseScore, Is.EqualTo(banked.ModifierBaseScore));
            Assert.That(resumed.ModifierMultiplier, Is.EqualTo(banked.ModifierMultiplier));

            var score = restored.Session.Execute(PlayHandAction.Instance, resumed.Revision).Events.OfType<HandScoredEvent>().Single().Score;
            Assert.That(score.ModifierContributions, Is.EqualTo(banked.ModifierContributions));
            Assert.That(score.ModifierBaseScore, Is.EqualTo(7));
            Assert.That(score.ModifierMultiplier, Is.EqualTo(3));
            Assert.That(score.BaseScore, Is.EqualTo(score.DiceSum + score.CategoryBaseScore + score.UpgradeBaseScore + 7));
            Assert.That(score.Multiplier, Is.EqualTo(score.CategoryMultiplier + score.UpgradeMultiplier + 3));
        }

        [Test]
        public void RepeatedPreviewMatchesCommittedPlayEffectsAndPreservesFutureState()
        {
            var definitions = new[]
            {
                Modifier("initial", ModifierPhase.InitialRoll, ModifierActivationLimit.OncePerHand,
                    new AddBaseScoreEffect(5)),
                Modifier("reroll", ModifierPhase.PlayerReroll, ModifierActivationLimit.OncePerHand,
                    new AddMultiplierEffect(2)),
                Modifier("play", ModifierPhase.PlayHand, ModifierActivationLimit.OncePerHand,
                    new AddBaseScoreEffect(11),
                    new AddRerollsEffect(3))
            };
            var session = Session(definitions, definitions.Select(value => value.Id), seed: 77);
            var control = Session(definitions, definitions.Select(value => value.Id), seed: 77);
            session.Execute(new RerollDiceAction(3), 0);
            control.Execute(new RerollDiceAction(3), 0);
            var before = session.Snapshot;
            HandPreview preview = null;
            for (int i = 0; i < 20; ++i)
            {
                preview = session.PreviewHandResolution();
                AssertScoreEqual(preview.Score, session.PreviewPlayHand());
                EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
            }
            Assert.That(preview.Events.OfType<ModifierActivatedEvent>().Select(value => value.DefinitionId), Is.EqualTo(new[] { "play" }));
            Assert.That(preview.Modifiers.Select(value => value.TotalActivations), Is.EqualTo(new long[] { 1, 1, 1 }));
            var committed = session.Execute(PlayHandAction.Instance, before.Revision);
            var score = committed.Events.OfType<HandScoredEvent>().Single().Score;
            AssertScoreEqual(preview.Score, score);
            Assert.That(preview.RerollsRemaining, Is.EqualTo(committed.Snapshot.RerollsRemaining));
            Assert.That(preview.Events.OfType<ModifierActivatedEvent>().Select(value => value.Contribution),
                Is.EqualTo(committed.Events.OfType<ModifierActivatedEvent>().Where(value => value.Phase == ModifierPhase.PlayHand)
                    .Select(value => value.Contribution)));
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(PlayHandAction.Instance, 1).Snapshot, committed.Snapshot);
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(new RerollDiceAction(31), 2).Snapshot,
                session.Execute(new RerollDiceAction(31), 2).Snapshot);
        }

        [TestCase(0, CommandRejection.InvalidDiceSelection)]
        [TestCase(32, CommandRejection.InvalidDiceSelection)]
        public void RejectedActionsLeaveModifiersAndFutureDiceUnchanged(int selectedMask, CommandRejection rejection)
        {
            var modifier = Modifier("reroll", ModifierPhase.PlayerReroll, ModifierActivationLimit.OncePerHand,
                new AddBaseScoreEffect(19),
                new AddRerollsEffect(1));
            var session = Session(new[] { modifier }, new[] { modifier.Id }, seed: 14);
            var control = Session(new[] { modifier }, new[] { modifier.Id }, seed: 14);
            var before = session.Snapshot;
            var rejected = session.Execute(new RerollDiceAction(selectedMask), 0);
            Assert.That(rejected.Rejection, Is.EqualTo(rejection));
            Assert.That(rejected.Events, Is.Empty);
            EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
            Assert.That(session.Execute(PlayHandAction.Instance, -1).Rejection, Is.EqualTo(CommandRejection.StaleRevision));
            EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(new RerollDiceAction(31), 0).Snapshot,
                session.Execute(new RerollDiceAction(31), 0).Snapshot);
        }

        [Test]
        public void OverflowAfterDrawingRerollDiceDoesNotCommitRandomStateCountersOrContributions()
        {
            var modifier = Modifier("overflow", ModifierPhase.PlayerReroll, ModifierActivationLimit.OncePerHand,
                new AddBaseScoreEffect(19),
                new AddRerollsEffect(int.MaxValue));
            var session = Session(new[] { modifier }, new[] { modifier.Id }, seed: 14);
            var control = Session(new[] { modifier }, new[] { modifier.Id }, seed: 14);
            var before = session.Snapshot;
            for (int i = 0; i < 2; ++i)
            {
                Assert.Throws<OverflowException>(() => session.Execute(new RerollDiceAction(31), 0));
                EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
            }
            var committed = session.Execute(PlayHandAction.Instance, 0);
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(PlayHandAction.Instance, 0).Snapshot, committed.Snapshot);
            Assert.That(committed.Events.OfType<ModifierActivatedEvent>(), Is.Empty);
        }

        [Test]
        public void PlayOverflowAndPreviewOverflowLeaveTheEncounterRetryable()
        {
            var modifier = Modifier("overflow", ModifierPhase.PlayHand, ModifierActivationLimit.OncePerHand,
                new AddBaseScoreEffect(long.MaxValue));
            var session = Session(new[] { modifier }, new[] { modifier.Id }, seed: 14);
            var control = Session(new[] { modifier }, new[] { modifier.Id }, seed: 14);
            var before = session.Snapshot;
            Assert.Throws<OverflowException>(() => session.PreviewPlayHand());
            Assert.Throws<OverflowException>(() => session.PreviewHandResolution());
            for (int i = 0; i < 2; ++i)
            {
                Assert.Throws<OverflowException>(() => session.Execute(PlayHandAction.Instance, 0));
                EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
            }
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(new RerollDiceAction(31), 0).Snapshot,
                session.Execute(new RerollDiceAction(31), 0).Snapshot);
        }

        private static ModifierDefinition Modifier(string id, ModifierPhase phase, ModifierActivationLimit limit,
            params ModifierEffect[] effects)
            => new ModifierDefinition(id, TestFamilies.Of("test." + id), phase, new AlwaysCondition(), effects, limit);

        private static EncounterSession Session(IEnumerable<ModifierDefinition> modifiers, IEnumerable<string> inventory,
            IEnumerable<StageDefinition> stages = null, ulong seed = 0)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                stages ?? new[] { new StageDefinition(1000000, 20, 20) }, startingModifiers: inventory);
            var catalog = new MechanicalCatalog("test.modifiers", TestContent.Hands(), new[] { encounter }, modifiers);
            return EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, seed);
        }

        private static void AssertHandCountPredicate(IModifierCondition condition, int hands, int rerolls, bool expected)
        {
            var modifier = new ModifierDefinition("condition", TestFamilies.Of("test.condition"), ModifierPhase.PlayHand,
                condition, new ModifierEffect[] { new AddBaseScoreEffect(17) });
            var session = Session(new[] { modifier }, new[] { modifier.Id },
                new[] { new StageDefinition(1000000, hands, rerolls) });
            var result = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(result.Events.OfType<ModifierActivatedEvent>().Any(), Is.EqualTo(expected), condition.GetType().Name);
            Assert.That(result.Events.OfType<HandScoredEvent>().Single().Score.ModifierBaseScore,
                Is.EqualTo(expected ? 17 : 0), condition.GetType().Name);
        }

        private static void AssertScoreEqual(ScoreBreakdown expected, ScoreBreakdown actual)
        {
            foreach (var property in typeof(ScoreBreakdown).GetProperties())
                Assert.That(property.GetValue(actual), Is.EqualTo(property.GetValue(expected)), property.Name);
        }
    }
}
