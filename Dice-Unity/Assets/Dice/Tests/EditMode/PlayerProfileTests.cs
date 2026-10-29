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
            profile.UnlockModifier(TestModifiers.IdOf("owned"));
            profile.UnlockModifier(TestModifiers.IdOf("future"));
            profile.CompleteEncounter(TestEncounters.IdOf("encounter.future"));
            Assert.That(profile.DiscoveredFamilyIds, Is.Empty);
            Assert.That(profile.GetDiscoveredFamilyIds(RewardCatalog()), Is.EquivalentTo(new[] { TestFamilies.IdOf("family.starter"), TestFamilies.IdOf("family.owned") }));
            byte[] bytes = PlayerProfileCodec.Serialize(profile);
            Assert.That(JObject.Parse(Encoding.UTF8.GetString(bytes))["UnlockedModifierIds"].Values<string>(),
                Is.EqualTo(new[] { TestModifiers.IdOf("owned").Value.ToString("N"), TestModifiers.IdOf("future").Value.ToString("N") }));
            var restored = PlayerProfileCodec.Deserialize(bytes);
            Assert.That(restored.DiscoveredFamilyIds, Is.Empty);
            Assert.That(restored.UnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf("owned", "future")));
            Assert.That(restored.CompletedEncounterIds, Is.EqualTo(new[] { TestEncounters.IdOf("encounter.future") }));
            Assert.That(restored.GetDiscoveredFamilyIds(RewardCatalog()), Is.EqualTo(profile.GetDiscoveredFamilyIds(RewardCatalog())));
        }

        [Test]
        public void JourneyCompletionIsPersistedMergedAndIndependentOfEncounters()
        {
            var profile = new PlayerProfile();
            var future = TestJourneys.IdOf("journey.future");
            var another = TestJourneys.IdOf("journey.another");
            Assert.That(profile.CompleteJourney(future), Is.True);
            Assert.That(profile.CompleteJourney(future), Is.False);
            Assert.That(profile.CompletedEncounterIds, Is.Empty);
            Assert.That(profile.VisitedEncounterIds, Is.Empty);
            var restored = PlayerProfileCodec.Deserialize(PlayerProfileCodec.Serialize(profile));
            Assert.That(restored.CompletedJourneyIds, Is.EqualTo(new[] { future }));
            var merged = new PlayerProfile();
            Assert.That(merged.Merge(restored), Is.True);
            Assert.That(merged.CompletedJourneyIds, Is.EqualTo(restored.CompletedJourneyIds));
            Assert.That(merged.Merge(restored), Is.False);
            string path = ArtifactPath();
            PlayerProfileFileStore.Save(path, restored);
            var additional = new PlayerProfile();
            additional.CompleteJourney(another);
            PlayerProfileFileStore.Save(path, additional);
            Assert.That(PlayerProfileFileStore.Load(path).CompletedJourneyIds, Is.EquivalentTo(new[] { future, another }));
        }

        [TestCase("missing")]
        [TestCase("null")]
        [TestCase("number")]
        [TestCase("duplicate")]
        [TestCase("invalid")]
        public void InvalidJourneyCompletionFieldsAreRejected(string invalid)
        {
            var document = JObject.Parse(Encoding.UTF8.GetString(PlayerProfileCodec.Serialize(new PlayerProfile())));
            string journey = TestJourneys.IdOf("journey").ToString();
            if (invalid == "missing") document.Remove("CompletedJourneyIds");
            if (invalid == "null") document["CompletedJourneyIds"] = JValue.CreateNull();
            if (invalid == "number") document["CompletedJourneyIds"] = new JArray(42);
            if (invalid == "duplicate") document["CompletedJourneyIds"] = new JArray(journey, journey);
            if (invalid == "invalid") document["CompletedJourneyIds"] = new JArray("journey\ninvalid");
            Assert.Throws<FormatException>(() => PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes(document.ToString())));
        }

        [Test]
        public void RewardsDiscoverAndGrantExplicitIndividualsIdempotently()
        {
            var catalog = RewardCatalog();
            var profile = new PlayerProfile();
            var discovered = new[] { TestFamilies.IdOf("family.revealed") };
            var granted = TestModifiers.IdsOf("owned");
            var reward = new ProgressionRewardDefinition(discovered, granted);
            discovered[0] = TestFamilies.IdOf("unknown");
            granted[0] = TestModifiers.IdOf("unknown");
            Assert.That(profile.ApplyReward(reward, catalog), Is.True);
            Assert.That(profile.UnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf("owned")));
            Assert.That(profile.DiscoveredFamilyIds, Is.EquivalentTo(new[] { TestFamilies.IdOf("family.revealed"), TestFamilies.IdOf("family.owned") }));
            Assert.That(profile.GetDiscoveredFamilyIds(catalog), Is.EquivalentTo(new[] { TestFamilies.IdOf("family.revealed"), TestFamilies.IdOf("family.owned"), TestFamilies.IdOf("family.starter") }));
            Assert.That(profile.ApplyReward(reward, catalog), Is.False);
            var restored = PlayerProfileCodec.Deserialize(PlayerProfileCodec.Serialize(profile));
            Assert.That(restored.ApplyReward(reward, catalog), Is.False);
            Assert.That(restored.UnlockedModifierIds, Has.No.Member(TestModifiers.IdOf("owned.variant")));
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
            var grants = TestModifiers.IdsOf("owned", invalid == "family" ? "owned.variant" : invalid == "modifier" ? "unknown" : invalid);
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
                RewardModifier("encounter", "family.encounter", ModifierAccess.EncounterExclusive),
                RewardModifier("inert", "family.inert", inert: true), RewardModifier("never", "family.never", never: true)
            };
            return new MechanicalCatalog("profile.rewards", TestContent.Hands(), new[]
            {
                new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) })
            }, modifiers);
        }

        private static ModifierDefinition RewardModifier(string id, string family, ModifierAccess access = ModifierAccess.PermanentUnlock,
            bool inert = false, bool never = false)
            => new ModifierDefinition(TestModifiers.IdOf(id), TestFamilies.Of(family), ModifierPhase.PlayHand,
                never ? new NeverCondition() : (IModifierCondition)new AlwaysCondition(),
                inert ? Array.Empty<ModifierEffect>() : new ModifierEffect[] { new AddBaseScoreEffect(1) },
                availability: new ModifierAvailabilityDefinition(access: access));

        [Test]
        public void RoundTripKeepsUnknownIdsAndProgressIsIdempotent()
        {
            var profile = new PlayerProfile();
            Assert.That(profile.UnlockModifier(TestModifiers.IdOf("modifier.future.unknown")), Is.True);
            Assert.That(profile.UnlockModifier(TestModifiers.IdOf("modifier.future.unknown")), Is.False);
            Assert.That(profile.CompleteEncounter(TestEncounters.IdOf("encounter.future")), Is.True);
            Assert.That(profile.CompleteEncounter(TestEncounters.IdOf("encounter.future")), Is.False);
            var restored = PlayerProfileCodec.Deserialize(PlayerProfileCodec.Serialize(profile));
            Assert.That(restored.UnlockedModifierIds, Is.EqualTo(profile.UnlockedModifierIds));
            Assert.That(restored.CompletedEncounterIds, Is.EqualTo(profile.CompletedEncounterIds));
            Assert.That(restored.Merge(profile), Is.False);
        }

        [TestCase("missing")]
        [TestCase("null")]
        [TestCase("number")]
        [TestCase("duplicate")]
        [TestCase("invalid")]
        [TestCase("field")]
        public void InvalidProfileFieldsAreRejected(string invalid)
        {
            var document = JObject.Parse(Encoding.UTF8.GetString(PlayerProfileCodec.Serialize(new PlayerProfile())));
            string modifier = TestModifiers.IdOf("modifier.a").ToString();
            if (invalid == "missing") document.Remove("UnlockedModifierIds");
            if (invalid == "null") document["UnlockedModifierIds"] = null;
            if (invalid == "number") document["UnlockedModifierIds"] = new JArray(42);
            if (invalid == "duplicate") document["UnlockedModifierIds"] = new JArray(modifier, modifier);
            if (invalid == "invalid") document["UnlockedModifierIds"] = new JArray("modifier.a");
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
            first.UnlockModifier(TestModifiers.IdOf("modifier.future.a"));
            PlayerProfileFileStore.Save(path, first);
            var second = new PlayerProfile();
            second.UnlockModifier(TestModifiers.IdOf("modifier.future.b"));
            second.CompleteEncounter(TestEncounters.IdOf("encounter.done"));
            PlayerProfileFileStore.Save(path, second);
            Assert.That(PlayerProfileFileStore.Load(path).UnlockedModifierIds,
                Is.EquivalentTo(TestModifiers.IdsOf("modifier.future.a", "modifier.future.b")));
            Assert.That(PlayerProfileFileStore.Load(path + ".bak").UnlockedModifierIds,
                Is.EqualTo(TestModifiers.IdsOf("modifier.future.a")));
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
