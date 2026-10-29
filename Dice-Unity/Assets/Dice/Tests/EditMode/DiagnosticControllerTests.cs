using System;
using System.IO;
using System.Linq;
using System.Text;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class DiagnosticControllerTests
    {
        private GameObject host;
        private DiagnosticEncounterController controller;
        private string artifactRoot;
        private string savePath;
        private readonly SyntheticCharms synthetic = new SyntheticCharms();

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Diagnostic controller test");
            host.SetActive(false);
            controller = host.AddComponent<DiagnosticEncounterController>();
            using (var serialized = new SerializedObject(controller))
            {
                serialized.FindProperty("view").objectReferenceValue = MinimalView(host.transform);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            artifactRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..",
                "Docs", "work", "diagnostic-file-tests", Guid.NewGuid().ToString("N")));
            savePath = Path.Combine(artifactRoot, "encounter.json");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            synthetic.Dispose();
            if (Directory.Exists(artifactRoot)) TestContext.WriteLine("Retained diagnostic test files: " + artifactRoot);
        }

        [Test]
        public void MissingCatalogKeepsTheHostUsableAndReportsConfiguration()
        {
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.StatusMessage, Does.Contain("catalog"));
            Assert.DoesNotThrow(() => controller.StartNew("60"));
            Assert.DoesNotThrow(() => controller.PlayHand());
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.EventLog, Is.Empty);
        }

        [Test]
        public void SelectionIsLocalAndAcceptedRerollAndPlayMatchTheEngine()
        {
            var catalog = TestContent.Catalog();
            Initialize(catalog, TestContent.EncounterId, 17, savePath);
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 17);
            var original = controller.Snapshot;
            var preview = controller.Preview;
            controller.ToggleDie(0);
            controller.ToggleDie(2);
            controller.ToggleDie(-1);
            controller.ToggleDie(DiceValues.Count);
            Assert.That(controller.SelectedMask, Is.EqualTo(5));
            Assert.That(controller.Snapshot, Is.SameAs(original));
            Assert.That(controller.Preview, Is.SameAs(preview));
            controller.Reroll();
            AssertSnapshots(expected.Execute(new RerollDiceAction(5), 0).Snapshot);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.Preview, Is.Not.SameAs(preview));
            controller.SelectAllDice();
            Assert.That(controller.SelectedMask, Is.EqualTo(DiceValues.AllMask));
            controller.ClearDiceSelection();
            Assert.That(controller.SelectedMask, Is.Zero);
            controller.ToggleDie(1);
            long score = controller.Preview.Score.HandScore;
            controller.PlayHand();
            AssertSnapshots(expected.Execute(PlayHandAction.Instance, 1).Snapshot);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.Snapshot.TotalScore, Is.EqualTo(score));
            Assert.That(controller.EventLog.Any(value => value.Contains(score.ToString())), Is.True);
        }

        [Test]
        public void LockedDiceCannotBeSelected()
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                rules: new[] { new DieBindingRuleDefinition("rule.test.binding", true, true) });
            var catalog = new MechanicalCatalog("test.binding.controller", TestContent.Hands(), new[] { encounter });
            Initialize(catalog, TestContent.EncounterId, 17, savePath);
            int marked = controller.Snapshot.Binding().MarkedMask;
            controller.ToggleDie(Array.IndexOf(new[] { 1, 2, 4, 8, 16 }, marked));
            controller.Reroll();
            Assert.That(controller.Snapshot.LockedMask, Is.EqualTo(marked));
            controller.ToggleDie(Array.IndexOf(new[] { 1, 2, 4, 8, 16 }, marked));
            Assert.That(controller.SelectedMask, Is.Zero, "Tapping a bound die changes nothing.");
            controller.SelectAllDice();
            Assert.That(controller.SelectedMask, Is.EqualTo(DiceValues.AllMask & ~marked));
            controller.Reroll();
            Assert.That(controller.Snapshot.RerollsSpentOnHand, Is.EqualTo(2));
        }

        [Test]
        public void RejectedActionsPreserveSelectionSnapshotPreviewAndHistory()
        {
            Initialize(TestContent.Catalog(new[] { new StageDefinition(1000000, 3, 0) }),
                TestContent.EncounterId, 2, savePath);
            controller.ToggleDie(3);
            var before = controller.Snapshot;
            var preview = controller.Preview;
            var history = controller.EventLog.ToArray();
            controller.Reroll();
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.NoRerollsRemaining)));
            Assert.That(controller.SelectedMask, Is.EqualTo(8));
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.EventLog, Is.EqualTo(history));
            controller.RefreshRewards();
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.InvalidPhase)));
            Assert.That(controller.SelectedMask, Is.EqualTo(8));
            controller.ClearDiceSelection();
            controller.Reroll();
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.InvalidDiceSelection)));
            Assert.That(controller.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void RewardActionsUseCurrentOfferAndOptionIdentities()
        {
            var catalog = RewardTestContent.Catalog();
            Initialize(catalog, TestContent.EncounterId, 60, savePath);
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 60);
            controller.ToggleDie(0);
            controller.ToggleDie(3);
            controller.Reroll();
            AssertSnapshots(expected.Execute(new RerollDiceAction(9), 0).Snapshot);
            // The first stage's threshold is reachable by any hand, so one play opens the reward offer.
            controller.PlayHand();
            AssertSnapshots(expected.Execute(PlayHandAction.Instance, 1).Snapshot);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
            Assert.That(controller.Preview, Is.Null);
            Assert.That(controller.ModifierInspections, Is.Empty);
            var stale = controller.Snapshot.PendingReward;
            controller.RefreshRewards();
            AssertSnapshots(expected.Execute(new RefreshModifierOfferAction(stale.OfferId), 2).Snapshot);
            var before = controller.Snapshot;
            controller.ChooseReward(stale.OfferId, stale.Options[0].OptionId);
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.InvalidOffer)));
            Assert.That(controller.Snapshot, Is.SameAs(before));
            var offer = before.PendingReward;
            controller.ChooseReward(offer.OfferId, -1);
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.InvalidRewardOption)));
            Assert.That(controller.Snapshot, Is.SameAs(before));
            controller.ChooseReward(offer.OfferId, offer.Options[0].OptionId);
            AssertSnapshots(expected.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), 3).Snapshot);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingHandUpgrade));
            offer = controller.Snapshot.PendingReward;
            controller.ChooseReward(offer.OfferId, offer.Options[0].OptionId);
            AssertSnapshots(expected.Execute(new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId), 4).Snapshot);
            Assert.That(controller.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.Playing));
            Assert.That(controller.Preview, Is.Not.Null);
            Assert.That(controller.ModifierInspections.Count, Is.EqualTo(controller.Snapshot.Modifiers.Count));
        }

        [Test]
        public void AbandonAndNewRunRespectCompletionAndResetDiagnosticState()
        {
            var catalog = TestContent.Catalog();
            Initialize(catalog, TestContent.EncounterId, 1, savePath);
            controller.Abandon();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            Assert.That(controller.Preview, Is.Null);
            var completed = controller.Snapshot;
            controller.PlayHand();
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.EncounterCompleted)));
            Assert.That(controller.Snapshot, Is.SameAs(completed));
            controller.StartNew("00042");
            Assert.That(controller.Seed, Is.EqualTo(42UL));
            AssertSnapshots(EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 42).Snapshot);
            Assert.That(controller.EventLog.Count,
                Is.EqualTo(EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 42).InitialResolution.Events.Count));
            Assert.That(controller.SelectedMask, Is.Zero);
        }

        [TestCase("-1")]
        [TestCase("18446744073709551616")]
        [TestCase("not a seed")]
        public void InvalidSeedPreservesTheActiveRunAndSelection(string seed)
        {
            Initialize(TestContent.Catalog(), TestContent.EncounterId, 42, savePath);
            controller.SelectAllDice();
            var before = controller.Snapshot;
            var preview = controller.Preview;
            var history = controller.EventLog.ToArray();
            controller.StartNew(seed);
            Assert.That(controller.StatusMessage, Does.Contain("unsigned 64-bit integer"));
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.SelectedMask, Is.EqualTo(DiceValues.AllMask));
            Assert.That(controller.EventLog, Is.EqualTo(history));
            Assert.That(controller.Seed, Is.EqualTo(42UL));
        }

        [Test]
        public void SaveLoadResumesExactlyAndDoesNotReplayEventsOrSelection()
        {
            var catalog = RewardTestContent.Catalog();
            Initialize(catalog, TestContent.EncounterId, 17, savePath);
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 17);
            controller.PlayHand();
            expected.Execute(PlayHandAction.Instance, 0);
            controller.Save();
            var saved = EncounterSaveFileStore.Load(savePath, catalog);
            Assert.That(EncounterSaveCodec.Serialize(saved), Is.EqualTo(EncounterSaveCodec.Serialize(expected.CaptureSave())));
            controller.StartNew("8");
            controller.SelectAllDice();
            controller.Load();
            AssertSnapshots(expected.Snapshot);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.EventLog, Is.Empty);
            Assert.That(controller.Seed, Is.EqualTo(8UL));
            Assert.That(controller.StatusMessage, Does.Contain("Loaded"));
            var offer = expected.Snapshot.PendingReward;
            controller.ChooseReward(offer.OfferId, offer.Options[0].OptionId);
            AssertSnapshots(expected.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), expected.Snapshot.Revision).Snapshot);
            controller.Save();
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(savePath + ".bak", catalog)),
                Is.EqualTo(EncounterSaveCodec.Serialize(saved)));
        }

        [TestCase("malformed")]
        [TestCase("format")]
        [TestCase("rules")]
        [TestCase("missing")]
        public void FailedLoadPreservesTheActiveRunSelectionAndCachedDisplay(string failure)
        {
            var catalog = TestContent.Catalog();
            Initialize(catalog, TestContent.EncounterId, 3, savePath);
            if (failure != "missing")
            {
                Directory.CreateDirectory(artifactRoot);
                var save = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 5).CaptureSave();
                var json = JObject.Parse(Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(save)));
                if (failure == "format") json["SaveFormatVersion"] = 99;
                if (failure == "rules") json["RulesVersion"] = 99;
                File.WriteAllText(savePath, failure == "malformed" ? "{ broken JSON" : json.ToString());
            }
            controller.ToggleDie(2);
            var before = controller.Snapshot;
            var preview = controller.Preview;
            var inspections = controller.ModifierInspections;
            var history = controller.EventLog.ToArray();
            controller.Load();
            Assert.That(controller.StatusMessage, Does.Contain("Cannot load"));
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.ModifierInspections, Is.SameAs(inspections));
            Assert.That(controller.SelectedMask, Is.EqualTo(4));
            Assert.That(controller.EventLog, Is.EqualTo(history));
            Assert.That(controller.Seed, Is.EqualTo(3UL));
        }

        [Test]
        public void ReadingCachedPreviewAndInspectionsNeverConsumesTheLiveRun()
        {
            var catalog = RewardTestContent.Catalog(inventory: new[] { "first" });
            Initialize(catalog, TestContent.EncounterId, 11, savePath);
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 11);
            var before = controller.Snapshot;
            var preview = controller.Preview;
            var inspections = controller.ModifierInspections;
            var history = controller.EventLog.ToArray();
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.ModifierInspections, Is.SameAs(inspections));
            Assert.That(controller.Preview.Score.HandScore, Is.EqualTo(expected.PreviewPlayHand().HandScore));
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(before.Modifiers[0].TotalActivations, Is.Zero);
            Assert.That(preview.Modifiers[0].TotalActivations, Is.EqualTo(1));
            Assert.That(controller.EventLog, Is.EqualTo(history));
            controller.Save();
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(savePath, catalog)),
                Is.EqualTo(EncounterSaveCodec.Serialize(expected.CaptureSave())));
            controller.PlayHand();
            AssertSnapshots(expected.Execute(PlayHandAction.Instance, 0).Snapshot);
            Assert.That(controller.Snapshot.Modifiers[0].TotalActivations, Is.EqualTo(1));
        }

        [Test]
        public void EventHistoryRemainsBoundedWhileActionsContinue()
        {
            var catalog = TestContent.Catalog(new[] { new StageDefinition(1000000, 100, 100) });
            Initialize(catalog, TestContent.EncounterId, 7, savePath);
            for (int i = 0; i < 90; ++i)
            {
                controller.SelectAllDice();
                controller.Reroll();
            }
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(90));
            Assert.That(controller.EventLog.Count, Is.InRange(100, 200));
            Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(10));
        }

        [Test]
        public void PreviewOverflowKeepsAcceptedCommandsAndRestoredEngineStateUsable()
        {
            var modifier = new ModifierDefinition("overflow", TestFamilies.Of("family.overflow"), ModifierPhase.PlayHand,
                new AlwaysCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(long.MaxValue) });
            var catalog = RewardTestContent.Catalog(modifiers: new[] { modifier }, inventory: new[] { modifier.Id });
            Assert.DoesNotThrow(() => Initialize(catalog, TestContent.EncounterId, 17, savePath));
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 17);
            Assert.That(controller.Preview, Is.Null);
            Assert.That(controller.ModifierInspections.Count, Is.EqualTo(1));
            Assert.That(controller.StatusMessage, Does.Contain("Started").And.Contain("Hand preview unavailable"));
            controller.ToggleDie(0);
            controller.Reroll();
            AssertSnapshots(expected.Execute(new RerollDiceAction(1), 0).Snapshot);
            Assert.That(controller.StatusMessage, Does.Contain("Accepted action").And.Contain("Hand preview unavailable"));
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.EventLog.Count, Is.GreaterThan(3));
            controller.Save();
            controller.StartNew("4");
            controller.SelectAllDice();
            controller.Load();
            AssertSnapshots(expected.Snapshot);
            Assert.That(controller.StatusMessage, Does.Contain("Loaded").And.Contain("Hand preview unavailable"));
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.EventLog, Is.Empty);
            Assert.That(controller.ModifierInspections.Count, Is.EqualTo(1));
            controller.Abandon();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            Assert.That(controller.StatusMessage, Does.Contain("Accepted action"));
            Assert.That(controller.StatusMessage, Does.Not.Contain("unavailable"));
        }

        [Test]
        public void CustomConditionFaultDoesNotBlockRerollSynchronizationOrReuseStaleCaches()
        {
            var modifier = new ModifierDefinition("faulting", TestFamilies.Of("family.faulting"), ModifierPhase.PlayHand,
                new FaultingCondition(), new ModifierEffect[] { new AddBaseScoreEffect(1) });
            var catalog = RewardTestContent.Catalog(modifiers: new[] { modifier }, inventory: new[] { modifier.Id });
            Assert.DoesNotThrow(() => Initialize(catalog, TestContent.EncounterId, 19, savePath));
            Assert.That(controller.Preview, Is.Null);
            Assert.That(controller.ModifierInspections, Is.Empty);
            Assert.That(controller.StatusMessage, Does.Contain("Hand preview unavailable").And.Contain("Modifier inspection unavailable"));
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 19);
            controller.SelectAllDice();
            controller.Reroll();
            AssertSnapshots(expected.Execute(new RerollDiceAction(DiceValues.AllMask), 0).Snapshot);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.StatusMessage, Does.Contain("Accepted action").And.Contain("Modifier inspection unavailable"));
            Assert.That(controller.StatusMessage, Does.Not.Contain("Cannot resolve"));
            Assert.That(controller.EventLog.Count, Is.GreaterThan(3));
            controller.Abandon();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            Assert.That(controller.ModifierInspections.Count, Is.EqualTo(1));
        }

        /**
         * A view holding only the controls its binding wires up, under the inactive host. The controller requires a view;
         * these tests drive the controller directly and never read the view.
         */
        private static DiagnosticEncounterView MinimalView(Transform parent)
        {
            T Child<T>(string name, Transform owner) where T : Component
            {
                var child = new GameObject(name);
                child.transform.SetParent(owner, false);
                return child.AddComponent<T>();
            }
            var view = Child<DiagnosticEncounterView>("Diagnostic view", parent);
            using var serialized = new SerializedObject(view);
            serialized.FindProperty("seedInput").objectReferenceValue = Child<InputField>("seedInput", view.transform);
            foreach (string button in new[] { "startButton", "saveButton", "loadButton", "abandonButton", "selectAllButton",
                "clearSelectionButton", "rerollButton", "playButton", "refreshButton" })
                serialized.FindProperty(button).objectReferenceValue = Child<Button>(button, view.transform);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private sealed class FaultingCondition : IModifierCondition
        {
            public bool Evaluate(in ModifierContext context) => throw new InvalidOperationException("Diagnostic condition failure.");
        }

        private void AssertSnapshots(EncounterSnapshot expected)
            => EncounterSessionTests.AssertSnapshotsEqual(expected, controller.Snapshot);

        /** Starts the controller on the catalog with a test-only presentation of its charms. */
        private void Initialize(MechanicalCatalog catalog, EncounterDefinition.Id encounterId, ulong seed, string path)
            => controller.Initialize(catalog, synthetic.For(catalog), encounterId, seed, path);
    }
}
