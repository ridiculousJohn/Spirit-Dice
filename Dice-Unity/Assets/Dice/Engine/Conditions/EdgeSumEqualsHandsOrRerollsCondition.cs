namespace Dice.Engine
{
    public sealed class EdgeSumEqualsHandsOrRerollsCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return (dice[0] + dice[4]) == (context.HandsRemaining + 1L) || (dice[0] + dice[4]) == context.RerollsRemaining;
        }
    }
}
