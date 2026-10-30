namespace Dice.Engine
{
    public sealed class ExtremeEqualsHandsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.Minimum(dice) == context.HandsRemaining || ModifierConditionHelpers.Maximum(dice) == context.HandsRemaining;
        }
    }
}
