using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Displays the run's hand bonuses, including each category's upgrades, and marks the current rank of the dice. */
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

        public void Build(Action close)
        {
            SpiritUi.RequireWording(transform);
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(() => close());
        }

        /** Shows each category's values at the run's levels and marks the row of the current rank; none is marked without one. */
        public void Render(MechanicalCatalog catalog, EncounterSnapshot snapshot, HandCategory? currentCategory)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (Root == null) throw new InvalidOperationException("Build the hand ranks panel before rendering it.");

            var wording = SpiritUi.GetWording(transform);
            var theme = SpiritUi.GetTheme(transform);
            foreach (var row in rows)
            {
                var hand = catalog.GetHand(row.Category);
                int level = snapshot.HandUpgrades[(int)row.Category];
                bool current = row.Category == currentCategory;
                row.CategoryLabel.text = wording.handNames.Name(row.Category);
                row.LevelLabel.text = wording.handRanks.Level(level);
                row.BaseLabel.text = wording.handRanks.BaseBonus(hand.BaseScoreAt(level));
                row.MultiplierLabel.text = wording.handRanks.MultiplierBonus(hand.MultiplierAt(level));
                row.CurrentLabel.gameObject.SetActive(current);
                row.CategoryLabel.color = current ? theme.amber : theme.cream;
                row.Background.color = current ? currentBackground : normalBackground;
            }
        }


    }
}
