using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Displays the run's hand bonuses, including each category's upgrades. */
    public sealed class SpiritHandRanksPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            [field: SerializeField] public HandCategory Category { get; internal set; }
            [field: SerializeField] public Image Background { get; internal set; }
            [field: SerializeField] public Text CategoryLabel { get; internal set; }
            [field: SerializeField] public Text LevelLabel { get; internal set; }
            [field: SerializeField] public Text BaseLabel { get; internal set; }
            [field: SerializeField] public Text MultiplierLabel { get; internal set; }
            [field: SerializeField] public Text CurrentLabel { get; internal set; }
        }

        [SerializeField] private Row[] rows = new Row[HandDefinition.CategoryCount];
        [SerializeField] private Color currentBackground = new Color32(73, 62, 37, 255);
        [SerializeField] private Color normalBackground = new Color32(29, 43, 34, 255);

        public RectTransform Root => (RectTransform)transform;
        [field: SerializeField] public Button CloseButton { get; private set; }
        public IReadOnlyList<Row> Rows => rows;
        public HandCategory? CurrentCategory { get; private set; }

        public void Build(RectTransform parent, Action close)
        {
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(() => close());
        }

        public void Render(MechanicalCatalog catalog, EncounterSnapshot snapshot, HandCategory? currentCategory)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (Root == null) throw new InvalidOperationException("Build the hand ranks panel before rendering it.");

            CurrentCategory = currentCategory;
            foreach (var row in rows)
            {
                var hand = catalog.GetHand(row.Category);
                int level = snapshot.HandUpgrades[(int)row.Category];
                long baseScore = checked(hand.BaseScore + hand.BaseScorePerUpgrade * level);
                long multiplier = checked(hand.Multiplier + hand.MultiplierPerUpgrade * level);
                bool current = row.Category == currentCategory;
                row.LevelLabel.text = "Level " + level;
                row.BaseLabel.text = "+Base " + DiagnosticText.Number(baseScore);
                row.MultiplierLabel.text = "+Mult " + DiagnosticText.Number(multiplier);
                row.CurrentLabel.text = current ? "CURRENT" : "";
                row.CategoryLabel.color = current ? SpiritUi.GetTheme(transform).amber : SpiritUi.GetTheme(transform).cream;
                row.Background.color = current ? currentBackground : normalBackground;
            }
        }


    }
}
