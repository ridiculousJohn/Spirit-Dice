using System;
using System.Collections.Generic;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class ChapterJourneyAssetTests
    {
        private const string EncounterId = "encounter.chapter";
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private MechanicalCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = new MechanicalCatalog("test.chapter", TestContent.Hands(), new[]
            {
                new EncounterDefinition(EncounterId, new[] { new StageDefinition(1, 1, 0) })
            });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void StoryWithoutMechanicalEncounterUsesItsIdAndReusesHostPresentation()
        {
            var story = Journey("story.first", true);
            var rule = Create<EncounterRulePresentationAsset>();
            Set(rule, "ruleId", "rule.not.in.encounter");
            Set(story.TablePresentation, "rulePresentations", new[] { rule });
            var journeys = Catalog(story);
            Assert.That(story.IsStoryOnly, Is.True);
            Assert.That(story.Id, Is.EqualTo(story.StoryId));
            Assert.That(story.EncounterId, Is.Null.Or.Empty);
            Assert.That(story.RequiredCompletedEncounterIds, Is.Empty);
            Assert.That(story.RequiredCompletedStoryIds, Is.Empty);
            Assert.That(journeys.Find(story.Id), Is.SameAs(story));
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
        }

        [TestCase("invitation")]
        [TestCase("automatic")]
        [TestCase("encounter")]
        [TestCase("presentation")]
        public void StoryRequiresExplicitCompletionAndNoMechanicalInvitation(string invalid)
        {
            var story = Journey("story.first", true);
            if (invalid == "invitation") Set(story.Beats[0], "kind", EncounterJourneyBeatKind.Invitation);
            if (invalid == "automatic") Set(story.Beats[0], "autoAdvance", true);
            if (invalid == "encounter") Set(story, "encounterId", EncounterId);
            if (invalid == "presentation") Set(story, "tablePresentation", null);
            Assert.Throws<FormatException>(() => Catalog(story).Validate(catalog));
        }

        [Test]
        public void MechanicalJourneyRetainsItsEncounterIdentityAndInvitation()
        {
            var encounter = Journey(EncounterId, false);
            Assert.That(encounter.Id, Is.EqualTo(EncounterId));
            Assert.That(encounter.IsStoryOnly, Is.False);
            Assert.That(encounter.StoryId, Is.Empty);
            Assert.DoesNotThrow(() => Catalog(encounter).Validate(catalog));
            Set(encounter.Beats[0], "kind", EncounterJourneyBeatKind.Dialogue);
            Assert.Throws<FormatException>(() => encounter.Validate(catalog));
        }

        [Test]
        public void FollowUpsAndCompletionPrerequisitesCanConnectEncountersAndStories()
        {
            var encounter = Journey(EncounterId, false);
            var story = Journey("story.first", true);
            var final = Journey("story.return", true);
            Set(encounter, "followUpEncounterIds", new[] { story.Id });
            Set(story, "requiredCompletedEncounterIds", new[] { EncounterId });
            Set(story, "followUpEncounterIds", new[] { final.Id });
            Set(final, "requiredCompletedStoryIds", new[] { story.Id });
            Assert.DoesNotThrow(() => Catalog(encounter, story, final).Validate(catalog));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingCompletionPrerequisitesAreRejected(bool storyPrerequisite)
        {
            var story = Journey("story.first", true);
            Set(story, storyPrerequisite ? "requiredCompletedStoryIds" : "requiredCompletedEncounterIds",
                new[] { "missing" });
            if (storyPrerequisite) Assert.Throws<FormatException>(() => Catalog(story).Validate(catalog));
            else Assert.Throws<KeyNotFoundException>(() => Catalog(story).Validate(catalog));
        }

        [Test]
        public void StoryPrerequisitesCannotReferToMechanicalJourneys()
        {
            var encounter = Journey(EncounterId, false);
            var story = Journey("story.first", true);
            Set(story, "requiredCompletedStoryIds", new[] { EncounterId });
            Assert.Throws<FormatException>(() => Catalog(encounter, story).Validate(catalog));
        }

        [TestCase("self")]
        [TestCase("duplicate")]
        [TestCase("null")]
        [TestCase("blank")]
        public void InvalidCompletionPrerequisiteIdsAreRejected(string invalid)
        {
            var first = Journey("story.first", true);
            var second = Journey("story.second", true);
            Set(first, "requiredCompletedStoryIds", invalid == "null" ? null :
                invalid == "duplicate" ? new[] { second.Id, second.Id } :
                new[] { invalid == "self" ? first.Id : " " });
            Assert.Throws<FormatException>(() => Catalog(first, second).Validate(catalog));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompletionPrerequisiteCyclesAreRejectedAcrossJourneyKinds(bool mixed)
        {
            var first = Journey(mixed ? EncounterId : "story.first", !mixed);
            var second = Journey("story.second", true);
            var third = Journey("story.third", true);
            Set(first, "requiredCompletedStoryIds", new[] { second.Id });
            Set(second, "requiredCompletedStoryIds", new[] { third.Id });
            Set(third, mixed ? "requiredCompletedEncounterIds" : "requiredCompletedStoryIds", new[] { first.Id });
            Assert.Throws<FormatException>(() => Catalog(first, second, third).Validate(catalog));
        }

        [Test]
        public void ChaptersFlattenInAuthoredOrderAndReplaceLegacyEntries()
        {
            var encounter = Journey(EncounterId, false);
            var story = Journey("story.first", true);
            var later = Journey("story.second", true);
            var legacy = Journey("story.legacy", true);
            var journeys = Catalog(legacy);
            var firstChapter = Chapter("chapter.first", encounter, story);
            var secondChapter = Chapter("chapter.second", later);
            Set(journeys, "chapters", new[] { firstChapter, secondChapter });
            Assert.That(journeys.Chapters, Is.EqualTo(new[] { firstChapter, secondChapter }));
            Assert.That(journeys.Entries, Is.EqualTo(new[] { encounter, story, later }));
            Assert.That(journeys.Find(later.Id), Is.SameAs(later));
            Assert.That(journeys.Find(legacy.Id), Is.Null);
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
        }

        [TestCase("null")]
        [TestCase("blank-id")]
        [TestCase("duplicate-id")]
        [TestCase("blank-title")]
        [TestCase("null-entries")]
        [TestCase("empty-entries")]
        [TestCase("null-journey")]
        [TestCase("duplicate-journey")]
        public void InvalidChaptersAreRejected(string invalid)
        {
            var encounter = Journey(EncounterId, false);
            var story = Journey("story.return", true);
            var firstChapter = Chapter("chapter.first", encounter);
            var secondChapter = Chapter("chapter.second", story);
            if (invalid == "blank-id") Set(firstChapter, "id", " ");
            if (invalid == "duplicate-id") Set(secondChapter, "id", firstChapter.Id);
            if (invalid == "blank-title") Set(firstChapter, "title", " ");
            if (invalid == "null-entries") Set(firstChapter, "entries", null);
            if (invalid == "empty-entries") Set(firstChapter, "entries", Array.Empty<EncounterJourneyAsset>());
            if (invalid == "null-journey") Set(firstChapter, "entries", new EncounterJourneyAsset[] { null });
            if (invalid == "duplicate-journey") Set(secondChapter, "entries", new[] { encounter });
            var journeys = Catalog();
            Set(journeys, "chapters", new[] { invalid == "null" ? null : firstChapter, secondChapter });
            Assert.Throws<FormatException>(() => journeys.Validate(catalog));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CrossChapterPrerequisitesRetainReferenceAndCycleValidation(bool cycle)
        {
            var encounter = Journey(EncounterId, false);
            var story = Journey("story.return", true);
            Set(encounter, "followUpEncounterIds", new[] { story.Id });
            Set(story, "requiredCompletedEncounterIds", new[] { encounter.Id });
            var journeys = Catalog();
            Set(journeys, "chapters", new[] { Chapter("chapter.first", encounter), Chapter("chapter.second", story) });
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
            Set(encounter, "requiredCompletedStoryIds", new[] { cycle ? story.Id : "story.missing" });
            Assert.Throws<FormatException>(() => journeys.Validate(catalog));
        }

        [Test]
        public void EmptyChapterArrayKeepsLegacyCatalogEntries()
        {
            var encounter = Journey(EncounterId, false);
            var journeys = Catalog(encounter);
            Assert.That(journeys.Chapters, Is.Empty);
            Assert.That(journeys.Entries, Is.EqualTo(new[] { encounter }));
            Assert.That(journeys.Find(EncounterId), Is.SameAs(encounter));
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
        }

        private EncounterChapterAsset Chapter(string id, params EncounterJourneyAsset[] entries)
        {
            var chapter = Create<EncounterChapterAsset>();
            Set(chapter, "id", id);
            Set(chapter, "title", id);
            Set(chapter, "entries", entries);
            return chapter;
        }

        private EncounterJourneyAsset Journey(string id, bool storyOnly)
        {
            var journey = Create<EncounterJourneyAsset>();
            Set(journey, storyOnly ? "storyId" : "encounterId", id);
            Set(journey, "locationTitle", "The clearing");
            Set(journey, "spiritName", "Spirit");
            Set(journey, "leadText", "A familiar spirit waits.");
            Set(journey, "tablePresentation", Create<EncounterTablePresentationAsset>());
            var beat = new EncounterJourneyBeat();
            Set(beat, "id", "conversation");
            Set(beat, "kind", storyOnly ? EncounterJourneyBeatKind.Dialogue : EncounterJourneyBeatKind.Invitation);
            Set(journey, "beats", new[] { beat });
            return journey;
        }

        private EncounterJourneyCatalogAsset Catalog(params EncounterJourneyAsset[] entries)
        {
            var result = Create<EncounterJourneyCatalogAsset>();
            Set(result, "entries", entries);
            return result;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
