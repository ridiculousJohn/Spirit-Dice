namespace Dice.Engine
{
    public sealed class FaceEqualsPositionCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            for (int i = 0; i < DiceValues.Count; ++i) if (dice[i] == i + 1) return true;
            return false;
        }
    }
}
