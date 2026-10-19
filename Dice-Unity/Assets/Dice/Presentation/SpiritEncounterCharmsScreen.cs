using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Introduces an encounter's own charm families after Accept and before the encounter begins. */
    public sealed class SpiritEncounterCharmsScreen : MonoBehaviour
    {
        private readonly List<SpiritModifierFamilyRow> rows = new List<SpiritModifierFamilyRow>();

        public RectTransform Root { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public RectTransform Content { get; private set; }
        public Button BeginButton { get; private set; }
        public Button BackButton { get; private set; }
        public IReadOnlyList<SpiritModifierFamilyRow> Rows => rows;
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        public void Build(RectTransform parent, Action begin)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (begin == null) throw new ArgumentNullException(nameof(begin));
            if (Root != null) throw new InvalidOperationException("The encounter charms screen has already been built.");

            var theme = SpiritUi.GetTheme(transform);
            Root = SpiritUi.Panel("EncounterCharmsIntroduction", parent, Vector2.zero, Vector2.one,
                new Color(theme.panelColor.r, theme.panelColor.g, theme.panelColor.b, 1)).rectTransform;
            SpiritUi.Panel("EncounterCharmsFrame", Root, Vector2.zero, Vector2.one,
                theme.panelColor, 0, true).raycastTarget = false;
            SpiritUi.Heading("EncounterCharmsTitle", Root, "ENCOUNTER CHARMS", 29,
                new Vector2(.035f, .89f), new Vector2(.965f, .985f), TextAnchor.MiddleCenter, theme.amber);
            SpiritUi.Label("EncounterCharmsScope", Root,
                "These charms can appear in charm choices during this encounter. They are not added to your permanent collection.", 19,
                new Vector2(.045f, .735f), new Vector2(.955f, .89f), TextAnchor.MiddleLeft, theme.muted);
            Scroll = SpiritUi.Scroll("EncounterCharmFamilies", Root,
                new Vector2(.015f, .19f), new Vector2(.985f, .735f), out var content);
            Content = content;
            var layout = Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BeginButton = SpiritUi.Button("BeginEncounterButton", Root, "Begin encounter",
                new Vector2(.035f, .09f), new Vector2(.965f, .17f), () => begin(), true);
            BackButton = SpiritUi.Button("BackToInvitationButton", Root, "Back to invitation",
                new Vector2(.035f, .012f), new Vector2(.965f, .085f), Hide);
            Root.gameObject.SetActive(false);
        }

        /** Groups the encounter's charms by family, in authored order, and opens the screen at the top. */
        public void Show(MechanicalCatalog catalog, IReadOnlyList<string> modifierIds)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (modifierIds == null) throw new ArgumentNullException(nameof(modifierIds));
            var families = new Dictionary<string, List<ModifierDefinition>>(StringComparer.Ordinal);
            foreach (string id in modifierIds)
            {
                var definition = catalog.GetModifier(id);
                if (!families.TryGetValue(definition.FamilyId, out var family))
                {
                    family = new List<ModifierDefinition>();
                    families.Add(definition.FamilyId, family);
                }
                family.Add(definition);
            }
            while (rows.Count < families.Count)
                rows.Add(SpiritModifierFamilyRow.Create("EncounterCharmFamily" + rows.Count, Content));
            int index = 0;
            foreach (var family in families.Values) rows[index++].Render(family);
            for (int rowIndex = 0; rowIndex < rows.Count; ++rowIndex)
                rows[rowIndex].gameObject.SetActive(rowIndex < families.Count);
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            Scroll.StopMovement();
            Scroll.verticalNormalizedPosition = 1;
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }
    }
}
