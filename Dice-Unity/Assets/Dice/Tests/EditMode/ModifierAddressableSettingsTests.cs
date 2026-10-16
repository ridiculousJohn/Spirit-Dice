using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class ModifierAddressableSettingsTests
    {
        private const string MechanicalFolder = "Assets/Dice/Content/Mechanical/Modifiers";
        private const string PresentationFolder = "Assets/Dice/Content/Presentation/SpiritHouse/Modifiers";
        private const string MechanicalLabel = "dice.modifiers";
        private const string PresentationLabel = "dice.theme.spirit-house.modifiers";

        [Test]
        public void EveryMechanicalModifierIsAddressableInOneLabeledGroup()
        {
            AddressableAssetSettings settings = Settings();
            AddressableAssetGroup group = Group(settings, "Dice Modifiers");
            ModifierAsset[] assets = Assets<ModifierAsset>(MechanicalFolder);
            Assert.That(assets.Length, Is.EqualTo(300));
            Assert.That(group.entries.Count, Is.EqualTo(300));
            Assert.That(assets.Select(asset => asset.Id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(300));
            Assert.That(LabeledEntries(settings, MechanicalLabel).Select(entry => entry.guid),
                Is.EquivalentTo(assets.Select(Guid)));
            foreach (ModifierAsset asset in assets)
            {
                AddressableAssetEntry entry = settings.FindAssetEntry(Guid(asset));
                Assert.That(entry, Is.Not.Null, asset.Id);
                Assert.That(entry.parentGroup, Is.SameAs(group), asset.Id);
                Assert.That(entry.labels, Does.Contain(MechanicalLabel), asset.Id);
                Assert.That(entry.address, Is.EqualTo(asset.Id), asset.Id);
            }
        }

        [Test]
        public void EveryThemePresentationReferencesItsAddressableMechanicalModifier()
        {
            AddressableAssetSettings settings = Settings();
            AddressableAssetGroup group = Group(settings, "Spirit House Modifier Presentation");
            AddressableAssetGroup mechanicalGroup = Group(settings, "Dice Modifiers");
            ModifierAsset[] mechanics = Assets<ModifierAsset>(MechanicalFolder);
            ModifierPresentationAsset[] presentations = Assets<ModifierPresentationAsset>(PresentationFolder);
            Assert.That(presentations.Length, Is.EqualTo(300));
            Assert.That(group.entries.Count, Is.EqualTo(300));
            Assert.That(LabeledEntries(settings, PresentationLabel).Select(entry => entry.guid),
                Is.EquivalentTo(presentations.Select(Guid)));
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModifierPresentationAsset presentation in presentations)
            {
                Assert.That(presentation.Modifier, Is.Not.Null, presentation.name);
                string modifierGuid = Guid(presentation.Modifier);
                Assert.That(referenced.Add(modifierGuid), Is.True, presentation.name);
                AddressableAssetEntry mechanicalEntry = settings.FindAssetEntry(modifierGuid);
                Assert.That(mechanicalEntry, Is.Not.Null, presentation.name);
                Assert.That(mechanicalEntry.parentGroup, Is.SameAs(mechanicalGroup), presentation.name);
                Assert.That(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(presentation), false),
                    Does.Contain(AssetDatabase.GetAssetPath(presentation.Modifier)), presentation.name);
                AddressableAssetEntry entry = settings.FindAssetEntry(Guid(presentation));
                Assert.That(entry, Is.Not.Null, presentation.name);
                Assert.That(entry.parentGroup, Is.SameAs(group), presentation.name);
                Assert.That(entry.labels, Does.Contain(PresentationLabel), presentation.name);
                Assert.That(entry.address, Is.EqualTo("theme.spirit-house." + presentation.Modifier.Id), presentation.name);
            }
            Assert.That(referenced, Is.EquivalentTo(mechanics.Select(Guid)));
            AddressableAssetEntry[] entries = mechanicalGroup.entries.Concat(group.entries).ToArray();
            Assert.That(entries.Select(entry => entry.guid).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(600));
            Assert.That(entries.Select(entry => entry.address).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(600));
        }

        [Test]
        public void PlayerBuildIncludesBothLocalGroupsAndTheirRuntimeCatalogKeys()
        {
            AddressableAssetSettings settings = Settings();
            Assert.That(settings.BuildAddressablesWithPlayerBuild,
                Is.EqualTo(AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer));
            string buildPathId = settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kLocalBuildPath).Id;
            string loadPathId = settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kLocalLoadPath).Id;
            foreach (string name in new[] { "Dice Modifiers", "Spirit House Modifier Presentation" })
            {
                BundledAssetGroupSchema schema = Group(settings, name).GetSchema<BundledAssetGroupSchema>();
                Assert.That(schema, Is.Not.Null, name);
                Assert.That(schema.IncludeInBuild, Is.True, name);
                Assert.That(schema.IncludeLabelsInCatalog, Is.True, name);
                Assert.That(schema.IncludeGUIDInCatalog, Is.True, name);
                Assert.That(schema.IncludeAddressInCatalog, Is.True, name);
                Assert.That(schema.BuildPath.Id, Is.EqualTo(buildPathId), name);
                Assert.That(schema.LoadPath.Id, Is.EqualTo(loadPathId), name);
                Assert.That(schema.BundleMode, Is.EqualTo(BundledAssetGroupSchema.BundlePackingMode.PackTogether), name);
            }
        }

        private static AddressableAssetSettings Settings()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);
            return settings;
        }

        private static AddressableAssetGroup Group(AddressableAssetSettings settings, string name)
        {
            AddressableAssetGroup group = settings.FindGroup(name);
            Assert.That(group, Is.Not.Null, name);
            return group;
        }

        private static T[] Assets<T>(string folder) where T : ScriptableObject
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        }

        private static string Guid(UnityEngine.Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

        private static IEnumerable<AddressableAssetEntry> LabeledEntries(AddressableAssetSettings settings, string label)
            => settings.groups.Where(group => group != null).SelectMany(group => group.entries)
                .Where(entry => entry.labels.Contains(label));
    }
}
