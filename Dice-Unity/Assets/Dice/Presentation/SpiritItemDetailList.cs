using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The items a screen shows as given to the player for the first time by the rewards tied to it, one item detail each, in
     * order under this object; it is hidden while there are none. The journey screen's narration and the encounter's ending each hold one.
     */
    public sealed class SpiritItemDetailList : MonoBehaviour
    {
        [SerializeField] private SpiritItemDetailView detailPrefab;
        private readonly List<SpiritItemDetailView> details = new List<SpiritItemDetailView>();

        public IReadOnlyList<SpiritItemDetailView> Details => details;

        /** Shows the items; with none it hides and reads no source. */
        public void Show(IReadOnlyList<ItemDefinition.Id> items, Func<ItemDetailSources> sources)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (detailPrefab == null) throw new InvalidOperationException("The item detail list " + name + " requires its detail prefab.");
            gameObject.SetActive(items.Count > 0);
            for (int index = items.Count; index < details.Count; ++index) details[index].gameObject.SetActive(false);
            if (items.Count == 0) return;
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            ItemDetailSources from = sources();
            while (details.Count < items.Count) details.Add(Instantiate(detailPrefab, transform, false));
            for (int index = 0; index < items.Count; ++index)
            {
                details[index].gameObject.SetActive(true);
                details[index].Render(items[index], from);
            }
        }

        public void Hide() => Show(Array.Empty<ItemDefinition.Id>(), null);
    }
}
