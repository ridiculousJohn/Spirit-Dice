namespace Dice.Engine
{
    public sealed class LastHandCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.HandsRemaining == 1;
        }
    }
}
