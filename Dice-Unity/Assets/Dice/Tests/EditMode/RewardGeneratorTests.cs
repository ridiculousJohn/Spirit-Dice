using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class RewardGeneratorTests
    {
        [Test]
        public void ModifierLayoutCadenceUsesTheIncomingStageIndex()
        {
            var modifiers = new[] { RewardTestContent.Modifier("first", "family.first"), RewardTestContent.Modifier("second", "family.second") };
            var policy = RewardTestContent.Policy(new[]
            {
                new ModifierRewardLayout(new[] { new ModifierRewardPoolDefinition("pool.first", new[] { "first" }) }),
                new ModifierRewardLayout(new[] { new ModifierRewardPoolDefinition("pool.second", new[] { "second" }) })
            });
            var catalog = RewardTestContent.Catalog(modifiers, policy);
            var random = new DeterministicRandom(41);
            for (int incomingStageIndex = 1; incomingStageIndex <= 8; ++incomingStageIndex)
            {
                var generated = RewardGenerator.GenerateModifiers(catalog, policy, incomingStageIndex,
                    Array.Empty<string>(), Array.Empty<ModifierFamilyDefinition.Id>(), ref random);
                Assert.That(generated.ModifierIds, Is.EqualTo(new[] { incomingStageIndex % 2 == 0 ? "first" : "second" }));
            }
        }

        [Test]
        public void ModifierDrawsExcludeOwnedDefinitionsAndKeepFamiliesUniqueWithinAnOffer()
        {
            var modifiers = new[]
            {
                RewardTestContent.Modifier("first.base", "family.first"), RewardTestContent.Modifier("first.mult", "family.first"),
                RewardTestContent.Modifier("second", "family.second"), RewardTestContent.Modifier("third", "family.third")
            };
            var policy = RewardTestContent.Policy(RewardTestContent.ThreeSlots(RewardTestContent.Pool(modifiers)));
            var catalog = RewardTestContent.Catalog(modifiers, policy);
            for (ulong seed = 0; seed < 30; ++seed)
            {
                var random = new DeterministicRandom(seed);
                var generated = RewardGenerator.GenerateModifiers(catalog, policy, 1,
                    new[] { "first.base" }, Array.Empty<ModifierFamilyDefinition.Id>(), ref random);
                Assert.That(generated.ModifierIds.Count, Is.EqualTo(3));
                Assert.That(generated.ModifierIds, Does.Not.Contain("first.base"));
                Assert.That(generated.ModifierIds, Does.Contain("first.mult"));
                Assert.That(generated.ModifierIds.Select(value => catalog.GetModifier(value).Family).Distinct().Count(), Is.EqualTo(3));
            }
        }

        [Test]
        public void FamilyHistoryIsRetainedWithThreeCandidatesAndResetWithTwoCandidates()
        {
            var modifiers = Enumerable.Range(0, 5).Select(i => RewardTestContent.Modifier("modifier." + i, "family." + i)).ToArray();
            var policy = RewardTestContent.Policy(new[] { new ModifierRewardLayout(new[] { RewardTestContent.Pool(modifiers) }) });
            var catalog = RewardTestContent.Catalog(modifiers, policy);
            var random = new DeterministicRandom(7);
            var retained = RewardGenerator.GenerateModifiers(catalog, policy, 1, Array.Empty<string>(),
                new[] { TestFamilies.IdOf("family.0"), TestFamilies.IdOf("family.1") }, ref random);
            Assert.That(retained.ModifierIds.Count, Is.EqualTo(1));
            Assert.That(retained.ModifierIds, Does.Not.Contain("modifier.0").And.Not.Contain("modifier.1"));
            Assert.That(retained.ExcludedFamilyIds, Has.Member(TestFamilies.IdOf("family.0")).And.Member(TestFamilies.IdOf("family.1")));

            var reset = RewardGenerator.GenerateModifiers(catalog, policy, 1, Array.Empty<string>(),
                new[] { "family.0", "family.1", "family.2" }.Select(TestFamilies.IdOf), ref random);
            Assert.That(reset.ModifierIds.Count, Is.EqualTo(1));
            Assert.That(reset.ExcludedFamilyIds, Is.EquivalentTo(new[] { "family.0", "family.1", "family.2" }.Select(TestFamilies.IdOf)
                .Concat(reset.ModifierIds.Select(value => catalog.GetModifier(value).Family.Identity)).Distinct()));
        }

        [Test]
        public void IgnoringHistoryForOneSmallPoolDoesNotRemoveExclusionsFromTheNextPool()
        {
            var modifiers = Enumerable.Range(0, 8).Select(i => RewardTestContent.Modifier("modifier." + i, "family." + i)).ToArray();
            var policy = RewardTestContent.Policy(new[] { new ModifierRewardLayout(new[]
            {
                new ModifierRewardPoolDefinition("small", modifiers.Take(4).Select(value => value.Id)),
                new ModifierRewardPoolDefinition("large", modifiers.Select(value => value.Id))
            }) });
            var catalog = RewardTestContent.Catalog(modifiers, policy);
            var history = new[] { "family.0", "family.1", "family.2" }.Select(TestFamilies.IdOf).ToArray();
            for (ulong seed = 0; seed < 30; ++seed)
            {
                var random = new DeterministicRandom(seed);
                var offer = RewardGenerator.GenerateModifiers(catalog, policy, 1, Array.Empty<string>(), history, ref random);
                Assert.That(offer.ModifierIds.Count, Is.EqualTo(2));
                Assert.That(history, Is.SubsetOf(offer.ExcludedFamilyIds));
                Assert.That(history, Has.No.Member(catalog.GetModifier(offer.ModifierIds[1]).Family.Identity));
                Assert.That(offer.ModifierIds.Select(value => catalog.GetModifier(value).Family).Distinct().Count(), Is.EqualTo(2));
            }
        }

        [Test]
        public void ExhaustedPoolCanSkipARewardSlotAndExplicitFallbackRemainsOutsideOrdinaryEligibility()
        {
            var normal = RewardTestContent.Modifier("normal", "family.normal");
            var fallback = new ModifierDefinition("fallback", TestFamilies.Of("family.fallback"), ModifierPhase.PlayHand,
                new NeverCondition(), Array.Empty<ModifierEffect>(), rewardEligible: false);
            var ordinaryPolicy = RewardTestContent.Policy(new[]
                { new ModifierRewardLayout(new[] { new ModifierRewardPoolDefinition("pool", new[] { normal.Id }) }) });
            var ordinary = RewardTestContent.Catalog(new[] { normal, fallback }, ordinaryPolicy);
            var random = new DeterministicRandom(12);
            var skipped = RewardGenerator.GenerateModifiers(ordinary, ordinaryPolicy, 1, new[] { normal.Id }, Array.Empty<ModifierFamilyDefinition.Id>(), ref random);
            Assert.That(skipped.ModifierIds, Is.Empty);

            var fallbackPolicy = RewardTestContent.Policy(new[]
                { new ModifierRewardLayout(new[] { new ModifierRewardPoolDefinition("pool", new[] { normal.Id }, fallback.Id) }) });
            var withFallback = RewardTestContent.Catalog(new[] { normal, fallback }, fallbackPolicy);
            var generated = RewardGenerator.GenerateModifiers(withFallback, fallbackPolicy, 1, new[] { normal.Id }, Array.Empty<ModifierFamilyDefinition.Id>(), ref random);
            Assert.That(generated.ModifierIds, Is.EqualTo(new[] { fallback.Id }));
        }

        [Test]
        public void IdenticalGeneratorInputsReproduceOffersHistoryAndRandomState()
        {
            var catalog = RewardTestContent.Catalog();
            var policy = catalog.GetEncounter(TestContent.EncounterId).RewardPolicy;
            var first = new DeterministicRandom(991);
            var second = new DeterministicRandom(991);
            for (int i = 1; i < 12; ++i)
            {
                var a = RewardGenerator.GenerateModifiers(catalog, policy, i, Array.Empty<string>(), Array.Empty<ModifierFamilyDefinition.Id>(), ref first);
                var b = RewardGenerator.GenerateModifiers(catalog, policy, i, Array.Empty<string>(), Array.Empty<ModifierFamilyDefinition.Id>(), ref second);
                Assert.That(a.ModifierIds, Is.EqualTo(b.ModifierIds));
                Assert.That(a.ExcludedFamilyIds, Is.EqualTo(b.ExcludedFamilyIds));
                Assert.That(first.State, Is.EqualTo(second.State));
            }
        }

        [TestCase(16, 3)]
        [TestCase(17, 2)]
        [TestCase(21, 2)]
        [TestCase(22, 0)]
        public void UpgradeHistoryUsesTheConfiguredIncomingStageBoundaries(int incomingStageNumber, int excludedCount)
        {
            var catalog = RewardTestContent.Catalog();
            var policy = catalog.GetEncounter(TestContent.EncounterId).RewardPolicy;
            var recent = new[] { HandCategory.ThreeOfAKind, HandCategory.TwoPair, HandCategory.Pair, HandCategory.HighDie };
            var excluded = recent.Take(excludedCount).ToArray();
            var seen = new HashSet<HandCategory>();
            for (ulong seed = 0; seed < 100; ++seed)
            {
                var random = new DeterministicRandom(seed);
                var options = RewardGenerator.GenerateHandUpgrades(catalog, policy, incomingStageNumber,
                    new int[HandDefinition.CategoryCount], recent, ref random);
                Assert.That(options.Count, Is.EqualTo(4));
                Assert.That(options.Distinct().Count(), Is.EqualTo(4));
                Assert.That(options.Intersect(excluded), Is.Empty);
                seen.UnionWith(options);
            }
            Assert.That(seen, Is.EquivalentTo(Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>().Except(excluded)));
        }

        [Test]
        public void UpgradeHistoryFallbackRestoresCandidatesWithoutOfferingCappedCategories()
        {
            var catalog = RewardTestContent.Catalog();
            var policy = catalog.GetEncounter(TestContent.EncounterId).RewardPolicy;
            int[] upgrades = { 0, 0, 0, 0, 6, 6, 6, 6 };
            var random = new DeterministicRandom(9);
            var options = RewardGenerator.GenerateHandUpgrades(catalog, policy, 10, upgrades,
                new[] { HandCategory.ThreeOfAKind, HandCategory.TwoPair, HandCategory.Pair }, ref random);
            Assert.That(options, Is.EquivalentTo(new[] { HandCategory.HighDie, HandCategory.Pair, HandCategory.TwoPair, HandCategory.ThreeOfAKind }));
        }

        [Test]
        public void UpgradeOfferShrinksToTheUncappedPoolAndCanBecomeEmpty()
        {
            var catalog = RewardTestContent.Catalog();
            var policy = catalog.GetEncounter(TestContent.EncounterId).RewardPolicy;
            int[] upgrades = { 6, 6, 6, 6, 6, 6, 5, 5 };
            var random = new DeterministicRandom(9);
            var options = RewardGenerator.GenerateHandUpgrades(catalog, policy, 10, upgrades,
                new[] { HandCategory.Straight, HandCategory.FiveOfAKind }, ref random);
            Assert.That(options, Is.EquivalentTo(new[] { HandCategory.Straight, HandCategory.FiveOfAKind }));
            Assert.That(RewardGenerator.GenerateHandUpgrades(catalog, policy, 10,
                Enumerable.Repeat(6, HandDefinition.CategoryCount).ToArray(), Array.Empty<HandCategory>(), ref random), Is.Empty);
        }
    }

    internal static class RewardTestContent
    {
        internal static ModifierDefinition Modifier(string id, string family)
            => new ModifierDefinition(id, TestFamilies.Of(family), ModifierPhase.PlayHand, new AlwaysCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(10) });

        /** The pool of the reward-eligible charms. */
        internal static ModifierRewardPoolDefinition Pool(IEnumerable<ModifierDefinition> modifiers)
            => new ModifierRewardPoolDefinition("pool", modifiers.Where(value => value.RewardEligible).Select(value => value.Id));

        /** One offer of three slots, each drawing from the pool. */
        internal static ModifierRewardLayout[] ThreeSlots(ModifierRewardPoolDefinition pool)
            => new[] { new ModifierRewardLayout(new[] { pool, pool, pool }) };

        /** A policy of the layouts; without them, three slots drawing from the pool of the default charms. */
        internal static RewardPolicyDefinition Policy(IEnumerable<ModifierRewardLayout> layouts = null,
            int startingRefreshes = 1, int refreshesPerClear = 1, int refreshCost = 1)
            => new RewardPolicyDefinition("policy.test", layouts ?? ThreeSlots(Pool(Modifiers())),
                startingRefreshes, refreshesPerClear, refreshCost, 4, new[]
                {
                    new HandUpgradeHistoryRule(16, 3), new HandUpgradeHistoryRule(21, 2), new HandUpgradeHistoryRule(int.MaxValue, 0)
                });

        /** A catalog of the charms, the default ones without any; without a policy, its three slots draw from the pool of the charms. */
        internal static MechanicalCatalog Catalog(IEnumerable<ModifierDefinition> modifiers = null,
            RewardPolicyDefinition policy = null, IEnumerable<StageDefinition> stages = null, IEnumerable<int> upgrades = null,
            IEnumerable<string> inventory = null)
        {
            var modifierList = (modifiers ?? Modifiers()).ToArray();
            var encounter = new EncounterDefinition(TestContent.EncounterId, stages ?? new[]
            {
                new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards),
                new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards),
                new StageDefinition(1000000, 3, 3, StageExitBehavior.AwaitRewards)
            }, upgrades, inventory, policy ?? Policy(ThreeSlots(Pool(modifierList))));
            return new MechanicalCatalog("test.rewards", TestContent.Hands(), new[] { encounter }, modifierList);
        }

        private static ModifierDefinition[] Modifiers()
            => new[] { Modifier("first", "family.first"), Modifier("second", "family.second"), Modifier("third", "family.third") };

        internal static EncounterSession Session(MechanicalCatalog catalog = null, ulong seed = 0)
            => EncounterSession.CreateEncounter(catalog ?? Catalog(), TestContent.EncounterId, seed);
    }
}
