using System;

namespace Dice.Engine
{
    public sealed class ThreeSmallestSumAtMostMaximumCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentHand.Faces;
            // Without three counted dice, the hand has no three smallest.
            if (context.CurrentHand.CountedCount < 3) return false;
            // Each smallest starts above every face and the maximum at the lowest face.
            int first = DiceValues.MaximumFace + 1, second = first, third = first, maximum = DiceValues.MinimumFace;
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                if (!context.CurrentHand.Counts(1 << i)) continue;
                int face = dice[i];
                maximum = Math.Max(maximum, face);
                if (face < first) { third = second; second = first; first = face; }
                else if (face < second) { third = second; second = face; }
                else if (face < third) third = face;
            }
            return first + second + third <= maximum;
        }
    }
}
