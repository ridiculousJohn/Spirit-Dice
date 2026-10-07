namespace Dice.Engine
{
    public sealed class MinimumEqualsRerollsSpentCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.Minimum(dice) == context.RerollsSpentOnHand;
        }
    }
}
