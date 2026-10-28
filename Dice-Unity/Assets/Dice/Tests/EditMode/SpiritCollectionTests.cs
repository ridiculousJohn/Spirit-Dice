using System;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritCollectionTests
    {
        private GameObject root;
        private SpiritCollectionView view;
        private MechanicalCatalog catalog;
        private ModifierDefinition starter;
        private ModifierDefinition locked;
        private ModifierDefinition unlocked;
        private ModifierDefinition encounterOnly;
        private ModifierDefinition discovered;
        private ModifierDefinition undiscovered;
        private ModifierDefinition inert;
        private readonly SyntheticCharms synthetic = new SyntheticCharms();
        private ModifierPresentationCatalog charms;
        private EncounterJourneyCatalogAsset journeys;
        private ScriptableObject[] journeyAssets;
        private SpiritUiWording wording;
        private SpiritUiTheme theme;

        [SetUp]
        public void BuildCollection()
        {
            starter = Modifier("starter", ModifierAccess.Starter, new AddBaseScoreEffect(1));
            locked = Modifier("locked", ModifierAccess.PermanentUnlock, new AddMultiplierEffect(1));
            unlocked = new ModifierDefinition("unlocked", TestFamilies.Of("family"), ModifierPhase.PlayHand, new AlwaysCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(5), new AddMultiplierEffect(1) },
                availability: new ModifierAvailabilityDefinition(access: ModifierAccess.PermanentUnlock));
            encounterOnly = Modifier("encounter.only", ModifierAccess.EncounterExclusive, new AddRerollsEffect(1), "family.encounter");
            discovered = Modifier("discovered", ModifierAccess.PermanentUnlock, new AddBaseScoreEffect(1), "family.discovered");
            undiscovered = Modifier("undiscovered", ModifierAccess.PermanentUnlock, new AddMultiplierEffect(1), "family.undiscovered");
            inert = Modifier("inert", ModifierAccess.Starter, new AddRerollsEffect(1), "family.inert", false);
            var encounter = new EncounterDefinition("encounter.test", new[]
                {
                    new StageDefinition(100, 3, 3), new StageDefinition(200, 3, 3), new StageDefinition(300, 3, 3)
                }, unlockMilestones: new[] { new ModifierUnlockMilestone(3) },
                encounterModifierIds: new[] { encounterOnly.Id }, unlockRewardModifierIds: new[] { locked.Id, unlocked.Id });
            catalog = new MechanicalCatalog("collection.test", TestContent.Hands(), new[] { encounter },
                new[] { starter, locked, unlocked, encounterOnly, discovered, undiscovered, inert });
            charms = synthetic.For(catalog);
            journeys = Journeys("encounter.test", "Test House", out journeyAssets);
            wording = SyntheticWording.Create();
            root = new GameObject("CollectionTest", typeof(RectTransform), typeof(Canvas), typeof(SpiritUiContext));
            ((RectTransform)root.transform).sizeDelta = new Vector2(443, 960);
            theme = ScriptableObject.CreateInstance<SpiritUiTheme>();
            root.GetComponent<SpiritUiContext>().Configure(theme, wording);
            var prefab = AssetDatabase.LoadAssetAtPath<SpiritCollectionView>(
                "Assets/Dice/Content/Presentation/UI/SpiritCollectionView.prefab");
            Assert.That(prefab, Is.Not.Null);
            view = UnityEngine.Object.Instantiate(prefab, root.transform, false);
            // The view and the rows it creates show the fabricated wording: rows come from an inactive copy of the authored row.
            var viewData = new SerializedObject(view);
            var rowPrefab = (SpiritModifierFamilyRow)viewData.FindProperty("rowPrefab").objectReferenceValue;
            var rowTemplate = UnityEngine.Object.Instantiate(rowPrefab, root.transform, false);
            rowTemplate.gameObject.SetActive(false);
            viewData.FindProperty("rowPrefab").objectReferenceValue = rowTemplate;
            viewData.ApplyModifiedPropertiesWithoutUndo();
            foreach (var context in root.GetComponentsInChildren<SpiritUiContext>(true)) context.Configure(context.Theme, wording);
            view.Build(() => view.Hide());
        }

        [TearDown]
        public void ReleaseCollection()
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(wording);
            UnityEngine.Object.DestroyImmediate(theme);
            foreach (var asset in journeyAssets) UnityEngine.Object.DestroyImmediate(asset);
            synthetic.Dispose();
        }

        [Test]
        public void FamilyRowSharesIdentityAndTriggerWhileVariantsRetainIndependentStates()
        {
            var profileIds = new[] { unlocked.Id };
            view.Show(catalog, charms, profileIds, journeys);
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.Owned));
            Assert.That(view.Rows.Count, Is.EqualTo(1));
            var row = view.Rows.Single();
            Assert.That(row.GetComponentsInChildren<Text>().Count(text => text.name == "FamilyName"), Is.EqualTo(1));
            Assert.That(row.GetComponentsInChildren<Text>().Count(text => text.name == "FamilyCondition"), Is.EqualTo(1));
            Assert.That(Find<Text>(row, "FamilyCondition").text, Does.Contain(charms.GetDescriptionLead(unlocked)));
            Assert.That(row.GetComponentsInChildren<Text>().Where(text => text.name == "VariantState").Select(text => text.text),
                Is.EqualTo(new[] { "<collection.available>", "<collection.locked>", "<collection.unlocked>" }));
            Assert.That(Find<Text>(view, "CollectionSummary").text, Is.EqualTo("<collection.summary>1|4|1"));
            Assert.That(profileIds, Is.EqualTo(new[] { unlocked.Id }));
            Assert.That(Find<Button>(row, "FamilyVariant1").GetComponent<CanvasGroup>().alpha, Is.EqualTo(SpiritModifierFamilyRow.UnavailableVariantAlpha));
            Assert.That(Find<Button>(row, "FamilyVariant2").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(Find<Button>(row, "FamilyVariant1").interactable, Is.True);

            Find<Button>(row, "FamilyVariant1").onClick.Invoke();
            Assert.That(view.SelectedModifierId, Is.EqualTo(locked.Id));
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text,
                Is.EqualTo("<collection.detailTitle>" + charms.GetName(locked) + "|<collection.locked>"));
            Assert.That(Find<Text>(view, "CollectionDetailEffect").text,
                Is.EqualTo(charms.GetEffectText(locked)));
            Assert.That(Find<Text>(view, "CollectionDetailSource").text, Does.Contain("<collection.unlockSource.one>Test House|3"));
        }

        [Test]
        public void ReopeningRefreshesPermanentProgressWithoutChangingEncounterOnlyStatus()
        {
            view.Show(catalog, charms, new[] { unlocked.Id }, journeys);
            Find<Button>(view.Rows.Single(), "FamilyVariant1").onClick.Invoke();
            view.Hide();
            Assert.That(view.IsVisible, Is.False);
            view.Show(catalog, charms, new[] { unlocked.Id, locked.Id, encounterOnly.Id }, journeys);
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.SelectedModifierId, Is.EqualTo(locked.Id));
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text, Does.EndWith("|<collection.unlocked>"));
            Assert.That(Find<Text>(view, "CollectionSummary").text, Does.StartWith("<collection.summary>2|4|"));
            Assert.That(SpiritCollectionView.GetStatusText(encounterOnly, true, wording.collection), Is.EqualTo("<collection.encounterOnly>"));
        }

        [Test]
        public void FilterCyclesOwnedDiscoveredAllAndKeepsSelectionInVisibleFamilies()
        {
            var discoveredIds = new[] { discovered.Family.Identity, encounterOnly.Family.Identity, inert.Family.Identity };
            view.Show(catalog, charms, Array.Empty<string>(), journeys, discoveredIds);
            Assert.That(view.Rows.Select(row => row.Family), Is.EqualTo(new[] { starter.Family }));
            var button = Find<Button>(view, "CollectionFilterButton");
            button.onClick.Invoke();
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.Discovered));
            Assert.That(Find<Text>(view, "CollectionFilterButtonLabel").text, Is.EqualTo("<collection.discoveredFilter>"));
            Assert.That(view.Rows.Select(row => row.Family), Is.EquivalentTo(new[] { starter.Family, discovered.Family }));
            Find<Button>(view.Rows.Single(row => row.Family == discovered.Family), "FamilyVariant0").onClick.Invoke();
            Assert.That(view.SelectedModifierId, Is.EqualTo(discovered.Id));
            button.onClick.Invoke();
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.All));
            Assert.That(view.SelectedModifierId, Is.EqualTo(discovered.Id));
            Assert.That(view.Rows.Count, Is.EqualTo(3));
            button.onClick.Invoke();
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.Owned));
            Assert.That(view.Rows.Select(row => row.Family), Is.EqualTo(new[] { starter.Family }));
            Assert.That(view.SelectedModifierId, Is.Not.EqualTo(discovered.Id));
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text, Does.EndWith("|<collection.available>"));
            view.SetFilter(SpiritCollectionFilter.All);
            view.Hide();
            view.Show(catalog, charms, Array.Empty<string>(), journeys);
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.Owned));
            view.SetFilter(SpiritCollectionFilter.Discovered);
            Assert.That(view.Rows.Select(row => row.Family), Is.EqualTo(new[] { starter.Family }));
            Assert.That(discoveredIds, Is.EqualTo(new[] { discovered.Family.Identity, encounterOnly.Family.Identity, inert.Family.Identity }));
        }

        [Test]
        public void AllUsesCompactUndiscoveredRowsWithoutRevealingOrInspectingTheirCharms()
        {
            view.Show(catalog, charms, new[] { unlocked.Id, encounterOnly.Id, inert.Id }, journeys);
            view.SetFilter(SpiritCollectionFilter.All);
            Assert.That(view.Rows.Count, Is.EqualTo(3));
            Assert.That(view.Rows.Count(row => row.IsUndiscovered), Is.EqualTo(2));
            string selected = view.SelectedModifierId;
            foreach (var row in view.Rows.Where(row => row.IsUndiscovered))
            {
                Assert.That(Find<Text>(row, "FamilyName").text, Is.EqualTo("<familyRow.undiscoveredFamily>"));
                Assert.That(Find<Text>(row, "FamilyCondition").text, Is.EqualTo("<familyRow.undiscoveredCondition>"));
                Assert.That(row.GetComponentsInChildren<Button>(), Is.Empty);
                Assert.That(row.GetComponentsInChildren<Text>(true).Where(text => text.name == "VariantEffect" || text.name == "VariantState")
                    .All(text => text.text == ""), Is.True);
                foreach (var button in row.GetComponentsInChildren<Button>(true)) button.onClick.Invoke();
            }
            Assert.That(view.SelectedModifierId, Is.EqualTo(selected));
            Assert.That(Find<Text>(view, "CollectionSummary").text, Is.EqualTo("<collection.summary>1|4|1"));
        }

        [Test]
        public void PermanentOwnershipImpliesDiscoveryAndEmptyFiltersClearPreviousDetails()
        {
            var permanentCatalog = new MechanicalCatalog("permanent.test", TestContent.Hands(), catalog.Encounters,
                new[] { locked, unlocked, encounterOnly, discovered, undiscovered });
            view.Show(permanentCatalog, charms, new[] { discovered.Id }, journeys);
            Assert.That(view.Rows.Single().Family, Is.EqualTo(discovered.Family));
            Assert.That(view.SelectedModifierId, Is.EqualTo(discovered.Id));
            view.SetFilter(SpiritCollectionFilter.Discovered);
            Assert.That(view.Rows.Single().Family, Is.EqualTo(discovered.Family));
            view.Show(permanentCatalog, charms, Array.Empty<string>(), journeys);
            Assert.That(view.Rows, Is.Empty);
            Assert.That(view.SelectedModifierId, Is.Null);
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text, Is.EqualTo("<collection.noOwnedFamilies>"));
            Assert.That(Find<Text>(view, "CollectionDetailEffect").text, Is.Empty);
            view.SetFilter(SpiritCollectionFilter.All);
            Assert.That(view.Rows.All(row => row.IsUndiscovered), Is.True);
            Assert.That(view.SelectedModifierId, Is.Null);
            Assert.That(Find<Text>(view, "CollectionDetailSource").text, Is.EqualTo("<collection.undiscoveredHint>"));
        }

        /** Checks a family's variants are listed in the order of their presentations in the skin / fails if they follow the catalog's order or their effects. */
        [Test]
        public void FamilyVariantsFollowTheSkinsPresentationOrder()
        {
            var category = synthetic.Category(synthetic.Icon());
            var baseScore = synthetic.Charm("order.base_score", "order", effects: new AddBaseScoreEffectConfig(1));
            var multiplier = synthetic.Charm("order.multiplier", "order", effects: new AddMultiplierEffectConfig(1));
            var rerolls = synthetic.Charm("order.rerolls", "order", effects: new AddRerollsEffectConfig(1));
            var content = SyntheticCharms.Content(baseScore, multiplier, rerolls);
            var skin = synthetic.Skin(new[] { rerolls, baseScore, multiplier }.Select(charm => synthetic.Presentation(charm, category)),
                new[] { category });
            view.Show(content.Catalog, synthetic.Load(skin, content), Array.Empty<string>(), journeys);
            var row = view.Rows.Single();
            var listed = Enumerable.Range(0, 3).Select(index =>
            {
                Find<Button>(row, "FamilyVariant" + index).onClick.Invoke();
                return view.SelectedModifierId;
            }).ToArray();
            Assert.That(listed, Is.EqualTo(new[] { rerolls.Id, baseScore.Id, multiplier.Id }));
        }

        [Test]
        public void SourcesDescribeAuthoredUnlockScheduleAndDistinguishRunOnlyAdditions()
        {
            var collection = wording.collection;
            Assert.That(SpiritCollectionView.GetSourceText(locked, catalog, journeys, collection), Does.StartWith(
                "<collection.unlockableSource><collection.unlockChoices><collection.unlockSource.one>Test House|3|"));
            Assert.That(SpiritCollectionView.GetSourceText(starter, catalog, journeys, collection),
                Does.StartWith("<collection.starterSource>"));
            Assert.That(SpiritCollectionView.GetSourceText(encounterOnly, catalog, journeys, collection),
                Does.StartWith("<collection.encounterOnlySource>"));
        }

        [Test]
        public void SourcesDescribeStoryAndCompletionGiftsWithoutClaimingTheyAreUnobtainable()
        {
            var reward = ScriptableObject.CreateInstance<ProgressionRewardAsset>();
            var journey = ScriptableObject.CreateInstance<EncounterJourneyAsset>();
            var journeys = ScriptableObject.CreateInstance<EncounterJourneyCatalogAsset>();
            EncounterChapterAsset chapter = null;
            try
            {
                var rewardData = new SerializedObject(reward);
                var grants = rewardData.FindProperty("grantedModifiers");
                grants.arraySize = 1;
                grants.GetArrayElementAtIndex(0).objectReferenceValue = synthetic.Charm(discovered.Id);
                rewardData.ApplyModifiedPropertiesWithoutUndo();
                var journeyData = new SerializedObject(journey);
                journeyData.FindProperty("encounterId").stringValue = "encounter.test";
                journeyData.FindProperty("locationTitle").stringValue = "Test House";
                journeyData.FindProperty("completionReward").objectReferenceValue = reward;
                var beats = journeyData.FindProperty("beats");
                beats.arraySize = 1;
                beats.GetArrayElementAtIndex(0).FindPropertyRelative("progressionReward").objectReferenceValue = reward;
                journeyData.ApplyModifiedPropertiesWithoutUndo();
                chapter = OnlyChapter(journeys, journey);
                Assert.That(SpiritCollectionView.GetSourceText(discovered, catalog, journeys, wording.collection), Does.StartWith(
                    "<collection.unlockableSource><collection.gifts><collection.storyGift>Test House |sourceSeparator| " +
                    "<collection.completionGift>Test House|"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(journeys);
                UnityEngine.Object.DestroyImmediate(chapter);
                UnityEngine.Object.DestroyImmediate(journey);
                UnityEngine.Object.DestroyImmediate(reward);
            }
        }

        /** Checks the source text formats the catalog's unlock and run reward sources under their journeys' locations and leaves out encounters without a journey / fails if a stated stage or source is dropped, an encounter is named another way, or an encounter without a journey is listed. */
        [Test]
        public void SourcesNameTheCatalogsSourcesByTheirJourneysLocations()
        {
            var stages = new[] { new StageDefinition(100, 3, 3), new StageDefinition(200, 3, 3), new StageDefinition(300, 3, 3) };
            var sourced = new EncounterDefinition("encounter.sourced", stages,
                unlockMilestones: new[] { new ModifierUnlockMilestone(1), new ModifierUnlockMilestone(3) },
                encounterModifierIds: new[] { discovered.Id }, unlockRewardModifierIds: new[] { locked.Id });
            var unjourneyed = new EncounterDefinition("encounter.unjourneyed", stages,
                unlockMilestones: new[] { new ModifierUnlockMilestone(2) }, encounterModifierIds: new[] { discovered.Id },
                unlockRewardModifierIds: new[] { locked.Id });
            var sources = new MechanicalCatalog("collection.sources", TestContent.Hands(), new[] { sourced, unjourneyed },
                new[] { locked, discovered });
            var journeys = Journeys("encounter.sourced", "Test House", out var created);
            try
            {
                Assert.That(SpiritCollectionView.GetSourceText(locked, sources, journeys, wording.collection), Is.EqualTo(
                    "<collection.unlockableSource><collection.unlockChoices><collection.unlockSource.many>Test House|1 |stageSeparator| 3|" +
                    "<collection.noRunRewards>"));
                Assert.That(SpiritCollectionView.GetSourceText(discovered, sources, journeys, wording.collection),
                    Is.EqualTo("<collection.unlockableSource><collection.notObtainable>|Test House"));
            }
            finally
            {
                foreach (var asset in created) UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        /** Checks a source encounter whose journey has no location title fails loudly / fails if the encounter is named by its id instead. */
        [Test]
        public void SourceJourneyWithoutALocationTitleFails()
        {
            var journeys = Journeys("encounter.test", "", out var created);
            try
            {
                var failure = Assert.Throws<InvalidOperationException>(() =>
                    SpiritCollectionView.GetSourceText(locked, catalog, journeys, wording.collection));
                Assert.That(failure.Message, Does.Contain("encounter.test"));
            }
            finally
            {
                foreach (var asset in created) UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        /** A journey catalog of one encounter journey with the given location title; the assets are returned for release. */
        private static EncounterJourneyCatalogAsset Journeys(string encounterId, string locationTitle, out ScriptableObject[] created)
        {
            var journey = ScriptableObject.CreateInstance<EncounterJourneyAsset>();
            var journeys = ScriptableObject.CreateInstance<EncounterJourneyCatalogAsset>();
            var journeyData = new SerializedObject(journey);
            journeyData.FindProperty("encounterId").stringValue = encounterId;
            journeyData.FindProperty("locationTitle").stringValue = locationTitle;
            journeyData.ApplyModifiedPropertiesWithoutUndo();
            created = new ScriptableObject[] { journeys, OnlyChapter(journeys, journey), journey };
            return journeys;
        }

        /** Makes a chapter of the one journey the catalog's only chapter; the chapter is returned for release. */
        private static EncounterChapterAsset OnlyChapter(EncounterJourneyCatalogAsset journeys, EncounterJourneyAsset journey)
        {
            var chapter = ScriptableObject.CreateInstance<EncounterChapterAsset>();
            var chapterData = new SerializedObject(chapter);
            var entries = chapterData.FindProperty("entries");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).objectReferenceValue = journey;
            chapterData.ApplyModifiedPropertiesWithoutUndo();
            var catalogData = new SerializedObject(journeys);
            var chapters = catalogData.FindProperty("chapters");
            chapters.arraySize = 1;
            chapters.GetArrayElementAtIndex(0).objectReferenceValue = chapter;
            catalogData.ApplyModifiedPropertiesWithoutUndo();
            return chapter;
        }

        private static ModifierDefinition Modifier(string id, ModifierAccess access, ModifierEffect effect,
            string family = "family", bool rewardEligible = true)
            => new ModifierDefinition(id, TestFamilies.Of(family), ModifierPhase.PlayHand, new AlwaysCondition(),
                new[] { effect }, rewardEligible: rewardEligible, availability: access == ModifierAccess.EncounterExclusive
                    ? new ModifierAvailabilityDefinition(scope: ModifierAvailabilityScope.Encounter, scopeId: "encounter.test", access: access)
                    : new ModifierAvailabilityDefinition(access: access));

        private static T Find<T>(Component parent, string name) where T : Component
            => parent.GetComponentsInChildren<T>(true).Single(component => component.name == name);
    }
}
