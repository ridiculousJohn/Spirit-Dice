namespace Dice.Engine
{
    public sealed class RerollsExceedHandsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.RerollsRemaining > (context.HandsRemaining + 1L);
        }
    }
}
