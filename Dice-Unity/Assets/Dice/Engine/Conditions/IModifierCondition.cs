namespace Dice.Engine
{
    /** Conditions are immutable and pure, used during previews, and may be shared across sessions.
     * Per-instance activation state belongs to the session counters. */
    public interface IModifierCondition
    {
        bool Evaluate(in ModifierContext context);
    }
}
