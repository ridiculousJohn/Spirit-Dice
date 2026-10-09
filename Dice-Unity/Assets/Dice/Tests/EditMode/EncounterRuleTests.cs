using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class EncounterRuleTests
    {
        [TestCase(1)]
        [TestCase(10)]
        [TestCase(31)]
        public void FirstAcceptedRerollWaivesCostAndSubsequentActionsSpendPaidResources(int mask)
        {
            var session = Session(Catalog());
            var initial = session.Snapshot;
            Assert.That(initial.EncounterRules.Single().Id, Is.EqualTo(FirstRerollFreeRuleDefinition.StableId));
            Assert.That(initial.EncounterRules.Single().IsAvailable, Is.True);
            var free = Execute(session, new RerollDiceAction(mask));
            Assert.That(free.Snapshot.RerollsRemaining, Is.EqualTo(2));
            Assert.That(free.Snapshot.RerollsSpentOnHand, Is.EqualTo(1));
            Assert.That(free.Snapshot.TotalRerollsSpent, Is.EqualTo(1));
            Assert.That(free.Snapshot.EncounterRules.Single().IsAvailable, Is.False);
            Assert.That(initial.EncounterRules.Single().IsAvailable, Is.True);
            Assert.That(free.Events.Select(value => value.GetType()), Is.EqualTo(new[]
                { typeof(EncounterRuleActivatedEvent), typeof(DiceRolledEvent) }));
            Assert.That(((EncounterRuleActivatedEvent)free.Events[0]).RuleId, Is.EqualTo(FirstRerollFreeRuleDefinition.StableId));
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((mask & (1 << i)) == 0) Assert.That(free.Snapshot.CurrentDice[i], Is.EqualTo(initial.CurrentDice[i]));
            var paid = Execute(session, new RerollDiceAction(mask));
            Assert.That(paid.Snapshot.RerollsRemaining, Is.EqualTo(1));
            Assert.That(paid.Snapshot.RerollsSpentOnHand, Is.EqualTo(2));
            Assert.That(paid.Events.Select(value => value.GetType()), Is.EqualTo(new[]
                { typeof(ResourceChangedEvent), typeof(DiceRolledEvent) }));
            Assert.That(session.CaptureSave().RerollsSpentInStage, Is.EqualTo(2));
            Assert.That(session.CaptureSave().FreeRerollsUsedInStage, Is.EqualTo(1));
            Assert.That(session.CaptureSave().TotalFreeRerollsUsed, Is.EqualTo(1));
        }

        [Test]
        public void InvalidAndStaleRequestsLeaveTheFreeActionAvailableAtZeroPaidResources()
        {
            var session = Session(Catalog(paidRerolls: 0));
            var before = session.CaptureSave();
            Assert.That(session.Snapshot.CanReroll, Is.True);
            Assert.That(session.Execute(new RerollDiceAction(1), 1).Rejection, Is.EqualTo(CommandRejection.StaleRevision));
            foreach (int mask in new[] { 0, -1, 32 })
                Assert.That(session.Execute(new RerollDiceAction(mask), 0).Rejection, Is.EqualTo(CommandRejection.InvalidDiceSelection));
            Assert.That(session.Execute(null, 0).Rejection, Is.EqualTo(CommandRejection.InvalidAction));
            EncounterPersistenceTests.AssertGraphEqual(before, session.CaptureSave());
            Execute(session, new RerollDiceAction(1));
            Assert.That(session.Snapshot.RerollsRemaining, Is.Zero);
            Assert.That(session.Snapshot.CanReroll, Is.False);
            var consumed = session.CaptureSave();
            var rejected = session.Execute(new RerollDiceAction(1), session.Snapshot.Revision);
            Assert.That(rejected.Rejection, Is.EqualTo(CommandRejection.NoRerollsRemaining));
            Assert.That(rejected.Events, Is.Empty);
            EncounterPersistenceTests.AssertGraphEqual(consumed, session.CaptureSave());
        }

        [Test]
        public void AnUnchangedFaceStillConsumesTheFreeActionAndCountsForModifiers()
        {
            ulong seed = 0;
            for (; seed < 1024; ++seed)
            {
                var random = new DeterministicRandom(seed);
                int first = random.NextInt(6);
                for (int i = 1; i < DiceValues.Count; ++i) random.NextInt(6);
                if (random.NextInt(6) == first) break;
            }
            Assert.That(seed, Is.LessThan(1024));
            var modifiers = new[]
            {
                Modifier("reroll", ModifierPhase.PlayerReroll, ModifierConditionKind.RerollsSpentAtLeast,
                    ModifierEffectKind.AddBaseScore, 7, 1),
                Modifier("untouched", ModifierPhase.PlayHand, ModifierConditionKind.NoDiceRerolled,
                    ModifierEffectKind.AddBaseScore, 100)
            };
            var session = Session(Catalog(paidRerolls: 0, modifiers: modifiers), seed);
            var before = session.Snapshot.CurrentDice;
            var free = Execute(session, new RerollDiceAction(1));
            Assert.That(free.Snapshot.CurrentDice, Is.EqualTo(before));
            Assert.That(free.Snapshot.LatestChangedMask, Is.Zero);
            Assert.That(free.Snapshot.EverRerolledMask, Is.EqualTo(1));
            Assert.That(free.Snapshot.Modifiers[0].TotalActivations, Is.EqualTo(1));
            Assert.That(free.Snapshot.ModifierBaseScore, Is.EqualTo(7));
            Assert.That(session.PreviewHandResolution().Events.OfType<ModifierActivatedEvent>()
                .Any(value => value.DefinitionId == "untouched"), Is.False);
            Assert.That(session.Snapshot.CanReroll, Is.False);
            Restore(Catalog(paidRerolls: 0, modifiers: modifiers), session.CaptureSave());
        }

        [Test]
        public void FreeRerollRunsResourceGrantModifiersAndTheNextActionSpendsTheirGrant()
        {
            var grant = Modifier("grant", ModifierPhase.PlayerReroll, ModifierConditionKind.RerollsSpentAtLeast,
                ModifierEffectKind.AddRerolls, 1, 1, ModifierActivationLimit.OncePerHand);
            var catalog = Catalog(paidRerolls: 0, modifiers: new[] { grant });
            var session = Session(catalog);
            var free = Execute(session, new RerollDiceAction(1));
            Assert.That(free.Snapshot.RerollsRemaining, Is.EqualTo(1));
            Assert.That(free.Snapshot.CanReroll, Is.True);
            Assert.That(free.Events[0], Is.TypeOf<EncounterRuleActivatedEvent>());
            Assert.That(free.Events[1], Is.TypeOf<DiceRolledEvent>());
            var resource = free.Events.OfType<ResourceChangedEvent>().Single();
            Assert.That(resource.Cause, Is.EqualTo(ResourceChangeCause.ModifierEffect));
            Assert.That(resource.Before, Is.Zero);
            Assert.That(resource.After, Is.EqualTo(1));
            Restore(catalog, session.CaptureSave());
            Execute(session, new RerollDiceAction(1));
            Assert.That(session.Snapshot.RerollsRemaining, Is.Zero);
            Assert.That(session.Snapshot.CanReroll, Is.False);
            Assert.That(session.Snapshot.TotalRerollsSpent, Is.EqualTo(2));
            Restore(catalog, session.CaptureSave());
        }

        [Test]
        public void AvailabilityResetsForEachHandAndIsDisabledWhenTheEncounterEnds()
        {
            var catalog = Catalog(paidRerolls: 0, hands: 2);
            var session = Session(catalog);
            Execute(session, new RerollDiceAction(1));
            Execute(session, PlayHandAction.Instance);
            Assert.That(session.Snapshot.HandIndex, Is.EqualTo(1));
            Assert.That(session.Snapshot.RerollsSpentOnHand, Is.Zero);
            Assert.That(session.Snapshot.CanReroll, Is.True);
            Assert.That(session.Snapshot.EncounterRules.Single().IsAvailable, Is.True);
            Execute(session, new RerollDiceAction(1));
            Execute(session, PlayHandAction.Instance);
            Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.Completed));
            Assert.That(session.Snapshot.CanReroll, Is.False);
            Assert.That(session.Snapshot.EncounterRules.Single().IsAvailable, Is.False);
            Assert.That(session.CaptureSave().FreeRerollsUsedInStage, Is.EqualTo(2));
            Restore(catalog, session.CaptureSave());
        }

        [Test]
        public void StageTransitionsResetAvailabilityAndStageAccountingWhileRetainingTotals()
        {
            var catalog = Catalog(stages: new[] { new StageDefinition(1, 1, 0), new StageDefinition(1000000, 2, 0) });
            var session = Session(catalog);
            Execute(session, new RerollDiceAction(1));
            Execute(session, PlayHandAction.Instance);
            Assert.That(session.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(session.Snapshot.CanReroll, Is.True);
            var save = session.CaptureSave();
            Assert.That(save.RerollsSpentInStage, Is.Zero);
            Assert.That(save.FreeRerollsUsedInStage, Is.Zero);
            Assert.That(save.TotalRerollsSpent, Is.EqualTo(1));
            Assert.That(save.TotalFreeRerollsUsed, Is.EqualTo(1));
            Restore(catalog, save);
            Execute(session, new RerollDiceAction(31));
            Restore(catalog, session.CaptureSave());
        }

        [Test]
        public void RewardAndAbandonedPhasesDoNotAdvertiseRerollAvailability()
        {
            var catalog = Catalog(stages: new[] { new StageDefinition(1, 1, 0, StageExitBehavior.AwaitRewards),
                new StageDefinition(1000000, 1, 0) });
            var session = Session(catalog);
            Execute(session, PlayHandAction.Instance);
            Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.AwaitingRewards));
            Assert.That(session.Snapshot.CanReroll, Is.False);
            Assert.That(session.Snapshot.EncounterRules.Single().IsAvailable, Is.False);
            Restore(catalog, session.CaptureSave());
            Execute(session, AbandonEncounterAction.Instance);
            Assert.That(session.Snapshot.CanReroll, Is.False);
            Restore(catalog, session.CaptureSave());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RestoringAtHandBoundariesPreservesFreeUsageAndDeterministicContinuation(int prefix)
        {
            var catalog = Catalog();
            var uninterrupted = Session(catalog);
            EncounterAction[] actions = { new RerollDiceAction(1), new RerollDiceAction(7), PlayHandAction.Instance };
            for (int i = 0; i < prefix; ++i) Execute(uninterrupted, actions[i]);
            var resumed = Restore(catalog, uninterrupted.CaptureSave());
            EncounterPersistenceTests.AssertGraphEqual(uninterrupted.CaptureSave(), resumed.CaptureSave());
            foreach (var action in new EncounterAction[] { new RerollDiceAction(31), PlayHandAction.Instance })
            {
                var expected = Execute(uninterrupted, action);
                var actual = Execute(resumed, action);
                Assert.That(actual.Snapshot.CurrentDice, Is.EqualTo(expected.Snapshot.CurrentDice));
                Assert.That(actual.Snapshot.CanReroll, Is.EqualTo(expected.Snapshot.CanReroll));
                Assert.That(actual.Events.Select(value => value.GetType()), Is.EqualTo(expected.Events.Select(value => value.GetType())));
                EncounterPersistenceTests.AssertGraphEqual(uninterrupted.CaptureSave(), resumed.CaptureSave());
            }
        }

        [TestCaseSource(nameof(InvalidAccounting))]
        public void RestoreRejectsImpossibleFreeAndPaidAccounting(Action<EncounterSaveData> mutate)
        {
            var catalog = Catalog();
            var session = Session(catalog);
            Execute(session, new RerollDiceAction(1));
            var save = session.CaptureSave();
            mutate(save);
            var restored = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(restored.Error, Is.EqualTo(EncounterRestoreError.InvalidState), restored.Detail);
        }

        [Test]
        public void RulesAreCopiedAndRejectNullOrDuplicateDefinitions()
        {
            var rules = new List<EncounterRuleDefinition> { new FirstRerollFreeRuleDefinition() };
            var stages = new[] { new StageDefinition(1000, 1, 0) };
            var definition = new EncounterDefinition(TestContent.EncounterId, stages, rules: rules);
            rules.Clear();
            Assert.That(definition.Rules, Has.Count.EqualTo(1));
            Assert.Throws<ArgumentException>(() => new EncounterDefinition(TestContent.EncounterId, stages,
                rules: new EncounterRuleDefinition[] { null }));
            Assert.Throws<ArgumentException>(() => new EncounterDefinition(TestContent.EncounterId, stages,
                rules: new[] { new FirstRerollFreeRuleDefinition(), new FirstRerollFreeRuleDefinition() }));
        }

        [Test]
        public void RestoreRejectsFreeHistoryOnAHandWithNoAcceptedReroll()
        {
            var catalog = Catalog();
            var save = Session(catalog).CaptureSave();
            save.Revision = 1;
            save.RerollsSpentInStage = 1;
            save.TotalRerollsSpent = 1;
            save.FreeRerollsUsedInStage = 1;
            save.TotalFreeRerollsUsed = 1;
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.InvalidState));
        }

        [Test]
        public void LegacyRulelessSavesDeriveStageSpendingAndUpgradeWhenCaptured()
        {
            var catalog = TestContent.Catalog();
            var session = Session(catalog);
            Execute(session, new RerollDiceAction(1));
            Execute(session, PlayHandAction.Instance);
            var legacy = session.CaptureSave();
            legacy.SaveFormatVersion = 1;
            legacy.RulesVersion = 3;
            legacy.RerollsSpentInStage = 0;
            var resumed = Restore(catalog, legacy);
            EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), resumed.CaptureSave());
            Execute(session, new RerollDiceAction(7));
            Execute(resumed, new RerollDiceAction(7));
            EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), resumed.CaptureSave());
        }

        [Test]
        public void VersionTwoRuleSavesPreserveFreeUsageWhenUpgradedAndContinued()
        {
            var catalog = Catalog();
            var session = Session(catalog);
            Execute(session, new RerollDiceAction(1));
            var legacy = session.CaptureSave();
            legacy.SaveFormatVersion = 2;
            legacy.RulesVersion = 4;
            legacy.UnlockedModifierIds = null;
            legacy.NewlyUnlockedModifierIds = null;
            var resumed = Restore(catalog, legacy);
            var upgraded = resumed.CaptureSave();
            Assert.That(upgraded.SaveFormatVersion, Is.EqualTo(EncounterSaveData.CurrentFormatVersion));
            Assert.That(upgraded.RulesVersion, Is.EqualTo(EncounterSession.RulesVersion));
            Assert.That(resumed.Snapshot.EncounterRules.Single().IsAvailable, Is.False);
            EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), upgraded);
            foreach (var action in new EncounterAction[] { new RerollDiceAction(7), PlayHandAction.Instance,
                new RerollDiceAction(31) })
            {
                Execute(session, action);
                Execute(resumed, action);
                EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), resumed.CaptureSave());
            }
        }

        [Test]
        public void LegacySavesCannotOptIntoEncounterRulesAndVersionPairsCannotBeMixed()
        {
            var catalog = Catalog();
            var save = Session(catalog).CaptureSave();
            save.SaveFormatVersion = 1;
            save.RulesVersion = 3;
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.RulesVersionMismatch));
            save.RulesVersion = EncounterSession.RulesVersion;
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.RulesVersionMismatch));
            save.SaveFormatVersion = EncounterSaveData.CurrentFormatVersion;
            save.RulesVersion = 3;
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.RulesVersionMismatch));
            save.SaveFormatVersion = 2;
            save.RulesVersion = EncounterSession.RulesVersion;
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.RulesVersionMismatch));
            save.SaveFormatVersion = EncounterSaveData.CurrentFormatVersion;
            save.RulesVersion = 4;
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.RulesVersionMismatch));
        }

        [Test]
        public void EncountersWithoutRulesKeepPaidCostsAndRejectInventedFreeHistory()
        {
            var catalog = TestContent.Catalog();
            var session = Session(catalog);
            Assert.That(session.Snapshot.EncounterRules, Is.Empty);
            var reroll = Execute(session, new RerollDiceAction(1));
            Assert.That(reroll.Snapshot.RerollsRemaining, Is.EqualTo(19));
            Assert.That(reroll.Events.OfType<EncounterRuleActivatedEvent>(), Is.Empty);
            var save = session.CaptureSave();
            save.TotalFreeRerollsUsed = 1;
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.InvalidState));
        }

        private static IEnumerable<TestCaseData> InvalidAccounting()
        {
            yield return Invalid("negative stage actions", save => save.RerollsSpentInStage = -1);
            yield return Invalid("negative stage free", save => save.FreeRerollsUsedInStage = -1);
            yield return Invalid("negative total free", save => save.TotalFreeRerollsUsed = -1);
            yield return Invalid("missing stage free", save => save.FreeRerollsUsedInStage = 0);
            yield return Invalid("missing total free", save => save.TotalFreeRerollsUsed = 0);
            yield return Invalid("inflated stage free", save => save.FreeRerollsUsedInStage = 2);
            yield return Invalid("inflated total free", save => save.TotalFreeRerollsUsed = 2);
            yield return Invalid("missing stage action", save => save.RerollsSpentInStage = 0);
            yield return Invalid("invented paid spending", save => --save.RerollsRemaining);
            yield return Invalid("invented total action", save => { ++save.TotalRerollsSpent; ++save.Revision; });
        }

        private static TestCaseData Invalid(string name, Action<EncounterSaveData> mutate)
            => new TestCaseData(mutate).SetName("EncounterRuleAccounting_" + name);

        private static ModifierDefinition Modifier(string id, ModifierPhase phase, ModifierConditionKind condition,
            ModifierEffectKind effect, long amount, int parameter = 0,
            ModifierActivationLimit limit = ModifierActivationLimit.Unlimited)
            => new ModifierDefinition(id, "family." + id, phase, new ModifierCondition(condition, parameter),
                new[] { new ModifierEffect(effect, amount) }, limit);

        private static MechanicalCatalog Catalog(int paidRerolls = 2, int hands = 5,
            IEnumerable<StageDefinition> stages = null, ModifierDefinition[] modifiers = null)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                stages ?? new[] { new StageDefinition(1000000, hands, paidRerolls) },
                startingModifiers: modifiers?.Select(value => value.Id),
                rules: new[] { new FirstRerollFreeRuleDefinition() });
            return new MechanicalCatalog("test.encounter_rules", TestContent.Hands(), new[] { encounter }, modifiers);
        }

        private static EncounterSession Session(MechanicalCatalog catalog, ulong seed = 14)
            => EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, seed);

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
