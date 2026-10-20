using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Requests a refresh of the authored inspection layout after data binding. */
    [ExecuteAlways]
    public sealed class SpiritCharmDetailsLayout : MonoBehaviour
    {
        internal void RefreshLayout() => LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }
}
