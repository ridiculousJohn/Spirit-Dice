using UnityEngine;

namespace Dice.Presentation
{
    /**
     * Where a journey's beats take place. A beat that sets a scene changes to it, and later beats stay in it until another
     * beat sets one. Each kind of scene presents itself on the journey screen.
     */
    public abstract class JourneySceneAsset : ScriptableObject
    {
        /** The table scene this scene is, or null for a scene shown over the table scene the journey reaches next. */
        public abstract EncounterTablePresentationAsset Table { get; }

        /** Shows the scene on the journey screen as a beat in it starts. */
        internal abstract void Present(IJourneyStage stage);

        /** Fails, naming the scene and the beat that sets it, when the scene's data is invalid. */
        internal abstract void Validate(string beat);
    }
}
