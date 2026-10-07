namespace Dice.Engine
{
    public sealed class InitialRankAbovePreviousCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return ModifierConditionHelpers.CompareRank(context.InitialDice, context.PreviousPlayedDice, 1);
        }
    }
}
