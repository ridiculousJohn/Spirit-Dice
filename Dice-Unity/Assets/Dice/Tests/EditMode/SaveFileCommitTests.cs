using System;
using System.IO;
using System.Linq;
using System.Text;
using Dice.App;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SaveFileCommitTests
    {
        /** The HRESULT of a Win32 error is this base combined with the error code. */
        private const int Win32ErrorHResult = unchecked((int)0x80070000);
        private string artifactRoot;
        private string source;
        private string destination;
        private string backup;

        [SetUp]
        public void SetUp()
        {
            artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "save-commit-tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(artifactRoot);
            source = Path.Combine(artifactRoot, "save.tmp");
            destination = Path.Combine(artifactRoot, "save.json");
            backup = destination + ".bak";
        }

        [TearDown]
        public void TearDown() => TestContext.WriteLine("Retained save files: " + artifactRoot);

        [Test]
        public void ReplacementRetriesThePreparedFileAndPreservesThePreviousSave()
        {
            PrepareFiles();
            int calls = 0;
            SaveFileCommit.ReplaceWithRetry(source, destination, backup, (pending, current, previous) =>
            {
                ++calls;
                Assert.That(pending, Is.EqualTo(source));
                Assert.That(current, Is.EqualTo(destination));
                Assert.That(previous, Is.EqualTo(backup));
                if (calls <= 2)
                {
                    AssertPreparedFiles();
                    throw new IOException("Replacement unavailable", SaveFileCommit.UnableToRemoveReplacedHResult);
                }
                File.Replace(pending, current, previous);
            });
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("new save"));
            Assert.That(File.ReadAllText(backup), Is.EqualTo("current save"));
            Assert.That(File.Exists(source), Is.False);
        }

        [Test]
        public void ExhaustedRetriesPropagateTheFinalExceptionAndRetainAllFiles()
        {
            PrepareFiles();
            int calls = 0;
            IOException final = null;
            var actual = Assert.Throws<IOException>(() => SaveFileCommit.ReplaceWithRetry(source, destination, backup,
                (pending, current, previous) =>
                {
                    ++calls;
                    final = new IOException("Failure " + calls, SaveFileCommit.UnableToRemoveReplacedHResult);
                    throw final;
                }));
            Assert.That(calls, Is.EqualTo(SaveFileCommit.MaximumReplaceRetries + 1));
            Assert.That(actual, Is.SameAs(final));
            AssertPreparedFiles();
        }

        [TestCase(5)]
        [TestCase(32)]
        [TestCase(1176)]
        [TestCase(1177)]
        public void OtherIoFailuresAreNotRetried(int error)
        {
            PrepareFiles();
            int calls = 0;
            var failure = new IOException("Other file failure", Win32ErrorHResult | error);
            var actual = Assert.Throws<IOException>(() => SaveFileCommit.ReplaceWithRetry(source, destination, backup,
                (pending, current, previous) => { ++calls; throw failure; }));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(actual, Is.SameAs(failure));
            AssertPreparedFiles();
        }

        [Test]
        public void ProfileReplacementsKeepMergedProgressAndThePreviousBackup()
        {
            byte[] previous = null;
            for (int index = 0; index < 128; ++index)
            {
                var profile = new PlayerProfile();
                profile.UnlockModifier(TestModifiers.IdOf("modifier.save-test." + index));
                PlayerProfileFileStore.Save(destination, profile);
                Assert.That(PlayerProfileFileStore.Load(destination).UnlockedModifierIds.Count, Is.EqualTo(index + 1));
                if (previous != null) Assert.That(File.ReadAllBytes(backup), Is.EqualTo(previous));
                previous = File.ReadAllBytes(destination);
            }
            Assert.That(Directory.GetFiles(artifactRoot).Length, Is.EqualTo(2));
        }

        /** Checks a written file is its seal line followed by the payload and reads back sealed / fails if the layout or payload changes. */
        [Test]
        public void WrittenFilesAreSealedAndReadBackTheirPayload()
        {
            byte[] payload = Encoding.UTF8.GetBytes("{ \"value\": 1 }");
            SaveFileCommit.Write(destination, payload);
            byte[] file = File.ReadAllBytes(destination);
            string seal = Encoding.ASCII.GetString(file, 0, SaveFileText.SealLength);
            Assert.That(seal, Does.Match("^sha256 [0-9a-f]{64}\n$"));
            Assert.That(file.Skip(SaveFileText.SealLength), Is.EqualTo(payload));
            Assert.That(SaveFileText.ReadBounded(destination, payload.Length, "The test file", out bool isSealed), Is.EqualTo(payload));
            Assert.That(isSealed, Is.True);
        }

        /** Checks a sealed file whose payload, checksum or seal line changed fails to read / fails if any of them reads. */
        [TestCase("payload")]
        [TestCase("checksum")]
        [TestCase("truncated")]
        public void AChangedSealedFileFailsToRead(string change)
        {
            SaveFileCommit.Write(destination, Encoding.UTF8.GetBytes("{ \"value\": 1 }"));
            byte[] file = File.ReadAllBytes(destination);
            if (change == "payload") file[file.Length - 2] ^= 1;
            if (change == "checksum") file[10] = file[10] == (byte)'0' ? (byte)'1' : (byte)'0';
            if (change == "truncated") file = file.Take(20).ToArray();
            File.WriteAllBytes(destination, file);
            Assert.Throws<FormatException>(() => SaveFileText.ReadBounded(destination, 1024, "The test file", out _));
        }

        private void PrepareFiles()
        {
            File.WriteAllText(source, "new save");
            File.WriteAllText(destination, "current save");
            File.WriteAllText(backup, "older save");
        }

        private void AssertPreparedFiles()
        {
            Assert.That(File.ReadAllText(source), Is.EqualTo("new save"));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("current save"));
            Assert.That(File.ReadAllText(backup), Is.EqualTo("older save"));
        }
    }
}
