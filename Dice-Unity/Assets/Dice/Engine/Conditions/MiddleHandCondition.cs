namespace Dice.Engine
{
    public sealed class MiddleHandCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.HandsRemaining == (context.StartingHands + 1L) / 2;
        }
    }
}
