using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class EncounterJourneyTests
    {
        private const string FirstId = "encounter.harbor";
        private const string SecondId = "encounter.observatory";
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
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
            host.GetComponent<SpiritEncounterView>().enabled = false;
            Set(controller, "useFixedSeed", true);
            Set(controller, "initialSeed", "17");
            var modifier = new ModifierDefinition("modifier.journey-test", "family.journey-test", ModifierPhase.PlayHand,
                new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) });
            var policy = new RewardPolicyDefinition("policy.journey-test", new[] { new ModifierRewardLayout(new[] { "pool.journey-test" }) },
                1, 1, 1, 3, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            catalog = new MechanicalCatalog("test.journeys.v1", TestContent.Hands(), new[]
            {
                new EncounterDefinition(FirstId, new[] { new StageDefinition(1000000, 5, 3) }),
                new EncounterDefinition(SecondId, new[] { new StageDefinition(1, 2, 3, StageExitBehavior.AwaitRewards),
                    new StageDefinition(1000000, 5, 3) }, rewardPolicy: policy)
            }, new[] { modifier }, new[] { new ModifierRewardPoolDefinition("pool.journey-test", new[] { modifier.Id }) });
            journeys = Create<EncounterJourneyCatalogAsset>();
            Set(journeys, "entries", new[] { Journey(FirstId, "Harbor", "Ferryman"), Journey(SecondId, "Observatory", "Astronomer") });
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
        }

        [Test]
        public void AuthoredJourneyCatalogMatchesMechanicalContent()
        {
            var journeyScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/Dice/Presentation/EncounterJourneyAsset.cs");
            Assert.That(journeyScript, Is.Not.Null);
            var importedJourney = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternJourney.asset");
            const string journeyPath = "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternJourney.asset";
            var directJourney = AssetDatabase.LoadAssetAtPath<EncounterJourneyAsset>(journeyPath);
            TestContext.WriteLine("Raw journey main asset: " + importedJourney);
            TestContext.WriteLine("Typed journey asset: " + directJourney);
            TestContext.WriteLine("Imported journey GUID: " + AssetDatabase.AssetPathToGUID(journeyPath));
            foreach (var imported in AssetDatabase.LoadAllAssetsAtPath(journeyPath))
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(imported, out string guid, out long localId);
                TestContext.WriteLine("Journey subasset: " + imported + ", GUID " + guid + ", local ID " + localId);
            }
            Assert.That(journeyScript.GetClass(), Is.EqualTo(typeof(EncounterJourneyAsset)),
                "Imported journey object type: " + (importedJourney == null ? "null" : importedJourney.GetType().FullName));
            var authored = AssetDatabase.LoadAssetAtPath<EncounterJourneyCatalogAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/SpiritJourneyCatalog.asset");
            Assert.That(authored, Is.Not.Null, "The authored journey catalog must resolve its script.");
            var serializedCatalog = new SerializedObject(authored);
            var entriesProperty = serializedCatalog.FindProperty("entries");
            TestContext.WriteLine("Serialized catalog entry count: " + entriesProperty.arraySize);
            for (int index = 0; index < entriesProperty.arraySize; ++index)
            {
                var entryProperty = entriesProperty.GetArrayElementAtIndex(index);
                TestContext.WriteLine("Serialized catalog entry " + index + ": entity ID " +
                    entryProperty.objectReferenceEntityIdValue + ", value " + entryProperty.objectReferenceValue);
            }
            Assert.That(directJourney, Is.Not.Null, "The direct typed journey load is missing; raw main asset: " + importedJourney);
            Assert.That(authored.Entries, Is.Not.Empty);
            foreach (var entry in authored.Entries)
            {
                Assert.That(entry, Is.Not.Null, "A catalog journey reference is missing.");
                Assert.That(entry.Beats, Is.Not.Empty, entry.name);
                Assert.That(entry.TablePresentation, Is.Not.Null, entry.name);
                Assert.That(entry.TablePresentation.RulePresentations, Is.Not.Null, entry.name);
                foreach (var rule in entry.TablePresentation.RulePresentations)
                    Assert.That(rule, Is.Not.Null, entry.name + " has a missing rule reference.");
            }
            var mechanical = TestContent.BuildStandardCatalog();
            Assert.DoesNotThrow(() => authored.Validate(mechanical));
            Assert.That(directJourney.FollowUpEncounterIds, Is.EqualTo(new[] { "encounter.owl.watchtower" }));
            Assert.That(directJourney.ResultContinueLabel, Is.EqualTo("Return to the trail"));
            var victory = directJourney.GetOutcomePresentation(EncounterOutcome.Won);
            Assert.That(victory, Is.Not.Null);
            Assert.That((int)victory.HostState, Is.EqualTo(4));
            Assert.That(victory.HoldDuration, Is.EqualTo(.25f));
            Assert.That(victory.FadeDuration, Is.EqualTo(.3f));
            Assert.That(victory.ShowDialogue, Is.True);
            var defeat = directJourney.GetOutcomePresentation(EncounterOutcome.Lost);
            Assert.That(defeat, Is.Not.Null);
            Assert.That((int)defeat.HostState, Is.EqualTo(5));
            Assert.That(defeat.HoldDuration, Is.EqualTo(.2f));
            Assert.That(defeat.FadeDuration, Is.EqualTo(.3f));
            Assert.That(defeat.ShowDialogue, Is.True);
            var abandon = directJourney.GetOutcomePresentation(EncounterOutcome.Abandoned);
            Assert.That(abandon, Is.Not.Null);
            Assert.That((int)abandon.HostState, Is.EqualTo(6));
            Assert.That(abandon.HoldDuration, Is.Zero);
            Assert.That(abandon.FadeDuration, Is.EqualTo(.2f));
            Assert.That(abandon.ShowDialogue, Is.False);
            Assert.That(controller.InitializeJourney(mechanical, authored, savePath), Is.True, controller.JourneyStatus);
            Assert.That(controller.Catalog, Is.SameAs(mechanical));
            Assert.That(controller.Snapshot, Is.Null);
        }

        [Test]
        public void AutomaticContinuationIsOptInAndAdvancingNeverAcceptsAnInvitation()
        {
            var journey = journeys.Entries[0];
            Assert.That(journey.Beats.All(beat => !beat.AutoAdvance), Is.True);
            Set(journey.Beats[0], "autoAdvance", true);
            Set(journey.Beats[journey.Beats.Count - 1], "autoAdvance", true);
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
            Set(journeys.Entries[0], "beats", Array.Empty<EncounterJourneyBeat>());
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.False);
            Assert.That(controller.StatusMessage, Does.StartWith("Cannot initialize investigation:"));
            Assert.That(controller.JourneyStatus, Is.EqualTo(controller.StatusMessage));
            Assert.That(controller.Catalog, Is.Null);
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(Get(controller, "application"), Is.Null);
        }

        [Test]
        public void InvestigationAndFirstVisitDoNotCreateOrMutateEngineState()
        {
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.True);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(Get(controller, "application"), Is.Null);
            object seed = Get(controller, "seed");
            controller.SelectEncounter(FirstId);
            controller.SkipIntro();
            controller.AcceptEncounter();
            controller.Reroll();
            controller.PlayHand();
            controller.AdvanceJourney();
            controller.ReturnToInvestigation();
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(Get(controller, "application"), Is.Null);
            Assert.That(Get(controller, "seed"), Is.EqualTo(seed));
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            Assert.That(File.Exists(controller.ProfilePath), Is.False);
            Assert.That(File.Exists(savePath), Is.False);
        }

        [TestCase(FirstId)]
        [TestCase(SecondId)]
        public void AcceptanceStartsSelectedDataOnceAndPresentsTheOpeningRoll(string id)
        {
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
        public void RepeatedVisitCanSkipButCancelSelectionDoesNotMarkAnotherEncounterVisited()
        {
            controller.InitializeJourney(catalog, journeys, savePath);
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            var active = controller.Snapshot;
            controller.ReturnToInvestigation();
            controller.SelectEncounter(SecondId);
            Assert.That(controller.CanSkipIntro, Is.False);
            controller.SkipIntro();
            Assert.That(controller.BeatIndex, Is.Zero);
            controller.ReturnToInvestigation();
            controller.SelectEncounter(FirstId);
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
            controller.SelectEncounter(FirstId);
            controller.PlayHand();
            controller.Abandon();
            Assert.That(controller.Snapshot, Is.SameAs(pending));
            controller.ReturnToInvestigation();
            controller.ResumeEncounter();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.SelectedJourney.EncounterId, Is.EqualTo(SecondId));
            Assert.That(controller.BeatIndex, Is.EqualTo(-1));
            Assert.That(controller.Snapshot, Is.SameAs(pending));
            Assert.That(File.Exists(savePath), Is.False);
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
            Assert.That(controller.SelectedJourney.EncounterId, Is.EqualTo(SecondId));
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
            object seed = Get(controller, "seed");
            controller.AcceptEncounter();
            Assert.That(controller.JourneyStatus, Does.Contain("cannot save player profile"));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(Get(controller, "seed"), Is.EqualTo(seed));
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            Assert.That(File.ReadAllText(controller.ProfilePath), Is.EqualTo("{ broken external profile"));
            controller.ReturnToInvestigation();
            controller.SelectEncounter(FirstId);
            Assert.That(controller.CanSkipIntro, Is.False);
        }

        [Test]
        public void InvalidDebugSeedDoesNotRecordVisitOrCreateEngineState()
        {
            controller.InitializeJourney(catalog, journeys, savePath);
            SelectInvitation(FirstId);
            Set(controller, "initialSeed", "invalid");
            controller.AcceptEncounter();
            Assert.That(controller.JourneyStatus, Does.Contain("Seed must be"));
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(File.Exists(controller.ProfilePath), Is.False);
        }

        [Test]
        public void LegacyProfileKeepsProgressAndNewVisitsRoundTripAndMerge()
        {
            var old = new JObject
            {
                ["ProfileFormatVersion"] = 1,
                ["UnlockedModifierIds"] = new JArray("modifier.future"),
                ["CompletedEncounterIds"] = new JArray("encounter.future")
            };
            var profile = PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes(old.ToString()));
            Assert.That(profile.VisitedEncounterIds, Is.Empty);
            Assert.That(profile.UnlockModifier("modifier.future"), Is.False);
            Assert.That(profile.CompleteEncounter("encounter.future"), Is.False);
            Assert.That(profile.VisitEncounter(SecondId), Is.True);
            Assert.That(profile.VisitEncounter(SecondId), Is.False);
            var restored = PlayerProfileCodec.Deserialize(PlayerProfileCodec.Serialize(profile));
            Assert.That(restored.VisitedEncounterIds, Is.EqualTo(new[] { SecondId }));
            Assert.That(new PlayerProfile().Merge(restored), Is.True);
            Assert.That(restored.Merge(profile), Is.False);
        }

        [TestCase("duplicate")]
        [TestCase("blank")]
        [TestCase("null")]
        public void InvalidVisitFieldsAreRejected(string error)
        {
            var document = JObject.Parse(Encoding.UTF8.GetString(PlayerProfileCodec.Serialize(new PlayerProfile())));
            if (error == "duplicate") document["VisitedEncounterIds"] = new JArray(FirstId, FirstId);
            if (error == "blank") document["VisitedEncounterIds"] = new JArray(" ");
            if (error == "null") document["VisitedEncounterIds"] = JValue.CreateNull();
            Assert.Throws<FormatException>(() => PlayerProfileCodec.Deserialize(Encoding.UTF8.GetBytes(document.ToString())));
        }

        [Test]
        public void JourneyValidationRejectsInvitationOrderUnknownEncounterAndRuleMismatch()
        {
            var journey = journeys.Entries[0];
            Set(journey, "beats", new[] { Beat("invitation", EncounterJourneyBeatKind.Invitation), Beat("later", EncounterJourneyBeatKind.Dialogue) });
            Assert.That(controller.InitializeJourney(catalog, journeys, savePath), Is.False);
            Assert.That(controller.JourneyStatus, Does.Contain("final journey beat"));
            Set(journey, "beats", new[] { Beat("invite-one", EncounterJourneyBeatKind.Invitation), Beat("invite-two", EncounterJourneyBeatKind.Invitation) });
            Assert.Throws<FormatException>(() => journeys.Validate(catalog));
            Set(journey, "beats", new[] { Beat("invitation", EncounterJourneyBeatKind.Invitation) });
            Set(journey, "encounterId", "encounter.missing");
            Assert.Throws<KeyNotFoundException>(() => journeys.Validate(catalog));
            Set(journey, "encounterId", FirstId);
            var rule = Create<EncounterRulePresentationAsset>();
            Set(rule, "ruleId", FirstRerollFreeRuleDefinition.StableId);
            Set(journey.TablePresentation, "rulePresentations", new[] { rule });
            Assert.Throws<FormatException>(() => journeys.Validate(catalog));
        }

        [Test]
        public void OutcomePresentationIsOptionalAndEnabledPerOutcome()
        {
            var journey = journeys.Entries[0];
            foreach (EncounterOutcome outcome in Enum.GetValues(typeof(EncounterOutcome)))
                Assert.That(journey.GetOutcomePresentation(outcome), Is.Null);
            var presentation = new EncounterOutcomePresentation();
            Assert.That(presentation.HoldDuration, Is.Zero);
            Assert.That(presentation.FadeDuration, Is.EqualTo(.3f));
            Assert.That(presentation.ShowDialogue, Is.False);
            Set(journey, "victoryPresentation", presentation);
            Assert.That(journey.GetOutcomePresentation(EncounterOutcome.Won), Is.Null);
            Set(presentation, "enabled", true);
            Assert.That(journey.GetOutcomePresentation(EncounterOutcome.Won), Is.SameAs(presentation));
            Assert.That(journey.GetOutcomePresentation(EncounterOutcome.Lost), Is.Null);
            Set(presentation, "holdDuration", float.NaN);
            Assert.Throws<FormatException>(() => journey.Validate(catalog));
            Set(presentation, "holdDuration", 0f);
            Set(presentation, "fadeDuration", -1f);
            Assert.Throws<FormatException>(() => journey.Validate(catalog));
        }

        [TestCase("duplicate")]
        [TestCase("self")]
        [TestCase("missing")]
        public void FollowUpReferencesMustBeUniqueOtherCatalogJourneys(string invalid)
        {
            Set(journeys.Entries[0], "followUpEncounterIds", new[] { SecondId });
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
            Set(journeys.Entries[0], "followUpEncounterIds", invalid == "duplicate" ? new[] { SecondId, SecondId } :
                new[] { invalid == "self" ? FirstId : "encounter.missing" });
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
            object application = Get(controller, "application");
            byte[] profileBytes = File.ReadAllBytes(controller.ProfilePath);
            controller.RetryEncounter();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            controller.CompletePresentation();
            controller.RetryEncounter();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(controller.SelectedJourney.EncounterId, Is.EqualTo(FirstId));
            Assert.That(controller.BeatIndex, Is.EqualTo(controller.SelectedJourney.Beats.Count - 1));
            Assert.That(controller.Snapshot, Is.SameAs(completed));
            Assert.That(Get(controller, "application"), Is.SameAs(application));
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
            Assert.That(controller.HighlightedEncounterIds, Is.EqualTo(new[] { SecondId }));
            Assert.That(controller.CompletedEncounterIds, Is.EqualTo(new[] { FirstId }));
            controller.ReturnToInvestigation();
            Assert.That(controller.HighlightedEncounterIds, Is.EqualTo(new[] { SecondId }));
            controller.SelectEncounter(SecondId);
            Assert.That(controller.HighlightedEncounterIds, Is.Empty);
            controller.ReturnToInvestigation();
            SelectInvitation(FirstId);
            controller.AcceptEncounter();
            controller.PlayHand();
            Assert.That(controller.HighlightedEncounterIds, Is.Empty);
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
            Assert.That(controller.HighlightedEncounterIds, Is.Empty);
            controller.ReturnToInvestigation();
            controller.SelectEncounter(SecondId);
            Assert.That(controller.SelectedJourney.EncounterId, Is.EqualTo(SecondId));
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
            Assert.That(controller.HighlightedEncounterIds, Is.Empty);
            controller.ReturnToInvestigation();
            Assert.That(controller.HighlightedEncounterIds, Is.Empty);
        }

        private void InitializeWinningJourney()
        {
            var winningCatalog = new MechanicalCatalog("test.journey-outcome.v1", TestContent.Hands(), new[]
            {
                new EncounterDefinition(FirstId, new[] { new StageDefinition(1, 2, 3) }),
                new EncounterDefinition(SecondId, new[] { new StageDefinition(1000000, 5, 3) })
            });
            Set(journeys.Entries[0], "followUpEncounterIds", new[] { SecondId });
            Assert.That(controller.InitializeJourney(winningCatalog, journeys, savePath), Is.True);
        }

        private EncounterJourneyAsset Journey(string id, string location, string spirit)
        {
            var journey = Create<EncounterJourneyAsset>();
            Set(journey, "encounterId", id);
            Set(journey, "locationTitle", location);
            Set(journey, "spiritName", spirit);
            Set(journey, "leadText", "A strange visitor waits beyond the door.");
            Set(journey, "tablePresentation", Create<EncounterTablePresentationAsset>());
            Set(journey, "beats", new[]
            {
                Beat("approach", EncounterJourneyBeatKind.Illustration),
                Beat("arrival", EncounterJourneyBeatKind.Arrival),
                Beat("bargain", EncounterJourneyBeatKind.Dialogue),
                Beat("invitation", EncounterJourneyBeatKind.Invitation)
            });
            return journey;
        }

        private static EncounterJourneyBeat Beat(string id, EncounterJourneyBeatKind kind)
        {
            var beat = new EncounterJourneyBeat();
            Set(beat, "id", id);
            Set(beat, "kind", kind);
            if (kind == EncounterJourneyBeatKind.Arrival) Set(beat, "hostState", SpiritPresentationState.Arrival);
            return beat;
        }

        private void SelectInvitation(string id)
        {
            controller.SelectEncounter(id);
            for (int index = 1; index < controller.SelectedJourney.Beats.Count; ++index) controller.AdvanceJourney();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static object Get(object target, string field)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    }
}
