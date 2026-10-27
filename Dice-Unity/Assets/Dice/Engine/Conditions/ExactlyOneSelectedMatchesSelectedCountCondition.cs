namespace Dice.Engine
{
    public sealed class ExactlyOneSelectedMatchesSelectedCountCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.SelectedCountOf(dice, context.SelectedMask, DiceValues.CountBays(context.SelectedMask)) == 1;
        }
    }
}
