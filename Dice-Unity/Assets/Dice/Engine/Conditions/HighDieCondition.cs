namespace Dice.Engine
{
    public sealed class HighDieCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return HandEvaluator.Evaluate(context.CurrentHand) == HandCategory.HighDie;
        }
    }
}
