namespace Dice.Engine
{
    public sealed class InteriorPairMatchesEdgeCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            int firstEdgeCount = 0, lastEdgeCount = 0;
            for (int i = 1; i < DiceValues.Count - 1; ++i)
            { if (dice[i] == dice[0]) ++firstEdgeCount; if (dice[i] == dice[4]) ++lastEdgeCount; }
            return firstEdgeCount >= 2 || lastEdgeCount >= 2;
        }
    }
}
