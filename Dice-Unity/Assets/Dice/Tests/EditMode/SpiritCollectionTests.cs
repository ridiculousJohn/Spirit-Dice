using System;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
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

        [SetUp]
        public void BuildCollection()
        {
            starter = Modifier("starter", ModifierAccess.Starter, ModifierEffectKind.AddBaseScore);
            locked = Modifier("locked", ModifierAccess.PermanentUnlock, ModifierEffectKind.AddMultiplier);
            unlocked = new ModifierDefinition("unlocked", "family", ModifierPhase.PlayHand, new AlwaysCondition(),
                new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 5), new ModifierEffect(ModifierEffectKind.AddMultiplier, 1) },
                availability: new ModifierAvailabilityDefinition(access: ModifierAccess.PermanentUnlock));
            encounterOnly = Modifier("encounter.only", ModifierAccess.EncounterExclusive, ModifierEffectKind.AddRerolls);
            var encounter = new EncounterDefinition("encounter.test", new[]
                {
                    new StageDefinition(100, 3, 3), new StageDefinition(200, 3, 3), new StageDefinition(300, 3, 3)
                }, unlockMilestones: new[] { new ModifierUnlockMilestone(3, Array.Empty<string>()) },
                encounterModifierIds: new[] { encounterOnly.Id }, unlockRewardModifierIds: new[] { locked.Id, unlocked.Id });
            catalog = new MechanicalCatalog("collection.test", TestContent.Hands(), new[] { encounter },
                new[] { starter, locked, unlocked, encounterOnly });
            root = new GameObject("CollectionTest", typeof(RectTransform), typeof(Canvas), typeof(SpiritUiContext));
            ((RectTransform)root.transform).sizeDelta = new Vector2(443, 960);
            var themePath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:SpiritUiTheme").First());
            root.GetComponent<SpiritUiContext>().Configure(AssetDatabase.LoadAssetAtPath<SpiritUiTheme>(themePath));
            view = root.AddComponent<SpiritCollectionView>();
            view.Build((RectTransform)root.transform, () => view.Hide());
        }

        [TearDown]
        public void ReleaseCollection()
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void FamilyRowSharesIdentityAndTriggerWhileVariantsRetainIndependentStates()
        {
            var profileIds = new[] { unlocked.Id };
            view.Show(catalog, profileIds);
            Assert.That(view.Rows.Count, Is.EqualTo(1));
            var row = view.Rows.Single();
            Assert.That(row.GetComponentsInChildren<Text>().Count(text => text.name == "FamilyName"), Is.EqualTo(1));
            Assert.That(row.GetComponentsInChildren<Text>().Count(text => text.name == "FamilyCondition"), Is.EqualTo(1));
            Assert.That(Find<Text>(row, "FamilyCondition").text, Does.Contain("When playing a hand"));
            Assert.That(row.GetComponentsInChildren<Text>().Where(text => text.name == "VariantState").Select(text => text.text),
                Is.EqualTo(new[] { "Available", "Locked", "Unlocked", "Encounter only" }));
            Assert.That(Find<Text>(view, "CollectionSummary").text, Is.EqualTo("Collected 1 / 2  |  Available at start 1"));
            Assert.That(profileIds, Is.EqualTo(new[] { unlocked.Id }));
            Assert.That(Find<Button>(row, "FamilyVariant1").GetComponent<CanvasGroup>().alpha, Is.EqualTo(.35f));
            Assert.That(Find<Button>(row, "FamilyVariant2").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(Find<Button>(row, "FamilyVariant1").interactable, Is.True);

            Find<Button>(row, "FamilyVariant1").onClick.Invoke();
            Assert.That(view.SelectedModifierId, Is.EqualTo(locked.Id));
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text, Does.EndWith(" - Locked"));
            Assert.That(Find<Text>(view, "CollectionDetailEffect").text,
                Is.EqualTo(SpiritModifierPresentation.GetEffectText(locked)));
            Assert.That(Find<Text>(view, "CollectionDetailSource").text, Does.Contain("after stage 3"));
        }

        [Test]
        public void ReopeningRefreshesPermanentProgressWithoutChangingEncounterOnlyStatus()
        {
            view.Show(catalog, new[] { unlocked.Id });
            Find<Button>(view.Rows.Single(), "FamilyVariant1").onClick.Invoke();
            view.Hide();
            Assert.That(view.IsVisible, Is.False);
            view.Show(catalog, new[] { unlocked.Id, locked.Id, encounterOnly.Id });
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.SelectedModifierId, Is.EqualTo(locked.Id));
            Assert.That(Find<Text>(view, "CollectionDetailTitle").text, Does.EndWith(" - Unlocked"));
            Assert.That(Find<Text>(view, "CollectionSummary").text, Does.StartWith("Collected 2 / 2"));
            Assert.That(SpiritCollectionView.GetStatusText(encounterOnly, true), Is.EqualTo("Encounter only"));
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

        private static ModifierDefinition Modifier(string id, ModifierAccess access, ModifierEffectKind kind)
            => new ModifierDefinition(id, "family", ModifierPhase.PlayHand, new AlwaysCondition(),
                new[] { new ModifierEffect(kind, 1) }, availability: access == ModifierAccess.EncounterExclusive
                    ? new ModifierAvailabilityDefinition(scope: ModifierAvailabilityScope.Encounter, scopeId: "encounter.test", access: access)
                    : new ModifierAvailabilityDefinition(access: access));

        private static T Find<T>(Component parent, string name) where T : Component
            => parent.GetComponentsInChildren<T>(true).Single(component => component.name == name);
    }
}
