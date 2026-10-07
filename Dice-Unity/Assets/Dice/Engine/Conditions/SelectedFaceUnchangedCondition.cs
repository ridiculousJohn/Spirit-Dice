namespace Dice.Engine
{
    public sealed class SelectedFaceUnchangedCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            if (!context.BeforeRerollDice.HasValue) return false;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((context.SelectedMask & (1 << i)) != 0 && dice[i] == context.BeforeRerollDice.Value[i]) return true;
            return false;
        }
    }
}
