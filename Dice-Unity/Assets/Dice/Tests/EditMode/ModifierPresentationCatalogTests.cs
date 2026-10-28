using System;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    /** The charm presentation built at load from a skin and the loaded content, over test-only charms and skins. */
    public sealed class ModifierPresentationCatalogTests
    {
        private SyntheticCharms synthetic;

        [SetUp]
        public void SetUp() => synthetic = new SyntheticCharms();

        [TearDown]
        public void TearDown() => synthetic.Dispose();

        /** Checks each charm gets the name, condition text and icon of its own presentation / fails if charms share or swap their presentation. */
        [Test]
        public void EachCharmShowsItsOwnPresentationsNameConditionAndIcon()
        {
            var category = synthetic.Category();
            var first = synthetic.Charm("test.first");
            var second = synthetic.Charm("test.second");
            var firstIcon = synthetic.Icon();
            var secondIcon = synthetic.Icon();
            var content = SyntheticCharms.Content(first, second);
            var charms = synthetic.Load(synthetic.Skin(new[]
            {
                synthetic.Presentation(second, category, "Second", "Second condition.", icon: secondIcon),
                synthetic.Presentation(first, category, "First", "First condition.", icon: firstIcon)
            }, new[] { category }), content);

            var firstDefinition = content.GetDefinition(first);
            var secondDefinition = content.GetDefinition(second);
            Assert.That(charms.GetName(firstDefinition), Is.EqualTo("First"));
            Assert.That(charms.GetConditionText(firstDefinition), Is.EqualTo("First condition."));
            Assert.That(charms.GetIcon(firstDefinition), Is.SameAs(firstIcon));
            Assert.That(charms.GetName(secondDefinition), Is.EqualTo("Second"));
            Assert.That(charms.GetConditionText(secondDefinition), Is.EqualTo("Second condition."));
            Assert.That(charms.GetIcon(secondDefinition), Is.SameAs(secondIcon));
        }

        /** Checks a definition outside the loaded content has no presentation / fails if a charm without presentation is named or drawn anyway. */
        [Test]
        public void ADefinitionOutsideTheLoadedContentHasNoPresentation()
        {
            var charms = Single(synthetic.Charm("test.loaded"));
            var outside = synthetic.Charm("test.loaded").Build();
            Assert.Throws<InvalidOperationException>(() => charms.GetName(outside));
            Assert.Throws<InvalidOperationException>(() => charms.GetIcon(outside));
        }

        /** Checks the placeholders show the referenced charm's parameters / fails if either is left unfilled or swapped. */
        [Test]
        public void ConditionTextFillsPlaceholdersFromTheReferencedMechanicalParameters()
        {
            const int parameter = 2;
            const int secondaryParameter = 5;
            var charm = synthetic.Charm("test.extremes", condition: new NoExtremeFacesConditionConfig(parameter, secondaryParameter));
            var charms = Single(charm, "Avoids {parameter} and {secondaryParameter}.");
            Assert.That(charms.GetConditionText(Definition(charms)), Is.EqualTo("Avoids " + parameter + " and " + secondaryParameter + "."));
        }

        /** Checks changed charm parameters appear in the text at once / fails if the text keeps the values it was loaded with. */
        [Test]
        public void ConditionTextFollowsChangedMechanicalParameters()
        {
            var charm = synthetic.Charm("test.extremes", condition: new NoExtremeFacesConditionConfig(1, 6));
            var charms = Single(charm, "Avoids {parameter} and {secondaryParameter}.");
            const int parameter = 3;
            const int secondaryParameter = 4;
            var mechanical = new SerializedObject(charm);
            mechanical.FindProperty("condition.firstFace").intValue = parameter;
            mechanical.FindProperty("condition.secondFace").intValue = secondaryParameter;
            mechanical.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(charms.GetConditionText(Definition(charms)), Is.EqualTo("Avoids " + parameter + " and " + secondaryParameter + "."));
        }

        /** Checks condition text without placeholders is shown exactly as written / fails if the text is altered. */
        [Test]
        public void ConditionTextWithoutPlaceholdersIsUsedAsWritten()
        {
            var charm = synthetic.Charm("test.pair", condition: new InitialPairConditionConfig());
            var charms = Single(charm, "Your opening dice pair up.");
            Assert.That(charms.GetConditionText(Definition(charms)), Is.EqualTo("Your opening dice pair up."));
        }

        /** Checks effect text words each effect with the skin's label for its kind and its current amount / fails if wording is not taken from the skin or an effect is dropped. */
        [Test]
        public void EffectTextUsesTheSkinsWordingForEachEffect()
        {
            var charm = synthetic.Charm("test.both", effects: new ModifierEffectConfig[] { new AddBaseScoreEffectConfig(10), new AddMultiplierEffectConfig(7) });
            var charms = Worded(charm, new[]
            {
                new ModifierEffectWording(ModifierEffectKind.AddBaseScore, "spark", "sparks", "S"),
                new ModifierEffectWording(ModifierEffectKind.AddMultiplier, "flame", "flames", "F")
            });
            Assert.That(charms.GetEffectText(Definition(charms)), Is.EqualTo("+10 sparks, +7 flames"));
        }

        /** Checks an amount of one takes the singular label and any other amount the plural / fails if the grammar ignores the amount. */
        [TestCase(1, "+1 spark")]
        [TestCase(2, "+2 sparks")]
        public void EffectTextUsesTheSingularLabelForOneAndThePluralOtherwise(int amount, string expected)
        {
            var charm = synthetic.Charm("test.amount", effects: new AddRerollsEffectConfig(amount));
            var charms = Worded(charm, new[] { new ModifierEffectWording(ModifierEffectKind.AddRerolls, "spark", "sparks", "S") });
            Assert.That(charms.GetEffectText(Definition(charms)), Is.EqualTo(expected));
        }

        /** Checks a charm with several effects marks each compact line with its kind's compact label and a single effect is unmarked / fails if compact labels are not the skin's or a single effect is marked. */
        [Test]
        public void CompactEffectTextMarksEachEffectOfAMultiEffectCharm()
        {
            var both = synthetic.Charm("test.both", effects: new ModifierEffectConfig[] { new AddBaseScoreEffectConfig(10), new AddMultiplierEffectConfig(7) });
            var single = synthetic.Charm("test.single", effects: new AddBaseScoreEffectConfig(3));
            var category = synthetic.Category();
            var content = SyntheticCharms.Content(both, single);
            var charms = synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(both, category), synthetic.Presentation(single, category) },
                new[] { category }, new[]
                {
                    new ModifierEffectWording(ModifierEffectKind.AddBaseScore, "spark", "sparks", "S"),
                    new ModifierEffectWording(ModifierEffectKind.AddMultiplier, "flame", "flames", "F")
                }), content);
            Assert.That(charms.GetCompactEffectText(content.GetDefinition(both)), Is.EqualTo("+10 S\n+7 F"));
            Assert.That(charms.GetCompactEffectText(content.GetDefinition(single)), Is.EqualTo("+3"));
        }

        /** Checks timing text joins the skin's labels for the charm's phase and activation limit / fails if either label is not the skin's. */
        [TestCase(ModifierPhase.InitialRoll, ModifierActivationLimit.OncePerHand)]
        [TestCase(ModifierPhase.PlayerReroll, ModifierActivationLimit.OncePerStage)]
        [TestCase(ModifierPhase.PlayHand, ModifierActivationLimit.Unlimited)]
        public void TimingTextUsesTheSkinsPhaseAndLimitLabels(ModifierPhase phase, ModifierActivationLimit limit)
        {
            var charms = Timed(synthetic.Charm("test.timing", phase: phase, limit: limit),
                new[] { new ModifierPhaseWording(phase, "At dusk", "When dusk falls") },
                new[] { new ModifierActivationLimitWording(limit, "Rarely", "Only rarely.") });
            Assert.That(charms.GetTimingText(Definition(charms)), Is.EqualTo("At dusk / Rarely"));
        }

        /**
         * Checks the reward description leads with the skin's phase lead and closes with the limit's note unless the
         * phase implies the limit or the note is empty / fails if the lead or note is not the skin's, an implied limit is
         * noted or a note is dropped.
         */
        [TestCase(false, "Only rarely.", "Only rarely.")]
        [TestCase(true, "Only rarely.", "")]
        [TestCase(false, "", "")]
        public void RewardDescriptionUsesTheSkinsLeadAndLimitNote(bool phaseImpliesLimit, string note, string expectedNote)
        {
            const int face = 4;
            var charm = synthetic.Charm("test.contains_face", phase: ModifierPhase.PlayerReroll, condition: new ContainsFaceConditionConfig(face),
                limit: ModifierActivationLimit.OncePerStage, effects: new AddMultiplierEffectConfig(2));
            var implied = phaseImpliesLimit ? new[] { ModifierActivationLimit.OncePerStage } : new ModifierActivationLimit[0];
            var charms = Timed(charm, new[] { new ModifierPhaseWording(ModifierPhase.PlayerReroll, "At dusk", "When dusk falls", implied) },
                new[] { new ModifierActivationLimitWording(ModifierActivationLimit.OncePerStage, "Rarely", note) },
                "Shows a {parameter}.");
            var definition = Definition(charms);
            string text = SpiritRewardCard.GetModifierDescription(charms, definition, Color.red);
            Assert.That(text, Does.StartWith("<b><color=#FF0000>When dusk falls</color></b>: "));
            Assert.That(text, Does.Contain("<b><color=#FF0000>" + face + "</color></b>"));
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
            string expected = "When dusk falls: " + charms.GetConditionText(definition) + (expectedNote.Length > 0 ? " " + expectedNote : "");
            Assert.That(plain, Is.EqualTo(expected));
        }

        /** Checks contribution text words each nonzero part with the skin's contribution labels / fails if a label is not the skin's, a zero part shows or the reroll grammar ignores the count. */
        [TestCase(3, 0, 0, "+3 grains")]
        [TestCase(0, 2, 0, "+2 sparks")]
        [TestCase(0, 0, 1, "+1 chance")]
        [TestCase(0, 0, 2, "+2 chances")]
        [TestCase(4, 5, 2, "+4 grains  /  +5 sparks  /  +2 chances")]
        [TestCase(0, 0, 0, "")]
        public void ContributionTextUsesTheSkinsContributionWording(long baseScore, long multiplier, long rerolls, string expected)
        {
            var charm = synthetic.Charm("test.contribution");
            var category = synthetic.Category();
            var charms = synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm, category) }, new[] { category },
                contribution: new ModifierContributionWording("grains", "sparks", "chance", "chances")), SyntheticCharms.Content(charm));
            Assert.That(charms.GetContributionText(baseScore, multiplier, rerolls), Is.EqualTo(expected));
        }

        /** Checks category styling and mark come from the presentation's category even when the effects suggest another role / fails if styling or the mark is inferred from effects or identity. */
        [Test]
        public void CategoryStylingComesFromThePresentationsCategory()
        {
            var rerolls = synthetic.Category(synthetic.Icon(), new Color(.2f, .6f, .9f), new Color(.1f, .3f, .5f));
            var charm = synthetic.Charm("test.lookup.base_score", effects: new AddBaseScoreEffectConfig(10));
            var content = SyntheticCharms.Content(charm);
            var charms = synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm, rerolls) }, new[] { rerolls }), content);
            var definition = content.GetDefinition(charm);
            Assert.That(charms.GetCategory(definition), Is.SameAs(rerolls));
            Assert.That(charms.GetEffectColor(definition), Is.EqualTo(rerolls.AccentColor));
            Assert.That(charms.GetInkColor(definition), Is.EqualTo(rerolls.InkColor));
            Assert.That(charms.GetCategorySprite(definition), Is.SameAs(rerolls.Symbol));
        }

        /** Checks a content charm without a presentation in the skin fails at load / fails if the charm loads without presentation. */
        [Test]
        public void ACharmWithoutPresentationFailsAtLoad()
        {
            var category = synthetic.Category();
            var presented = synthetic.Charm("test.presented");
            var missing = synthetic.Charm("test.missing");
            var skin = synthetic.Skin(new[] { synthetic.Presentation(presented, category) }, new[] { category });
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(presented, missing)));
        }

        /** Checks two presentations of one charm fail at load / fails if either silently wins. */
        [Test]
        public void TwoPresentationsOfOneCharmFailAtLoad()
        {
            var category = synthetic.Category();
            var charm = synthetic.Charm("test.twice");
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm, category, "First"), synthetic.Presentation(charm, category, "Second") },
                new[] { category });
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
        }

        /** Checks a presentation of a charm the content does not use fails at load / fails if a skin can present unknown charms. */
        [Test]
        public void APresentationOfACharmOutsideTheContentFailsAtLoad()
        {
            var category = synthetic.Category();
            var used = synthetic.Charm("test.used");
            var unused = synthetic.Charm("test.unused");
            var skin = synthetic.Skin(new[] { synthetic.Presentation(used, category), synthetic.Presentation(unused, category) },
                new[] { category });
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(used)));
        }

        /** Checks an effect kind the content uses without wording in the skin fails at load / fails if an effect loads unworded. */
        [Test]
        public void AnEffectKindWithoutWordingFailsAtLoad()
        {
            var charm = synthetic.Charm("test.rerolls", effects: new AddRerollsEffectConfig(1));
            Assert.Throws<InvalidOperationException>(() => Worded(charm,
                new[] { new ModifierEffectWording(ModifierEffectKind.AddBaseScore, "spark", "sparks", "S") }));
        }

        /** Checks a phase the content uses without wording in the skin fails at load / fails if a charm loads without timing text. */
        [Test]
        public void APhaseWithoutWordingFailsAtLoad()
        {
            var charm = synthetic.Charm("test.reroll", phase: ModifierPhase.PlayerReroll);
            Assert.Throws<InvalidOperationException>(() => Timed(charm,
                new[] { new ModifierPhaseWording(ModifierPhase.PlayHand, "At dusk", "When dusk falls") }, SyntheticCharms.LimitWording()));
        }

        /** Checks an activation limit the content uses without wording in the skin fails at load / fails if a charm loads without limit text. */
        [Test]
        public void AnActivationLimitWithoutWordingFailsAtLoad()
        {
            var charm = synthetic.Charm("test.stage", limit: ModifierActivationLimit.OncePerStage);
            Assert.Throws<InvalidOperationException>(() => Timed(charm, SyntheticCharms.PhaseWording(),
                new[] { new ModifierActivationLimitWording(ModifierActivationLimit.OncePerHand, "Rarely", "Only rarely.") }));
        }

        /** Checks phase wording without a timing label or description lead fails at load / fails if incomplete phase wording loads. */
        [TestCase(" ", "When dusk falls")]
        [TestCase("At dusk", " ")]
        public void IncompletePhaseWordingFailsAtLoad(string timingLabel, string descriptionLead)
        {
            var charm = synthetic.Charm("test.phase", phase: ModifierPhase.InitialRoll);
            Assert.Throws<InvalidOperationException>(() => Timed(charm,
                new[] { new ModifierPhaseWording(ModifierPhase.InitialRoll, timingLabel, descriptionLead) }, SyntheticCharms.LimitWording()));
        }

        /** Checks activation limit wording without a timing label fails at load / fails if incomplete limit wording loads. */
        [Test]
        public void IncompleteActivationLimitWordingFailsAtLoad()
        {
            var charm = synthetic.Charm("test.limit", limit: ModifierActivationLimit.Unlimited);
            Assert.Throws<InvalidOperationException>(() => Timed(charm, SyntheticCharms.PhaseWording(),
                new[] { new ModifierActivationLimitWording(ModifierActivationLimit.Unlimited, " ", "") }));
        }

        /** Checks a phase worded twice fails at load / fails if either wording silently wins. */
        [Test]
        public void APhaseWordedTwiceFailsAtLoad()
        {
            var charm = synthetic.Charm("test.twice", phase: ModifierPhase.PlayHand);
            Assert.Throws<InvalidOperationException>(() => Timed(charm, new[]
            {
                new ModifierPhaseWording(ModifierPhase.PlayHand, "At dusk", "When dusk falls"),
                new ModifierPhaseWording(ModifierPhase.PlayHand, "At dawn", "When dawn breaks")
            }, SyntheticCharms.LimitWording()));
        }

        /** Checks contribution wording missing any label fails at load / fails if score playback could show an unworded part. */
        [TestCase(" ", "sparks", "chance", "chances")]
        [TestCase("grains", " ", "chance", "chances")]
        [TestCase("grains", "sparks", " ", "chances")]
        [TestCase("grains", "sparks", "chance", " ")]
        public void IncompleteContributionWordingFailsAtLoad(string baseScore, string multiplier, string reroll, string rerolls)
        {
            var charm = synthetic.Charm("test.contribution");
            var category = synthetic.Category();
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm, category) }, new[] { category },
                contribution: new ModifierContributionWording(baseScore, multiplier, reroll, rerolls));
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
        }

        /** Checks a charm without effects fails at load when the skin has no text for it / fails if its effect text would be empty. */
        [Test]
        public void ACharmWithoutEffectsFailsAtLoadWithoutNoEffectsText()
        {
            var charm = synthetic.Charm("test.inert");
            var serialized = new SerializedObject(charm);
            serialized.FindProperty("effects").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var category = synthetic.Category();
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm, category) }, new[] { category }, noEffectsText: " ");
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
        }

        /** Checks the skin's text for a charm without effects is its effect text / fails if the text is not taken from the skin. */
        [Test]
        public void ACharmWithoutEffectsShowsTheSkinsNoEffectsText()
        {
            var charm = synthetic.Charm("test.inert");
            var serialized = new SerializedObject(charm);
            serialized.FindProperty("effects").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var category = synthetic.Category();
            var content = SyntheticCharms.Content(charm);
            var charms = synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm, category) }, new[] { category },
                noEffectsText: "Nothing happens"), content);
            Assert.That(charms.GetEffectText(content.GetDefinition(charm)), Is.EqualTo("Nothing happens"));
        }

        /** Checks a presentation without its charm reference, name, condition text or category fails at load / fails if an incomplete presentation loads. */
        [TestCase("modifier")]
        [TestCase("displayName")]
        [TestCase("conditionTemplate")]
        [TestCase("category")]
        public void AnIncompletePresentationFailsAtLoad(string field)
        {
            var category = synthetic.Category();
            var charm = synthetic.Charm("test.incomplete");
            var presentation = synthetic.Presentation(charm, category);
            var serialized = new SerializedObject(presentation);
            SerializedProperty property = serialized.FindProperty(field);
            if (property.propertyType == SerializedPropertyType.ObjectReference) property.objectReferenceValue = null;
            else property.stringValue = " ";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var skin = synthetic.Skin(new[] { presentation }, new[] { category });
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
        }

        /** Checks a presentation without an icon fails at load with a message naming the charm / fails if a charm loads without artwork or the failure does not say which charm. */
        [Test]
        public void APresentationWithoutAnIconFailsAtLoadNamingTheCharm()
        {
            var category = synthetic.Category();
            var charm = synthetic.Charm("test.without_icon");
            var presentation = synthetic.Presentation(charm, category);
            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("icon").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var skin = synthetic.Skin(new[] { presentation }, new[] { category });
            var error = Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
            Assert.That(error.Message, Does.Contain("test.without_icon"));
        }

        /** Checks a presentation whose category the skin does not list fails at load / fails if an unlisted category is used. */
        [Test]
        public void ACategoryTheSkinDoesNotListFailsAtLoad()
        {
            var listed = synthetic.Category();
            var unlisted = synthetic.Category();
            var charm = synthetic.Charm("test.unlisted");
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm, unlisted) }, new[] { listed });
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
        }

        /** Checks condition text with a placeholder other than {parameter} or {secondaryParameter} fails at load / fails if a broken placeholder loads. */
        [TestCase("Shows {face}.")]
        [TestCase("Shows {Parameter}.")]
        [TestCase("Shows {parameter.")]
        [TestCase("Shows parameter}.")]
        public void AnUnknownPlaceholderFailsAtLoad(string template)
        {
            var charm = synthetic.Charm("test.placeholder");
            Assert.Throws<InvalidOperationException>(() => Single(charm, template));
        }

        /** A presentation of one charm with the given condition text and the standard test wording. */
        private ModifierPresentationCatalog Single(ModifierAsset charm, string template = "Test condition.")
        {
            var category = synthetic.Category();
            return synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm, category, template: template) }, new[] { category }),
                SyntheticCharms.Content(charm));
        }

        /** A presentation of one charm with the given effect wording. */
        private ModifierPresentationCatalog Worded(ModifierAsset charm, ModifierEffectWording[] wording)
        {
            var category = synthetic.Category();
            return synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm, category) }, new[] { category }, wording),
                SyntheticCharms.Content(charm));
        }

        /** A presentation of one charm with the given phase and activation limit wording. */
        private ModifierPresentationCatalog Timed(ModifierAsset charm, ModifierPhaseWording[] phases,
            ModifierActivationLimitWording[] limits, string template = "Test condition.")
        {
            var category = synthetic.Category();
            return synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm, category, template: template) }, new[] { category },
                phases: phases, limits: limits), SyntheticCharms.Content(charm));
        }

        private static ModifierDefinition Definition(ModifierPresentationCatalog charms) => charms.Catalog.Modifiers.Single();
    }
}
