using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * The draggable icon of an item slot (Content/Presentation/UI/ItemSlotIcon.prefab), in the HUD's item icon layer over the
     * tray: it rests on its slot at the neutral position and over its own emblem on the bay its item's piece is on. At rest
     * it is invisible but still takes a drag, the handle over what shows the item there (the slot's own image, or the bay
     * indicator's emblem); it is visible only while held and while it settles. Dropping it away from every bay, or back on
     * the slot, is the neutral position. Its state changes are announced for what shows the item elsewhere, never polled.
     */
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SpiritItemSlotIcon : SpiritPositionDrag
    {
        [Tooltip("The image that shows the item's icon.")]
        [SerializeField] private Image image;

        private CanvasGroup visibility;

        /** Raised when it is bound or unbound, taken up or released, or ends a settle. */
        public event Action Changed;

        /** Whether the icon draws; false at rest, where it is only the drag handle. */
        public bool IsVisible => Visibility.alpha > 0f;

        /** Shows the item's icon and starts taking drags. */
        public void Show(Sprite icon)
        {
            if (image == null) throw new InvalidOperationException("The item slot icon " + name + " requires its image.");
            image.sprite = icon;
            gameObject.SetActive(true);
            StateChanged();
        }

        public void Hide()
        {
            Unbind();
            gameObject.SetActive(false);
        }

        /** Dropping the item away from every bay puts it back on its slot, as dropping it on the slot does. */
        protected override bool ReleaseAwayIsNeutral => true;

        protected override void StateChanged()
        {
            Visibility.alpha = IsDragging || IsSettling ? 1f : 0f;
            Changed?.Invoke();
        }

        private CanvasGroup Visibility => visibility != null ? visibility : visibility = GetComponent<CanvasGroup>();
    }
}
