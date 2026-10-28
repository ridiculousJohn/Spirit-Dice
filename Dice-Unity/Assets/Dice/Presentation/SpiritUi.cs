using UnityEngine;
using UnityEngine.UI;
namespace Dice.Presentation
{
    /**
     * The theme and wording of a UI hierarchy are authored once, on the SpiritUiContext at its root; a component finds
     * them through its parents, so a screen or prefab follows the skin of the hierarchy it is placed in without a
     * reference of its own.
     */
    internal static class SpiritUi
    {
        internal static SpiritUiTheme GetTheme(Transform parent)
        {
            var context = parent.GetComponentInParent<SpiritUiContext>(true);
            if (context == null || context.Theme == null)
                throw new System.InvalidOperationException("The UI hierarchy requires an assigned theme.");
            return context.Theme;
        }

        internal static SpiritUiWording GetWording(Transform parent)
        {
            var context = parent.GetComponentInParent<SpiritUiContext>(true);
            if (context == null || context.Wording == null)
                throw new System.InvalidOperationException("The UI hierarchy requires assigned wording.");
            return context.Wording;
        }

        /** The hierarchy's wording, validated as a screen is configured; missing wording fails naming its field. */
        internal static SpiritUiWording RequireWording(Transform parent)
        {
            var wording = GetWording(parent);
            wording.Validate();
            return wording;
        }

        internal static void SetButtonLabel(Button button, string value)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = value;
        }
    }
}
