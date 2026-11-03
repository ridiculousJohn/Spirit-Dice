using System;

namespace Dice.Engine
{
    public sealed class NoExtremeFacesCondition : IModifierCondition
    {
        public int Parameter { get; }
        public int SecondaryParameter { get; }

        public NoExtremeFacesCondition(int parameter, int secondaryParameter)
        {
            if (!DiceValues.IsFace(parameter) || !DiceValues.IsFace(secondaryParameter)) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
            SecondaryParameter = secondaryParameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.CountOf(Parameter) == 0 && context.CurrentHand.CountOf(SecondaryParameter) == 0;
        }
    }
}
