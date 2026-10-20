using System;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class StandardEncounterReplayTests
    {
        [Test]
        public void DiagnosticControlsCompleteTheLegacyStandardVictoryReplay()
        {
            var host = new GameObject("Diagnostic victory replay");
            try
            {
                var controller = AuthoredController(host);
                var catalog = LegacyCatalog();
                controller.Initialize(catalog, "encounter.standard", 60, null);
                var expected = EncounterSession.CreateEncounter(catalog, "encounter.standard", 60);
                foreach (string row in VictoryActions.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] values = row.Split(',');
                    var action = Action(values, expected);
                    switch (values[0])
                    {
                        case "R":
                            controller.ClearDiceSelection();
                            int mask = int.Parse(values[1]);
                            for (int index = 0; index < DiceValues.Count; ++index)
                                if ((mask & (1 << index)) != 0) controller.ToggleDie(index);
                            controller.Reroll();
                            break;
                        case "P":
                            Assert.That(controller.Preview.Score.HandScore, Is.EqualTo(long.Parse(values[2])), row);
                            controller.PlayHand();
                            break;
                        case "F": controller.RefreshRewards(); break;
                        case "M":
                        case "U": controller.ChooseReward(long.Parse(values[1]), long.Parse(values[2])); break;
                    }
                    var result = expected.Execute(action, expected.Snapshot.Revision);
                    Assert.That(result.Accepted, Is.True, row);
                    EncounterSessionTests.AssertSnapshotsEqual(result.Snapshot, controller.Snapshot);
                }
                Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
                Assert.That(controller.Snapshot.TotalScore, Is.EqualTo(80561));
                Assert.That(controller.Preview, Is.Null);
                Assert.That(controller.EventLog.Last(), Does.Contain("Won"));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void DiagnosticControlsReachResourceExhaustionLoss()
        {
            var host = new GameObject("Diagnostic loss replay");
            try
            {
                var controller = AuthoredController(host);
                controller.Initialize(Catalog(), "encounter.standard", 0, null);
                for (int i = 0; i < 200 && controller.Snapshot.Phase != EncounterPhase.Completed; ++i)
                {
                    if (controller.Snapshot.Phase == EncounterPhase.Playing) controller.PlayHand();
                    else
                    {
                        var offer = controller.Snapshot.PendingReward;
                        controller.ChooseReward(offer.OfferId, offer.Options[0].OptionId);
                    }
                }
                Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
                Assert.That(controller.Snapshot.HandsRemaining, Is.Zero);
                Assert.That(controller.Preview, Is.Null);
                Assert.That(controller.EventLog.Last(), Does.Contain("Lost"));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static DiagnosticEncounterController AuthoredController(GameObject host)
        {
            host.SetActive(false);
            var controller = host.AddComponent<DiagnosticEncounterController>();
            var prefab = AssetDatabase.LoadAssetAtPath<DiagnosticEncounterView>(
                "Assets/Dice/Content/Presentation/UI/DiagnosticEncounter.prefab");
            Assert.That(prefab, Is.Not.Null);
            var view = UnityEngine.Object.Instantiate(prefab, host.transform, false);
            using var serialized = new SerializedObject(controller);
            serialized.FindProperty("view").objectReferenceValue = view;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        [Test]
        public void LegacyStandardCatalogCompletesAllTwentyFiveStagesThroughLegalRewardAndPlayActions()
        {
            var catalog = LegacyCatalog();
            var session = EncounterSession.CreateEncounter(catalog, "encounter.standard", 60);
            var control = EncounterSession.CreateEncounter(catalog, "encounter.standard", 60);
            Assert.That(session.Snapshot.StageCount, Is.EqualTo(25));
            Assert.That(session.Snapshot.StartingThreshold, Is.EqualTo(160));
            Assert.That(session.Snapshot.HandsRemaining, Is.EqualTo(3));
            Assert.That(session.Snapshot.RerollsRemaining, Is.EqualTo(3));
            Assert.That(session.Snapshot.Modifiers, Is.Empty);
            Assert.That(session.Snapshot.HandUpgrades, Is.EqualTo(new int[HandDefinition.CategoryCount]));
            var cleared = new List<int>();
            int modifiers = 0, upgrades = 0, refreshes = 0;
            foreach (string row in VictoryActions.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] values = row.Split(',');
                var before = session.Snapshot;
                var action = Action(values, session);
                var result = session.Execute(action, before.Revision);
                var repeated = control.Execute(action, control.Snapshot.Revision);
                Assert.That(result.Accepted, Is.True, row);
                Assert.That(result.PreviousRevision, Is.EqualTo(before.Revision), row);
                Assert.That(result.Snapshot.Revision, Is.EqualTo(before.Revision + 1), row);
                EncounterSessionTests.AssertSnapshotsEqual(repeated.Snapshot, result.Snapshot);
                AssertEventsEqual(repeated.Events, result.Events);
                cleared.AddRange(result.Events.OfType<StageClearedEvent>().Select(value => value.StageIndex));
                modifiers += result.Events.OfType<ModifierAcquiredEvent>().Count();
                upgrades += result.Events.OfType<HandUpgradedEvent>().Count();
                refreshes += result.Events.OfType<RewardOfferedEvent>().Count(value => value.IsRefresh);
            }
            var final = session.Snapshot;
            Assert.That(final.Phase, Is.EqualTo(EncounterPhase.Completed));
            Assert.That(final.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(cleared, Is.EqualTo(Enumerable.Range(0, 25)));
            Assert.That(final.StageIndex, Is.EqualTo(24));
            Assert.That(final.StartingThreshold, Is.EqualTo(7725));
            Assert.That(final.RemainingThreshold, Is.Zero);
            Assert.That(final.TotalScore, Is.EqualTo(80561));
            Assert.That(final.TotalHandsPlayed, Is.EqualTo(67));
            Assert.That(final.TotalRerollsSpent, Is.EqualTo(140));
            Assert.That(modifiers, Is.EqualTo(24));
            Assert.That(upgrades, Is.EqualTo(24));
            Assert.That(refreshes, Is.EqualTo(9));
            Assert.That(final.Modifiers.Count, Is.EqualTo(24));
            Assert.That(final.HandUpgrades, Is.EqualTo(new[] { 0, 0, 0, 6, 4, 6, 2, 6 }));
            Assert.That(final.RewardRefreshes, Is.EqualTo(16));
            Assert.That(final.PendingReward, Is.Null);
        }

        [Test]
        public void ActualStandardCatalogCanLoseByExhaustingHandsThroughLegalActions()
        {
            var catalog = Catalog();
            var session = EncounterSession.CreateEncounter(catalog, "encounter.standard", 0);
            var control = EncounterSession.CreateEncounter(catalog, "encounter.standard", 0);
            int actions = 0;
            while (session.Snapshot.Phase != EncounterPhase.Completed && actions++ < 200)
            {
                var before = session.Snapshot;
                EncounterAction action;
                if (before.Phase == EncounterPhase.Playing) action = PlayHandAction.Instance;
                else
                {
                    var offer = before.PendingReward;
                    action = before.Phase == EncounterPhase.ChoosingModifier
                        ? (EncounterAction)new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId)
                        : new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId);
                }
                var result = session.Execute(action, before.Revision);
                var repeated = control.Execute(action, control.Snapshot.Revision);
                Assert.That(result.Accepted, Is.True);
                EncounterSessionTests.AssertSnapshotsEqual(repeated.Snapshot, result.Snapshot);
                AssertEventsEqual(repeated.Events, result.Events);
                if (result.Snapshot.Phase == EncounterPhase.Completed)
                {
                    Assert.That(result.Events.OfType<EncounterCompletedEvent>().Single().Outcome, Is.EqualTo(EncounterOutcome.Lost));
                    Assert.That(result.Events.OfType<StageClearedEvent>(), Is.Empty);
                    Assert.That(result.Events.OfType<DiceRolledEvent>(), Is.Empty);
                    Assert.That(result.Events.OfType<RewardOfferedEvent>(), Is.Empty);
                }
            }
            Assert.That(session.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            Assert.That(session.Snapshot.HandsRemaining, Is.Zero);
            Assert.That(session.Snapshot.RemainingThreshold, Is.GreaterThan(0));
            Assert.That(session.Snapshot.TotalRerollsSpent, Is.Zero);
            Assert.That(session.Snapshot.PendingReward, Is.Null);
        }

        [Test]
        public void ActualStandardProgressionCatalogReproducesOffersAndActionsThroughCompletion()
        {
            var catalog = Catalog();
            Assert.That(catalog.GetEncounter("encounter.standard").RewardPolicy.TierRules, Is.Not.Empty);
            Assert.That(catalog.Modifiers.Any(value => value.Availability.PowerTier > 1), Is.True);
            var session = EncounterSession.CreateEncounter(catalog, "encounter.standard", 60);
            var repeated = EncounterSession.CreateEncounter(catalog, "encounter.standard", 60);
            var phases = new HashSet<EncounterPhase> { session.Snapshot.Phase };
            int refreshes = 0;
            for (int count = 0; count < 1000 && session.Snapshot.Phase != EncounterPhase.Completed; ++count)
            {
                var action = ProgressionAction(catalog, session);
                var before = session.Snapshot;
                var result = session.Execute(action, before.Revision);
                var control = repeated.Execute(action, repeated.Snapshot.Revision);
                Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
                Assert.That(control.Accepted, Is.True, control.Rejection.ToString());
                Assert.That(result.Snapshot.Revision, Is.EqualTo(before.Revision + 1));
                EncounterSessionTests.AssertSnapshotsEqual(control.Snapshot, result.Snapshot);
                EncounterPersistenceTests.AssertGraphEqual(repeated.CaptureSave(), session.CaptureSave());
                AssertEventsEqual(control.Events, result.Events);
                phases.Add(result.Snapshot.Phase);
                refreshes += result.Events.OfType<RewardOfferedEvent>().Count(value => value.IsRefresh);
            }
            Assert.That(phases, Is.EquivalentTo(new[] { EncounterPhase.Playing, EncounterPhase.ChoosingModifier,
                EncounterPhase.ChoosingHandUpgrade, EncounterPhase.Completed }));
            Assert.That(refreshes, Is.GreaterThan(0));
            Assert.That(session.Snapshot.StageIndex, Is.GreaterThanOrEqualTo(1));
            Assert.That(session.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won).Or.EqualTo(EncounterOutcome.Lost));
            Assert.That(session.Snapshot.PendingReward, Is.Null);
        }

        internal static EncounterAction ProgressionAction(MechanicalCatalog catalog, EncounterSession session)
        {
            var snapshot = session.Snapshot;
            // This prefix clears the first authored stage before rewards can influence the dice stream.
            if (snapshot.Revision == 0) return new RerollDiceAction(9);
            if (snapshot.Revision <= 2) return PlayHandAction.Instance;
            if (snapshot.Phase == EncounterPhase.ChoosingModifier)
            {
                var encounter = catalog.GetEncounter(snapshot.EncounterId);
                var offer = snapshot.PendingReward;
                foreach (var option in offer.Options)
                {
                    var modifier = catalog.GetModifier(option.DefinitionId);
                    Assert.That(modifier.Availability.MatchesEncounter(encounter), Is.True, modifier.Id);
                    Assert.That(modifier.Availability.Access != ModifierAccess.PermanentUnlock ||
                        snapshot.UnlockedModifierIds.Contains(modifier.Id), Is.True, modifier.Id);
                    Assert.That(snapshot.Modifiers.Any(value => value.DefinitionId == modifier.Id), Is.False, modifier.Id);
                }
                Assert.That(offer.Options.Select(value => catalog.GetModifier(value.DefinitionId).FamilyId).Distinct().Count(),
                    Is.EqualTo(offer.Options.Count));
                if (offer.OfferId == 1 && snapshot.RewardRefreshes >= encounter.RewardPolicy.RefreshCost)
                    return new RefreshModifierOfferAction(offer.OfferId);
                var selected = offer.Options.OrderByDescending(value => catalog.GetModifier(value.DefinitionId).Availability.PowerTier)
                    .ThenBy(value => value.DefinitionId, StringComparer.Ordinal).First();
                return new SelectModifierAction(offer.OfferId, selected.OptionId);
            }
            if (snapshot.Phase == EncounterPhase.ChoosingHandUpgrade)
            {
                var offer = snapshot.PendingReward;
                var selected = offer.Options.OrderByDescending(value => value.Category.Value).First();
                return new SelectHandUpgradeAction(offer.OfferId, selected.OptionId);
            }
            Assert.That(snapshot.Phase, Is.EqualTo(EncounterPhase.Playing));
            if (snapshot.CanReroll && snapshot.RerollsSpentOnHand < 2)
            {
                int face = snapshot.CurrentDice.ToArray().GroupBy(value => value).OrderByDescending(value => value.Count())
                    .ThenByDescending(value => value.Key).First().Key;
                int mask = 0;
                for (int index = 0; index < DiceValues.Count; ++index)
                    if (snapshot.CurrentDice[index] != face) mask |= 1 << index;
                if (mask != 0) return new RerollDiceAction(mask);
            }
            return PlayHandAction.Instance;
        }

        private static EncounterAction Action(string[] values, EncounterSession session)
        {
            var snapshot = session.Snapshot;
            switch (values[0])
            {
                case "R": return new RerollDiceAction(int.Parse(values[1]));
                case "P":
                    Assert.That(snapshot.StageIndex, Is.EqualTo(int.Parse(values[1])));
                    Assert.That(session.PreviewPlayHand().HandScore, Is.EqualTo(long.Parse(values[2])));
                    return PlayHandAction.Instance;
                case "F":
                    Assert.That(snapshot.PendingReward.OfferId, Is.EqualTo(long.Parse(values[1])));
                    return new RefreshModifierOfferAction(long.Parse(values[1]));
                case "M":
                case "U":
                    long offerId = long.Parse(values[1]);
                    long optionId = long.Parse(values[2]);
                    Assert.That(snapshot.PendingReward.OfferId, Is.EqualTo(offerId));
                    Assert.That(snapshot.PendingReward.Options.Single(value => value.OptionId == optionId).DefinitionId,
                        Is.EqualTo(values[3]));
                    return values[0] == "M" ? (EncounterAction)new SelectModifierAction(offerId, optionId)
                        : new SelectHandUpgradeAction(offerId, optionId);
                default: throw new InvalidOperationException(values[0]);
            }
        }

        private static void AssertEventsEqual(IReadOnlyList<RuleEvent> expected, IReadOnlyList<RuleEvent> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int i = 0; i < expected.Count; ++i)
            {
                Assert.That(actual[i].GetType(), Is.EqualTo(expected[i].GetType()));
                foreach (var property in expected[i].GetType().GetProperties())
                {
                    var expectedValue = property.GetValue(expected[i]);
                    var actualValue = property.GetValue(actual[i]);
                    if (expectedValue is ScoreBreakdown expectedScore)
                        foreach (var scoreProperty in typeof(ScoreBreakdown).GetProperties())
                            Assert.That(scoreProperty.GetValue(actualValue), Is.EqualTo(scoreProperty.GetValue(expectedScore)), scoreProperty.Name);
                    else Assert.That(actualValue, Is.EqualTo(expectedValue), property.Name);
                }
            }
        }

        internal static MechanicalCatalog Catalog()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset");
            Assert.That(asset, Is.Not.Null);
            return asset.Build(TestContent.ModifierAssets());
        }

        internal static MechanicalCatalog LegacyCatalog()
        {
            var authored = Catalog();
            var encounter = authored.GetEncounter("encounter.standard");
            var policy = encounter.RewardPolicy;
            var legacyPolicy = new RewardPolicyDefinition(policy.Id, policy.ModifierLayouts, policy.StartingRefreshes,
                policy.RefreshesPerClear, policy.RefreshCost, policy.HandUpgradeOptionCount, policy.UpgradeHistory);
            var legacyEncounter = new EncounterDefinition(encounter.Id, encounter.Stages, encounter.StartingUpgrades,
                encounter.StartingModifiers, legacyPolicy, encounter.Rules);
            var modifiers = authored.Modifiers.Select(value => new ModifierDefinition(value.Id, value.FamilyId, value.Phase,
                value.Condition, value.Effects, value.ActivationLimit, value.RewardEligible));
            return new MechanicalCatalog("test.standard.legacy", authored.Hands, new[] { legacyEncounter }, modifiers, authored.RewardPools);
        }

        // Legal action trace for the legacy Standard mechanics fixture with seed 60.
        private const string VictoryActions = @"R,9
P,0,152
P,0,81
M,1,2,modifier.play_hand.contains_face.multiplier
U,2,7,hand.five_of_a_kind
R,14
R,14
R,14
P,1,84
P,1,72
P,1,450
M,3,10,modifier.play_hand.edges_match_interior.combined
U,4,14,hand.four_of_a_kind
R,21
P,2,198
P,2,222
F,5
M,6,19,modifier.play_hand.interior_pair_matches_edge.multiplier
U,7,23,hand.three_of_a_kind
R,19
P,3,288
R,25
P,3,329
M,8,26,modifier.player_reroll.selected_face_unchanged.rerolls
U,9,30,hand.full_house
R,25
R,25
R,25
P,4,470
P,4,96
F,10
F,11
M,12,39,modifier.play_hand.rank_equals_previous.multiplier
U,13,44,hand.five_of_a_kind
R,7
R,7
P,5,328
R,13
P,5,520
M,14,45,modifier.play_hand.edges_match_interior.base_score
U,15,51,hand.straight
R,22
R,22
P,6,1001
M,16,53,modifier.initial_roll.previous_stage_at_most_half_hands_played.multiplier
U,17,58,hand.four_of_a_kind
R,28
R,28
R,7
P,7,630
P,7,737
M,18,61,modifier.play_hand.interior_pair_matches_edge.rerolls
U,19,65,hand.three_of_a_kind
R,3
R,1
P,8,630
R,25
P,8,384
M,20,68,modifier.play_hand.rank_equals_previous.combined
U,21,72,hand.five_of_a_kind
R,28
P,9,870
R,28
R,28
R,7
P,9,517
M,22,74,modifier.play_hand.hands_remaining_even.multiplier
U,23,78,hand.full_house
R,19
R,19
R,19
R,19
P,10,624
R,14
R,8
P,10,1120
P,10,138
M,24,80,modifier.play_hand.sum_greater_than.base_score
U,25,85,hand.four_of_a_kind
R,25
R,25
P,11,814
R,7
P,11,1700
M,26,87,modifier.play_hand.no_extreme_faces.rerolls
U,27,92,hand.three_of_a_kind
P,12,950
R,22
R,22
R,6
P,12,704
R,25
P,12,624
M,28,95,modifier.initial_roll.last_hand.multiplier
U,29,100,hand.five_of_a_kind
R,7
R,5
P,13,976
R,27
R,22
R,20
R,16
R,16
P,13,1218
R,21
P,13,567
M,30,103,modifier.play_hand.adjacent_matching_triple.combined
U,31,106,hand.full_house
R,28
R,28
R,28
R,28
P,14,1156
R,7
P,14,1776
F,32
M,33,112,modifier.play_hand.sum_greater_than.multiplier
U,34,116,hand.four_of_a_kind
R,15
R,26
R,26
R,26
R,26
P,15,297
R,22
R,22
P,15,880
R,22
P,15,1581
P,15,1008
M,35,120,modifier.play_hand.rank_above_previous.rerolls
U,36,124,hand.five_of_a_kind
R,29
R,20
P,16,1860
R,22
R,20
R,20
R,20
P,16,2507
M,37,125,modifier.play_hand.adjacent_matching_triple.base_score
U,38,130,hand.three_of_a_kind
R,11
R,3
P,17,1580
R,30
R,13
R,9
R,9
P,17,990
R,19
P,17,1701
M,39,133,modifier.play_hand.no_extreme_faces.multiplier
U,40,138,hand.four_of_a_kind
R,17
P,18,1482
R,15
R,15
R,19
R,17
P,18,1513
R,28
R,28
R,19
R,19
P,18,4785
F,41
M,42,144,modifier.initial_roll.previous_stage_at_most_half_hands_played.combined
U,43,148,hand.five_of_a_kind
P,19,675
R,21
R,21
R,20
P,19,594
R,28
R,20
R,20
R,20
P,19,1060
R,21
R,14
R,14
P,19,2058
M,44,151,modifier.play_hand.same_parity.rerolls
U,45,154,hand.full_house
P,20,550
R,26
R,26
R,26
R,26
R,26
R,26
R,26
P,20,1216
R,28
R,12
P,20,1584
P,20,2398
M,46,158,modifier.player_reroll.selected_face_unchanged.combined
U,47,162,hand.four_of_a_kind
R,14
R,14
P,21,693
R,14
R,19
P,21,1914
R,24
R,8
R,8
P,21,2331
R,28
P,21,1976
F,48
F,49
F,50
M,51,172,modifier.play_hand.interior_pair_matches_edge.base_score
U,52,177,hand.three_of_a_kind
R,5
P,22,2320
R,22
R,4
R,4
P,22,4200
F,53
M,54,183,modifier.player_reroll.ordered.multiplier
U,55,186,hand.three_of_a_kind
P,23,1300
R,13
R,22
R,22
R,18
R,18
P,23,1501
R,21
R,21
R,21
R,21
R,17
R,17
P,23,1581
R,17
R,17
P,23,2834
M,56,189,modifier.play_hand.edge_pair_differences_equal.rerolls
U,57,195,hand.straight
R,9
P,24,1264
R,25
R,17
R,16
R,16
R,16
R,16
P,24,3042
R,7
R,26
R,26
R,5
R,4
R,4
P,24,2990
R,19
R,19
P,24,1870";
    }
}
