namespace Dice.Engine
{
    public sealed class ExtremeEqualsHandsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.Minimum(dice) == (context.HandsRemaining + 1L) || ModifierConditionHelpers.Maximum(dice) == (context.HandsRemaining + 1L);
        }
    }
}
