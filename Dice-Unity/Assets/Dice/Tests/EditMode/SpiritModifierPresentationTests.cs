using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SpiritModifierPresentationTests
    {
        private MechanicalCatalog catalog;
        private ModifierPresentationAsset[] themeAssets;
        private IDisposable theme;

        [SetUp]
        public void InstallAuthoredTheme()
        {
            catalog = TestContent.BuildStandardCatalog();
            themeAssets = AssetDatabase.FindAssets("t:ModifierPresentationAsset",
                    new[] { "Assets/Dice/Content/Presentation/SpiritHouse/Modifiers" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ModifierPresentationAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(asset => asset.Modifier.Id, StringComparer.Ordinal).ToArray();
            theme = SpiritModifierPresentation.InstallTheme(themeAssets, catalog);
        }

        [TearDown]
        public void ReleaseAuthoredTheme() { theme?.Dispose(); }

        private static readonly string[] AuthoredIds =
        {
            "modifier.initial_roll.initial_pair.base_score",
            "modifier.player_reroll.selected_face_unchanged.rerolls",
            "modifier.player_reroll.last_reroll_on_last_hand.combined",
            "modifier.play_hand.no_extreme_faces.base_score",
            "modifier.play_hand.contains_face.multiplier",
            "modifier.play_hand.middle_unique.combined"
        };

        [TestCase("modifier.initial_roll.initial_pair.base_score", "Welcoming Pair / Base", typeof(InitialPairCondition),
            "Your initial roll is a Pair.", "+10 base score", "On initial roll / Once per hand")]
        [TestCase("modifier.player_reroll.selected_face_unchanged.rerolls", "Second Chance / Rerolls", typeof(SelectedFaceUnchangedCondition),
            "At least one rerolled die keeps its face.", "+1 reroll", "After rerolling / Once per hand")]
        [TestCase("modifier.player_reroll.last_reroll_on_last_hand.combined", "Last Lantern / Base + Mult", typeof(LastRerollOnLastHandCondition),
            "Spend your last reroll on your last hand.", "+10 base score, +2 multiplier", "After rerolling / Once per hand")]
        [TestCase("modifier.play_hand.no_extreme_faces.base_score", "Quiet Grove / Base", typeof(NoExtremeFacesCondition),
            "No die shows 1 or 6.", "+15 base score", "When played / Once per hand")]
        [TestCase("modifier.play_hand.contains_face.multiplier", "One Spark / Mult", typeof(ContainsFaceCondition),
            "At least one die shows 1.", "+2 multiplier", "When played / Once per hand")]
        [TestCase("modifier.play_hand.middle_unique.combined", "Lone Watcher / Base + Mult", typeof(MiddleUniqueCondition),
            "The middle die has a face no other die shows.", "+5 base score, +1 multiplier", "When played / Once per hand")]
        public void AuthoredIdentityDescribesTheActualStandardDefinition(string id, string name, Type conditionType,
            string conditionText, string effectText, string timingText)
        {
            var definition = Catalog().GetModifier(id);
            Assert.That(definition.Condition, Is.TypeOf(conditionType), id);
            Assert.That(SpiritModifierPresentation.GetName(definition), Is.EqualTo(name));
            Assert.That(SpiritModifierPresentation.GetConditionText(definition), Is.EqualTo(conditionText));
            Assert.That(SpiritModifierPresentation.GetEffectText(definition), Is.EqualTo(effectText));
            Assert.That(SpiritModifierPresentation.GetTimingText(definition), Is.EqualTo(timingText));
        }

        [Test]
        public void AuthoredIdentitiesHaveUniqueNamesAndDistinctVisibleArtwork()
        {
            var catalog = Catalog();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var sprites = new HashSet<Sprite>();
            var textures = new HashSet<Texture2D>();
            var images = new List<Color32[]>();
            foreach (string id in AuthoredIds)
            {
                var definition = catalog.GetModifier(id);
                Assert.That(names.Add(SpiritModifierPresentation.GetName(definition)), Is.True, id);
                var sprite = SpiritModifierIcons.GetSprite(definition);
                Assert.That(sprite, Is.Not.Null, id);
                string artworkName = themeAssets.Single(asset => asset.Modifier.Id == id).Artwork.ToString();
                Assert.That(sprite.name, Is.EqualTo("Spirit Charm " + artworkName), id);
                Assert.That(sprite.texture.name, Is.EqualTo("Spirit Charm Ink " + artworkName), id);
                Assert.That(sprites.Add(sprite), Is.True, id);
                Assert.That(textures.Add(sprite.texture), Is.True, id);
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.SameAs(sprite), id);

                var pixels = ReadPixels(sprite.texture);
                Assert.That(pixels.Any(value => value.a > 0), Is.True, id + " requires visible artwork.");
                Assert.That(pixels.Any(value => value.a == 0), Is.True, id + " requires transparent space.");
                Assert.That(images.All(value => !value.SequenceEqual(pixels)), Is.True, id + " duplicates another charm's image.");
                images.Add(pixels);
            }
        }

        [Test]
        public void UnselectedDefinitionsKeepActualDescriptionsWithoutAssignedArtwork()
        {
            var catalog = Catalog();
            foreach (var definition in catalog.Modifiers.Where(value => !AuthoredIds.Contains(value.Id)))
            {
                Assert.That(SpiritModifierPresentation.GetName(definition), Is.Not.Null.And.Not.Empty, definition.Id);
                Assert.That(SpiritModifierPresentation.GetConditionText(definition),
                    Is.EqualTo(DiagnosticText.DescribeCondition(definition.Condition)), definition.Id);
                Assert.That(SpiritModifierPresentation.GetEffectText(definition),
                    Is.EqualTo(DiagnosticText.DescribeEffects(definition).Replace("+1 rerolls", "+1 reroll")), definition.Id);
                Assert.That(SpiritModifierPresentation.GetTimingText(definition), Is.Not.Null.And.Not.Empty, definition.Id);
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.Null, definition.Id);
            }

            foreach (string id in AuthoredIds)
            {
                var selected = catalog.GetModifier(id);
                foreach (var sibling in catalog.Modifiers.Where(value => value.FamilyId == selected.FamilyId && value.Id != id))
                    Assert.That(SpiritModifierPresentation.GetName(sibling),
                        Is.Not.EqualTo(SpiritModifierPresentation.GetName(selected)), sibling.Id);
            }
        }

        [Test]
        public void EveryStandardFamilyHasOneDistinctThematicName()
        {
            var families = Catalog().Modifiers.GroupBy(definition => definition.FamilyId).ToArray();
            Assert.That(families.Length, Is.EqualTo(74));
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var family in families)
            {
                var definition = family.First();
                string name = SpiritModifierPresentation.GetName(definition).Split(new[] { " / " }, StringSplitOptions.None)[0];
                Assert.That(names.Add(name), Is.True, family.Key + " shares another family's name.");
                Assert.That(name.Split(' ').Length, Is.InRange(2, 3), family.Key);
                string typeName = definition.Condition.GetType().Name;
                string fallback = DiagnosticText.Humanize(typeName.Substring(0, typeName.Length - "Condition".Length));
                Assert.That(name, Is.Not.EqualTo(fallback), family.Key + " still uses a condition class name.");
                var variantNames = family.Select(SpiritModifierPresentation.GetName).ToArray();
                Assert.That(variantNames.Distinct().Count(), Is.EqualTo(4), family.Key);
                foreach (var variantName in variantNames)
                    Assert.That(variantName, Does.StartWith(name + " / "), family.Key);
            }
        }

        [TestCase("modifier.initial_roll.first_hand", "Open Gate")]
        [TestCase("modifier.player_reroll.rank_below_before_reroll", "Falling Leaf")]
        [TestCase("modifier.play_hand.edge_pair_sums_equal", "Balanced Scales")]
        [TestCase("modifier.play_hand.out_of_rerolls", "Empty Cup")]
        [TestCase("modifier.play_hand.range_greater_than", "Wide Valley")]
        public void StandardFamiliesUseTheirCuratedNames(string familyId, string expected)
        {
            foreach (var definition in Catalog().Modifiers.Where(value => value.FamilyId == familyId))
                Assert.That(SpiritModifierPresentation.GetName(definition), Does.StartWith(expected + " / "), definition.Id);
        }

        [TestCase("base_score", "Base")]
        [TestCase("multiplier", "Mult")]
        [TestCase("combined", "Base + Mult")]
        [TestCase("rerolls", "Rerolls")]
        public void AuthoredVariantNamesIdentifyTheirEffectRole(string role, string label)
        {
            var variants = Catalog().Modifiers.Where(value => value.Id.EndsWith("." + role, StringComparison.Ordinal)).ToArray();
            Assert.That(variants.Length, Is.EqualTo(74));
            foreach (var definition in variants)
                Assert.That(SpiritModifierPresentation.GetName(definition), Does.EndWith(" / " + label), definition.Id);
            Assert.That(SpiritModifierPresentation.GetName(Catalog().GetModifier(
                "modifier.play_hand.edge_sum_equals_either." + role)), Is.EqualTo("Two Gateposts / " + label));
            Assert.That(Catalog().Modifiers.Select(SpiritModifierPresentation.GetName).Distinct().Count(), Is.EqualTo(296));
        }

        [Test]
        public void UnknownFamilyKeepsItsActualConditionAndEffectFallbacks()
        {
            var definition = new ModifierDefinition("custom.contains_face.multiplier", "custom.contains_face",
                ModifierPhase.PlayHand, new ContainsFaceCondition(4),
                new[] { new ModifierEffect(ModifierEffectKind.AddMultiplier, 7) });
            Assert.That(SpiritModifierPresentation.GetName(definition), Is.EqualTo("Contains Face"));
            Assert.That(SpiritModifierPresentation.GetConditionText(definition), Is.EqualTo("At least one die shows 4."));
            Assert.That(SpiritModifierPresentation.GetEffectText(definition), Is.EqualTo("+7 multiplier"));
        }

        [TestCase("modifier.initial_roll.initial_pair.base_score", "+101 base score")]
        [TestCase("modifier.player_reroll.selected_face_unchanged.rerolls", "+101 rerolls")]
        [TestCase("modifier.player_reroll.last_reroll_on_last_hand.combined", "+101 base score, +102 multiplier")]
        [TestCase("modifier.play_hand.no_extreme_faces.base_score", "+101 base score")]
        [TestCase("modifier.play_hand.contains_face.multiplier", "+101 multiplier")]
        [TestCase("modifier.play_hand.middle_unique.combined", "+101 base score, +102 multiplier")]
        public void EffectTextUsesCurrentDefinitionAmounts(string id, string expected)
        {
            var original = Catalog().GetModifier(id);
            var changed = new ModifierDefinition(original.Id, original.FamilyId, original.Phase, original.Condition,
                original.Effects.Select((effect, index) => new ModifierEffect(effect.Kind, 101 + index)),
                original.ActivationLimit, original.RewardEligible);
            Assert.That(SpiritModifierPresentation.GetEffectText(changed), Is.EqualTo(expected));
            Assert.That(SpiritModifierPresentation.GetEffectText(changed),
                Is.Not.EqualTo(SpiritModifierPresentation.GetEffectText(original)));
            Assert.That(SpiritModifierPresentation.GetName(changed), Is.EqualTo(SpiritModifierPresentation.GetName(original)));
        }

        [Test]
        public void EffectTextUsesCurrentEffectKindsRatherThanTheAuthoredVariantRole()
        {
            var original = Catalog().GetModifier(AuthoredIds[0]);
            var changed = new ModifierDefinition(original.Id, original.FamilyId, original.Phase, original.Condition,
                new[] { new ModifierEffect(ModifierEffectKind.AddRerolls, 3) });
            Assert.That(SpiritModifierPresentation.GetEffectText(changed), Is.EqualTo("+3 rerolls"));
        }

        [TestCase(1, "+1 reroll")]
        [TestCase(2, "+2 rerolls")]
        public void GenericEffectTextUsesCurrentRerollAmountAndGrammar(int amount, string expected)
        {
            var definition = new ModifierDefinition("custom.rerolls", "custom", ModifierPhase.PlayHand,
                new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddRerolls, amount) });
            Assert.That(SpiritModifierPresentation.GetEffectText(definition), Is.EqualTo(expected));
        }

        [Test]
        public void ConditionTextUsesCurrentPredicateParameters()
        {
            var catalog = Catalog();
            var contains = catalog.GetModifier("modifier.play_hand.contains_face.multiplier");
            var changedContains = new ModifierDefinition(contains.Id, contains.FamilyId, contains.Phase,
                new ContainsFaceCondition(4), contains.Effects);
            Assert.That(SpiritModifierPresentation.GetConditionText(changedContains), Is.EqualTo("At least one die shows 4."));

            var extremes = catalog.GetModifier("modifier.play_hand.no_extreme_faces.base_score");
            var changedExtremes = new ModifierDefinition(extremes.Id, extremes.FamilyId, extremes.Phase,
                new NoExtremeFacesCondition(2, 5), extremes.Effects);
            Assert.That(SpiritModifierPresentation.GetConditionText(changedExtremes), Is.EqualTo("No die shows 2 or 5."));
        }

        [Test]
        public void ChangedPredicateFallsBackToItsActualConditionDescription()
        {
            var original = Catalog().GetModifier(AuthoredIds[0]);
            var changed = new ModifierDefinition(original.Id, original.FamilyId, original.Phase,
                new AlwaysCondition(), original.Effects);
            Assert.That(SpiritModifierPresentation.GetConditionText(changed), Is.EqualTo("Always meets its condition."));
        }

        [TestCase(ModifierPhase.InitialRoll, ModifierActivationLimit.OncePerHand, "On initial roll / Once per hand")]
        [TestCase(ModifierPhase.InitialRoll, ModifierActivationLimit.OncePerStage, "On initial roll / Once per stage")]
        [TestCase(ModifierPhase.InitialRoll, ModifierActivationLimit.Unlimited, "On initial roll / Every time")]
        [TestCase(ModifierPhase.PlayerReroll, ModifierActivationLimit.OncePerHand, "After rerolling / Once per hand")]
        [TestCase(ModifierPhase.PlayerReroll, ModifierActivationLimit.OncePerStage, "After rerolling / Once per stage")]
        [TestCase(ModifierPhase.PlayerReroll, ModifierActivationLimit.Unlimited, "After rerolling / Every time")]
        [TestCase(ModifierPhase.PlayHand, ModifierActivationLimit.OncePerHand, "When played / Once per hand")]
        [TestCase(ModifierPhase.PlayHand, ModifierActivationLimit.OncePerStage, "When played / Once per stage")]
        [TestCase(ModifierPhase.PlayHand, ModifierActivationLimit.Unlimited, "When played / Every time")]
        public void TimingTextUsesCurrentPhaseAndActivationLimit(ModifierPhase phase, ModifierActivationLimit limit, string expected)
        {
            var original = Catalog().GetModifier(AuthoredIds[0]);
            var changed = new ModifierDefinition(original.Id, original.FamilyId, phase, original.Condition, original.Effects, limit);
            Assert.That(SpiritModifierPresentation.GetTimingText(changed), Is.EqualTo(expected));
        }

        [Test]
        public void DefinitionRegistrationOverridesArtworkAndRemovalRestoresItWithoutChangingOtherCharms()
        {
            var catalog = Catalog();
            var definition = catalog.GetModifier(AuthoredIds[0]);
            var originals = AuthoredIds.Select(id => SpiritModifierIcons.GetSprite(catalog.GetModifier(id))).ToArray();
            Assert.That(originals[0], Is.Not.Null);
            var replacement = Sprite.Create(originals[0].texture, originals[0].rect, new Vector2(0.5f, 0.5f));
            try
            {
                SpiritModifierIcons.RegisterIcon(definition.Id, replacement);
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.SameAs(replacement));
                for (int i = 1; i < AuthoredIds.Length; ++i)
                    Assert.That(SpiritModifierIcons.GetSprite(catalog.GetModifier(AuthoredIds[i])), Is.SameAs(originals[i]), AuthoredIds[i]);

                SpiritModifierIcons.RegisterIcon(definition.Id, null);
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.SameAs(originals[0]));
            }
            finally
            {
                SpiritModifierIcons.RegisterIcon(definition.Id, null);
                UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void DefinitionRegistrationTakesPriorityOverFamilyAndBothRemovalsRestoreArtwork()
        {
            var catalog = Catalog();
            var definition = catalog.GetModifier(AuthoredIds[0]);
            var sibling = catalog.GetModifier(definition.FamilyId + ".multiplier");
            var original = SpiritModifierIcons.GetSprite(definition);
            Assert.That(original, Is.Not.Null);
            var familyReplacement = Sprite.Create(original.texture, original.rect, new Vector2(0.5f, 0.5f));
            var definitionReplacement = Sprite.Create(original.texture, original.rect, new Vector2(0.5f, 0.5f));
            try
            {
                SpiritModifierIcons.RegisterIcon(definition.FamilyId, familyReplacement);
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.SameAs(familyReplacement));
                Assert.That(SpiritModifierIcons.GetSprite(sibling), Is.SameAs(familyReplacement));
                SpiritModifierIcons.RegisterIcon(definition.Id, definitionReplacement);
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.SameAs(definitionReplacement));
                Assert.That(SpiritModifierIcons.GetSprite(sibling), Is.SameAs(familyReplacement));

                SpiritModifierIcons.RegisterIcon(definition.Id, null);
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.SameAs(familyReplacement));
                SpiritModifierIcons.RegisterIcon(definition.FamilyId, null);
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.SameAs(original));
                Assert.That(SpiritModifierIcons.GetSprite(sibling), Is.Null);
            }
            finally
            {
                SpiritModifierIcons.RegisterIcon(definition.Id, null);
                SpiritModifierIcons.RegisterIcon(definition.FamilyId, null);
                UnityEngine.Object.DestroyImmediate(definitionReplacement);
                UnityEngine.Object.DestroyImmediate(familyReplacement);
            }
        }

        [Test]
        public void ThemeAssetsReferenceEachStandardMechanicalAssetExactlyOnce()
        {
            Assert.That(themeAssets.Length, Is.EqualTo(catalog.Modifiers.Count));
            CollectionAssert.AreEquivalent(TestContent.ModifierAssets(), themeAssets.Select(asset => asset.Modifier));
        }

        [Test]
        public void ThemeInstallationRejectsDuplicatesAndMissingCoverageWithoutReplacingActiveTheme()
        {
            var definition = catalog.GetModifier(AuthoredIds[0]);
            string currentName = SpiritModifierPresentation.GetName(definition);
            Assert.Throws<InvalidOperationException>(() =>
                SpiritModifierPresentation.InstallTheme(themeAssets.Concat(new[] { themeAssets[0] }), catalog));
            Assert.Throws<InvalidOperationException>(() =>
                SpiritModifierPresentation.InstallTheme(themeAssets.Skip(1), catalog));
            Assert.That(SpiritModifierPresentation.GetName(definition), Is.EqualTo(currentName));
        }

        [TestCase("modifier")]
        [TestCase("displayName")]
        public void ThemeInstallationRejectsMissingAuthoredFields(string field)
        {
            var replacement = UnityEngine.Object.Instantiate(themeAssets[0]);
            try
            {
                var serialized = new SerializedObject(replacement);
                SerializedProperty property = serialized.FindProperty(field);
                if (field == "modifier") property.objectReferenceValue = null;
                else property.stringValue = " ";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => SpiritModifierPresentation.InstallTheme(
                    new[] { replacement }.Concat(themeAssets.Skip(1)), catalog));
            }
            finally { UnityEngine.Object.DestroyImmediate(replacement); }
        }

        [Test]
        public void ThemeInstallationRejectsUnknownMechanicalIds()
        {
            var unknown = ScriptableObject.CreateInstance<ModifierAsset>();
            var replacement = UnityEngine.Object.Instantiate(themeAssets[0]);
            try
            {
                var mechanical = new SerializedObject(unknown);
                mechanical.FindProperty("id").stringValue = "custom.unknown";
                mechanical.ApplyModifiedPropertiesWithoutUndo();
                var presentation = new SerializedObject(replacement);
                presentation.FindProperty("modifier").objectReferenceValue = unknown;
                presentation.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => SpiritModifierPresentation.InstallTheme(
                    new[] { replacement }.Concat(themeAssets.Skip(1)), catalog));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacement);
                UnityEngine.Object.DestroyImmediate(unknown);
            }
        }

        [Test]
        public void NewThemeSuppliesNamesAndSpritesAndSurvivesOlderOwnerDisposal()
        {
            var authored = themeAssets.Single(asset => asset.Modifier.Id == AuthoredIds[0]);
            var replacement = UnityEngine.Object.Instantiate(authored);
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f));
            IDisposable newerTheme = null;
            try
            {
                var serialized = new SerializedObject(replacement);
                serialized.FindProperty("displayName").stringValue = "Other Theme";
                serialized.FindProperty("icon").objectReferenceValue = sprite;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                newerTheme = SpiritModifierPresentation.InstallTheme(
                    themeAssets.Select(asset => asset == authored ? replacement : asset), catalog);
                theme.Dispose();
                var definition = catalog.GetModifier(AuthoredIds[0]);
                Assert.That(SpiritModifierPresentation.GetName(definition), Is.EqualTo("Other Theme"));
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.SameAs(sprite));
                newerTheme.Dispose();
                Assert.That(SpiritModifierPresentation.GetName(definition), Is.EqualTo("Initial Pair"));
                Assert.That(SpiritModifierIcons.GetSprite(definition), Is.Null);
            }
            finally
            {
                newerTheme?.Dispose();
                UnityEngine.Object.DestroyImmediate(replacement);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private MechanicalCatalog Catalog() => catalog;

        private static Color32[] ReadPixels(Texture2D texture)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readable.Apply();
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }
    }
}
