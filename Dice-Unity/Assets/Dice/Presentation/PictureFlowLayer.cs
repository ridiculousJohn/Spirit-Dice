using System;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * A layer that moves a painted part of a picture scene's plate, such as smoke, by its own pixels: inside the layer's
     * rectangle the plate is redrawn pulled along a flow field, in two phases half a period apart that cross-fade so the loop
     * never jumps, and blended over the plate by the window's alpha. With no displacement it draws the plate unchanged, so
     * nothing is added to the painting. Rectangles are in picture widths and heights, so the layer keeps its place on the
     * plate as the view moves over the picture.
     */
    [Serializable]
    public sealed class PictureFlowLayer
    {
        [Tooltip("The layer's rectangle in normalized coordinates of the picture (origin bottom left), inside the picture; " +
            "the window and the flow cover it.")]
        [SerializeField] private Rect rect = new Rect(0, 0, 1, 1);
        [Tooltip("Alpha is where the picture's pixels move and how strongly (0 still, 1 fully): 0 at the rectangle's edge and " +
            "where the motion starts. Import it with sRGB off, clamped.")]
        [SerializeField] private Texture2D window;
        [Tooltip("RG is the motion's direction times its relative speed (at most 1) per pixel, encoded as direction * 0.5 + " +
            "0.5 (x right, y up); B is a low-frequency noise, 0 to 1, that offsets the phase. Import it with sRGB off, " +
            "clamped, without mipmaps or compression.")]
        [SerializeField] private Texture2D flow;
        [Tooltip("Pixels of the picture's plate per second at relative speed 1.")]
        [SerializeField] private float speed = 12;
        [Tooltip("Seconds of one phase: the plate is pulled at most Speed * Phase Period pixels before the phase fades out and " +
            "starts again.")]
        [SerializeField] private float phasePeriod = 3;
        [Tooltip("How far the flow's noise offsets each pixel's phase, in periods (0 none, at most 1), so the phases do not " +
            "fade in unison.")]
        [SerializeField] private float noiseStrength;

        public Rect Rect => rect;
        public Texture2D Window => window;
        public Texture2D Flow => flow;
        public float Speed => speed;
        public float PhasePeriod => phasePeriod;
        public float NoiseStrength => noiseStrength;

        /** Fails, naming the layer as its owner describes it, when a value cannot be drawn. */
        internal void Validate(string layer)
        {
            if (window == null) throw Invalid(layer, "has no window");
            if (flow == null) throw Invalid(layer, "has no flow");
            if (!FiniteValue.Is(rect.position) || !FiniteValue.Is(rect.size) || rect.width <= 0 || rect.height <= 0)
                throw Invalid(layer, "requires a finite rectangle of positive size");
            if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > 1 || rect.yMax > 1)
                throw Invalid(layer, "requires a rectangle inside the picture");
            if (!FiniteValue.Is(speed) || speed <= 0) throw Invalid(layer, "requires a finite speed above zero");
            if (!FiniteValue.Is(phasePeriod) || phasePeriod <= 0)
                throw Invalid(layer, "requires a finite phase period above zero");
            if (!FiniteValue.Is(noiseStrength) || noiseStrength < 0 || noiseStrength > 1)
                throw Invalid(layer, "requires a noise strength from 0 to 1");
        }

        private static FormatException Invalid(string layer, string problem) => new FormatException(layer + " " + problem + ".");
    }
}
