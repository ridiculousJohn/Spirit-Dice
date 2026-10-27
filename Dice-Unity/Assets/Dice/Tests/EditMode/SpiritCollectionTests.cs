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

        [SetUp]
        public void BuildCollection()
        {
            starter = Modifier("starter", ModifierAccess.Starter, ModifierEffectKind.AddBaseScore);
            locked = Modifier("locked", ModifierAccess.PermanentUnlock, ModifierEffectKind.AddMultiplier);
            unlocked = new ModifierDefinition("unlocked", "family", ModifierPhase.PlayHand, new AlwaysCondition(),
                new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 5), new ModifierEffect(ModifierEffectKind.AddMultiplier, 1) },
                availability: new ModifierAvailabilityDefinition(access: ModifierAccess.PermanentUnlock));
            encounterOnly = Modifier("encounter.only", ModifierAccess.EncounterExclusive, ModifierEffectKind.AddRerolls, "family.encounter");
            discovered = Modifier("discovered", ModifierAccess.PermanentUnlock, ModifierEffectKind.AddBaseScore, "family.discovered");
            undiscovered = Modifier("undiscovered", ModifierAccess.PermanentUnlock, ModifierEffectKind.AddMultiplier, "family.undiscovered");
            inert = Modifier("inert", ModifierAccess.Starter, ModifierEffectKind.AddRerolls, "family.inert", false);
            var encounter = new EncounterDefinition("encounter.test", new[]
                {
                    new StageDefinition(100, 3, 3), new StageDefinition(200, 3, 3), new StageDefinition(300, 3, 3)
                }, unlockMilestones: new[] { new ModifierUnlockMilestone(3, Array.Empty<string>()) },
                encounterModifierIds: new[] { encounterOnly.Id }, unlockRewardModifierIds: new[] { locked.Id, unlocked.Id });
            catalog = new MechanicalCatalog("collection.test", TestContent.Hands(), new[] { encounter },
                new[] { starter, locked, unlocked, encounterOnly, discovered, undiscovered, inert });
            charms = synthetic.For(catalog);
            root = new GameObject("CollectionTest", typeof(RectTransform), typeof(Canvas), typeof(SpiritUiContext));
            ((RectTransform)root.transform).sizeDelta = new Vector2(443, 960);
            var themePath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:SpiritUiTheme").First());
            root.GetComponent<SpiritUiContext>().Configure(AssetDatabase.LoadAssetAtPath<SpiritUiTheme>(themePath));
            var prefab = AssetDatabase.LoadAssetAtPath<SpiritCollectionView>(
                "Assets/Dice/Content/Presentation/UI/SpiritCollectionView.prefab");
            Assert.That(prefab, Is.Not.Null);
            view = UnityEngine.Object.Instantiate(prefab, root.transform, false);
            view.Build((RectTransform)root.transform, () => view.Hide());
        }

        [TearDown]
        public void ReleaseCollection()
        {
            UnityEngine.Object.DestroyImmediate(root);
            synthetic.Dispose();
        }

        [Test]
        public void FamilyRowSharesIdentityAndTriggerWhileVariantsRetainIndependentStates()
        {
            var profileIds = new[] { unlocked.Id };
            view.Show(catalog, charms, profileIds);
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.Owned));
            Assert.That(view.Rows.Count, Is.EqualTo(1));
            var row = view.Rows.Single();
            Assert.That(row.GetComponentsInChildren<Text>().Count(text => text.name == "FamilyName"), Is.EqualTo(1));
            Assert.That(row.GetComponentsInChildren<Text>().Count(text => text.name == "FamilyCondition"), Is.EqualTo(1));
            Assert.That(Find<Text>(row, "FamilyCondition").text, Does.Contain(charms.GetDescriptionLead(unlocked)));
            Assert.That(row.GetComponentsInChildren<Text>().Where(text => text.name == "VariantState").Select(text => text.text),
                Is.EqualTo(new[] { "Available", "Locked", "Unlocked" }));
            Assert.That(Find<Text>(view, "CollectionSummary").text, Is.EqualTo("Collected 1 / 4  |  Available at start 1"));
            Assert.That(profileIds, Is.EqualTo(new[] { unlocked.Id }));
            Assert.That(Find<Button>(row, "FamilyVariant1").GetComponent<CanvasGroup>().alpha, Is.EqualTo(.35f));
            Assert.That(Find<Button>(row, "FamilyVariant2").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(Find<Button>(row, "FamilyVariant1").interactable, Is.True);

            Find<Button>(row, "FamilyVariant1").onClick.Invoke();
            Assert.That(view.SelectedModifierId, Is.EqualTo(locked.Id));
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text, Does.EndWith(" - Locked"));
            Assert.That(Find<Text>(view, "CollectionDetailEffect").text,
                Is.EqualTo(charms.GetEffectText(locked)));
            Assert.That(Find<Text>(view, "CollectionDetailSource").text, Does.Contain("after stage 3"));
        }

        [Test]
        public void ReopeningRefreshesPermanentProgressWithoutChangingEncounterOnlyStatus()
        {
            view.Show(catalog, charms, new[] { unlocked.Id });
            Find<Button>(view.Rows.Single(), "FamilyVariant1").onClick.Invoke();
            view.Hide();
            Assert.That(view.IsVisible, Is.False);
            view.Show(catalog, charms, new[] { unlocked.Id, locked.Id, encounterOnly.Id });
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.SelectedModifierId, Is.EqualTo(locked.Id));
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text, Does.EndWith(" - Unlocked"));
            Assert.That(Find<Text>(view, "CollectionSummary").text, Does.StartWith("Collected 2 / 4"));
            Assert.That(SpiritCollectionView.GetStatusText(encounterOnly, true), Is.EqualTo("Encounter only"));
        }

        [Test]
        public void FilterCyclesOwnedDiscoveredAllAndKeepsSelectionInVisibleFamilies()
        {
            var discoveredIds = new[] { discovered.FamilyId, encounterOnly.FamilyId, inert.FamilyId };
            view.Show(catalog, charms, Array.Empty<string>(), null, discoveredIds);
            Assert.That(view.Rows.Select(row => row.FamilyId), Is.EqualTo(new[] { starter.FamilyId }));
            var button = Find<Button>(view, "CollectionFilterButton");
            button.onClick.Invoke();
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.Discovered));
            Assert.That(Find<Text>(view, "CollectionFilterButtonLabel").text, Is.EqualTo("Discovered"));
            Assert.That(view.Rows.Select(row => row.FamilyId), Is.EquivalentTo(new[] { starter.FamilyId, discovered.FamilyId }));
            Find<Button>(view.Rows.Single(row => row.FamilyId == discovered.FamilyId), "FamilyVariant0").onClick.Invoke();
            Assert.That(view.SelectedModifierId, Is.EqualTo(discovered.Id));
            button.onClick.Invoke();
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.All));
            Assert.That(view.SelectedModifierId, Is.EqualTo(discovered.Id));
            Assert.That(view.Rows.Count, Is.EqualTo(3));
            button.onClick.Invoke();
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.Owned));
            Assert.That(view.Rows.Select(row => row.FamilyId), Is.EqualTo(new[] { starter.FamilyId }));
            Assert.That(view.SelectedModifierId, Is.Not.EqualTo(discovered.Id));
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text, Does.EndWith(" - Available"));
            view.SetFilter(SpiritCollectionFilter.All);
            view.Hide();
            view.Show(catalog, charms, Array.Empty<string>());
            Assert.That(view.Filter, Is.EqualTo(SpiritCollectionFilter.Owned));
            view.SetFilter(SpiritCollectionFilter.Discovered);
            Assert.That(view.Rows.Select(row => row.FamilyId), Is.EqualTo(new[] { starter.FamilyId }));
            Assert.That(discoveredIds, Is.EqualTo(new[] { discovered.FamilyId, encounterOnly.FamilyId, inert.FamilyId }));
        }

        [Test]
        public void AllUsesCompactUndiscoveredRowsWithoutRevealingOrInspectingTheirCharms()
        {
            view.Show(catalog, charms, new[] { unlocked.Id, encounterOnly.Id, inert.Id });
            view.SetFilter(SpiritCollectionFilter.All);
            Assert.That(view.Rows.Count, Is.EqualTo(3));
            Assert.That(view.Rows.Count(row => row.IsUndiscovered), Is.EqualTo(2));
            string selected = view.SelectedModifierId;
            foreach (var row in view.Rows.Where(row => row.IsUndiscovered))
            {
                Assert.That(Find<Text>(row, "FamilyName").text, Is.EqualTo("Undiscovered family"));
                Assert.That(Find<Text>(row, "FamilyCondition").text, Is.EqualTo("Locked"));
                Assert.That(row.GetComponentsInChildren<Button>(), Is.Empty);
                var rect = (RectTransform)row.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(LayoutUtility.GetPreferredHeight(rect), Is.LessThan(100));
                Assert.That(row.GetComponentsInChildren<Text>(true).Where(text => text.name == "VariantEffect" || text.name == "VariantState")
                    .All(text => text.text == ""), Is.True);
                foreach (var button in row.GetComponentsInChildren<Button>(true)) button.onClick.Invoke();
            }
            Assert.That(view.SelectedModifierId, Is.EqualTo(selected));
            Assert.That(Find<Text>(view, "CollectionSummary").text, Is.EqualTo("Collected 1 / 4  |  Available at start 1"));
        }

        [Test]
        public void PermanentOwnershipImpliesDiscoveryAndEmptyFiltersClearPreviousDetails()
        {
            var permanentCatalog = new MechanicalCatalog("permanent.test", TestContent.Hands(), catalog.Encounters,
                new[] { locked, unlocked, encounterOnly, discovered, undiscovered });
            view.Show(permanentCatalog, charms, new[] { discovered.Id });
            Assert.That(view.Rows.Single().FamilyId, Is.EqualTo(discovered.FamilyId));
            Assert.That(view.SelectedModifierId, Is.EqualTo(discovered.Id));
            view.SetFilter(SpiritCollectionFilter.Discovered);
            Assert.That(view.Rows.Single().FamilyId, Is.EqualTo(discovered.FamilyId));
            view.Show(permanentCatalog, charms, Array.Empty<string>());
            Assert.That(view.Rows, Is.Empty);
            Assert.That(view.SelectedModifierId, Is.Null);
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text, Is.EqualTo("No owned families"));
            Assert.That(Find<Text>(view, "CollectionDetailEffect").text, Is.Empty);
            view.SetFilter(SpiritCollectionFilter.All);
            Assert.That(view.Rows.All(row => row.IsUndiscovered), Is.True);
            Assert.That(view.SelectedModifierId, Is.Null);
            Assert.That(Find<Text>(view, "CollectionDetailSource").text, Does.Contain("Discover charm families")
                .And.Not.Contain("Unlock choice:"));
        }

        [Test]
        public void SourcesDescribeAuthoredUnlockScheduleAndDistinguishRunOnlyAdditions()
        {
            Assert.That(SpiritCollectionView.GetSourceText(locked, catalog), Does.Contain("Unlock choice:")
                .And.Contain("after stage 3").And.Contain("Once unlocked, run charm pool:"));
            Assert.That(SpiritCollectionView.GetSourceText(starter, catalog), Does.StartWith("Available from the start."));
            Assert.That(SpiritCollectionView.GetSourceText(encounterOnly, catalog),
                Does.StartWith("Encounter only; never permanently collected.").And.Contain("Run charm pool:"));
        }

        [Test]
        public void SourcesDescribeStoryAndCompletionGiftsWithoutClaimingTheyAreUnobtainable()
        {
            var reward = ScriptableObject.CreateInstance<ProgressionRewardAsset>();
            var journey = ScriptableObject.CreateInstance<EncounterJourneyAsset>();
            var journeys = ScriptableObject.CreateInstance<EncounterJourneyCatalogAsset>();
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
                var catalogData = new SerializedObject(journeys);
                var entries = catalogData.FindProperty("entries");
                entries.arraySize = 1;
                entries.GetArrayElementAtIndex(0).objectReferenceValue = journey;
                catalogData.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(SpiritCollectionView.GetSourceText(discovered, catalog, journeys),
                    Does.Contain("Gift: Test House - story; Test House - completion")
                        .And.Not.Contain("Not obtainable yet"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(journeys);
                UnityEngine.Object.DestroyImmediate(journey);
                UnityEngine.Object.DestroyImmediate(reward);
            }
        }

        private static ModifierDefinition Modifier(string id, ModifierAccess access, ModifierEffectKind kind,
            string familyId = "family", bool rewardEligible = true)
            => new ModifierDefinition(id, familyId, ModifierPhase.PlayHand, new AlwaysCondition(),
                new[] { new ModifierEffect(kind, 1) }, rewardEligible: rewardEligible, availability: access == ModifierAccess.EncounterExclusive
                    ? new ModifierAvailabilityDefinition(scope: ModifierAvailabilityScope.Encounter, scopeId: "encounter.test", access: access)
                    : new ModifierAvailabilityDefinition(access: access));

        private static T Find<T>(Component parent, string name) where T : Component
            => parent.GetComponentsInChildren<T>(true).Single(component => component.name == name);
    }
}
