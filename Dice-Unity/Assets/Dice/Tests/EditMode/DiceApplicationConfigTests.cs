using System;
using Dice.App;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class DiceApplicationConfigTests
    {
        private DiceApplicationConfig configuration;
        private MechanicalCatalogAsset catalogAsset;
        private EncounterJourneyCatalogAsset journeyCatalog;
        private ModifierSkinAsset skin;
        private readonly SyntheticCharmAssets content = new SyntheticCharmAssets();

        [SetUp]
        public void SetUp()
        {
            configuration = ScriptableObject.CreateInstance<DiceApplicationConfig>();
            catalogAsset = ScriptableObject.CreateInstance<MechanicalCatalogAsset>();
            journeyCatalog = ScriptableObject.CreateInstance<EncounterJourneyCatalogAsset>();
            skin = ScriptableObject.CreateInstance<ModifierSkinAsset>();
            Set("catalogAsset", catalogAsset);
            Set("modifierSkin", skin);
            Set("encounter", content.Encounter("encounter.test"));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(configuration);
            UnityEngine.Object.DestroyImmediate(catalogAsset);
            UnityEngine.Object.DestroyImmediate(journeyCatalog);
            UnityEngine.Object.DestroyImmediate(skin);
            content.Dispose();
        }

        [Test]
        public void DirectEncounterConfigurationNeedsNoJourneyCatalog()
        {
            Assert.That(configuration.JourneyCatalog, Is.Null);
            Assert.DoesNotThrow(configuration.Validate);
        }

        [Test]
        public void JourneyConfigurationNeedsNoDirectEncounter()
        {
            Set("encounter", null);
            Set("journeyCatalog", journeyCatalog);
            Assert.That(configuration.JourneyCatalog, Is.Not.Null);
            Assert.DoesNotThrow(configuration.Validate);
        }

        [Test]
        public void MissingMechanicalCatalogIsRejected()
        {
            Set("catalogAsset", null);
            Assert.Throws<InvalidOperationException>(configuration.Validate);
        }

        [Test]
        public void MissingModifierSkinIsRejected()
        {
            Set("modifierSkin", null);
            Assert.Throws<InvalidOperationException>(configuration.Validate);
        }

        [Test]
        public void DirectConfigurationWithoutAnEncounterIsRejected()
        {
            Set("encounter", null);
            Assert.Throws<InvalidOperationException>(configuration.Validate);
        }

        private void Set(string name, object value)
        {
            using var serialized = new SerializedObject(configuration);
            var property = serialized.FindProperty(name);
            Assert.That(property, Is.Not.Null, name);
            if (property.propertyType == SerializedPropertyType.String) property.stringValue = (string)value;
            else if (property.propertyType == SerializedPropertyType.Boolean) property.boolValue = (bool)value;
            else property.objectReferenceValue = (UnityEngine.Object)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
