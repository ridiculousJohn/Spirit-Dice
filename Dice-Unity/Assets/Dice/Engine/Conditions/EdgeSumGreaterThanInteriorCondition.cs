namespace Dice.Engine
{
    public sealed class EdgeSumGreaterThanInteriorCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            if (!context.CurrentHand.Counts(DiceValues.AllMask)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            return (dice[0] + dice[4]) > dice[1] + dice[2] + dice[3];
        }
    }
}
