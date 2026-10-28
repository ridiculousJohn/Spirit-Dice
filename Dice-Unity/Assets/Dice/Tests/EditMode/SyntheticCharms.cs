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
        public static MechanicalContent Content(params ModifierAsset[] charms)
        {
            var definitions = charms.Select(charm => charm.Build()).ToArray();
            return new MechanicalContent(Catalog(definitions), charms.Select((charm, index) =>
                new KeyValuePair<ModifierAsset, ModifierDefinition>(charm, definitions[index])));
        }

        /** A presentation of the given modifiers in a one-encounter catalog, as For(catalog) presents them. */
        public ModifierPresentationCatalog For(params ModifierDefinition[] definitions) => For(Catalog(definitions));

        /** A presentation of the given modifiers in a one-encounter catalog with the given category. */
        public ModifierPresentationCatalog For(ModifierCategoryPresentationAsset category, params ModifierDefinition[] definitions)
            => For(Catalog(definitions), category);

        /** A one-encounter catalog of the given modifiers. */
        public static MechanicalCatalog Catalog(params ModifierDefinition[] definitions)
            => new MechanicalCatalog("test.charms", TestContent.Hands(),
                new[] { new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) }) }, definitions);
    }
}
