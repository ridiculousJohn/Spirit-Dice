namespace Dice.Engine
{
    public sealed class AlternatingParityCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            for (int i = 1; i < DiceValues.Count; ++i) if ((dice[i - 1] & 1) == (dice[i] & 1)) return false;
            return true;
        }
    }
}
