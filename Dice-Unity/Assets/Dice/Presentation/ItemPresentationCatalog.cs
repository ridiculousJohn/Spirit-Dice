using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.UnityContent;

namespace Dice.Presentation
{
    /**
     * The presentation of every item of the loaded content (the catalog's items and the items its encounters lend), built
     * at load from an item skin. Each presentation is matched to its item's definition through its own item reference.
     * Construction fails, naming the skin, presentation or item, when a presentation is missing or references no item or
     * an item that is not of the content, an item of the content has more than one presentation or none, or a
     * presentation's cards do not present exactly its item's rules with text using only their rules' placeholders.
     */
    public sealed class ItemPresentationCatalog
    {
        private readonly Dictionary<ItemDefinition.Id, ItemPresentationAsset> presentations =
            new Dictionary<ItemDefinition.Id, ItemPresentationAsset>();

        public ItemPresentationCatalog(ItemSkinAsset skin, MechanicalContent content)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (skin.Items == null) throw new InvalidOperationException("The item skin " + skin.name + " has no item presentation list.");
            foreach (ItemPresentationAsset presentation in skin.Items)
            {
                if (presentation == null) throw new InvalidOperationException("The item skin " + skin.name + " has a missing item presentation.");
                ItemAsset item = presentation.Item;
                if (item == null) throw new InvalidOperationException("The item presentation " + presentation.name + " has no item reference.");
                ItemDefinition definition = content.GetDefinition(item);
                if (presentations.ContainsKey(definition.Identity))
                    throw new InvalidOperationException("The item skin " + skin.name + " has more than one presentation for the item " +
                        item.name + ".");
                presentation.Validate(definition);
                presentations.Add(definition.Identity, presentation);
            }
            foreach (ItemAsset item in content.Items)
                if (!presentations.ContainsKey(content.GetDefinition(item).Identity))
                    throw new InvalidOperationException("The item skin " + skin.name + " has no presentation for the item " + item.name + ".");
        }

        /** The rule cards in play at the table for the encounter: the table's own cards, then each item's cards in item order. */
        public RuleCardsInPlay CardsInPlay(EncounterTablePresentationAsset table, EncounterDefinition encounter)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            var cards = new List<EncounterRulePresentationAsset>(table.RulePresentations);
            foreach (ItemDefinition item in encounter.Items)
            {
                if (!presentations.TryGetValue(item.Identity, out ItemPresentationAsset presentation))
                    throw new InvalidOperationException("The item " + item.Identity + " of the encounter " + encounter.Identity +
                        " has no presentation.");
                cards.AddRange(presentation.RuleCards);
            }
            return new RuleCardsInPlay(table, cards);
        }
    }
}
