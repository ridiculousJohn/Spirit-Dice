using System;
using System.IO;
using System.Linq;
using System.Text;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Application = UnityEngine.Application;

namespace Dice.Tests
{
    public sealed class AppPersistenceTests
    {
        [Test]
        public void CodecPreservesIntegerBitsAndDistinguishesNullFromEmptyArrays()
        {
            var save = EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 42).CaptureSave();
            save.DiceRandomState = ulong.MaxValue;
            save.RewardRandomState = 1UL << 63;
            save.TotalScore = long.MaxValue;
            save.Revision = (1L << 53) + 1;
            save.NextOfferId = long.MaxValue - 1;
            save.PendingUpgradeCategories = Array.Empty<HandCategory>();
            var restored = EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(save), TestContent.Catalog());
            Assert.That(restored.DiceRandomState, Is.EqualTo(ulong.MaxValue));
            Assert.That(restored.RewardRandomState, Is.EqualTo(1UL << 63));
            Assert.That(restored.TotalScore, Is.EqualTo(long.MaxValue));
            Assert.That(restored.Revision, Is.EqualTo((1L << 53) + 1));
            Assert.That(restored.NextOfferId, Is.EqualTo(long.MaxValue - 1));
            Assert.That(restored.DiceBeforeLastReroll, Is.Null);
            Assert.That(restored.PendingUpgradeCategories, Is.Not.Null.And.Empty);
            Assert.That(EncounterSaveCodec.Serialize(restored), Is.EqualTo(EncounterSaveCodec.Serialize(save)));
        }

        [Test]
        public void CodecRejectsEveryTruncatedPrefixAndTrailingData()
        {
            var session = EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 3);
            byte[] bytes = EncounterSaveCodec.Serialize(session.CaptureSave());
            for (int length = 0; length < bytes.Length; ++length)
                Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(bytes.Take(length).ToArray(), TestContent.Catalog()), length.ToString());
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(bytes.Concat(new byte[] { (byte)'{' }).ToArray(), TestContent.Catalog()));
        }

        [Test]
        public void CodecRejectsInvalidSignatureBooleanAndUnsupportedFormat()
        {
            byte[] bytes = EncounterSaveCodec.Serialize(EncounterSession.CreateEncounter(
                TestContent.Catalog(), TestContent.EncounterId, 3).CaptureSave());
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes("[]"), TestContent.Catalog()));
            var invalid = JObject.Parse(Encoding.UTF8.GetString(bytes));
            invalid["HasPreviousStage"] = 2;
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes(invalid.ToString()), TestContent.Catalog()));
            invalid = JObject.Parse(Encoding.UTF8.GetString(bytes));
            invalid["SaveFormatVersion"] = 99;
            Assert.Throws<NotSupportedException>(() => EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes(invalid.ToString()), TestContent.Catalog()));
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(new byte[EncounterSaveCodec.MaximumSaveBytes + 1], TestContent.Catalog()));
        }

        [Test]
        public void DecodedCompatibilityMismatchRemainsAnExplicitEngineFailure()
        {
            var catalog = TestContent.Catalog();
            var save = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 8).CaptureSave();
            ++save.RulesVersion;
            var decoded = EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(save), catalog);
            bool success = EncounterApplication.TryRestore(catalog, decoded, out var application, out var result);
            Assert.That(success, Is.False);
            Assert.That(application, Is.Null);
            Assert.That(result.Session, Is.Null);
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.RulesVersionMismatch));
        }

        [Test]
        public void RestoredApplicationSynchronizesPresenterWithoutReplayingGameplay()
        {
            var catalog = TestContent.Catalog();
            var original = new EncounterApplication(catalog, TestContent.EncounterId, 52);
            Assert.That(original.Submit(new RerollDiceAction(5), 0).Accepted, Is.True);
            var save = EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(original.CaptureSave()), catalog);
            Assert.That(EncounterApplication.TryRestore(catalog, save, out var restored, out var result), Is.True, result.Detail);
            Assert.That(restored.InitialResolution.Events, Is.Empty);
            Assert.That(restored.InitialResolution.PreviousRevision, Is.EqualTo(save.Revision));
            var presenter = new RecordingPresenter();
            restored.AttachPresenter(presenter);
            Assert.That(presenter.Synchronizations, Is.EqualTo(1));
            Assert.That(presenter.Resolutions, Is.Zero);
            EncounterSessionTests.AssertSnapshotsEqual(original.Snapshot, presenter.Snapshot);
            Assert.That(restored.Submit(PlayHandAction.Instance, 0).Rejection, Is.EqualTo(CommandRejection.StaleRevision));
            Assert.That(presenter.Resolutions, Is.Zero);
            var expected = original.Submit(PlayHandAction.Instance, original.Snapshot.Revision);
            var actual = restored.Submit(PlayHandAction.Instance, restored.Snapshot.Revision);
            Assert.That(actual.Accepted, Is.True);
            Assert.That(presenter.Resolutions, Is.EqualTo(1));
            EncounterSessionTests.AssertSnapshotsEqual(expected.Snapshot, actual.Snapshot);
            Assert.That(EncounterSaveCodec.Serialize(restored.CaptureSave()), Is.EqualTo(EncounterSaveCodec.Serialize(original.CaptureSave())));
        }

        [Test]
        public void FileStoreCommitsCompleteSavesAndRetainsThePreviousSave()
        {
            string artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath,
                "..", "..", "Docs", "work", "save-resume-file-tests", Guid.NewGuid().ToString("N")));
            string path = Path.Combine(artifactRoot, "encounter.json");
            var catalog = TestContent.Catalog();
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 17);
            var first = session.CaptureSave();
            EncounterSaveFileStore.Save(path, first);
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(path, catalog)), Is.EqualTo(EncounterSaveCodec.Serialize(first)));
            Assert.That(session.Execute(new RerollDiceAction(3), 0).Accepted, Is.True);
            var second = session.CaptureSave();
            EncounterSaveFileStore.Save(path, second);
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(path, catalog)), Is.EqualTo(EncounterSaveCodec.Serialize(second)));
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(path + ".bak", catalog)), Is.EqualTo(EncounterSaveCodec.Serialize(first)));
            var invalid = session.CaptureSave();
            ++invalid.SaveFormatVersion;
            Assert.Throws<NotSupportedException>(() => EncounterSaveFileStore.Save(path, invalid));
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(path, catalog)), Is.EqualTo(EncounterSaveCodec.Serialize(second)));
            Assert.That(Directory.GetFiles(artifactRoot).Length, Is.EqualTo(2));
            TestContext.WriteLine("Retained save files: " + artifactRoot);
        }

        [Test]
        public void FileStoreRepeatedlyReplacesSavesAndRetainsEachPreviousSave()
        {
            string artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath,
                "..", "..", "Docs", "work", "save-resume-file-tests", Guid.NewGuid().ToString("N")));
            string path = Path.Combine(artifactRoot, "encounter.json");
            var catalog = TestContent.Catalog();
            byte[] previous = null;
            try
            {
                for (ulong seed = 0; seed < 128; ++seed)
                {
                    var save = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, seed).CaptureSave();
                    byte[] expected = EncounterSaveCodec.Serialize(save);
                    try { EncounterSaveFileStore.Save(path, save); }
                    catch (Exception exception)
                    {
                        TestContext.WriteLine("Save failed for seed " + seed + ", HResult " + exception.HResult);
                        throw;
                    }
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(expected), "Main save for seed " + seed);
                    if (previous != null)
                        Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(previous), "Backup for seed " + seed);
                    previous = expected;
                }
                Assert.That(Directory.GetFiles(artifactRoot).Length, Is.EqualTo(2));
            }
            finally
            {
                TestContext.WriteLine("Retained save files: " + artifactRoot);
            }
        }

        private sealed class RecordingPresenter : IEncounterPresenter
        {
            internal int Synchronizations;
            internal int Resolutions;
            internal EncounterSnapshot Snapshot;

            public void Synchronize(EncounterSnapshot snapshot)
            {
                ++Synchronizations;
                Snapshot = snapshot;
            }

            public void Present(Resolution resolution)
            {
                ++Resolutions;
                Snapshot = resolution.Snapshot;
            }
        }
    }
}
