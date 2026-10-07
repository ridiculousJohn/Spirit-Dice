using System.Linq;
using Dice.Engine;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;

namespace Dice.Tests
{
    public sealed class RewardCatalogTests
    {
        [Test]
        public void ImportedStandardPolicyPreservesCadenceEconomyAndUpgradeHistory()
        {
            var catalog = Catalog();
            var policy = catalog.GetEncounter("encounter.standard").RewardPolicy;
            Assert.That(catalog.Version, Is.EqualTo("standard.rewards.v1"));
            Assert.That(policy, Is.Not.Null);
            Assert.That(policy.Id, Is.EqualTo("reward_policy.standard"));
            Assert.That(policy.StartingRefreshes, Is.EqualTo(1));
            Assert.That(policy.RefreshesPerClear, Is.EqualTo(1));
            Assert.That(policy.RefreshCost, Is.EqualTo(1));
            Assert.That(policy.HandUpgradeOptionCount, Is.EqualTo(4));
            Assert.That(policy.UpgradeHistory.Select(value => value.MaxIncomingStageNumber),
                Is.EqualTo(new[] { 16, 21, int.MaxValue }));
            Assert.That(policy.UpgradeHistory.Select(value => value.ExcludedRecentChoices),
                Is.EqualTo(new[] { 3, 2, 0 }));
            string[][] layouts =
            {
                new[] { "reward_pool.rerolls", "reward_pool.rerolls", "reward_pool.rerolls" },
                new[] { "reward_pool.base_score", "reward_pool.multiplier", "reward_pool.combined" },
                new[] { "reward_pool.base_score", "reward_pool.multiplier", "reward_pool.combined" },
                new[] { "reward_pool.base_score", "reward_pool.multiplier", "reward_pool.combined" }
            };
            Assert.That(policy.ModifierLayouts.Count, Is.EqualTo(layouts.Length));
            for (int i = 0; i < layouts.Length; ++i)
                Assert.That(policy.ModifierLayouts[i].PoolIds, Is.EqualTo(layouts[i]));
            Assert.That(catalog.RewardPools.Select(value => value.Id), Is.EquivalentTo(new[]
            {
                "reward_pool.base_score", "reward_pool.multiplier", "reward_pool.combined", "reward_pool.rerolls"
            }));
        }

        [TestCase("base_score", ModifierEffectKind.AddBaseScore)]
        [TestCase("multiplier", ModifierEffectKind.AddMultiplier)]
        [TestCase("combined", ModifierEffectKind.AddBaseScore, ModifierEffectKind.AddMultiplier)]
        [TestCase("rerolls", ModifierEffectKind.AddRerolls)]
        public void ImportedRewardPoolContainsOnlyItsMechanicalEffectsAndExplicitNoOpFallback(
            string role, params ModifierEffectKind[] expectedEffects)
        {
            var catalog = Catalog();
            var pool = catalog.GetRewardPool("reward_pool." + role);
            Assert.That(pool.ModifierIds.Count, Is.EqualTo(73));
            Assert.That(pool.ModifierIds.Distinct().Count(), Is.EqualTo(73));
            var members = pool.ModifierIds.Select(catalog.GetModifier).ToArray();
            Assert.That(members.Select(value => value.FamilyId).Distinct().Count(), Is.EqualTo(73));
            foreach (var member in members)
            {
                Assert.That(member.Id, Is.EqualTo(member.FamilyId + "." + role));
                Assert.That(member.FamilyId,
                    Does.Match(@"^modifier\.(initial_roll|player_reroll|play_hand)\.[a-z0-9_]+$"));
                Assert.That(member.RewardEligible, Is.True, member.Id);
                Assert.That(member.Effects.Select(value => value.Kind), Is.EquivalentTo(expectedEffects), member.Id);
                Assert.That(member.Effects.All(value => value.Amount > 0), Is.True, member.Id);
            }
            Assert.That(pool.FallbackModifierId, Is.EqualTo("modifier.play_hand.never." + role));
            Assert.That(pool.ModifierIds, Does.Not.Contain(pool.FallbackModifierId));
            var fallback = catalog.GetModifier(pool.FallbackModifierId);
            Assert.That(fallback.Condition, Is.TypeOf<NeverCondition>());
            Assert.That(fallback.RewardEligible, Is.False);
            Assert.That(fallback.Effects, Is.Empty);
        }

        private static MechanicalCatalog Catalog()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset");
            Assert.That(asset, Is.Not.Null);
            return asset.Build();
        }
    }
}
