namespace Dice.Engine
{
    public sealed class SameParityCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            int oddCount = ModifierConditionHelpers.OddCount(context);
            return oddCount == 0 || oddCount == context.CurrentHand.CountedCount;
        }
    }
}
