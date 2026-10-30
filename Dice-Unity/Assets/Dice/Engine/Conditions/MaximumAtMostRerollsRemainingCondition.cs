namespace Dice.Engine
{
    public sealed class MaximumAtMostRerollsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.Maximum(dice) <= context.RerollsRemaining;
        }
    }
}
