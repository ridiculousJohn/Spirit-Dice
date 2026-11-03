namespace Dice.Engine
{
    public sealed class SelectedFaceUnchangedCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            if (!context.BeforeRerollHand.HasValue) return false;
            HandDice before = context.BeforeRerollHand.Value;
            // A selected die that did not count before or after the reroll has no face to compare.
            if (!context.CurrentHand.Counts(context.SelectedMask) || !before.Counts(context.SelectedMask)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((context.SelectedMask & (1 << i)) != 0 && dice[i] == before.Faces[i]) return true;
            return false;
        }
    }
}
