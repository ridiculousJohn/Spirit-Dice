namespace Dice.Engine
{
    public sealed class LargestGroupEqualsHandsOrRerollsCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            int largestGroup = ModifierConditionHelpers.LargestGroup(dice);
            return largestGroup == context.HandsRemaining || largestGroup == context.RerollsRemaining;
        }
    }
}
