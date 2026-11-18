using System;
using Dice.Engine;

namespace Dice.Presentation
{
    /** What an item detail reads its item from: the loaded catalog, the charm skin's presentation and the item skin's. */
    public readonly struct ItemDetailSources
    {
        public MechanicalCatalog Catalog { get; }
        public ModifierPresentationCatalog Charms { get; }
        public ItemPresentationCatalog Items { get; }

        public ItemDetailSources(MechanicalCatalog catalog, ModifierPresentationCatalog charms, ItemPresentationCatalog items)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Charms = charms ?? throw new ArgumentNullException(nameof(charms));
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        /** The sources the encounter screen's controls hold. */
        public static ItemDetailSources Of(ISpiritEncounterControls controls)
        {
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            return new ItemDetailSources(controls.Catalog, controls.ModifierPresentation, controls.ItemPresentation);
        }
    }
}
