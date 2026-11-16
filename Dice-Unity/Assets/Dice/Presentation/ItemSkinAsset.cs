using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * How the items of a skin show: every item presentation. At load it becomes an ItemPresentationCatalog validated
     * against the content.
     */
    [CreateAssetMenu(menuName = "Dice/Item Skin")]
    public sealed class ItemSkinAsset : ScriptableObject
    {
        [Tooltip("One presentation for each item of the content.")]
        [SerializeField] private ItemPresentationAsset[] items = Array.Empty<ItemPresentationAsset>();

        public IReadOnlyList<ItemPresentationAsset> Items => items;
    }
}
