namespace Dice.Engine
{
    public sealed class LargestGroupEqualsHandsOrRerollsCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            int largestGroup = ModifierConditionHelpers.LargestGroup(context);
            return largestGroup == context.HandsRemaining || largestGroup == context.RerollsRemaining;
        }
    }
}
