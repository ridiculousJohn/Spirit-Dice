using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;

namespace Dice.Tests
{
    /** Synthetic charm assets, presentations and skins, with one-encounter test catalogs of their charms. */
    internal sealed class SyntheticCharms : SyntheticCharmAssets
    {
        /** Content of the given charms: each charm's definition in a one-encounter catalog. */
        public static MechanicalContent Content(params ModifierAsset[] charms) => Content(Catalog, charms);

        /**
         * Content of the given charms in a one-encounter catalog whose one reward pool holds every reward-eligible charm, so
         * none of those is addition-only.
         */
        public static MechanicalContent PooledContent(params ModifierAsset[] charms) => Content(PooledCatalog, charms);

        private static MechanicalContent Content(Func<ModifierDefinition[], MechanicalCatalog> catalog, ModifierAsset[] charms)
        {
            // Each family asset is built once, so the charms of a family share its definition.
            var families = new Dictionary<ModifierFamilyAsset, ModifierFamilyDefinition>();
            var definitions = charms.Select(charm =>
            {
                if (!families.TryGetValue(charm.Family, out ModifierFamilyDefinition family))
                    families.Add(charm.Family, family = charm.Family.Build());
                return charm.Build(family);
            }).ToArray();
            return new MechanicalContent(catalog(definitions), charms.Select((charm, index) =>
                new KeyValuePair<ModifierAsset, ModifierDefinition>(charm, definitions[index])));
        }

        /** A presentation of the given modifiers in a one-encounter catalog, as For(catalog) presents them. */
        public ModifierPresentationCatalog For(params ModifierDefinition[] definitions) => For(Catalog(definitions));

        /** A presentation of the given modifiers in a one-encounter catalog, every effect set showing the given category. */
        public ModifierPresentationCatalog For(ModifierCategoryPresentationAsset category, params ModifierDefinition[] definitions)
            => For(Catalog(definitions), category);

        /** A one-encounter catalog of the given modifiers. */
        public static MechanicalCatalog Catalog(params ModifierDefinition[] definitions)
            => new MechanicalCatalog("test.charms", TestContent.Hands(),
                new[] { new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) }) }, definitions);

        /** A one-encounter catalog of the given modifiers whose one reward pool holds every reward-eligible one. */
        private static MechanicalCatalog PooledCatalog(params ModifierDefinition[] definitions)
            => new MechanicalCatalog("test.charms.pooled", TestContent.Hands(), new[]
            {
                new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) },
                    rewardPolicy: RewardTestContent.Policy(new[] { new ModifierRewardLayout(new[] { RewardTestContent.Pool(definitions) }) }))
            }, definitions);
    }
}
