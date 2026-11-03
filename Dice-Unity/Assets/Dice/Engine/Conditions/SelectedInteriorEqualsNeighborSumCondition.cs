namespace Dice.Engine
{
    public sealed class SelectedInteriorEqualsNeighborSumCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentHand.Faces;
            // Each selected interior die reads itself and both neighbours.
            int read = 0;
            for (int i = 1; i < DiceValues.Count - 1; ++i)
                if ((context.SelectedMask & (1 << i)) != 0) read |= 7 << (i - 1);
            if (!context.CurrentHand.Counts(read)) return false;
            for (int i = 1; i < DiceValues.Count - 1; ++i)
                if ((context.SelectedMask & (1 << i)) != 0 && dice[i] == dice[i - 1] + dice[i + 1]) return true;
            return false;
        }
    }
}
