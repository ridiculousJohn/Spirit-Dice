using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * Introduces an encounter's own charm families: after Accept and before the encounter begins, or, through an
     * instance on the encounter screen, before the first charm offer that can contain them. Draw order is authored.
     */
    public sealed class SpiritEncounterCharmsScreen : MonoBehaviour
    {
        [SerializeField] private SpiritModifierFamilyRow rowPrefab;
        private readonly List<SpiritModifierFamilyRow> rows = new List<SpiritModifierFamilyRow>();

        public RectTransform Root => (RectTransform)transform;
        [field: SerializeField] public ScrollRect Scroll { get; private set; }
        [field: SerializeField] public RectTransform Content { get; private set; }
        [field: SerializeField] public Button BeginButton { get; private set; }
        [field: SerializeField] public Button BackButton { get; private set; }
        /** Optional narration line above the family list; inactive when no line is given. */
        [field: SerializeField] public Text Narration { get; private set; }
        public IReadOnlyList<SpiritModifierFamilyRow> Rows => rows;
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        public void Build(Action begin)
        {
            SpiritUi.RequireWording(transform);
            BeginButton.onClick.RemoveAllListeners();
            BeginButton.onClick.AddListener(() => begin());
            BackButton.onClick.RemoveAllListeners();
            BackButton.onClick.AddListener(Hide);
        }

        /**
         * Groups the encounter's charms by family, in authored order, and opens the screen at the top. A narration line
         * shows in quotation marks above the families; without one the narration stays hidden.
         */
        public void Show(MechanicalCatalog catalog, ModifierPresentationCatalog charms, IReadOnlyList<string> modifierIds,
            string narration = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (charms == null) throw new ArgumentNullException(nameof(charms));
            if (modifierIds == null) throw new ArgumentNullException(nameof(modifierIds));
            var families = new Dictionary<ModifierFamilyDefinition, List<ModifierDefinition>>();
            foreach (string id in modifierIds)
            {
                var definition = catalog.GetModifier(id);
                if (!families.TryGetValue(definition.Family, out var family))
                {
                    family = new List<ModifierDefinition>();
                    families.Add(definition.Family, family);
                }
                family.Add(definition);
            }
            while (rows.Count < families.Count)
                rows.Add(SpiritModifierFamilyRow.Create(rowPrefab, "EncounterCharmFamily" + rows.Count, Content));
            int index = 0;
            foreach (var family in families.Values) rows[index++].Render(charms, family);
            for (int rowIndex = 0; rowIndex < rows.Count; ++rowIndex)
                rows[rowIndex].gameObject.SetActive(rowIndex < families.Count);
            bool narrated = !string.IsNullOrWhiteSpace(narration);
            Narration.text = narrated ? SpiritUi.GetWording(transform).encounterCharms.Narration(narration.Trim()) : "";
            Narration.gameObject.SetActive(narrated);
            Root.gameObject.SetActive(true);
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
