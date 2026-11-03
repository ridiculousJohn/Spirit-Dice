using System;

namespace Dice.Engine
{
    public sealed class SelectedMatchingGroupAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        public SelectedMatchingGroupAtLeastCondition(int parameter)
        {
            if (parameter < 1 || parameter > DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            for (int face = DiceValues.MinimumFace; face <= DiceValues.MaximumFace; ++face)
                if (context.CurrentHand.CountOf(face, context.SelectedMask) >= Parameter) return true;
            return false;
        }
    }
}
