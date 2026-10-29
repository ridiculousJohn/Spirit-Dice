using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class ChapterJourneyAssetTests
    {
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly SyntheticCharmAssets content = new SyntheticCharmAssets();
        private MechanicalCatalog catalog;
        private EncounterAsset encounterAsset;

        [SetUp]
        public void SetUp()
        {
            encounterAsset = content.Encounter("encounter.chapter");
            catalog = new MechanicalCatalog("test.chapter", TestContent.Hands(), new[]
            {
                new EncounterDefinition(encounterAsset.Identity, new[] { new StageDefinition(1, 1, 0) })
            });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
            content.Dispose();
        }

        [Test]
        public void StoryWithoutMechanicalEncounterReusesHostPresentation()
        {
            var story = Journey("story.first");
            var rule = Create<EncounterRulePresentationAsset>();
            SyntheticUi.Set(rule, "ruleId", "rule.not.in.encounter");
            SyntheticUi.Set(story.TablePresentation, "rulePresentations", new[] { rule });
            var journeys = Catalog(story);
            Assert.That(story.IsStoryOnly, Is.True);
            Assert.That(story.Encounter, Is.Null);
            Assert.That(story.RequiredCompletedJourneys, Is.Empty);
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
        }

        [TestCase("invitation")]
        [TestCase("automatic")]
        [TestCase("presentation")]
        public void StoryRequiresExplicitCompletionAndNoMechanicalInvitation(string invalid)
        {
            var story = Journey("story.first");
            if (invalid == "invitation") TestJourneys.SetBeat(story, 0, "kind", EncounterJourneyBeatKind.Invitation);
            if (invalid == "automatic") TestJourneys.SetBeat(story, 0, "autoAdvance", true);
            if (invalid == "presentation") SyntheticUi.Set(story, "tablePresentation", null);
            Assert.Throws<FormatException>(() => Catalog(story).Validate(catalog));
        }

        [Test]
        public void MechanicalJourneyRetainsItsEncounterIdentityAndInvitation()
        {
            var encounter = EncounterJourney();
            Assert.That(encounter.Encounter, Is.SameAs(encounterAsset));
            Assert.That(encounter.IsStoryOnly, Is.False);
            var journeys = Catalog(encounter);
            Assert.That(journeys.Find(encounterAsset.Identity), Is.SameAs(encounter));
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
            TestJourneys.SetBeat(encounter, 0, "kind", EncounterJourneyBeatKind.Dialogue);
            Assert.Throws<FormatException>(() => encounter.Validate(catalog));
        }

        [Test]
        public void FollowUpsAndCompletionPrerequisitesCanConnectEncountersAndStories()
        {
            var encounter = EncounterJourney();
            var story = Journey("story.first");
            var final = Journey("story.return");
            SyntheticUi.Set(encounter, "followUpJourneys", new[] { story });
            SyntheticUi.Set(story, "requiredCompletedJourneys", new[] { encounter });
            SyntheticUi.Set(story, "followUpJourneys", new[] { final });
            SyntheticUi.Set(final, "requiredCompletedJourneys", new[] { story });
            Assert.DoesNotThrow(() => Catalog(encounter, story, final).Validate(catalog));
        }

        [Test]
        public void MissingCompletionPrerequisitesAreRejected()
        {
            var story = Journey("story.first");
            SyntheticUi.Set(story, "requiredCompletedJourneys", new[] { Journey("story.missing") });
            Assert.Throws<FormatException>(() => Catalog(story).Validate(catalog));
        }

        [TestCase("self")]
        [TestCase("duplicate")]
        [TestCase("null")]
        public void InvalidCompletionPrerequisitesAreRejected(string invalid)
        {
            var first = Journey("story.first");
            var second = Journey("story.second");
            SyntheticUi.Set(first, "requiredCompletedJourneys", invalid == "duplicate" ? new[] { second, second } :
                new[] { invalid == "self" ? first : null });
            Assert.Throws<FormatException>(() => Catalog(first, second).Validate(catalog));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompletionPrerequisiteCyclesAreRejectedAcrossJourneyKinds(bool mixed)
        {
            var first = mixed ? EncounterJourney() : Journey("story.first");
            var second = Journey("story.second");
            var third = Journey("story.third");
            SyntheticUi.Set(first, "requiredCompletedJourneys", new[] { second });
            SyntheticUi.Set(second, "requiredCompletedJourneys", new[] { third });
            SyntheticUi.Set(third, "requiredCompletedJourneys", new[] { first });
            Assert.Throws<FormatException>(() => Catalog(first, second, third).Validate(catalog));
        }

        [Test]
        public void ChaptersFlattenInAuthoredOrder()
        {
            var encounter = EncounterJourney();
            var story = Journey("story.first");
            var later = Journey("story.second");
            var journeys = Catalog();
            var firstChapter = Chapter("chapter.first", encounter, story);
            var secondChapter = Chapter("chapter.second", later);
            SyntheticUi.Set(journeys, "chapters", new[] { firstChapter, secondChapter });
            Assert.That(journeys.Chapters, Is.EqualTo(new[] { firstChapter, secondChapter }));
            Assert.That(journeys.Entries, Is.EqualTo(new[] { encounter, story, later }));
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
        }

        [TestCase("null")]
        [TestCase("blank-id")]
        [TestCase("duplicate-id")]
        [TestCase("blank-title")]
        [TestCase("empty-entries")]
        [TestCase("null-journey")]
        [TestCase("duplicate-journey")]
        public void InvalidChaptersAreRejected(string invalid)
        {
            var encounter = EncounterJourney();
            var story = Journey("story.return");
            var firstChapter = Chapter("chapter.first", encounter);
            var secondChapter = Chapter("chapter.second", story);
            if (invalid == "blank-id") SyntheticUi.Set(firstChapter, "id", " ");
            if (invalid == "duplicate-id") SyntheticUi.Set(secondChapter, "id", firstChapter.Id);
            if (invalid == "blank-title") SyntheticUi.Set(firstChapter, "title", " ");
            if (invalid == "empty-entries") SyntheticUi.Set(firstChapter, "entries", Array.Empty<EncounterJourneyAsset>());
            if (invalid == "null-journey") SyntheticUi.Set(firstChapter, "entries", new EncounterJourneyAsset[] { null });
            if (invalid == "duplicate-journey") SyntheticUi.Set(secondChapter, "entries", new[] { encounter });
            var journeys = Catalog();
            SyntheticUi.Set(journeys, "chapters", new[] { invalid == "null" ? null : firstChapter, secondChapter });
            Assert.Throws<FormatException>(() => journeys.Validate(catalog));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CrossChapterPrerequisitesRetainReferenceAndCycleValidation(bool cycle)
        {
            var encounter = EncounterJourney();
            var story = Journey("story.return");
            SyntheticUi.Set(encounter, "followUpJourneys", new[] { story });
            SyntheticUi.Set(story, "requiredCompletedJourneys", new[] { encounter });
            var journeys = Catalog();
            SyntheticUi.Set(journeys, "chapters", new[] { Chapter("chapter.first", encounter), Chapter("chapter.second", story) });
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
            SyntheticUi.Set(encounter, "requiredCompletedJourneys", new[] { cycle ? story : Journey("story.missing") });
            Assert.Throws<FormatException>(() => journeys.Validate(catalog));
        }

        private EncounterChapterAsset Chapter(string id, params EncounterJourneyAsset[] entries)
            => TestJourneys.Chapter(Create<EncounterChapterAsset>(), id, entries);

        /** A story journey identified by the name. */
        private EncounterJourneyAsset Journey(string name) => Journey(name, null);

        /** The journey of the encounter the test catalog holds. */
        private EncounterJourneyAsset EncounterJourney() => Journey("journey.chapter", encounterAsset);

        private EncounterJourneyAsset Journey(string name, EncounterAsset encounter)
        {
            bool storyOnly = encounter == null;
            var journey = TestJourneys.Identify(Create<EncounterJourneyAsset>(), name);
            SyntheticUi.Set(journey, "encounter", encounter);
            SyntheticUi.Set(journey, "locationTitle", "The clearing");
            SyntheticUi.Set(journey, "spiritName", "Spirit");
            SyntheticUi.Set(journey, "leadText", "A familiar spirit waits.");
            SyntheticUi.Set(journey, "tablePresentation", Create<EncounterTablePresentationAsset>());
            TestJourneys.SetBeats(journey, TestJourneys.Beat("conversation", storyOnly ? EncounterJourneyBeatKind.Dialogue : EncounterJourneyBeatKind.Invitation));
            return journey;
        }

        /** A catalog of one chapter holding the journeys, or of no chapters when there are none. */
        private EncounterJourneyCatalogAsset Catalog(params EncounterJourneyAsset[] entries)
            => TestJourneys.Catalog(Create<EncounterJourneyCatalogAsset>(), entries.Length == 0 ? Array.Empty<EncounterChapterAsset>()
                : new[] { Chapter("chapter.journeys", entries) });

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }
    }
}
