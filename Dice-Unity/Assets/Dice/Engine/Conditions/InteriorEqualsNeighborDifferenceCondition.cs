using System;

namespace Dice.Engine
{
    public sealed class InteriorEqualsNeighborDifferenceCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            for (int i = 1; i < DiceValues.Count - 1; ++i)
                if (dice[i] == Math.Abs(dice[i - 1] - dice[i + 1])) return true;
            return false;
        }
    }
}
