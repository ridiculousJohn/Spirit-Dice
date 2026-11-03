namespace Dice.Engine
{
    public sealed class RankBelowPreviousCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return ModifierConditionHelpers.CompareRank(context.CurrentHand, context.PreviousPlayedHand, -1, true);
        }
    }
}
