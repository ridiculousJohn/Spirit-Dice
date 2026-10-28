using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** A hand ranks panel over a synthetic hierarchy of one row per category, rendering a fabricated run's upgrade levels. */
    public sealed class HandRanksPanelTests
    {
        private SyntheticUi ui;
        private SpiritHandRanksPanel panel;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            panel = ui.Add<SpiritHandRanksPanel>(ui.Root, "Hand Ranks");
            using var serialized = new SerializedObject(panel);
            var rows = serialized.FindProperty("rows");
            rows.arraySize = HandDefinition.CategoryCount;
            for (int index = 0; index < HandDefinition.CategoryCount; ++index)
            {
                var row = rows.GetArrayElementAtIndex(index);
                var parent = ui.Rect(panel.transform, "Row " + index);
                row.FindPropertyRelative("<Category>k__BackingField").enumValueIndex = index;
                row.FindPropertyRelative("<Background>k__BackingField").objectReferenceValue = ui.Add<Image>(parent, "Background");
                foreach (string label in new[] { "CategoryLabel", "LevelLabel", "BaseLabel", "MultiplierLabel" })
                    row.FindPropertyRelative("<" + label + ">k__BackingField").objectReferenceValue = ui.Add<Text>(parent, label);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => ui.Dispose();

        /** Checks each row shows its category's name, level and the base score and multiplier the hand states at that level in the wording's templates / fails if a row shows another level's values, another category's name or recomputes them from other figures. */
        [Test]
        public void EachRowShowsTheValuesItsHandStatesAtTheRunsLevel()
        {
            var upgrades = new[] { 0, 2, 0, 1, 0, 0, 6, 0 };
            var catalog = TestContent.Catalog(upgrades: upgrades);
            panel.Render(catalog, EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 3).Snapshot);
            Assert.That(panel.Rows.Count, Is.EqualTo(HandDefinition.CategoryCount));
            foreach (var row in panel.Rows)
            {
                var hand = catalog.GetHand(row.Category);
                int level = upgrades[(int)row.Category];
                Assert.That(row.CategoryLabel.text, Is.EqualTo(ui.Wording.handNames.Name(row.Category)), row.Category.ToString());
                Assert.That(row.LevelLabel.text, Is.EqualTo("<handRanks.level>" + level), row.Category.ToString());
                Assert.That(row.BaseLabel.text, Is.EqualTo("<handRanks.baseBonus>" + DiagnosticText.Number(hand.BaseScoreAt(level))),
                    row.Category.ToString());
                Assert.That(row.MultiplierLabel.text,
                    Is.EqualTo("<handRanks.multiplierBonus>" + DiagnosticText.Number(hand.MultiplierAt(level))), row.Category.ToString());
            }
        }
    }
}
