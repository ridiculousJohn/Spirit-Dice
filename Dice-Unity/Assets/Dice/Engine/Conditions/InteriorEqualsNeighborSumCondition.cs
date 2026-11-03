namespace Dice.Engine
{
    public sealed class InteriorEqualsNeighborSumCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            if (!context.CurrentHand.Counts(DiceValues.AllMask)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            for (int i = 1; i < DiceValues.Count - 1; ++i)
                if (dice[i] == dice[i - 1] + dice[i + 1]) return true;
            return false;
        }
    }
}
