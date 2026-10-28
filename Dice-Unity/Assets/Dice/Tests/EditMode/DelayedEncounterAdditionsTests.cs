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
            var generated = RewardGenerator.GenerateModifiers(catalog, encounter.RewardPolicy, clearedStage, new[] { Owned },
                Array.Empty<string>(), ref random, encounter, Array.Empty<string>(), clearedStage + 1);
            Assert.That(generated.ModifierIds, Is.EqualTo(offered ? new[] { Addition } : Array.Empty<string>()));
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
                string offered = session.Snapshot.PendingReward.Options.Single().DefinitionId;
                if (catalog == delayed) Assert.That(offered, Is.EqualTo(Other));
                string substitute = offered == Other ? Addition : Other;
                var save = session.CaptureSave();
                save.PendingReward.Options[0].DefinitionId = substitute;
                save.ExcludedRewardFamilies = new[] { "family." + substitute };
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
            Assert.That(later.Snapshot.PendingReward.Options.Single().DefinitionId, Is.EqualTo(Addition));
            var restored = Restore(delayed, later.CaptureSave());
            EncounterPersistenceTests.AssertGraphEqual(later.CaptureSave(), restored.CaptureSave());
        }

        [Test]
        public void TheFirstClearedStageDefaultsToOneAndMustLieWithinTheEncounter()
        {
            var stages = new[] { new StageDefinition(1, 1, 0), new StageDefinition(1, 1, 0) };
            Assert.That(new EncounterDefinition("encounter.default", stages).EncounterModifiersFirstClearedStageNumber, Is.EqualTo(1));
            Assert.That(new EncounterDefinition("encounter.last", stages, encounterModifiersFirstClearedStageNumber: 2)
                .EncounterModifiersFirstClearedStageNumber, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EncounterDefinition("encounter.zero", stages, encounterModifiersFirstClearedStageNumber: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EncounterDefinition("encounter.beyond", stages, encounterModifiersFirstClearedStageNumber: 3));
        }

        private static MechanicalCatalog Catalog(int firstClearedStage, string[] ordinary)
        {
            var modifiers = ordinary.Select(id => Modifier(id)).Concat(new[]
            {
                Modifier(Addition, ModifierAvailabilityScope.Encounter, TestContent.EncounterId, ModifierAccess.EncounterExclusive)
            }).ToArray();
            var policy = new RewardPolicyDefinition("policy", new[] { new ModifierRewardLayout(new[] { "pool" }) },
                0, 0, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var encounter = new EncounterDefinition(TestContent.EncounterId, Enumerable.Range(0, 5)
                    .Select(index => new StageDefinition(1, 2, 1, index < 4 ? StageExitBehavior.AwaitRewards : StageExitBehavior.AdvanceAutomatically)),
                startingModifiers: new[] { Owned }, rewardPolicy: policy, encounterModifierIds: new[] { Addition },
                encounterModifiersFirstClearedStageNumber: firstClearedStage);
            return new MechanicalCatalog("test.delayed_additions", TestContent.Hands(), new[] { encounter }, modifiers,
                new[] { new ModifierRewardPoolDefinition("pool", ordinary) });
        }

        private static ModifierDefinition Modifier(string id, ModifierAvailabilityScope scope = ModifierAvailabilityScope.Global,
            string scopeId = null, ModifierAccess access = ModifierAccess.Starter)
            => new ModifierDefinition(id, "family." + id, ModifierPhase.PlayHand,
                new NeverCondition(), Array.Empty<ModifierEffect>(),
                availability: new ModifierAvailabilityDefinition(1, scope, scopeId, access));

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
