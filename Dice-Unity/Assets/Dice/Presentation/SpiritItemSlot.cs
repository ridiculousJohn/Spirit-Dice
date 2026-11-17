using System;
using Dice.UnityContent;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * Where an item's icon rests (Content/Presentation/UI/ItemSlot.prefab): the player's slot on the HUD, which shows the
     * item the player carries, or a slot a table scene places for an item its encounter lends. The slot always shows its
     * item's icon in its frame: at full strength while the item rests on the slot, dimmed while it is on a bay or in the
     * player's hand. What the player drags is a separate SpiritItemSlotIcon in the HUD's item icon layer over the tray, so
     * it draws above the tray while it is held and stays below the dice's touch targets; its neutral anchor is the slot's
     * icon anchor. A slot is hidden unless an item in play is assigned to it.
     */
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class SpiritItemSlot : MonoBehaviour
    {
        [Tooltip("The lent item a table scene's slot holds; empty on the player's slot, which holds the item the player carries.")]
        [SerializeField] private ItemAsset item;
        [Tooltip("The draggable icon of this slot, in the HUD's item icon layer; its neutral anchor is this slot's icon anchor.")]
        [SerializeField] private SpiritItemSlotIcon icon;
        [Tooltip("The image in the slot's frame that always shows the slot's item.")]
        [SerializeField] private Image itemImage;
        [Tooltip("The slot's frame and its item's image, faded to Dimmed Alpha while the item is on a bay or in the player's hand.")]
        [SerializeField] private CanvasGroup frame;
        [Tooltip("Opacity of the slot's frame and its item's image while the item is on a bay or in the player's hand.")]
        [SerializeField, Range(0, 1)] private float dimmedAlpha = .45f;

        /** The lent item a scene's slot holds, or null for the player's slot. */
        public ItemAsset Item => item;
        public SpiritItemSlotIcon Icon => icon;
        public bool IsDimmed { get; private set; }

        /** Shows the slot with the item's icon in its frame and on its draggable icon, not dimmed. */
        public void Show(Sprite itemIcon)
        {
            if (itemIcon == null) throw new ArgumentNullException(nameof(itemIcon));
            RequireAuthored();
            gameObject.SetActive(true);
            itemImage.sprite = itemIcon;
            icon.Show(itemIcon);
            SetDimmed(false);
        }

        /** Dims the slot while its item is on a bay or in the player's hand. */
        public void SetDimmed(bool dimmed)
        {
            RequireAuthored();
            IsDimmed = dimmed;
            frame.alpha = dimmed ? dimmedAlpha : 1f;
        }

        /** Hides the slot and its icon; no item is assigned to it. */
        public void Hide()
        {
            RequireAuthored();
            SetDimmed(false);
            icon.Hide();
            gameObject.SetActive(false);
        }

        private void RequireAuthored()
        {
            if (icon == null || itemImage == null || frame == null)
                throw new InvalidOperationException("The item slot " + name + " requires its icon, item image and frame.");
        }
    }
}
