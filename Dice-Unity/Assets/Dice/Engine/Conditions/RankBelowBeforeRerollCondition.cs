namespace Dice.Engine
{
    public sealed class RankBelowBeforeRerollCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return ModifierConditionHelpers.CompareRank(context.CurrentHand, context.BeforeRerollHand, -1);
        }
    }
}
