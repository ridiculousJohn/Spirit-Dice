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
        }

        [SerializeField] private Row[] rows = new Row[HandDefinition.CategoryCount];
        [SerializeField] private Color normalBackground = new Color32(29, 43, 34, 255);

        public RectTransform Root => (RectTransform)transform;
        [field: SerializeField] public Button CloseButton { get; private set; }
        public IReadOnlyList<Row> Rows => rows;

        public void Build(RectTransform parent, Action close)
        {
            SpiritUi.RequireWording(transform);
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(() => close());
        }

        public void Render(MechanicalCatalog catalog, EncounterSnapshot snapshot)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (Root == null) throw new InvalidOperationException("Build the hand ranks panel before rendering it.");

            var wording = SpiritUi.GetWording(transform);
            foreach (var row in rows)
            {
                var hand = catalog.GetHand(row.Category);
                int level = snapshot.HandUpgrades[(int)row.Category];
                row.CategoryLabel.text = wording.handNames.Name(row.Category);
                row.LevelLabel.text = wording.handRanks.Level(level);
                row.BaseLabel.text = wording.handRanks.BaseBonus(hand.BaseScoreAt(level));
                row.MultiplierLabel.text = wording.handRanks.MultiplierBonus(hand.MultiplierAt(level));
                row.CategoryLabel.color = SpiritUi.GetTheme(transform).cream;
                row.Background.color = normalBackground;
            }
        }


    }
}
