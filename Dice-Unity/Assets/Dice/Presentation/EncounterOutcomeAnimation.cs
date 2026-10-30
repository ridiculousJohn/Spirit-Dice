using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** The clip a scene figure plays when the encounter ends with the outcome. */
    [Serializable]
    public sealed class EncounterOutcomeClip
    {
        [Tooltip("Outcome that plays the clip: Won, Lost or Abandoned.")]
        public EncounterOutcome Outcome;
        [Tooltip("Legacy clip played once on the figure's Animation, over the clip it plays by default.")]
        public AnimationClip Clip;
    }

    /**
     * Plays a scene figure's authored clip for the encounter's outcome, once, on the figure's Animation component. The clip
     * plays on a layer above the Animation's default clip, so the figure's idle keeps driving every property the outcome
     * clip does not animate. An outcome without a clip plays nothing. Resetting returns the figure to its idle: an outcome
     * clip still playing is stopped at its first frame, the pose the figure had before the outcome, and the default clip
     * plays on. The encounter screen lists the figures of its table scene and tells them the outcome as it presents it.
     */
    [DisallowMultipleComponent]
    public sealed class EncounterOutcomeAnimation : MonoBehaviour
    {
        // Layer 0 holds the Animation's own clips, among them the default clip it plays as the figure's idle.
        private const int OutcomeLayer = 1;

        [Tooltip("The figure's Animation, which plays its idle by default and the outcome clips over it.")]
        [SerializeField] private Animation figure;
        [Tooltip("The clip played for each outcome; an outcome without an entry plays nothing.")]
        [SerializeField] private List<EncounterOutcomeClip> outcomeClips = new List<EncounterOutcomeClip>();

        /** True while an outcome clip plays. */
        public bool IsPlayingOutcome
        {
            get
            {
                foreach (var entry in outcomeClips)
                    if (entry != null && figure.IsPlaying(StateName(entry.Outcome))) return true;
                return false;
            }
        }

        /** Fails, naming the figure, without its Animation or with an entry that lacks its clip or repeats an outcome. */
        public void Validate()
        {
            if (figure == null)
                throw new InvalidOperationException("The outcome animation on " + name + " requires the figure's Animation.");
            var outcomes = new HashSet<EncounterOutcome>();
            foreach (var entry in outcomeClips)
            {
                if (entry == null || entry.Clip == null)
                    throw new InvalidOperationException("An outcome entry of the outcome animation on " + name + " has no clip.");
                if (entry.Outcome == EncounterOutcome.None)
                    throw new InvalidOperationException("An outcome entry of the outcome animation on " + name + " names no outcome.");
                if (!outcomes.Add(entry.Outcome))
                    throw new InvalidOperationException("The outcome animation on " + name + " authors " + entry.Outcome + " twice.");
                // The legacy Animation component plays only clips marked Legacy.
                if (!entry.Clip.legacy)
                    throw new InvalidOperationException("The outcome animation on " + name + " has a " + entry.Outcome + " clip, " +
                        entry.Clip.name + ", that is not marked Legacy.");
            }
        }

        /** Plays the outcome's clip once over the figure's idle; an outcome without a clip plays nothing. */
        public void Play(EncounterOutcome outcome)
        {
            AnimationClip clip = ClipFor(outcome);
            if (clip == null) return;
            ResetToIdle();
            string state = StateName(outcome);
            if (figure[state] == null) figure.AddClip(clip, state);
            var playing = figure[state];
            playing.layer = OutcomeLayer;
            playing.wrapMode = WrapMode.Once;
            playing.time = 0;
            figure.Play(state, PlayMode.StopSameLayer);
        }

        /** Stops an outcome clip still playing at its first frame, leaving the figure in the idle its Animation plays by default. */
        public void ResetToIdle()
        {
            if (figure == null) return;
            foreach (var entry in outcomeClips)
            {
                if (entry == null) continue;
                string state = StateName(entry.Outcome);
                if (!figure.IsPlaying(state)) continue;
                figure[state].time = 0;
                figure.Sample();
                figure.Stop(state);
            }
        }

        private AnimationClip ClipFor(EncounterOutcome outcome)
        {
            foreach (var entry in outcomeClips)
                if (entry != null && entry.Outcome == outcome) return entry.Clip;
            return null;
        }

        // Outcome clips play as states of their own, apart from any state of the same clip in the Animation's list.
        private static string StateName(EncounterOutcome outcome) => "Outcome " + outcome;
    }
}
