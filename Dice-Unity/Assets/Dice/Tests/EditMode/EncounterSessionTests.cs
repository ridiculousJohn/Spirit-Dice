using System;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class EncounterSessionTests
    {
        [Test]
        public void RandomGeneratorMatchesKnownSplitMix64SequenceAndCanResume()
        {
            var random = new DeterministicRandom(0);
            Assert.That(random.NextUInt64(), Is.EqualTo(0xe220a8397b1dcdafUL));
            Assert.That(random.NextUInt64(), Is.EqualTo(0x6e789e6aa1b965f4UL));
            Assert.That(random.NextUInt64(), Is.EqualTo(0x06c45d188009454fUL));
            var restored = new DeterministicRandom(random.State);
            for (int i = 0; i < 100; ++i)
                Assert.That(restored.NextInt(6), Is.EqualTo(random.NextInt(6)));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
        }

        [Test]
        public void InitialResolutionContainsOrderedFactsAndNoHandIsSpent()
        {
            var session = TestContent.Session(stages: new[] { new StageDefinition(100, 3, 2) });
            Assert.That(session.InitialResolution.PreviousRevision, Is.EqualTo(-1));
            Assert.That(session.Snapshot.Revision, Is.Zero);
            Assert.That(session.Snapshot.HandsRemaining, Is.EqualTo(3));
            Assert.That(session.Snapshot.RerollsRemaining, Is.EqualTo(2));
            Assert.That(session.Snapshot.InitialDice, Is.EqualTo(session.Snapshot.CurrentDice));
            Assert.That(session.InitialResolution.Events.Select(value => value.GetType()), Is.EqualTo(new[]
            {
                typeof(StageStartedEvent), typeof(HandStartedEvent), typeof(DiceRolledEvent)
            }));
            var roll = (DiceRolledEvent)session.InitialResolution.Events[2];
            Assert.That(roll.Kind, Is.EqualTo(RollKind.Initial));
            Assert.That(roll.Before, Is.Null);
            Assert.That(roll.After.IsValid, Is.True);
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(31)]
        public void RerollOnlyChangesSelectedPositionsAndCostsOneAction(int selectedMask)
        {
            var session = TestContent.Session();
            EncounterSnapshot before = session.Snapshot;
            Resolution result = session.Execute(new RerollDiceAction(selectedMask), before.Revision);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Snapshot.RerollsRemaining, Is.EqualTo(before.RerollsRemaining - 1));
            Assert.That(result.Snapshot.HandsRemaining, Is.EqualTo(before.HandsRemaining));
            Assert.That(result.Snapshot.TotalRerollsSpent, Is.EqualTo(1));
            Assert.That(result.Snapshot.LatestRerollMask, Is.EqualTo(selectedMask));
            Assert.That(result.Snapshot.EverRerolledMask, Is.EqualTo(selectedMask));
            Assert.That(result.Snapshot.DiceBeforeLastReroll, Is.EqualTo(before.CurrentDice));
            Assert.That(result.Snapshot.InitialDice, Is.EqualTo(before.InitialDice));
            int changedMask = 0;
            for (int i = 0; i < 5; ++i)
            {
                if ((selectedMask & (1 << i)) == 0)
                    Assert.That(result.Snapshot.CurrentDice[i], Is.EqualTo(before.CurrentDice[i]));
                if (result.Snapshot.CurrentDice[i] != before.CurrentDice[i]) changedMask |= 1 << i;
            }
            Assert.That(result.Snapshot.LatestChangedMask, Is.EqualTo(changedMask));
            Assert.That(result.Events[0], Is.TypeOf<ResourceChangedEvent>());
            Assert.That(result.Events[1], Is.TypeOf<DiceRolledEvent>());
            Assert.That(before.Revision, Is.Zero);
        }

        [Test]
        public void RerollingIntoTheSameValueStillCountsAsRerolled()
        {
            bool foundUnchangedRoll = false;
            for (ulong seed = 0; seed < 100; ++seed)
            {
                var session = TestContent.Session(seed);
                var before = session.Snapshot;
                var after = session.Execute(new RerollDiceAction(1), 0).Snapshot;
                if (before.CurrentDice[0] != after.CurrentDice[0]) continue;
                Assert.That(after.LatestChangedMask, Is.Zero);
                Assert.That(after.LatestRerollMask, Is.EqualTo(1));
                Assert.That(after.EverRerolledMask, Is.EqualTo(1));
                Assert.That(after.RerollsSpentOnHand, Is.EqualTo(1));
                foundUnchangedRoll = true;
                break;
            }
            Assert.That(foundUnchangedRoll, Is.True);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(32)]
        public void InvalidSelectionDoesNotChangeStateOrFutureRolls(int selectedMask)
        {
            var session = TestContent.Session(14);
            var control = TestContent.Session(14);
            var before = session.Snapshot;
            var rejected = session.Execute(new RerollDiceAction(selectedMask), 0);
            Assert.That(rejected.Rejection, Is.EqualTo(CommandRejection.InvalidDiceSelection));
            Assert.That(rejected.Events, Is.Empty);
            AssertSnapshotsEqual(before, session.Snapshot);
            AssertSnapshotsEqual(session.Execute(new RerollDiceAction(31), 0).Snapshot,
                control.Execute(new RerollDiceAction(31), 0).Snapshot);
        }

        [Test]
        public void StaleActionsCannotSpendResourcesTwice()
        {
            var session = TestContent.Session();
            session.Execute(new RerollDiceAction(1), 0);
            var before = session.Snapshot;
            var rejected = session.Execute(new RerollDiceAction(1), 0);
            Assert.That(rejected.Rejection, Is.EqualTo(CommandRejection.StaleRevision));
            Assert.That(rejected.Events, Is.Empty);
            AssertSnapshotsEqual(before, session.Snapshot);
        }

        [Test]
        public void EmptyRerollBudgetStillAllowsPlaying()
        {
            var session = TestContent.Session(stages: new[] { new StageDefinition(1, 1, 0) });
            Assert.That(session.Execute(new RerollDiceAction(1), 0).Rejection, Is.EqualTo(CommandRejection.NoRerollsRemaining));
            Assert.That(session.Execute(PlayHandAction.Instance, 0).Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
        }

        [Test]
        public void PreviewMatchesCommittedScoreAndDoesNotChangeFutureState()
        {
            var session = TestContent.Session(77);
            var control = TestContent.Session(77);
            var before = session.Snapshot;
            ScoreBreakdown preview = null;
            for (int i = 0; i < 20; ++i) preview = session.PreviewPlayHand();
            AssertSnapshotsEqual(before, session.Snapshot);
            var committed = session.Execute(PlayHandAction.Instance, 0);
            var scored = committed.Events.OfType<HandScoredEvent>().Single().Score;
            Assert.That(scored.HandScore, Is.EqualTo(preview.HandScore));
            Assert.That(scored.BaseScore, Is.EqualTo(preview.BaseScore));
            Assert.That(scored.Multiplier, Is.EqualTo(preview.Multiplier));
            AssertSnapshotsEqual(committed.Snapshot, control.Execute(PlayHandAction.Instance, 0).Snapshot);
        }

        [Test]
        public void NewHandResetsHandHistoryButPreservesStageRerollsAndPlayedDice()
        {
            var session = TestContent.Session();
            var rerolled = session.Execute(new RerollDiceAction(3), 0).Snapshot;
            var played = session.Execute(PlayHandAction.Instance, 1);
            var after = played.Snapshot;
            Assert.That(after.HandIndex, Is.EqualTo(1));
            Assert.That(after.RerollsSpentOnHand, Is.Zero);
            Assert.That(after.EverRerolledMask, Is.Zero);
            Assert.That(after.LatestChangedMask, Is.Zero);
            Assert.That(after.LatestRerollMask, Is.Zero);
            Assert.That(after.DiceBeforeLastReroll, Is.Null);
            Assert.That(after.PreviousPlayedDice, Is.EqualTo(rerolled.CurrentDice));
            Assert.That(after.RerollsRemaining, Is.EqualTo(rerolled.RerollsRemaining));
            Assert.That(after.HandsRemaining, Is.EqualTo(rerolled.HandsRemaining - 1));
            var scored = played.Events.OfType<HandScoredEvent>().Single();
            var rolled = played.Events.OfType<DiceRolledEvent>().Single();
            Assert.That(scored.HandIndex, Is.Zero);
            Assert.That(scored.Dice, Is.EqualTo(rerolled.CurrentDice));
            Assert.That(rolled.HandIndex, Is.EqualTo(1));
        }

        [Test]
        public void LastHandClearWinsAndExcessScoreDoesNotBecomeNegativeThreshold()
        {
            var session = TestContent.Session(stages: new[] { new StageDefinition(1, 1, 0) });
            var result = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(result.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(result.Snapshot.HandsRemaining, Is.Zero);
            Assert.That(result.Snapshot.RemainingThreshold, Is.Zero);
            var threshold = result.Events.OfType<ThresholdChangedEvent>().Single();
            Assert.That(threshold.AppliedScore, Is.EqualTo(1));
            Assert.That(threshold.ExcessScore, Is.EqualTo(threshold.HandScore - 1));
            Assert.That(result.Snapshot.TotalScore, Is.EqualTo(threshold.HandScore));
            Assert.That(result.Events.Last(), Is.TypeOf<EncounterCompletedEvent>());
            Assert.That(result.Events.OfType<DiceRolledEvent>(), Is.Empty);
        }

        [Test]
        public void ExhaustingHandsLosesWithoutRollingAnotherHand()
        {
            var session = TestContent.Session(stages: new[] { new StageDefinition(100000, 1, 5) });
            var result = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(result.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            Assert.That(result.Snapshot.RerollsRemaining, Is.EqualTo(5));
            Assert.That(result.Events.OfType<DiceRolledEvent>(), Is.Empty);
            Assert.That(result.Events.OfType<StageClearedEvent>(), Is.Empty);
            Assert.That(session.Execute(PlayHandAction.Instance, 1).Rejection, Is.EqualTo(CommandRejection.EncounterCompleted));
        }

        [Test]
        public void StageTransitionReplenishesBudgetsAndRetainsPreviousStageHistory()
        {
            var session = TestContent.Session(stages: new[]
            {
                new StageDefinition(1, 3, 3), new StageDefinition(1000, 4, 4)
            });
            session.Execute(new RerollDiceAction(1), 0);
            var playedDice = session.Snapshot.CurrentDice;
            var result = session.Execute(PlayHandAction.Instance, 1);
            Assert.That(result.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(result.Snapshot.HandIndex, Is.Zero);
            Assert.That(result.Snapshot.HandsRemaining, Is.EqualTo(4));
            Assert.That(result.Snapshot.RerollsRemaining, Is.EqualTo(4));
            Assert.That(result.Snapshot.RemainingThreshold, Is.EqualTo(1000));
            Assert.That(result.Snapshot.PreviousStageStartingHands, Is.EqualTo(3));
            Assert.That(result.Snapshot.PreviousStageHandsRemaining, Is.EqualTo(2));
            Assert.That(result.Snapshot.PreviousPlayedDice, Is.EqualTo(playedDice));
            Assert.That(result.Snapshot.HandsPlayedInStage, Is.Zero);
            Assert.That(result.Events.OfType<StageClearedEvent>().Single().StageIndex, Is.Zero);
            Assert.That(result.Events.OfType<DiceRolledEvent>().Single().StageIndex, Is.EqualTo(1));
        }

        [Test]
        public void RewardBoundaryIsExplicitAndDoesNotSkipToNextStage()
        {
            var session = TestContent.Session(stages: new[]
            {
                new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards), new StageDefinition(100, 3, 3)
            });
            var result = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(result.Snapshot.Phase, Is.EqualTo(EncounterPhase.AwaitingRewards));
            Assert.That(result.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None));
            Assert.That(result.Snapshot.StageIndex, Is.Zero);
            Assert.That(result.Events.Last(), Is.TypeOf<RewardsRequiredEvent>());
            Assert.That(result.Events.OfType<DiceRolledEvent>(), Is.Empty);
            Assert.That(session.Execute(PlayHandAction.Instance, 1).Rejection, Is.EqualTo(CommandRejection.InvalidPhase));
            Assert.That(session.Execute(new RerollDiceAction(1), 1).Rejection, Is.EqualTo(CommandRejection.InvalidPhase));
            Assert.That(session.Execute(AbandonEncounterAction.Instance, 1).Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
        }

        [Test]
        public void FinalStageCompletesWithoutOfferingMoreRewards()
        {
            var session = TestContent.Session(stages: new[] { new StageDefinition(1, 1, 0, StageExitBehavior.AwaitRewards) });
            var result = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(result.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(result.Events.OfType<RewardsRequiredEvent>(), Is.Empty);
        }

        [Test]
        public void TwentyFiveStageFixtureIsDeterministicAndCompletes()
        {
            var stages = Enumerable.Range(0, 25).Select(i => new StageDefinition(1, 3, 3)).ToArray();
            var first = TestContent.Session(991, stages);
            var second = TestContent.Session(991, stages);
            for (int i = 0; i < 25; ++i)
            {
                AssertSnapshotsEqual(first.Snapshot, second.Snapshot);
                first.Execute(new RerollDiceAction(1 << (i % 5)), first.Snapshot.Revision);
                second.Execute(new RerollDiceAction(1 << (i % 5)), second.Snapshot.Revision);
                var a = first.Execute(PlayHandAction.Instance, first.Snapshot.Revision);
                var b = second.Execute(PlayHandAction.Instance, second.Snapshot.Revision);
                Assert.That(a.Events.Select(value => value.GetType()), Is.EqualTo(b.Events.Select(value => value.GetType())));
                AssertSnapshotsEqual(a.Snapshot, b.Snapshot);
            }
            Assert.That(first.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(first.Snapshot.TotalHandsPlayed, Is.EqualTo(25));
            Assert.That(first.Snapshot.TotalRerollsSpent, Is.EqualTo(25));
        }

        [Test]
        public void OverflowDoesNotPartiallyCommitAHand()
        {
            var hands = TestContent.Hands().Select(hand => new HandDefinition(hand.Id, hand.Category, long.MaxValue / 2 - 100, 1, 0, 0, 0));
            var catalog = TestContent.Catalog(new[] { new StageDefinition(long.MaxValue, 4, 0) }, hands: hands);
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 0);
            session.Execute(PlayHandAction.Instance, 0);
            session.Execute(PlayHandAction.Instance, 1);
            var before = session.Snapshot;
            Assert.Throws<OverflowException>(() => session.Execute(PlayHandAction.Instance, 2));
            AssertSnapshotsEqual(before, session.Snapshot);
            Assert.Throws<OverflowException>(() => session.Execute(PlayHandAction.Instance, 2));
        }

        internal static void AssertSnapshotsEqual(EncounterSnapshot expected, EncounterSnapshot actual)
        {
            foreach (var property in typeof(EncounterSnapshot).GetProperties())
                Assert.That(property.GetValue(actual), Is.EqualTo(property.GetValue(expected)), property.Name);
        }
    }
}
