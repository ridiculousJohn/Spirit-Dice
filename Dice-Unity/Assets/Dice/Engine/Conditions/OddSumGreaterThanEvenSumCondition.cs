namespace Dice.Engine
{
    public sealed class OddSumGreaterThanEvenSumCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            int oddSum = ModifierConditionHelpers.OddSum(context);
            return oddSum > context.CurrentHand.Sum - oddSum;
        }
    }
}
