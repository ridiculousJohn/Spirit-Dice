namespace Dice.Engine
{
    public sealed class OddSumGreaterThanEvenSumCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            int oddSum = ModifierConditionHelpers.OddSum(dice);
            return oddSum > dice.Sum - oddSum;
        }
    }
}
