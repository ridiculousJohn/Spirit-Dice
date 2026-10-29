using System;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class EncounterCharmsIntroductionTests
    {
        private const string Addition = "addition";
        private const string Unlock = "unlock";
        private static readonly string[] Ordinary = { "owned", "first", "second", "third", "fourth", "fifth", "sixth" };

        [Test]
        public void OnlyTheFirstCharmOfferThatCanContainDelayedAdditionsIsIntroduced()
        {
            var catalog = Catalog(firstClearedStage: 3, additions: true);
            var encounter = catalog.GetEncounter(TestContent.EncounterId);
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 11);
            Assert.That(SpiritEncounterCharmsIntroduction.IntroducesOffer(encounter, session.Snapshot), Is.False, "Playing");
            int introduced = 0;
            for (int cleared = 1; cleared <= 4; ++cleared)
            {
                Execute(session, PlayHandAction.Instance);
                if (cleared == 3)
                {
                    Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
                    Assert.That(SpiritEncounterCharmsIntroduction.IntroducesOffer(encounter, session.Snapshot), Is.False,
                        "The permanent unlock choice comes first.");
                    ContinueAfterUnlock(session);
                }
                Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier), "Clearing stage " + cleared);
                var offer = session.Snapshot.PendingReward;
                bool expected = cleared == 3;
                Assert.That(SpiritEncounterCharmsIntroduction.IntroducesOffer(encounter, session.Snapshot), Is.EqualTo(expected),
                    "Clearing stage " + cleared);
                if (expected)
                {
                    ++introduced;
                    Execute(session, new RefreshModifierOfferAction(offer.OfferId));
                    Assert.That(session.Snapshot.PendingReward.OfferId, Is.Not.EqualTo(offer.OfferId));
                    Assert.That(SpiritEncounterCharmsIntroduction.IntroducesOffer(encounter, session.Snapshot), Is.True,
                        "A refreshed offer for the same stage is still the introduced offer.");
                    offer = session.Snapshot.PendingReward;
                }
                Execute(session, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
                Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingHandUpgrade));
                Assert.That(SpiritEncounterCharmsIntroduction.IntroducesOffer(encounter, session.Snapshot), Is.False);
                var upgrade = session.Snapshot.PendingReward;
                Execute(session, new SelectHandUpgradeAction(upgrade.OfferId, upgrade.Options[0].OptionId));
            }
            Assert.That(introduced, Is.EqualTo(1));
        }

        [Test]
        public void AdditionsFromTheFirstClearAndEncountersWithoutAdditionsAreNeverIntroducedInside()
        {
            foreach (var catalog in new[] { Catalog(firstClearedStage: 1, additions: true), Catalog(firstClearedStage: 3, additions: false) })
            {
                var encounter = catalog.GetEncounter(TestContent.EncounterId);
                var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 11);
                for (int cleared = 1; cleared <= 4; ++cleared)
                {
                    Execute(session, PlayHandAction.Instance);
                    if (session.Snapshot.Phase == EncounterPhase.ChoosingUnlock)
                        ContinueAfterUnlock(session);
                    Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
                    Assert.That(SpiritEncounterCharmsIntroduction.IntroducesOffer(encounter, session.Snapshot), Is.False,
                        catalog.Version + " clearing stage " + cleared);
                    var offer = session.Snapshot.PendingReward;
                    Execute(session, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
                    var upgrade = session.Snapshot.PendingReward;
                    Execute(session, new SelectHandUpgradeAction(upgrade.OfferId, upgrade.Options[0].OptionId));
                }
            }
            Assert.That(SpiritEncounterCharmsIntroduction.IntroducesOffer(null, null), Is.False);
        }

        internal static MechanicalCatalog Catalog(int firstClearedStage, bool additions)
        {
            var modifiers = Ordinary.Select(id => Modifier(id)).Concat(new[]
            {
                Modifier(Addition, ModifierAccess.EncounterExclusive),
                Modifier(Unlock, access: ModifierAccess.PermanentUnlock)
            }).ToArray();
            var policy = new RewardPolicyDefinition(
                new[] { new ModifierRewardLayout(new[] { new ModifierRewardPoolDefinition("pool", TestModifiers.IdsOf(Ordinary)) }) },
                0, 1, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var encounter = new EncounterDefinition(TestContent.EncounterId, Enumerable.Range(0, 6)
                    .Select(index => new StageDefinition(1, 2, 1, index < 5 ? StageExitBehavior.AwaitRewards : StageExitBehavior.AdvanceAutomatically)),
                startingModifiers: TestModifiers.IdsOf(Ordinary[0]), rewardPolicy: policy,
                unlockMilestones: new[] { new ModifierUnlockMilestone(3) },
                encounterModifierIds: additions ? new[] { TestModifiers.IdOf(Addition) } : Array.Empty<ModifierDefinition.Id>(),
                unlockRewardModifierIds: TestModifiers.IdsOf(Unlock), encounterModifiersFirstClearedStageNumber: firstClearedStage);
            return new MechanicalCatalog("test.charms_introduction." + firstClearedStage + (additions ? ".additions" : ".none"),
                TestContent.Hands(), new[] { encounter }, modifiers);
        }

        private static ModifierDefinition Modifier(string id, ModifierAccess access = ModifierAccess.Starter)
            => new ModifierDefinition(TestModifiers.IdOf(id), TestFamilies.Of("family." + id), ModifierPhase.PlayHand,
                new NeverCondition(), Array.Empty<ModifierEffect>(),
                availability: new ModifierAvailabilityDefinition(access: access));

        internal static void ContinueAfterUnlock(EncounterSession session)
        {
            var offer = session.Snapshot.PendingReward;
            Execute(session, offer.Options.Count == 0 ? (EncounterAction)new AcknowledgeModifierUnlockAction(offer.OfferId)
                : new SelectModifierUnlockAction(offer.OfferId, offer.Options[0].OptionId));
        }

        internal static void Execute(EncounterSession session, EncounterAction action)
        {
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
        }
    }
}
