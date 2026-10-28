using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** A compact footer keeps state recognizable while leaving the card's rule wording unobstructed. */
    public sealed class SpiritCharmStatusBadge : MonoBehaviour
    {
        [SerializeField] private Color readyColor = new Color32(167, 204, 158, 255);
        public SpiritCharmStatusPresentation Presentation { get; private set; }
        [field: SerializeField] public Image Icon { get; private set; }
        public void Render(SpiritCharmStatusPresentation presentation)
        {
            Presentation = presentation;
            var theme = SpiritUi.GetTheme(transform);
            Icon.sprite = theme.GetCharmStatusSymbol(presentation.State);
            Color tint = presentation.State == SpiritCharmStatusState.Ready ? readyColor :
                presentation.State == SpiritCharmStatusState.WaitingCondition || presentation.State == SpiritCharmStatusState.OnRoll ?
                    theme.amber : theme.muted;
            Icon.color = tint;
        }
    }
}
