using System;

namespace Dice.Engine
{
    public sealed class BoundFaceCountAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        public BoundFaceCountAtLeastCondition(int parameter = 3)
        {
            parameter = parameter == 0 ? 3 : parameter;
            if (parameter < 1 || parameter > DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        /** Counts the dice showing each bound die's face, the bound die included. */
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((context.BoundMask & (1 << i)) != 0 &&
                    ModifierConditionHelpers.CountOf(context.CurrentDice, context.CurrentDice[i]) >= Parameter) return true;
            return false;
        }
    }
}
