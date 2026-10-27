namespace Dice.Engine
{
    /** Conditions are immutable and pure, used during previews, and may be shared across sessions.
     * Per-instance activation state belongs to the session counters. */
    public interface IModifierCondition
    {
        bool Evaluate(in ModifierContext context);

        /** False for a condition that never activates in any context. */
        bool CanEverActivate => true;
    }
}
