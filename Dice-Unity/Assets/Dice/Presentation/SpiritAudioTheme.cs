using UnityEngine;

namespace Dice.Presentation
{
    /** Replaceable encounter clips and levels; audio remains independent of encounter rules. */
    [CreateAssetMenu(menuName = "Dice/Spirit Audio Theme", fileName = "SpiritAudioTheme")]
    public sealed class SpiritAudioTheme : ScriptableObject
    {
        [Tooltip("Recordings by dice count: element 0 is one die, element 1 is two dice, and so on.")]
        public AudioClip[] rollClips;
        [Tooltip("Contact tracks in the same dice-count order as rollClips.")]
        public SpiritRollContactTrack[] rollContactTracks;
        [Min(0)] public float rollLaunchTime = .15f;
        public AudioClip spirit;
        [Tooltip("Gesture variations chosen randomly without immediate repeats. Spirit is the fallback when none are assigned.")]
        public AudioClip[] spiritClips;
        public AudioClip score;
        public AudioClip stageClear;
        public AudioClip ambience;
        public AudioClip creak;
        public TextAsset credits;

        [Range(0, 1)] public float ambienceVolume = .24f;
        [Range(0, 1)] public float effectsVolume = .65f;
        [Range(0, 1)] public float creakVolume = .16f;
        [Min(1)] public float minimumCreakInterval = 17;
        [Min(1)] public float maximumCreakInterval = 38;
    }
}
