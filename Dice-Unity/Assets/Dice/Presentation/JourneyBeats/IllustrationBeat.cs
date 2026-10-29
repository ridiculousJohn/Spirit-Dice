using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** A beat over a picture: the view moves across the picture's background and foreground over the beat's duration. */
    [Serializable]
    public sealed class IllustrationBeat : EncounterJourneyBeat
    {
        [Tooltip("Foreground offset at the start and end of the beat, in picture widths and heights.")]
        [SerializeField] private Vector2 motionFrom;
        [SerializeField] private Vector2 motionTo;
        [SerializeField] private float scaleFrom = 1;
        [SerializeField] private float scaleTo = 1;
        [Tooltip("Background offset at the start and end of the beat, in picture widths and heights.")]
        [SerializeField] private Vector2 backgroundMotionFrom;
        [SerializeField] private Vector2 backgroundMotionTo;
        [SerializeField] private float backgroundScaleFrom = 1;
        [SerializeField] private float backgroundScaleTo = 1;

        public Vector2 MotionFrom => motionFrom;
        public Vector2 MotionTo => motionTo;
        public float ScaleFrom => scaleFrom;
        public float ScaleTo => scaleTo;
        public Vector2 BackgroundMotionFrom => backgroundMotionFrom;
        public Vector2 BackgroundMotionTo => backgroundMotionTo;
        public float BackgroundScaleFrom => backgroundScaleFrom;
        public float BackgroundScaleTo => backgroundScaleTo;

        internal override void Move(IJourneyStage stage, float eased)
            => stage.MovePicture(Vector2.Lerp(backgroundMotionFrom, backgroundMotionTo, eased),
                Mathf.Lerp(backgroundScaleFrom, backgroundScaleTo, eased), Vector2.Lerp(motionFrom, motionTo, eased),
                Mathf.Lerp(scaleFrom, scaleTo, eased));

        internal override void Validate(EncounterJourneyAsset journey, int index, MechanicalCatalog catalog)
        {
            base.Validate(journey, index, catalog);
            if (!FiniteValue.Is(motionFrom) || !FiniteValue.Is(motionTo) ||
                !FiniteValue.Is(backgroundMotionFrom) || !FiniteValue.Is(backgroundMotionTo) ||
                !Positive(scaleFrom) || !Positive(scaleTo) || !Positive(backgroundScaleFrom) || !Positive(backgroundScaleTo))
                throw Invalid(journey, index, "has invalid motion");
        }

        private static bool Positive(float value) => FiniteValue.Is(value) && value > 0;
    }
}
