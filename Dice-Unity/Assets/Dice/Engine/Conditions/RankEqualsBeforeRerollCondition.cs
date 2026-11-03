namespace Dice.Engine
{
    public sealed class RankEqualsBeforeRerollCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return ModifierConditionHelpers.CompareRank(context.CurrentHand, context.BeforeRerollHand, 0);
        }
    }
}
