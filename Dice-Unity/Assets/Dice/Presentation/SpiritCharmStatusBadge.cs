using UnityEngine;

namespace Dice.Presentation
{
    /** A compact footer keeps state recognizable while leaving the card's rule wording unobstructed. */
    public sealed class SpiritCharmStatusBadge : MonoBehaviour
    {
        [SerializeField] private Color readyColor = new Color32(167, 204, 158, 255);
        public SpiritCharmStatusPresentation Presentation { get; private set; }
        [field: SerializeField] public SpiritCharmStatusGraphic Icon { get; private set; }
        public void Render(SpiritCharmStatusPresentation presentation)
        {
            Presentation = presentation;
            Icon.State = presentation.State;
            Color tint = presentation.State == SpiritCharmStatusState.Ready ? readyColor :
                presentation.State == SpiritCharmStatusState.WaitingCondition || presentation.State == SpiritCharmStatusState.OnRoll ?
                    SpiritUi.GetTheme(transform).amber : SpiritUi.GetTheme(transform).muted;
            Icon.color = tint;
        }
    }
}
