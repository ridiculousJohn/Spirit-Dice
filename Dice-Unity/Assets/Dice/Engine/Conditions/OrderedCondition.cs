namespace Dice.Engine
{
    public sealed class OrderedCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            if (!context.CurrentHand.Counts(DiceValues.AllMask)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            bool ascending = true, descending = true;
            for (int i = 1; i < DiceValues.Count; ++i)
            { ascending &= dice[i - 1] <= dice[i]; descending &= dice[i - 1] >= dice[i]; }
            return ascending || descending;
        }
    }
}
