using System;
using System.Collections.Generic;
using Dice.Engine;

namespace Dice.UnityContent
{
    /**
     * An engine catalog paired with the charm asset each of its modifier definitions was built from and the item asset
     * each item its encounters lend was built from.
     */
    public sealed class MechanicalContent
    {
        private readonly Dictionary<ModifierAsset, ModifierDefinition> definitions = new Dictionary<ModifierAsset, ModifierDefinition>();
        private readonly Dictionary<ItemAsset, ItemDefinition> items = new Dictionary<ItemAsset, ItemDefinition>();

        public MechanicalCatalog Catalog { get; }
        public IReadOnlyCollection<ModifierAsset> Modifiers => definitions.Keys;
        /** The item assets of the items the catalog's encounters lend. */
        public IReadOnlyCollection<ItemAsset> Items => items.Keys;

        /**
         * Every modifier definition of the catalog is paired with exactly one charm asset, and every pair names one of them;
         * likewise every item a catalog encounter lends is paired with exactly one item asset.
         */
        public MechanicalContent(MechanicalCatalog catalog, IEnumerable<KeyValuePair<ModifierAsset, ModifierDefinition>> modifiers,
            IEnumerable<KeyValuePair<ItemAsset, ItemDefinition>> lentItems = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (modifiers == null) throw new ArgumentNullException(nameof(modifiers));
            var catalogModifiers = new HashSet<ModifierDefinition>(catalog.Modifiers);
            var paired = new HashSet<ModifierDefinition>();
            foreach (KeyValuePair<ModifierAsset, ModifierDefinition> pair in modifiers)
            {
                if (pair.Key == null) throw new ArgumentException("A charm asset is missing.", nameof(modifiers));
                if (pair.Value == null || !catalogModifiers.Contains(pair.Value))
                    throw new ArgumentException("A charm asset is paired with a definition outside the catalog: " + pair.Key.name, nameof(modifiers));
                if (definitions.ContainsKey(pair.Key))
                    throw new ArgumentException("A charm asset is paired more than once: " + pair.Key.name, nameof(modifiers));
                if (!paired.Add(pair.Value))
                    throw new ArgumentException("A modifier definition is paired with more than one charm asset: " + pair.Key.name, nameof(modifiers));
                definitions.Add(pair.Key, pair.Value);
            }
            if (definitions.Count != catalogModifiers.Count)
                throw new ArgumentException("Every catalog modifier requires the charm asset it was built from.", nameof(modifiers));
            var catalogItems = new HashSet<ItemDefinition>();
            foreach (EncounterDefinition encounter in catalog.Encounters) catalogItems.UnionWith(encounter.Items);
            var pairedItems = new HashSet<ItemDefinition>();
            foreach (KeyValuePair<ItemAsset, ItemDefinition> pair in lentItems ?? Array.Empty<KeyValuePair<ItemAsset, ItemDefinition>>())
            {
                if (pair.Key == null) throw new ArgumentException("An item asset is missing.", nameof(lentItems));
                if (pair.Value == null || !catalogItems.Contains(pair.Value))
                    throw new ArgumentException("An item asset is paired with an item no catalog encounter lends: " + pair.Key.name, nameof(lentItems));
                if (items.ContainsKey(pair.Key))
                    throw new ArgumentException("An item asset is paired more than once: " + pair.Key.name, nameof(lentItems));
                if (!pairedItems.Add(pair.Value))
                    throw new ArgumentException("An item definition is paired with more than one item asset: " + pair.Key.name, nameof(lentItems));
                items.Add(pair.Key, pair.Value);
            }
            if (items.Count != catalogItems.Count)
                throw new ArgumentException("Every item a catalog encounter lends requires the item asset it was built from.", nameof(lentItems));
        }

        /** The definition built from a charm the content uses. */
        public ModifierDefinition GetDefinition(ModifierAsset modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            if (!definitions.TryGetValue(modifier, out ModifierDefinition definition))
                throw new InvalidOperationException("The content does not use the charm " + modifier.name + ".");
            return definition;
        }

        /** The definition built from an item an encounter of the content lends. */
        public ItemDefinition GetDefinition(ItemAsset item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!items.TryGetValue(item, out ItemDefinition definition))
                throw new InvalidOperationException("No encounter of the content lends the item " + item.name + ".");
            return definition;
        }
    }
}
