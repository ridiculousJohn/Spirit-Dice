using System;

namespace Dice.Engine
{
    public sealed class EdgePairDifferencesEqualCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return Math.Abs(dice[0] - dice[1]) == Math.Abs(dice[3] - dice[4]);
        }
    }
}
