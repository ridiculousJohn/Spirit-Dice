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
        public sealed class Row
        {
            public HandCategory Category { get; internal set; }
            public Image Background { get; internal set; }
            public Text CategoryLabel { get; internal set; }
            public Text LevelLabel { get; internal set; }
            public Text BaseLabel { get; internal set; }
            public Text MultiplierLabel { get; internal set; }
            public Text CurrentLabel { get; internal set; }
        }

        private readonly Row[] rows = new Row[HandDefinition.CategoryCount];

        public RectTransform Root { get; private set; }
        public Button CloseButton { get; private set; }
        public IReadOnlyList<Row> Rows => rows;
        public HandCategory? CurrentCategory { get; private set; }

        public void Build(RectTransform parent, Action close)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (close == null) throw new ArgumentNullException(nameof(close));
            if (Root != null) throw new InvalidOperationException("The hand ranks panel has already been built.");

            Root = SpiritUi.Panel("HandRanksOverlay", parent, Vector2.zero, Vector2.one,
                SpiritUi.GetTheme(transform).panelColor, 0, true).rectTransform;
            SpiritUi.Heading("HandRanksTitle", Root, "HAND RANKS", 29,
                new Vector2(.03f, .9f), new Vector2(.73f, .98f), TextAnchor.MiddleLeft, SpiritUi.GetTheme(transform).amber);
            CloseButton = SpiritUi.Button("CloseHandRanksButton", Root, "Close",
                new Vector2(.75f, .9f), new Vector2(.98f, .98f), () => close());
            var subtitle = SpiritUi.Label("HandRanksSubtitle", Root,
                "Hand bonuses include upgrades. Dice and charm bonuses are extra.", 18,
                new Vector2(.03f, .835f), new Vector2(.97f, .9f), TextAnchor.MiddleLeft, SpiritUi.GetTheme(transform).muted);
            FitLabel(subtitle, 14);
            var rule = SpiritUi.Rect("HandRanksRule", Root, new Vector2(.02f, .813f), new Vector2(.98f, .831f));
            SpiritUi.Decorate(rule);

            for (int i = 0; i < rows.Length; ++i)
            {
                var category = (HandCategory)i;
                float top = .805f - i * .091f;
                var background = SpiritUi.Panel("HandRankRow" + category, Root,
                    new Vector2(.04f, top - .085f), new Vector2(.96f, top), SpiritUi.GetTheme(transform).panelColor);
                var row = new Row
                {
                    Category = category,
                    Background = background,
                    CategoryLabel = SpiritUi.Heading("CategoryLabel", background.transform,
                        DiagnosticText.Humanize(category.ToString()), 23,
                        new Vector2(.01f, .48f), new Vector2(.73f, 1)),
                    CurrentLabel = SpiritUi.Label("CurrentLabel", background.transform, "", 14,
                        new Vector2(.73f, .48f), new Vector2(.99f, 1), TextAnchor.MiddleRight, SpiritUi.GetTheme(transform).amber),
                    LevelLabel = SpiritUi.Label("LevelLabel", background.transform, "", 18,
                        new Vector2(.01f, 0), new Vector2(.29f, .48f), TextAnchor.MiddleLeft, SpiritUi.GetTheme(transform).muted),
                    BaseLabel = SpiritUi.Label("BaseLabel", background.transform, "", 18,
                        new Vector2(.29f, 0), new Vector2(.66f, .48f)),
                    MultiplierLabel = SpiritUi.Label("MultiplierLabel", background.transform, "", 18,
                        new Vector2(.66f, 0), new Vector2(.99f, .48f))
                };
                FitLabel(row.CurrentLabel, 11);
                FitLabel(row.LevelLabel, 12);
                FitLabel(row.BaseLabel, 12);
                FitLabel(row.MultiplierLabel, 12);
                rows[i] = row;
            }

            SpiritUi.Label("HandRanksFooter", Root, "Highest matching rank is played.", 17,
                new Vector2(.04f, .025f), new Vector2(.96f, .072f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            Root.gameObject.SetActive(false);
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
                row.Background.color = current ? new Color32(73, 62, 37, 255) : new Color32(29, 43, 34, 255);
            }
        }

        private static void FitLabel(Text text, int minimumSize)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimumSize;
            text.resizeTextMaxSize = text.fontSize;
        }
    }
}
