namespace Dice.Engine
{
    public sealed class ContainsRerollsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return context.RerollsRemaining >= 1 && context.RerollsRemaining <= 6 && ModifierConditionHelpers.CountOf(dice, context.RerollsRemaining) > 0;
        }
    }
}
