namespace Dice.Engine
{
    public sealed class RankAbovePreviousCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.CompareRank(dice, context.PreviousPlayedDice, 1, true);
        }
    }
}
