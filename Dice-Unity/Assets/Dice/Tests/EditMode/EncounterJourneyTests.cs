using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using static Dice.Tests.TestJourneys;

namespace Dice.Tests
{
    public sealed class EncounterJourneyTests
    {
        private const string FirstName = "encounter.harbor";
        private const string SecondName = "encounter.observatory";
        private static readonly EncounterDefinition.Id FirstId = TestEncounters.IdOf(FirstName);
        private static readonly EncounterDefinition.Id SecondId = TestEncounters.IdOf(SecondName);
        private const string GiftId = "modifier.story-gift";
        private const string GiftFamily = "family.story-gift";
        private static readonly ModifierFamilyDefinition.Id GiftFamilyId = TestFamilies.IdOf(GiftFamily);
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private readonly SyntheticCharms charms = new SyntheticCharms();
        private GameObject host;
        private SpiritEncounterController controller;
        private MechanicalCatalog catalog;
        private EncounterJourneyCatalogAsset journeys;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Journey controls test");
            host.SetActive(false);
            controller = host.AddComponent<SpiritEncounterController>();
            controller.ConfigureSeedPolicy(true, 17);
            var modifier = new ModifierDefinition("modifier.journey-test", TestFamilies.Of("family.journey-test"), ModifierPhase.PlayHand,
                new AlwaysCondition(), new ModifierEffect[] { new AddBaseScoreEffect(1) });
            var policy = new RewardPolicyDefinition("policy.journey-test", new[] { new ModifierRewardLayout(new[] { "pool.journey-test" }) },
                1, 1, 1, 3, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            catalog = new MechanicalCatalog("test.journeys.v1", TestContent.Hands(), new[]
            {
                new EncounterDefinition(FirstId, new[] { new StageDefinition(1000000, 5, 3) }),
                new EncounterDefinition(SecondId, new[] { new StageDefinition(1, 2, 3, StageExitBehavior.AwaitRewards),
                    new StageDefinition(1000000, 5, 3) }, rewardPolicy: policy)
            }, new[] { modifier, new ModifierDefinition(GiftId, TestFamilies.Of(GiftFamily), ModifierPhase.PlayHand,
                new AlwaysCondition(), new ModifierEffect[] { new AddBaseScoreEffect(5) },
                availability: new ModifierAvailabilityDefinition(access: ModifierAccess.PermanentUnlock)) },
                new[] { new ModifierRewardPoolDefinition("pool.journey-test", new[] { modifier.Id }) });
            journeys = Create<EncounterJourneyCatalogAsset>();
            SyntheticUi.Set(journeys, "chapters", new[] { Chapter("chapter.journeys", Journey(FirstName, "Harbor", "Ferryman"),
                Journey(SecondName, "Observatory", "Astronomer")) });
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "Docs", "work",
                "encounter-journey-file-tests", Guid.NewGuid().ToString("N")));
            savePath = Path.Combine(directory, "encounter.json");
            TestContext.WriteLine("Retained journey test files: " + directory);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
            charms.Dispose();
        }

        [Test]
        public void StoryBeatDiscoversFamilyAndInvitationGrantsCharmBeforeStarting()
        {
            SetBeat(journeys.Entries[0], 0, "progressionReward", Reward(true, false));
            SetBeat(journeys.Entries[0], 3, "progressionReward", Reward(false, true));
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            controller.SelectEncounter(journeys.Find(FirstId));
            Assert.That(controller.ProfileDiscoveredFamilyIds, Has.No.Member(GiftFamilyId));
            controller.AdvanceJourney();
            Assert.That(controller.ProfileDiscoveredFamilyIds, Does.Contain(GiftFamilyId));
            Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).DiscoveredFamilyIds, Does.Contain(GiftFamilyId));
            controller.AdvanceJourney();
            controller.AdvanceJourney();
            controller.AcceptEncounter();
            Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(GiftId));
            Assert.That(controller.Snapshot.DiscoveredFamilyIds, Does.Contain(GiftFamilyId));
            controller.ReturnToInvestigation();
            controller.SelectEncounter(journeys.Find(FirstId));
            controller.SkipIntro();
            controller.AcceptEncounter();
            Assert.That(controller.ProfileUnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
        }

        [Test]
        public void FailedStoryRewardSaveLeavesBeatAndOwnershipUnchangedUntilRetry()
        {
            SetBeat(journeys.Entries[0], 0, "progressionReward", Reward(true, true));
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            PlayerProfileFileStore.Save(controller.ProfilePath, new PlayerProfile());
            controller.SelectEncounter(journeys.Find(FirstId));
            using (var lockedProfile = new FileStream(controller.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                controller.AdvanceJourney();
                Assert.That(controller.BeatIndex, Is.Zero);
                Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
                Assert.That(controller.ProfileDiscoveredFamilyIds, Has.No.Member(GiftFamilyId));
                Assert.That(controller.JourneyStatus.Profile.Kind, Is.EqualTo(PlayerProfileStatusKind.RewardFailed));
            }
            controller.AdvanceJourney();
            Assert.That(controller.BeatIndex, Is.EqualTo(1));
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds, Does.Contain(GiftId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdvancingOrSkippingAnIntroPublishesJourneyOnlyAfterApplyingRewards(bool skip)
        {
            var profile = new PlayerProfile();
            profile.VisitEncounter(FirstId);
            PlayerProfileFileStore.Save(Path.Combine(Path.GetDirectoryName(savePath), "spirit-profile.json"), profile);
            SetBeat(journeys.Entries[0], 0, "progressionReward", Reward(true, true));
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            controller.SelectEncounter(journeys.Find(FirstId));
            int journeyNotifications = 0;
            int stateNotifications = 0;
            controller.JourneyChanged += () =>
            {
                ++journeyNotifications;
                Assert.That(controller.BeatIndex, Is.EqualTo(skip ? 3 : 1));
                Assert.That(controller.ProfileUnlockedModifierIds, Does.Contain(GiftId));
            };
            controller.StateChanged += () => ++stateNotifications;

            if (skip) controller.SkipIntro();
            else controller.AdvanceJourney();

            Assert.That(journeyNotifications, Is.EqualTo(1));
            Assert.That(stateNotifications, Is.Zero);
        }

        [Test]
        public void VictoryAwardsAuthoredCompletionRewardAndReloadIsIdempotent()
        {
            var winningCatalog = new MechanicalCatalog("test.story-reward.v1", TestContent.Hands(), new[]
            {
                new EncounterDefinition(FirstId, new[] { new StageDefinition(1, 2, 3) }),
                new EncounterDefinition(SecondId, new[] { new StageDefinition(1000000, 5, 3) })
            }, catalog.Modifiers);
            SyntheticUi.Set(journeys.Entries[0], "completionReward", Reward(true, true));
            Assert.That(controller.InitializeJourney(winningCatalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            controller.CompletePresentation();
            controller.PlayHand();
            controller.CompletePresentation();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
            controller.Save();
            controller.Load();
            Assert.That(controller.ProfileUnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
        }

        private ProgressionRewardAsset Reward(bool discover, bool grant)
        {
            var reward = Create<ProgressionRewardAsset>();
            SyntheticUi.Set(reward, "discoveredFamilies", discover ? new[] { charms.Family(GiftFamily) } : Array.Empty<ModifierFamilyAsset>());
            SyntheticUi.Set(reward, "grantedModifiers", grant ? new[] { charms.Charm(GiftId, GiftFamily) } : Array.Empty<ModifierAsset>());
            return reward;
        }

        [Test]
        public void PrerequisitesGateNewLeadsButPreservePreviouslyVisitedEncounters()
        {
            SyntheticUi.Set(journeys.Entries[1], "requiredCompletedEncounters", new[] { journeys.Entries[0].Encounter });
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            Assert.That(controller.IsJourneyAvailable(journeys.Find(FirstId)), Is.True);
            Assert.That(controller.IsJourneyAvailable(journeys.Find(SecondId)), Is.False);
            controller.SelectEncounter(journeys.Find(SecondId));
            Assert.That(controller.SelectedJourney, Is.Null);
            var saved = new PlayerProfile();
            saved.VisitEncounter(SecondId);
            PlayerProfileFileStore.Save(controller.ProfilePath, saved);
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            Assert.That(controller.IsJourneyAvailable(journeys.Find(SecondId)), Is.True);
            saved.CompleteEncounter(FirstId);
            PlayerProfileFileStore.Save(controller.ProfilePath, saved);
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            controller.SelectEncounter(journeys.Find(SecondId));
            Assert.That(controller.SelectedJourney.Encounter.Identity, Is.EqualTo(SecondId));
        }

        [Test]
        public void StoryCompletionPersistsRewardsWithoutStartingOrSavingAnEncounter()
        {
            var story = AddStory();
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            controller.SelectEncounter(story);
            Assert.That(controller.CanSkipIntro, Is.False);
            controller.AcceptEncounter();
            Assert.That(controller.Snapshot, Is.Null);
            controller.AdvanceJourney();
            Assert.That(controller.CompletedStoryIds, Is.Empty);
            controller.AdvanceJourney();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(File.Exists(savePath), Is.False);
            var saved = PlayerProfileFileStore.Load(controller.ProfilePath);
            Assert.That(saved.CompletedStoryIds, Is.EqualTo(new[] { story.StoryId }));
            Assert.That(saved.UnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
            Assert.That(saved.DiscoveredFamilyIds, Does.Contain(GiftFamilyId));
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            controller.SelectEncounter(story);
            controller.AdvanceJourney();
            controller.AdvanceJourney();
            Assert.That(controller.ProfileUnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
            Assert.That(controller.CompletedStoryIds, Is.EqualTo(new[] { story.StoryId }));
        }

        [Test]
        public void StoryCompletionRetainsAnExistingRunAndItsSave()
        {
            var story = AddStory();
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            controller.CompletePresentation();
            controller.Save();
            byte[] savedRun = File.ReadAllBytes(savePath);
            var before = controller.Snapshot;
            controller.ReturnToInvestigation();
            controller.SelectEncounter(story);
            controller.AdvanceJourney();
            controller.AdvanceJourney();
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(before.EncounterId));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(before.Revision));
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(savedRun));
            Assert.That(controller.CanResume, Is.True);
        }

        [Test]
        public void StoryCompletionPublishesRetainedRunRewardsToStateAndJourneyObservers()
        {
            var story = AddStory();
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            var retained = controller.Snapshot;
            controller.ReturnToInvestigation();
            controller.SelectEncounter(story);
            controller.AdvanceJourney();
            int journeyChanges = 0;
            int stateChanges = 0;
            Action assertCompletedStory = () =>
            {
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
                Assert.That(controller.SelectedJourney, Is.Null);
                Assert.That(controller.BeatIndex, Is.EqualTo(-1));
                Assert.That(controller.CompletedStoryIds, Does.Contain(story.StoryId));
                Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(retained.EncounterId));
                Assert.That(controller.Snapshot.Revision, Is.EqualTo(retained.Revision));
                Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(retained.CurrentDice));
                Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(GiftId));
                Assert.That(controller.Snapshot.DiscoveredFamilyIds, Does.Contain(GiftFamilyId));
                Assert.That(controller.Preview, Is.Not.Null);
                Assert.That(controller.Preview.RerollsRemaining, Is.EqualTo(controller.Snapshot.RerollsRemaining));
                Assert.That(controller.Preview.Modifiers.Select(modifier => modifier.DefinitionId),
                    Is.EqualTo(controller.Snapshot.Modifiers.Select(modifier => modifier.DefinitionId)));
                Assert.That(controller.ModifierInspections.Select(inspection => inspection.DefinitionId),
                    Is.EqualTo(controller.Snapshot.Modifiers.Select(modifier => modifier.DefinitionId)));
                Assert.That(controller.CanResume, Is.True);
            };
            controller.JourneyChanged += () => { ++journeyChanges; assertCompletedStory(); };
            controller.StateChanged += () => { ++stateChanges; assertCompletedStory(); };

            controller.AdvanceJourney();

            Assert.That(journeyChanges, Is.GreaterThan(0));
            Assert.That(stateChanges, Is.GreaterThan(0));
        }

        [Test]
        public void FailedStoryCompletionSaveRetainsTheFinalBeatAndDoesNotGrantRewards()
        {
            var story = AddStory();
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            PlayerProfileFileStore.Save(controller.ProfilePath, new PlayerProfile());
            controller.SelectEncounter(story);
            controller.AdvanceJourney();
            using (var lockedProfile = new FileStream(controller.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                controller.AdvanceJourney();
                Assert.That(controller.BeatIndex, Is.EqualTo(1));
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
                Assert.That(controller.CompletedStoryIds, Is.Empty);
                Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
                Assert.That(controller.JourneyStatus.Profile.Kind, Is.EqualTo(PlayerProfileStatusKind.StoryFailed));
            }
            controller.AdvanceJourney();
            Assert.That(controller.CompletedStoryIds, Does.Contain(story.StoryId));
            Assert.That(controller.ProfileUnlockedModifierIds, Does.Contain(GiftId));
        }

        private EncounterJourneyAsset AddStory()
        {
            var story = Journey(null, "A return visit", "The guide");
            SyntheticUi.Set(story, "storyId", "story.return");
            SetBeats(story, Beat("report", EncounterJourneyBeatKind.Dialogue),
                Beat("prepare", EncounterJourneyBeatKind.Dialogue));
            SyntheticUi.Set(story, "completionReward", Reward(true, true));
            SyntheticUi.Set(journeys, "chapters", new[] { Chapter("chapter.journeys", journeys.Entries[0], journeys.Entries[1], story) });
            return story;
        }

        [Test]
        public void DirectStoryGrantUpdatesAnActiveRunWithoutEquippingTheCharm()
        {
            controller.Initialize(catalog, FirstId, 17, savePath);
            Assert.That(controller.ApplyProgressionReward(Reward(false, true).Build(catalog)), Is.True);
            Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(GiftId));
            Assert.That(controller.Snapshot.DiscoveredFamilyIds, Does.Contain(GiftFamilyId));
            Assert.That(controller.Snapshot.Modifiers.Select(modifier => modifier.DefinitionId), Does.Not.Contain(GiftId));
            Assert.That(controller.ApplyProgressionReward(Reward(false, true).Build(catalog)), Is.True);
            Assert.That(controller.ProfileUnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
        }

        [Test]
        public void AutomaticContinuationIsOptInAndAdvancingNeverAcceptsAnInvitation()
        {
            var journey = journeys.Entries[0];
            Assert.That(journey.Beats.All(beat => !beat.AutoAdvance), Is.True);
            SetBeat(journey, 0, "autoAdvance", true);
            SetBeat(journey, journey.Beats.Count - 1, "autoAdvance", true);
            Assert.That(journey.Beats[0].AutoAdvance, Is.True);
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AdvanceJourney();
            controller.AdvanceJourney();
            Assert.That(controller.BeatIndex, Is.EqualTo(journey.Beats.Count - 1));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            controller.AcceptEncounter();
            Assert.That(controller.Snapshot, Is.Not.Null);
        }

        [Test]
        public void InitializationFailuresExposeMessageWithoutCreatingApplication()
        {
            SetBeats(journeys.Entries[0]);
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.False);
            Assert.That(controller.Status.Kind, Is.EqualTo(SpiritStatusKind.InvestigationFailed));
            Assert.That(controller.JourneyStatus, Is.SameAs(controller.Status));
            Assert.That(controller.Catalog, Is.Null);
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.Application, Is.Null);
        }

        [Test]
        public void InvestigationAndFirstVisitDoNotCreateOrMutateEngineState()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.Application, Is.Null);
            ulong seed = controller.Seed;
            controller.SelectEncounter(journeys.Find(FirstId));
            controller.SkipIntro();
            controller.AcceptEncounter();
            controller.Reroll();
            controller.PlayHand();
            controller.AdvanceJourney();
            controller.ReturnToInvestigation();
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.Application, Is.Null);
            Assert.That(controller.Seed, Is.EqualTo(seed));
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            Assert.That(File.Exists(controller.ProfilePath), Is.False);
            Assert.That(File.Exists(savePath), Is.False);
        }

        [Test]
        public void AcceptanceStartsSelectedDataOnceAndPresentsTheOpeningRoll()
        {
            // The second catalog entry, so starting the first entry instead would fail.
            var id = SecondId;
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            int presented = 0;
            controller.Resolved += resolution =>
            {
                ++presented;
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
                Assert.That(resolution.PreviousRevision, Is.EqualTo(-1));
                Assert.That(resolution.Events.OfType<DiceRolledEvent>().Single().Kind, Is.EqualTo(RollKind.Initial));
                Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).VisitedEncounterIds, Does.Contain(id));
                controller.CompletePresentation();
            };
            SelectInvitation(id);
            Assert.That(controller.Snapshot, Is.Null);
            controller.AcceptEncounter();
            var snapshot = controller.Snapshot;
            controller.AcceptEncounter();
            Assert.That(presented, Is.EqualTo(1));
            Assert.That(controller.Snapshot, Is.SameAs(snapshot));
            Assert.That(snapshot.EncounterId, Is.EqualTo(id));
            Assert.That(snapshot.Revision, Is.Zero);
            Assert.That(snapshot.TotalHandsPlayed, Is.Zero);
            Assert.That(snapshot.TotalRerollsSpent, Is.Zero);
            Assert.That(snapshot.CurrentDice, Is.EqualTo(new EncounterApplication(catalog, id, 17).Snapshot.CurrentDice));
        }

        [Test]
        public void ReinitializingJourneyPublishesInvestigationWhileRetainingTheExistingRun()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            var retained = controller.Snapshot;
            var retainedPreview = controller.Preview;
            var retainedInspections = controller.ModifierInspections;
            int journeyChanges = 0;
            int stateChanges = 0;
            Action assertInvestigation = () =>
            {
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
                Assert.That(controller.SelectedJourney, Is.Null);
                Assert.That(controller.BeatIndex, Is.EqualTo(-1));
                Assert.That(controller.Snapshot, Is.SameAs(retained));
                Assert.That(controller.Preview, Is.SameAs(retainedPreview));
                Assert.That(controller.ModifierInspections, Is.SameAs(retainedInspections));
                Assert.That(controller.CanResume, Is.True);
            };
            controller.JourneyChanged += () => { ++journeyChanges; assertInvestigation(); };
            controller.StateChanged += () => { ++stateChanges; assertInvestigation(); };

            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);

            Assert.That(journeyChanges, Is.GreaterThan(0));
            Assert.That(stateChanges, Is.GreaterThan(0));
        }

        [Test]
        public void AcceptingAnotherEncounterPublishesItsRunAndCachesBeforePlayingNotifications()
        {
            var observableCatalog = CreateObservableRunCatalog();
            Assert.That(controller.InitializeJourney(observableCatalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            var previous = controller.Snapshot;
            var previousPreview = controller.Preview;
            Assert.That(controller.ModifierInspections, Has.Count.EqualTo(1));
            controller.ReturnToInvestigation();
            SelectInvitation(SecondId);
            var expected = new EncounterApplication(observableCatalog, SecondId, 17);
            int journeyChanges = 0;
            int stateChanges = 0;
            controller.JourneyChanged += () =>
            {
                if (controller.JourneyPhase != EncounterJourneyPhase.Playing) return;
                ++journeyChanges;
                AssertPublishedRun(expected, previous, previousPreview);
            };
            controller.StateChanged += () =>
            {
                if (controller.JourneyPhase != EncounterJourneyPhase.Playing) return;
                ++stateChanges;
                AssertPublishedRun(expected, previous, previousPreview);
            };

            controller.AcceptEncounter();

            Assert.That(journeyChanges, Is.GreaterThan(0));
            Assert.That(stateChanges, Is.GreaterThan(0));
        }

        [Test]
        public void LoadingAnotherEncounterPublishesItsRunAndCachesBeforePlayingNotifications()
        {
            var observableCatalog = CreateObservableRunCatalog();
            Assert.That(controller.InitializeJourney(observableCatalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            var previous = controller.Snapshot;
            var previousPreview = controller.Preview;
            Assert.That(controller.ModifierInspections, Has.Count.EqualTo(1));
            var expected = new EncounterApplication(observableCatalog, SecondId, 29);
            Assert.That(expected.Submit(new RerollDiceAction(1), expected.Snapshot.Revision).Accepted, Is.True);
            EncounterSaveFileStore.Save(savePath, expected.CaptureSave());
            int journeyChanges = 0;
            int stateChanges = 0;
            int resolutions = 0;
            controller.Resolved += resolution => ++resolutions;
            controller.JourneyChanged += () =>
            {
                if (controller.JourneyPhase != EncounterJourneyPhase.Playing) return;
                ++journeyChanges;
                AssertPublishedRun(expected, previous, previousPreview);
            };
            controller.StateChanged += () =>
            {
                if (controller.JourneyPhase != EncounterJourneyPhase.Playing) return;
                ++stateChanges;
                AssertPublishedRun(expected, previous, previousPreview);
            };

            controller.Load();

            Assert.That(journeyChanges, Is.GreaterThan(0));
            Assert.That(stateChanges, Is.GreaterThan(0));
            Assert.That(resolutions, Is.Zero);
        }

        private MechanicalCatalog CreateObservableRunCatalog()
            => new MechanicalCatalog("test.journey-notifications.v1", TestContent.Hands(), new[]
            {
                new EncounterDefinition(FirstId, new[] { new StageDefinition(1000000, 5, 3) },
                    startingModifiers: new[] { "modifier.journey-test" }),
                new EncounterDefinition(SecondId, new[] { new StageDefinition(1000000, 5, 7) })
            }, catalog.Modifiers);

        private void AssertPublishedRun(EncounterApplication expected, EncounterSnapshot previous, HandPreview previousPreview)
        {
            Assert.That(controller.SelectedJourney, Is.SameAs(journeys.Find(expected.Snapshot.EncounterId)));
            Assert.That(controller.Snapshot, Is.Not.SameAs(previous));
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(expected.Snapshot.EncounterId));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(expected.Snapshot.Revision));
            Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(expected.Snapshot.CurrentDice));
            Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(expected.Snapshot.RerollsRemaining));
            Assert.That(controller.Preview, Is.Not.Null);
            Assert.That(controller.Preview, Is.Not.SameAs(previousPreview));
            var expectedPreview = expected.PreviewHandResolution();
            Assert.That(controller.Preview.RerollsRemaining, Is.EqualTo(expectedPreview.RerollsRemaining));
            Assert.That(controller.Preview.Modifiers.Select(modifier => modifier.DefinitionId),
                Is.EqualTo(expectedPreview.Modifiers.Select(modifier => modifier.DefinitionId)));
            Assert.That(controller.ModifierInspections.Select(inspection => inspection.DefinitionId),
                Is.EqualTo(expected.InspectModifiersForPlayHand().Select(inspection => inspection.DefinitionId)));
        }

        [Test]
        public void RepeatedVisitCanSkipButCancelSelectionDoesNotMarkAnotherEncounterVisited()
        {
            controller.InitializeJourney(catalog, journeys, savePath);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            var active = controller.Snapshot;
            controller.ReturnToInvestigation();
            controller.SelectEncounter(journeys.Find(SecondId));
            Assert.That(controller.CanSkipIntro, Is.False);
            controller.SkipIntro();
            Assert.That(controller.BeatIndex, Is.Zero);
            controller.ReturnToInvestigation();
            controller.SelectEncounter(journeys.Find(FirstId));
            Assert.That(controller.CanSkipIntro, Is.True);
            controller.SkipIntro();
            Assert.That(controller.BeatIndex, Is.EqualTo(controller.SelectedJourney.Beats.Count - 1));
            Assert.That(controller.Snapshot, Is.SameAs(active));
            Assert.That(controller.VisitedEncounterIds, Is.EqualTo(new[] { FirstId }));
        }

        [Test]
        public void ReturnSelectionAndResumePreservePendingOfferAndGameplayState()
        {
            controller.InitializeJourney(catalog, journeys, savePath);
            SelectInvitation(SecondId);
            controller.AcceptEncounter();
            controller.PlayHand();
            var pending = controller.Snapshot;
            Assert.That(pending.PendingReward, Is.Not.Null);
            controller.ReturnToInvestigation();
            Assert.That(controller.CanResume, Is.True);
            controller.SelectEncounter(journeys.Find(FirstId));
            controller.PlayHand();
            controller.Abandon();
            Assert.That(controller.Snapshot, Is.SameAs(pending));
            controller.ReturnToInvestigation();
            controller.ResumeEncounter();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.SelectedJourney.Encounter.Identity, Is.EqualTo(SecondId));
            Assert.That(controller.BeatIndex, Is.EqualTo(-1));
            Assert.That(controller.Snapshot, Is.SameAs(pending));
            var restored = EncounterSession.RestoreEncounter(catalog, EncounterSaveFileStore.Load(savePath, catalog));
            Assert.That(restored.Success, Is.True);
            EncounterSessionTests.AssertSnapshotsEqual(pending, restored.Session.Snapshot);
        }

        [Test]
        public void LoadedEncounterBypassesIntroAndPreservesPendingOfferWithoutOpeningPlayback()
        {
            var existing = new EncounterApplication(catalog, SecondId, 17);
            existing.Submit(PlayHandAction.Instance, existing.Snapshot.Revision);
            Assert.That(existing.Snapshot.PendingReward, Is.Not.Null);
            EncounterSaveFileStore.Save(savePath, existing.CaptureSave());
            controller.InitializeJourney(catalog, journeys, savePath);
            int resolutions = 0;
            controller.Resolved += resolution => ++resolutions;
            controller.Load();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.SelectedJourney.Encounter.Identity, Is.EqualTo(SecondId));
            Assert.That(controller.BeatIndex, Is.EqualTo(-1));
            Assert.That(resolutions, Is.Zero);
            Assert.That(controller.Snapshot.PendingReward.OfferId, Is.EqualTo(existing.Snapshot.PendingReward.OfferId));
            Assert.That(controller.Snapshot.PendingReward.Options.Select(option => option.OptionId),
                Is.EqualTo(existing.Snapshot.PendingReward.Options.Select(option => option.OptionId)));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(existing.Snapshot.Revision));
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
        }

        [Test]
        public void CorruptProfileBlocksAcceptanceAndLeavesItsBytesAndEngineUntouched()
        {
            controller.InitializeJourney(catalog, journeys, savePath);
            SelectInvitation(FirstId);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(controller.ProfilePath, "{ broken external profile");
            ulong seed = controller.Seed;
            controller.AcceptEncounter();
            Assert.That(controller.JourneyStatus.Profile.Kind, Is.EqualTo(PlayerProfileStatusKind.VisitFailed));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.Seed, Is.EqualTo(seed));
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            Assert.That(File.ReadAllText(controller.ProfilePath), Is.EqualTo("{ broken external profile"));
            controller.ReturnToInvestigation();
            controller.SelectEncounter(journeys.Find(FirstId));
            Assert.That(controller.CanSkipIntro, Is.False);
        }

        [Test]
        public void JourneyValidationRejectsInvitationOrderUnknownEncounterAndRuleMismatch()
        {
            var journey = journeys.Entries[0];
            SetBeats(journey, Beat("invitation", EncounterJourneyBeatKind.Invitation), Beat("later", EncounterJourneyBeatKind.Dialogue));
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.False);
            Assert.That(controller.JourneyStatus.Kind, Is.EqualTo(SpiritStatusKind.InvestigationFailed));
            Assert.That(controller.JourneyStatus.Detail, Does.Contain("final journey beat"));
            SetBeats(journey, Beat("invite-one", EncounterJourneyBeatKind.Invitation), Beat("invite-two", EncounterJourneyBeatKind.Invitation));
            Assert.Throws<FormatException>(() => journeys.Validate(catalog));
            SetBeats(journey, Beat("invitation", EncounterJourneyBeatKind.Invitation));
            var encounter = journey.Encounter;
            SyntheticUi.Set(journey, "encounter", charms.Encounter("encounter.missing"));
            Assert.Throws<KeyNotFoundException>(() => journeys.Validate(catalog));
            SyntheticUi.Set(journey, "encounter", encounter);
            var rule = Create<EncounterRulePresentationAsset>();
            SyntheticUi.Set(rule, "ruleId", FirstRerollFreeRuleDefinition.StableId);
            SyntheticUi.Set(journey.TablePresentation, "rulePresentations", new[] { rule });
            Assert.Throws<FormatException>(() => journeys.Validate(catalog));
        }

        [Test]
        public void OutcomePresentationIsOptionalAndEnabledPerOutcome()
        {
            var journey = journeys.Entries[0];
            foreach (EncounterOutcome outcome in Enum.GetValues(typeof(EncounterOutcome)))
                Assert.That(journey.GetOutcomePresentation(outcome), Is.Null);
            SyntheticUi.Set(journey, "victoryPresentation.holdDuration", 2f);
            Assert.That(journey.GetOutcomePresentation(EncounterOutcome.Won), Is.Null, "An authored but disabled outcome is not used.");
            SyntheticUi.Set(journey, "victoryPresentation.enabled", true);
            Assert.That(journey.GetOutcomePresentation(EncounterOutcome.Won)?.HoldDuration, Is.EqualTo(2f));
            Assert.That(journey.GetOutcomePresentation(EncounterOutcome.Lost), Is.Null);
            SyntheticUi.Set(journey, "victoryPresentation.holdDuration", float.NaN);
            Assert.Throws<FormatException>(() => journey.Validate(catalog));
            SyntheticUi.Set(journey, "victoryPresentation.holdDuration", 0f);
            SyntheticUi.Set(journey, "victoryPresentation.fadeDuration", -1f);
            Assert.Throws<FormatException>(() => journey.Validate(catalog));
        }

        [TestCase("duplicate")]
        [TestCase("self")]
        [TestCase("missing")]
        public void FollowUpReferencesMustBeUniqueOtherCatalogJourneys(string invalid)
        {
            var second = journeys.Entries[1].Encounter;
            SyntheticUi.Set(journeys.Entries[0], "followUpEncounters", new[] { second });
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
            SyntheticUi.Set(journeys.Entries[0], "followUpEncounters", invalid == "duplicate" ? new[] { second, second } :
                new[] { invalid == "self" ? journeys.Entries[0].Encounter : charms.Encounter("encounter.missing") });
            Assert.Throws<FormatException>(() => journeys.Validate(catalog));
        }

        [Test]
        public void RetryRequiresCompletedPresentationAndWaitsAtSameInvitation()
        {
            controller.InitializeJourney(catalog, journeys, savePath);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            controller.RetryEncounter();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            controller.Resolved += resolution => { };
            controller.Abandon();
            var completed = controller.Snapshot;
            var application = controller.Application;
            byte[] profileBytes = File.ReadAllBytes(controller.ProfilePath);
            controller.RetryEncounter();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            controller.CompletePresentation();
            controller.RetryEncounter();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(controller.SelectedJourney.Encounter.Identity, Is.EqualTo(FirstId));
            Assert.That(controller.BeatIndex, Is.EqualTo(controller.SelectedJourney.Beats.Count - 1));
            Assert.That(controller.Snapshot, Is.SameAs(completed));
            Assert.That(controller.Application, Is.SameAs(application));
            Assert.That(File.ReadAllBytes(controller.ProfilePath), Is.EqualTo(profileBytes));
            controller.AcceptEncounter();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None));
            Assert.That(controller.Snapshot.TotalHandsPlayed, Is.Zero);
            Assert.That(controller.VisitedEncounterIds, Is.EqualTo(new[] { FirstId }));
        }

        [Test]
        public void FirstVictoryHighlightsUnvisitedFollowUpsUntilSelectedAndRepeatVictoryDoesNotRepeatThem()
        {
            InitializeWinningJourney();
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            controller.PlayHand();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(controller.HighlightedJourneys, Is.EqualTo(new[] { journeys.Find(SecondId) }));
            Assert.That(controller.CompletedEncounterIds, Is.EqualTo(new[] { FirstId }));
            controller.ReturnToInvestigation();
            Assert.That(controller.HighlightedJourneys, Is.EqualTo(new[] { journeys.Find(SecondId) }));
            controller.SelectEncounter(journeys.Find(SecondId));
            Assert.That(controller.HighlightedJourneys, Is.Empty);
            controller.ReturnToInvestigation();
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            controller.PlayHand();
            Assert.That(controller.HighlightedJourneys, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstVictoryDoesNotHighlightVisitedOrCompletedFollowUps(bool completed)
        {
            var profile = new PlayerProfile();
            if (completed) profile.CompleteEncounter(SecondId);
            else profile.VisitEncounter(SecondId);
            string profilePath = Path.Combine(Path.GetDirectoryName(savePath), "spirit-profile.json");
            PlayerProfileFileStore.Save(profilePath, profile);
            InitializeWinningJourney();
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            controller.PlayHand();
            Assert.That(controller.HighlightedJourneys, Is.Empty);
            controller.ReturnToInvestigation();
            controller.SelectEncounter(journeys.Find(SecondId));
            Assert.That(controller.SelectedJourney.Encounter.Identity, Is.EqualTo(SecondId));
        }

        [Test]
        public void LoadingCompletedEncounterRecordsProgressWithoutCreatingNewLeadHighlights()
        {
            InitializeWinningJourney();
            var existing = new EncounterApplication(controller.Catalog, FirstId, 17);
            existing.Submit(PlayHandAction.Instance, existing.Snapshot.Revision);
            Assert.That(existing.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            EncounterSaveFileStore.Save(savePath, existing.CaptureSave());
            controller.Load();
            Assert.That(controller.CompletedEncounterIds, Is.EqualTo(new[] { FirstId }));
            Assert.That(controller.HighlightedJourneys, Is.Empty);
            controller.ReturnToInvestigation();
            Assert.That(controller.HighlightedJourneys, Is.Empty);
        }

        private void InitializeWinningJourney()
        {
            var winningCatalog = new MechanicalCatalog("test.journey-outcome.v1", TestContent.Hands(), new[]
            {
                new EncounterDefinition(FirstId, new[] { new StageDefinition(1, 2, 3) }),
                new EncounterDefinition(SecondId, new[] { new StageDefinition(1000000, 5, 3) })
            });
            SyntheticUi.Set(journeys.Entries[0], "followUpEncounters", new[] { journeys.Entries[1].Encounter });
            Assert.That(controller.InitializeJourney(winningCatalog, journeys, savePath), Is.True);
        }

        [Test]
        public void MenuInitializationClearsRunAndProfileMemoryWithoutReadingTheSelectedProfile()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            controller.ToggleDie(2);
            byte[] savedRun = File.ReadAllBytes(savePath);
            string profilePath = controller.ProfilePath;
            const string corrupt = "{ broken profile";
            File.WriteAllText(profilePath, corrupt);
            int notifications = 0;
            Action assertMenu = () =>
            {
                ++notifications;
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
                Assert.That(controller.Snapshot, Is.Null);
                Assert.That(controller.Preview, Is.Null);
                Assert.That(controller.ModifierInspections, Is.Empty);
                Assert.That(controller.SelectedMask, Is.Zero);
                Assert.That(controller.ProfilePath, Is.Null);
                Assert.That(controller.VisitedEncounterIds, Is.Empty);
            };
            controller.JourneyChanged += assertMenu;
            controller.StateChanged += assertMenu;
            Assert.That(controller.InitializeMenu(catalog, journeys, savePath), Is.True);
            controller.JourneyChanged -= assertMenu;
            controller.StateChanged -= assertMenu;
            Assert.That(notifications, Is.EqualTo(2));
            Assert.That(controller.Catalog, Is.SameAs(catalog));
            Assert.That(controller.JourneyCatalog, Is.SameAs(journeys));
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo(corrupt));
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(savedRun));
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.False);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(controller.Snapshot, Is.Null);
        }

        [Test]
        public void MenuInitializationDoesNotMergeThePreviousInMemoryProfile()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            string nextSavePath = Path.Combine(Path.GetDirectoryName(savePath), "another-campaign", "encounter.json");
            Assert.That(controller.InitializeMenu(catalog, journeys, nextSavePath), Is.True);
            Assert.That(controller.InitializeJourney(catalog, journeys, nextSavePath), Is.True);
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            Assert.That(controller.Snapshot, Is.Null);
        }

        [Test]
        public void ShowingMainMenuRetainsTheRunAndProfileWithoutWriting()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            controller.ToggleDie(2);
            var before = controller.Snapshot;
            var preview = controller.Preview;
            var inspections = controller.ModifierInspections;
            byte[] savedRun = File.ReadAllBytes(savePath);
            byte[] savedProfile = File.ReadAllBytes(controller.ProfilePath);
            Assert.That(controller.ShowMainMenu(), Is.True);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(controller.SelectedJourney, Is.Null);
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.ModifierInspections, Is.SameAs(inspections));
            Assert.That(controller.SelectedMask, Is.EqualTo(4));
            Assert.That(controller.VisitedEncounterIds, Does.Contain(FirstId));
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(savedRun));
            Assert.That(File.ReadAllBytes(controller.ProfilePath), Is.EqualTo(savedProfile));
        }

        [Test]
        public void MainMenuAndInitializationAreBlockedDuringPresentation()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.Resolved += _ => { };
            controller.AcceptEncounter();
            var before = controller.Snapshot;
            int changes = 0;
            controller.JourneyChanged += () => ++changes;
            controller.StateChanged += () => ++changes;
            Assert.That(controller.ShowMainMenu(), Is.False);
            Assert.That(controller.InitializeMenu(catalog, journeys, savePath), Is.False);
            Assert.That(controller.OpenSavedChapter(), Is.False);
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void InvalidMenuContentPreservesTheActiveRunAndProgress()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            var before = controller.Snapshot;
            var preview = controller.Preview;
            string profile = controller.ProfilePath;
            var invalid = Create<EncounterJourneyCatalogAsset>();
            Assert.That(controller.InitializeMenu(catalog, invalid, savePath), Is.False);
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.JourneyCatalog, Is.SameAs(journeys));
            Assert.That(controller.ProfilePath, Is.EqualTo(profile));
            Assert.That(controller.VisitedEncounterIds, Does.Contain(FirstId));
        }

        [Test]
        public void EveryJourneyActionSavesCommittedStateBeforePresentation()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            int resolutions = 0;
            controller.Resolved += resolution =>
            {
                ++resolutions;
                var loaded = EncounterSession.RestoreEncounter(catalog, EncounterSaveFileStore.Load(savePath, catalog));
                Assert.That(loaded.Success, Is.True);
                EncounterSessionTests.AssertSnapshotsEqual(resolution.Snapshot, loaded.Session.Snapshot);
                controller.CompletePresentation();
            };
            controller.AcceptEncounter();
            controller.ToggleDie(0);
            controller.Reroll();
            controller.PlayHand();
            Assert.That(resolutions, Is.EqualTo(3));
        }

        [Test]
        public void FailedOrdinaryAutosaveBlocksFurtherActionsUntilSaved()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            long revision = controller.Snapshot.Revision;
            using (var lockedRun = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                controller.ToggleDie(0);
                controller.Reroll();
                Assert.That(controller.Snapshot.Revision, Is.EqualTo(revision + 1));
                Assert.That(controller.Status.Profile.Kind, Is.EqualTo(PlayerProfileStatusKind.EncounterSavePending));
                controller.PlayHand();
                Assert.That(controller.Snapshot.Revision, Is.EqualTo(revision + 1));
                Assert.That(controller.TrySave(), Is.False);
            }
            Assert.That(controller.TrySave(), Is.True);
            var saved = EncounterSaveFileStore.Load(savePath, catalog);
            Assert.That(saved.Revision, Is.EqualTo(controller.Snapshot.Revision));
            controller.PlayHand();
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(revision + 2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OpeningSavedChapterRetainsSavedRunOrSelectsProfileDefault(bool hasRun)
        {
            var first = Chapter("chapter.first", journeys.Entries[0]);
            var second = Chapter("chapter.second", journeys.Entries[1]);
            SyntheticUi.Set(journeys, "chapters", new[] { first, second });
            var profile = new PlayerProfile();
            profile.CompleteEncounter(FirstId);
            string profilePath = Path.Combine(Path.GetDirectoryName(savePath), "spirit-profile.json");
            PlayerProfileFileStore.Save(profilePath, profile);
            byte[] runBytes = null;
            EncounterApplication savedRun = null;
            if (hasRun)
            {
                savedRun = new EncounterApplication(catalog, FirstId, 17);
                Assert.That(savedRun.Submit(new RerollDiceAction(1), savedRun.Snapshot.Revision).Accepted, Is.True);
                EncounterSaveFileStore.Save(savePath, savedRun.CaptureSave());
                runBytes = File.ReadAllBytes(savePath);
            }
            byte[] profileBytes = File.ReadAllBytes(profilePath);
            Assert.That(controller.InitializeMenu(catalog, journeys, savePath), Is.True);
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            Assert.That(controller.SelectedChapter, Is.SameAs(second));
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            Assert.That(controller.OpenSavedChapter(), Is.True);
            Assert.That(controller.SelectedChapter, Is.SameAs(hasRun ? first : second));
            if (hasRun) EncounterSessionTests.AssertSnapshotsEqual(savedRun.Snapshot, controller.Snapshot);
            else Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.HasActiveEncounter, Is.EqualTo(hasRun));
            Assert.That(controller.CanResume, Is.EqualTo(hasRun));
            Assert.That(resolutions, Is.Zero);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(File.ReadAllBytes(profilePath), Is.EqualTo(profileBytes));
            if (hasRun)
            {
                Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(runBytes));
                controller.ResumeEncounter();
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
                Assert.That(resolutions, Is.Zero);
                controller.ToggleDie(0);
                controller.Reroll();
                savedRun.Submit(new RerollDiceAction(1), savedRun.Snapshot.Revision);
                EncounterSessionTests.AssertSnapshotsEqual(savedRun.Snapshot, controller.Snapshot);
            }
            else Assert.That(File.Exists(savePath), Is.False);
        }

        [TestCase("malformed")]
        [TestCase("rules")]
        public void InvalidSavedChapterLeavesNoLiveRunAndCanReturnToMenu(string failure)
        {
            Assert.That(controller.InitializeMenu(catalog, journeys, savePath), Is.True);
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            var json = JObject.Parse(Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(new EncounterApplication(catalog, FirstId, 17).CaptureSave())));
            json["RulesVersion"] = 99;
            string contents = failure == "malformed" ? "{ broken run" : json.ToString();
            File.WriteAllText(savePath, contents);
            Assert.That(controller.OpenSavedChapter(), Is.False);
            Assert.That(controller.JourneyStatus.Kind, Is.EqualTo(SpiritStatusKind.LoadFailed));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.ShowMainMenu(), Is.True);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(File.ReadAllText(savePath), Is.EqualTo(contents));
        }

        [Test]
        public void ChapterSelectionPublishesItsLocationAndDefaultsFromSavedProgress()
        {
            var first = Chapter("chapter.first", journeys.Entries[0]);
            var second = Chapter("chapter.second", journeys.Entries[1]);
            SyntheticUi.Set(journeys.Entries[1], "requiredCompletedEncounters", new[] { journeys.Entries[0].Encounter });
            SyntheticUi.Set(journeys, "chapters", new[] { first, second });
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            Assert.That(controller.SelectedChapter, Is.SameAs(first));
            controller.SelectChapter(second.Id);
            Assert.That(controller.SelectedChapter, Is.SameAs(first));
            Assert.That(controller.JourneyStatus.Kind, Is.EqualTo(SpiritStatusKind.ChapterLocked));
            var profile = new PlayerProfile();
            profile.CompleteEncounter(FirstId);
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            Assert.That(controller.SelectedChapter, Is.SameAs(second));
            Assert.That(controller.IsChapterAvailable(first.Id), Is.True);
            Assert.That(controller.IsChapterCompleted(first.Id), Is.True);
            Assert.That(controller.IsChapterCompleted(second.Id), Is.False);
            int changes = 0;
            controller.JourneyChanged += () =>
            {
                ++changes;
                Assert.That(controller.SelectedChapter, Is.SameAs(first));
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            };
            controller.SelectChapter(first.Id);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void StoryCompletionSelectsTheUnlockedChapterBeforeNotification()
        {
            var story = AddStory();
            var first = Chapter("chapter.first", journeys.Entries[0], story);
            var second = Chapter("chapter.second", journeys.Entries[1]);
            SyntheticUi.Set(journeys.Entries[1], "requiredCompletedStoryIds", new[] { story.StoryId });
            SyntheticUi.Set(journeys, "chapters", new[] { first, second });
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            controller.SelectEncounter(story);
            controller.AdvanceJourney();
            int changes = 0;
            controller.JourneyChanged += () =>
            {
                ++changes;
                Assert.That(controller.CompletedStoryIds, Does.Contain(story.StoryId));
                Assert.That(controller.SelectedChapter, Is.SameAs(second));
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            };
            controller.AdvanceJourney();
            Assert.That(changes, Is.EqualTo(1));
        }

        private EncounterChapterAsset Chapter(string id, params EncounterJourneyAsset[] entries)
            => TestJourneys.Chapter(Create<EncounterChapterAsset>(), id, entries);

        /** A journey of a new encounter asset of the encounter name, or of no encounter without one. */
        private EncounterJourneyAsset Journey(string encounterName, string location, string spirit)
        {
            var journey = Create<EncounterJourneyAsset>();
            if (encounterName != null) SyntheticUi.Set(journey, "encounter", charms.Encounter(encounterName));
            SyntheticUi.Set(journey, "locationTitle", location);
            SyntheticUi.Set(journey, "spiritName", spirit);
            SyntheticUi.Set(journey, "leadText", "A strange visitor waits beyond the door.");
            SyntheticUi.Set(journey, "tablePresentation", Create<EncounterTablePresentationAsset>());
            SetBeats(journey, Beat("approach", EncounterJourneyBeatKind.Illustration),
                Beat("arrival", EncounterJourneyBeatKind.Arrival),
                Beat("bargain", EncounterJourneyBeatKind.Dialogue),
                Beat("invitation", EncounterJourneyBeatKind.Invitation));
            return journey;
        }

        private void SelectInvitation(EncounterDefinition.Id id)
        {
            controller.SelectEncounter(journeys.Find(id));
            for (int index = 1; index < controller.SelectedJourney.Beats.Count; ++index) controller.AdvanceJourney();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

    }
}
