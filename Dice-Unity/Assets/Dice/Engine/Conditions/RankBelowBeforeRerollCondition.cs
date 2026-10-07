namespace Dice.Engine
{
    public sealed class RankBelowBeforeRerollCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.CompareRank(dice, context.BeforeRerollDice, -1);
        }
    }
}
