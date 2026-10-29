using System;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    public enum EncounterJourneyTransition { Fade, PartingPanels }

    /**
     * One step of a journey. Each kind of beat is its own class and shows what it adds to the journey screen; every beat has
     * its narration, its pacing, the host's pose and, optionally, the scene it changes to.
     */
    [Serializable]
    public abstract class EncounterJourneyBeat
    {
        [SerializeField] private string title;
        [SerializeField, TextArea] private string text;
        [SerializeField] private string continueLabel;
        [SerializeField] private float duration = 1;
        [Tooltip("The host's pose over the beat; during a picture it prepares the host of the table scene behind it.")]
        [SerializeField] private SpiritPresentationState hostState;
        [SerializeField] private AudioClip audioCue;
        [Tooltip("Granted when the player continues from the beat.")]
        [SerializeField] private ProgressionRewardAsset progressionReward;
        [Tooltip("The scene the beat changes to; empty stays in the current scene. The first beat sets one.")]
        [SerializeField] private JourneySceneAsset scene;
        [Tooltip("How the beat changes the scene when it sets one.")]
        [SerializeField] private EncounterJourneyTransition transition;

        public string Title => title;
        public string Text => text;
        public string ContinueLabel => continueLabel;
        public float Duration => duration;
        public SpiritPresentationState HostState => hostState;
        public AudioClip AudioCue => audioCue;
        public ProgressionRewardAsset ProgressionReward => progressionReward;
        /** The scene the beat changes to, or null when it stays in the current scene. */
        public JourneySceneAsset Scene => scene;
        public EncounterJourneyTransition Transition => transition;

        /** Shows what this kind of beat adds to the journey screen as the beat starts. */
        internal virtual void Show(IJourneyStage stage, EncounterJourneyAsset journey, int index) { }

        /** Moves the picture to the eased progress of the beat's motion. */
        internal virtual void Move(IJourneyStage stage, float eased) { }

        /** The narration's opacity at the progress of the beat's motion. */
        internal virtual float NarrativeOpacity(float progress) => 1;

        /** Whether the beat continues to the next beat by itself when its motion ends. */
        internal virtual bool AdvancesItself => false;

        /** Fails, naming the journey and the beat, when the beat's data is invalid. */
        internal virtual void Validate(EncounterJourneyAsset journey, int index, MechanicalCatalog catalog)
        {
            if (!Enum.IsDefined(typeof(SpiritPresentationState), hostState) || hostState == SpiritPresentationState.Reaction)
                throw Invalid(journey, index, "has an invalid host state");
            if (!Enum.IsDefined(typeof(EncounterJourneyTransition), transition))
                throw Invalid(journey, index, "has an invalid transition");
            if (!FiniteValue.Is(duration) || duration < 0) throw Invalid(journey, index, "has an invalid duration");
            if (scene != null) scene.Validate(Describe(journey, index));
            progressionReward?.Build(catalog);
        }

        /** The beat as validation messages name it, such as "beat 2 (DialogueBeat) of the journey LastLanternJourney". */
        protected string Describe(EncounterJourneyAsset journey, int index)
            => "beat " + index + " (" + GetType().Name + ") of the journey " + journey.name;

        protected FormatException Invalid(EncounterJourneyAsset journey, int index, string problem)
        {
            string beat = Describe(journey, index);
            return new FormatException(char.ToUpperInvariant(beat[0]) + beat.Substring(1) + " " + problem + ".");
        }
    }
}
