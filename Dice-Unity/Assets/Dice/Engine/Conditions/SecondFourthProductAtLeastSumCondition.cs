namespace Dice.Engine
{
    public sealed class SecondFourthProductAtLeastSumCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return dice[1] * dice[3] >= dice.Sum;
        }
    }
}
