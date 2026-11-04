using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** A clip with its relative weight in a random choice, so a rare sound can share a list with common ones. */
    [Serializable]
    public sealed class WeightedAudioClip
    {
        public AudioClip clip;
        [Tooltip("Relative chance of this clip against the others in its list; 1 is an ordinary entry.")]
        [Min(0)] public float weight = 1;
    }
}
