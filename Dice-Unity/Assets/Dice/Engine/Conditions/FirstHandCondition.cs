namespace Dice.Engine
{
    public sealed class FirstHandCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.HandsPlayedInStage == 0;
        }
    }
}
