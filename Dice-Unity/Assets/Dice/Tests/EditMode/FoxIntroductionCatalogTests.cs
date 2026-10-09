using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;

namespace Dice.Tests
{
    public sealed class FoxIntroductionCatalogTests
    {
        private const string EncounterId = "encounter.fox.introduction";
        private const string PoolPrefix = "reward_pool.fox.introduction.";
        private static readonly string[] Families =
        {
            "modifier.play_hand.contains_face",
            "modifier.play_hand.contains_upper_face",
            "modifier.play_hand.no_extreme_faces",
            "modifier.play_hand.sum_less_than",
            "modifier.play_hand.sum_greater_than",
            "modifier.play_hand.range_less_than",
            "modifier.play_hand.two_pair",
            "modifier.play_hand.high_die",
            "modifier.play_hand.middle_unique",
            "modifier.initial_roll.initial_pair",
            "modifier.player_reroll.selected_face_unchanged",
            "modifier.player_reroll.exactly_one_selected_face"
        };

        [Test]
        public void IntroductionStartsWithoutUpgradesOrModifiersAndUsesTenThreeHandStages()
        {
            var catalog = Catalog();
            var encounter = catalog.GetEncounter(EncounterId);
            Assert.That(encounter.StartingModifiers, Is.Empty);
            Assert.That(encounter.StartingUpgrades, Is.EqualTo(new int[HandDefinition.CategoryCount]));
            Assert.That(encounter.Stages.Select(value => value.Threshold),
                Is.EqualTo(new long[] { 120, 170, 230, 300, 380, 470, 570, 680, 800, 930 }));
            foreach (var stage in encounter.Stages)
            {
                Assert.That(stage.StartingHands, Is.EqualTo(3));
                Assert.That(stage.StartingRerolls, Is.EqualTo(3));
                Assert.That(stage.ExitBehavior, Is.EqualTo(StageExitBehavior.AwaitRewards));
            }
            Assert.That(catalog.Version, Is.EqualTo("standard.rewards.v1"));
            Assert.That(catalog.GetEncounter("encounter.standard").Stages.Count, Is.EqualTo(25));
            Assert.That(catalog.GetEncounter("encounter.standard").Rules, Is.Empty);
        }

        [Test]
        public void SerializedRuleAssetBuildsTheReusableFirstRerollRule()
        {
            var asset = AssetDatabase.LoadAssetAtPath<FirstRerollFreeRuleAsset>(
                "Assets/Dice/Content/Mechanical/FirstRerollFree.asset");
            Assert.That(asset, Is.Not.Null);
            var rule = asset.Build();
            Assert.That(rule, Is.TypeOf<FirstRerollFreeRuleDefinition>());
            Assert.That(rule.Id, Is.EqualTo("rule.first_reroll_free"));
            var encounter = Catalog().GetEncounter(EncounterId);
            Assert.That(encounter.Rules.Count, Is.EqualTo(1));
            Assert.That(encounter.Rules[0], Is.TypeOf<FirstRerollFreeRuleDefinition>());
            Assert.That(encounter.Rules[0].Id, Is.EqualTo(rule.Id));
        }

        [TestCase("base_score", ModifierEffectKind.AddBaseScore)]
        [TestCase("multiplier", ModifierEffectKind.AddMultiplier)]
        [TestCase("combined", ModifierEffectKind.AddBaseScore, ModifierEffectKind.AddMultiplier)]
        [TestCase("rerolls", ModifierEffectKind.AddRerolls)]
        public void StarterPoolIncludesAllTwelveExactDefinitionsForItsRoleWithoutFallback(
            string role, params ModifierEffectKind[] effects)
        {
            var catalog = Catalog();
            var pool = catalog.GetRewardPool(PoolPrefix + role);
            Assert.That(pool.FallbackModifierId, Is.Null);
            Assert.That(pool.ModifierIds.Where(id => catalog.GetModifier(id).Availability.Access != ModifierAccess.PermanentUnlock),
                Is.EquivalentTo(Families.Select(value => value + "." + role)));
            Assert.That(pool.ModifierIds.Count, Is.EqualTo(15));
            foreach (var member in pool.ModifierIds.Select(catalog.GetModifier))
            {
                Assert.That(member.RewardEligible, Is.True, member.Id);
                Assert.That(member.Effects.Select(value => value.Kind), Is.EquivalentTo(effects), member.Id);
                Assert.That(member.Effects.All(value => value.Amount > 0), Is.True, member.Id);
                Assert.That(member.ActivationLimit, Is.EqualTo(ModifierActivationLimit.OncePerHand), member.Id);
            }
        }

        [Test]
        public void NineOffersProvideScoringChoicesAndRerollsAfterStagesThreeSixAndNine()
        {
            var catalog = Catalog();
            var policy = catalog.GetEncounter(EncounterId).RewardPolicy;
            Assert.That(policy.Id, Is.EqualTo("reward_policy.fox.introduction"));
            Assert.That(policy.StartingRefreshes, Is.EqualTo(1));
            Assert.That(policy.RefreshesPerClear, Is.EqualTo(1));
            Assert.That(policy.RefreshCost, Is.EqualTo(1));
            Assert.That(policy.HandUpgradeOptionCount, Is.EqualTo(4));
            Assert.That(policy.UpgradeHistory.Count, Is.EqualTo(1));
            Assert.That(policy.UpgradeHistory[0].MaxIncomingStageNumber, Is.EqualTo(int.MaxValue));
            Assert.That(policy.UpgradeHistory[0].ExcludedRecentChoices, Is.EqualTo(1));
            var random = new DeterministicRandom(17);
            for (int clearedStageNumber = 1; clearedStageNumber <= 9; ++clearedStageNumber)
            {
                var generated = RewardGenerator.GenerateModifiers(catalog, policy, clearedStageNumber,
                    Array.Empty<string>(), Array.Empty<string>(), ref random, catalog.GetEncounter(EncounterId), Array.Empty<string>());
                string thirdRole = clearedStageNumber % 3 == 0 ? "rerolls" : "combined";
                AssertOffer(catalog, generated, Array.Empty<string>(), new[] { "base_score", "multiplier", thirdRole });
            }
        }

        [TestCase("base_score")]
        [TestCase("multiplier")]
        [TestCase("combined")]
        [TestCase("rerolls")]
        public void NinthOfferRetainsThreeFunctionalChoicesWithConcentratedOwnershipAndRefreshHistory(string ownedRole)
        {
            var catalog = Catalog();
            var policy = catalog.GetEncounter(EncounterId).RewardPolicy;
            string[] owned = Families.Take(8).Select(value => value + "." + ownedRole).ToArray();
            for (ulong seed = 0; seed < 32; ++seed)
            {
                var random = new DeterministicRandom(seed);
                IEnumerable<string> history = Array.Empty<string>();
                for (int refresh = 0; refresh < 64; ++refresh)
                {
                    var generated = RewardGenerator.GenerateModifiers(catalog, policy, 9, owned, history, ref random,
                        catalog.GetEncounter(EncounterId), Array.Empty<string>());
                    AssertOffer(catalog, generated, owned, new[] { "base_score", "multiplier", "rerolls" });
                    history = generated.ExcludedFamilyIds;
                }
            }
        }

        [Test]
        public void UpgradeOffersExcludeOnlyTheImmediatelyPreviousCategory()
        {
            var catalog = Catalog();
            var policy = catalog.GetEncounter(EncounterId).RewardPolicy;
            var recent = new[] { HandCategory.Pair, HandCategory.TwoPair, HandCategory.FullHouse };
            var seen = new HashSet<HandCategory>();
            for (ulong seed = 0; seed < 64; ++seed)
            {
                var random = new DeterministicRandom(seed);
                var generated = RewardGenerator.GenerateHandUpgrades(catalog, policy, 10,
                    new int[HandDefinition.CategoryCount], recent, ref random);
                Assert.That(generated.Count, Is.EqualTo(4));
                Assert.That(generated.Distinct().Count(), Is.EqualTo(4));
                Assert.That(generated, Has.No.Member(HandCategory.Pair));
                seen.UnionWith(generated);
            }
            Assert.That(seen, Is.EquivalentTo(Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>()
                .Except(new[] { HandCategory.Pair })));
        }

        [TestCase(0UL)]
        public void IntroductionCanCompleteTenStagesWithSaveRestoreAfterEveryLegalAction(ulong seed)
        {
            var catalog = Catalog();
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, seed);
            var uninterrupted = EncounterSession.CreateEncounter(catalog, EncounterId, seed);
            var cleared = new List<int>();
            var trace = new List<string>();
            int modifiers = 0, upgrades = 0;
            for (int actionIndex = 0; actionIndex < 256 && session.Snapshot.Phase != EncounterPhase.Completed; ++actionIndex)
            {
                var action = SmokeAction(catalog, session);
                var before = session.Snapshot;
                var result = session.Execute(action, before.Revision);
                var control = uninterrupted.Execute(action, uninterrupted.Snapshot.Revision);
                Assert.That(result.Accepted, Is.True, "seed " + seed + ", action " + actionIndex);
                EncounterSessionTests.AssertSnapshotsEqual(control.Snapshot, result.Snapshot);
                cleared.AddRange(result.Events.OfType<StageClearedEvent>().Select(value => value.StageIndex));
                modifiers += result.Events.OfType<ModifierAcquiredEvent>().Count();
                upgrades += result.Events.OfType<HandUpgradedEvent>().Count();
                trace.Add(SmokeTrace(action, before));
                var save = session.CaptureSave();
                var restored = EncounterSession.RestoreEncounter(catalog, save);
                Assert.That(restored.Success, Is.True,
                    "seed " + seed + ", action " + actionIndex + ": " + restored.Detail);
                EncounterPersistenceTests.AssertGraphEqual(save, restored.Session.CaptureSave());
                EncounterSessionTests.AssertSnapshotsEqual(result.Snapshot, restored.Session.Snapshot);
                session = restored.Session;
            }
            Assert.That(session.Snapshot.Phase, Is.EqualTo(EncounterPhase.Completed), "seed " + seed);
            Assert.That(session.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(cleared, Is.EqualTo(Enumerable.Range(0, 10)));
            Assert.That(modifiers, Is.EqualTo(9));
            Assert.That(upgrades, Is.EqualTo(9));
            Assert.That(session.Snapshot.StageIndex, Is.EqualTo(9));
            Assert.That(session.Snapshot.PendingReward, Is.Null);
            Assert.That(session.CaptureSave().TotalFreeRerollsUsed, Is.GreaterThan(0));
            TestContext.WriteLine("Fox introduction winning seed: " + seed);
            TestContext.WriteLine(string.Join("\n", trace));
        }

        private static EncounterAction SmokeAction(MechanicalCatalog catalog, EncounterSession session)
        {
            var snapshot = session.Snapshot;
            if (snapshot.Phase == EncounterPhase.Playing)
            {
                var score = session.PreviewPlayHand();
                if (score.HandScore >= snapshot.RemainingThreshold || (int)score.Category >= (int)HandCategory.FullHouse)
                    return PlayHandAction.Instance;
                if (!snapshot.CanReroll || snapshot.RerollsSpentOnHand >= (snapshot.HandsRemaining == 1 ? 6 : 2))
                    return PlayHandAction.Instance;
                var dice = snapshot.CurrentDice;
                var counts = new int[7];
                for (int i = 0; i < DiceValues.Count; ++i) ++counts[dice[i]];
                int keptFace = Enumerable.Range(1, 6).OrderByDescending(face => counts[face]).ThenByDescending(face => face).First();
                int mask = 0;
                for (int i = 0; i < DiceValues.Count; ++i)
                    if (score.Category == HandCategory.TwoPair ? counts[dice[i]] == 1 : dice[i] != keptFace) mask |= 1 << i;
                return new RerollDiceAction(mask);
            }
            var offer = snapshot.PendingReward;
            if (snapshot.Phase == EncounterPhase.ChoosingModifier)
            {
                var selected = offer.Options.OrderByDescending(option =>
                    catalog.GetModifier(option.DefinitionId).Effects.Sum(effect => effect.Kind == ModifierEffectKind.AddMultiplier
                        ? effect.Amount * 20 : effect.Kind == ModifierEffectKind.AddBaseScore ? effect.Amount : 0)).First();
                return new SelectModifierAction(offer.OfferId, selected.OptionId);
            }
            var categoryOrder = new[] { HandCategory.FourOfAKind, HandCategory.FullHouse, HandCategory.ThreeOfAKind,
                HandCategory.FiveOfAKind, HandCategory.TwoPair, HandCategory.Pair, HandCategory.Straight, HandCategory.HighDie };
            var upgrade = offer.Options.OrderBy(option => Array.IndexOf(categoryOrder, option.Category.Value)).First();
            return new SelectHandUpgradeAction(offer.OfferId, upgrade.OptionId);
        }

        private static string SmokeTrace(EncounterAction action, EncounterSnapshot before)
        {
            if (action is RerollDiceAction reroll) return "R," + reroll.SelectedMask;
            if (action is PlayHandAction) return "P," + before.StageIndex;
            long optionId = action is SelectModifierAction modifier ? modifier.OptionId : ((SelectHandUpgradeAction)action).OptionId;
            var selected = before.PendingReward.Options.Single(option => option.OptionId == optionId);
            return (action is SelectModifierAction ? "M," : "U,") + before.PendingReward.OfferId + "," + optionId
                + "," + selected.DefinitionId;
        }

        private static void AssertOffer(MechanicalCatalog catalog, ModifierRewardGeneration generated,
            IEnumerable<string> owned, string[] roles)
        {
            Assert.That(generated.ModifierIds.Count, Is.EqualTo(3));
            Assert.That(generated.ModifierIds.Intersect(owned), Is.Empty);
            var definitions = generated.ModifierIds.Select(catalog.GetModifier).ToArray();
            Assert.That(definitions.Select(value => value.FamilyId).Distinct().Count(), Is.EqualTo(3));
            for (int i = 0; i < roles.Length; ++i)
            {
                Assert.That(definitions[i].Id, Is.EqualTo(definitions[i].FamilyId + "." + roles[i]));
                Assert.That(Families, Does.Contain(definitions[i].FamilyId));
                Assert.That(definitions[i].Effects, Is.Not.Empty);
            }
        }

        private static MechanicalCatalog Catalog()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset");
            Assert.That(asset, Is.Not.Null);
            return asset.Build(TestContent.ModifierAssets());
        }
    }
}
