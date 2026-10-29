using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** The host arriving: the beat can hide its narration while the host moves and continue by itself afterwards. */
    [Serializable]
    public sealed class ArrivalBeat : EncounterJourneyBeat
    {
        /** Fraction of a hidden-narrative beat after which the narrative fades in over the remainder. */
        private const float NarrativeRevealStart = .9f;

        [SerializeField] private bool hideNarrativeDuringMotion;
        [Tooltip("Continues to the next beat when the motion ends; the journey's last beat cannot.")]
        [SerializeField] private bool autoAdvance;

        public bool HideNarrativeDuringMotion => hideNarrativeDuringMotion;
        public bool AutoAdvance => autoAdvance;

        internal override bool AdvancesItself => autoAdvance;

        internal override float NarrativeOpacity(float progress)
            => hideNarrativeDuringMotion ? autoAdvance ? 0 : Mathf.InverseLerp(NarrativeRevealStart, 1, progress) : 1;

        internal override void Validate(EncounterJourneyAsset journey, int index, MechanicalCatalog catalog)
        {
            base.Validate(journey, index, catalog);
            if (autoAdvance && index == journey.Beats.Count - 1)
                throw Invalid(journey, index, "is the journey's last beat and cannot continue by itself");
        }
    }
}
