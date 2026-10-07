using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.App;
using Dice.Engine;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;

namespace Dice.Tests
{
    public sealed class EncounterPersistenceTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void InitialAndRerollSavesResumeContributionsLimitsHistoryAndBothRandomStreams(int prefixLength)
        {
            var catalog = ModifierCatalog();
            var uninterrupted = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
            EncounterAction[] prefix = { new RerollDiceAction(3), new RerollDiceAction(5), PlayHandAction.Instance };
            for (int i = 0; i < prefixLength; ++i)
                uninterrupted.Execute(prefix[i], uninterrupted.Snapshot.Revision);
            var save = uninterrupted.CaptureSave();
            var resumed = Restore(catalog, save);
            AssertGraphEqual(save, resumed.CaptureSave());
            Assert.That(resumed.InitialResolution.Events, Is.Empty);
            Assert.That(resumed.InitialResolution.PreviousRevision, Is.EqualTo(save.Revision));
            foreach (var action in new EncounterAction[] { new RerollDiceAction(31), PlayHandAction.Instance, new RerollDiceAction(7) })
                AssertContinuation(uninterrupted, resumed, action);
        }

        [Test]
        public void CaptureReturnsIndependentNestedDataThatCannotMutateTheLiveSession()
        {
            var catalog = ModifierCatalog();
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
            session.Execute(new RerollDiceAction(3), 0);
            var before = session.Snapshot;
            var expected = session.CaptureSave();
            var mutated = session.CaptureSave();
            Assert.That(mutated.CurrentDice, Is.Not.SameAs(expected.CurrentDice));
            Assert.That(mutated.InitialDice, Is.Not.SameAs(expected.InitialDice));
            Assert.That(mutated.Modifiers[0], Is.Not.SameAs(expected.Modifiers[0]));
            Assert.That(mutated.ModifierContributions[0], Is.Not.SameAs(expected.ModifierContributions[0]));
            mutated.CurrentDice[0] = mutated.CurrentDice[0] % 6 + 1;
            mutated.InitialDice[0] = mutated.InitialDice[0] % 6 + 1;
            mutated.DiceBeforeLastReroll[0] = mutated.DiceBeforeLastReroll[0] % 6 + 1;
            mutated.HandUpgrades[0] = 5;
            mutated.Modifiers[0].DefinitionId = "mutation";
            mutated.Modifiers[0].TotalActivations = 99;
            mutated.ModifierContributions[0].BaseScore = 99;
            mutated.ModifierBaseScore = 99;
            mutated.DiceRandomState = 0;
            mutated.RewardRandomState = 0;
            EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
            AssertGraphEqual(expected, session.CaptureSave());
            var frozen = session.CaptureSave();
            session.Execute(PlayHandAction.Instance, 1);
            AssertGraphEqual(expected, frozen);
        }

        [Test]
        public void RestoreCopiesCallerDataAndPreservesDuplicateInstances()
        {
            var catalog = ModifierCatalog();
            var uninterrupted = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
            uninterrupted.Execute(new RerollDiceAction(3), 0);
            var input = uninterrupted.CaptureSave();
            var resumed = Restore(catalog, input);
            var expected = resumed.CaptureSave();
            input.CurrentDice[0] = input.CurrentDice[0] % 6 + 1;
            input.HandUpgrades[0] = 5;
            input.Modifiers[1].InstanceId = 99;
            input.Modifiers[1].ActivationsThisHand = 99;
            input.ModifierContributions[0].DefinitionId = "mutation";
            input.ModifierContributions = Array.Empty<EncounterSaveContributionData>();
            AssertGraphEqual(expected, resumed.CaptureSave());
            Assert.That(resumed.Snapshot.Modifiers.Select(value => value.InstanceId), Is.EqualTo(new long[] { 1, 2, 3, 4 }));
            Assert.That(resumed.Snapshot.Modifiers[1].DefinitionId, Is.EqualTo(resumed.Snapshot.Modifiers[2].DefinitionId));
            AssertContinuation(uninterrupted, resumed, new RerollDiceAction(7));
            AssertContinuation(uninterrupted, resumed, PlayHandAction.Instance);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void RewardPhaseSavesPreserveOffersFutureUpgradeChoicesAndNextIdentities(int boundary)
        {
            var catalog = RewardTestContent.Catalog();
            var uninterrupted = RewardTestContent.Session(catalog, 77);
            uninterrupted.Execute(PlayHandAction.Instance, 0);
            if (boundary >= 1)
                uninterrupted.Execute(new RefreshModifierOfferAction(uninterrupted.Snapshot.PendingReward.OfferId), 1);
            if (boundary >= 2)
            {
                var offer = uninterrupted.Snapshot.PendingReward;
                uninterrupted.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), uninterrupted.Snapshot.Revision);
            }
            var save = uninterrupted.CaptureSave();
            var resumed = Restore(catalog, save);
            AssertGraphEqual(save, resumed.CaptureSave());
            if (boundary < 2)
            {
                Assert.That(save.PendingUpgradeCategories.Length, Is.EqualTo(4));
                AssertContinuation(uninterrupted, resumed,
                    new RefreshModifierOfferAction(uninterrupted.Snapshot.PendingReward.OfferId));
                var offer = uninterrupted.Snapshot.PendingReward;
                AssertContinuation(uninterrupted, resumed, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
            }
            var upgrades = uninterrupted.Snapshot.PendingReward;
            AssertContinuation(uninterrupted, resumed, new SelectHandUpgradeAction(upgrades.OfferId, upgrades.Options[0].OptionId));
            AssertContinuation(uninterrupted, resumed, new RerollDiceAction(31));
            AssertContinuation(uninterrupted, resumed, PlayHandAction.Instance);
        }

        [Test]
        public void PendingOfferAndCachedUpgradeDataAreDetachedFromCaptureAndRestoreInputs()
        {
            var catalog = RewardTestContent.Catalog();
            var uninterrupted = RewardTestContent.Session(catalog, 77);
            uninterrupted.Execute(PlayHandAction.Instance, 0);
            var input = uninterrupted.CaptureSave();
            var resumed = Restore(catalog, input);
            var expected = resumed.CaptureSave();
            input.PendingReward.Options[0].DefinitionId = "mutation";
            input.PendingReward.Options[0].OptionId = 99;
            input.PendingReward.OfferId = 99;
            input.PendingUpgradeCategories[0] = (HandCategory)99;
            input.ExcludedRewardFamilies[0] = "mutation";
            input.PendingReward.Options = null;
            input.PendingUpgradeCategories = null;
            AssertGraphEqual(expected, resumed.CaptureSave());
            AssertGraphEqual(expected, uninterrupted.CaptureSave());
            var offer = uninterrupted.Snapshot.PendingReward;
            AssertContinuation(uninterrupted, resumed, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
            var upgrades = uninterrupted.Snapshot.PendingReward;
            AssertContinuation(uninterrupted, resumed, new SelectHandUpgradeAction(upgrades.OfferId, upgrades.Options[0].OptionId));
        }

        [TestCase(EncounterOutcome.Won)]
        [TestCase(EncounterOutcome.Lost)]
        [TestCase(EncounterOutcome.Abandoned)]
        public void CompletedSavesRestoreWithoutOfferingRewardsOrReplayingCompletion(EncounterOutcome outcome)
        {
            var catalog = TestContent.Catalog(new[] { new StageDefinition(outcome == EncounterOutcome.Won ? 1 : 1000000, 1, 0) });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
            session.Execute(outcome == EncounterOutcome.Abandoned ? (EncounterAction)AbandonEncounterAction.Instance : PlayHandAction.Instance, 0);
            var save = session.CaptureSave();
            var restored = Restore(catalog, save);
            Assert.That(restored.Snapshot.Outcome, Is.EqualTo(outcome));
            Assert.That(restored.InitialResolution.Events, Is.Empty);
            var result = restored.Execute(PlayHandAction.Instance, restored.Snapshot.Revision);
            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.EncounterCompleted));
            Assert.That(result.Events, Is.Empty);
            AssertGraphEqual(save, restored.CaptureSave());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AbandonmentAtEitherRewardPhaseRestoresWithOffersAndCachedChoicesCleared(bool afterModifierSelection)
        {
            var catalog = RewardTestContent.Catalog();
            var session = RewardTestContent.Session(catalog);
            session.Execute(PlayHandAction.Instance, 0);
            if (afterModifierSelection)
            {
                var offer = session.Snapshot.PendingReward;
                session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), 1);
            }
            session.Execute(AbandonEncounterAction.Instance, session.Snapshot.Revision);
            var save = session.CaptureSave();
            Assert.That(save.PendingReward, Is.Null);
            Assert.That(save.PendingUpgradeCategories, Is.Null);
            var restored = Restore(catalog, save);
            Assert.That(restored.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            AssertGraphEqual(save, restored.CaptureSave());
        }

        [Test]
        public void LegacyAwaitingRewardsWithNoPolicyRemainsRestorable()
        {
            var catalog = TestContent.Catalog(new[]
            {
                new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards), new StageDefinition(100, 3, 3)
            });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
            session.Execute(PlayHandAction.Instance, 0);
            Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.AwaitingRewards));
            var restored = Restore(catalog, session.CaptureSave());
            AssertContinuation(session, restored, AbandonEncounterAction.Instance);
        }

        [Test]
        public void DuplicateFallbackRewardsRestoreAsDistinctOwnedInstances()
        {
            var normal = RewardTestContent.Modifier("normal", "family.normal");
            var fallback = new ModifierDefinition("fallback", "family.fallback", ModifierPhase.PlayHand,
                new ModifierCondition(ModifierConditionKind.Never), Array.Empty<ModifierEffect>(), rewardEligible: false);
            var catalog = RewardTestContent.Catalog(new[] { normal, fallback },
                new[] { new ModifierRewardPoolDefinition("pool", new[] { normal.Id }, fallback.Id) },
                upgrades: Enumerable.Repeat(6, HandDefinition.CategoryCount).ToArray(), inventory: new[] { normal.Id });
            var session = RewardTestContent.Session(catalog);
            for (int i = 0; i < 2; ++i)
            {
                session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
                var offer = session.Snapshot.PendingReward;
                Assert.That(offer.Options, Is.Not.Empty);
                Assert.That(offer.Options.Select(value => value.DefinitionId), Is.All.EqualTo(fallback.Id));
                session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), session.Snapshot.Revision);
            }
            var restored = Restore(catalog, session.CaptureSave());
            Assert.That(restored.Snapshot.Modifiers.Select(value => value.InstanceId), Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(restored.Snapshot.Modifiers.Select(value => value.DefinitionId), Is.EqualTo(new[] { "normal", "fallback", "fallback" }));
            AssertContinuation(session, restored, new RerollDiceAction(1));
        }

        [Test]
        public void EarnedUpgradeHistoryCannotBeRemovedFromAnOtherwiseValidSave()
        {
            var catalog = RewardTestContent.Catalog();
            var session = RewardTestContent.Session(catalog);
            session.Execute(PlayHandAction.Instance, 0);
            var offer = session.Snapshot.PendingReward;
            session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), 1);
            offer = session.Snapshot.PendingReward;
            session.Execute(new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId), 2);
            var save = session.CaptureSave();
            Restore(catalog, save);
            Assert.That(save.RecentHandUpgrades.Length, Is.EqualTo(1));
            save.RecentHandUpgrades = Array.Empty<HandCategory>();
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState));
            Assert.That(result.Session, Is.Null);
        }

        [Test]
        public void AnActiveHandCannotContainConsistentPlayedHandEffectsFromAnEarlierHand()
        {
            var catalog = ModifierCatalog();
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
            session.Execute(PlayHandAction.Instance, 0);
            var save = session.CaptureSave();
            Restore(catalog, save);
            Assert.That(save.Phase, Is.EqualTo(EncounterPhase.Playing));
            Assert.That(save.HandIndex, Is.EqualTo(1));
            Assert.That(save.Modifiers[3].ActivationsThisHand, Is.Zero);
            Assert.That(save.Modifiers[3].ActivationsThisStage, Is.EqualTo(1));
            save.Modifiers[3].ActivationsThisHand = 1;
            save.ModifierContributions = save.ModifierContributions.Concat(new[]
            {
                new EncounterSaveContributionData
                {
                    InstanceId = 4, DefinitionId = "play", Phase = ModifierPhase.PlayHand, BaseScore = 11
                }
            }).ToArray();
            save.ModifierBaseScore += 11;
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState));
            Assert.That(result.Session, Is.Null);
        }

        [Test]
        public void AnOfferCannotSubstituteFallbackWhileNormalCandidatesRemain()
        {
            var normal = new[] { RewardTestContent.Modifier("first", "family.first"),
                RewardTestContent.Modifier("second", "family.second"), RewardTestContent.Modifier("third", "family.third") };
            var fallback = new ModifierDefinition("fallback", "family.fallback", ModifierPhase.PlayHand,
                new ModifierCondition(ModifierConditionKind.Never), Array.Empty<ModifierEffect>(), rewardEligible: false);
            var policy = RewardTestContent.Policy(new[] { new ModifierRewardLayout(new[] { "pool" }) });
            var catalog = RewardTestContent.Catalog(normal.Concat(new[] { fallback }),
                new[] { new ModifierRewardPoolDefinition("pool", normal.Select(value => value.Id), fallback.Id) }, policy);
            var session = RewardTestContent.Session(catalog);
            session.Execute(PlayHandAction.Instance, 0);
            var save = session.CaptureSave();
            Restore(catalog, save);
            save.PendingReward.Options[0].DefinitionId = fallback.Id;
            save.ExcludedRewardFamilies = save.ExcludedRewardFamilies.Concat(new[] { fallback.FamilyId }).ToArray();
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState));
            Assert.That(result.Session, Is.Null);
        }

        [Test]
        public void AnOfferCannotSkipARewardSlotWhileUnusedNormalCandidatesRemain()
        {
            var catalog = RewardTestContent.Catalog();
            var session = RewardTestContent.Session(catalog);
            session.Execute(PlayHandAction.Instance, 0);
            var save = session.CaptureSave();
            Restore(catalog, save);
            Assert.That(save.PendingReward.Options.Length, Is.EqualTo(3));
            save.PendingReward.Options = save.PendingReward.Options.Skip(1).ToArray();
            Assert.That(save.PendingReward.Options[0].OptionId, Is.EqualTo(save.NextOptionId - save.PendingReward.Options.Length));
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState));
            Assert.That(result.Session, Is.Null);
        }

        [Test]
        public void RestoredRevisionRejectsStaleActionsWithoutChangingStateOrFutureResults()
        {
            var catalog = ModifierCatalog();
            var uninterrupted = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
            uninterrupted.Execute(new RerollDiceAction(3), 0);
            var resumed = Restore(catalog, uninterrupted.CaptureSave());
            var before = resumed.CaptureSave();
            var rejected = resumed.Execute(new RerollDiceAction(7), 0);
            Assert.That(rejected.Rejection, Is.EqualTo(CommandRejection.StaleRevision));
            Assert.That(rejected.Events, Is.Empty);
            AssertGraphEqual(before, resumed.CaptureSave());
            AssertContinuation(uninterrupted, resumed, new RerollDiceAction(7));
        }

        [TestCase(0UL)]
        [TestCase(ulong.MaxValue)]
        public void EveryUnsignedRandomStateCanBeRestoredWithoutConsumption(ulong randomState)
        {
            var catalog = TestContent.Catalog();
            var save = TestContent.Session().CaptureSave();
            save.DiceRandomState = randomState;
            save.RewardRandomState = randomState;
            var restored = Restore(catalog, save);
            AssertGraphEqual(save, restored.CaptureSave());
            var random = new DeterministicRandom(randomState);
            var expected = new DiceValues(random.NextInt(6) + 1, random.NextInt(6) + 1, random.NextInt(6) + 1,
                random.NextInt(6) + 1, random.NextInt(6) + 1);
            var result = restored.Execute(new RerollDiceAction(31), restored.Snapshot.Revision);
            Assert.That(result.Snapshot.CurrentDice, Is.EqualTo(expected));
            Assert.That(restored.CaptureSave().DiceRandomState, Is.EqualTo(random.State));
            Assert.That(restored.CaptureSave().RewardRandomState, Is.EqualTo(randomState));
        }

        [Test]
        public void RestoringAtEveryActualStandardActionBoundaryPreservesTheCompleteVictory()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset");
            Assert.That(asset, Is.Not.Null);
            var catalog = asset.Build();
            var uninterrupted = EncounterSession.CreateEncounter(catalog, "encounter.standard", 60);
            var resumed = Restore(catalog, uninterrupted.CaptureSave());
            var traceField = typeof(StandardEncounterReplayTests).GetField("VictoryActions", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(traceField, Is.Not.Null);
            string trace = (string)traceField.GetRawConstantValue();
            var restoredPhases = new HashSet<EncounterPhase> { resumed.Snapshot.Phase };
            foreach (string row in trace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] values = row.Split(',');
                EncounterAction action;
                switch (values[0])
                {
                    case "R": action = new RerollDiceAction(int.Parse(values[1])); break;
                    case "P": action = PlayHandAction.Instance; break;
                    case "F": action = new RefreshModifierOfferAction(long.Parse(values[1])); break;
                    case "M": action = new SelectModifierAction(long.Parse(values[1]), long.Parse(values[2])); break;
                    case "U": action = new SelectHandUpgradeAction(long.Parse(values[1]), long.Parse(values[2])); break;
                    default: throw new InvalidOperationException(row);
                }
                AssertContinuation(uninterrupted, resumed, action);
                var save = resumed.CaptureSave();
                resumed = Restore(catalog, EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(save)));
                AssertGraphEqual(save, resumed.CaptureSave());
                restoredPhases.Add(resumed.Snapshot.Phase);
            }
            Assert.That(restoredPhases, Is.EquivalentTo(new[]
            {
                EncounterPhase.Playing, EncounterPhase.ChoosingModifier, EncounterPhase.ChoosingHandUpgrade, EncounterPhase.Completed
            }));
            Assert.That(resumed.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(resumed.Snapshot.TotalScore, Is.EqualTo(80561));
            Assert.That(resumed.Snapshot.Modifiers.Count, Is.EqualTo(24));
        }

        [Test]
        public void NullSaveIsRejectedWithoutProducingASession()
        {
            var result = EncounterSession.RestoreEncounter(TestContent.Catalog(), null);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.NullSave));
            Assert.That(result.Session, Is.Null);
        }

        [TestCaseSource(nameof(InvalidInitialSaves))]
        public void IncompatibleOrMalformedInitialSavesAreRejected(string name, Action<EncounterSaveData> mutate, EncounterRestoreError error)
        {
            var catalog = ModifierCatalog();
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
            var before = session.CaptureSave();
            var save = session.CaptureSave();
            mutate(save);
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.False, name);
            Assert.That(result.Error, Is.EqualTo(error), name + ": " + result.Detail);
            Assert.That(result.Session, Is.Null, name);
            AssertGraphEqual(before, session.CaptureSave());
        }

        [TestCaseSource(nameof(InvalidRewardSaves))]
        public void MalformedRewardOffersAndCachedUpgradeChoicesAreRejected(string name, Action<EncounterSaveData> mutate)
        {
            var catalog = RewardTestContent.Catalog();
            var session = RewardTestContent.Session(catalog);
            session.Execute(PlayHandAction.Instance, 0);
            var save = session.CaptureSave();
            mutate(save);
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.False, name);
            Assert.That(result.Session, Is.Null, name);
        }

        private static IEnumerable<TestCaseData> InvalidInitialSaves()
        {
            yield return Invalid("save version", value => ++value.SaveFormatVersion, EncounterRestoreError.SaveFormatVersionMismatch);
            yield return Invalid("rules version", value => ++value.RulesVersion, EncounterRestoreError.RulesVersionMismatch);
            yield return Invalid("random version", value => ++value.RandomAlgorithmVersion, EncounterRestoreError.RandomAlgorithmVersionMismatch);
            yield return Invalid("catalog version", value => value.CatalogVersion = "unknown", EncounterRestoreError.CatalogVersionMismatch);
            yield return Invalid("encounter", value => value.EncounterId = "unknown", EncounterRestoreError.EncounterNotFound);
            yield return Invalid("modifier definition", value => value.Modifiers[0].DefinitionId = "unknown", EncounterRestoreError.InvalidDefinition);
            yield return Invalid("revision", value => value.Revision = -1);
            yield return Invalid("phase", value => value.Phase = (EncounterPhase)99);
            yield return Invalid("outcome", value => value.Outcome = (EncounterOutcome)99);
            yield return Invalid("stage", value => value.StageIndex = 99);
            yield return Invalid("hand count", value => value.HandsRemaining = -1);
            yield return Invalid("reroll count", value => value.RerollsRemaining = -1);
            yield return Invalid("threshold", value => value.RemainingThreshold = -1);
            yield return Invalid("playing without hands", value => value.HandsRemaining = 0);
            yield return Invalid("playing cleared threshold", value => value.RemainingThreshold = 0);
            yield return Invalid("missing current dice", value => value.CurrentDice = null);
            yield return Invalid("dice count", value => value.CurrentDice = new[] { 1, 2, 3, 4 });
            yield return Invalid("dice face", value => value.CurrentDice[0] = 7);
            yield return Invalid("selected mask", value => value.LatestRerollMask = 32);
            yield return Invalid("changed mask", value => value.LatestChangedMask = 32);
            yield return Invalid("ever mask", value => value.EverRerolledMask = 32);
            yield return Invalid("upgrade array", value => value.HandUpgrades = new[] { 0 });
            yield return Invalid("upgrade cap", value => value.HandUpgrades[0] = 7);
            yield return Invalid("modifier array", value => value.Modifiers = null);
            yield return Invalid("null modifier", value => value.Modifiers[0] = null);
            yield return Invalid("duplicate instance", value => value.Modifiers[1].InstanceId = value.Modifiers[0].InstanceId);
            yield return Invalid("activation count", value => value.Modifiers[0].ActivationsThisHand = -1);
            yield return Invalid("contribution array", value => value.ModifierContributions = null);
            yield return Invalid("contribution instance", value => value.ModifierContributions[0].InstanceId = 99);
            yield return Invalid("contribution amount", value => ++value.ModifierContributions[0].BaseScore);
            yield return Invalid("accumulator", value => ++value.ModifierBaseScore);
            yield return Invalid("refresh currency", value => value.RewardRefreshes = -1);
            yield return Invalid("next offer identity", value => value.NextOfferId = 0);
            yield return Invalid("next option identity", value => value.NextOptionId = 0);
            yield return Invalid("unknown family", value => value.ExcludedRewardFamilies = new[] { "unknown" }, EncounterRestoreError.InvalidDefinition);
            yield return Invalid("unknown upgrade history", value => value.RecentHandUpgrades = new[] { (HandCategory)99 });
            yield return Invalid("unexpected cached choices", value => value.PendingUpgradeCategories = new[] { HandCategory.Pair });
        }

        private static IEnumerable<TestCaseData> InvalidRewardSaves()
        {
            yield return InvalidReward("missing offer", value => value.PendingReward = null);
            yield return InvalidReward("empty options", value => value.PendingReward.Options = Array.Empty<EncounterSaveRewardOptionData>());
            yield return InvalidReward("null option", value => value.PendingReward.Options[0] = null);
            yield return InvalidReward("duplicate option identity", value => value.PendingReward.Options[1].OptionId = value.PendingReward.Options[0].OptionId);
            yield return InvalidReward("wrong offer kind", value => value.PendingReward.Kind = RewardKind.HandUpgrade);
            yield return InvalidReward("wrong target stage", value => value.PendingReward.TargetStageIndex = value.StageIndex);
            yield return InvalidReward("offer identity collision", value => value.NextOfferId = value.PendingReward.OfferId);
            yield return InvalidReward("option identity collision", value => value.NextOptionId = value.PendingReward.Options[0].OptionId);
            yield return InvalidReward("unknown option definition", value => value.PendingReward.Options[0].DefinitionId = "unknown");
            yield return InvalidReward("modifier option with category", value => value.PendingReward.Options[0].HasCategory = true);
            yield return InvalidReward("missing cached choices", value => value.PendingUpgradeCategories = null);
            yield return InvalidReward("unknown cached category", value => value.PendingUpgradeCategories[0] = (HandCategory)99);
            yield return InvalidReward("duplicate cached category", value => value.PendingUpgradeCategories[1] = value.PendingUpgradeCategories[0]);
            yield return InvalidReward("capped cached category", value => value.HandUpgrades[(int)value.PendingUpgradeCategories[0]] = 6);
        }

        private static TestCaseData Invalid(string name, Action<EncounterSaveData> mutate,
            EncounterRestoreError error = EncounterRestoreError.InvalidState)
            => new TestCaseData(name, mutate, error).SetName("InvalidInitialSave_" + name);

        private static TestCaseData InvalidReward(string name, Action<EncounterSaveData> mutate)
            => new TestCaseData(name, mutate).SetName("InvalidRewardSave_" + name);

        private static MechanicalCatalog ModifierCatalog()
        {
            var definitions = new[]
            {
                new ModifierDefinition("initial", "family.initial", ModifierPhase.InitialRoll,
                    new ModifierCondition(ModifierConditionKind.Always), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 5) }),
                new ModifierDefinition("reroll", "family.reroll", ModifierPhase.PlayerReroll,
                    new ModifierCondition(ModifierConditionKind.Always), new[] { new ModifierEffect(ModifierEffectKind.AddMultiplier, 2),
                        new ModifierEffect(ModifierEffectKind.AddRerolls, 1) }),
                new ModifierDefinition("play", "family.play", ModifierPhase.PlayHand,
                    new ModifierCondition(ModifierConditionKind.Always), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 11) })
            };
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                startingModifiers: new[] { "initial", "reroll", "reroll", "play" });
            return new MechanicalCatalog("test.persistence", TestContent.Hands(), new[] { encounter }, definitions);
        }

        private static EncounterSession Restore(MechanicalCatalog catalog, EncounterSaveData save)
        {
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.True, result.Error + ": " + result.Detail);
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.None));
            Assert.That(result.Session, Is.Not.Null);
            Assert.That(result.Session.InitialResolution.Events, Is.Empty);
            Assert.That(result.Session.InitialResolution.Snapshot.Revision, Is.EqualTo(save.Revision));
            return result.Session;
        }

        private static void AssertContinuation(EncounterSession uninterrupted, EncounterSession resumed, EncounterAction action)
        {
            var expected = uninterrupted.Execute(action, uninterrupted.Snapshot.Revision);
            var actual = resumed.Execute(action, resumed.Snapshot.Revision);
            Assert.That(expected.Accepted, Is.True);
            Assert.That(actual.Accepted, Is.True);
            EncounterSessionTests.AssertSnapshotsEqual(expected.Snapshot, actual.Snapshot);
            AssertGraphEqual(uninterrupted.CaptureSave(), resumed.CaptureSave());
            Assert.That(actual.Events.Count, Is.EqualTo(expected.Events.Count));
            for (int i = 0; i < expected.Events.Count; ++i)
            {
                Assert.That(actual.Events[i].GetType(), Is.EqualTo(expected.Events[i].GetType()));
                foreach (var property in expected.Events[i].GetType().GetProperties())
                {
                    var expectedValue = property.GetValue(expected.Events[i]);
                    var actualValue = property.GetValue(actual.Events[i]);
                    if (expectedValue is ScoreBreakdown score)
                        foreach (var scoreProperty in typeof(ScoreBreakdown).GetProperties())
                            Assert.That(scoreProperty.GetValue(actualValue), Is.EqualTo(scoreProperty.GetValue(score)), scoreProperty.Name);
                    else Assert.That(actualValue, Is.EqualTo(expectedValue), property.Name);
                }
            }
        }

        internal static void AssertGraphEqual(object expected, object actual, string path = "save")
        {
            if (expected == null || actual == null)
            {
                Assert.That(actual, Is.EqualTo(expected), path);
                return;
            }
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()), path);
            if (expected is Array expectedArray)
            {
                var actualArray = (Array)actual;
                Assert.That(actualArray.Length, Is.EqualTo(expectedArray.Length), path);
                for (int i = 0; i < expectedArray.Length; ++i)
                    AssertGraphEqual(expectedArray.GetValue(i), actualArray.GetValue(i), path + "[" + i + "]");
                return;
            }
            var type = expected.GetType();
            if (type.IsPrimitive || type.IsEnum || expected is string)
            {
                Assert.That(actual, Is.EqualTo(expected), path);
                return;
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                AssertGraphEqual(field.GetValue(expected), field.GetValue(actual), path + "." + field.Name);
        }
    }
}
