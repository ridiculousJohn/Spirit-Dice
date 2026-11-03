namespace Dice.Engine
{
    public sealed class EdgeSumEqualsHandsOrRerollsCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            if (!context.CurrentHand.Counts(ModifierConditionHelpers.EdgeDice)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            return (dice[0] + dice[4]) == context.HandsRemaining || (dice[0] + dice[4]) == context.RerollsRemaining;
        }
    }
}
