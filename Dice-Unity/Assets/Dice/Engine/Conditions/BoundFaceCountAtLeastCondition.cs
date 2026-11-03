using System;

namespace Dice.Engine
{
    public sealed class BoundFaceCountAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        public BoundFaceCountAtLeastCondition(int parameter)
        {
            if (parameter < 1 || parameter > DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        /**
         * Counts the dice showing each bound die's face, the bound die included, over the dice every binding rule has bound.
         * A bound die that does not count has no face, so the condition is false; the counts see only the counted dice.
         */
        public bool Evaluate(in ModifierContext context)
        {
            int boundMask = 0;
            foreach (var rule in context.EncounterRules)
                if (rule is DieBindingRuleSnapshot binding) boundMask |= binding.BoundMask;
            if (!context.CurrentHand.Counts(boundMask)) return false;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((boundMask & (1 << i)) != 0 &&
                    context.CurrentHand.CountOf(context.CurrentHand.Faces[i]) >= Parameter) return true;
            return false;
        }
    }
}
