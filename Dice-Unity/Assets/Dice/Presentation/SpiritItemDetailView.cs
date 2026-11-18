using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * One item as a reward shows it (Content/Presentation/UI/ItemDetail.prefab): its icon and name from its presentation,
     * one invitation rule row per rule card (name, description, icon) and the charms the item adds as the encounter's charms
     * introduction shows charms, one family row per family. Everything it shows comes from the item presentation, its rule
     * cards and the charm skin.
     */
    public sealed class SpiritItemDetailView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text itemName;
        [Tooltip("The rule row each of the item's rule cards shows in, as the invitation shows rule cards.")]
        [SerializeField] private SpiritInvitationRuleView ruleRowPrefab;
        [SerializeField] private RectTransform ruleRowsRoot;
        [Tooltip("The family row the item's charms show in, as the encounter's charms introduction shows charms.")]
        [SerializeField] private SpiritModifierFamilyRow familyRowPrefab;
        [SerializeField] private RectTransform familyRowsRoot;
        private readonly List<SpiritInvitationRuleView> ruleRows = new List<SpiritInvitationRuleView>();
        private readonly List<SpiritModifierFamilyRow> familyRows = new List<SpiritModifierFamilyRow>();

        public Text ItemName => itemName;

        /** Shows the item: its presentation's icon and name, its rule cards and the charms its rules add. */
        public void Render(ItemDefinition.Id item, ItemDetailSources sources)
        {
            if (icon == null || itemName == null || ruleRowPrefab == null || ruleRowsRoot == null || familyRowPrefab == null ||
                familyRowsRoot == null)
                throw new InvalidOperationException("The item detail " + name + " requires its authored icon, name and row lists.");
            ItemDefinition definition = sources.Catalog.GetItem(item);
            ItemPresentationAsset presentation = sources.Items.PresentationOf(definition);
            icon.sprite = presentation.Icon;
            itemName.text = presentation.DisplayName;
            var cards = presentation.RuleCards;
            while (ruleRows.Count < cards.Count) ruleRows.Add(Instantiate(ruleRowPrefab, ruleRowsRoot, false));
            for (int index = 0; index < ruleRows.Count; ++index)
            {
                bool shown = index < cards.Count;
                ruleRows[index].gameObject.SetActive(shown);
                // An item's card shows its own icon; an item has no host whose sigil stands in for a missing one.
                if (shown) ruleRows[index].Render(cards[index].DisplayName, cards[index].Description, cards[index].Icon, null);
            }
            ruleRowsRoot.gameObject.SetActive(cards.Count > 0);
            SpiritModifierFamilyRow.ShowFamilies(familyRowPrefab, familyRows, familyRowsRoot, "ItemCharmFamily", sources.Catalog,
                sources.Charms, definition.AddedModifierIds);
            familyRowsRoot.gameObject.SetActive(definition.AddedModifierIds.Count > 0);
        }
    }
}
