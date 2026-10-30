namespace Dice.Engine
{
    public sealed class AdjacentDifferentCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.AdjacentDifferences(dice, 1, DiceValues.MaximumFaceDifference);
        }
    }
}
