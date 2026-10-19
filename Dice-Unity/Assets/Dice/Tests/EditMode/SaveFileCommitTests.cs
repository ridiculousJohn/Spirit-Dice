using System;
using System.IO;
using Dice.App;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SaveFileCommitTests
    {
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
                    throw new IOException("Replacement unavailable", unchecked((int)0x80070497));
                }
                File.Replace(pending, current, previous);
            });
            Assert.That(calls, Is.InRange(3, 4));
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
                    final = new IOException("Failure " + calls, unchecked((int)0x80070497));
                    throw final;
                }));
            Assert.That(calls, Is.EqualTo(4));
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
            var failure = new IOException("Other file failure", unchecked((int)0x80070000) | error);
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
                profile.UnlockModifier("modifier.save-test." + index);
                PlayerProfileFileStore.Save(destination, profile);
                Assert.That(PlayerProfileFileStore.Load(destination).UnlockedModifierIds.Count, Is.EqualTo(index + 1));
                if (previous != null) Assert.That(File.ReadAllBytes(backup), Is.EqualTo(previous));
                previous = File.ReadAllBytes(destination);
            }
            Assert.That(Directory.GetFiles(artifactRoot).Length, Is.EqualTo(2));
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
