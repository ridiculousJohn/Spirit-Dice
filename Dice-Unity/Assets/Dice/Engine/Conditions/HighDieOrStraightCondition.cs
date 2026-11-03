namespace Dice.Engine
{
    public sealed class HighDieOrStraightCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            var category = HandEvaluator.Evaluate(context.CurrentHand);
            return category == HandCategory.HighDie || category == HandCategory.Straight;
        }
    }
}
