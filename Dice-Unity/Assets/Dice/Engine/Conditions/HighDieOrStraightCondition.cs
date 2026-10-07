namespace Dice.Engine
{
    public sealed class HighDieOrStraightCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            var category = HandEvaluator.Evaluate(dice);
            return category == HandCategory.HighDie || category == HandCategory.Straight;
        }
    }
}
