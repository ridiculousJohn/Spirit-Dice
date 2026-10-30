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
            var first = synthetic.Charm("test.first");
            var second = synthetic.Charm("test.second");
            var firstIcon = synthetic.Icon();
            var secondIcon = synthetic.Icon();
            var content = SyntheticCharms.Content(first, second);
            var charms = synthetic.Load(synthetic.Skin(new[]
            {
                synthetic.Presentation(second, "Second", "Second condition.", icon: secondIcon),
                synthetic.Presentation(first, "First", "First condition.", icon: firstIcon)
            }), content);

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
            var outside = synthetic.Charm("test.loaded").Build(TestFamilies.Of("test.loaded"));
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
            Assert.That(charms.GetEffectText(Definition(charms)), Is.EqualTo("[10 sparks] & [7 flames]"));
        }

        /** Checks an amount of one takes the singular label and any other amount the plural / fails if the grammar ignores the amount. */
        [TestCase(1, "[1 spark]")]
        [TestCase(2, "[2 sparks]")]
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
            var content = SyntheticCharms.Content(both, single);
            var charms = synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(both), synthetic.Presentation(single) },
                wording: new[]
                {
                    new ModifierEffectWording(ModifierEffectKind.AddBaseScore, "spark", "sparks", "S"),
                    new ModifierEffectWording(ModifierEffectKind.AddMultiplier, "flame", "flames", "F")
                }), content);
            Assert.That(charms.GetCompactEffectText(content.GetDefinition(both)), Is.EqualTo("[10 S] # [7 F]"));
            Assert.That(charms.GetCompactEffectText(content.GetDefinition(single)), Is.EqualTo("[3]"));
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
            Assert.That(charms.GetTimingText(Definition(charms)), Is.EqualTo("At dusk @ Rarely"));
        }

        /**
         * Checks the reward description fills the wording's description template with the skin's phase lead, the
         * condition with numbers and the wording's terms emphasized, and the limit's note unless the phase implies the
         * limit or the note is empty / fails if the lead or note is not the skin's, the template is not the wording's, an
         * implied limit is noted or a note is dropped.
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
            var wording = new SpiritUiWording.CharmText
            {
                description = "{lead} ~ {condition}", descriptionWithNote = "{lead} ~ {condition} ~ {note}", highlightedTerms = new[] { "shows" }
            };
            string text = SpiritRewardCard.GetModifierDescription(charms, definition, wording, Color.red);
            Assert.That(text, Does.StartWith("<b><color=#FF0000>When dusk falls</color></b> ~ "));
            Assert.That(text, Does.Contain("<b><color=#FF0000>" + face + "</color></b>"));
            Assert.That(text, Does.Contain("<b><color=#FF0000>Shows</color></b>"), "Highlighted terms match regardless of case.");
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
            string expected = "When dusk falls ~ " + charms.GetConditionText(definition) + (expectedNote.Length > 0 ? " ~ " + expectedNote : "");
            Assert.That(plain, Is.EqualTo(expected));
        }

        /** Checks contribution text words each nonzero part with the skin's contribution labels / fails if a label is not the skin's, a zero part shows or the reroll grammar ignores the count. */
        [TestCase(3, 0, 0, "[3 grains]")]
        [TestCase(0, 2, 0, "[2 sparks]")]
        [TestCase(0, 0, 1, "[1 chance]")]
        [TestCase(0, 0, 2, "[2 chances]")]
        [TestCase(4, 5, 2, "[4 grains] ; [5 sparks] ; [2 chances]")]
        [TestCase(0, 0, 0, "")]
        public void ContributionTextUsesTheSkinsContributionWording(long baseScore, long multiplier, long rerolls, string expected)
        {
            var charm = synthetic.Charm("test.contribution");
            var charms = synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm) },
                contribution: new ModifierContributionWording("grains", "sparks", "chance", "chances")), SyntheticCharms.Content(charm));
            Assert.That(charms.GetContributionText(baseScore, multiplier, rerolls), Is.EqualTo(expected));
        }

        /**
         * Checks each charm shows the category, styling and mark the skin maps the set of its effect kinds to, whatever
         * the order or number of its effects / fails if the category is not the one of exactly the charm's effect kinds,
         * or a repeated kind or the effects' order changes it.
         */
        [Test]
        public void EachCharmShowsTheCategoryOfItsSetOfEffectKinds()
        {
            var baseScore = synthetic.Category(synthetic.Icon(), new Color(.9f, .6f, .2f), new Color(.5f, .3f, .1f));
            var combined = synthetic.Category(synthetic.Icon(), new Color(.2f, .6f, .9f), new Color(.1f, .3f, .5f));
            var rerolls = synthetic.Category(synthetic.Icon());
            var none = synthetic.Category();
            var single = synthetic.Charm("test.base_score", effects: new AddBaseScoreEffectConfig(10));
            var both = synthetic.Charm("test.both", effects: new ModifierEffectConfig[]
            {
                new AddMultiplierEffectConfig(2), new AddBaseScoreEffectConfig(10), new AddBaseScoreEffectConfig(5)
            });
            var reroll = synthetic.Charm("test.rerolls", effects: new AddRerollsEffectConfig(1));
            var inert = synthetic.Charm("test.inert");
            var serialized = new SerializedObject(inert);
            serialized.FindProperty("effects").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var content = SyntheticCharms.Content(single, both, reroll, inert);
            var charms = synthetic.Load(synthetic.Skin(new[] { single, both, reroll, inert }.Select(charm => synthetic.Presentation(charm)), new[]
            {
                new ModifierEffectsCategory(rerolls, ModifierEffectKind.AddRerolls),
                new ModifierEffectsCategory(combined, ModifierEffectKind.AddBaseScore, ModifierEffectKind.AddMultiplier),
                new ModifierEffectsCategory(none),
                new ModifierEffectsCategory(baseScore, ModifierEffectKind.AddBaseScore)
            }), content);

            Assert.That(charms.GetCategory(content.GetDefinition(single)), Is.SameAs(baseScore));
            Assert.That(charms.GetCategory(content.GetDefinition(both)), Is.SameAs(combined));
            Assert.That(charms.GetCategory(content.GetDefinition(reroll)), Is.SameAs(rerolls));
            Assert.That(charms.GetCategory(content.GetDefinition(inert)), Is.SameAs(none));
            var definition = content.GetDefinition(both);
            Assert.That(charms.GetEffectColor(definition), Is.EqualTo(combined.AccentColor));
            Assert.That(charms.GetInkColor(definition), Is.EqualTo(combined.InkColor));
            Assert.That(charms.GetCategorySprite(definition), Is.SameAs(combined.Symbol));
        }

        /** Checks a charm whose set of effect kinds the skin maps to no category fails at load naming the charm / fails if it loads with another set's category. */
        [Test]
        public void ACharmWhoseEffectKindsHaveNoCategoryFailsAtLoadNamingTheCharm()
        {
            var charm = synthetic.Charm("test.unmatched", effects: new ModifierEffectConfig[]
            {
                new AddBaseScoreEffectConfig(10), new AddRerollsEffectConfig(1)
            });
            var category = synthetic.Category();
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm) }, new[]
            {
                new ModifierEffectsCategory(category, ModifierEffectKind.AddBaseScore),
                new ModifierEffectsCategory(category, ModifierEffectKind.AddRerolls)
            });
            var failure = Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
            Assert.That(failure.Message, Does.Contain("test.unmatched"));
        }

        /**
         * Checks a skin entry without a category, repeating an effect kind or mapping the set of another entry fails at
         * load naming the skin / fails if an incomplete or ambiguous mapping loads.
         */
        [TestCase("no category")]
        [TestCase("repeated kind")]
        [TestCase("same set")]
        public void AFaultyCategoryEntryFailsAtLoadNamingTheSkin(string fault)
        {
            var charm = synthetic.Charm("test.charm");
            var first = synthetic.Category();
            var second = synthetic.Category();
            var faulty = fault == "no category" ? new ModifierEffectsCategory(null, ModifierEffectKind.AddMultiplier) :
                fault == "repeated kind" ? new ModifierEffectsCategory(second, ModifierEffectKind.AddMultiplier, ModifierEffectKind.AddMultiplier) :
                new ModifierEffectsCategory(second, ModifierEffectKind.AddMultiplier, ModifierEffectKind.AddBaseScore);
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm) }, new[]
            {
                new ModifierEffectsCategory(first, ModifierEffectKind.AddBaseScore),
                new ModifierEffectsCategory(first, ModifierEffectKind.AddBaseScore, ModifierEffectKind.AddMultiplier),
                faulty
            });
            var failure = Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
            Assert.That(failure.Message, Does.Contain(skin.name));
        }

        /** Checks every variant of a family shows the skin's name of the family / fails if the name comes from a variant's own display name. */
        [Test]
        public void EachVariantShowsTheSkinsNameOfItsFamily()
        {
            var first = synthetic.Charm("test.first", "family.shared");
            var second = synthetic.Charm("test.second", "family.shared");
            var other = synthetic.Charm("test.other", "family.other");
            var content = SyntheticCharms.Content(first, second, other);
            var charms = synthetic.Load(synthetic.Skin(new[]
            {
                synthetic.Presentation(first, "Shared / First"),
                synthetic.Presentation(second, "Second"),
                synthetic.Presentation(other, "Other / Variant")
            }, families: new[]
            {
                new ModifierFamilyWording(synthetic.Family("family.shared"), "Shared family"),
                new ModifierFamilyWording(synthetic.Family("family.other"), "Other family")
            }), content);

            Assert.That(charms.GetFamilyName(content.GetDefinition(first)), Is.EqualTo("Shared family"));
            Assert.That(charms.GetFamilyName(content.GetDefinition(second)), Is.EqualTo("Shared family"));
            Assert.That(charms.GetFamilyName(content.GetDefinition(other)), Is.EqualTo("Other family"));
        }

        /** Checks a content family without a name, with a blank name or named twice fails at load / fails if a family loads unnamed or ambiguous. */
        [TestCase("missing")]
        [TestCase("blank")]
        [TestCase("twice")]
        public void AFamilyWithoutExactlyOneNameFailsAtLoad(string fault)
        {
            var charm = synthetic.Charm("test.charm", "family.test");
            var families = fault == "missing" ? new ModifierFamilyWording[0] :
                fault == "blank" ? new[] { new ModifierFamilyWording(synthetic.Family("family.test"), " ") } :
                new[] { new ModifierFamilyWording(synthetic.Family("family.test"), "One"), new ModifierFamilyWording(synthetic.Family("family.test"), "Two") };
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm) }, families: families);
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
        }

        /** Checks a content charm without a presentation in the skin fails at load / fails if the charm loads without presentation. */
        [Test]
        public void ACharmWithoutPresentationFailsAtLoad()
        {
            var presented = synthetic.Charm("test.presented");
            var missing = synthetic.Charm("test.missing");
            var skin = synthetic.Skin(new[] { synthetic.Presentation(presented) });
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(presented, missing)));
        }

        /** Checks two presentations of one charm fail at load / fails if either silently wins. */
        [Test]
        public void TwoPresentationsOfOneCharmFailAtLoad()
        {
            var charm = synthetic.Charm("test.twice");
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm, "First"), synthetic.Presentation(charm, "Second") });
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
        }

        /** Checks a presentation of a charm the content does not use fails at load / fails if a skin can present unknown charms. */
        [Test]
        public void APresentationOfACharmOutsideTheContentFailsAtLoad()
        {
            var used = synthetic.Charm("test.used");
            var unused = synthetic.Charm("test.unused");
            var skin = synthetic.Skin(new[] { synthetic.Presentation(used), synthetic.Presentation(unused) });
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
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm) },
                contribution: new ModifierContributionWording(baseScore, multiplier, reroll, rerolls));
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
        }

        /** Checks a text format missing any field fails at load naming it, while spaces suffice for a separator / fails if charm text could be joined without the skin's format. */
        [TestCase("effect")]
        [TestCase("effectSeparator")]
        [TestCase("compactEffect")]
        [TestCase("labeledCompactEffect")]
        [TestCase("compactEffectSeparator")]
        [TestCase("timing")]
        [TestCase("contribution")]
        [TestCase("contributionSeparator")]
        public void IncompleteTextFormatFailsAtLoadNamingTheField(string field)
        {
            var charm = synthetic.Charm("test.format");
            string Value(string name, string value) => name == field ? "" : value;
            var format = new ModifierTextFormat(Value("effect", "e"), Value("effectSeparator", " "), Value("compactEffect", "c"),
                Value("labeledCompactEffect", "l"), Value("compactEffectSeparator", "\n"), Value("timing", "t"),
                Value("contribution", "p"), Value("contributionSeparator", " "));
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm) }, format: format);
            var failure = Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
            Assert.That(failure.Message, Does.Contain("text format " + field + "."));
        }

        /** Checks a charm without effects fails at load when the skin has no text for it / fails if its effect text would be empty. */
        [Test]
        public void ACharmWithoutEffectsFailsAtLoadWithoutNoEffectsText()
        {
            var charm = synthetic.Charm("test.inert");
            var serialized = new SerializedObject(charm);
            serialized.FindProperty("effects").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var skin = synthetic.Skin(new[] { synthetic.Presentation(charm) }, noEffectsText: " ");
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
            var content = SyntheticCharms.Content(charm);
            var charms = synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm) },
                noEffectsText: "Nothing happens"), content);
            Assert.That(charms.GetEffectText(content.GetDefinition(charm)), Is.EqualTo("Nothing happens"));
        }

        /** Checks a presentation without its charm reference, name or condition text fails at load / fails if an incomplete presentation loads. */
        [TestCase("modifier")]
        [TestCase("displayName")]
        [TestCase("conditionTemplate")]
        public void AnIncompletePresentationFailsAtLoad(string field)
        {
            var charm = synthetic.Charm("test.incomplete");
            var presentation = synthetic.Presentation(charm);
            var serialized = new SerializedObject(presentation);
            SerializedProperty property = serialized.FindProperty(field);
            if (property.propertyType == SerializedPropertyType.ObjectReference) property.objectReferenceValue = null;
            else property.stringValue = " ";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var skin = synthetic.Skin(new[] { presentation });
            Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
        }

        /** Checks a presentation without an icon fails at load with a message naming the charm / fails if a charm loads without artwork or the failure does not say which charm. */
        [Test]
        public void APresentationWithoutAnIconFailsAtLoadNamingTheCharm()
        {
            var charm = synthetic.Charm("test.without_icon");
            var presentation = synthetic.Presentation(charm);
            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("icon").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var skin = synthetic.Skin(new[] { presentation });
            var error = Assert.Throws<InvalidOperationException>(() => synthetic.Load(skin, SyntheticCharms.Content(charm)));
            Assert.That(error.Message, Does.Contain("test.without_icon"));
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
            return synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm, template: template) }),
                SyntheticCharms.Content(charm));
        }

        /** A presentation of one charm with the given effect wording. */
        private ModifierPresentationCatalog Worded(ModifierAsset charm, ModifierEffectWording[] wording)
        {
            return synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm) }, wording: wording),
                SyntheticCharms.Content(charm));
        }

        /** A presentation of one charm with the given phase and activation limit wording. */
        private ModifierPresentationCatalog Timed(ModifierAsset charm, ModifierPhaseWording[] phases,
            ModifierActivationLimitWording[] limits, string template = "Test condition.")
        {
            return synthetic.Load(synthetic.Skin(new[] { synthetic.Presentation(charm, template: template) },
                phases: phases, limits: limits), SyntheticCharms.Content(charm));
        }

        private static ModifierDefinition Definition(ModifierPresentationCatalog charms) => charms.Catalog.Modifiers.Single();
    }
}
