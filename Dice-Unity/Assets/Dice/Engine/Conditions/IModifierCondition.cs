using System.Collections.Generic;

namespace Dice.Engine
{
    /** Conditions are immutable and pure, used during previews, and may be shared across sessions.
     * Per-instance activation state belongs to the session counters. */
    public interface IModifierCondition
    {
        bool Evaluate(in ModifierContext context);

        /** False for a condition that never activates in any context. */
        bool CanEverActivate => true;

        /**
         * False for a condition that can never be true while only rulesInPlay are in play: a condition reading a kind of
         * rule's state needs such a rule. Catalog construction checks that every charm can activate where it is offered.
         */
        bool CanEverActivateWith(IReadOnlyList<EncounterRuleDefinition> rulesInPlay) => true;
    }
}
