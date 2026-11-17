using System;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The mark an item puts on a dice bay, authored as a prefab: a glow on the bay floor tinted with the item's glow colour
     * and the item's emblem at the bay's label anchor, which each tray geometry authors at the bay's front lip. The tray
     * hangs it from a bay's root, scaled so its offsets are in die sizes, and lays the emblems of the indicators sharing a
     * bay side by side about that anchor. The emblem's authored local position is its offset from the bay's label anchor,
     * in die sizes. Both are sprites with unlit materials, so
     * the mark reads the same under every table's lighting; the icon and colour are set on the sprite renderers, leaving
     * their materials shared.
     */
    public sealed class SpiritBayIndicator : MonoBehaviour
    {
        [Tooltip("Sprite on the bay floor, tinted with the item's glow colour; on the tray's render layer.")]
        [SerializeField] private SpriteRenderer glow;
        [Tooltip("Sprite that shows the item's icon at the bay's label anchor, on its front lip; a direct child of the indicator on the tray's render layer. Its local position is its offset from the label anchor, in die sizes.")]
        [SerializeField] private SpriteRenderer emblem;
        [Tooltip("Length of the longer side of the emblem's icon, in die sizes, whatever the icon sprite's own size.")]
        [SerializeField, Min(.01f)] private float emblemSize = .4f;
        [Tooltip("Distance between the centres of neighbouring emblems when items share a bay, in die sizes.")]
        [SerializeField, Min(0)] private float emblemSpacing = .4f;
        // The emblem's authored local position: its offset from the bay's label anchor, in die sizes.
        private Vector3 emblemOffset;

        /**
         * Fails, naming the prefab, unless it has both renderers on the tray's render layer and its emblem is its own child;
         * an item presentation that uses the prefab checks it when it loads.
         */
        internal void Validate()
        {
            if (glow == null || emblem == null)
                throw new InvalidOperationException("The bay indicator " + name + " requires its glow and emblem renderers.");
            if (emblem.transform.parent != transform)
                throw new InvalidOperationException("The emblem of the bay indicator " + name +
                    " must be a direct child of the indicator, so its spacing is in die sizes.");
            if (glow.gameObject.layer != SpiritDiceTray.RenderLayer || emblem.gameObject.layer != SpiritDiceTray.RenderLayer)
                throw new InvalidOperationException("The glow and emblem of the bay indicator " + name +
                    " must be on the tray's render layer, " + SpiritDiceTray.RenderLayer + ".");
        }

        /** Shows or hides the emblem; the glow stays. */
        internal void ShowEmblem(bool shown) => emblem.enabled = shown;

        /** Where the emblem is, in world space. */
        internal Vector3 EmblemPosition => emblem.transform.position;

        /** Shows the item's icon at the emblem size and the glow colour. */
        internal void Initialize(Sprite icon, Color glowColor)
        {
            emblemOffset = emblem.transform.localPosition;
            emblem.sprite = icon;
            Vector3 size = icon.bounds.size;
            emblem.transform.localScale = Vector3.one * (emblemSize / Mathf.Max(size.x, size.y));
            glow.color = glowColor;
        }

        /**
         * Places the emblem at its place among the count emblems on its bay, the row centred on the bay's label anchor (a
         * world point) plus the emblem's offset.
         */
        internal void PlaceEmblem(Vector3 labelAnchor, int place, int count)
            => emblem.transform.localPosition = transform.InverseTransformPoint(labelAnchor) + emblemOffset +
                Vector3.right * ((place - (count - 1) * .5f) * emblemSpacing);
    }
}
