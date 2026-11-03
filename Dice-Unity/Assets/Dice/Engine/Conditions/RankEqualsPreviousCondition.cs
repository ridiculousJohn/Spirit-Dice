namespace Dice.Engine
{
    public sealed class RankEqualsPreviousCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return ModifierConditionHelpers.CompareRank(context.CurrentHand, context.PreviousPlayedHand, 0, true);
        }
    }
}
