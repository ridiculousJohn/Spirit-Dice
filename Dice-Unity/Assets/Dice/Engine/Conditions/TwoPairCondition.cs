namespace Dice.Engine
{
    public sealed class TwoPairCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return HandEvaluator.Evaluate(dice) == HandCategory.TwoPair;
        }
    }
}
