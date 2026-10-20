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
        [SerializeField] private SpiritModifierFamilyRow rowPrefab;
        private readonly List<SpiritModifierFamilyRow> rows = new List<SpiritModifierFamilyRow>();

        public RectTransform Root => (RectTransform)transform;
        [field: SerializeField] public ScrollRect Scroll { get; private set; }
        [field: SerializeField] public RectTransform Content { get; private set; }
        [field: SerializeField] public Button BeginButton { get; private set; }
        [field: SerializeField] public Button BackButton { get; private set; }
        public IReadOnlyList<SpiritModifierFamilyRow> Rows => rows;
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        public void Build(RectTransform parent, Action begin)
        {
            BeginButton.onClick.RemoveAllListeners();
            BeginButton.onClick.AddListener(() => begin());
            BackButton.onClick.RemoveAllListeners();
            BackButton.onClick.AddListener(Hide);
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
                rows.Add(SpiritModifierFamilyRow.Create(rowPrefab, "EncounterCharmFamily" + rows.Count, Content));
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
