namespace Dice.Engine
{
    public sealed class SelectedInteriorEqualsNeighborSumCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            for (int i = 1; i < DiceValues.Count - 1; ++i)
                if ((context.SelectedMask & (1 << i)) != 0 && dice[i] == dice[i - 1] + dice[i + 1]) return true;
            return false;
        }
    }
}
