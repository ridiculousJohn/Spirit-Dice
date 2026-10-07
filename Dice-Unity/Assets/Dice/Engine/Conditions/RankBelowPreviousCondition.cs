namespace Dice.Engine
{
    public sealed class RankBelowPreviousCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.CompareRank(dice, context.PreviousPlayedDice, -1, true);
        }
    }
}
