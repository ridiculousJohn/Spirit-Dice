using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Mechanical Catalog")]
    public sealed class MechanicalCatalogAsset : ScriptableObject
    {
        [SerializeField] private string catalogVersion;
        [SerializeField] private HandData[] hands = Array.Empty<HandData>();
        [SerializeField] private EncounterAsset[] encounters = Array.Empty<EncounterAsset>();

        /**
         * Builds the engine catalog of the listed encounters from the charms the encounters and their reward pools
         * reference. An encounter played before it joins the list, as in the diagnostic scene, is added to the catalog
         * built for it.
         */
        public MechanicalContent Build(EncounterAsset played = null)
        {
            if (hands == null || encounters == null)
                throw new InvalidOperationException("Catalog arrays are missing.");
            var referenced = new List<ModifierAsset>();
            var seen = new HashSet<ModifierAsset>();
            var byId = new Dictionary<ModifierDefinition.Id, ModifierAsset>();
            void Use(ModifierAsset modifier)
            {
                if (modifier == null) throw new InvalidOperationException("A charm reference is missing.");
                if (!seen.Add(modifier)) return;
                if (byId.TryGetValue(modifier.Identity, out ModifierAsset other))
                    throw new InvalidOperationException("Two charm assets share the id " + modifier.Identity + ": " + other.name + ", " + modifier.name);
                byId.Add(modifier.Identity, modifier);
                referenced.Add(modifier);
            }
            var handDefinitions = new HandDefinition[hands.Length];
            for (int i = 0; i < hands.Length; ++i)
            {
                var source = hands[i] ?? throw new InvalidOperationException("A hand entry is missing.");
                handDefinitions[i] = new HandDefinition(source.category, source.baseScore, source.multiplier,
                    source.baseScorePerUpgrade, source.multiplierPerUpgrade, source.maximumUpgrades);
            }
            // Each pool asset is built once, so the layouts drawing from a pool share its definition.
            var pools = new Dictionary<RewardPoolAsset, ModifierRewardPoolDefinition>();
            ModifierRewardPoolDefinition Pool(RewardPoolAsset pool)
            {
                if (!pools.TryGetValue(pool, out ModifierRewardPoolDefinition built))
                {
                    built = pool.Build(Use);
                    pools.Add(pool, built);
                }
                return built;
            }
            var encounterAssets = new List<EncounterAsset>(encounters);
            if (played != null && !encounterAssets.Contains(played)) encounterAssets.Add(played);
            var encounterDefinitions = new EncounterDefinition[encounterAssets.Count];
            var encountersById = new Dictionary<EncounterDefinition.Id, EncounterAsset>();
            for (int i = 0; i < encounterDefinitions.Length; ++i)
            {
                var source = encounterAssets[i] ?? throw new InvalidOperationException("An encounter reference is missing.");
                if (encountersById.TryGetValue(source.Identity, out EncounterAsset other))
                    throw new InvalidOperationException("Two encounter assets share the id " + source.Identity + ": " + other.name + ", " + source.name);
                encountersById.Add(source.Identity, source);
                encounterDefinitions[i] = source.Build(Use, Pool);
            }
            // Ordered by GUID, so the catalog's charm order does not depend on the order content references them.
            referenced.Sort((left, right) => left.Identity.Value.CompareTo(right.Identity.Value));
            // Each family asset is built once, so the charms of a family share its definition.
            var families = new Dictionary<ModifierFamilyAsset, ModifierFamilyDefinition>();
            var familiesById = new Dictionary<ModifierFamilyDefinition.Id, ModifierFamilyAsset>();
            ModifierFamilyDefinition Family(ModifierAsset modifier)
            {
                ModifierFamilyAsset family = modifier.Family;
                if (family == null) throw new InvalidOperationException("The charm asset " + modifier.name + " has no family.");
                if (families.TryGetValue(family, out ModifierFamilyDefinition built)) return built;
                built = family.Build();
                if (familiesById.TryGetValue(built.Identity, out ModifierFamilyAsset other))
                    throw new InvalidOperationException("Two charm family assets share the id " + built.Identity + ": " + other.name + ", " + family.name);
                familiesById.Add(built.Identity, family);
                families.Add(family, built);
                return built;
            }
            var modifierDefinitions = new ModifierDefinition[referenced.Count];
            var pairs = new KeyValuePair<ModifierAsset, ModifierDefinition>[referenced.Count];
            for (int i = 0; i < referenced.Count; ++i)
            {
                modifierDefinitions[i] = referenced[i].Build(Family(referenced[i]));
                pairs[i] = new KeyValuePair<ModifierAsset, ModifierDefinition>(referenced[i], modifierDefinitions[i]);
            }
            var catalog = new MechanicalCatalog(catalogVersion, handDefinitions, encounterDefinitions, modifierDefinitions);
            return new MechanicalContent(catalog, pairs);
        }

        /** The ids of the charms, each passed to use first. */
        internal static ModifierDefinition.Id[] Ids(ModifierAsset[] sources, Action<ModifierAsset> use)
        {
            if (sources == null) throw new InvalidOperationException("A charm list is missing.");
            var ids = new ModifierDefinition.Id[sources.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                use(sources[i]);
                ids[i] = sources[i].Identity;
            }
            return ids;
        }

        [Serializable]
        private sealed class HandData
        {
            public HandCategory category;
            public long baseScore;
            public long multiplier;
            public long baseScorePerUpgrade;
            public long multiplierPerUpgrade;
            public int maximumUpgrades;
        }
    }
}
