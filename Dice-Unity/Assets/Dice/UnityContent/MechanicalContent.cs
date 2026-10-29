using System;
using System.Collections.Generic;
using Dice.Engine;

namespace Dice.UnityContent
{
    /** An engine catalog paired with the charm asset each of its modifier definitions was built from. */
    public sealed class MechanicalContent
    {
        private readonly Dictionary<ModifierAsset, ModifierDefinition> definitions = new Dictionary<ModifierAsset, ModifierDefinition>();

        public MechanicalCatalog Catalog { get; }
        public IReadOnlyCollection<ModifierAsset> Modifiers => definitions.Keys;

        /** Every modifier definition of the catalog is paired with exactly one charm asset, and every pair names one of them. */
        public MechanicalContent(MechanicalCatalog catalog, IEnumerable<KeyValuePair<ModifierAsset, ModifierDefinition>> modifiers)
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
        }

        /** The definition built from a charm the content uses. */
        public ModifierDefinition GetDefinition(ModifierAsset modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            if (!definitions.TryGetValue(modifier, out ModifierDefinition definition))
                throw new InvalidOperationException("The content does not use the charm " + modifier.name + ".");
            return definition;
        }
    }
}
