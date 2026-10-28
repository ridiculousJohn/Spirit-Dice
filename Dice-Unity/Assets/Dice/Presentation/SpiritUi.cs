using UnityEngine;
using UnityEngine.UI;
namespace Dice.Presentation
{
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
