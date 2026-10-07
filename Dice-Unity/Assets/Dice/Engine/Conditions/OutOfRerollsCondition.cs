namespace Dice.Engine
{
    public sealed class OutOfRerollsCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.RerollsRemaining == 0;
        }
    }
}
