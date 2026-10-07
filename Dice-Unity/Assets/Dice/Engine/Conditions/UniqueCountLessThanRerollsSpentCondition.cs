namespace Dice.Engine
{
    public sealed class UniqueCountLessThanRerollsSpentCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.UniqueCount(dice) < context.RerollsSpentOnHand;
        }
    }
}
