using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    public sealed class SpiritControllerTests
    {
        private GameObject host;
        private SpiritEncounterController controller;
        private string artifactRoot;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Spirit controller test");
            host.SetActive(false);
            controller = host.AddComponent<SpiritEncounterController>();
            artifactRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..",
                "Docs", "work", "spirit-file-tests", Guid.NewGuid().ToString("N")));
            savePath = Path.Combine(artifactRoot, "encounter.json");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            if (Directory.Exists(artifactRoot)) TestContext.WriteLine("Retained spirit test files: " + artifactRoot);
        }

        [Test]
        public void SaveAndLoadReportMissingApplicationAndCatalog()
        {
            Assert.That(controller.TrySave(), Is.False);
            Assert.That(controller.TryLoad(), Is.False);
        }

        [Test]
        public void SaveAndLoadReportSuccessAndWriteFailure()
        {
            controller.Initialize(TestContent.Catalog(), TestContent.EncounterId, 17, savePath);
            Assert.That(controller.TrySave(), Is.True);
            var before = controller.Snapshot;
            using (var lockedRun = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(controller.TrySave(), Is.False);
                Assert.That(controller.TryLoad(), Is.False);
                Assert.That(controller.Snapshot, Is.SameAs(before));
            }
            Assert.That(controller.TryLoad(), Is.True);
            Assert.That(controller.Snapshot, Is.Not.SameAs(before));
        }

        [Test]
        public void MissingCatalogLeavesTheHostUsable()
        {
            Assert.That(host.GetComponent<SpiritEncounterView>(), Is.Null, "Controllers do not construct presentation.");
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.StatusMessage, Does.Contain("startup"));
            Assert.DoesNotThrow(() => controller.StartNew());
            Assert.DoesNotThrow(() => controller.PlayHand());
            Assert.DoesNotThrow(() => controller.Load());
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.IsBusy, Is.False);
        }

        [Test]
        public void TappingALockedDieChangesNothing()
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                rules: new[] { new DieBindingRuleDefinition("rule.test.binding", true, true) });
            var catalog = new MechanicalCatalog("test.binding.controller", TestContent.Hands(), new[] { encounter });
            controller.Initialize(catalog, TestContent.EncounterId, 17, savePath);
            int bay = Array.IndexOf(new[] { 1, 2, 4, 8, 16 }, controller.Snapshot.Binding().MarkedMask);
            controller.ToggleDie(bay);
            controller.Reroll();
            Assert.That(controller.Snapshot.LockedMask, Is.EqualTo(1 << bay));
            int changes = 0;
            controller.StateChanged += () => ++changes;
            controller.ToggleDie(bay);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(changes, Is.Zero);
            controller.ToggleDie((bay + 1) % DiceValues.Count);
            Assert.That(controller.SelectedMask, Is.EqualTo(1 << (bay + 1) % DiceValues.Count));
        }

        /** Checks an accepted resolution clears the dice selection exactly when one of its events states it ends the selection / fails if the controller decides by event type or ignores the event's statement. */
        [TestCase(false)]
        [TestCase(true)]
        public void ResolutionClearsTheSelectionWhenAnEventEndsIt(bool ends)
        {
            controller.Initialize(TestContent.Catalog(), TestContent.EncounterId, 17, savePath);
            controller.ToggleDie(0);
            controller.ToggleDie(2);
            controller.Present(new Resolution(controller.Snapshot.Revision, controller.Snapshot,
                new RuleEvent[] { new SelectionEvent(ends) }));
            Assert.That(controller.SelectedMask, Is.EqualTo(ends ? 0 : 5));
            controller.CompletePresentation();
        }

        [Test]
        public void NoResolutionSubscriberImmediatelyReleasesPresentation()
        {
            var catalog = TestContent.Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 17, savePath);
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 17);
            controller.ToggleDie(0);
            controller.ToggleDie(2);
            var states = new List<bool>();
            controller.StateChanged += () => states.Add(controller.IsBusy);
            controller.Reroll();
            AssertSnapshots(expected.Execute(new RerollDiceAction(5), 0).Snapshot);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(states, Is.EqualTo(new[] { true, false }));
            Assert.That(controller.IsBusy, Is.False);
            controller.PlayHand();
            AssertSnapshots(expected.Execute(PlayHandAction.Instance, 1).Snapshot);
            Assert.That(controller.IsBusy, Is.False);
        }

        [Test]
        public void AcceptedResolutionCachesFinalStateBeforeCallbacksAndWaitsForCompletion()
        {
            var catalog = TestContent.Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 17, savePath);
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 17);
            var result = expected.Execute(new RerollDiceAction(1), 0);
            var originalPreview = controller.Preview;
            controller.ToggleDie(0);
            var callbacks = new List<string>();
            controller.StateChanged += () =>
            {
                callbacks.Add(controller.IsBusy ? "busy" : "ready");
                AssertSnapshots(result.Snapshot);
                Assert.That(controller.SelectedMask, Is.Zero);
            };
            Resolution presented = null;
            controller.Resolved += resolution =>
            {
                callbacks.Add("resolved");
                presented = resolution;
                Assert.That(controller.IsBusy, Is.True);
                Assert.That(controller.Snapshot, Is.SameAs(resolution.Snapshot));
                Assert.That(controller.Preview, Is.Not.Null.And.Not.SameAs(originalPreview));
            };
            controller.Reroll();
            Assert.That(presented, Is.Not.Null);
            Assert.That(callbacks, Is.EqualTo(new[] { "busy", "resolved" }));
            Assert.That(controller.IsBusy, Is.True);
            controller.CompletePresentation();
            Assert.That(callbacks, Is.EqualTo(new[] { "busy", "resolved", "ready" }));
            Assert.That(controller.IsBusy, Is.False);
            controller.CompletePresentation();
            Assert.That(callbacks.Count, Is.EqualTo(3));
        }

        [Test]
        public void AllCommandsAreBlockedUntilPresentationCompletes()
        {
            var catalog = TestContent.Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 17, savePath);
            int resolutions = 0;
            controller.Resolved += resolution => ++resolutions;
            controller.ToggleDie(0);
            controller.Reroll();
            var before = controller.Snapshot;
            var preview = controller.Preview;
            var inspections = controller.ModifierInspections;
            string status = controller.StatusMessage;
            int changes = 0;
            controller.StateChanged += () => ++changes;
            controller.ToggleDie(2);
            controller.Reroll();
            controller.PlayHand();
            controller.RefreshRewards();
            controller.ChooseReward(1, 1);
            controller.StartNew();
            controller.Initialize(catalog, TestContent.EncounterId, 42, savePath);
            controller.Save();
            controller.Load();
            Assert.That(controller.TrySave(), Is.False);
            Assert.That(controller.TryLoad(), Is.False);
            controller.Abandon();
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.ModifierInspections, Is.SameAs(inspections));
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.StatusMessage, Is.EqualTo(status));
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(changes, Is.Zero);
            Assert.That(resolutions, Is.EqualTo(1));
            Assert.That(File.Exists(savePath), Is.False);
            controller.CompletePresentation();
            controller.PlayHand();
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(before.Revision + 1));
            Assert.That(resolutions, Is.EqualTo(2));
        }

        [Test]
        public void RejectedCommandsPreserveSelectionAndCachesWithoutEnteringPresentation()
        {
            controller.Initialize(TestContent.Catalog(new[] { new StageDefinition(1000000, 3, 0) }),
                TestContent.EncounterId, 2, savePath);
            controller.ToggleDie(3);
            controller.ToggleDie(-1);
            controller.ToggleDie(DiceValues.Count);
            var before = controller.Snapshot;
            var preview = controller.Preview;
            var inspections = controller.ModifierInspections;
            int resolutions = 0;
            controller.Resolved += resolution => ++resolutions;
            controller.Reroll();
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.NoRerollsRemaining)));
            Assert.That(controller.SelectedMask, Is.EqualTo(8));
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.ModifierInspections, Is.SameAs(inspections));
            Assert.That(controller.IsBusy, Is.False);
            controller.RefreshRewards();
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.InvalidPhase)));
            Assert.That(resolutions, Is.Zero);
        }

        [Test]
        public void RewardTransitionsAndSaveLoadMatchTheEngineWithoutReplayingHistory()
        {
            var catalog = RewardTestContent.Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 17, savePath);
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 17);
            int resolutions = 0;
            controller.Resolved += resolution =>
            {
                ++resolutions;
                Assert.That(controller.IsBusy, Is.True);
                controller.CompletePresentation();
            };
            controller.PlayHand();
            AssertSnapshots(expected.Execute(PlayHandAction.Instance, 0).Snapshot);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
            Assert.That(controller.Preview, Is.Null);
            var stale = controller.Snapshot.PendingReward;
            controller.RefreshRewards();
            AssertSnapshots(expected.Execute(new RefreshModifierOfferAction(stale.OfferId), 1).Snapshot);
            controller.Save();
            var saved = EncounterSaveFileStore.Load(savePath, catalog);
            Assert.That(EncounterSaveCodec.Serialize(saved), Is.EqualTo(EncounterSaveCodec.Serialize(expected.CaptureSave())));
            var refreshed = controller.Snapshot;
            controller.ChooseReward(stale.OfferId, stale.Options[0].OptionId);
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.InvalidOffer)));
            Assert.That(controller.Snapshot, Is.SameAs(refreshed));
            controller.StartNew();
            controller.ToggleDie(2);
            controller.Load();
            AssertSnapshots(expected.Snapshot);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(resolutions, Is.EqualTo(2));
            Assert.That(controller.StatusMessage, Does.Contain("Loaded"));
            var offer = controller.Snapshot.PendingReward;
            controller.ChooseReward(offer.OfferId, -1);
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.InvalidRewardOption)));
            controller.ChooseReward(offer.OfferId, offer.Options[0].OptionId);
            AssertSnapshots(expected.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), 2).Snapshot);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingHandUpgrade));
            offer = controller.Snapshot.PendingReward;
            controller.ChooseReward(offer.OfferId, offer.Options[0].OptionId);
            AssertSnapshots(expected.Execute(new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId), 3).Snapshot);
            Assert.That(controller.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.Playing));
            Assert.That(controller.Preview, Is.Not.Null);
            Assert.That(resolutions, Is.EqualTo(4));
            controller.Save();
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(savePath + ".bak", catalog)),
                Is.EqualTo(EncounterSaveCodec.Serialize(saved)));
        }

        [TestCase("malformed")]
        [TestCase("format")]
        [TestCase("rules")]
        [TestCase("missing")]
        public void FailedLoadPreservesTheActiveRunSelectionAndCaches(string failure)
        {
            var catalog = TestContent.Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 3, savePath);
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
            int resolutions = 0;
            controller.Resolved += resolution => ++resolutions;
            Assert.That(controller.TryLoad(), Is.False);
            Assert.That(controller.StatusMessage, Does.Contain("Cannot load"));
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.ModifierInspections, Is.SameAs(inspections));
            Assert.That(controller.SelectedMask, Is.EqualTo(4));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(resolutions, Is.Zero);
        }

        [Test]
        public void PreviewFailureDoesNotInvalidateAcceptedOrRestoredState()
        {
            var modifier = new ModifierDefinition("overflow", "family.overflow", ModifierPhase.PlayHand,
                new AlwaysCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(long.MaxValue) });
            var catalog = RewardTestContent.Catalog(modifiers: new[] { modifier }, inventory: new[] { modifier.Id });
            Assert.DoesNotThrow(() => controller.Initialize(catalog, TestContent.EncounterId, 17, savePath));
            var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 17);
            Assert.That(controller.Preview, Is.Null);
            Assert.That(controller.ModifierInspections.Count, Is.EqualTo(1));
            Assert.That(controller.StatusMessage, Does.Contain("Hand preview unavailable"));
            controller.ToggleDie(0);
            controller.Reroll();
            AssertSnapshots(expected.Execute(new RerollDiceAction(1), 0).Snapshot);
            Assert.That(controller.StatusMessage, Does.Contain("Accepted action").And.Contain("Hand preview unavailable"));
            Assert.That(controller.IsBusy, Is.False);
            controller.Save();
            controller.StartNew();
            controller.ToggleDie(2);
            controller.Load();
            AssertSnapshots(expected.Snapshot);
            Assert.That(controller.StatusMessage, Does.Contain("Loaded").And.Contain("Hand preview unavailable"));
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.ModifierInspections.Count, Is.EqualTo(1));
        }

        [Test]
        public void NewRunSynchronizesTheRememberedSeedAndAbandonUsesAnEngineResolution()
        {
            ConfigureSeedPolicy(true, "42");
            var catalog = TestContent.Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 42, savePath);
            int resolutions = 0;
            controller.Resolved += resolution =>
            {
                ++resolutions;
                controller.CompletePresentation();
            };
            controller.Abandon();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            Assert.That(controller.Preview, Is.Null);
            Assert.That(resolutions, Is.EqualTo(1));
            controller.PlayHand();
            Assert.That(controller.StatusMessage, Does.Contain(nameof(CommandRejection.EncounterCompleted)));
            Assert.That(resolutions, Is.EqualTo(1));
            controller.StartNew();
            AssertSnapshots(EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 42).Snapshot);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(resolutions, Is.EqualTo(1));
        }

        [Test]
        public void ConsecutiveDefaultNewRunsUseDifferentDiceAndRewardStreams()
        {
            controller.Initialize(TestContent.Catalog(), TestContent.EncounterId, 42, savePath);
            controller.Save();
            Assert.That(controller.StatusMessage, Does.StartWith("Saved encounter."), controller.StatusMessage);
            var previous = EncounterSaveFileStore.Load(savePath, TestContent.Catalog());
            var unityRandomState = UnityEngine.Random.state;
            for (int run = 0; run < 2; ++run)
            {
                controller.StartNew();
                Assert.That(controller.StatusMessage, Does.StartWith("Started encounter with seed "), controller.StatusMessage);
                Assert.That(controller.Snapshot.Revision, Is.Zero);
                Assert.That(controller.IsBusy, Is.False);
                Assert.That(UnityEngine.Random.state, Is.EqualTo(unityRandomState));
                controller.Save();
                Assert.That(controller.StatusMessage, Does.StartWith("Saved encounter."), controller.StatusMessage);
                var current = EncounterSaveFileStore.Load(savePath, TestContent.Catalog());
                Assert.That(current.DiceRandomState, Is.Not.EqualTo(previous.DiceRandomState));
                Assert.That(current.RewardRandomState, Is.Not.EqualTo(previous.RewardRandomState));
                previous = current;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitInitializationReplaysItsSeedWithoutChangingTheDebugPolicy(bool fixedSeed)
        {
            ConfigureSeedPolicy(fixedSeed, "17");
            var catalog = TestContent.Catalog();
            for (int run = 0; run < 2; ++run)
            {
                controller.Initialize(catalog, TestContent.EncounterId, 42, savePath);
                var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 42);
                AssertSnapshots(expected.Snapshot);
                controller.ToggleDie(0);
                controller.ToggleDie(2);
                controller.Reroll();
                AssertSnapshots(expected.Execute(new RerollDiceAction(5), 0).Snapshot);
            }
            Assert.That(typeof(SpiritEncounterController).GetField("useFixedSeed",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.EqualTo(fixedSeed));
            if (fixedSeed)
            {
                controller.StartNew();
                AssertSnapshots(EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 17).Snapshot);
            }
        }

        [Test]
        public void FixedDebugSeedRepeatsInitialDiceRerollsAndRewardOffers()
        {
            ConfigureSeedPolicy(true, "42");
            var catalog = RewardTestContent.Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 17, savePath);
            byte[] previous = null;
            for (int run = 0; run < 2; ++run)
            {
                controller.StartNew();
                var expected = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 42);
                AssertSnapshots(expected.Snapshot);
                controller.ToggleDie(0);
                controller.ToggleDie(2);
                controller.Reroll();
                AssertSnapshots(expected.Execute(new RerollDiceAction(5), 0).Snapshot);
                controller.PlayHand();
                AssertSnapshots(expected.Execute(PlayHandAction.Instance, 1).Snapshot);
                Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
                var offer = controller.Snapshot.PendingReward;
                controller.RefreshRewards();
                AssertSnapshots(expected.Execute(new RefreshModifierOfferAction(offer.OfferId), 2).Snapshot);
                controller.Save();
                byte[] current = EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(savePath, catalog));
                Assert.That(current, Is.EqualTo(EncounterSaveCodec.Serialize(expected.CaptureSave())));
                if (previous != null) Assert.That(current, Is.EqualTo(previous));
                previous = current;
            }
        }

        [TestCase("not a seed")]
        [TestCase("-1")]
        [TestCase("18446744073709551616")]
        public void InvalidFixedSeedPreservesTheRunAndIsIgnoredInNormalMode(string invalidSeed)
        {
            controller.Initialize(TestContent.Catalog(), TestContent.EncounterId, 3, savePath);
            controller.ToggleDie(2);
            var before = controller.Snapshot;
            var preview = controller.Preview;
            var inspections = controller.ModifierInspections;
            ConfigureSeedPolicy(true, invalidSeed);
            controller.StartNew();
            Assert.That(controller.StatusMessage, Does.Contain("Cannot start").And.Contain("unsigned 64-bit integer"));
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.ModifierInspections, Is.SameAs(inspections));
            Assert.That(controller.SelectedMask, Is.EqualTo(4));
            Assert.That(controller.IsBusy, Is.False);
            ConfigureSeedPolicy(false, invalidSeed);
            controller.StartNew();
            Assert.That(controller.StatusMessage, Does.Contain("Started encounter"));
            Assert.That(controller.Snapshot, Is.Not.SameAs(before));
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(controller.SelectedMask, Is.Zero);
        }

        [Test]
        public void LoadingARandomlyStartedRunResumesItsDiceAndRewardStreams()
        {
            var catalog = RewardTestContent.Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 17, savePath);
            controller.StartNew();
            controller.Save();
            var saved = EncounterSaveFileStore.Load(savePath, catalog);
            for (int replay = 0; replay < 2; ++replay)
            {
                var restored = EncounterSession.RestoreEncounter(catalog, saved);
                Assert.That(restored.Success, Is.True);
                var expected = restored.Session;
                if (replay > 0)
                {
                    controller.StartNew();
                    controller.Load();
                }
                AssertSnapshots(expected.Snapshot);
                controller.ToggleDie(0);
                controller.ToggleDie(2);
                controller.Reroll();
                AssertSnapshots(expected.Execute(new RerollDiceAction(5), 0).Snapshot);
                controller.PlayHand();
                AssertSnapshots(expected.Execute(PlayHandAction.Instance, 1).Snapshot);
                var offer = controller.Snapshot.PendingReward;
                controller.RefreshRewards();
                AssertSnapshots(expected.Execute(new RefreshModifierOfferAction(offer.OfferId), 2).Snapshot);
                if (replay > 0)
                {
                    controller.Save();
                    Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(savePath, catalog)),
                        Is.EqualTo(EncounterSaveCodec.Serialize(expected.CaptureSave())));
                }
            }
        }

        private void ConfigureSeedPolicy(bool fixedSeed, string value)
        {
            controller.ConfigureSeedPolicy(fixedSeed, value);
        }

        private void AssertSnapshots(EncounterSnapshot expected)
            => EncounterSessionTests.AssertSnapshotsEqual(expected, controller.Snapshot);

        /** An event that states whether it ends the dice selection, and nothing else. */
        private sealed class SelectionEvent : RuleEvent
        {
            private readonly bool ends;
            internal SelectionEvent(bool ends) : base(0, 0) => this.ends = ends;
            public override bool EndsDiceSelection => ends;
            protected override string DescribeDetail(IDiagnosticFormat format) => "selection";
        }
    }
}
