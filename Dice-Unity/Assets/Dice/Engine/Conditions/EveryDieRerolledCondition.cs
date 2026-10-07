namespace Dice.Engine
{
    public sealed class EveryDieRerolledCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.EverRerolledMask == DiceValues.AllMask;
        }
    }
}
