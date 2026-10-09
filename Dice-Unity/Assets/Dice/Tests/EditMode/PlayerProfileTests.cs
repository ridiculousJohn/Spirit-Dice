using System;
using System.IO;
using System.Text;
using Dice.App;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class PlayerProfileTests
    {
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
            document["ProfileFormatVersion"] = 99;
            Assert.Throws<NotSupportedException>(() => PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes(document.ToString())));
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
