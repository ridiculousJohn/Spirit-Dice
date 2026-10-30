namespace Dice.Engine
{
    public sealed class MiddleEqualsOddCountCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return dice[2] == ModifierConditionHelpers.OddCount(dice);
        }
    }
}
