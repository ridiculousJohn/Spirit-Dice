namespace Dice.Engine
{
    public sealed class RankAbovePreviousCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return ModifierConditionHelpers.CompareRank(context.CurrentHand, context.PreviousPlayedHand, 1, true);
        }
    }
}
