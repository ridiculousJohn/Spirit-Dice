namespace Dice.Engine
{
    public sealed class SameParityCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            int oddCount = ModifierConditionHelpers.OddCount(dice);
            return oddCount == 0 || oddCount == DiceValues.Count;
        }
    }
}
