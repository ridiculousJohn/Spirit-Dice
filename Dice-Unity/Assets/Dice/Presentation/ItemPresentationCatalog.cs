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
     * presentation lacks its name or icon, its cards do not present exactly its item's rules with text using only their
     * rules' placeholders, or it lacks the piece presenter its item's piece needs or has one its item does not.
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

        /** The rule cards in play at the table with the items in play: the table's own cards, then each item's cards in item order. */
        public RuleCardsInPlay CardsInPlay(EncounterTablePresentationAsset table, IReadOnlyList<ItemDefinition> items)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (items == null) throw new ArgumentNullException(nameof(items));
            var cards = new List<EncounterRulePresentationAsset>(table.RulePresentations);
            foreach (ItemDefinition item in items) cards.AddRange(Find(item, " at the table " + table.name).RuleCards);
            return new RuleCardsInPlay(table, cards);
        }

        /** The presentation of an item in play; fails, naming the item, when it has none. */
        public ItemPresentationAsset PresentationOf(ItemDefinition item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            return Find(item, "");
        }

        /** where completes the failure's "in play", such as " at the table X". */
        private ItemPresentationAsset Find(ItemDefinition item, string where)
            => presentations.TryGetValue(item.Identity, out ItemPresentationAsset presentation) ? presentation
                : throw new InvalidOperationException("The item " + item.Identity + " in play" + where + " has no presentation.");
    }
}
