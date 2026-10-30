namespace Dice.Engine
{
    public sealed class RankEqualsPreviousCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.CompareRank(dice, context.PreviousPlayedDice, 0, true);
        }
    }
}
