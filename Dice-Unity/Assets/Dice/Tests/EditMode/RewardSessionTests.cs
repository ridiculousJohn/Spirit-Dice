using System;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class RewardSessionTests
    {
        [Test]
        public void ClearOffersModifierThenUpgradeAndOnlyBothSelectionsStartTheNextStage()
        {
            var catalog = RewardTestContent.Catalog();
            var session = RewardTestContent.Session(catalog);
            var initial = session.Snapshot;
            Assert.That(initial.RewardRefreshes, Is.EqualTo(1));
            Assert.That(initial.Modifiers, Is.Empty);
            Assert.That(initial.PendingReward, Is.Null);
            var clear = session.Execute(PlayHandAction.Instance, 0);
            var offered = clear.Snapshot.PendingReward;
            Assert.That(clear.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
            Assert.That(clear.Snapshot.StageIndex, Is.Zero);
            Assert.That(clear.Snapshot.RewardRefreshes, Is.EqualTo(2));
            Assert.That(offered.Kind, Is.EqualTo(RewardKind.Modifier));
            Assert.That(offered.TargetStageIndex, Is.EqualTo(1));
            Assert.That(clear.Events.OfType<DiceRolledEvent>(), Is.Empty);
            var credit = clear.Events.OfType<ResourceChangedEvent>().Single(value => value.Resource == ResourceKind.RewardRefreshes);
            Assert.That(credit.Cause, Is.EqualTo(ResourceChangeCause.StageClear));
            Assert.That(credit.Before, Is.EqualTo(1));
            Assert.That(credit.After, Is.EqualTo(2));
            Assert.That(clear.Events.OfType<RewardOfferedEvent>().Single().Offer, Is.EqualTo(offered));
            var selected = offered.Options[0];
            var modifierResult = session.Execute(new SelectModifierAction(offered.OfferId, selected.OptionId), 1);
            Assert.That(modifierResult.Snapshot.StageIndex, Is.Zero);
            Assert.That(modifierResult.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingHandUpgrade));
            Assert.That(modifierResult.Snapshot.PendingReward.Kind, Is.EqualTo(RewardKind.HandUpgrade));
            Assert.That(modifierResult.Snapshot.PendingReward.TargetStageIndex, Is.EqualTo(1));
            Assert.That(modifierResult.Snapshot.Modifiers.Count, Is.EqualTo(1));
            Assert.That(modifierResult.Snapshot.Modifiers[0].DefinitionId, Is.EqualTo(selected.DefinitionId));
            Assert.That(modifierResult.Snapshot.Modifiers[0].InstanceId, Is.EqualTo(1));
            Assert.That(modifierResult.Snapshot.Modifiers[0].TotalActivations, Is.Zero);
            Assert.That(modifierResult.Snapshot.ExcludedRewardFamilies, Is.Empty);
            Assert.That(modifierResult.Events.OfType<DiceRolledEvent>(), Is.Empty);
            Assert.That(modifierResult.Events.OfType<ModifierAcquiredEvent>().Single().DefinitionId, Is.EqualTo(selected.DefinitionId));
            Assert.That(modifierResult.Events.OfType<RewardSelectedEvent>().Single().Option, Is.EqualTo(selected));

            var upgradeOffer = modifierResult.Snapshot.PendingReward;
            var upgrade = upgradeOffer.Options[0];
            var category = upgrade.Category.Value;
            var upgradeResult = session.Execute(new SelectHandUpgradeAction(upgradeOffer.OfferId, upgrade.OptionId), 2);
            Assert.That(upgradeResult.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(upgradeResult.Snapshot.Phase, Is.EqualTo(EncounterPhase.Playing));
            Assert.That(upgradeResult.Snapshot.PendingReward, Is.Null);
            Assert.That(upgradeResult.Snapshot.HandsRemaining, Is.EqualTo(3));
            Assert.That(upgradeResult.Snapshot.RerollsRemaining, Is.EqualTo(3));
            Assert.That(upgradeResult.Snapshot.HandUpgrades[(int)category], Is.EqualTo(1));
            Assert.That(upgradeResult.Snapshot.RecentHandUpgrades, Is.EqualTo(new[] { category }));
            Assert.That(upgradeResult.Snapshot.PreviousPlayedDice, Is.EqualTo(initial.CurrentDice));
            Assert.That(upgradeResult.Snapshot.PreviousStageStartingHands, Is.EqualTo(3));
            Assert.That(upgradeResult.Snapshot.PreviousStageHandsRemaining, Is.EqualTo(2));
            var upgraded = upgradeResult.Events.OfType<HandUpgradedEvent>().Single();
            Assert.That(upgraded.Category, Is.EqualTo(category));
            Assert.That(upgraded.Before, Is.Zero);
            Assert.That(upgraded.After, Is.EqualTo(1));
            Assert.That(upgradeResult.Events.OfType<StageStartedEvent>().Single().StageIndex, Is.EqualTo(1));
            Assert.That(upgradeResult.Events.OfType<DiceRolledEvent>().Single().Kind, Is.EqualTo(RollKind.Initial));
            var next = upgradeResult.Snapshot;
            var hand = catalog.GetHand(HandEvaluator.Evaluate(next.CurrentDice));
            int count = next.HandUpgrades[(int)hand.Category];
            long expectedBase = next.CurrentDice.Sum + hand.BaseScore + count * hand.BaseScorePerUpgrade + 10;
            long expectedMultiplier = hand.Multiplier + count * hand.MultiplierPerUpgrade;
            Assert.That(session.PreviewPlayHand().HandScore, Is.EqualTo(expectedBase * expectedMultiplier));
        }

        [Test]
        public void RefreshSpendsCurrencyReplacesIdentitiesAndPreservesTheClearedStage()
        {
            var session = RewardTestContent.Session();
            session.Execute(PlayHandAction.Instance, 0);
            var before = session.Snapshot;
            var offer = before.PendingReward;
            long[] originalOptions = offer.Options.Select(value => value.OptionId).ToArray();
            var result = session.Execute(new RefreshModifierOfferAction(offer.OfferId), 1);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Snapshot.PendingReward.OfferId, Is.GreaterThan(offer.OfferId));
            Assert.That(result.Snapshot.PendingReward.Options.Select(value => value.OptionId).Min(), Is.GreaterThan(originalOptions.Max()));
            Assert.That(result.Snapshot.RewardRefreshes, Is.EqualTo(1));
            Assert.That(result.Snapshot.StageIndex, Is.EqualTo(before.StageIndex));
            Assert.That(result.Snapshot.CurrentDice, Is.EqualTo(before.CurrentDice));
            Assert.That(result.Snapshot.HandsRemaining, Is.EqualTo(before.HandsRemaining));
            Assert.That(result.Snapshot.RemainingThreshold, Is.EqualTo(before.RemainingThreshold));
            Assert.That(result.Events.OfType<DiceRolledEvent>(), Is.Empty);
            Assert.That(result.Events.OfType<RewardOfferedEvent>().Single().IsRefresh, Is.True);
            var spent = result.Events.OfType<ResourceChangedEvent>().Single();
            Assert.That(spent.Resource, Is.EqualTo(ResourceKind.RewardRefreshes));
            Assert.That(spent.Cause, Is.EqualTo(ResourceChangeCause.RefreshReward));
            Assert.That(spent.Before, Is.EqualTo(2));
            Assert.That(spent.After, Is.EqualTo(1));
            Assert.That(offer.Options.Select(value => value.OptionId), Is.EqualTo(originalOptions));
            var rejected = session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), 2);
            Assert.That(rejected.Rejection, Is.EqualTo(CommandRejection.InvalidOffer));
            Assert.That(rejected.Events, Is.Empty);
            EncounterSessionTests.AssertSnapshotsEqual(result.Snapshot, session.Snapshot);
        }

        [Test]
        public void InvalidPhaseOfferOptionRevisionAndReplayNeverPartiallySelectAReward()
        {
            var session = RewardTestContent.Session();
            var control = RewardTestContent.Session();
            AssertRejected(session, new SelectModifierAction(0, 0), CommandRejection.InvalidPhase);
            AssertRejected(session, new SelectHandUpgradeAction(0, 0), CommandRejection.InvalidPhase);
            AssertRejected(session, new RefreshModifierOfferAction(0), CommandRejection.InvalidPhase);
            session.Execute(PlayHandAction.Instance, 0);
            control.Execute(PlayHandAction.Instance, 0);
            var offer = session.Snapshot.PendingReward;
            AssertRejected(session, new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId), CommandRejection.InvalidPhase);
            AssertRejected(session, new SelectModifierAction(offer.OfferId + 1, offer.Options[0].OptionId), CommandRejection.InvalidOffer);
            AssertRejected(session, new SelectModifierAction(offer.OfferId, -1), CommandRejection.InvalidRewardOption);
            AssertRejected(session, new RefreshModifierOfferAction(offer.OfferId + 1), CommandRejection.InvalidOffer);
            var before = session.Snapshot;
            var choice = new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId);
            Assert.That(session.Execute(choice, before.Revision - 1).Rejection, Is.EqualTo(CommandRejection.StaleRevision));
            EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
            var accepted = session.Execute(choice, before.Revision);
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(choice, before.Revision).Snapshot, accepted.Snapshot);
            Assert.That(session.Execute(choice, before.Revision).Rejection, Is.EqualTo(CommandRejection.StaleRevision));
            AssertRejected(session, choice, CommandRejection.InvalidPhase);
            var upgradeOffer = session.Snapshot.PendingReward;
            AssertRejected(session, new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId), CommandRejection.InvalidOffer);
            AssertRejected(session, new SelectHandUpgradeAction(upgradeOffer.OfferId, -1), CommandRejection.InvalidRewardOption);
            var upgrade = new SelectHandUpgradeAction(upgradeOffer.OfferId, upgradeOffer.Options[0].OptionId);
            var after = session.Execute(upgrade, session.Snapshot.Revision);
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(upgrade, control.Snapshot.Revision).Snapshot, after.Snapshot);
            AssertRejected(session, upgrade, CommandRejection.InvalidPhase);
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(new RerollDiceAction(31), control.Snapshot.Revision).Snapshot,
                session.Execute(new RerollDiceAction(31), session.Snapshot.Revision).Snapshot);
        }

        [Test]
        public void ExhaustedRefreshCurrencyRejectsWithoutReplacingTheOfferOrAdvancingRewardRandomness()
        {
            var session = RewardTestContent.Session();
            var control = RewardTestContent.Session();
            session.Execute(PlayHandAction.Instance, 0);
            control.Execute(PlayHandAction.Instance, 0);
            for (int i = 0; i < 2; ++i)
            {
                var action = new RefreshModifierOfferAction(session.Snapshot.PendingReward.OfferId);
                EncounterSessionTests.AssertSnapshotsEqual(control.Execute(action, control.Snapshot.Revision).Snapshot,
                    session.Execute(action, session.Snapshot.Revision).Snapshot);
            }
            Assert.That(session.Snapshot.RewardRefreshes, Is.Zero);
            AssertRejected(session, new RefreshModifierOfferAction(session.Snapshot.PendingReward.OfferId),
                CommandRejection.InsufficientRewardRefreshes);
            SelectFirstPair(session);
            SelectFirstPair(control);
            session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            control.Execute(PlayHandAction.Instance, control.Snapshot.Revision);
            EncounterSessionTests.AssertSnapshotsEqual(control.Snapshot, session.Snapshot);
        }

        [Test]
        public void ExtraRewardRefreshesDoNotChangeTheDiceStream()
        {
            int[] startingUpgrades = { 0, 0, 0, 0, 6, 6, 6, 6 };
            var catalog = RewardTestContent.Catalog(upgrades: startingUpgrades);
            var session = RewardTestContent.Session(catalog, 77);
            var control = RewardTestContent.Session(catalog, 77);
            session.Execute(PlayHandAction.Instance, 0);
            control.Execute(PlayHandAction.Instance, 0);
            session.Execute(new RefreshModifierOfferAction(session.Snapshot.PendingReward.OfferId), 1);
            SelectPair(session, "first", HandCategory.HighDie);
            SelectPair(control, "first", HandCategory.HighDie);
            Assert.That(session.Snapshot.Revision, Is.EqualTo(control.Snapshot.Revision + 1));
            Assert.That(session.Snapshot.CurrentDice, Is.EqualTo(control.Snapshot.CurrentDice));
            Assert.That(session.Snapshot.InitialDice, Is.EqualTo(control.Snapshot.InitialDice));
            Assert.That(session.Snapshot.HandUpgrades, Is.EqualTo(control.Snapshot.HandUpgrades));
            Assert.That(session.Snapshot.Modifiers, Is.EqualTo(control.Snapshot.Modifiers));
            for (int i = 0; i < 3; ++i)
            {
                var a = session.Execute(new RerollDiceAction(1 << i), session.Snapshot.Revision);
                var b = control.Execute(new RerollDiceAction(1 << i), control.Snapshot.Revision);
                Assert.That(a.Accepted, Is.True);
                Assert.That(a.Snapshot.CurrentDice, Is.EqualTo(b.Snapshot.CurrentDice));
                Assert.That(a.Snapshot.LatestChangedMask, Is.EqualTo(b.Snapshot.LatestChangedMask));
            }
        }

        [Test]
        public void ModifierRefreshesDoNotChangeTheAlreadyGeneratedHandUpgradeOffer()
        {
            for (ulong seed = 0; seed < 20; ++seed)
            {
                var session = RewardTestContent.Session(seed: seed);
                var control = RewardTestContent.Session(seed: seed);
                session.Execute(PlayHandAction.Instance, 0);
                control.Execute(PlayHandAction.Instance, 0);
                session.Execute(new RefreshModifierOfferAction(session.Snapshot.PendingReward.OfferId), 1);
                var first = session.Snapshot.PendingReward;
                var second = control.Snapshot.PendingReward;
                var firstChoice = first.Options.Single(value => value.DefinitionId == TestModifiers.IdOf("first"));
                var secondChoice = second.Options.Single(value => value.DefinitionId == TestModifiers.IdOf("first"));
                session.Execute(new SelectModifierAction(first.OfferId, firstChoice.OptionId), session.Snapshot.Revision);
                control.Execute(new SelectModifierAction(second.OfferId, secondChoice.OptionId), control.Snapshot.Revision);
                Assert.That(session.Snapshot.PendingReward.Options.Select(value => value.Category),
                    Is.EqualTo(control.Snapshot.PendingReward.Options.Select(value => value.Category)));
            }
        }

        [Test]
        public void CappedUpgradePoolSkipsTheUpgradeDecisionWithoutBlockingStageEntry()
        {
            var session = RewardTestContent.Session(RewardTestContent.Catalog(
                upgrades: Enumerable.Repeat(6, HandDefinition.CategoryCount).ToArray()));
            session.Execute(PlayHandAction.Instance, 0);
            var offer = session.Snapshot.PendingReward;
            var result = session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), 1);
            Assert.That(result.Snapshot.Phase, Is.EqualTo(EncounterPhase.Playing));
            Assert.That(result.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(result.Snapshot.PendingReward, Is.Null);
            Assert.That(result.Events.OfType<RewardSkippedEvent>().Single().Kind, Is.EqualTo(RewardKind.HandUpgrade));
            Assert.That(result.Events.OfType<HandUpgradedEvent>(), Is.Empty);
        }

        [Test]
        public void ExhaustedModifierAndUpgradePoolsSkipBothDecisionsAndContinueTheEncounter()
        {
            var owned = RewardTestContent.Modifier("owned", "family.owned");
            var catalog = RewardTestContent.Catalog(modifiers: new[] { owned }, inventory: new[] { owned.Identity },
                upgrades: Enumerable.Repeat(6, HandDefinition.CategoryCount).ToArray());
            var session = RewardTestContent.Session(catalog);
            var result = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(result.Snapshot.Phase, Is.EqualTo(EncounterPhase.Playing));
            Assert.That(result.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(result.Snapshot.PendingReward, Is.Null);
            Assert.That(result.Events.OfType<RewardSkippedEvent>().Select(value => value.Kind),
                Is.EqualTo(new[] { RewardKind.Modifier, RewardKind.HandUpgrade }));
            Assert.That(result.Events.OfType<DiceRolledEvent>().Single().StageIndex, Is.EqualTo(1));
        }

        [Test]
        public void RefreshPolicyAmountsAreAppliedExactlyAndUnusedCurrencyCarriesAcrossStages()
        {
            var policy = RewardTestContent.Policy(startingRefreshes: 3, refreshesPerClear: 2, refreshCost: 2);
            var session = RewardTestContent.Session(RewardTestContent.Catalog(policy: policy));
            session.Execute(PlayHandAction.Instance, 0);
            Assert.That(session.Snapshot.RewardRefreshes, Is.EqualTo(5));
            for (int i = 0; i < 2; ++i)
                Assert.That(session.Execute(new RefreshModifierOfferAction(session.Snapshot.PendingReward.OfferId),
                    session.Snapshot.Revision).Accepted, Is.True);
            Assert.That(session.Snapshot.RewardRefreshes, Is.EqualTo(1));
            AssertRejected(session, new RefreshModifierOfferAction(session.Snapshot.PendingReward.OfferId),
                CommandRejection.InsufficientRewardRefreshes);
            SelectFirstPair(session);
            session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(session.Snapshot.RewardRefreshes, Is.EqualTo(3));
        }

        /** Checks the snapshot states the refresh cost and how many refreshes the held points pay for, and states none for an encounter without a reward policy / fails if the cost or count is missing, rounded up or defaulted. */
        [Test]
        public void SnapshotStatesTheRefreshCostAndTheRefreshesAvailable()
        {
            var policy = RewardTestContent.Policy(startingRefreshes: 3, refreshesPerClear: 2, refreshCost: 2);
            var session = RewardTestContent.Session(RewardTestContent.Catalog(policy: policy));
            Assert.That(session.Snapshot.RewardRefreshCost, Is.EqualTo(2));
            Assert.That(session.Snapshot.RewardRefreshesAvailable, Is.EqualTo(1));
            session.Execute(PlayHandAction.Instance, 0);
            Assert.That(session.Snapshot.RewardRefreshesAvailable, Is.EqualTo(2));
            for (int i = 0; i < 2; ++i)
                session.Execute(new RefreshModifierOfferAction(session.Snapshot.PendingReward.OfferId), session.Snapshot.Revision);
            Assert.That(session.Snapshot.RewardRefreshes, Is.EqualTo(1));
            Assert.That(session.Snapshot.RewardRefreshesAvailable, Is.Zero);
            var withoutPolicy = TestContent.Session().Snapshot;
            Assert.That(withoutPolicy.RewardRefreshCost, Is.Zero);
            Assert.That(withoutPolicy.RewardRefreshesAvailable, Is.Zero);
        }

        [Test]
        public void RefreshCreditOverflowDoesNotCommitTheClearedHandOrItsFutureDice()
        {
            var policy = RewardTestContent.Policy(startingRefreshes: int.MaxValue);
            var catalog = RewardTestContent.Catalog(policy: policy);
            var session = RewardTestContent.Session(catalog, 14);
            var control = RewardTestContent.Session(catalog, 14);
            var before = session.Snapshot;
            for (int i = 0; i < 2; ++i)
            {
                Assert.Throws<OverflowException>(() => session.Execute(PlayHandAction.Instance, 0));
                EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
            }
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(new RerollDiceAction(31), 0).Snapshot,
                session.Execute(new RerollDiceAction(31), 0).Snapshot);
        }

        [Test]
        public void FinalClearCompletesWithoutAnOfferOrARefreshCredit()
        {
            var session = RewardTestContent.Session(RewardTestContent.Catalog(stages: new[]
            {
                new StageDefinition(1, 1, 0, StageExitBehavior.AwaitRewards)
            }));
            var result = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(result.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(result.Snapshot.RewardRefreshes, Is.EqualTo(1));
            Assert.That(result.Snapshot.PendingReward, Is.Null);
            Assert.That(result.Events.OfType<RewardOfferedEvent>(), Is.Empty);
            Assert.That(result.Events.OfType<ResourceChangedEvent>().Any(value => value.Resource == ResourceKind.RewardRefreshes), Is.False);
            AssertRejected(session, new SelectModifierAction(0, 0), CommandRejection.EncounterCompleted);
            AssertRejected(session, new SelectHandUpgradeAction(0, 0), CommandRejection.EncounterCompleted);
            AssertRejected(session, new RefreshModifierOfferAction(0), CommandRejection.EncounterCompleted);
        }

        private static void AssertRejected(EncounterSession session, EncounterAction action, CommandRejection expected)
        {
            var before = session.Snapshot;
            var result = session.Execute(action, before.Revision);
            Assert.That(result.Rejection, Is.EqualTo(expected));
            Assert.That(result.Events, Is.Empty);
            EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
        }

        private static void SelectFirstPair(EncounterSession session)
        {
            var offer = session.Snapshot.PendingReward;
            Assert.That(session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), session.Snapshot.Revision).Accepted, Is.True);
            offer = session.Snapshot.PendingReward;
            Assert.That(session.Execute(new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId), session.Snapshot.Revision).Accepted, Is.True);
        }

        private static void SelectPair(EncounterSession session, string modifierId, HandCategory category)
        {
            var offer = session.Snapshot.PendingReward;
            var option = offer.Options.Single(value => value.DefinitionId == TestModifiers.IdOf(modifierId));
            Assert.That(session.Execute(new SelectModifierAction(offer.OfferId, option.OptionId), session.Snapshot.Revision).Accepted, Is.True);
            offer = session.Snapshot.PendingReward;
            option = offer.Options.Single(value => value.Category == category);
            Assert.That(session.Execute(new SelectHandUpgradeAction(offer.OfferId, option.OptionId), session.Snapshot.Revision).Accepted, Is.True);
        }
    }
}
