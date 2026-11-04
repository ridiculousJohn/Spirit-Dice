using System;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * Where a journey's beats take place. A beat that sets a scene changes to it, and later beats stay in it until another
     * beat sets one. Each kind of scene presents itself on the journey screen.
     */
    public abstract class JourneySceneAsset : ScriptableObject
    {
        [Tooltip("Where the top of the journey screen's narration panel sits while this scene shows, as a fraction of the " +
            "view's height from the bottom; the panel grows down from it to fit the beat's text. Set it to keep the panel " +
            "off what the scene must show, such as the host.")]
        [Range(0, 1)]
        [SerializeField] private float narrativeTop = .65f;

        /** The top of the journey screen's narration panel while this scene shows, as a fraction of the view's height from the bottom. */
        public float NarrativeTop => narrativeTop;

        /** The table scene this scene is, or null for a scene shown over the table scene the journey reaches next. */
        public abstract EncounterTablePresentationAsset Table { get; }

        /** Shows the scene on the journey screen as a beat in it starts. */
        internal abstract void Present(IJourneyStage stage);

        /** Fails, naming the scene and the beat that sets it, when the scene's data is invalid. */
        internal abstract void Validate(string beat);

        /** Fails, naming the scene as the kind given (such as "picture scene") and the beat, when its narrative top is invalid. */
        protected void ValidateNarrativeTop(string kind, string beat)
        {
            if (!FiniteValue.Is(narrativeTop) || narrativeTop < 0 || narrativeTop > 1)
                throw new FormatException("The narrative top of the " + kind + " " + name + " of " + beat +
                    " must be a finite number from 0 to 1.");
        }
    }
}
