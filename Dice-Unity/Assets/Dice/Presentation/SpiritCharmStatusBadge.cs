using UnityEngine;

namespace Dice.Presentation
{
    /** A compact footer keeps state recognizable while leaving the card's rule wording unobstructed. */
    public sealed class SpiritCharmStatusBadge : MonoBehaviour
    {
        public SpiritCharmStatusPresentation Presentation { get; private set; }
        public SpiritCharmStatusGraphic Icon { get; private set; }

        internal static SpiritCharmStatusBadge Create(Transform parent)
        {
            var rect = SpiritUi.Rect("StatusBadge", parent, new Vector2(1, 0), new Vector2(1, 0));
            rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(24, 24);
            rect.anchoredPosition = new Vector2(-16, 12);
            var badge = rect.gameObject.AddComponent<SpiritCharmStatusBadge>();
            var iconRect = SpiritUi.Rect("StatusIcon", rect, Vector2.zero, Vector2.one);
            badge.Icon = iconRect.gameObject.AddComponent<SpiritCharmStatusGraphic>();
            badge.Icon.raycastTarget = false;
            return badge;
        }

        public void Render(SpiritCharmStatusPresentation presentation)
        {
            Presentation = presentation;
            Icon.State = presentation.State;
            Color tint = presentation.State == SpiritCharmStatusState.Ready ? new Color32(167, 204, 158, 255) :
                presentation.State == SpiritCharmStatusState.WaitingCondition || presentation.State == SpiritCharmStatusState.OnRoll ?
                    SpiritUi.GetTheme(transform).amber : SpiritUi.GetTheme(transform).muted;
            Icon.color = tint;
        }
    }
}
