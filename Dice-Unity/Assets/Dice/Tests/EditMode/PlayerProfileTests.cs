using System;
using System.IO;
using System.Text;
using Dice.App;
using Dice.Engine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class PlayerProfileTests
    {
        [Test]
        public void DiscoveryIsPersistedMergedAndIndependentOfIndividualOwnership()
        {
            var profile = new PlayerProfile();
            Assert.That(profile.DiscoverFamily(TestFamilies.IdOf("family.future")), Is.True);
            Assert.That(profile.DiscoverFamily(TestFamilies.IdOf("family.future")), Is.False);
            Assert.That(profile.UnlockedModifierIds, Is.Empty);
            var restored = PlayerProfileCodec.Deserialize(PlayerProfileCodec.Serialize(profile));
            Assert.That(restored.DiscoveredFamilyIds, Is.EqualTo(new[] { TestFamilies.IdOf("family.future") }));
            Assert.That(restored.Merge(profile), Is.False);
            var merged = new PlayerProfile();
            Assert.That(merged.Merge(restored), Is.True);
            Assert.That(merged.DiscoveredFamilyIds, Is.EqualTo(restored.DiscoveredFamilyIds));
        }

        [Test]
        public void UndiscoveredProfileRetainsUnknownIdsAndInfersStarterAndOwnedFamilies()
        {
            var profile = new PlayerProfile();
            profile.UnlockModifier("owned");
            profile.UnlockModifier("future");
            profile.CompleteEncounter("encounter.future");
            Assert.That(profile.DiscoveredFamilyIds, Is.Empty);
            Assert.That(profile.GetDiscoveredFamilyIds(RewardCatalog()), Is.EquivalentTo(new[] { TestFamilies.IdOf("family.starter"), TestFamilies.IdOf("family.owned") }));
            var restored = PlayerProfileCodec.Deserialize(PlayerProfileCodec.Serialize(profile));
            Assert.That(restored.DiscoveredFamilyIds, Is.Empty);
            Assert.That(restored.UnlockedModifierIds, Is.EqualTo(new[] { "owned", "future" }));
            Assert.That(restored.CompletedEncounterIds, Is.EqualTo(new[] { "encounter.future" }));
            Assert.That(restored.GetDiscoveredFamilyIds(RewardCatalog()), Is.EqualTo(profile.GetDiscoveredFamilyIds(RewardCatalog())));
        }

        [Test]
        public void StoryCompletionIsPersistedMergedAndIndependentOfEncounters()
        {
            var profile = new PlayerProfile();
            Assert.That(profile.CompleteStory("story.future"), Is.True);
            Assert.That(profile.CompleteStory("story.future"), Is.False);
            Assert.That(profile.CompletedEncounterIds, Is.Empty);
            Assert.That(profile.VisitedEncounterIds, Is.Empty);
            var restored = PlayerProfileCodec.Deserialize(PlayerProfileCodec.Serialize(profile));
            Assert.That(restored.CompletedStoryIds, Is.EqualTo(new[] { "story.future" }));
            var merged = new PlayerProfile();
            Assert.That(merged.Merge(restored), Is.True);
            Assert.That(merged.CompletedStoryIds, Is.EqualTo(restored.CompletedStoryIds));
            Assert.That(merged.Merge(restored), Is.False);
            string path = ArtifactPath();
            PlayerProfileFileStore.Save(path, restored);
            var additional = new PlayerProfile();
            additional.CompleteStory("story.another");
            PlayerProfileFileStore.Save(path, additional);
            Assert.That(PlayerProfileFileStore.Load(path).CompletedStoryIds,
                Is.EquivalentTo(new[] { "story.future", "story.another" }));
        }

        [TestCase("missing")]
        [TestCase("null")]
        [TestCase("number")]
        [TestCase("duplicate")]
        [TestCase("blank")]
        [TestCase("control")]
        public void InvalidStoryCompletionFieldsAreRejected(string invalid)
        {
            var document = JObject.Parse(Encoding.UTF8.GetString(PlayerProfileCodec.Serialize(new PlayerProfile())));
            if (invalid == "missing") document.Remove("CompletedStoryIds");
            if (invalid == "null") document["CompletedStoryIds"] = JValue.CreateNull();
            if (invalid == "number") document["CompletedStoryIds"] = new JArray(42);
            if (invalid == "duplicate") document["CompletedStoryIds"] = new JArray("story.a", "story.a");
            if (invalid == "blank") document["CompletedStoryIds"] = new JArray(" ");
            if (invalid == "control") document["CompletedStoryIds"] = new JArray("story.\ninvalid");
            Assert.Throws<FormatException>(() => PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes(document.ToString())));
        }

        [Test]
        public void RewardsDiscoverAndGrantExplicitIndividualsIdempotently()
        {
            var catalog = RewardCatalog();
            var profile = new PlayerProfile();
            var discovered = new[] { TestFamilies.IdOf("family.revealed") };
            var granted = new[] { "owned" };
            var reward = new ProgressionRewardDefinition(discovered, granted);
            discovered[0] = TestFamilies.IdOf("unknown");
            granted[0] = "unknown";
            Assert.That(profile.ApplyReward(reward, catalog), Is.True);
            Assert.That(profile.UnlockedModifierIds, Is.EqualTo(new[] { "owned" }));
            Assert.That(profile.DiscoveredFamilyIds, Is.EquivalentTo(new[] { TestFamilies.IdOf("family.revealed"), TestFamilies.IdOf("family.owned") }));
            Assert.That(profile.GetDiscoveredFamilyIds(catalog), Is.EquivalentTo(new[] { TestFamilies.IdOf("family.revealed"), TestFamilies.IdOf("family.owned"), TestFamilies.IdOf("family.starter") }));
            Assert.That(profile.ApplyReward(reward, catalog), Is.False);
            var restored = PlayerProfileCodec.Deserialize(PlayerProfileCodec.Serialize(profile));
            Assert.That(restored.ApplyReward(reward, catalog), Is.False);
            Assert.That(restored.UnlockedModifierIds, Does.Not.Contain("owned.variant"));
        }

        [TestCase("family")]
        [TestCase("modifier")]
        [TestCase("encounter")]
        [TestCase("inert")]
        [TestCase("never")]
        [TestCase("starter")]
        public void InvalidRewardRejectsAllProgressBeforeMutation(string invalid)
        {
            var profile = new PlayerProfile();
            ModifierFamilyDefinition.Id[] discoveries = { TestFamilies.IdOf(invalid == "family" ? "unknown.family" : "family.revealed") };
            string[] grants = { "owned", invalid == "family" ? "owned.variant" : invalid == "modifier" ? "unknown" : invalid };
            var reward = new ProgressionRewardDefinition(discoveries, grants);
            Assert.Throws<ArgumentException>(() => profile.ApplyReward(reward, RewardCatalog()));
            Assert.That(profile.DiscoveredFamilyIds, Is.Empty);
            Assert.That(profile.UnlockedModifierIds, Is.Empty);
        }

        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("invalid")]
        public void InvalidDiscoveryProfileFieldsAreRejected(string invalid)
        {
            var document = JObject.Parse(Encoding.UTF8.GetString(PlayerProfileCodec.Serialize(new PlayerProfile())));
            if (invalid == "missing") document.Remove("DiscoveredFamilyIds");
            string family = TestFamilies.IdOf("family").ToString();
            if (invalid == "duplicate") document["DiscoveredFamilyIds"] = new JArray(family, family);
            if (invalid == "invalid") document["DiscoveredFamilyIds"] = new JArray("family\ninvalid");
            Assert.Throws<FormatException>(() => PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes(document.ToString())));
        }

        private static MechanicalCatalog RewardCatalog()
        {
            var modifiers = new[]
            {
                RewardModifier("starter", "family.starter", ModifierAccess.Starter),
                RewardModifier("owned", "family.owned"), RewardModifier("owned.variant", "family.owned"),
                RewardModifier("revealed", "family.revealed"),
                RewardModifier("encounter", "family.encounter", scope: ModifierAvailabilityScope.Encounter),
                RewardModifier("inert", "family.inert", inert: true), RewardModifier("never", "family.never", never: true)
            };
            return new MechanicalCatalog("profile.rewards", TestContent.Hands(), new[]
            {
                new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) })
            }, modifiers);
        }

        private static ModifierDefinition RewardModifier(string id, string family, ModifierAccess access = ModifierAccess.PermanentUnlock,
            ModifierAvailabilityScope scope = ModifierAvailabilityScope.Global, bool inert = false, bool never = false)
            => new ModifierDefinition(id, TestFamilies.Of(family), ModifierPhase.PlayHand,
                never ? new NeverCondition() : (IModifierCondition)new AlwaysCondition(),
                inert ? Array.Empty<ModifierEffect>() : new ModifierEffect[] { new AddBaseScoreEffect(1) },
                availability: new ModifierAvailabilityDefinition(scope: scope,
                    scopeId: scope == ModifierAvailabilityScope.Encounter ? TestContent.EncounterId : null, access: access));

        [Test]
        public void RoundTripKeepsUnknownIdsAndProgressIsIdempotent()
        {
            var profile = new PlayerProfile();
            Assert.That(profile.UnlockModifier("modifier.future.unknown"), Is.True);
            Assert.That(profile.UnlockModifier("modifier.future.unknown"), Is.False);
            Assert.That(profile.CompleteEncounter("encounter.future"), Is.True);
            Assert.That(profile.CompleteEncounter("encounter.future"), Is.False);
            var restored = PlayerProfileCodec.Deserialize(PlayerProfileCodec.Serialize(profile));
            Assert.That(restored.UnlockedModifierIds, Is.EqualTo(profile.UnlockedModifierIds));
            Assert.That(restored.CompletedEncounterIds, Is.EqualTo(profile.CompletedEncounterIds));
            Assert.That(restored.Merge(profile), Is.False);
        }

        [TestCase("missing")]
        [TestCase("null")]
        [TestCase("number")]
        [TestCase("duplicate")]
        [TestCase("blank")]
        [TestCase("control")]
        [TestCase("field")]
        public void InvalidProfileFieldsAreRejected(string invalid)
        {
            var document = JObject.Parse(Encoding.UTF8.GetString(PlayerProfileCodec.Serialize(new PlayerProfile())));
            if (invalid == "missing") document.Remove("UnlockedModifierIds");
            if (invalid == "null") document["UnlockedModifierIds"] = null;
            if (invalid == "number") document["UnlockedModifierIds"] = new JArray(42);
            if (invalid == "duplicate") document["UnlockedModifierIds"] = new JArray("modifier.a", "modifier.a");
            if (invalid == "blank") document["UnlockedModifierIds"] = new JArray(" ");
            if (invalid == "control") document["UnlockedModifierIds"] = new JArray("modifier.\ninvalid");
            if (invalid == "field") document["Unsupported"] = true;
            Assert.Throws<FormatException>(() => PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes(document.ToString())));
        }

        [Test]
        public void InvalidJsonAndUnsupportedVersionsAreRejected()
        {
            Assert.Throws<FormatException>(() => PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes("{ broken")));
            Assert.Throws<FormatException>(() => PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes("[]")));
            Assert.Throws<FormatException>(() => PlayerProfileCodec.Deserialize(new byte[] { 0xff }));
            Assert.Throws<FormatException>(() => PlayerProfileCodec.Deserialize(new byte[PlayerProfileCodec.MaximumProfileBytes + 1]));
            var document = JObject.Parse(Encoding.UTF8.GetString(PlayerProfileCodec.Serialize(new PlayerProfile())));
            Assert.That(document["ProfileFormatVersion"].Value<int>(), Is.EqualTo(PlayerProfile.CurrentFormatVersion));
            foreach (int version in new[] { PlayerProfile.CurrentFormatVersion - 1, PlayerProfile.CurrentFormatVersion + 1 })
            {
                document["ProfileFormatVersion"] = version;
                Assert.Throws<NotSupportedException>(() => PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes(document.ToString())),
                    version.ToString());
            }
        }

        [Test]
        public void AtomicStoreMergesExistingProgressAndKeepsBackup()
        {
            string path = ArtifactPath();
            var first = PlayerProfileFileStore.LoadOrCreate(path);
            first.UnlockModifier("modifier.future.a");
            PlayerProfileFileStore.Save(path, first);
            var second = new PlayerProfile();
            second.UnlockModifier("modifier.future.b");
            second.CompleteEncounter("encounter.done");
            PlayerProfileFileStore.Save(path, second);
            Assert.That(PlayerProfileFileStore.Load(path).UnlockedModifierIds,
                Is.EquivalentTo(new[] { "modifier.future.a", "modifier.future.b" }));
            Assert.That(PlayerProfileFileStore.Load(path + ".bak").UnlockedModifierIds,
                Is.EqualTo(new[] { "modifier.future.a" }));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)).Length, Is.EqualTo(2));
        }

        [Test]
        public void CorruptExistingProfileCannotBeResetByLoadOrSave()
        {
            string path = ArtifactPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] corrupt = Encoding.UTF8.GetBytes("{ broken profile");
            File.WriteAllBytes(path, corrupt);
            Assert.Throws<FormatException>(() => PlayerProfileFileStore.LoadOrCreate(path));
            Assert.Throws<FormatException>(() => PlayerProfileFileStore.Save(path, new PlayerProfile()));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(corrupt));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)).Length, Is.EqualTo(1));
        }

        private static string ArtifactPath()
        {
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..",
                "Docs", "work", "player-profile-tests", Guid.NewGuid().ToString("N")));
            TestContext.WriteLine("Retained profile test files: " + directory);
            return Path.Combine(directory, "spirit-profile.json");
        }
    }
}
