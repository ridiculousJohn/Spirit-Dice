namespace Dice.Engine
{
    public sealed class RankEqualsBeforeRerollCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.CompareRank(dice, context.BeforeRerollDice, 0);
        }
    }
}
