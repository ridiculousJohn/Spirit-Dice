namespace Dice.Engine
{
    public sealed class InitialRankAbovePreviousCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return ModifierConditionHelpers.CompareRank(context.InitialHand, context.PreviousPlayedHand, 1);
        }
    }
}
