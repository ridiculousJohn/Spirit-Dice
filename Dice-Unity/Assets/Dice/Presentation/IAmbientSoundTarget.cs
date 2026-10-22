using UnityEngine;

namespace Dice.Presentation
{
    /** Plays an ambient activity's prop and burst sounds; the target owns the voices, mute and suspension. */
    public interface IAmbientSoundTarget
    {
        void PlayAmbientSound(AudioClip clip, float volume);
    }
}
