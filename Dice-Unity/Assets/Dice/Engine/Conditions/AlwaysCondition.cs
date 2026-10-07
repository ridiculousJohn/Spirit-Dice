namespace Dice.Engine
{
    public sealed class AlwaysCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return true;
        }
    }
}
