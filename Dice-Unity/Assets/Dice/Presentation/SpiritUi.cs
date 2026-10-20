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

        internal static void SetButtonLabel(Button button, string value)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = value;
        }

    }
}
