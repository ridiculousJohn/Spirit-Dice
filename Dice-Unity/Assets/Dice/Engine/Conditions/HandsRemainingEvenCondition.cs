namespace Dice.Engine
{
    public sealed class HandsRemainingEvenCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return ((context.HandsRemaining + 1L) & 1) == 0;
        }
    }
}
