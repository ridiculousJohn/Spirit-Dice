namespace Dice.Engine
{
    public sealed class ContainsRerollsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return DiceValues.IsFace(context.RerollsRemaining) && ModifierConditionHelpers.CountOf(dice, context.RerollsRemaining) > 0;
        }
    }
}
