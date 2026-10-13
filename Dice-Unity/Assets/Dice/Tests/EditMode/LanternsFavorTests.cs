using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    public sealed class LanternsFavorTests
    {
        private const string EncounterId = "encounter.moth.crossroads";
        private const string RuleId = CyclingFavoredFaceMultiplierRuleDefinition.StableId;

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void EachMatchingDieAddsMultiplierOnlyToTheScoredHand(int matches)
        {
            var catalog = RuleCatalog(new StageDefinition(1000000, 12, 3));
            int[] dice = Enumerable.Repeat(2, DiceValues.Count).ToArray();
            for (int i = 0; i < matches; ++i) dice[i] = 1;
            var save = EncounterSession.CreateEncounter(catalog, EncounterId, 27).CaptureSave();
            save.CurrentDice = dice;
            save.InitialDice = (int[])dice.Clone();
            var session = Restore(catalog, save);
            var before = session.CaptureSave();
            var baseline = HandScorer.Calculate(session.Snapshot.CurrentDice, catalog, session.Snapshot.HandUpgrades);
            var preview = session.PreviewPlayHand();

            EncounterPersistenceTests.AssertGraphEqual(before, session.CaptureSave());
            Assert.That(preview.EncounterRuleMultiplier, Is.EqualTo(matches * 2));
            Assert.That(preview.ModifierMultiplier, Is.Zero);
            Assert.That(preview.Multiplier, Is.EqualTo(baseline.Multiplier + matches * 2));
            Assert.That(preview.HandScore, Is.EqualTo(baseline.BaseScore * (baseline.Multiplier + matches * 2)));

            var resolution = Execute(session, PlayHandAction.Instance);
            Assert.That(resolution.Events.OfType<HandScoredEvent>().Single().Score.HandScore, Is.EqualTo(preview.HandScore));
            var activations = resolution.Events.OfType<EncounterRuleActivatedEvent>().ToArray();
            Assert.That(activations.Length, Is.EqualTo(matches == 0 ? 0 : 1));
            if (matches > 0)
            {
                Assert.That(activations[0].RuleId, Is.EqualTo(RuleId));
                Assert.That(activations[0].MultiplierBonus, Is.EqualTo(matches * 2));
            }
            Assert.That(session.Snapshot.EncounterRules.Single().FavoredFace, Is.EqualTo(2));
            Assert.That(session.Snapshot.ModifierMultiplier, Is.Zero);
            Restore(catalog, session.CaptureSave());
        }

        [Test]
        public void RerollsDoNotAdvanceTheFaceAndResumePreservesTheFullCycle()
        {
            var catalog = RuleCatalog(new StageDefinition(1000000, 12, 7));
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, 27);
            var presentation = AssetDatabase.LoadAssetAtPath<EncounterRulePresentationAsset>(
                "Assets/Dice/Content/Presentation/MothCrossroads/LanternsFavor.asset");
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.RuleId, Is.EqualTo(RuleId));
            Assert.That(presentation.ActivationPhase, Is.EqualTo(ModifierPhase.PlayHand));

            for (int hand = 0; hand < 7; ++hand)
            {
                int favored = hand % 6 + 1;
                Assert.That(session.Snapshot.EncounterRules.Single().FavoredFace, Is.EqualTo(favored));
                Assert.That(presentation.TextFor(session.Snapshot.EncounterRules.Single()),
                    Is.EqualTo("Face " + favored + ": +2 per match"));
                var reroll = Execute(session, new RerollDiceAction(1));
                Assert.That(session.Snapshot.EncounterRules.Single().FavoredFace, Is.EqualTo(favored));
                Assert.That(reroll.Events.OfType<EncounterRuleActivatedEvent>(), Is.Empty);
                Assert.That(session.Snapshot.ModifierMultiplier, Is.Zero);

                var resumed = Restore(catalog, session.CaptureSave());
                Assert.That(resumed.Snapshot.EncounterRules.Single(), Is.EqualTo(session.Snapshot.EncounterRules.Single()));
                Assert.That(session.PreviewPlayHand().EncounterRuleMultiplier,
                    Is.EqualTo(session.Snapshot.CurrentDice.ToArray().Count(value => value == favored) * 2));
                var original = Execute(session, PlayHandAction.Instance);
                var continued = Execute(resumed, PlayHandAction.Instance);
                Assert.That(continued.Events.OfType<HandScoredEvent>().Single().Score.HandScore,
                    Is.EqualTo(original.Events.OfType<HandScoredEvent>().Single().Score.HandScore));
                EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), resumed.CaptureSave());
                session = resumed;
            }
        }

        [Test]
        public void StageBoundaryAndRestoreRetainTheEncounterWideFavoredFace()
        {
            var catalog = RuleCatalog(new StageDefinition(1, 3, 3), new StageDefinition(1000000, 3, 3));
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, 27);
            Execute(session, PlayHandAction.Instance);
            Assert.That(session.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(session.Snapshot.HandIndex, Is.Zero);
            Assert.That(session.Snapshot.EncounterRules.Single().FavoredFace, Is.EqualTo(2));
            Assert.That(Restore(catalog, session.CaptureSave()).Snapshot.EncounterRules.Single().FavoredFace, Is.EqualTo(2));
        }

        [Test]
        public void AuthoredMothMilestonesAndPoolsReuseSharedCanonicalModifiers()
        {
            var catalog = TestContent.BuildStandardCatalog();
            var encounter = catalog.GetEncounter(EncounterId);
            Assert.That(encounter.Rules.Select(value => value.Id), Is.EqualTo(new[] { RuleId }));
            Assert.That(encounter.UnlockMilestones, Is.Not.Empty);
            foreach (var milestone in encounter.UnlockMilestones)
            {
                Assert.That(milestone.ModifierIds, Is.Not.Empty);
                var families = milestone.ModifierIds.Select(id => catalog.GetModifier(id).FamilyId).Distinct().ToArray();
                Assert.That(milestone.ModifierIds, Is.EquivalentTo(catalog.Modifiers
                    .Where(value => families.Contains(value.FamilyId)).Select(value => value.Id)));
                foreach (string id in milestone.ModifierIds)
                {
                    var modifier = catalog.GetModifier(id);
                    Assert.That(id, Does.Not.StartWith("modifier.moth."));
                    Assert.That(modifier.Availability.Scope, Is.EqualTo(ModifierAvailabilityScope.Global));
                    Assert.That(modifier.Availability.Access, Is.EqualTo(ModifierAccess.PermanentUnlock));
                    Assert.That(modifier.Availability.MatchesEncounter(catalog.GetEncounter("encounter.owl.watchtower")), Is.True);
                    Assert.That(modifier.Availability.MatchesEncounter(catalog.GetEncounter("encounter.fox.introduction")), Is.True);
                }
            }
            foreach (string poolId in encounter.RewardPolicy.ModifierLayouts.SelectMany(value => value.PoolIds).Distinct())
            {
                var pool = catalog.GetRewardPool(poolId);
                Assert.That(pool.ModifierIds, Is.Not.Empty);
                foreach (string id in pool.ModifierIds)
                {
                    var modifier = catalog.GetModifier(id);
                    Assert.That(id, Does.Not.StartWith("modifier.moth."));
                    Assert.That(modifier.RewardEligible, Is.True);
                    Assert.That(modifier.Availability.MatchesEncounter(encounter), Is.True);
                }
            }
        }

        [Test]
        public void ClearingAuthoredStagesUnlocksMilestonesAndOffersValidRewards()
        {
            var authored = TestContent.BuildStandardCatalog();
            var source = authored.GetEncounter(EncounterId);
            var encounter = new EncounterDefinition(EncounterId,
                source.Stages.Select(stage => new StageDefinition(1, stage.StartingHands, stage.StartingRerolls, stage.ExitBehavior)),
                source.StartingUpgrades, source.StartingModifiers, source.RewardPolicy, source.Rules, source.SpiritId, source.UnlockMilestones);
            var catalog = new MechanicalCatalog("test.moth.progression", authored.Hands, new[] { encounter }, authored.Modifiers, authored.RewardPools);
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, 27);
            for (int cleared = 1; cleared <= source.Stages.Count; ++cleared)
            {
                Assert.That(session.Snapshot.EncounterRules.Single().FavoredFace, Is.EqualTo((cleared - 1) % 6 + 1));
                Execute(session, PlayHandAction.Instance);
                var unlocked = source.UnlockMilestones.Where(value => value.StageNumber <= cleared)
                    .SelectMany(value => value.ModifierIds).Distinct().ToArray();
                Assert.That(session.Snapshot.UnlockedModifierIds, Is.EquivalentTo(unlocked));
                session = Restore(catalog, session.CaptureSave());
                if (cleared == source.Stages.Count) break;

                var offer = session.Snapshot.PendingReward;
                Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
                Assert.That(offer.Options, Is.Not.Empty);
                var offered = offer.Options.Select(option => catalog.GetModifier(option.DefinitionId)).ToArray();
                Assert.That(offered.Select(value => value.FamilyId).Distinct().Count(), Is.EqualTo(offered.Length));
                foreach (var modifier in offered)
                {
                    Assert.That(modifier.RewardEligible, Is.True);
                    Assert.That(modifier.Availability.MatchesEncounter(encounter), Is.True);
                    Assert.That(modifier.Availability.Access != ModifierAccess.PermanentUnlock || unlocked.Contains(modifier.Id), Is.True);
                }
                int face = session.Snapshot.EncounterRules.Single().FavoredFace;
                Execute(session, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
                offer = session.Snapshot.PendingReward;
                Assert.That(offer.Kind, Is.EqualTo(RewardKind.HandUpgrade));
                Assert.That(offer.Options, Is.Not.Empty);
                Execute(session, new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId));
                Assert.That(session.Snapshot.EncounterRules.Single().FavoredFace, Is.EqualTo(face));
            }
            Assert.That(session.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
        }

        private static MechanicalCatalog RuleCatalog(params StageDefinition[] stages)
            => new MechanicalCatalog("test.lanterns_favor", TestContent.Hands(), new[]
            {
                new EncounterDefinition(EncounterId, stages,
                    rules: new[] { new CyclingFavoredFaceMultiplierRuleDefinition() }, spiritId: "spirit.moth")
            });

        private static EncounterSession Restore(MechanicalCatalog catalog, EncounterSaveData save)
        {
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.True, result.Error + ": " + result.Detail);
            EncounterPersistenceTests.AssertGraphEqual(save, result.Session.CaptureSave());
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
