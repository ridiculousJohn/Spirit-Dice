using System;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;

namespace Dice.Tests
{
    public sealed class WatchtowerEncounterTests
    {
        private const string EncounterId = "encounter.owl.watchtower";
        private static readonly string[] Families =
        {
            "modifier.play_hand.high_die", "modifier.play_hand.middle_unique",
            "modifier.play_hand.adjacent_different", "modifier.play_hand.range_greater_than",
            "modifier.play_hand.exactly_one_face", "modifier.play_hand.face_equals_position",
            "modifier.player_reroll.selected_face_unchanged", "modifier.player_reroll.exactly_one_selected_face",
            "modifier.player_reroll.exactly_one_selected_matches_selected_count",
            "modifier.player_reroll.high_die_or_straight"
        };

        [Test]
        public void AuthoredOutcomesHaveHostMotionAndLeadToTheCrossroads()
        {
            var journeys = AssetDatabase.LoadAssetAtPath<EncounterJourneyCatalogAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/SpiritJourneyCatalog.asset");
            Assert.DoesNotThrow(() => journeys.Validate(Catalog()));
            var journey = journeys.Find(EncounterId);
            Assert.That(journey.FollowUpEncounterIds, Is.EqualTo(new[] { "encounter.moth.crossroads" }));
            foreach (var outcome in new[] { EncounterOutcome.Won, EncounterOutcome.Lost, EncounterOutcome.Abandoned })
            {
                var ending = journey.GetOutcomePresentation(outcome);
                Assert.That(ending, Is.Not.Null, outcome.ToString());
                Assert.That(ending.ShowDialogue, Is.EqualTo(outcome != EncounterOutcome.Abandoned));
                var sequences = journey.TablePresentation.HostPresentation.Sequences.Where(sequence => sequence.State == ending.HostState).ToArray();
                Assert.That(sequences, Is.Not.Empty, outcome.ToString());
                Assert.That(sequences.All(sequence => !sequence.Loop && sequence.Duration > 0), Is.True);
            }
        }

        [Test]
        public void AuthoredEncounterUsesTenProvisionalStagesAndOnlyTheDistinctFaceRule()
        {
            var catalog = Catalog();
            var encounter = catalog.GetEncounter(EncounterId);
            Assert.That(encounter.SpiritId, Is.EqualTo("spirit.owl"));
            Assert.That(encounter.Stages.Select(value => value.Threshold),
                Is.EqualTo(catalog.GetEncounter("encounter.fox.introduction").Stages.Select(value => value.Threshold)));
            Assert.That(encounter.Stages.Count, Is.EqualTo(10));
            Assert.That(encounter.StartingModifiers, Is.Empty);
            Assert.That(encounter.StartingUpgrades, Is.EqualTo(new int[HandDefinition.CategoryCount]));
            Assert.That(encounter.Rules.Single(), Is.TypeOf<DistinctFacesMultiplierRuleDefinition>());
            var asset = AssetDatabase.LoadAssetAtPath<DistinctFacesMultiplierRuleAsset>(
                "Assets/Dice/Content/Mechanical/DistinctFacesMultiplier.asset");
            Assert.That(asset.Build().Id, Is.EqualTo(DistinctFacesMultiplierRuleDefinition.StableId));
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, 27);
            var reroll = Execute(session, new RerollDiceAction(1));
            Assert.That(reroll.Snapshot.RerollsRemaining, Is.EqualTo(2));
            Assert.That(reroll.Events.OfType<EncounterRuleActivatedEvent>(), Is.Empty);
        }

        [TestCase("base_score")]
        [TestCase("multiplier")]
        [TestCase("combined")]
        [TestCase("rerolls")]
        public void PoolsContainTenUsableFamiliesAndThreeScopedMilestoneFamilies(string role)
        {
            var catalog = Catalog();
            var encounter = catalog.GetEncounter(EncounterId);
            var pool = catalog.GetRewardPool("reward_pool.owl.watchtower." + role);
            Assert.That(pool.ModifierIds.Count, Is.EqualTo(13));
            Assert.That(pool.FallbackModifierId, Is.Null);
            Assert.That(pool.ModifierIds.Where(id => catalog.GetModifier(id).Availability.Access != ModifierAccess.PermanentUnlock),
                Is.EquivalentTo(Families.Select(value => value + "." + role)));
            Assert.That(pool.ModifierIds.All(id => catalog.GetModifier(id).Availability.MatchesEncounter(encounter)), Is.True);
            Assert.That(encounter.UnlockMilestones.Select(value => value.StageNumber), Is.EqualTo(new[] { 3, 6, 10 }));
            string[] milestones = { "modifier.play_hand.edge_pair_sums_equal", "modifier.play_hand.alternating_parity",
                "modifier.play_hand.ordered" };
            for (int i = 0; i < milestones.Length; ++i)
            {
                var members = encounter.UnlockMilestones[i].ModifierIds.Select(catalog.GetModifier).ToArray();
                Assert.That(members.Length, Is.EqualTo(4));
                Assert.That(members.All(value => value.FamilyId == milestones[i]), Is.True);
                foreach (var member in members)
                {
                    Assert.That(member.Availability.Access, Is.EqualTo(ModifierAccess.PermanentUnlock));
                    Assert.That(member.Availability.Scope, Is.EqualTo(ModifierAvailabilityScope.Spirit));
                    Assert.That(member.Availability.ScopeId, Is.EqualTo("spirit.owl"));
                    Assert.That(member.Availability.MatchesEncounter(catalog.GetEncounter("encounter.fox.introduction")), Is.False);
                }
            }
        }

        [TestCase(1, 1, 1, 1, 1, 1)]
        [TestCase(3, 3, 3, 5, 5, 2)]
        [TestCase(2, 2, 3, 5, 6, 4)]
        [TestCase(1, 2, 3, 5, 6, 5)]
        public void OnlyDistinctFinalFacesIncreaseMultiplierAndPreviewMatchesCommittedScore(
            int first, int second, int third, int fourth, int fifth, int distinct)
        {
            var catalog = RuleCatalog();
            var session = WithDice(catalog, new DiceValues(first, second, third, fourth, fifth));
            var before = session.CaptureSave();
            var baseline = HandScorer.Calculate(session.Snapshot.CurrentDice, catalog, session.Snapshot.HandUpgrades);
            var preview = session.PreviewHandResolution();
            EncounterPersistenceTests.AssertGraphEqual(before, session.CaptureSave());
            Assert.That(preview.Score.EncounterRuleMultiplier, Is.EqualTo(distinct));
            Assert.That(preview.Score.ModifierMultiplier, Is.Zero);
            Assert.That(preview.Score.Multiplier, Is.EqualTo(baseline.Multiplier + distinct));
            Assert.That(preview.Score.HandScore, Is.EqualTo(baseline.BaseScore * (baseline.Multiplier + distinct)));
            var result = Execute(session, PlayHandAction.Instance);
            var activation = result.Events.OfType<EncounterRuleActivatedEvent>().Single();
            Assert.That(activation.RuleId, Is.EqualTo(DistinctFacesMultiplierRuleDefinition.StableId));
            Assert.That(activation.MultiplierBonus, Is.EqualTo(distinct));
            var scored = result.Events.OfType<HandScoredEvent>().Single();
            Assert.That(scored.Score.HandScore, Is.EqualTo(preview.Score.HandScore));
            Assert.That(result.Events.ToList().IndexOf(activation), Is.LessThan(result.Events.ToList().IndexOf(scored)));
            Assert.That(session.Snapshot.TotalScore, Is.EqualTo(preview.Score.HandScore));
            Restore(catalog, session.CaptureSave());
        }

        [Test]
        public void RerollsNeverBankRuleBonusAndRestorePreservesPreviewAndContinuation()
        {
            var catalog = RuleCatalog();
            var session = WithDice(catalog, new DiceValues(1, 2, 3, 5, 6));
            for (int i = 0; i < 3; ++i)
            {
                var result = Execute(session, new RerollDiceAction(DiceValues.AllMask));
                Assert.That(result.Events.OfType<EncounterRuleActivatedEvent>(), Is.Empty);
                int distinct = session.Snapshot.CurrentDice.ToArray().Distinct().Count();
                Assert.That(session.PreviewPlayHand().EncounterRuleMultiplier, Is.EqualTo(distinct));
                Assert.That(session.Snapshot.ModifierMultiplier, Is.Zero);
            }
            var resumed = Restore(catalog, session.CaptureSave());
            Assert.That(resumed.PreviewPlayHand().HandScore, Is.EqualTo(session.PreviewPlayHand().HandScore));
            for (int i = 0; i < 3; ++i)
            {
                var expected = Execute(session, PlayHandAction.Instance);
                var actual = Execute(resumed, PlayHandAction.Instance);
                Assert.That(actual.Events.OfType<HandScoredEvent>().Single().Score.HandScore,
                    Is.EqualTo(expected.Events.OfType<HandScoredEvent>().Single().Score.HandScore));
                EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), resumed.CaptureSave());
            }
        }

        [Test]
        public void RuleBonusCombinesWithUpgradesAndModifierBonusesWithoutBecomingAModifier()
        {
            var modifier = new ModifierDefinition("test.pattern", "test.pattern", ModifierPhase.PlayHand,
                new ModifierCondition(ModifierConditionKind.MiddleUnique),
                new[] { new ModifierEffect(ModifierEffectKind.AddMultiplier, 7) });
            var upgrades = new int[HandDefinition.CategoryCount];
            upgrades[(int)HandCategory.HighDie] = 2;
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 3, 3) },
                upgrades, new[] { modifier.Id }, rules: new[] { new DistinctFacesMultiplierRuleDefinition() });
            var catalog = new MechanicalCatalog("test.owl", TestContent.Hands(), new[] { encounter }, new[] { modifier });
            var session = WithDice(catalog, new DiceValues(1, 2, 3, 5, 6));
            var preview = session.PreviewPlayHand();
            Assert.That(preview.ModifierMultiplier, Is.EqualTo(7));
            Assert.That(preview.EncounterRuleMultiplier, Is.EqualTo(5));
            Assert.That(preview.ModifierContributions.Count, Is.EqualTo(1));
            Assert.That(preview.Multiplier, Is.EqualTo(preview.CategoryMultiplier + preview.UpgradeMultiplier + 12));
            var result = Execute(session, PlayHandAction.Instance);
            Assert.That(result.Events.OfType<HandScoredEvent>().Single().Score.HandScore, Is.EqualTo(preview.HandScore));
            Restore(catalog, session.CaptureSave());
        }

        [TestCase(1UL)]
        [TestCase(27UL)]
        [TestCase(99UL)]
        public void FullProgressionKeepsRewardOffersRestorableAndUnlocksEachOwlMilestone(ulong seed)
        {
            var authored = Catalog();
            var source = authored.GetEncounter(EncounterId);
            var encounter = new EncounterDefinition(EncounterId,
                source.Stages.Select(stage => new StageDefinition(1, stage.StartingHands, stage.StartingRerolls,
                    stage.ExitBehavior)), source.StartingUpgrades, source.StartingModifiers, source.RewardPolicy,
                source.Rules, source.SpiritId, source.UnlockMilestones);
            var catalog = new MechanicalCatalog("test.owl.progression", authored.Hands, new[] { encounter },
                authored.Modifiers, authored.RewardPools);
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, seed);
            for (int cleared = 1; cleared <= 10; ++cleared)
            {
                Execute(session, PlayHandAction.Instance);
                foreach (var milestone in encounter.UnlockMilestones.Where(value => value.StageNumber <= cleared))
                    Assert.That(milestone.ModifierIds.Except(session.Snapshot.UnlockedModifierIds), Is.Empty);
                var restored = Restore(catalog, session.CaptureSave());
                EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), restored.CaptureSave());
                session = restored;
                if (cleared == 10) break;
                Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
                Assert.That(session.Snapshot.PendingReward.Options.Count, Is.EqualTo(3));
                Assert.That(session.Snapshot.PendingReward.Options.Select(option =>
                    catalog.GetModifier(option.DefinitionId).FamilyId).Distinct().Count(), Is.EqualTo(3));
                var offer = session.Snapshot.PendingReward;
                Execute(session, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
                offer = session.Snapshot.PendingReward;
                Execute(session, new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId));
            }
            Assert.That(session.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(session.Snapshot.UnlockedModifierIds.Count, Is.EqualTo(12));
            Assert.That(session.Snapshot.EncounterRules.Single().IsAvailable, Is.False);
        }

        private static MechanicalCatalog Catalog()
            => AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset")
                .Build(TestContent.ModifierAssets());

        private static MechanicalCatalog RuleCatalog()
            => new MechanicalCatalog("test.owl", TestContent.Hands(), new[]
            {
                new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 5, 3) },
                    rules: new[] { new DistinctFacesMultiplierRuleDefinition() })
            });

        private static EncounterSession WithDice(MechanicalCatalog catalog, DiceValues dice)
        {
            var save = EncounterSession.CreateEncounter(catalog, EncounterId, 27).CaptureSave();
            save.CurrentDice = dice.ToArray();
            save.InitialDice = dice.ToArray();
            return Restore(catalog, save);
        }

        private static EncounterSession Restore(MechanicalCatalog catalog, EncounterSaveData save)
        {
            var restored = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(restored.Success, Is.True, restored.Error + ": " + restored.Detail);
            return restored.Session;
        }

        private static Resolution Execute(EncounterSession session, EncounterAction action)
        {
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            return result;
        }
    }
}
