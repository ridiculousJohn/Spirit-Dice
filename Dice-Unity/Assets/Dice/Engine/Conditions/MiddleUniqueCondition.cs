namespace Dice.Engine
{
    public sealed class MiddleUniqueCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.CountOf(dice, dice[2]) == 1;
        }
    }
}
