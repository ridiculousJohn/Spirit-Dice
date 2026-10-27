using System.Collections.Generic;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** The reward card's hand-upgrade preview and the choice its button reports. */
    public sealed class RewardCardChoiceTests
    {
        private readonly List<(long Offer, long Option)> choices = new List<(long Offer, long Option)>();
        private GameObject parent;
        private SpiritRewardCard card;

        [SetUp]
        public void SetUp()
        {
            choices.Clear();
            parent = new GameObject("Reward card test", typeof(RectTransform));
            var prefab = AssetDatabase.LoadAssetAtPath<SpiritRewardCard>("Assets/Dice/Content/Presentation/UI/SpiritRewardCard.prefab");
            Assert.That(prefab, Is.Not.Null);
            card = SpiritRewardCard.Create(prefab, "TestRewardCard", parent.transform, (offer, option) => choices.Add((offer, option)));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(parent);

        /**
         * Checks that a hand-upgrade card shows the hand's current level and values, adds the next level's increments
         * only while selected, and returns to the current values when deselected. Fails if the upgrade is previewed
         * before selection or remains after deselection.
         */
        [Test]
        public void HandUpgradePreviewsTheNextLevelOnlyWhileSelected()
        {
            // A test-only hand at level 3 whose base and multiplier rise by different amounts per level.
            var hand = new HandDefinition("hand.reward-test", HandCategory.Pair, 17, 3, 7, 2, 6);
            const int level = 3;
            long baseScore = hand.BaseScore + level * hand.BaseScorePerUpgrade;
            long multiplier = hand.Multiplier + level * hand.MultiplierPerUpgrade;
            string current = "BASE " + DiagnosticText.Number(baseScore) + "\nMULT " + DiagnosticText.Number(multiplier);
            string upgraded = "BASE " + DiagnosticText.Number(baseScore) + " +" + DiagnosticText.Number(hand.BaseScorePerUpgrade) +
                "\nMULT " + DiagnosticText.Number(multiplier) + " +" + DiagnosticText.Number(hand.MultiplierPerUpgrade);
            var levelLabel = Field<Text>("handLevelText");
            var values = Field<Text>("handValues");

            card.RenderHandUpgrade(hand, level, 1, 1);
            Assert.That(levelLabel.text, Is.EqualTo("Level " + level));
            Assert.That(values.text, Is.EqualTo(current));
            card.SetHandUpgradeSelected(true);
            Assert.That(levelLabel.text, Is.EqualTo("Level " + level + " +1"));
            Assert.That(values.text, Is.EqualTo(upgraded));
            card.SetHandUpgradeSelected(false);
            Assert.That(levelLabel.text, Is.EqualTo("Level " + level));
            Assert.That(values.text, Is.EqualTo(current));
        }

        /**
         * Checks that the card's button reports the offer and option of its latest rendering, once per click. Fails if a
         * click reports an earlier rendering's choice or re-rendering adds another listener.
         */
        [Test]
        public void ButtonReportsTheLatestRenderedChoiceOncePerClick()
        {
            var hand = TestContent.Hands()[0];
            card.RenderHandUpgrade(hand, 0, 11, 21);
            card.Button.onClick.Invoke();
            card.RenderHandUpgrade(hand, 0, 12, 22);
            card.Button.onClick.Invoke();
            Assert.That(choices, Is.EqualTo(new[] { (11L, 21L), (12L, 22L) }));
        }

        private T Field<T>(string name) where T : Object
        {
            var field = typeof(SpiritRewardCard).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            var value = field.GetValue(card) as T;
            Assert.That(value, Is.Not.Null, name);
            return value;
        }
    }
}
