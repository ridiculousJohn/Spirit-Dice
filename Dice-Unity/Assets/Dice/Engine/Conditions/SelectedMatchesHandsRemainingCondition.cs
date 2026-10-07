namespace Dice.Engine
{
    public sealed class SelectedMatchesHandsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.SelectedCountOf(dice, context.SelectedMask, context.HandsRemaining) > 0;
        }
    }
}
