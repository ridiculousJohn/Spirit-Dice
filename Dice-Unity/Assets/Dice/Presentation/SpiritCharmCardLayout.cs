using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Exposes the authored card layout's preferred height to inventory layout. */
    [ExecuteAlways]
    public sealed class SpiritCharmCardLayout : MonoBehaviour
    {
        internal float Arrange(float width)
        {
            var rect = (RectTransform)transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            return LayoutUtility.GetPreferredHeight(rect);
        }
    }
}
