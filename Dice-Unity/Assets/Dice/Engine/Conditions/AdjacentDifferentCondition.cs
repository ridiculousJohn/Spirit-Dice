namespace Dice.Engine
{
    public sealed class AdjacentDifferentCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return ModifierConditionHelpers.AdjacentDifferences(context, 1, DiceValues.MaximumFaceDifference);
        }
    }
}
