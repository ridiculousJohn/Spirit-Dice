namespace Dice.Presentation
{
    /** Scene lights a rule presenter raises above their resting level for a moment, such as a flare for a reaction. */
    public interface IRuleSceneLights
    {
        int LightCount { get; }

        /**
         * Raises the light to at least the peak multiplier of its authored intensity: it rises from its present raise over the
         * rise time, holds and falls back to its resting level over the fall time. A later flare of the same light replaces it.
         */
        void Flare(int index, float peak, float rise, float hold, float fall);
    }
}
