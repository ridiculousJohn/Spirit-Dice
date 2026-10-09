using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** A compact footer keeps state recognizable while leaving the card's rule wording unobstructed. */
    public sealed class SpiritCharmStatusBadge : MonoBehaviour
    {
        public SpiritCharmStatusPresentation Presentation { get; private set; }
        public SpiritCharmStatusGraphic Icon { get; private set; }
        public Text Count { get; private set; }

        internal static SpiritCharmStatusBadge Create(Transform parent, Text status)
        {
            var rect = SpiritUi.Rect("StatusBadge", parent, new Vector2(1, 0), new Vector2(1, 0));
            rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(78, 24);
            rect.anchoredPosition = new Vector2(-16, 12);
            var badge = rect.gameObject.AddComponent<SpiritCharmStatusBadge>();
            var iconRect = SpiritUi.Rect("StatusIcon", rect, Vector2.zero, new Vector2(0, 1));
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = new Vector2(24, 0);
            badge.Icon = iconRect.gameObject.AddComponent<SpiritCharmStatusGraphic>();
            badge.Icon.raycastTarget = false;
            badge.Count = status;
            status.transform.SetParent(rect, false);
            status.rectTransform.anchorMin = Vector2.zero;
            status.rectTransform.anchorMax = Vector2.one;
            status.rectTransform.offsetMin = new Vector2(30, 0);
            status.rectTransform.offsetMax = Vector2.zero;
            status.alignment = TextAnchor.MiddleRight;
            status.fontSize = 14;
            status.resizeTextForBestFit = false;
            return badge;
        }

        public void Render(SpiritCharmStatusPresentation presentation)
        {
            Presentation = presentation;
            Icon.State = presentation.State;
            Color tint = presentation.State == SpiritCharmStatusState.Ready ? new Color32(167, 204, 158, 255) :
                presentation.State == SpiritCharmStatusState.WaitingCondition || presentation.State == SpiritCharmStatusState.OnRoll ?
                    SpiritUi.Amber : SpiritUi.Muted;
            Icon.color = Count.color = tint;
            Count.text = presentation.UseCountText;
        }
    }
}
