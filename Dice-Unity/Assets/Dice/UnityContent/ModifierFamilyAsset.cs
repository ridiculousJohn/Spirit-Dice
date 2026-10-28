using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    /** A charm family; its charms reference it, and its identity is its asset GUID. */
    [CreateAssetMenu(menuName = "Dice/Modifier Family")]
    public sealed class ModifierFamilyAsset : ContentAsset
    {
        public ModifierFamilyDefinition.Id Identity => new ModifierFamilyDefinition.Id(AssetGuid);

        public ModifierFamilyDefinition Build() => new ModifierFamilyDefinition(Identity);
    }
}
