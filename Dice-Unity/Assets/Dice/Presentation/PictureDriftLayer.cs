using System;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * A layer drifting over a picture scene's plate, such as fog or smoke: a texture that tiles both ways moves across the
     * layer's rectangle at a steady velocity, showing only where its window lets it, at an opacity that may swell slowly.
     * Rectangles, tile sizes and velocities are in picture widths and heights, so the layer keeps its place on the plate as
     * the view moves over the picture.
     */
    [Serializable]
    public sealed class PictureDriftLayer
    {
        [Tooltip("The drifting texture; it repeats both ways, so import it with Wrap Mode Repeat.")]
        [SerializeField] private Texture2D texture;
        [Tooltip("Alpha limits where the layer shows over its rectangle (0 nowhere, 1 fully); empty shows it over the whole rectangle.")]
        [SerializeField] private Texture2D window;
        [Tooltip("The layer's rectangle in normalized coordinates of the picture (origin bottom left); the window covers it.")]
        [SerializeField] private Rect rect = new Rect(0, 0, 1, 1);
        [Tooltip("One tile of the texture, in picture widths and heights.")]
        [SerializeField] private Vector2 tileSize = Vector2.one;
        [Tooltip("The texture's drift, in picture widths and heights per second (x right, y up).")]
        [SerializeField] private Vector2 velocity;
        [Tooltip("Opacity at rest, 0 to 1.")]
        [SerializeField] private float opacity = 1;
        [Tooltip("The opacity's swell either way, as a fraction of Opacity (0 steady, at most 1).")]
        [SerializeField] private float swell;
        [Tooltip("Seconds of one swell cycle; used when Swell is above 0.")]
        [SerializeField] private float swellPeriod = 30;

        public Texture2D Texture => texture;
        public Texture2D Window => window;
        public Rect Rect => rect;
        public Vector2 TileSize => tileSize;
        public Vector2 Velocity => velocity;
        public float Opacity => opacity;
        public float Swell => swell;
        public float SwellPeriod => swellPeriod;

        /** Fails, naming the layer as its owner describes it, when a value cannot be drawn. */
        internal void Validate(string layer)
        {
            if (texture == null) throw Invalid(layer, "has no texture");
            if (!FiniteValue.Is(rect.position) || !FiniteValue.Is(rect.size) || rect.width <= 0 || rect.height <= 0)
                throw Invalid(layer, "requires a finite rectangle of positive size");
            if (!FiniteValue.Is(tileSize) || tileSize.x <= 0 || tileSize.y <= 0)
                throw Invalid(layer, "requires a finite tile size above zero");
            if (!FiniteValue.Is(velocity)) throw Invalid(layer, "requires a finite velocity");
            if (!FiniteValue.Is(opacity) || opacity < 0 || opacity > 1) throw Invalid(layer, "requires an opacity from 0 to 1");
            if (!FiniteValue.Is(swell) || swell < 0 || swell > 1) throw Invalid(layer, "requires a swell from 0 to 1");
            if (!FiniteValue.Is(swellPeriod) || swell > 0 && swellPeriod <= 0)
                throw Invalid(layer, "requires a finite swell period, above zero when it swells");
        }

        private static FormatException Invalid(string layer, string problem) => new FormatException(layer + " " + problem + ".");
    }
}
