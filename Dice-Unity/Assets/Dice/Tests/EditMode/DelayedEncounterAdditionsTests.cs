using System;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class DelayedEncounterAdditionsTests
    {
        private const string Owned = "owned";
        private const string Other = "other";
        private const string Addition = "addition";

        [TestCase(2, false)]
        [TestCase(3, true)]
        public void GeneratedOffersIncludeAdditionsOnlyFromTheAuthoredClearedStage(int clearedStage, bool offered)
        {
            var catalog = Catalog(firstClearedStage: 3, ordinary: new[] { Owned });
            var encounter = catalog.GetEncounter(TestContent.EncounterId);
            var random = new DeterministicRandom(7);
            var generated = RewardGenerator.GenerateModifiers(catalog, encounter.RewardPolicy, clearedStage, TestModifiers.IdsOf(Owned),
                Array.Empty<ModifierFamilyDefinition.Id>(), ref random, encounter, Array.Empty<ModifierDefinition.Id>());
            Assert.That(generated.ModifierIds, Is.EqualTo(offered ? TestModifiers.IdsOf(Addition) : Array.Empty<ModifierDefinition.Id>()));
        }

        [Test]
        public void RestoredPendingOffersAgreeWithTheAdditionSchedule()
        {
            var delayed = Catalog(firstClearedStage: 3, ordinary: new[] { Owned, Other });
            var immediate = Catalog(firstClearedStage: 1, ordinary: new[] { Owned, Other });
            foreach (var catalog in new[] { delayed, immediate })
            {
                var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 11);
                Execute(session, PlayHandAction.Instance);
                string offered = TestModifiers.NameOf(session.Snapshot.PendingReward.Options.Single().DefinitionId.Value);
                if (catalog == delayed) Assert.That(offered, Is.EqualTo(Other));
                string substitute = offered == Other ? Addition : Other;
                var save = session.CaptureSave();
                save.PendingReward.Options[0].DefinitionId = TestModifiers.IdOf(substitute);
                save.ExcludedRewardFamilies = new[] { TestFamilies.IdOf("family." + substitute) };
                var result = EncounterSession.RestoreEncounter(catalog, save);
                if (catalog == immediate) Assert.That(result.Success, Is.True, result.Detail);
                else
                {
                    Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState));
                    Assert.That(result.Detail, Does.Contain("eligibility"));
                }
            }

            var later = EncounterSession.CreateEncounter(delayed, TestContent.EncounterId, 11);
            for (int cleared = 1; cleared <= 3; ++cleared)
            {
                Execute(later, PlayHandAction.Instance);
                if (cleared == 3) break;
                if (later.Snapshot.Phase == EncounterPhase.ChoosingModifier)
                {
                    var offer = later.Snapshot.PendingReward;
                    Execute(later, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
                }
                var upgrade = later.Snapshot.PendingReward;
                Execute(later, new SelectHandUpgradeAction(upgrade.OfferId, upgrade.Options[0].OptionId));
            }
            Assert.That(later.Snapshot.PendingReward.Options.Single().DefinitionId, Is.EqualTo(TestModifiers.IdOf(Addition)));
            var restored = Restore(delayed, later.CaptureSave());
            EncounterPersistenceTests.AssertGraphEqual(later.CaptureSave(), restored.CaptureSave());
        }

        [Test]
        public void TheFirstClearedStageDefaultsToOneAndMustLieWithinTheEncounter()
        {
            var stages = new[] { new StageDefinition(1, 1, 0), new StageDefinition(1, 1, 0) };
            Assert.That(new EncounterDefinition(TestEncounters.IdOf("encounter.default"), stages).EncounterModifiersFirstClearedStageNumber, Is.EqualTo(1));
            Assert.That(new EncounterDefinition(TestEncounters.IdOf("encounter.last"), stages, encounterModifiersFirstClearedStageNumber: 2)
                .EncounterModifiersFirstClearedStageNumber, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EncounterDefinition(TestEncounters.IdOf("encounter.zero"), stages, encounterModifiersFirstClearedStageNumber: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EncounterDefinition(TestEncounters.IdOf("encounter.beyond"), stages, encounterModifiersFirstClearedStageNumber: 3));
        }

        /** Checks an encounter states its own charms are available from the start only when it has some and its first offer can contain them / fails if delayed additions or an encounter without additions claim it. */
        [TestCase(true, 1, true)]
        [TestCase(true, 2, false)]
        [TestCase(false, 1, false)]
        public void EncounterStatesWhetherItsOwnCharmsAreAvailableFromTheStart(bool additions, int firstClearedStage, bool expected)
        {
            var stages = new[] { new StageDefinition(1, 1, 0), new StageDefinition(1, 1, 0) };
            var encounter = new EncounterDefinition(TestEncounters.IdOf("encounter.from_start"), stages,
                encounterModifierIds: additions ? new[] { TestModifiers.IdOf(Addition) } : Array.Empty<ModifierDefinition.Id>(),
                encounterModifiersFirstClearedStageNumber: firstClearedStage);
            Assert.That(encounter.EncounterModifiersAvailableFromStart, Is.EqualTo(expected));
        }

        /** Checks only the charm offers made on the authored first clear state that they introduce the encounter's own charms, a restored one included, and hand-upgrade offers never do / fails if an earlier or later offer, or an upgrade offer, claims it or a restore drops it. */
        [Test]
        public void OnlyOffersOnTheFirstClearThatCanContainAdditionsIntroduceThem()
        {
            var catalog = Catalog(firstClearedStage: 3, ordinary: new[] { Owned, Other });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 11);
            for (int cleared = 1; cleared <= 3; ++cleared)
            {
                Execute(session, PlayHandAction.Instance);
                if (session.Snapshot.Phase == EncounterPhase.ChoosingModifier)
                {
                    var offer = session.Snapshot.PendingReward;
                    Assert.That(offer.IntroducesEncounterModifiers, Is.EqualTo(cleared == 3), "Clearing stage " + cleared);
                    if (cleared == 3) break;
                    Execute(session, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
                }
                var upgrade = session.Snapshot.PendingReward;
                Assert.That(upgrade.Kind, Is.EqualTo(RewardKind.HandUpgrade));
                Assert.That(upgrade.IntroducesEncounterModifiers, Is.False, "Upgrade after clearing stage " + cleared);
                Execute(session, new SelectHandUpgradeAction(upgrade.OfferId, upgrade.Options[0].OptionId));
            }
            Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
            Assert.That(Restore(catalog, session.CaptureSave()).Snapshot.PendingReward.IntroducesEncounterModifiers, Is.True);
        }

        private static MechanicalCatalog Catalog(int firstClearedStage, string[] ordinary)
        {
            var modifiers = ordinary.Select(id => Modifier(id)).Concat(new[]
            {
                Modifier(Addition, ModifierAccess.EncounterExclusive)
            }).ToArray();
            var policy = new RewardPolicyDefinition(
                new[] { new ModifierRewardLayout(new[] { new ModifierRewardPoolDefinition("pool", TestModifiers.IdsOf(ordinary)) }) },
                0, 0, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var encounter = new EncounterDefinition(TestContent.EncounterId, Enumerable.Range(0, 5)
                    .Select(index => new StageDefinition(1, 2, 1, index < 4 ? StageExitBehavior.AwaitRewards : StageExitBehavior.AdvanceAutomatically)),
                startingModifiers: TestModifiers.IdsOf(Owned), rewardPolicy: policy, encounterModifierIds: TestModifiers.IdsOf(Addition),
                encounterModifiersFirstClearedStageNumber: firstClearedStage);
            return new MechanicalCatalog("test.delayed_additions", TestContent.Hands(), new[] { encounter }, modifiers);
        }

        private static ModifierDefinition Modifier(string id, ModifierAccess access = ModifierAccess.Starter)
            => new ModifierDefinition(TestModifiers.IdOf(id), TestFamilies.Of("family." + id), ModifierPhase.PlayHand,
                new NeverCondition(), Array.Empty<ModifierEffect>(),
                availability: new ModifierAvailabilityDefinition(access: access));

        private static Resolution Execute(EncounterSession session, EncounterAction action)
        {
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            return result;
        }

        private static EncounterSession Restore(MechanicalCatalog catalog, EncounterSaveData save)
        {
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.True, result.Error + ": " + result.Detail);
            return result.Session;
        }
    }
}
