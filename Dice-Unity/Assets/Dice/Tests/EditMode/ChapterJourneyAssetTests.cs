using System;
using System.Collections.Generic;
using System.Linq;
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
        public void JourneyWithoutAnEncounterBeatReusesATableSceneOfOtherRules()
        {
            var story = Journey("story.first");
            var rule = Create<EncounterRulePresentationAsset>();
            SyntheticUi.Set(rule, "rule", content.Own(TestRules.Asset(TestRules.IdOf("not.in.encounter"))));
            SyntheticUi.Set(story.TableSceneAt(0), "rulePresentations", new[] { rule });
            var journeys = Catalog(story);
            Assert.That(story.Encounters, Is.Empty);
            Assert.That(journeys.Find(encounterAsset.Identity), Is.Null);
            Assert.That(story.RequiredCompletedJourneys, Is.Empty);
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
        }

        [TestCase("invitation")]
        [TestCase("automatic")]
        [TestCase("scene")]
        public void JourneyRequiresAFirstSceneALastBeatThatWaitsAndInvitationsWithTheirEncounters(string invalid)
        {
            var story = Journey("story.first");
            var table = story.TableSceneAt(0);
            if (invalid == "invitation") TestJourneys.SetBeats(story, table, new InvitationBeat());
            if (invalid == "automatic")
            {
                TestJourneys.SetBeats(story, table, new ArrivalBeat());
                TestJourneys.SetBeat(story, 0, "autoAdvance", true);
            }
            if (invalid == "scene") TestJourneys.SetBeat(story, 0, "scene", null);
            Assert.That(Assert.Throws<FormatException>(() => Catalog(story).Validate(catalog)).Message, Does.Contain(story.name));
        }

        [Test]
        public void MechanicalJourneyRetainsItsEncounterIdentityAndInvitation()
        {
            var encounter = EncounterJourney();
            Assert.That(encounter.Encounters, Is.EqualTo(new[] { encounterAsset }));
            var journeys = Catalog(encounter);
            Assert.That(journeys.Find(encounterAsset.Identity), Is.SameAs(encounter));
            Assert.DoesNotThrow(() => journeys.Validate(catalog));
            TestJourneys.SetBeats(encounter, encounter.TableSceneAt(0), new DialogueBeat(), new EncounterBeat());
            TestJourneys.SetBeat(encounter, 1, "encounter", encounterAsset);
            Assert.Throws<FormatException>(() => encounter.Validate(catalog));
        }

        [Test]
        public void ABeatThatSetsNoSceneStaysInTheCurrentScene()
        {
            var journey = Journey("story.scenes");
            var picture = Create<PictureSceneAsset>();
            var table = journey.TableSceneAt(0);
            TestJourneys.SetBeats(journey, picture, new IllustrationBeat(), new IllustrationBeat(), new DialogueBeat(),
                new DialogueBeat());
            TestJourneys.SetBeat(journey, 2, "scene", table);
            Assert.That(Enumerable.Range(0, 4).Select(journey.SceneAt), Is.EqualTo(new JourneySceneAsset[] { picture, picture, table, table }));
            Assert.That(Enumerable.Range(0, 4).Select(journey.TableSceneAt), Is.EqualTo(new[] { table, table, table, table }),
                "The table scene the journey reaches next stays loaded behind a picture.");
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

        /** A journey of one dialogue beat, or of an invitation and the encounter's Encounter beat, in a new table scene. */
        private EncounterJourneyAsset Journey(string name, EncounterAsset encounter)
        {
            var journey = TestJourneys.Identify(Create<EncounterJourneyAsset>(), name);
            SyntheticUi.Set(journey, "locationTitle", "The clearing");
            SyntheticUi.Set(journey, "spiritName", "Spirit");
            SyntheticUi.Set(journey, "leadText", "A familiar spirit waits.");
            var table = TestJourneys.WithTray(Create<EncounterTablePresentationAsset>(), Create<SpiritTrayPresentationAsset>());
            if (encounter == null) TestJourneys.SetBeats(journey, table, new DialogueBeat());
            else TestJourneys.SetEncounterBeats(journey, table, encounter);
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
