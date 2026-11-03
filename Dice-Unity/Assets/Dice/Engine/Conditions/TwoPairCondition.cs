namespace Dice.Engine
{
    public sealed class TwoPairCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return HandEvaluator.Evaluate(context.CurrentHand) == HandCategory.TwoPair;
        }
    }
}
