using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** A reward card over a synthetic hierarchy with test colors: its charm and hand upgrade modes and their selection. */
    public sealed class RewardCardBindingTests
    {
        private static readonly Color ModifierColor = new Color(.1f, .2f, .3f, 1);
        private static readonly Color ModifierSelectedColor = new Color(.9f, .1f, .8f, 1);
        private static readonly Color HandColor = new Color(.3f, .2f, .1f, .8f);
        private static readonly Color HandSelectedColor = new Color(.1f, .9f, .9f, 1);
        private static readonly Color TitleColor = new Color(.9f, .8f, .7f, 1);
        private static readonly Color SelectedTitleColor = new Color(1, .9f, 0, 1);
        private static readonly Color HandLevelColor = new Color(.5f, .6f, .7f, 1);

        private SyntheticUi ui;
        private readonly SyntheticCharms synthetic = new SyntheticCharms();
        private ModifierDefinition charm;
        private ModifierPresentationCatalog charms;
        private SpiritRewardCard card;
        private GameObject modifierRoot;
        private GameObject handRoot;
        private Image modifierBackground;
        private Image handBackground;
        private Text title;
        private Text handTitle;
        private Text handLevel;
        private Image marker;
        private Image handPattern;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            charm = NewCharm();
            charms = synthetic.For(charm);
            card = ui.Add<SpiritRewardCard>(ui.Root, "Reward Card");
            modifierRoot = ui.Rect(card.transform, "Modifier").gameObject;
            handRoot = ui.Rect(card.transform, "Hand").gameObject;
            modifierBackground = ui.Add<Image>(modifierRoot.transform, "Modifier Background");
            handBackground = ui.Add<Image>(handRoot.transform, "Hand Background");
            title = ui.Add<Text>(modifierRoot.transform, "Title");
            handTitle = ui.Add<Text>(handRoot.transform, "Hand Title");
            handLevel = ui.Add<Text>(handRoot.transform, "Hand Level");
            marker = ui.Add<Image>(card.transform, "Selection Marker");
            SyntheticUi.Set(card, "modifierRoot", modifierRoot);
            SyntheticUi.Set(card, "handRoot", handRoot);
            SyntheticUi.Set(card, "modifierBackground", modifierBackground);
            SyntheticUi.Set(card, "handBackground", handBackground);
            SyntheticUi.Set(card, "title", title);
            SyntheticUi.Set(card, "handTitle", handTitle);
            SyntheticUi.Set(card, "handLevelText", handLevel);
            SyntheticUi.Set(card, "selectionMarker", marker);
            SyntheticUi.Set(card, "condition", ui.Add<Text>(modifierRoot.transform, "Condition"));
            SyntheticUi.Set(card, "effects", ui.Add<Text>(modifierRoot.transform, "Effects"));
            SyntheticUi.Set(card, "charmIcon", ui.Add<Image>(modifierRoot.transform, "Charm Icon"));
            SyntheticUi.Set(card, "charmCategory", ui.CategoryBadge(modifierRoot.transform));
            SyntheticUi.Set(card, "handValues", ui.Add<Text>(handRoot.transform, "Hand Values"));
            handPattern = ui.Add<Image>(handRoot.transform, "Hand Pattern");
            SyntheticUi.Set(card, "handPattern", handPattern);
            SetHandPatterns(HandCategory.HighDie, HandCategory.Pair);
            SyntheticUi.Set(card, "modifierColor", ModifierColor);
            SyntheticUi.Set(card, "modifierSelectedColor", ModifierSelectedColor);
            SyntheticUi.Set(card, "handColor", HandColor);
            SyntheticUi.Set(card, "handSelectedColor", HandSelectedColor);
            // Titles take the theme's cream, amber when selected; the hand level takes its muted colour.
            var theme = ui.Root.GetComponent<SpiritUiContext>().Theme;
            theme.cream = TitleColor;
            theme.amber = SelectedTitleColor;
            theme.muted = HandLevelColor;
        }

        [TearDown]
        public void TearDown()
        {
            ui.Dispose();
            synthetic.Dispose();
        }

        /** Checks selection applies the selected colors and marker of the mode the card shows and deselection restores them / fails if a mode shows the other's root, the other mode's selection styles the card, or deselection leaves the selected style. */
        [Test]
        public void SelectionStylesOnlyTheModeTheCardShows()
        {
            card.RenderModifier(charms, charm, 1, 2);
            Assert.That(modifierRoot.activeSelf, Is.True);
            Assert.That(handRoot.activeSelf, Is.False);
            AssertModifierStyle(false);
            card.SetHandUpgradeSelected(true);
            AssertModifierStyle(false);
            card.SetModifierSelected(true);
            AssertModifierStyle(true);
            card.SetModifierSelected(false);
            AssertModifierStyle(false);

            card.RenderHandUpgrade(TestContent.Hands()[0], 0, 3, 4);
            Assert.That(modifierRoot.activeSelf, Is.False);
            Assert.That(handRoot.activeSelf, Is.True);
            AssertHandStyle(false);
            card.SetModifierSelected(true);
            AssertHandStyle(false);
            card.SetHandUpgradeSelected(true);
            AssertHandStyle(true);
            card.SetHandUpgradeSelected(false);
            AssertHandStyle(false);
        }

        /** Checks rendering and selecting either mode keeps the hierarchy's font sizes, image types and objects / fails if binding restyles or rebuilds the authored hierarchy. */
        [Test]
        public void BindingKeepsTheHierarchysTypographyAndObjects()
        {
            title.fontSize = 31;
            handTitle.fontSize = 27;
            modifierBackground.type = Image.Type.Sliced;
            handBackground.type = Image.Type.Tiled;
            int objects = card.GetComponentsInChildren<Transform>(true).Length;

            card.RenderModifier(charms, charm, 1, 2);
            card.SetModifierSelected(true);
            card.RenderHandUpgrade(TestContent.Hands()[0], 0, 3, 4);
            card.SetHandUpgradeSelected(true);

            Assert.That(title.fontSize, Is.EqualTo(31));
            Assert.That(handTitle.fontSize, Is.EqualTo(27));
            Assert.That(modifierBackground.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(handBackground.type, Is.EqualTo(Image.Type.Tiled));
            Assert.That(card.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        }

        /** Checks a hand upgrade shows the pattern sprite authored for its hand's category and rendering a category without one fails / fails if another category's sprite shows or a missing pattern passes silently. */
        [Test]
        public void HandUpgradeShowsItsCategorysAuthoredPattern()
        {
            var patterns = SetHandPatterns(HandCategory.Pair, HandCategory.HighDie);

            card.RenderHandUpgrade(Hand(HandCategory.HighDie), 0, 3, 4);
            Assert.That(handPattern.sprite, Is.SameAs(patterns[1]));
            card.RenderHandUpgrade(Hand(HandCategory.Pair), 0, 3, 4);
            Assert.That(handPattern.sprite, Is.SameAs(patterns[0]));
            Assert.Throws<System.InvalidOperationException>(() => card.RenderHandUpgrade(Hand(HandCategory.Straight), 0, 3, 4));
        }

        private static HandDefinition Hand(HandCategory category) => System.Array.Find(TestContent.Hands(), hand => hand.Category == category);

        private Sprite[] SetHandPatterns(params HandCategory[] categories)
        {
            var sprites = new Sprite[categories.Length];
            using var serialized = new SerializedObject(card);
            var entries = serialized.FindProperty("handPatternSprites");
            entries.arraySize = categories.Length;
            for (int index = 0; index < categories.Length; ++index)
            {
                var texture = ui.Own(new Texture2D(2, 2));
                sprites[index] = ui.Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f));
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("Category").enumValueIndex = (int)categories[index];
                entry.FindPropertyRelative("Sprite").objectReferenceValue = sprites[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return sprites;
        }

        private void AssertModifierStyle(bool selected)
        {
            Assert.That(modifierBackground.color, Is.EqualTo(selected ? ModifierSelectedColor : ModifierColor), "background");
            Assert.That(title.color, Is.EqualTo(selected ? SelectedTitleColor : TitleColor), "title");
            Assert.That(marker.gameObject.activeSelf, Is.EqualTo(selected), "marker");
        }

        private void AssertHandStyle(bool selected)
        {
            Assert.That(handBackground.color, Is.EqualTo(selected ? HandSelectedColor : HandColor), "background");
            Assert.That(handTitle.color, Is.EqualTo(selected ? SelectedTitleColor : TitleColor), "title");
            Assert.That(handLevel.color, Is.EqualTo(selected ? SelectedTitleColor : HandLevelColor), "level");
            Assert.That(marker.gameObject.activeSelf, Is.EqualTo(selected), "marker");
        }

        private static ModifierDefinition NewCharm() => new ModifierDefinition("test.reward", "test.reward", ModifierPhase.PlayHand,
            new AlwaysCondition(), new ModifierEffect[] { new AddBaseScoreEffect(5) });
    }
}
