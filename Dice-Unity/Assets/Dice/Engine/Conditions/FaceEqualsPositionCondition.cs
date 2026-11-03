namespace Dice.Engine
{
    public sealed class FaceEqualsPositionCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            if (!context.CurrentHand.Counts(DiceValues.AllMask)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            for (int i = 0; i < DiceValues.Count; ++i) if (dice[i] == i + 1) return true;
            return false;
        }
    }
}
